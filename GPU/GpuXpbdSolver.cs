using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GpuXpbdSolver : MonoBehaviour
{

    #region === Inspector Settings ===
    [Header("Simulation")]
    [SerializeField] private int substeps = 10;
    [SerializeField, Range(0.5f, 2.5f)] private float sorOmega = 1.5f;
    [SerializeField] private ComputeShader compute;
    [SerializeField] private float compliance = 0.0f;
    [SerializeField] private float boundsPadding = 1.0f;

    [Header("Physics")]
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField] private float particleRadiusSim = 0.025f;

    [Header("Collision Filter")]
    [SerializeField] private LayerMask overlapLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    #endregion

    #region === Public Readonly API ===
    public int ParticleCount => particleCount;
    public int ConstraintCount => constraintCount;

    public ComputeBuffer ParticleBuffer;
    public ComputeBuffer ConstraintBuffer;
    public ComputeBuffer DeltaXBuffer, DeltaYBuffer, DeltaZBuffer, CountBuffer;
    public ComputeBuffer SphereBuffer;
    public ComputeBuffer CollisionConstraintBuffer;
    public ComputeBuffer CollisionCountBuffer;
    #endregion

    #region === Constants / IDs ===
    private const int THREADS = 256;

    // Kernel IDs
    private static class Kid
    {
        public static int Predict;
        public static int Integrate;
        public static int SolveDistanceJacobi;
        public static int ApplyDeltas;
        public static int UpdateVelocities;
        public static int BuildCollisionConstraints;
        public static int SolveCollisionConstraints;
        public static int ResetCollisionCounts;
    }

    // Shader property IDs
    private static class Sid
    {
        public static readonly int Particles = Shader.PropertyToID("particles");
        public static readonly int Constraints = Shader.PropertyToID("constraints");
        public static readonly int DeltaX = Shader.PropertyToID("deltaX");
        public static readonly int DeltaY = Shader.PropertyToID("deltaY");
        public static readonly int DeltaZ = Shader.PropertyToID("deltaZ");
        public static readonly int CountBuf = Shader.PropertyToID("countBuf");
        public static readonly int CollisionConstraints = Shader.PropertyToID("collisionConstraints");
        public static readonly int CollisionCounts = Shader.PropertyToID("collisionCounts");
        public static readonly int Spheres = Shader.PropertyToID("spheres");

        public static readonly int ParticleCount = Shader.PropertyToID("particleCount");
        public static readonly int ConstraintCount = Shader.PropertyToID("constraintCount");
        public static readonly int Omega = Shader.PropertyToID("omega");
        public static readonly int Dt = Shader.PropertyToID("dt");
        public static readonly int Dts = Shader.PropertyToID("dts");
        public static readonly int Dts2 = Shader.PropertyToID("dts2");
        public static readonly int Gravity = Shader.PropertyToID("gravity");
        public static readonly int SphereCount = Shader.PropertyToID("sphereCount");
    }
    #endregion

    #region === Internal State / Scratch ===
    private int particleCount;
    private int constraintCount;

    private readonly List<GpuCloth> cloths = new();
    private readonly List<GpuParticle> allParticlesList = new();
    private readonly List<GpuDistanceConstraint> allConstraintsList = new();

    // Scratch for bounds building (CPU readback subset)
    private GpuParticle[] _particleSubsetScratch;

    // Collision detection scratch
    private readonly HashSet<Collider> _overlapSet = new();                 // dedup across all cloths
    private readonly List<GpuSphereCollider> _sphereCollidersScratch = new();
    #endregion


    #region === Unity Lifecycle ===
    private void Start()
    {
        if (compute == null)
        {
            Debug.LogError("[GpuXpbdSolver] ComputeShader is missing");
            enabled = false;
            return;
        }

        // Cache kernel IDs once
        Kid.Predict = compute.FindKernel("Predict");
        Kid.Integrate = compute.FindKernel("Integrate");
        Kid.SolveDistanceJacobi = compute.FindKernel("SolveDistanceJacobi");
        Kid.ApplyDeltas = compute.FindKernel("ApplyDeltas");
        Kid.UpdateVelocities = compute.FindKernel("UpdateVelocities");
        Kid.BuildCollisionConstraints = compute.FindKernel("BuildCollisionConstraints");
        Kid.SolveCollisionConstraints = compute.FindKernel("SolveCollisionConstraints");
        Kid.ResetCollisionCounts = compute.FindKernel("ResetCollisionCounts");

        RegisterAllCloths();
        InitializeBuffers();
    }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() => ReleaseAll();
    private void FixedUpdate()
    {
        if (compute == null || ParticleBuffer == null) return;

        float dt = Time.fixedDeltaTime;
        float dts = dt / Mathf.Max(1, substeps);
        float dts2 = dts * dts;

        compute.SetFloat(Sid.Dt, dt);
        compute.SetFloat(Sid.Dts, dts);
        compute.SetFloat(Sid.Dts2, dts2);
        compute.SetVector(Sid.Gravity, gravity);
        compute.SetFloat(Sid.Omega, sorOmega);

        int groupsP = Mathf.CeilToInt(Mathf.Max(1, particleCount) / (float)THREADS);
        int groupsC = Mathf.CeilToInt(Mathf.Max(1, constraintCount) / (float)THREADS);

        compute.Dispatch(Kid.Predict, groupsP, 1, 1);
        UpdateClothBounds();
        GetBoundOverlaps();
        UpdateCollisionBuffers();

        compute.Dispatch(Kid.ResetCollisionCounts, groupsP, 1, 1);

        for (int s = 0; s < substeps; s++)
        {
            compute.Dispatch(Kid.Integrate, groupsP, 1, 1);

            if (constraintCount > 0)
            {
                compute.Dispatch(Kid.SolveDistanceJacobi, groupsC, 1, 1);
                compute.Dispatch(Kid.ApplyDeltas, groupsP, 1, 1);
            }

            if (SphereBuffer != null)
                compute.Dispatch(Kid.BuildCollisionConstraints, groupsP, 1, 1);

            compute.Dispatch(Kid.SolveCollisionConstraints, groupsP, 1, 1);
            compute.Dispatch(Kid.UpdateVelocities, groupsP, 1, 1);
        }
    }
    #endregion


    #region === Init / Teardown ===
    private void RegisterAllCloths()
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
    private void InitializeBuffers()
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

        // Particles
        int particleStride = 15 * sizeof(float); // float3*4 + float*3
        ParticleBuffer = new ComputeBuffer(particleCount, particleStride, ComputeBufferType.Structured);
        ParticleBuffer.SetData(allParticles);

        // Constraints
        int conStride = sizeof(uint) * 2 + sizeof(float) * 2;
        if (constraintCount > 0)
        {
            ConstraintBuffer = new ComputeBuffer(constraintCount, conStride, ComputeBufferType.Structured);
            ConstraintBuffer.SetData(allConstraints);
        }

        // Accumulators
        DeltaXBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        DeltaYBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        DeltaZBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        CountBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        ZeroAccumulators();

        // Bind common buffers to kernels
        foreach (int k in new[]{ Kid.Predict, Kid.Integrate, Kid.SolveDistanceJacobi, Kid.ApplyDeltas, Kid.UpdateVelocities, Kid.BuildCollisionConstraints, Kid.SolveCollisionConstraints })
        {
            compute.SetBuffer(k, Sid.Particles, ParticleBuffer);
        }

        if (ConstraintBuffer != null)
        {
            compute.SetBuffer(Kid.SolveDistanceJacobi, Sid.Constraints, ConstraintBuffer);
            compute.SetBuffer(Kid.SolveDistanceJacobi, Sid.DeltaX, DeltaXBuffer);
            compute.SetBuffer(Kid.SolveDistanceJacobi, Sid.DeltaY, DeltaYBuffer);
            compute.SetBuffer(Kid.SolveDistanceJacobi, Sid.DeltaZ, DeltaZBuffer);
            compute.SetBuffer(Kid.SolveDistanceJacobi, Sid.CountBuf, CountBuffer);
        }

        compute.SetBuffer(Kid.ApplyDeltas, Sid.DeltaX, DeltaXBuffer);
        compute.SetBuffer(Kid.ApplyDeltas, Sid.DeltaY, DeltaYBuffer);
        compute.SetBuffer(Kid.ApplyDeltas, Sid.DeltaZ, DeltaZBuffer);
        compute.SetBuffer(Kid.ApplyDeltas, Sid.CountBuf, CountBuffer);

        // Collision constraints (per particle)
        const int MAX_COLLISIONS = 8;
        int collisionStride = GpuCollisionConstraint.Stride;
        CollisionConstraintBuffer = new ComputeBuffer(particleCount * MAX_COLLISIONS, collisionStride, ComputeBufferType.Structured);
        CollisionCountBuffer = new ComputeBuffer(particleCount, sizeof(uint), ComputeBufferType.Structured);
        CollisionCountBuffer.SetData(new uint[particleCount]);

        compute.SetBuffer(Kid.BuildCollisionConstraints, Sid.CollisionConstraints, CollisionConstraintBuffer);
        compute.SetBuffer(Kid.BuildCollisionConstraints, Sid.CollisionCounts, CollisionCountBuffer);

        compute.SetBuffer(Kid.SolveCollisionConstraints, Sid.CollisionConstraints, CollisionConstraintBuffer);
        compute.SetBuffer(Kid.SolveCollisionConstraints, Sid.CollisionCounts, CollisionCountBuffer);

        compute.SetBuffer(Kid.ResetCollisionCounts, Sid.CollisionCounts, CollisionCountBuffer);


        compute.SetInt(Sid.ParticleCount, particleCount);
        compute.SetInt(Sid.ConstraintCount, constraintCount);
        compute.SetFloat(Sid.Omega, sorOmega);

        Debug.Log($"[GpuXpbdSolver] Registered: {particleCount} particles, {constraintCount} constraints.");
    }
    private static void SafeRelease(ref ComputeBuffer buf)
    {
        if (buf == null) return;
        buf.Release();
        buf = null;
    }
    private void ReleaseAll()
    {
        SafeRelease(ref ParticleBuffer);
        SafeRelease(ref ConstraintBuffer);
        SafeRelease(ref DeltaXBuffer);
        SafeRelease(ref DeltaYBuffer);
        SafeRelease(ref DeltaZBuffer);
        SafeRelease(ref CountBuffer);
        SafeRelease(ref SphereBuffer);
        SafeRelease(ref CollisionConstraintBuffer);
        SafeRelease(ref CollisionCountBuffer);
    }
    #endregion


    #region === Simulation Steps ===
    private void UpdateClothBounds()
    {
        if (ParticleBuffer == null || particleCount <= 0) return;

        foreach (var cloth in cloths)
        {
            int count = cloth.count;
            if (count <= 0) continue;

            if (_particleSubsetScratch == null || _particleSubsetScratch.Length < count)
                _particleSubsetScratch = new GpuParticle[Mathf.Max(1, Mathf.NextPowerOfTwo(count))];

            ParticleBuffer.GetData(_particleSubsetScratch, 0, cloth.startIndex, cloth.count);

            Vector3 mn = _particleSubsetScratch[0].positionPredicted;
            Vector3 mx = mn;

            for (int i = 1; i < cloth.count; i++)
            {
                var p = _particleSubsetScratch[i].positionPredicted;
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }

            cloth.aabbMin = mn;
            cloth.aabbMax = mx;
        }
    }
    private void GetBoundOverlaps()
    {
        _overlapSet.Clear();

        foreach (var cloth in cloths)
        {
            Bounds b = cloth.CurrentBounds;
            b.Expand(boundsPadding);
            if (b.size.x <= 0f || b.size.y <= 0f || b.size.z <= 0f) continue;

            var hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity, overlapLayerMask, triggerInteraction);
            if (hits == null || hits.Length == 0) continue;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h != null) _overlapSet.Add(h);
            }
        }
    }
    private void UpdateCollisionBuffers()
    {
        _sphereCollidersScratch.Clear();

        foreach (Collider col in _overlapSet)
        {
            if (col is not SphereCollider sc) continue;

            Transform t = sc.transform;
            Vector3 center = t.TransformPoint(sc.center);
            Vector3 s = t.lossyScale;
            float radius = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

            _sphereCollidersScratch.Add(new GpuSphereCollider(center, radius));
        }

        int sphereCount = _sphereCollidersScratch.Count;
        compute.SetInt(Sid.SphereCount, sphereCount);

        if (sphereCount == 0)
        {
            SafeRelease(ref SphereBuffer);
            return;
        }

        SafeRelease(ref SphereBuffer);
        int stride = sizeof(float) * 4; // float3 + float
        SphereBuffer = new ComputeBuffer(sphereCount, stride, ComputeBufferType.Structured);
        SphereBuffer.SetData(_sphereCollidersScratch);

        compute.SetBuffer(Kid.BuildCollisionConstraints, Sid.Spheres, SphereBuffer);
    }

    private void ZeroAccumulators()
    {
        if (particleCount <= 0) return;
        var zeros = new uint[particleCount];
        DeltaXBuffer.SetData(zeros);
        DeltaYBuffer.SetData(zeros);
        DeltaZBuffer.SetData(zeros);
        CountBuffer.SetData(zeros);
    }
    #endregion

}
