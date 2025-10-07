using System.Collections.Generic;
using UnityEngine;
using static GpuXpbdShaderIds;   // für Sid/Kid/THREADS

public sealed class GpuXpbdBufferSet
{
    // ---- Public Readonly API ----
    public int ParticleCount { get; private set; }
    public int ConstraintCount { get; private set; }

    // ---- Core Buffers ----
    public ComputeBuffer ParticleBuffer;
    public ComputeBuffer ConstraintBuffer;
    public ComputeBuffer DeltaXBuffer, DeltaYBuffer, DeltaZBuffer, CountBuffer;

    // ---- Collision Buffers ----
    public ComputeBuffer CollisionConstraintBuffer, CollisionCountBuffer;
    public ComputeBuffer SphereBuffer, CapsuleBuffer, BoxBuffer;
    public ComputeBuffer MeshTriangleBuffer, MeshRangeBuffer;

    // ---- Cloth/Broadphase ----
    public ComputeBuffer ClothRangesBuffer, ClothAabbsBuffer;

    // ---- Internal ----
    private const int MAX_COLLISIONS = 8;



    // -------- Init / Teardown --------
    public void InitializeParticlesAndConstraints(GpuParticle[] particles, GpuDistanceConstraint[] constraints, float growFactor = 1.5f)
    {
        ParticleCount = particles?.Length ?? 0;
        ConstraintCount = constraints?.Length ?? 0;

        if (ParticleCount <= 0)
            return;

        // Particles
        Ensure(ref ParticleBuffer, ParticleCount, GpuParticle.Stride, ComputeBufferType.Structured, growFactor);
        ParticleBuffer.SetData(particles);

        // Constraints
        if (ConstraintCount > 0)
        {
            Ensure(ref ConstraintBuffer, ConstraintCount, GpuDistanceConstraint.Stride, ComputeBufferType.Structured, growFactor);
            ConstraintBuffer.SetData(constraints);
        }

        // Accumulators
        Ensure(ref DeltaXBuffer, ParticleCount, sizeof(uint));
        Ensure(ref DeltaYBuffer, ParticleCount, sizeof(uint));
        Ensure(ref DeltaZBuffer, ParticleCount, sizeof(uint));
        Ensure(ref CountBuffer, ParticleCount, sizeof(uint));
        ZeroAccumulators();

        // Collision constraints (per particle)
        int colCap = Mathf.Max(1, ParticleCount * MAX_COLLISIONS);
        Ensure(ref CollisionConstraintBuffer, colCap, GpuCollisionConstraint.Stride);
        Ensure(ref CollisionCountBuffer, ParticleCount, sizeof(uint));
        ZeroUInt(ref CollisionCountBuffer, ParticleCount);
    }
    public void InitializeCloth(ClothRange[] ranges, ComputeShader cs, int buildClothAabbsKernel)
    {
        if (ranges == null || ranges.Length == 0) return;

        Ensure(ref ClothRangesBuffer, ranges.Length, ClothRange.Stride);
        ClothRangesBuffer.SetData(ranges);

        Ensure(ref ClothAabbsBuffer, ranges.Length, Aabb.Stride);
        // Bind für BuildClothAabbs
        cs.SetBuffer(buildClothAabbsKernel, Sid.Particles, ParticleBuffer);
        cs.SetBuffer(buildClothAabbsKernel, Sid.ClothRanges, ClothRangesBuffer);
        cs.SetBuffer(buildClothAabbsKernel, Sid.ClothAabbs, ClothAabbsBuffer);
    }
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
        Release(ref SphereBuffer);
        Release(ref CapsuleBuffer);
        Release(ref BoxBuffer);
        Release(ref MeshTriangleBuffer);
        Release(ref MeshRangeBuffer);

        Release(ref ClothRangesBuffer);
        Release(ref ClothAabbsBuffer);

        ParticleCount = 0;
        ConstraintCount = 0;
    }


    // -------- Zero / Counts --------
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


    // -------- Binding Helpers --------
    public void BindParticlesTo(ComputeShader cs, params int[] kernels)
    {
        if (ParticleBuffer == null) return;
        for (int i = 0; i < kernels.Length; i++)
            cs.SetBuffer(kernels[i], Sid.Particles, ParticleBuffer);
    }
    public void BindDistanceSolve(ComputeShader cs, int solveDistanceKernel, int applyDeltasKernel)
    {
        if (ConstraintBuffer != null)
            cs.SetBuffer(solveDistanceKernel, Sid.Constraints, ConstraintBuffer);

        cs.SetBuffer(solveDistanceKernel, Sid.DeltaX, DeltaXBuffer);
        cs.SetBuffer(solveDistanceKernel, Sid.DeltaY, DeltaYBuffer);
        cs.SetBuffer(solveDistanceKernel, Sid.DeltaZ, DeltaZBuffer);
        cs.SetBuffer(solveDistanceKernel, Sid.CountBuf, CountBuffer);

        cs.SetBuffer(applyDeltasKernel, Sid.DeltaX, DeltaXBuffer);
        cs.SetBuffer(applyDeltasKernel, Sid.DeltaY, DeltaYBuffer);
        cs.SetBuffer(applyDeltasKernel, Sid.DeltaZ, DeltaZBuffer);
        cs.SetBuffer(applyDeltasKernel, Sid.CountBuf, CountBuffer);
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
    }


    // -------- Collider Upload --------
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


    // -------- Helpers --------
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
    }
    public static void Release(ref ComputeBuffer buf)
    {
        if (buf == null)
            return;
        buf.Release();
        buf = null;
    }
    public static void ReleaseAll(params ComputeBuffer[] buffers)
    {
        if (buffers == null)
            return;
        foreach (var b in buffers)
        {
            if (b != null)
                b.Release();
        }
    }
    public static void ZeroUInt(ref ComputeBuffer buffer, int count)
    {
        if (buffer == null)
            return;
        var zeros = new uint[count];
        buffer.SetData(zeros);
    }

}
