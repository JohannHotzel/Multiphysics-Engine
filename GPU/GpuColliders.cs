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
    public Vector3 center;
    public Vector3 axisRight;
    public Vector3 axisUp;
    public Vector3 axisForward;
    public Vector3 halfExtents;

    // 3*3 + 3 + 3 = 12 floats
    public const int Stride = sizeof(float) * (3 + 3 + 3 + 3 + 3);

    public GpuBoxCollider(Vector3 c, Vector3 r, Vector3 u, Vector3 f, Vector3 he)
    {
        center = c;
        axisRight = r;
        axisUp = u;
        axisForward = f;
        halfExtents = he;
    }
}

public struct GpuTriangle
{
    public Vector3 a, b, c;
    public const int Stride = sizeof(float) * 9; // 3 * float3

    public GpuTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        this.a = a; this.b = b; this.c = c;
    }
}

public struct GpuMeshRange
{
    public uint start;
    public uint count;
    public const int Stride = sizeof(uint) * 2;

    public GpuMeshRange(uint s, uint c)
    {
        start = s; count = c;
    }
}






//Axis-Aligned Bounding Box Check
public struct Aabb
{
    public Vector3 mn;
    public Vector3 mx;
    public const int Stride = sizeof(float) * 6; // float3 + float3
    public Aabb(Vector3 min, Vector3 max)
    {
        mn = min;
        mx = max;
    }
}
//This will be later changed to a aggregat strure used for Softbodies and Cloths
public struct ClothRange
{
    public uint start;
    public uint count;
    public const int Stride = sizeof(int) * 2; // int + int
    public ClothRange(uint s, uint c)
    {
        start = s;
        count = c;
    }
}
