using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuSphereCollider
{
    public Vector3 center;
    public float radius;
    public int rbIndex;

    public static readonly int Stride = Marshal.SizeOf<GpuSphereCollider>();
    public GpuSphereCollider(Vector3 pos, float rad, int index)
    {
        center = pos;
        radius = rad;
        rbIndex = index;
    }
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuCapsuleCollider
{
    public Vector3 p0;
    public Vector3 p1;
    public float radius;
    public int rbIndex;

    public static readonly int Stride = Marshal.SizeOf<GpuCapsuleCollider>();
    public GpuCapsuleCollider(Vector3 point0, Vector3 point1, float rad, int index)
    {
        p0 = point0;
        p1 = point1;
        radius = rad;
        rbIndex = index;
    }
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuBoxCollider
{
    public Vector3 center;
    public Vector3 axisRight;
    public Vector3 axisUp;
    public Vector3 axisForward;
    public Vector3 halfExtents;
    public int rbIndex;

    public static readonly int Stride = Marshal.SizeOf<GpuBoxCollider>();
    public GpuBoxCollider(Vector3 c, Vector3 r, Vector3 u, Vector3 f, Vector3 he, int index)
    {
        center = c;
        axisRight = r;
        axisUp = u;
        axisForward = f;
        halfExtents = he;
        rbIndex = index;
    }
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuTriangle
{
    public Vector3 a, b, c;

    public static readonly int Stride = Marshal.SizeOf<GpuTriangle>();
    public GpuTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        this.a = a; this.b = b; this.c = c;
    }
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GpuMeshRange
{
    public uint start;
    public uint count;
    public int rbIndex;

    public static readonly int Stride = Marshal.SizeOf<GpuMeshRange>();
    public GpuMeshRange(uint s, uint c, int index)
    {
        start = s;
        count = c;
        rbIndex = index;
    }
}




//Axis-Aligned Bounding Box Check
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Aabb
{
    public Vector3 mn;
    public Vector3 mx;

    public static readonly int Stride = Marshal.SizeOf<Aabb>();
    public Aabb(Vector3 min, Vector3 max)
    {
        mn = min;
        mx = max;
    }
}
//This will be later changed to a aggregat strure used for Softbodies and Cloths
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AggregateRange
{
    public uint start;
    public uint count;

    public static readonly int Stride = Marshal.SizeOf<AggregateRange>();
    public AggregateRange(uint s, uint c)
    {
        start = s;
        count = c;
    }
}
