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

    [Header("Physics")]
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField] private float particleRadiusSim = 0.025f;

    [Header("Collision Filter")]
    [SerializeField] private float maxSeparationSpeed = 1.5f;
    [SerializeField] private LayerMask overlapLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("GPU Collision")]
    [SerializeField] private float boundsPadding = 1.0f;
    [SerializeField] private float bufferGrowFactor = 1.5f;

    #endregion

    #region === Public Readonly API ===
    public int ParticleCount => particleCount;
    public int ConstraintCount => constraintCount;

    public ComputeBuffer ParticleBuffer;
    public ComputeBuffer ConstraintBuffer;
    public ComputeBuffer DeltaXBuffer, DeltaYBuffer, DeltaZBuffer, CountBuffer;
    public ComputeBuffer SphereBuffer, CapsuleBuffer;
    public ComputeBuffer CollisionConstraintBuffer, CollisionCountBuffer;
    public ComputeBuffer ClothRangesBuffer, ClothAabbsBuffer;

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
        public static int BuildSphereConstraints;
        public static int BuildCapsuleConstraints;
        public static int SolveCollisionConstraints;
        public static int ResetCollisionCounts;
        public static int BuildClothAabbs;
    }

    // Shader property IDs
    private static class Sid
    {
        public static readonly int Particles = Shader.PropertyToID("particles");
        public static readonly int ParticleCount = Shader.PropertyToID("particleCount");

        public static readonly int Constraints = Shader.PropertyToID("constraints");
        public static readonly int ConstraintCount = Shader.PropertyToID("constraintCount");
        public static readonly int DeltaX = Shader.PropertyToID("deltaX");
        public static readonly int DeltaY = Shader.PropertyToID("deltaY");
        public static readonly int DeltaZ = Shader.PropertyToID("deltaZ");
        public static readonly int CountBuf = Shader.PropertyToID("countBuf");

        //Colliders
        public static readonly int ClothRanges = Shader.PropertyToID("clothRanges");
        public static readonly int ClothAabbs = Shader.PropertyToID("clothAabbs");

        public static readonly int VMax = Shader.PropertyToID("vMax");
        public static readonly int CollisionConstraints = Shader.PropertyToID("collisionConstraints");
        public static readonly int CollisionCounts = Shader.PropertyToID("collisionCounts");
        
        public static readonly int Spheres = Shader.PropertyToID("spheres");
        public static readonly int SphereCount = Shader.PropertyToID("sphereCount");
        public static readonly int Capsules = Shader.PropertyToID("capsules");
        public static readonly int CapsuleCount = Shader.PropertyToID("capsuleCount");


        public static readonly int Omega = Shader.PropertyToID("omega");
        public static readonly int Dt = Shader.PropertyToID("dt");
        public static readonly int Dts = Shader.PropertyToID("dts");
        public static readonly int Dts2 = Shader.PropertyToID("dts2");
        public static readonly int Gravity = Shader.PropertyToID("gravity");
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
    private Aabb[] _aabbCpu;

    private readonly HashSet<Collider> _overlapSet = new();                 
    private readonly List<GpuSphereCollider> _sphereCollidersScratch = new();
    private readonly List<GpuCapsuleCollider> _capsuleCollidersScratch = new(); 
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
        Kid.BuildSphereConstraints = compute.FindKernel("BuildSphereConstraints");
        Kid.BuildCapsuleConstraints = compute.FindKernel("BuildCapsuleConstraints");
        Kid.SolveCollisionConstraints = compute.FindKernel("SolveCollisionConstraints");
        Kid.ResetCollisionCounts = compute.FindKernel("ResetCollisionCounts");
        Kid.BuildClothAabbs = compute.FindKernel("BuildClothAabbs");


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
        compute.SetFloat(Sid.VMax, maxSeparationSpeed);

        int groupsP = Mathf.CeilToInt(Mathf.Max(1, particleCount) / (float)THREADS);
        int groupsC = Mathf.CeilToInt(Mathf.Max(1, constraintCount) / (float)THREADS);

        compute.Dispatch(Kid.Predict, groupsP, 1, 1);
        UpdateClothBoundsGPUGetData();
        GetBoundOverlaps();
        UpdateCollisionBuffers();


        for (int s = 0; s < substeps; s++)
        {
            compute.Dispatch(Kid.Integrate, groupsP, 1, 1);

            if (constraintCount > 0)
            {
                compute.Dispatch(Kid.SolveDistanceJacobi, groupsC, 1, 1);
                compute.Dispatch(Kid.ApplyDeltas, groupsP, 1, 1);
            }

            compute.Dispatch(Kid.ResetCollisionCounts, groupsP, 1, 1);

            if (SphereBuffer != null) compute.Dispatch(Kid.BuildSphereConstraints, groupsP, 1, 1);
            if (CapsuleBuffer != null) compute.Dispatch(Kid.BuildCapsuleConstraints, groupsP, 1, 1);

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
        int particleStride = GpuParticle.Stride;
        ParticleBuffer = new ComputeBuffer(particleCount, particleStride, ComputeBufferType.Structured);
        ParticleBuffer.SetData(allParticles);

        // Constraints
        int conStride = GpuDistanceConstraint.Stride;
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
        foreach (int k in new[]{ Kid.Predict, Kid.Integrate, Kid.SolveDistanceJacobi, Kid.ApplyDeltas, Kid.UpdateVelocities, Kid.BuildSphereConstraints, Kid.BuildCapsuleConstraints, Kid.SolveCollisionConstraints })
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

        compute.SetBuffer(Kid.BuildSphereConstraints, Sid.CollisionConstraints, CollisionConstraintBuffer);
        compute.SetBuffer(Kid.BuildSphereConstraints, Sid.CollisionCounts, CollisionCountBuffer);

        compute.SetBuffer(Kid.BuildCapsuleConstraints, Sid.CollisionConstraints, CollisionConstraintBuffer);
        compute.SetBuffer(Kid.BuildCapsuleConstraints, Sid.CollisionCounts, CollisionCountBuffer);


        compute.SetBuffer(Kid.SolveCollisionConstraints, Sid.CollisionConstraints, CollisionConstraintBuffer);
        compute.SetBuffer(Kid.SolveCollisionConstraints, Sid.CollisionCounts, CollisionCountBuffer);

        compute.SetBuffer(Kid.ResetCollisionCounts, Sid.CollisionCounts, CollisionCountBuffer);


        compute.SetInt(Sid.ParticleCount, particleCount);
        compute.SetInt(Sid.ConstraintCount, constraintCount);
        compute.SetFloat(Sid.Omega, sorOmega);


        if (cloths.Count > 0)
        {
            var ranges = new ClothRange[cloths.Count];
            for (int c = 0; c < cloths.Count; c++)
            {
                ranges[c] = new ClothRange
                {
                    start = (uint)cloths[c].startIndex,
                    count = (uint)cloths[c].count
                };
            }

            SafeRelease(ref ClothRangesBuffer);
            SafeRelease(ref ClothAabbsBuffer);

            ClothRangesBuffer = new ComputeBuffer(cloths.Count, ClothRange.Stride, ComputeBufferType.Structured);
            ClothRangesBuffer.SetData(ranges);

            ClothAabbsBuffer = new ComputeBuffer(cloths.Count, Aabb.Stride, ComputeBufferType.Structured);
            _aabbCpu = new Aabb[cloths.Count];

            compute.SetBuffer(Kid.BuildClothAabbs, Sid.Particles, ParticleBuffer);
            compute.SetBuffer(Kid.BuildClothAabbs, Sid.ClothRanges, ClothRangesBuffer);
            compute.SetBuffer(Kid.BuildClothAabbs, Sid.ClothAabbs, ClothAabbsBuffer);
        }


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
        SafeRelease(ref CapsuleBuffer);
        SafeRelease(ref CollisionConstraintBuffer);
        SafeRelease(ref CollisionCountBuffer);
        SafeRelease(ref ClothRangesBuffer);
        SafeRelease(ref ClothAabbsBuffer);

    }
    #endregion


    #region === Simulation Steps ===
    private void UpdateClothBoundsGPUGetData()
    {
        if (cloths.Count == 0 || ClothAabbsBuffer == null) return;

        compute.Dispatch(Kid.BuildClothAabbs, cloths.Count, 1, 1);
        ClothAabbsBuffer.GetData(_aabbCpu);

        for (int c = 0; c < cloths.Count; c++)
        {
            cloths[c].aabbMin = _aabbCpu[c].mn;
            cloths[c].aabbMax = _aabbCpu[c].mx;
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
        _capsuleCollidersScratch.Clear();

        foreach (Collider col in _overlapSet)
        {
            if (col is SphereCollider sc)
            {
                Transform t = sc.transform;
                Vector3 center = t.TransformPoint(sc.center);
                Vector3 s = t.lossyScale;
                float radius = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                _sphereCollidersScratch.Add(new GpuSphereCollider(center, radius));
            }
            else if (col is CapsuleCollider cc)
            {
                ExtractCapsule(cc, out var p0, out var p1, out float r);
                _capsuleCollidersScratch.Add(new GpuCapsuleCollider(p0, p1, r));
            }
        }

        // --- SphereColliders ---
        int sphereCount = _sphereCollidersScratch.Count;
        compute.SetInt(Sid.SphereCount, sphereCount);

        if (sphereCount > 0)
        {
            EnsureBuffer(ref SphereBuffer, sphereCount, GpuSphereCollider.Stride);
            SphereBuffer.SetData(_sphereCollidersScratch.ToArray(), 0, 0, sphereCount);
            compute.SetBuffer(Kid.BuildSphereConstraints, Sid.Spheres, SphereBuffer);
        }

        // --- CapsuleColliders ---
        int capsuleCount = _capsuleCollidersScratch.Count;
        compute.SetInt(Sid.CapsuleCount, capsuleCount);

        if (capsuleCount > 0)
        {
            EnsureBuffer(ref CapsuleBuffer, capsuleCount, GpuCapsuleCollider.Stride);
            CapsuleBuffer.SetData(_capsuleCollidersScratch.ToArray(), 0, 0, capsuleCount);
            compute.SetBuffer(Kid.BuildCapsuleConstraints, Sid.Capsules, CapsuleBuffer);
        }
    }
    static void EnsureBuffer(ref ComputeBuffer buf, int neededCount, int stride, ComputeBufferType type = ComputeBufferType.Structured, float growFactor = 1.5f)
    {
        int current = (buf == null) ? 0 : buf.count;
        if (current >= neededCount && buf != null) return;

        int newCount = Mathf.Max(neededCount, Mathf.Max(64, current > 0 ? Mathf.CeilToInt(current * growFactor) : neededCount));
        if (buf != null) { buf.Dispose(); buf = null; }
        buf = new ComputeBuffer(newCount, stride, type);
    }


    private static void ExtractCapsule(CapsuleCollider cc, out Vector3 p0, out Vector3 p1, out float rWorld)
    {
        var t = cc.transform;

        Vector3 axisLocal = cc.direction switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };

        float halfCyl = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
        Vector3 centerLocal = cc.center;

        Vector3 p0Local = centerLocal - axisLocal * halfCyl;
        Vector3 p1Local = centerLocal + axisLocal * halfCyl;

        p0 = t.TransformPoint(p0Local);
        p1 = t.TransformPoint(p1Local);

        Vector3 s = t.lossyScale;
        rWorld = cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
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
