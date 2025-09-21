using UnityEngine;

public struct GpuDistanceConstraint
{
    public uint i, j;
    public float rest;
    public float compliance;

    public GpuDistanceConstraint(uint a, uint b, float r, float c)
    {
        i = a;
        j = b;
        rest = r;
        compliance = c;
    }
}
