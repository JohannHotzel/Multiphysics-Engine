#ifndef XPBD_COLLISION_CHECKS_INCLUDED
#define XPBD_COLLISION_CHECKS_INCLUDED


// === Sphere ===
inline bool CollideSphere(float3 x, float pr, SphereCollider sc, out float3 target, out float3 n)
{
    float3 d = x - sc.center;
    float dist = length(d);
    const float EPS = 1e-6f;

    float rCombined = sc.radius + pr;
    float pen = rCombined - dist;
    if (pen <= 0.0f)
        return false;

    n = (dist > EPS) ? (d / dist) : float3(0, 1, 0);
    target = sc.center + n * sc.radius;
    return true;
}

// === Capsule ===
inline float3 ClosestPointOnSegment(float3 a, float3 b, float3 p)
{
    float3 ab = b - a;
    float t = dot(p - a, ab) / max(dot(ab, ab), 1e-8);
    t = clamp(t, 0.0, 1.0);
    return a + t * ab;
}

inline bool CollideCapsule(float3 x, float pr, CapsuleCollider c, out float3 target, out float3 n)
{
    float3 q = ClosestPointOnSegment(c.p0, c.p1, x);
    float3 d = x - q;
    float dist = length(d);
    const float EPS = 1e-6f;

    float rCombined = c.r + pr;
    float pen = rCombined - dist;
    if (pen <= 0.0f)
        return false;

    n = (dist > EPS) ? (d / dist) : float3(0, 1, 0);
    target = q + n * c.r;
    return true;
}

// === Box ===
inline float3 ClosestPointOnOBB(float3 p, BoxCollider b, out float3 normal, out float signedDist)
{
    float3 d = p - b.center;
    float x = dot(d, b.axisRight);
    float y = dot(d, b.axisUp);
    float z = dot(d, b.axisForward);
    float3 q = float3(x, y, z);
    float3 c = clamp(q, -b.halfExtents, b.halfExtents);

    float3 worldC = b.center + b.axisRight * c.x + b.axisUp * c.y + b.axisForward * c.z;
    float3 over = q - c;

    if (all(over == 0))
    {
        float3 distToFace = b.halfExtents - abs(q);
        if (distToFace.x < distToFace.y && distToFace.x < distToFace.z)
            normal = (x > 0) ? b.axisRight : -b.axisRight;
        else if (distToFace.y < distToFace.z)
            normal = (y > 0) ? b.axisUp : -b.axisUp;
        else
            normal = (z > 0) ? b.axisForward : -b.axisForward;

        signedDist = -min(distToFace.x, min(distToFace.y, distToFace.z));
    }
    else
    {
        float3 nLocal = normalize(over);
        normal = normalize(b.axisRight * nLocal.x + b.axisUp * nLocal.y + b.axisForward * nLocal.z);
        signedDist = length(p - worldC);
    }

    return worldC;
}

inline bool CollideBox(float3 x, float pr, BoxCollider b, out float3 target, out float3 n)
{
    float signedDist;
    float3 cp = ClosestPointOnOBB(x, b, n, signedDist);
    float dist = length(x - cp);
    target = cp;
    if (all(x == cp))
        return true;
    return (dist < pr + 1e-6f);
}

// === Triangle ===
inline float3 ClosestPointOnTriangle(float3 p, float3 a, float3 b, float3 c)
{
    float3 ab = b - a;
    float3 ac = c - a;
    float3 ap = p - a;
    float d1 = dot(ab, ap);
    float d2 = dot(ac, ap);
    if (d1 <= 0 && d2 <= 0)
        return a;
    float3 bp = p - b;
    float d3 = dot(ab, bp);
    float d4 = dot(ac, bp);
    if (d3 >= 0 && d4 <= d3)
        return b;
    float vc = d1 * d4 - d3 * d2;
    if (vc <= 0 && d1 >= 0 && d3 <= 0)
    {
        float v = d1 / (d1 - d3);
        return a + v * ab;
    }
    float3 cp = p - c;
    float d5 = dot(ab, cp);
    float d6 = dot(ac, cp);
    if (d6 >= 0 && d5 <= d6)
        return c;
    float vb = d5 * d2 - d1 * d6;
    if (vb <= 0 && d2 >= 0 && d6 <= 0)
    {
        float w = d2 / (d2 - d6);
        return a + w * ac;
    }
    float va = d3 * d6 - d5 * d4;
    if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
    {
        float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
        return b + w * (c - b);
    }
    float3 n = normalize(cross(ab, ac));
    float dist = dot(p - a, n);
    return p - dist * n;
}

inline bool CollideTriangle(float3 x, float pr, Triangle tri, out float3 target, out float3 n)
{
    float3 cp = ClosestPointOnTriangle(x, tri.a, tri.b, tri.c);
    float3 d = x - cp;
    float dist = length(d);
    if (dist <= pr + 1e-6f)
    {
        float3 ab = tri.b - tri.a;
        float3 ac = tri.c - tri.a;
        float3 nn = cross(ab, ac);
        float len = length(nn);
        n = (len > 1e-6f) ? (nn / len) : float3(0, 1, 0);
        target = cp;
        return true;
    }
    target = cp;
    n = float3(0, 1, 0);
    return false;
}

#endif
