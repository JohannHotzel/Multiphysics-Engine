using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ClosestPointOnMesh;

public static class CollisionDetector
{
    public static CollisionConstraint detectCollisionSubstep(Particle p, Vector3 predictedPos, XPBDSolver solver)
    {
        Collider[] hits = Physics.OverlapSphere(predictedPos, 0.1f);
        if (hits.Length == 0) return null;
        MeshCollider meshCollider = hits.FirstOrDefault(h => h is MeshCollider) as MeshCollider;
        if (meshCollider == null) return null;

        ClosestPoint cp = ClosestPointOnMesh.GetClosestPointOnMeshPlanes(meshCollider, predictedPos);
        if (cp == null || !cp.isInside) return null;

        CollisionConstraint collisionConstraint = new CollisionConstraint(p, cp.point, cp.normal, 0, 0, solver);
        return collisionConstraint;
    }

    public static CollisionConstraint detectCollisionSubstepRadius(Particle p, Vector3 predictedPos, XPBDSolver solver)
    {
        Collider[] hits = Physics.OverlapSphere(predictedPos, p.radius * 1.1f);
        if (hits.Length == 0) return null;
        MeshCollider meshCollider = hits.FirstOrDefault(h => h is MeshCollider) as MeshCollider;
        if (meshCollider == null) return null;


        ClosestPoint cp = ClosestPointOnMesh.GetClosestPointOnMesh(meshCollider, predictedPos);
        if (cp == null) return null;

        if (cp.isInside)
        {
            Vector3 delta = cp.point - predictedPos;
            Vector3 normal = delta.normalized;

            return new CollisionConstraint(p, cp.point, normal, 0, p.radius, solver);
        }

        else if (!cp.isInside)
        {
            Vector3 delta = predictedPos - cp.point;
            if (delta.magnitude < p.radius)
            {
                Vector3 normal = delta.normalized;
                return new CollisionConstraint(p, cp.point, normal, 0, p.radius, solver);
            }
        }
        return null;
    }


}




