using UnityEngine;
using UnityEngine.Rendering;


public class GpuXpbdSolver : MonoBehaviour
{

    [Header("Simulation")]
    public ComputeShader compute;
    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;
    public float height = 10f;
    public float gravity = 9.81f;

    public Vector3 boundsMin = new Vector3(-5f, -5f, -5f);
    public Vector3 boundsMax = new Vector3(5f, 5f, 5f);

    [Header("Rendering")]
    public Mesh quadMesh;
    public Material particleMaterial;
    public float particleSize = 0.05f;
    public Color particleColor = Color.blue;

    const int THREADS = 256;

    int particleCount;
    int kIntegrate, kSolveConstraints, kUpdateVelocities;

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


    ComputeBuffer particleBuffer; // StructuredBuffer<Particle>
    ComputeBuffer argsBuffer;     // Indirect draw args
    readonly uint[] args = new uint[5];
    Bounds drawBounds;

    void Start()
    {
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

        // --- GPU buffer
        if (particleBuffer != null) particleBuffer.Release();
        int stride = 12 * sizeof(float); // 3*float3 + 3*float = 48 bytes
        particleBuffer = new ComputeBuffer(particleCount, stride, ComputeBufferType.Structured);
        particleBuffer.SetData(particles);

        // --- Compute kernels & bindings
        kIntegrate = compute.FindKernel("Integrate");
        kSolveConstraints = compute.FindKernel("SolveConstraints");
        kUpdateVelocities = compute.FindKernel("UpdateVelocities");

        // Bind shared buffers to ALL kernels
        compute.SetBuffer(kIntegrate, "_Particles", particleBuffer);
        compute.SetBuffer(kSolveConstraints, "_Particles", particleBuffer);
        compute.SetBuffer(kUpdateVelocities, "_Particles", particleBuffer);

        // Static params
        compute.SetVector("_BoundsMin", boundsMin);
        compute.SetVector("_BoundsMax", boundsMax);
        compute.SetFloat("_Gravity", gravity);
        compute.SetInt("_ParticleCount", particleCount);




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
        compute.SetFloat("_DeltaTime", dt);

        int groups = Mathf.CeilToInt(particleCount / (float)THREADS);

        compute.Dispatch(kIntegrate, groups, 1, 1);
        compute.Dispatch(kSolveConstraints, groups, 1, 1); //TODO: Consraints iterations
        compute.Dispatch(kUpdateVelocities, groups, 1, 1);
    }




    void OnDestroy()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
    }
}
