using System.Collections.Generic;
using UnityEngine;
using static CollisionDetector;

[RequireComponent(typeof(MeshCollider))]
public class ClosestPointOnMeshTest : MonoBehaviour
{
    [Header("Query Point")]
    public Vector3 point;
    private MeshCollider meshCollider;
    private Vector3 closestPoint = Vector3.zero;

    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.convex = true;

    }

    void Update()
    {

        Mesh mesh = meshCollider.sharedMesh;
        mesh.RecalculateNormals();
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        int[] tris = mesh.triangles;
        Transform tr = meshCollider.transform;

        Vector3[] averagedNormals = GetAveragedNormals(verts, norms);



        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 normal = norms[i];
            Vector3 noralWorld = tr.TransformDirection(normal).normalized;
            Vector3 vertexWorld = tr.TransformPoint(verts[i]);

            //  Debug.DrawLine(vertexWorld, vertexWorld + noralWorld * 0.5f, Color.blue);

            Vector3 averagedNormal = averagedNormals[i];
            Vector3 averagedNormalWorld = tr.TransformDirection(averagedNormal).normalized;
            Debug.DrawLine(vertexWorld, vertexWorld + averagedNormalWorld * 0.5f, Color.yellow);

        }


        ClosestPoint detectorClosestPoint = CollisionDetector.GetClosestPointOnMesh(meshCollider, point);
        closestPoint = detectorClosestPoint.point;

        Debug.Log(detectorClosestPoint.isInside);
        Debug.DrawLine(detectorClosestPoint.point, detectorClosestPoint.point + detectorClosestPoint.normal * 0.5f, Color.red);

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






    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(closestPoint, 0.1f);
    }


}
