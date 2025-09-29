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
