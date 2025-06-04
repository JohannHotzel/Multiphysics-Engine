using System.Collections.Generic;
using UnityEngine;
using static ClosestPointOnMesh;

[RequireComponent(typeof(MeshCollider))]
public class ClosestPointOnMeshTest : MonoBehaviour
{
    [Header("Query Point")]
    public Vector3 point;
    private MeshCollider meshCollider;
    private Vector3 closestPoint = Vector3.zero;

    public Vector3 direction;
    public float maxDistance = 0.1f;
    private Vector3 intersectionPoint;

    public Vector3 testVector;



    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
    }

    void Update()
    {
        ClosestPoint cp = ClosestPointOnMesh.GetClosestPointOnMeshNormal(meshCollider, point);
        closestPoint = cp.point;
        Vector3 normal = cp.normal;
        Debug.DrawLine(closestPoint, closestPoint + normal * 0.5f, Color.red);



        Vector3 t1;
        if (Mathf.Abs(testVector.x) > 0.7071f)
        {
            t1 = new Vector3(testVector.y, -testVector.x, 0f).normalized;
        }
        else
        {
            t1 = new Vector3(0f, testVector.z, -testVector.y).normalized;
        }
        Vector3 t2 = Vector3.Cross(testVector, t1);

        Debug.DrawLine(Vector3.zero, t1 * 0.5f, Color.blue);
        Debug.DrawLine(Vector3.zero, t2 * 0.5f, Color.green);
        Debug.DrawLine(Vector3.zero, testVector * 0.5f, Color.yellow);


    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.05f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(closestPoint, 0.1f);





    }


}
