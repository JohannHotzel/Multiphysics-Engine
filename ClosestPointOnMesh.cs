using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
public class ClosestPointOnMeshCPU : MonoBehaviour
{
    [Header("Query Point")]
    public Vector3 point;

    private MeshCollider meshCollider;
    private Vector3[] vertices;
    private int[] triangles;

    private float minDistance = float.MaxValue;
    private Vector3 closestPoint = Vector3.zero;

    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.convex = true;

        Mesh mesh = meshCollider.sharedMesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;
    }

    void Update()
    {
        if (meshCollider == null || vertices == null || triangles == null)
            return;

        minDistance = float.MaxValue;
        closestPoint = Vector3.zero;

        Transform tr = meshCollider.transform;
        Vector3 worldPoint = point;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = tr.TransformPoint(vertices[triangles[i]]);
            Vector3 v1 = tr.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v2 = tr.TransformPoint(vertices[triangles[i + 2]]);

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            float signedDist = Vector3.Dot(normal, worldPoint - v0);
            Vector3 projPoint = worldPoint - signedDist * normal;

            if (IsPointInTriangle(projPoint, v0, v1, v2))
            {
                float dist = Mathf.Abs(signedDist);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestPoint = projPoint;
                }
            }
            else
            {
                TryUpdateClosestPointOnSegment(v0, v1, worldPoint);
                TryUpdateClosestPointOnSegment(v1, v2, worldPoint);
                TryUpdateClosestPointOnSegment(v2, v0, worldPoint);
            }
        }
    }

    private void TryUpdateClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 candidate = ClosestPointOnLineSegment(a, b, p);
        float dist = Vector3.Distance(p, candidate);
        if (dist < minDistance)
        {
            minDistance = dist;
            closestPoint = candidate;
        }
    }

    private Vector3 ClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    private bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
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

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(closestPoint, 0.1f);
    }


}
