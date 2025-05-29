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

    private List<ClosestPoint> closestPoints = new List<ClosestPoint>();
    public float maxDistance = 0.1f;

    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.convex = true;

    }

    void Update()
    {

        ClosestPoint detectorClosestPoint = CollisionDetector.GetClosestPointOnMesh(meshCollider, point);

        if (detectorClosestPoint.isInside)
        {
            closestPoint = detectorClosestPoint.point;
            Debug.Log(detectorClosestPoint.isInside);
            Debug.DrawLine(closestPoint, closestPoint + detectorClosestPoint.normal * 0.1f, Color.green);
        }


    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(closestPoint, 0.1f);

    }


}
