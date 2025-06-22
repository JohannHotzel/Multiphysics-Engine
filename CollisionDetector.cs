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
        MeshCollider meshCollider = hits.FirstOrDefault(h => h is MeshCollider) as MeshCollider;
        if (meshCollider == null) return null;


        ClosestPoint cp = ClosestPointOnMesh.GetClosestPointOnMeshNormal(meshCollider, predictedPos);
        if (cp == null) return null;

        Rigidbody rb = meshCollider.attachedRigidbody;

        return new CollisionConstraint(p, cp.point, cp.normal, p.radius, solver, rb);
    }
}




