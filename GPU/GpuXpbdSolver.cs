using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


public class GpuXpbdSolver : MonoBehaviour
{

    [Header("Simulation")]
    private float dt;
    public int substeps = 10;
    [Range(0.5f, 2.5f)] public float sorOmega = 1.6f;
    private float dts;
    private float dts2;

    public float compliance = 0.0f;

    public ComputeShader compute;

    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;
    public float height = 10f;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    public Vector3 boundsMin = new Vector3(-5f, -5f, -5f);
    public Vector3 boundsMax = new Vector3(5f, 5f, 5f);

    [Header("Rendering")]
    public Mesh quadMesh;
    public Material particleMaterial;
    public float particleSize = 0.05f;
    public Color particleColor = Color.blue;

    const int THREADS = 256;

    int particleCount, constraintCount;
    int kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kSolveBounds, kUpdateVelocities;


    struct GpuParticle
    {
        public Vector3 positionP; // previous
        public Vector3 positionX; // current/predicted
        public Vector3 velocity;
        public float m;
        public float w;
        public float radius;

        public GpuParticle(Vector3 pos, float mass, float rad)
        {
            positionP = pos;
            positionX = pos;
            velocity = Vector3.zero;
            m = mass;
            w = (mass == 0f) ? 0f : 1f / mass;
            radius = rad;
        }
    }

    struct GpuDistanceConstraint
    {
        public uint i, j;
        public float rest;
        public float compliance;

        public GpuDistanceConstraint(uint a, uint b, float r, float c)
        { i = a; j = b; rest = r; compliance = c; }
    }



    // Buffers
    ComputeBuffer particleBuffer;
    ComputeBuffer argsBuffer;
    ComputeBuffer constraintBuffer;   // DistanceConstraint[]
    ComputeBuffer deltaXBuffer;       // uint[]   (atomare Float-Adds per Komponente)
    ComputeBuffer deltaYBuffer;       // uint[]
    ComputeBuffer deltaZBuffer;       // uint[]
    ComputeBuffer countBuffer;        // uint[]

    readonly uint[] args = new uint[5];
    Bounds drawBounds;

