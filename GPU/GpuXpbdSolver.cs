using System.Collections.Generic;
using UnityEngine;

public class GpuXpbdSolver : MonoBehaviour
{
    [Header("Simulation")]
    public int substeps = 10;
    [Range(0.5f, 2.5f)] public float sorOmega = 1.6f;

    public float compliance = 0.0f;
    public ComputeShader compute;

    [Header("Grid / Init")]
    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;
    public float height = 10f;

    [Header("Physics")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Header("Sim Radius")]
    // Nur für Physik (kann vom visuellen Size abweichen)
    public float particleRadiusSim = 0.025f;

    const int THREADS = 256;
    int particleCount, constraintCount;

    // Kernel IDs
    int kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kUpdateVelocities;

    // --- Buffers (public readonly Properties für Renderer) ---
    public ComputeBuffer ParticleBuffer { get; private set; }
    public ComputeBuffer ConstraintBuffer { get; private set; }
    public ComputeBuffer DeltaXBuffer { get; private set; }
    public ComputeBuffer DeltaYBuffer { get; private set; }
    public ComputeBuffer DeltaZBuffer { get; private set; }
    public ComputeBuffer CountBuffer { get; private set; }

    public int ParticleCount => particleCount;
    public int ConstraintCount => constraintCount;

    // Für Renderer nützlich: (z.B. für Bounds)
    public Vector3 WorldCenter => transform.position;
    public Vector2 GridSize => new Vector2(width, height);

    void Start()
    {
        // --- Init Particles & Constraints ---
        particleCount = numParticlesX * numParticlesY;
        var particles = new GpuParticle[particleCount];

        float dx = (numParticlesX > 1) ? width / (numParticlesX - 1) : 0f;
        float dy = (numParticlesY > 1) ? height / (numParticlesY - 1) : 0f;
        float offsetX = (numParticlesX > 1) ? width * 0.5f : 0f;
        float offsetY = (numParticlesY > 1) ? height * 0.5f : 0f;

        int idx = 0;
        for (int j = 0; j < numParticlesY; j++)
            for (int i = 0; i < numParticlesX; i++)
            {
                Vector3 localPos = new Vector3(i * dx - offsetX, j * dy - offsetY, 0f);
                Vector3 worldPos = transform.TransformPoint(localPos);
                particles[idx++] = new GpuParticle(worldPos, 1f, particleRadiusSim);
            }

        // Pin top row (zero mass)
        for (int x = 0; x < numParticlesX; x++)
        {
            int idTop = (numParticlesY - 1) * numParticlesX + x;
            var p = particles[idTop]; p.m = 0f; p.w = 0f; particles[idTop] = p;
        }

        var cons = new List<GpuDistanceConstraint>();
        for (int y = 0; y < numParticlesY; y++)
            for (int x = 0; x < numParticlesX; x++)
            {
                int i = y * numParticlesX + x;
                if (x + 1 < numParticlesX) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 1), dx, compliance));
                if (y + 1 < numParticlesY) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + numParticlesX), dy, compliance));
            }
        constraintCount = cons.Count;

        // --- GPU buffers ---
        ReleaseAll();

        int particleStride = 12 * sizeof(float); // float3*3 + float*3
        ParticleBuffer = new ComputeBuffer(particleCount, particleStride, ComputeBufferType.Structured);
        ParticleBuffer.SetData(particles);

        int conStride = sizeof(uint) * 2 + sizeof(float) * 2;
        ConstraintBuffer = new ComputeBuffer(constraintCount, conStride, ComputeBufferType.Structured);
        ConstraintBuffer.SetData(cons);

        DeltaXBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        DeltaYBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        DeltaZBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        CountBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        ZeroAccumulators();

        // --- Kernels & Bindings ---
        kIntegrate = compute.FindKernel("Integrate");
        kSolveDistanceJacobi = compute.FindKernel("SolveDistanceJacobi");
        kApplyDeltas = compute.FindKernel("ApplyDeltas");
        kUpdateVelocities = compute.FindKernel("UpdateVelocities");

        foreach (int k in new[] { kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kUpdateVelocities })
            compute.SetBuffer(k, "particles", ParticleBuffer);

        compute.SetBuffer(kSolveDistanceJacobi, "constraints", ConstraintBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "deltaX", DeltaXBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "deltaY", DeltaYBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "deltaZ", DeltaZBuffer);
        compute.SetBuffer(kSolveDistanceJacobi, "countBuf", CountBuffer);

        compute.SetBuffer(kApplyDeltas, "deltaX", DeltaXBuffer);
        compute.SetBuffer(kApplyDeltas, "deltaY", DeltaYBuffer);
        compute.SetBuffer(kApplyDeltas, "deltaZ", DeltaZBuffer);
        compute.SetBuffer(kApplyDeltas, "countBuf", CountBuffer);

        compute.SetInt("particleCount", particleCount);
        compute.SetInt("constraintCount", constraintCount);
    }

    void FixedUpdate()
    {
        if (compute == null || ParticleBuffer == null) return;

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
            compute.Dispatch(kUpdateVelocities, groupsP, 1, 1);
        }
    }

    void OnDestroy() => ReleaseAll();

    void ZeroAccumulators()
    {
        var zeros = new uint[particleCount];
        DeltaXBuffer.SetData(zeros);
        DeltaYBuffer.SetData(zeros);
        DeltaZBuffer.SetData(zeros);
        CountBuffer.SetData(zeros);
    }

    void ReleaseAll()
    {
        if (ParticleBuffer != null) { ParticleBuffer.Release(); ParticleBuffer = null; }
        if (ConstraintBuffer != null) { ConstraintBuffer.Release(); ConstraintBuffer = null; }
        if (DeltaXBuffer != null) { DeltaXBuffer.Release(); DeltaXBuffer = null; }
        if (DeltaYBuffer != null) { DeltaYBuffer.Release(); DeltaYBuffer = null; }
        if (DeltaZBuffer != null) { DeltaZBuffer.Release(); DeltaZBuffer = null; }
        if (CountBuffer != null) { CountBuffer.Release(); CountBuffer = null; }
    }
}
