using UnityEngine;

public static class CollisionDetector
{
    public static CollisionConstraint detectCollisions(Particle p, Vector3 worldPos, XPBDSolver solver)
    {
        float radius = p.radius;

        Collider[] hits = Physics.OverlapSphere(worldPos, radius * 1.01f);
        if (hits == null || hits.Length == 0)
            return null;

        Collider col = hits[0];
        ClosestPoint closest = (col is MeshCollider meshCol && meshCol.sharedMesh != null)
            ? GetClosestPointOnMesh(meshCol, worldPos)
            : null;

        if (closest == null) return null;

        Vector3 delta = worldPos - closest.point;
        float dist = delta.magnitude;

        if (dist <= radius)
        {
            Vector3 normal = delta / dist;
            if (closest.isInside)
                normal = -normal;

            return new CollisionConstraint(p, closest.point, normal, 0f, solver);
        }

        return null;
    }

    public class ClosestPoint
    {
        public Vector3 point;
        public bool isInside;
    }
    public static ClosestPoint GetClosestPointOnMesh(MeshCollider meshCollider, Vector3 point)
    {
        Mesh mesh = meshCollider.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        float minDist = float.MaxValue;
        Vector3 bestPoint = tr.TransformPoint(verts[0]);
        bool isInside = false;

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
                    isInside = signedDist < 0f;
                }
            }
            else
            {
                TryEdge(a, b, point, ref minDist, ref bestPoint, ref isInside);
                TryEdge(b, c, point, ref minDist, ref bestPoint, ref isInside);
                TryEdge(c, a, point, ref minDist, ref bestPoint, ref isInside);
            }
        }

        return new ClosestPoint
        {
            point = bestPoint,
            isInside = isInside
        };

    }
    private static void TryEdge(Vector3 a, Vector3 b, Vector3 p, ref float minDist, ref Vector3 bestPoint, ref bool isInside)
    {
        Vector3 cand = ClosestPointOnSegment(a, b, p);
        float d = Vector3.Distance(p, cand);
        if (d < minDist)
        {
            minDist = d;
            bestPoint = cand;
            isInside = false;
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
