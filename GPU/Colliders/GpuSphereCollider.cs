using UnityEngine;

public struct GpuSphereCollider
{
    public Vector3 position;
    public float radius;
    public GpuSphereCollider(Vector3 pos, float rad)
    {
        position = pos;
        radius = rad;
    }
}
