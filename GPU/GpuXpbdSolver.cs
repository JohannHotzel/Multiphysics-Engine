using System.Collections.Generic;
using UnityEngine;
using static GpuXpbdShaderIds;

public class GpuXpbdSolver : MonoBehaviour
{
    #region === Inspector Settings ===
    [Header("Simulation")]
    [SerializeField] private int substeps = 10;
    [SerializeField, Range(0.5f, 2.5f)] private float sorOmega = 1.5f;
    [SerializeField] private ComputeShader compute;

    [Header("Physics")]
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField] private float particleRadiusSim = 0.025f;

    [Header("Collision Filter")]
    [SerializeField] private float maxSeparationSpeed = 1.5f;
    [SerializeField] private LayerMask overlapLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool enableParticleParticleCollision = false;

    [Header("GPU Collision")]
    [SerializeField] private float boundsPadding = 1.0f;
    [SerializeField] private float bufferGrowFactor = 1.5f;
    [SerializeField] private float collisionMargin = 0.01f;
    #endregion


    #region === Internal State ===
    private int particleCount;
    private int constraintCount;

    private GpuXpbdBufferSet buffers;
    public GpuXpbdBufferSet Buffers => buffers;

    private readonly List<GpuCloth> cloths = new();
    private readonly List<GpuParticle> allParticlesList = new();
    private readonly List<GpuDistanceConstraint> allConstraintsList = new();

    // Collision scratch
    private Aabb[] _aabbCpu;
    private readonly HashSet<Collider> _overlapSet = new();
    private readonly List<GpuSphereCollider> _sphereScratch = new();
    private readonly List<GpuCapsuleCollider> _capsuleScratch = new();
    private readonly List<GpuBoxCollider> _boxScratch = new();
    private readonly List<GpuTriangle> _meshTrisScratch = new();
    private readonly List<GpuMeshRange> _meshRangesScratch = new();

    // Attachments
    private readonly List<GpuAttachmentObject> _attachObjsCpu = new();
    private readonly List<Transform> _attachObjRefs = new();
    private readonly Dictionary<Transform, int> _objToIndex = new();
    private readonly List<GpuAttachmentConstraint> _attachConsCpu = new();

    #endregion


    #region === Unity Lifecycle ===
    private void Start()
    {
        if (compute == null)
        {
            Debug.LogError("[GpuXpbdSolver] Missing ComputeShader!");
            enabled = false;
            return;
        }

        Kid.Init(compute);
        buffers = new GpuXpbdBufferSet();

        RegisterAllCloths();
        BuildInitialAttachments();
        InitializeBuffers();
    }

    private void OnDisable() => buffers?.ReleaseAll();
    private void OnDestroy() => buffers?.ReleaseAll();

    private void FixedUpdate()
    {
        if (compute == null || buffers == null || buffers.ParticleCount == 0)
            return;

        float dt = Time.fixedDeltaTime;
        float dts = dt / Mathf.Max(1, substeps);
        float dts2 = dts * dts;

        compute.SetFloat(Sid.Dt, dt);
        compute.SetFloat(Sid.Dts, dts);
        compute.SetFloat(Sid.Dts2, dts2);
        compute.SetVector(Sid.Gravity, gravity);
        compute.SetFloat(Sid.Omega, sorOmega);
        compute.SetFloat(Sid.VMax, maxSeparationSpeed);
        compute.SetFloat(Sid.CollisionMargin, collisionMargin);

        int groupsP = Mathf.CeilToInt(Mathf.Max(1, buffers.ParticleCount) / (float)THREADS);
        int groupsC = Mathf.CeilToInt(Mathf.Max(1, buffers.ConstraintCount) / (float)THREADS);
        int groupsA = Mathf.CeilToInt(buffers.AttachmentConstraintCount / (float)THREADS);

        compute.Dispatch(Kid.Predict, groupsP, 1, 1);
        UpdateClothBoundsGPUGetData();
        GetBoundOverlaps();
        UpdateCollisionBuffers();
        
        UpdateAttachmentObjects();

        if(enableParticleParticleCollision)
            RebuildSpatialHash();

        for (int s = 0; s < substeps; s++)
        {
            compute.Dispatch(Kid.Integrate, groupsP, 1, 1);

            if (buffers.AttachmentConstraintCount > 0) compute.Dispatch(Kid.SetAttachmentPositions, groupsA, 1, 1);

            if (buffers.ConstraintCount > 0)
            {
                compute.Dispatch(Kid.SolveDistanceJacobi, groupsC, 1, 1);
                compute.Dispatch(Kid.ApplyDeltas, groupsP, 1, 1);
            }

            if (enableParticleParticleCollision)
            {
                compute.Dispatch(Kid.SolveParticleCollisionsHashed, groupsP, 1, 1);
                compute.Dispatch(Kid.ApplyDeltas, groupsP, 1, 1);
            }

            compute.Dispatch(Kid.ResetCollisionCounts, groupsP, 1, 1);
            if (buffers.SphereBuffer != null) compute.Dispatch(Kid.BuildSphereConstraints, groupsP, 1, 1);
            if (buffers.CapsuleBuffer != null) compute.Dispatch(Kid.BuildCapsuleConstraints, groupsP, 1, 1);
            if (buffers.BoxBuffer != null) compute.Dispatch(Kid.BuildBoxConstraints, groupsP, 1, 1);
            if (buffers.MeshTriangleBuffer != null) compute.Dispatch(Kid.BuildMeshConstraints, groupsP, 1, 1);
            compute.Dispatch(Kid.SolveCollisionConstraints, groupsP, 1, 1);

            compute.Dispatch(Kid.UpdateVelocities, groupsP, 1, 1);
        }
    }
    #endregion


    #region === Initialization ===
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
            c.Build(out var particles, out var constraints, particleRadiusSim);

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
    private void BuildInitialAttachments()
    {
        _attachObjsCpu.Clear();
        _attachObjRefs.Clear();
        _objToIndex.Clear();
        _attachConsCpu.Clear();

        for (int i = 0; i < allParticlesList.Count; i++)
        {
            var p = allParticlesList[i];

            if (!TryFindAttachmentForParticle(p.positionX, p.radius, out Transform tr, out Vector3 contactPoint))
                continue;

            // Make particle static 
            p.w = 0f; 
            allParticlesList[i] = p;

            if (!_objToIndex.TryGetValue(tr, out int objIdx))
            {
                _objToIndex[tr] = objIdx = _attachObjsCpu.Count;
                _attachObjRefs.Add(tr);
                _attachObjsCpu.Add(new GpuAttachmentObject { world = tr.localToWorldMatrix });
            }

            _attachConsCpu.Add(new GpuAttachmentConstraint
            {
                particle = (uint)i,
                objectIndex = (uint)objIdx,
                localPoint = tr.InverseTransformPoint(contactPoint),
            });
        }
    }
    private void InitializeBuffers()
    {
        buffers.ReleaseAll();

        var allParticles = allParticlesList.ToArray();
        var allConstraints = allConstraintsList.ToArray();

        var attachObjs = _attachObjsCpu.Count > 0 ? _attachObjsCpu.ToArray() : null;
        var attachCons = _attachConsCpu.Count > 0 ? _attachConsCpu.ToArray() : null;

        particleCount = allParticles.Length;
        constraintCount = allConstraints.Length;

        if (particleCount == 0)
        {
            Debug.LogWarning("[GpuXpbdSolver] No particles to register.");
            return;
        }

        // Core init
        buffers.InitializeParticlesAndConstraints(allParticles, allConstraints, attachObjs, attachCons, bufferGrowFactor);
        buffers.SetCountsOn(compute);


        buffers.BindParticlesTo(compute,
            Kid.Predict, Kid.Integrate, Kid.SolveDistanceJacobi, Kid.ApplyDeltas, Kid.UpdateVelocities,
            Kid.BuildSphereConstraints, Kid.BuildCapsuleConstraints, Kid.BuildBoxConstraints, Kid.BuildMeshConstraints,
            Kid.SolveCollisionConstraints, Kid.SetAttachmentPositions, Kid.HashCountCells, Kid.HashFillEntries, Kid.SolveParticleCollisionsHashed
        );

        buffers.BindDistanceSolve(compute, Kid.SolveDistanceJacobi);

        buffers.BindCollisionCore(compute, Kid.SolveCollisionConstraints, Kid.ResetCollisionCounts, Kid.BuildSphereConstraints, Kid.BuildCapsuleConstraints, Kid.BuildBoxConstraints, Kid.BuildMeshConstraints);

        buffers.BindAttachments(compute, Kid.SetAttachmentPositions);

        buffers.BindDeltasTo(compute, Kid.SolveDistanceJacobi, Kid.ApplyDeltas, Kid.SolveParticleCollisionsHashed);

        buffers.InitializeHash(compute, particleCount, particleRadiusSim, bufferGrowFactor);
        buffers.BindHashTo(compute, Kid.HashClearCounts, Kid.HashCountCells, Kid.HashFillEntries, Kid.SolveParticleCollisionsHashed);


        // Cloth
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

            buffers.InitializeCloth(ranges, compute, Kid.BuildClothAabbs);
            _aabbCpu = new Aabb[cloths.Count];
        }


        Debug.Log($"[GpuXpbdSolver] Registered: {particleCount} particles, {constraintCount} constraints, " + $"{buffers.AttachmentConstraintCount} attachments on {buffers.AttachmentObjectCount} objects.");
    }
    #endregion


    #region === Simulation Helpers ===
    private void UpdateClothBoundsGPUGetData()
    {
        if (cloths.Count == 0 || buffers.ClothAabbsBuffer == null)
            return;

        compute.Dispatch(Kid.BuildClothAabbs, cloths.Count, 1, 1);
        buffers.ClothAabbsBuffer.GetData(_aabbCpu);

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
            if (hits == null) continue;

            for (int i = 0; i < hits.Length; i++)
                if (hits[i] != null)
                    _overlapSet.Add(hits[i]);
        }
    }
    private void UpdateCollisionBuffers()
    {
        _sphereScratch.Clear();
        _capsuleScratch.Clear();
        _boxScratch.Clear();
        _meshTrisScratch.Clear();
        _meshRangesScratch.Clear();

        foreach (Collider col in _overlapSet)
        {
            if (col is SphereCollider sc)
            {
                Transform t = sc.transform;
                Vector3 center = t.TransformPoint(sc.center);
                Vector3 s = t.lossyScale;
                float radius = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                _sphereScratch.Add(new GpuSphereCollider(center, radius));
            }
            else if (col is CapsuleCollider cc)
            {
                ExtractCapsule(cc, out var p0, out var p1, out float r);
                _capsuleScratch.Add(new GpuCapsuleCollider(p0, p1, r));
            }
            else if (col is BoxCollider bc)
            {
                ExtractBox(bc, out var center, out var right, out var up, out var fwd, out var halfExtents);
                _boxScratch.Add(new GpuBoxCollider(center, right, up, fwd, halfExtents));
            }
            else if (col is MeshCollider mc && mc.sharedMesh != null)
            {
                ExtractMeshTriangles(mc, _meshTrisScratch, _meshRangesScratch);
            }
        }

        buffers.UploadSpheres(compute, Kid.BuildSphereConstraints, _sphereScratch);
        buffers.UploadCapsules(compute, Kid.BuildCapsuleConstraints, _capsuleScratch);
        buffers.UploadBoxes(compute, Kid.BuildBoxConstraints, _boxScratch);
        buffers.UploadMeshes(compute, Kid.BuildMeshConstraints, _meshTrisScratch, _meshRangesScratch);
    }
    private void RebuildSpatialHash()
    {
        if (buffers == null || buffers.ParticleCount <= 0) return;

        int groupsH = Mathf.CeilToInt( (buffers.HashTableSize + 1) / (float)THREADS);
        int groupsP = Mathf.CeilToInt(buffers.ParticleCount / (float)THREADS);

        compute.Dispatch(Kid.HashClearCounts, groupsH, 1, 1);
        compute.Dispatch(Kid.HashCountCells, groupsP, 1, 1);


        var ends = new uint[buffers.HashTableSize + 1];
        buffers.HashCellStarts.GetData(ends, 0, 0, buffers.HashTableSize);

        uint running = 0;
        for (int h = 0; h < buffers.HashTableSize; h++)
        {
            running += ends[h];
            ends[h] = running; 
        }
        ends[buffers.HashTableSize] = running;

        buffers.HashCellStarts.SetData(ends);

        compute.Dispatch(Kid.HashFillEntries, groupsP, 1, 1);
    }
    #endregion

    #region == Attachment Helpers ===
    private bool TryFindAttachmentForParticle(Vector3 particlePos, float particleRadius, out Transform tr, out Vector3 contactPoint)
    {
        tr = null;
        contactPoint = Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(
            particlePos,
            particleRadius,
            overlapLayerMask,
            triggerInteraction
        );

        if (hits == null || hits.Length == 0)
            return false;

        Collider first = hits[0];
        if (first == null)
            return false;

        tr = first.transform;
        contactPoint = particlePos;
        return true;
    }
    private void UpdateAttachmentObjects()
    {
        if (buffers.AttachmentObjectCount == 0) return;
        for (int i = 0; i < _attachObjRefs.Count; i++)
            _attachObjsCpu[i] = new GpuAttachmentObject { world = _attachObjRefs[i].localToWorldMatrix };

        buffers.AttachmentObjectsBuffer.SetData(_attachObjsCpu);
    }
    #endregion

    #region === Geometry Extract Helpers ===
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
    private static void ExtractBox(BoxCollider bc, out Vector3 center, out Vector3 right, out Vector3 up, out Vector3 fwd, out Vector3 halfExtents)
    {
        var t = bc.transform;
        Matrix4x4 localToWorld = t.localToWorldMatrix;

        Vector3 hxLocal = new(bc.size.x * 0.5f, 0f, 0f);
        Vector3 hyLocal = new(0f, bc.size.y * 0.5f, 0f);
        Vector3 hzLocal = new(0f, 0f, bc.size.z * 0.5f);

        Vector3 hxWorld = localToWorld.MultiplyVector(hxLocal);
        Vector3 hyWorld = localToWorld.MultiplyVector(hyLocal);
        Vector3 hzWorld = localToWorld.MultiplyVector(hzLocal);

        float ex = hxWorld.magnitude;
        float ey = hyWorld.magnitude;
        float ez = hzWorld.magnitude;

        right = (ex > 1e-8f) ? (hxWorld / ex) : t.right;
        up = (ey > 1e-8f) ? (hyWorld / ey) : t.up;
        fwd = (ez > 1e-8f) ? (hzWorld / ez) : t.forward;
        halfExtents = new Vector3(ex, ey, ez);

        center = t.TransformPoint(bc.center);
    }
    private static void ExtractMeshTriangles(MeshCollider mc, List<GpuTriangle> outTris, List<GpuMeshRange> outRanges)
    {
        var mesh = mc.sharedMesh;
        var t = mc.transform;

        var verts = mesh.vertices;
        var indices = mesh.triangles;
        if (indices == null || indices.Length < 3) return;

        uint start = (uint)outTris.Count;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = t.TransformPoint(verts[indices[i]]);
            Vector3 b = t.TransformPoint(verts[indices[i + 1]]);
            Vector3 c = t.TransformPoint(verts[indices[i + 2]]);
            outTris.Add(new GpuTriangle(a, b, c));
        }

        uint count = (uint)(outTris.Count) - start;
        if (count > 0)
            outRanges.Add(new GpuMeshRange(start, count));
    }
    #endregion
}