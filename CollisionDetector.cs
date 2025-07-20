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
       
        Vector3 point;
        Vector3 normal;

        Collider col = hits[0];
        Rigidbody rb = col.attachedRigidbody;

        //------- MeshCollider ------------------------------------------------------------------------------
        if (col is MeshCollider meshCol)
        {
            var cp = ClosestPointOnMesh.GetClosestPointOnMeshNormal(meshCol, predictedPos);
            if (cp == null) return null;

            point = cp.point;
            normal = cp.normal;
            return new CollisionConstraint(p, point, normal, p.radius, solver, rb);
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
            return new CollisionConstraint(p, point, normal, p.radius, solver, rb);
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
            return new CollisionConstraint(p, point, normal, p.radius, solver, rb);
        }

        //------- BoxCollider ----------------------------------------------------------------------------------------
        else if (col is BoxCollider boxCol)
        {
            Vector3 halfSize = boxCol.size * 0.5f;
            Vector3 localPos = boxCol.transform.InverseTransformPoint(predictedPos) - boxCol.center;

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

            Vector3 dir = predictedPos - closestWorld;
            float dist = dir.magnitude;
            if (!isInside && dist > p.radius)
                return null;

            normal = dir.normalized;
            if (isInside)
                normal = -normal;

            point = closestWorld;
            return new CollisionConstraint(p, point, normal, p.radius, solver, rb);
        }


        else
        {
            return null;
        }

    }

    public static void detectParticleCollisions(XPBDSolver solver)
    {
        var particles = solver.particles;
        int count = particles.Count;

        for (int i = 0; i < count; i++)
        {
            Particle p1 = particles[i];
            if (!p1.solveForCollision) continue;

            for (int j = i + 1; j < count; j++)
            {
                Particle p2 = particles[j];
                if (!p2.solveForCollision) continue;

                Vector3 delta = p2.positionX - p1.positionX;
                float distSq = delta.sqrMagnitude;

                float radiusSum = p1.radius + p2.radius;
                float radiusSumSq = radiusSum * radiusSum;

                if (distSq < radiusSumSq)
                {
                    float dist = Mathf.Sqrt(distSq);
                    Vector3 normal = delta / dist;

                    float penetration = dist - radiusSum;
                    float wSum = p1.w + p2.w;

                    p1.positionX += normal * penetration * (p1.w / wSum);
                    p2.positionX -= normal * penetration * (p2.w / wSum);
                }
            }
        }
    }

    public static void createHash()
    {

    }
}




