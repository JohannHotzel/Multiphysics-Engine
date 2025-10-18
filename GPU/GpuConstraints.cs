using System.Runtime.InteropServices;
using UnityEngine;


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuDistanceConstraint
{
    public uint i, j;
    public float rest;
    public float compliance;

    public static readonly int Stride = Marshal.SizeOf<GpuDistanceConstraint>();
    public GpuDistanceConstraint(uint a, uint b, float r, float c)
    {
        i = a;
        j = b;
        rest = r;
        compliance = c;
    }
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuCollisionConstraint
{
    public Vector3 target;
    public Vector3 normal;
    public float radius;
    public int rbIndex;

    public static readonly int Stride = Marshal.SizeOf<GpuCollisionConstraint>();
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuImpulseEvent
{
    public int rbIndex;
    public Vector3 pointWS;
    public Vector3 J;

    public static readonly int Stride = Marshal.SizeOf<GpuImpulseEvent>();
}




[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuAttachmentObject
{
    public Matrix4x4 world;             // Transform.localToWorldMatrix
    public static readonly int Stride = Marshal.SizeOf<GpuAttachmentObject>();
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuAttachmentConstraint
{
    public uint particle;                
    public uint objectIndex;             
    public Vector3 localPoint;

    public static readonly int Stride = Marshal.SizeOf<GpuAttachmentConstraint>();
}
