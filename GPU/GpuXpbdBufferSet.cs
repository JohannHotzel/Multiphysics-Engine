using System.Collections.Generic;
using UnityEngine;
using static GpuXpbdShaderIds;

public sealed class GpuXpbdBufferSet
{
    // ---- Public Readonly API ----
    public int ParticleCount { get; private set; }
    public int ConstraintCount { get; private set; }
    public int AttachmentObjectCount { get; private set; }
    public int AttachmentConstraintCount { get; private set; }

    // ---- Core Buffers ----
    public ComputeBuffer ParticleBuffer;
    public ComputeBuffer ConstraintBuffer;
    public ComputeBuffer DeltaXBuffer, DeltaYBuffer, DeltaZBuffer, CountBuffer;

    // ---- Collision Buffers ----
    public ComputeBuffer CollisionConstraintBuffer, CollisionCountBuffer;
    public ComputeBuffer ImpulseEventBuffer;
    private ComputeBuffer _appendCountScratch;
    public ComputeBuffer SphereBuffer, CapsuleBuffer, BoxBuffer;
    public ComputeBuffer MeshTriangleBuffer, MeshRangeBuffer;

    // --- Spacial Hashing Buffers ----
    public ComputeBuffer HashCellStarts, HashCellEntries;
    public int HashTableSize;
    public float HashSpacing;

    // ---- Attachment Buffers ----
    public ComputeBuffer AttachmentObjectsBuffer, AttachmentConstraintsBuffer;

    // ---- Cloth/Broadphase ----
    public ComputeBuffer ClothRangesBuffer, ClothAabbsBuffer;



    // ---- Init / Teardown ----
    public void InitializeParticles(GpuParticle[] particles, float growFactor = 1.5f)
    {
        ParticleCount = particles?.Length ?? 0;
        if (ParticleCount <= 0) return;
        Ensure(ref ParticleBuffer, ParticleCount, GpuParticle.Stride, ComputeBufferType.Structured, growFactor);
        ParticleBuffer.SetData(particles);
    }
    public void InitializeConstraints(GpuDistanceConstraint[] constraints, float growFactor = 1.5f)
    {
        ConstraintCount = constraints?.Length ?? 0;
        if (ConstraintCount <= 0) return;
        Ensure(ref ConstraintBuffer, ConstraintCount, GpuDistanceConstraint.Stride, ComputeBufferType.Structured, growFactor);
        ConstraintBuffer.SetData(constraints);
    }
    public void InitializeAccumulators(int particleCount)
    {
        if (particleCount <= 0) return;
        Ensure(ref DeltaXBuffer, particleCount, sizeof(uint));
        Ensure(ref DeltaYBuffer, particleCount, sizeof(uint));
        Ensure(ref DeltaZBuffer, particleCount, sizeof(uint));
        Ensure(ref CountBuffer, particleCount, sizeof(uint));
        ZeroAccumulators();
    }
    public void InitializeCollisionStorage(int particleCount, int substeps, float growFactor = 1.5f)
    {
        if (particleCount <= 0) return;

        int colCap = Mathf.Max(1, particleCount);
        Ensure(ref CollisionConstraintBuffer, colCap, GpuCollisionConstraint.Stride, ComputeBufferType.Structured, growFactor);
        Ensure(ref CollisionCountBuffer, particleCount, sizeof(uint), ComputeBufferType.Structured, growFactor);
        ZeroUInt(ref CollisionCountBuffer, particleCount);

        int evtCap = Mathf.Max(1, particleCount * substeps);
        Ensure(ref ImpulseEventBuffer, evtCap, GpuImpulseEvent.Stride, ComputeBufferType.Append);
        ImpulseEventBuffer.SetCounterValue(0);
    }
    public void InitializeAttachments(GpuAttachmentObject[] objs, GpuAttachmentConstraint[] cons, float growFactor = 1.5f)
    {
        AttachmentObjectCount = objs?.Length ?? 0;
        AttachmentConstraintCount = cons?.Length ?? 0;

        if (AttachmentObjectCount > 0)
        {
            Ensure(ref AttachmentObjectsBuffer, AttachmentObjectCount, GpuAttachmentObject.Stride, ComputeBufferType.Structured, growFactor);
            AttachmentObjectsBuffer.SetData(objs);
        }
        if (AttachmentConstraintCount > 0)
        {
            Ensure(ref AttachmentConstraintsBuffer, AttachmentConstraintCount, GpuAttachmentConstraint.Stride, ComputeBufferType.Structured, growFactor);
            AttachmentConstraintsBuffer.SetData(cons);
        }
    }
    public void InitializeCloth(ClothRange[] ranges)
    {
        if (ranges == null || ranges.Length == 0) return;
        Ensure(ref ClothRangesBuffer, ranges.Length, ClothRange.Stride);
        ClothRangesBuffer.SetData(ranges);
        Ensure(ref ClothAabbsBuffer, ranges.Length, Aabb.Stride);
    }
    public void InitializeHash(ComputeShader cs, int particleCount, float particleRadius, float growFactor = 1.5f)
    {
        if (particleCount <= 0) return;

        HashSpacing = 2f * particleRadius;
        HashTableSize = Mathf.NextPowerOfTwo((int)(particleCount * 1.5f));

        Ensure(ref HashCellStarts, HashTableSize + 1, sizeof(uint), ComputeBufferType.Structured, growFactor);
        Ensure(ref HashCellEntries, particleCount, sizeof(uint), ComputeBufferType.Structured, growFactor);

        cs.SetInt(Sid.HashTableSize, HashTableSize);
        cs.SetFloat(Sid.HashSpacing, HashSpacing);
    }