    void Start()
    {
        //-----------------------------------------------------------
        // --- Initialize Particles & Constraints
        //-----------------------------------------------------------

        particleCount = numParticlesX * numParticlesY;
        var particles = new GpuParticle[particleCount];

        float dx = (numParticlesX > 1) ? width / (numParticlesX - 1) : 0f;
        float dy = (numParticlesY > 1) ? height / (numParticlesY - 1) : 0f;
        float offsetX = (numParticlesX > 1) ? width * 0.5f : 0f;
        float offsetY = (numParticlesY > 1) ? height * 0.5f : 0f;

        int idx = 0;
        for (int j = 0; j < numParticlesY; j++)
        {
            for (int i = 0; i < numParticlesX; i++)
            {
                Vector3 localPos = new Vector3(i * dx - offsetX, j * dy - offsetY, 0f);
                Vector3 worldPos = transform.TransformPoint(localPos);
                particles[idx++] = new GpuParticle(worldPos, 1f, particleSize * 0.5f);
            }
        }

        // Pin top row (zero mass)
        for (int x = 0; x < numParticlesX; x++)
        {
            int idTop = (numParticlesY - 1) * numParticlesX + x;
            var p = particles[idTop]; p.m = 0f; p.w = 0f; particles[idTop] = p;
        }

        // Create distance constraints
        var cons = new List<GpuDistanceConstraint>();
        for (int y = 0; y < numParticlesY; y++)
            for (int x = 0; x < numParticlesX; x++)
            {
                int i = y * numParticlesX + x;
                if (x + 1 < numParticlesX) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 1), dx, compliance));
                if (y + 1 < numParticlesY) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + numParticlesX), dy, compliance));
            }
        constraintCount = cons.Count;


        // --- GPU buffer
        ReleaseAll();

        int stride = 12 * sizeof(float); // 3*float3 + 3*float = 48 bytes
        particleBuffer = new ComputeBuffer(particleCount, stride, ComputeBufferType.Structured);
        particleBuffer.SetData(particles);

        int conStride = sizeof(uint) * 2 + sizeof(float) * 2;
        constraintBuffer = new ComputeBuffer(constraintCount, conStride, ComputeBufferType.Structured);
        constraintBuffer.SetData(cons);

        deltaXBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        deltaYBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        deltaZBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        countBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        ZeroAccumulators();


        // --- Compute kernels & bindings
        kIntegrate = compute.FindKernel("Integrate");
        kSolveDistanceJacobi = compute.FindKernel("SolveDistanceJacobi");
        kApplyDeltas = compute.FindKernel("ApplyDeltas");
        kSolveBounds = compute.FindKernel("SolveBounds");
        kUpdateVelocities = compute.FindKernel("UpdateVelocities");

        Debug.Log($"kernels: {kIntegrate},{kSolveDistanceJacobi},{kApplyDeltas},{kSolveBounds},{kUpdateVelocities}");


        // Shared particle buffer
        foreach (int k in new[] { kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kSolveBounds, kUpdateVelocities })
            compute.SetBuffer(k, "particles", particleBuffer);

        // Constraint & accumulation bindings
        compute.SetBuffer(kSolveDistanceJacobi, "constraints", constraintBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "deltaX", deltaXBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "deltaY", deltaYBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "deltaZ", deltaZBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "countBuf", countBuffer);

        compute.SetBuffer(kApplyDeltas, "deltaX", deltaXBuffer);
        compute.SetBuffer(kApplyDeltas, "deltaY", deltaYBuffer);
        compute.SetBuffer(kApplyDeltas, "deltaZ", deltaZBuffer);
        compute.SetBuffer(kApplyDeltas, "countBuf", countBuffer);

        // Static params
        compute.SetVector("boundsMin", boundsMin);
        compute.SetVector("boundsMax", boundsMax);
        compute.SetInt("particleCount", particleCount);
        compute.SetInt("constraintCount", constraintCount);


        // --- Rendering
        if (quadMesh == null)
        {
            // Built-in mesh name works in recent Unity versions
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        }
        if (particleMaterial == null)
        {
            Debug.LogError("Missing particle material.");
            enabled = false;
            return;
        }

        particleMaterial.EnableKeyword("UNITY_PROCEDURAL_INSTANCING_ENABLED");
        particleMaterial.SetBuffer("_ParticleBuffer", particleBuffer);
        particleMaterial.SetFloat("_Size", particleSize);
        particleMaterial.SetColor("_BaseColor", particleColor);

        // Indirect args: [indexCount, instanceCount, startIndex, baseVertex, startInstance]
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        args[0] = quadMesh != null ? (uint)quadMesh.GetIndexCount(0) : 0u;
        args[1] = (uint)particleCount;
        args[2] = quadMesh != null ? (uint)quadMesh.GetIndexStart(0) : 0u;
        args[3] = quadMesh != null ? (uint)quadMesh.GetBaseVertex(0) : 0u;
        args[4] = 0u;
        argsBuffer.SetData(args);

        var center = (boundsMin + boundsMax) * 0.5f;
        var size = (boundsMax - boundsMin) + Vector3.one * (particleSize * 4f);
        drawBounds = new Bounds(center, size * 2f);
    }

    void Update()
    {
        if (quadMesh == null || particleMaterial == null || argsBuffer == null) return;

        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 0, particleMaterial, drawBounds, argsBuffer,
            0, null, ShadowCastingMode.Off, receiveShadows: false, layer: gameObject.layer
        );
    }

    void FixedUpdate()
    {
        if (compute == null || particleBuffer == null) return;

        float dt = Time.fixedDeltaTime;
        float dts = dt / substeps;
        float dts2 = dts * dts;

        compute.SetFloat("dt", dt);
        compute.SetFloat("dts", dts);
        compute.SetFloat("dts2", dts2);
        compute.SetVector("gravity", gravity);
        compute.SetFloat("omega", sorOmega);

        int groupsP = Mathf.CeilToInt(particleCount / (float)THREADS);
        int groupsC = Mathf.CeilToInt(constraintCount / (float)THREADS);

        for (int s = 0; s < substeps; s++)
        {
            compute.Dispatch(kIntegrate, groupsP, 1, 1);

            compute.Dispatch(kSolveDistanceJacobi, groupsC, 1, 1);
            compute.Dispatch(kApplyDeltas, groupsP, 1, 1);
           
            compute.Dispatch(kSolveBounds, groupsP, 1, 1);
            compute.Dispatch(kUpdateVelocities, groupsP, 1, 1);
        }

    }

    void OnDestroy() => ReleaseAll();


    void ZeroAccumulators()
    {
        var zeros = new uint[particleCount];
        deltaXBuffer.SetData(zeros);
        deltaYBuffer.SetData(zeros);
        deltaZBuffer.SetData(zeros);
        countBuffer.SetData(zeros);
    }

    void ReleaseAll()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (constraintBuffer != null) { constraintBuffer.Release(); constraintBuffer = null; }
        if (deltaXBuffer != null) { deltaXBuffer.Release(); deltaXBuffer = null; }
        if (deltaYBuffer != null) { deltaYBuffer.Release(); deltaYBuffer = null; }
        if (deltaZBuffer != null) { deltaZBuffer.Release(); deltaZBuffer = null; }
        if (countBuffer != null) { countBuffer.Release(); countBuffer = null; }
        if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
    }
}
