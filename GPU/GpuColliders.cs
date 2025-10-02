using UnityEngine;

public struct GpuSphereCollider
{
    public Vector3 position;
    public float radius;

    public const int Stride = sizeof(float) * 4; // float3 + float

    public GpuSphereCollider(Vector3 pos, float rad)
    {
        position = pos;
        radius = rad;
    }
}

public struct GpuCapsulCollider
{

}

public struct GpuBoxCollider
{

}

public struct GpuMeshCollider
{

}
