using System.Collections.Generic;
using UnityEngine;

public class GpuXpbdSolver : MonoBehaviour
{
    [Header("Simulation")]
    public int substeps = 10;
    [Range(0.5f, 2.5f)] public float sorOmega = 1.5f;
    public ComputeShader compute;
    public float compliance = 0.0f;

    [Header("Physics")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float particleRadiusSim = 0.025f;

    const int THREADS = 256;
    int particleCount, constraintCount;

    int kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kUpdateVelocities;

    public ComputeBuffer ParticleBuffer { get; private set; }
    public ComputeBuffer ConstraintBuffer { get; private set; }
    public ComputeBuffer DeltaXBuffer { get; private set; }
    public ComputeBuffer DeltaYBuffer { get; private set; }
    public ComputeBuffer DeltaZBuffer { get; private set; }
    public ComputeBuffer CountBuffer { get; private set; }

    public int ParticleCount => particleCount;
    public int ConstraintCount => constraintCount;

    List<GpuParticle> allParticlesList = new List<GpuParticle>();
    List<GpuDistanceConstraint> allConstraintsList = new List<GpuDistanceConstraint>();

    [System.Serializable]
    public struct ClothRange { public GpuCloth cloth; public int start; public int count; }
    public List<ClothRange> clothRanges = new List<ClothRange>();


    // Initialization
    void Start()
    {
        if (compute == null)
        {
            Debug.LogError("[GpuXpbdSolver] ComputeShader is missing");
            enabled = false; return;
        }

        kIntegrate = compute.FindKernel("Integrate");
        kSolveDistanceJacobi = compute.FindKernel("SolveDistanceJacobi");
        kApplyDeltas = compute.FindKernel("ApplyDeltas");
        kUpdateVelocities = compute.FindKernel("UpdateVelocities");

        RegisterAllCloths();
        InitializeBuffers();
    }

    // Buffer setup
    void InitializeBuffers()
    {
        ReleaseAll();

        var allParticles = allParticlesList.ToArray();
        var allConstraints = allConstraintsList.ToArray();

        particleCount = allParticles.Length;
        constraintCount = allConstraints.Length;

        if (particleCount == 0)
        {
            Debug.LogWarning("[GpuXpbdSolver] No particles to register.");
            return;
        }

        // GPU buffers
        int particleStride = 12 * sizeof(float); // float3*3 + float*3 = 48
        ParticleBuffer = new ComputeBuffer(particleCount, particleStride, ComputeBufferType.Structured);
        ParticleBuffer.SetData(allParticles);

        int conStride = sizeof(uint) * 2 + sizeof(float) * 2; // 16
        if (constraintCount > 0)
        {
            ConstraintBuffer = new ComputeBuffer(constraintCount, conStride, ComputeBufferType.Structured);
            ConstraintBuffer.SetData(allConstraints);
        }

        DeltaXBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        DeltaYBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        DeltaZBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        CountBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        ZeroAccumulators();

        // Bindings
        foreach (int k in new[] { kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kUpdateVelocities })
            compute.SetBuffer(k, "particles", ParticleBuffer);

        if (ConstraintBuffer != null)
        {
            compute.SetBuffer(kSolveDistanceJacobi, "constraints", ConstraintBuffer);
            compute.SetBuffer(kSolveDistanceJacobi, "deltaX", DeltaXBuffer);
            compute.SetBuffer(kSolveDistanceJacobi, "deltaY", DeltaYBuffer);
            compute.SetBuffer(kSolveDistanceJacobi, "deltaZ", DeltaZBuffer);
            compute.SetBuffer(kSolveDistanceJacobi, "countBuf", CountBuffer);
        }

        compute.SetBuffer(kApplyDeltas, "deltaX", DeltaXBuffer);
        compute.SetBuffer(kApplyDeltas, "deltaY", DeltaYBuffer);
        compute.SetBuffer(kApplyDeltas, "deltaZ", DeltaZBuffer);
        compute.SetBuffer(kApplyDeltas, "countBuf", CountBuffer);

        compute.SetInt("particleCount", particleCount);
        compute.SetInt("constraintCount", constraintCount);

        Debug.Log($"[GpuXpbdSolver] Registered: {particleCount} particles, {constraintCount} constraints (arrays).");
    }

    // Simulation
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
        int groupsC = Mathf.CeilToInt(Mathf.Max(1, constraintCount) / (float)THREADS);

        for (int s = 0; s < substeps; s++)
        {
            compute.Dispatch(kIntegrate, groupsP, 1, 1);
            if (constraintCount > 0)
            {
                compute.Dispatch(kSolveDistanceJacobi, groupsC, 1, 1);
                compute.Dispatch(kApplyDeltas, groupsP, 1, 1);
            }
            compute.Dispatch(kUpdateVelocities, groupsP, 1, 1);
        }
    }


    // Cleanup
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



    // Cloth registration
    public void RegisterAllCloths()
    {
        var cloths = FindObjectsByType<GpuCloth>(FindObjectsSortMode.None);
        var packages = new List<ClothData>(cloths.Length);
        clothRanges.Clear();

        int runningOffset = 0;
        foreach (var c in cloths)
        {
            ClothData data = c.Build(particleRadiusSim, compliance);
            packages.Add(data);
            clothRanges.Add(new ClothRange { cloth = c, start = runningOffset, count = data.particles.Length });
            runningOffset += data.particles.Length;
        }

        RegisterCloths(packages);
    }
    public void RegisterCloths(IEnumerable<ClothData> cloths)
    {
        ReleaseAll();

        int offset = 0;
        foreach (var cd in cloths)
        {
            allParticlesList.AddRange(cd.particles);

            var cons = cd.constraints;
            for (int k = 0; k < cons.Length; k++)
            {
                var c = cons[k];
                allConstraintsList.Add(new GpuDistanceConstraint(
                    (uint)(c.i + (uint)offset),
                    (uint)(c.j + (uint)offset),
                    c.rest,
                    c.compliance
                ));
            }

            offset += cd.particles.Length;
        }
    }
}
