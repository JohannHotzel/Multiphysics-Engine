using UnityEngine;

public static class CollisionDetector
{
    public static void detectCollisions(Particle p)
    {
        Vector3 worldPos = p.positionX;
        float radius = p.radius;

        Collider[] hits = Physics.OverlapSphere(worldPos, radius);
        if (hits == null || hits.Length == 0)
            return;

        Collider col = hits[0];
        Vector3 closest = (col is MeshCollider meshCol && meshCol.sharedMesh != null)
            ? GetClosestPointOnMesh(meshCol, worldPos)
            : Vector3.zero;

        if (closest == Vector3.zero) return;

        Vector3 delta = worldPos - closest;
        float dist = delta.magnitude;

        if (dist < radius)
        {
            Vector3 normal = delta / dist;
            float penetration = radius - dist;
            p.positionX += normal * penetration;
        }
    }

    private static Vector3 GetClosestPointOnMesh(MeshCollider meshCollider, Vector3 point)
    {
        Mesh mesh = meshCollider.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        float minDist = float.MaxValue;
        Vector3 bestPoint = tr.TransformPoint(verts[0]);

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = tr.TransformPoint(verts[tris[i]]);
            Vector3 b = tr.TransformPoint(verts[tris[i + 1]]);
            Vector3 c = tr.TransformPoint(verts[tris[i + 2]]);

            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            float signedDist = Vector3.Dot(normal, point - a);
            Vector3 proj = point - signedDist * normal;

            if (IsPointInTriangle(proj, a, b, c))
            {
                float d = Mathf.Abs(signedDist);
                if (d < minDist)
                {
                    minDist = d;
                    bestPoint = proj;
                }
            }
            else
            {
                TryEdge(a, b, point, ref minDist, ref bestPoint);
                TryEdge(b, c, point, ref minDist, ref bestPoint);
                TryEdge(c, a, point, ref minDist, ref bestPoint);
            }
        }
        return bestPoint;
    }
    private static void TryEdge(Vector3 a, Vector3 b, Vector3 p, ref float minDist, ref Vector3 bestPoint)
    {
        Vector3 cand = ClosestPointOnSegment(a, b, p);
        float d = Vector3.Distance(p, cand);
        if (d < minDist)
        {
            minDist = d;
            bestPoint = cand;
        }
    }
    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }
    private static bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = c - a;
        Vector3 v1 = b - a;
        Vector3 v2 = p - a;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0f) && (v >= 0f) && (u + v <= 1f);
    }
}