    // ---- Binding Helpers ----
    public void BindParticlesTo(ComputeShader cs, params int[] kernels)
    {
        if (ParticleBuffer == null) return;
        for (int i = 0; i < kernels.Length; i++)
            cs.SetBuffer(kernels[i], Sid.Particles, ParticleBuffer);
    }
    public void BindDistanceSolve(ComputeShader cs, int solveDistanceKernel)
    {
        if (ConstraintBuffer != null)
            cs.SetBuffer(solveDistanceKernel, Sid.Constraints, ConstraintBuffer);
    }
    public void BindCollisionCore(ComputeShader cs, int solveKernel, int resetKernel, params int[] buildKernels)
    {
        foreach (var k in buildKernels)
        {
            cs.SetBuffer(k, Sid.CollisionConstraints, CollisionConstraintBuffer);
            cs.SetBuffer(k, Sid.CollisionCounts, CollisionCountBuffer);
        }
        cs.SetBuffer(solveKernel, Sid.CollisionConstraints, CollisionConstraintBuffer);
        cs.SetBuffer(solveKernel, Sid.CollisionCounts, CollisionCountBuffer);
        cs.SetBuffer(resetKernel, Sid.CollisionCounts, CollisionCountBuffer);

        cs.SetBuffer(solveKernel, Sid.ImpulseEvents, ImpulseEventBuffer);
    }
    public void BindAttachments(ComputeShader cs, params int[] kernels)
    {
        if (AttachmentConstraintCount <= 0) return;

        foreach (var k in kernels)
        {
            if (AttachmentObjectsBuffer != null)
                cs.SetBuffer(k, Sid.AttachmentObjects, AttachmentObjectsBuffer);

            cs.SetInt(Sid.AttachmentObjectCount, AttachmentObjectCount);

            if (AttachmentConstraintsBuffer != null)
                cs.SetBuffer(k, Sid.AttachmentConstraints, AttachmentConstraintsBuffer);

            cs.SetInt(Sid.AttachmentConstraintCount, AttachmentConstraintCount);
        }
    }
    public void BindDeltasTo(ComputeShader cs, params int[] kernels)
    {
        if (DeltaXBuffer == null || DeltaYBuffer == null || DeltaZBuffer == null || CountBuffer == null)
            return;

        foreach (var k in kernels)
        {
            cs.SetBuffer(k, Sid.DeltaX, DeltaXBuffer);
            cs.SetBuffer(k, Sid.DeltaY, DeltaYBuffer);
            cs.SetBuffer(k, Sid.DeltaZ, DeltaZBuffer);
            cs.SetBuffer(k, Sid.CountBuf, CountBuffer);
        }
    }
    public void BindHashTo(ComputeShader cs, params int[] kernels)
    {
        if (HashCellStarts == null || HashCellEntries == null) return;

        foreach (var k in kernels)
        {
            cs.SetBuffer(k, Sid.HashCellStarts, HashCellStarts);
            cs.SetBuffer(k, Sid.HashCellEntries, HashCellEntries);
        }
    }
    public void BindCloth(ComputeShader cs, int buildClothAabbsKernel)
    {
        if (ClothRangesBuffer == null || ClothAabbsBuffer == null || ParticleBuffer == null) return;
        cs.SetBuffer(buildClothAabbsKernel, Sid.Particles, ParticleBuffer);
        cs.SetBuffer(buildClothAabbsKernel, Sid.ClothRanges, ClothRangesBuffer);
        cs.SetBuffer(buildClothAabbsKernel, Sid.ClothAabbs, ClothAabbsBuffer);
    }


    // ---- Release ----
    public void ReleaseAll()
    {
        Release(ref ParticleBuffer);
        Release(ref ConstraintBuffer);
        Release(ref DeltaXBuffer);
        Release(ref DeltaYBuffer);
        Release(ref DeltaZBuffer);
        Release(ref CountBuffer);

        Release(ref CollisionConstraintBuffer);
        Release(ref CollisionCountBuffer);
        Release(ref ImpulseEventBuffer);
        Release(ref _appendCountScratch);
        Release(ref SphereBuffer);
        Release(ref CapsuleBuffer);
        Release(ref BoxBuffer);
        Release(ref MeshTriangleBuffer);
        Release(ref MeshRangeBuffer);

        Release(ref ClothRangesBuffer);
        Release(ref ClothAabbsBuffer);

        Release(ref AttachmentObjectsBuffer);
        Release(ref AttachmentConstraintsBuffer);

        Release(ref HashCellStarts);
        Release(ref HashCellEntries);


        AttachmentObjectCount = 0;
        AttachmentConstraintCount = 0;

        ParticleCount = 0;
        ConstraintCount = 0;
    }


