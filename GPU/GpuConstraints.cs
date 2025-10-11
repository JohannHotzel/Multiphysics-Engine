using UnityEngine;

public struct GpuDistanceConstraint
{
    public uint i, j;
    public float rest;
    public float compliance;

    public const int Stride = sizeof(uint) * 2 + sizeof(float) * 2; // float*2 + float3*2 = 20 bytes

    public GpuDistanceConstraint(uint a, uint b, float r, float c)
    {
        i = a;
        j = b;
        rest = r;
        compliance = c;
    }
}

public struct GpuCollisionConstraint
{
    public Vector3 target;
    public Vector3 normal;
    public float radius;

    public const int Stride = (3 + 3 + 1) * sizeof(float); // 28
}


public struct GpuAttachmentObject
{
    public Matrix4x4 world;             // Transform.localToWorldMatrix
    public const int Stride = 64;
}

public struct GpuAttachmentConstraint
{
    public uint particle;                
    public uint objectIndex;             
    public Vector3 localPoint;          
    public const int Stride = sizeof(uint) * 2 + sizeof(float) * 3; // 8 + 12 = 20
}
