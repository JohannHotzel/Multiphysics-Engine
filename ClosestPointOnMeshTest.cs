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



    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
    }

    void Update()
    {
        ClosestPoint cp = ClosestPointOnMesh.GetClosestPointOnMesh(meshCollider, point);
        closestPoint = cp.point;



    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.05f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(closestPoint, 0.1f);

    }


}
