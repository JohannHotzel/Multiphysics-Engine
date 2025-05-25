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
            //Vector3 normal = delta / dist;
           // if (closest.isInside)
           //     normal = -normal;

            return new CollisionConstraint(p, closest.point, closest.normal, 0f, solver);
        }

        return null;
    }

    public class ClosestPoint
    {
        public Vector3 point;
        public Vector3 normal;
        public bool isInside;
    }
    public static ClosestPoint GetClosestPointOnMesh(MeshCollider meshCollider, Vector3 point)
    {
        Mesh mesh = meshCollider.sharedMesh;
        mesh.RecalculateNormals();
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        float minDist = float.MaxValue;
        Vector3 bestPoint = tr.TransformPoint(verts[0]);
        Vector3 bestNormal = Vector3.up;
        bool isInside = false;

        Vector3 GetVertexNormal(int idx)
            => tr.TransformDirection(norms[idx]).normalized;

        for (int i = 0; i < tris.Length; i += 3)
        {
            int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
            Vector3 a = tr.TransformPoint(verts[i0]);
            Vector3 b = tr.TransformPoint(verts[i1]);
            Vector3 c = tr.TransformPoint(verts[i2]);

            Vector3 faceN = Vector3.Cross(b - a, c - a).normalized;
            float signedDist = Vector3.Dot(faceN, point - a);
            Vector3 proj = point - signedDist * faceN;

            if (IsPointInTriangle(proj, a, b, c))
            {
                float d = Mathf.Abs(signedDist);
                if (d < minDist)
                {
                    minDist = d;
                    bestPoint = proj;
                    isInside = signedDist < 0f;
                    bestNormal = faceN;
                }
            }
            else
            {
                TryEdge(a, b, i0, i1, point, ref minDist, ref bestPoint, ref bestNormal, ref isInside, GetVertexNormal);
                TryEdge(b, c, i1, i2, point, ref minDist, ref bestPoint, ref bestNormal, ref isInside, GetVertexNormal);
                TryEdge(c, a, i2, i0, point, ref minDist, ref bestPoint, ref bestNormal, ref isInside, GetVertexNormal);
            }
        }

        return new ClosestPoint
        {
            point = bestPoint,
            normal = bestNormal.normalized,
            isInside = isInside
        };
    }

    private static void TryEdge(Vector3 a, Vector3 b, int ia, int ib, Vector3 p,
        ref float minDist, ref Vector3 bestPoint, ref Vector3 bestNormal, ref bool isInside,
        System.Func<int, Vector3> getVertNormal)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        Vector3 cand = a + ab * t;
        float d = Vector3.Distance(p, cand);
        if (d < minDist)
        {
            minDist = d;
            bestPoint = cand;
            isInside = false;
            //(Gouraud-Shading)
            Vector3 na = getVertNormal(ia);
            Vector3 nb = getVertNormal(ib);
            bestNormal = Vector3.Lerp(na, nb, t).normalized;
        }
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
