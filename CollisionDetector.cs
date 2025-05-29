using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
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

        CollisionConstraint collisionConstraint = new CollisionConstraint(p, cp.point, cp.normal, cp.distance, 0, solver);
        return collisionConstraint;
    }


}




