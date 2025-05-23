using UnityEngine;
using static CollisionDetector;

[RequireComponent(typeof(MeshCollider))]
public class ClosestPointOnMeshCPU : MonoBehaviour
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

        ClosestPoint detectorClosestPoint = CollisionDetector.GetClosestPointOnMesh(meshCollider, point);
        closestPoint = detectorClosestPoint.point;

        Debug.Log(detectorClosestPoint.isInside);
    }





    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(closestPoint, 0.1f);
    }


}
