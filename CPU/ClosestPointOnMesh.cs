using System.Collections.Generic;
using UnityEngine;

public class ClosestPointOnMesh
{


    //--------------------------------------------------------------------------------------------------------------------------------------------//
    //---------------------------------- Returns a single closest point --------------------------------------------------------------------------//
    //--------------------------------------------------------------------------------------------------------------------------------------------//
    public class ClosestPoint
    {
        public Vector3 point;
        public bool isInside;
        public float distance;
        public Vector3 normal;
    }
    
    //Returns the closest point on the mesh (Checks Planes and Edges)
    public static ClosestPoint GetClosestPointOnMesh(MeshCollider meshCollider, Vector3 point)
    {
        Mesh mesh = meshCollider.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        float minDist = float.MaxValue;
        Vector3 bestPoint = tr.TransformPoint(verts[0]);
        bool inside = false;

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
                    inside = signedDist <= 0f;
                }
            }

            else
            {
                TryEdge(a, b, point, ref minDist, ref bestPoint, ref inside);
                TryEdge(b, c, point, ref minDist, ref bestPoint, ref inside);
                TryEdge(c, a, point, ref minDist, ref bestPoint, ref inside);
            }
        }

        return new ClosestPoint
        {
            point = bestPoint,
            isInside = inside
        };
    }
    
    //Returns the closest point on the mesh with the correct normal
    public static ClosestPoint GetClosestPointOnMeshNormal(MeshCollider meshCollider, Vector3 point)
    {
        Mesh mesh = meshCollider.sharedMesh;
        //mesh.RecalculateNormals();
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        Vector3[] averagedNormals = GetAveragedNormals(verts, norms);
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        float minDist = float.MaxValue;
        Vector3 bestPoint = tr.TransformPoint(verts[0]);
        Vector3 bestNormal = Vector3.up;

        Vector3 GetVertexNormal(int idx)
            => tr.TransformDirection(averagedNormals[idx]).normalized;

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
                    bestNormal = faceN;
                }
            }
            else
            {
                TryEdge(a, b, i0, i1, point, ref minDist, ref bestPoint, ref bestNormal, GetVertexNormal);
                TryEdge(b, c, i1, i2, point, ref minDist, ref bestPoint, ref bestNormal, GetVertexNormal);
                TryEdge(c, a, i2, i0, point, ref minDist, ref bestPoint, ref bestNormal, GetVertexNormal);
            }
        }

        return new ClosestPoint
        {
            point = bestPoint,
            normal = bestNormal.normalized,
            distance = minDist
        };
    }

    //---------------------------------- UTILS: (Returns a single closest point) -----------------------------------------------------------------//
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
    private static void TryEdge(Vector3 a, Vector3 b, Vector3 p, ref float minDist, ref Vector3 bestPoint, ref bool inside)
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
            inside = false;
        }
    }
    private static void TryEdge(Vector3 a, Vector3 b, int ia, int ib, Vector3 p, ref float minDist, ref Vector3 bestPoint, ref Vector3 bestNormal, System.Func<int, Vector3> getVertNormal)
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
            //(Gouraud-Shading)
            Vector3 na = getVertNormal(ia);
            Vector3 nb = getVertNormal(ib);

            if (t == 0 || t == 1)
            {
                bestNormal = Vector3.Lerp(na, nb, t).normalized;
            }
            else
            {
                bestNormal = (na + nb).normalized;
            }
        }
    }
    public static Vector3[] GetAveragedNormals(Vector3[] verts, Vector3[] norms)
    {
        int n = verts.Length;

        var sumDict = new Dictionary<Vector3, Vector3>();
        var countDict = new Dictionary<Vector3, int>();

        for (int i = 0; i < n; i++)
        {
            Vector3 pos = verts[i];
            Vector3 nor = norms[i];

            if (!sumDict.TryGetValue(pos, out Vector3 sum))
            {
                sumDict[pos] = nor;
                countDict[pos] = 1;
            }
            else
            {
                sumDict[pos] = sum + nor;
                countDict[pos] = countDict[pos] + 1;
            }
        }

        var avgDict = new Dictionary<Vector3, Vector3>(sumDict.Count);
        foreach (var kv in sumDict)
        {
            Vector3 pos = kv.Key;
            Vector3 sum = kv.Value;
            int cnt = countDict[pos];
            avgDict[pos] = (sum / cnt).normalized;
        }

        var newNormals = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            newNormals[i] = avgDict[verts[i]];
        }

        return newNormals;
    }


    //--------------------------------------------------------------------------------------------------------------------------------------------//
    //---------------------------------- Custom Raycast ------------------------------------------------------------------------------------------//
    //--------------------------------------------------------------------------------------------------------------------------------------------//
    public class RayIntersection
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
    }
    public static RayIntersection RaycastMesh(MeshCollider meshCollider, Vector3 origin, Vector3 direction, float maxDistance)
    {
        Mesh mesh = meshCollider.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        RayIntersection closestHit = null;
        float closestT = float.MaxValue;

        for (int i = 0; i < tris.Length; i += 3)
        {
            int i0 = tris[i];
            int i1 = tris[i + 1];
            int i2 = tris[i + 2];

            // Eckpunkte im Welt-Raum
            Vector3 a = tr.TransformPoint(verts[i0]);
            Vector3 b = tr.TransformPoint(verts[i1]);
            Vector3 c = tr.TransformPoint(verts[i2]);

            if (IntersectRayTriangle(origin, direction, a, b, c, out float t))
            {
                if (t < 0f || t > maxDistance)
                    continue;

                if (t < closestT)
                {
                    closestT = t;

                    Vector3 hitPoint = origin + direction * t;

                    Vector3 ab = b - a;
                    Vector3 ac = c - a;
                    Vector3 worldNormal = Vector3.Cross(ab, ac).normalized;

                    closestHit = new RayIntersection
                    {
                        point = hitPoint,
                        normal = worldNormal,
                        distance = t
                    };
                }
            }
        }

        return closestHit;
    }
    public static bool IntersectRayTriangle(Vector3 rayOrigin, Vector3 rayDirection, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
    {
        t = 0;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;

        Vector3 pVec = Vector3.Cross(rayDirection, edge2);
        float det = Vector3.Dot(edge1, pVec);

        if (det > -float.Epsilon && det < float.Epsilon)
        {
            return false;
        }

        float invDet = 1.0f / det;


        Vector3 tVec = rayOrigin - v0;
        float u = Vector3.Dot(tVec, pVec) * invDet;

        if (u < 0 || u > 1)
        {
            return false;
        }

        Vector3 qVec = Vector3.Cross(tVec, edge1);
        float v = Vector3.Dot(rayDirection, qVec) * invDet;

        if (v < 0 || u + v > 1)
        {
            return false;
        }

        t = Vector3.Dot(edge2, qVec) * invDet;

        return t > 0;
    }

}
