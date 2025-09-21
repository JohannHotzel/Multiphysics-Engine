using UnityEngine;

public struct GpuCollisionConstraint
{
    public int particleIndex;
    public Vector3 point;
    public Vector3 normal;
    public float radius;

    public GpuCollisionConstraint(int pIdx, Vector3 pt, Vector3 n, float r)
    {
        particleIndex = pIdx;
        point = pt;
        normal = n;
        radius = r;
    }
}
