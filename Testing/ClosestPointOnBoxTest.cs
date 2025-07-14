using UnityEngine;

public class ClosestPointOnBoxTest : MonoBehaviour
{
    public BoxCollider boxCol;
    public Vector3 pos;
    public float radius;

    private Vector3 point;
    private Vector3 normal;
    

    void Update()
    {
        Vector3 worldCenter = boxCol.transform.TransformPoint(boxCol.center);
        Vector3 halfSize = Vector3.Scale(boxCol.size * 0.5f, boxCol.transform.lossyScale);

        Vector3 localPos = boxCol.transform.InverseTransformPoint(pos) - boxCol.center;

        bool insideX = Mathf.Abs(localPos.x) <= halfSize.x;
        bool insideY = Mathf.Abs(localPos.y) <= halfSize.y;
        bool insideZ = Mathf.Abs(localPos.z) <= halfSize.z;
        bool isInside = insideX && insideY && insideZ;

        Vector3 clampedLocal = localPos;
        if (isInside)
        {
            float dx = halfSize.x - Mathf.Abs(localPos.x);
            float dy = halfSize.y - Mathf.Abs(localPos.y);
            float dz = halfSize.z - Mathf.Abs(localPos.z);

            if (dx < dy && dx < dz) clampedLocal.x = Mathf.Sign(localPos.x) * halfSize.x;
            else if (dy < dz) clampedLocal.y = Mathf.Sign(localPos.y) * halfSize.y;
            else clampedLocal.z = Mathf.Sign(localPos.z) * halfSize.z;
        }
        else
        {
            clampedLocal.x = Mathf.Clamp(localPos.x, -halfSize.x, halfSize.x);
            clampedLocal.y = Mathf.Clamp(localPos.y, -halfSize.y, halfSize.y);
            clampedLocal.z = Mathf.Clamp(localPos.z, -halfSize.z, halfSize.z);
        }

        Vector3 closestWorld = boxCol.transform.TransformPoint(clampedLocal + boxCol.center);

        Vector3 dir = pos - closestWorld;
        float dist = dir.magnitude;
        if (!isInside && dist > radius)
        {
            point = Vector3.zero;
            return;
        }
            

        normal = dir.normalized;
        if (isInside)
            normal = -normal;

        point = closestWorld;

    }

    void OnDrawGizmos()
    {
        if (boxCol == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(boxCol.transform.TransformPoint(boxCol.center), boxCol.size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, radius);


        Gizmos.color = Color.green;
        Gizmos.DrawSphere(point, 0.1f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(point, normal * 1f);
    }
}
