using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GpuXpbdSolver : MonoBehaviour
{
    [Header("Simulation")]
    public int substeps = 10;
    [Range(0.5f, 2.5f)] public float sorOmega = 1.5f;
    public ComputeShader compute;
    public float compliance = 0.0f;
    public float boundsPadding = 1.0f;

    [Header("Physics")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float particleRadiusSim = 0.025f;

    const int THREADS = 256;
    int particleCount, constraintCount;
    public int ParticleCount => particleCount;
    public int ConstraintCount => constraintCount;

    // Solver Kernels
    int kPredict, kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kUpdateVelocities;

    // Collision Kernels
    int kBuildCollisionConstraints, kSolveCollisionConstraints;

    // Solver Buffers
    public ComputeBuffer ParticleBuffer { get; private set; }
    public ComputeBuffer ConstraintBuffer { get; private set; }
    public ComputeBuffer DeltaXBuffer { get; private set; }
    public ComputeBuffer DeltaYBuffer { get; private set; }
    public ComputeBuffer DeltaZBuffer { get; private set; }
    public ComputeBuffer CountBuffer { get; private set; }

    // Collision Buffers
    public ComputeBuffer SphereBuffer { get; private set; }
    public ComputeBuffer CollisionConstraintBuffer { get; private set; }

    // Scene
    List<GpuCloth> cloths = new List<GpuCloth>();
    readonly List<GpuParticle> allParticlesList = new List<GpuParticle>();
    readonly List<GpuDistanceConstraint> allConstraintsList = new List<GpuDistanceConstraint>();

    //Particle Subset
    private GpuParticle[] _tmpSubset;

    //Collision Detection
    private Collider[] _overlapBuffer = new Collider[64]; 
    private readonly HashSet<Collider> _tmpColliders = new HashSet<Collider>();
    private readonly List<GpuSphereCollider> _tmpSphereColliders = new List<GpuSphereCollider>();

    void Start()
    {
        if (compute == null)
        {
            Debug.LogError("[GpuXpbdSolver] ComputeShader is missing");
            enabled = false; return;
        }

        // Solver Kernels
        kPredict = compute.FindKernel("Predict");
        kIntegrate = compute.FindKernel("Integrate");
        kSolveDistanceJacobi = compute.FindKernel("SolveDistanceJacobi");
        kApplyDeltas = compute.FindKernel("ApplyDeltas");
        kUpdateVelocities = compute.FindKernel("UpdateVelocities");

        // Collision Kernels
        kBuildCollisionConstraints = compute.FindKernel("BuildCollisionConstraints");
        kSolveCollisionConstraints = compute.FindKernel("SolveCollisionConstraints");

        RegisterAllCloths();
        InitializeBuffers();
    }
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

        int particleStride = 15 * sizeof(float); // float3*4 + float*3
        ParticleBuffer = new ComputeBuffer(particleCount, particleStride, ComputeBufferType.Structured);
        ParticleBuffer.SetData(allParticles);


        int conStride = sizeof(uint) * 2 + sizeof(float) * 2;
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

        foreach (int k in new[] { kPredict, kIntegrate, kSolveDistanceJacobi, kApplyDeltas, kUpdateVelocities, kBuildCollisionConstraints, kSolveCollisionConstraints })
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

        int collisionStride = GpuCollisionConstraint.Stride;
        CollisionConstraintBuffer = new ComputeBuffer(particleCount, collisionStride, ComputeBufferType.Structured);
        compute.SetBuffer(kBuildCollisionConstraints, "collisionConstraints", CollisionConstraintBuffer);
        compute.SetBuffer(kSolveCollisionConstraints, "collisionConstraints", CollisionConstraintBuffer);
        ResetCollisionConstraints();

        compute.SetInt("particleCount", particleCount);
        compute.SetInt("constraintCount", constraintCount);
        compute.SetFloat("omega", sorOmega);

        

        Debug.Log($"[GpuXpbdSolver] Registered: {particleCount} particles, {constraintCount} constraints.");
    }


    void FixedUpdate()
    {
        if (compute == null || ParticleBuffer == null) return;

        float dt = Time.fixedDeltaTime;
        float dts = dt / Mathf.Max(1, substeps);
        float dts2 = dts * dts;

        compute.SetFloat("dt", dt);
        compute.SetFloat("dts", dts);
        compute.SetFloat("dts2", dts2);
        compute.SetVector("gravity", gravity);
        compute.SetFloat("omega", sorOmega);

        int groupsP = Mathf.CeilToInt(Mathf.Max(1, particleCount) / (float)THREADS);
        int groupsC = Mathf.CeilToInt(Mathf.Max(1, constraintCount) / (float)THREADS);

        compute.Dispatch(kPredict, groupsP, 1, 1);
        UpdateClothBounds();
        GetBoundOverlaps();
        UpdateCollisionBuffers();

        for (int s = 0; s < substeps; s++)
        {
            compute.Dispatch(kIntegrate, groupsP, 1, 1);

            if (constraintCount > 0)
            {
                compute.Dispatch(kSolveDistanceJacobi, groupsC, 1, 1);
                compute.Dispatch(kApplyDeltas, groupsP, 1, 1);
            }

            if (SphereBuffer != null) compute.Dispatch(kBuildCollisionConstraints, groupsP, 1, 1);
           
            compute.Dispatch(kSolveCollisionConstraints, groupsP, 1, 1);

            compute.Dispatch(kUpdateVelocities, groupsP, 1, 1);
        }
    }
    void UpdateClothBounds() 
    {
        if (ParticleBuffer == null) return;

        foreach (var cloth in cloths)
        {
            int count = cloth.count;
            if (count <= 0) continue;

            if (_tmpSubset == null || _tmpSubset.Length < count)
                _tmpSubset = new GpuParticle[Mathf.NextPowerOfTwo(count)];

            ParticleBuffer.GetData(_tmpSubset, 0, cloth.startIndex, count);

            Vector3 mn = _tmpSubset[0].positionPredicted;
            Vector3 mx = mn;
            for (int i = 1; i < count; i++)
            {
                var p = _tmpSubset[i].positionPredicted;
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }

            cloth.aabbMin = mn;
            cloth.aabbMax = mx;
        }
    }
    void GetBoundOverlaps()
    {
        //TODO: Use NonAlloc Later 
        _tmpColliders.Clear();

        foreach (var cloth in cloths)
        {
            Bounds bounds = cloth.CurrentBounds;
            bounds.Expand(boundsPadding);

            if (bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f)
                continue;

            var hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity);
            if (hits == null || hits.Length == 0) continue;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h != null) _tmpColliders.Add(h);
            }
        }
    }
    void UpdateCollisionBuffers()
    {
        _tmpSphereColliders.Clear();

        foreach (Collider col in _tmpColliders)
        {
            if (col is SphereCollider sc)
            {
                Transform t = sc.transform;
                Vector3 center = t.TransformPoint(sc.center);
                Vector3 s = t.lossyScale;
                float radius = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                _tmpSphereColliders.Add(new GpuSphereCollider(center, radius));
            }
        }

        int sphereCount = _tmpSphereColliders.Count;
        compute.SetInt("sphereCount", sphereCount);

        if (sphereCount == 0)
        {
            if (SphereBuffer != null) { SphereBuffer.Release(); SphereBuffer = null; }
            return;
        }

        int stride = sizeof(float) * 4; // float3 + float
        if (SphereBuffer != null) SphereBuffer.Release();
        SphereBuffer = new ComputeBuffer(sphereCount, stride, ComputeBufferType.Structured);
        SphereBuffer.SetData(_tmpSphereColliders.ToArray());

        compute.SetBuffer(kBuildCollisionConstraints, "spheres", SphereBuffer);
    }
    void ResetCollisionConstraints()
    {
        if (CollisionConstraintBuffer == null || particleCount <= 0) return;
        var zeros = new GpuCollisionConstraint[particleCount];
        CollisionConstraintBuffer.SetData(zeros);
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
        if (SphereBuffer != null) { SphereBuffer.Release(); SphereBuffer = null; }
        if (CollisionConstraintBuffer != null) { CollisionConstraintBuffer.Release(); CollisionConstraintBuffer = null; }
    }

    void RegisterAllCloths()
    {
        cloths.Clear();
        allParticlesList.Clear();
        allConstraintsList.Clear();

        var found = FindObjectsByType<GpuCloth>(FindObjectsSortMode.None);
        cloths.AddRange(found);

        int runningOffset = 0;

        foreach (var c in cloths)
        {
            c.Build(out var particles, out var constraints, particleRadiusSim, compliance);

            c.startIndex = runningOffset;
            c.count = particles.Length;

            allParticlesList.AddRange(particles);

            for (int k = 0; k < constraints.Length; k++)
            {
                var con = constraints[k];
                allConstraintsList.Add(new GpuDistanceConstraint(
                    (uint)(con.i + (uint)runningOffset),
                    (uint)(con.j + (uint)runningOffset),
                    con.rest,
                    con.compliance
                ));
            }

            runningOffset += particles.Length;
        }
    }
}