    // ---- Zero / Counts ----
    public void ZeroAccumulators()
    {
        if (ParticleCount <= 0) return;
        ZeroUInt(ref DeltaXBuffer, ParticleCount);
        ZeroUInt(ref DeltaYBuffer, ParticleCount);
        ZeroUInt(ref DeltaZBuffer, ParticleCount);
        ZeroUInt(ref CountBuffer, ParticleCount);
    }
    public void SetCountsOn(ComputeShader cs)
    {
        cs.SetInt(Sid.ParticleCount, ParticleCount);
        cs.SetInt(Sid.ConstraintCount, ConstraintCount);
    }


    // ---- Collider Upload ----
    public void UploadSpheres(ComputeShader cs, int buildKernel, List<GpuSphereCollider> spheres)
    {
        int count = spheres?.Count ?? 0;
        cs.SetInt(Sid.SphereCount, count);
        if (count <= 0) return;

        Ensure(ref SphereBuffer, count, GpuSphereCollider.Stride);
        SphereBuffer.SetData(spheres);
        cs.SetBuffer(buildKernel, Sid.Spheres, SphereBuffer);
    }
    public void UploadCapsules(ComputeShader cs, int buildKernel, List<GpuCapsuleCollider> capsules)
    {
        int count = capsules?.Count ?? 0;
        cs.SetInt(Sid.CapsuleCount, count);
        if (count <= 0) return;

        Ensure(ref CapsuleBuffer, count, GpuCapsuleCollider.Stride);
        CapsuleBuffer.SetData(capsules);
        cs.SetBuffer(buildKernel, Sid.Capsules, CapsuleBuffer);
    }
    public void UploadBoxes(ComputeShader cs, int buildKernel, List<GpuBoxCollider> boxes)
    {
        int count = boxes?.Count ?? 0;
        cs.SetInt(Sid.BoxCount, count);
        if (count <= 0) return;

        Ensure(ref BoxBuffer, count, GpuBoxCollider.Stride);
        BoxBuffer.SetData(boxes);
        cs.SetBuffer(buildKernel, Sid.Boxes, BoxBuffer);
    }
    public void UploadMeshes(ComputeShader cs, int buildKernel, List<GpuTriangle> tris, List<GpuMeshRange> ranges)
    {
        int triCount = tris?.Count ?? 0;
        int meshCount = ranges?.Count ?? 0;

        cs.SetInt(Sid.MeshCount, meshCount);
        // Sid.TriangleCount ist optional—du nutzt ihn derzeit nicht im Shader

        if (triCount <= 0 || meshCount <= 0) return;

        Ensure(ref MeshTriangleBuffer, triCount, GpuTriangle.Stride);
        Ensure(ref MeshRangeBuffer, meshCount, GpuMeshRange.Stride);

        MeshTriangleBuffer.SetData(tris);
        MeshRangeBuffer.SetData(ranges);

        cs.SetBuffer(buildKernel, Sid.MeshTriangles, MeshTriangleBuffer);
        cs.SetBuffer(buildKernel, Sid.MeshRanges, MeshRangeBuffer);
    }


    // ---- Helpers ----
    public static void Ensure(ref ComputeBuffer buf, int neededCount, int stride, ComputeBufferType type = ComputeBufferType.Structured, float growFactor = 1.5f)
    {
        int current = (buf == null) ? 0 : buf.count;
        if (current >= neededCount && buf != null)
            return;

        int newCount = Mathf.Max(neededCount, Mathf.Max(64, current > 0 ? Mathf.CeilToInt(current * growFactor) : neededCount));
        if (buf != null)
        {
            buf.Release();
            buf = null;
        }

        buf = new ComputeBuffer(newCount, stride, type);

        Debug.Log("[GpuXpbdBufferSet] Allocated ComputeBuffer of size " + newCount + " (stride " + stride + ", type " + type + ")");
    }
    public static void Release(ref ComputeBuffer buf)
    {
        if (buf == null)
            return;
        buf.Release();
        buf = null;
    }
    public static void ZeroUInt(ref ComputeBuffer buffer, int count)
    {
        if (buffer == null)
            return;
        var zeros = new uint[count];
        buffer.SetData(zeros);
    }
    
    public void ResetImpulseEvents() => ImpulseEventBuffer?.SetCounterValue(0);
    public int GetAppendCount(ComputeBuffer append)
    {
        _appendCountScratch ??= new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(append, _appendCountScratch, 0);
        var tmp = new uint[1]; _appendCountScratch.GetData(tmp);
        return (int)tmp[0];
    }
    public int GetImpulseEventCount() => GetAppendCount(ImpulseEventBuffer);

}