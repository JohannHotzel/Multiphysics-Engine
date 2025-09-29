using UnityEngine;

public struct GpuCollisionConstraint
{
    public Vector3 target; 
    public Vector3 normal;
    public float radius;

    public const int Stride = (3 + 3 + 1) * sizeof(float); // 28
}
