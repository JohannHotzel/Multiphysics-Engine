using UnityEngine;

public static class GpuXpbdShaderIds
{
    public const int THREADS = 256;

    public static class Kid
    {
        public static int Predict;
        public static int Integrate;
        public static int SolveDistanceJacobi;
        public static int ApplyDeltas;
        public static int UpdateVelocities;

        public static int BuildSphereConstraints;
        public static int BuildCapsuleConstraints;
        public static int BuildBoxConstraints;
        public static int BuildMeshConstraints;

        public static int SolveCollisionConstraints;
        public static int SolveParticleCollisionsNaive;
        public static int ResetCollisionCounts;
        public static int BuildClothAabbs;
        
        public static int SetAttachmentPositions;

        public static void Init(ComputeShader cs)
        {
            Predict = cs.FindKernel("Predict");
            Integrate = cs.FindKernel("Integrate");
            SolveDistanceJacobi = cs.FindKernel("SolveDistanceJacobi");
            ApplyDeltas = cs.FindKernel("ApplyDeltas");
            UpdateVelocities = cs.FindKernel("UpdateVelocities");

            BuildSphereConstraints = cs.FindKernel("BuildSphereConstraints");
            BuildCapsuleConstraints = cs.FindKernel("BuildCapsuleConstraints");
            BuildBoxConstraints = cs.FindKernel("BuildBoxConstraints");
            BuildMeshConstraints = cs.FindKernel("BuildMeshConstraints");

            SolveCollisionConstraints = cs.FindKernel("SolveCollisionConstraints");
            SolveParticleCollisionsNaive = cs.FindKernel("SolveParticleCollisionsNaive");
            ResetCollisionCounts = cs.FindKernel("ResetCollisionCounts");
            BuildClothAabbs = cs.FindKernel("BuildClothAabbs");

            SetAttachmentPositions = cs.FindKernel("SetAttachmentPositions");
        }
    }

    public static class Sid
    {
        // Particles
        public static readonly int Particles = Shader.PropertyToID("particles");
        public static readonly int ParticleCount = Shader.PropertyToID("particleCount");


        // Distance constraints
        public static readonly int Constraints = Shader.PropertyToID("constraints");
        public static readonly int ConstraintCount = Shader.PropertyToID("constraintCount");

        public static readonly int DeltaX = Shader.PropertyToID("deltaX");
        public static readonly int DeltaY = Shader.PropertyToID("deltaY");
        public static readonly int DeltaZ = Shader.PropertyToID("deltaZ");
        public static readonly int CountBuf = Shader.PropertyToID("countBuf");


        // Colliders / Cloth
        public static readonly int ClothRanges = Shader.PropertyToID("clothRanges");
        public static readonly int ClothAabbs = Shader.PropertyToID("clothAabbs");

        public static readonly int CollisionConstraints = Shader.PropertyToID("collisionConstraints");
        public static readonly int CollisionCounts = Shader.PropertyToID("collisionCounts");

        public static readonly int Spheres = Shader.PropertyToID("spheres");
        public static readonly int SphereCount = Shader.PropertyToID("sphereCount");
        public static readonly int Capsules = Shader.PropertyToID("capsules");
        public static readonly int CapsuleCount = Shader.PropertyToID("capsuleCount");
        public static readonly int Boxes = Shader.PropertyToID("boxes");
        public static readonly int BoxCount = Shader.PropertyToID("boxCount");
        public static readonly int MeshTriangles = Shader.PropertyToID("meshTriangles");
        public static readonly int MeshRanges = Shader.PropertyToID("meshRanges");
        public static readonly int MeshCount = Shader.PropertyToID("meshCount");
        public static readonly int TriangleCount = Shader.PropertyToID("triangleCount");


        // Attachment Constraints
        public static readonly int AttachmentObjects = Shader.PropertyToID("attachmentObjects");
        public static readonly int AttachmentObjectCount = Shader.PropertyToID("attachmentObjectCount");
        public static readonly int AttachmentConstraints = Shader.PropertyToID("attachmentConstraints");
        public static readonly int AttachmentConstraintCount = Shader.PropertyToID("attachmentConstraintCount");


        // Params
        public static readonly int Omega = Shader.PropertyToID("omega");
        public static readonly int Dt = Shader.PropertyToID("dt");
        public static readonly int Dts = Shader.PropertyToID("dts");
        public static readonly int Dts2 = Shader.PropertyToID("dts2");
        public static readonly int Gravity = Shader.PropertyToID("gravity");
        public static readonly int VMax = Shader.PropertyToID("vMax");
        public static readonly int CollisionMargin = Shader.PropertyToID("collisionMargin");
    }
}