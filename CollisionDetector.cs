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

        CollisionConstraint collisionConstraint = new CollisionConstraint(p, cp.point, cp.normal, 0, solver, null);
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

            return new CollisionConstraint(p, cp.point, normal, p.radius, solver, null);
        }

        else if (!cp.isInside)
        {
            Vector3 delta = predictedPos - cp.point;
            if (delta.magnitude < p.radius)
            {
                Vector3 normal = delta.normalized;
                return new CollisionConstraint(p, cp.point, normal, p.radius, solver, null);
            }
        }
        return null;
    }
    public static CollisionConstraint detectCollisionSubstepRadiusNormal(Particle p, Vector3 predictedPos, XPBDSolver solver)
    {
        if (!p.solveForCollision) return null;

        Collider[] hits = Physics.OverlapSphere(predictedPos, p.radius * 1.1f);
        if (hits.Length == 0) return null;
        MeshCollider meshCollider = hits.FirstOrDefault(h => h is MeshCollider) as MeshCollider;
        if (meshCollider == null) return null;


        ClosestPoint cp = ClosestPointOnMesh.GetClosestPointOnMeshNormal(meshCollider, predictedPos);
        if (cp == null) return null;

        Rigidbody rb = meshCollider.attachedRigidbody;

        return new CollisionConstraint(p, cp.point, cp.normal, p.radius, solver, rb);
    }


}




