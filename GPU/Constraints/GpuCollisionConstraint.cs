using UnityEngine;

public struct GpuCollisionConstraint
{
    public Vector3 target; 
    public Vector3 normal;
    public float radius;   
    public uint valid;   

    public const int Stride = (3 + 3 + 1) * sizeof(float) + sizeof(uint); // 12+12+4+4 = 32
}
