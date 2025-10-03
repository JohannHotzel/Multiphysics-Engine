using UnityEngine;

public struct GpuSphereCollider
{
    public Vector3 center;
    public float radius;

    public const int Stride = sizeof(float) * 4; // float3 + float

    public GpuSphereCollider(Vector3 pos, float rad)
    {
        center = pos;
        radius = rad;
    }
}

public struct GpuCapsuleCollider
{
    public Vector3 p0;
    public Vector3 p1;
    public float radius;

    public const int Stride = sizeof(float) * (3 + 3 + 1); // float3 + float3 + float

    public GpuCapsuleCollider(Vector3 point0, Vector3 point1, float rad)
    {
        p0 = point0;
        p1 = point1;
        radius = rad;
    }
}

public struct GpuBoxCollider
{

}

public struct GpuMeshCollider
{

}
