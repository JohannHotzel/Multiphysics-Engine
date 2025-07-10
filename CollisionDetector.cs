using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ClosestPointOnMesh;

public static class CollisionDetector
{
    public static CollisionConstraint detectCollisionSubstepRadiusNormal(Particle p, Vector3 predictedPos, XPBDSolver solver)
    {
        if (!p.solveForCollision) return null;

        Collider[] hits = Physics.OverlapSphere(predictedPos, p.radius * 1.1f);

        if (hits.Length == 0) return null;
        Collider col = hits[0];

        Vector3 point;
        Vector3 normal;
        Rigidbody rb = col.attachedRigidbody;

        //------- MeshCollider ------------------------------------------------------------------------------
        if (col is MeshCollider meshCol)
        {
            var cp = ClosestPointOnMesh.GetClosestPointOnMeshNormal(meshCol, predictedPos);
            if (cp == null) return null;

            point = cp.point;
            normal = cp.normal;
        }

        //------- SphereCollider ------------------------------------------------------------------------------
        else if (col is SphereCollider sphereCol)
        {
            Vector3 center = sphereCol.transform.TransformPoint(sphereCol.center);

            float uniformScale = sphereCol.transform.lossyScale.x;
            float worldRadius = sphereCol.radius * uniformScale;

            Vector3 dir = predictedPos - center;
            float dist = dir.magnitude;

            if (dist > worldRadius + p.radius)
                return null;

            normal = dir.normalized;
            point = center + normal * worldRadius;
        }

        //------- CapsuleCollider ------------------------------------------------------------------------------
        else if (col is CapsuleCollider capCol)
        {
            Vector3 worldCenter = capCol.transform.TransformPoint(capCol.center);
            Vector3 worldUp = capCol.transform.up;

            float radius = capCol.radius * Mathf.Max(
                capCol.transform.lossyScale.x,
                capCol.transform.lossyScale.z
            );
            float height = Mathf.Max(
                capCol.height * capCol.transform.lossyScale.y,
                2f * radius
            );

            float halfSegment = (height * 0.5f) - radius;

            Vector3 p1 = worldCenter + worldUp * halfSegment;
            Vector3 p2 = worldCenter - worldUp * halfSegment;

            Vector3 d = p2 - p1;
            float t = Vector3.Dot(predictedPos - p1, d) / Vector3.Dot(d, d);
            t = Mathf.Clamp01(t);
            Vector3 closestOnSegment = p1 + d * t;

            Vector3 dir = predictedPos - closestOnSegment;
            float dist = dir.magnitude;
            if (dist > radius + p.radius)
                return null;

            normal = dir.normalized;
            point = closestOnSegment + normal * radius;
        }

        //------- Else -----------------------------------------------------------------------------------------
        else
        {
            return null;
        }

        return new CollisionConstraint(p, point, normal, p.radius, solver, rb);
    }
}




