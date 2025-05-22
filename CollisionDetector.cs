using UnityEngine;

public static class CollisionDetector
{

    public static void detectCollisions(Particle p)
    {

        Collider[] hits = Physics.OverlapSphere(p.positionX, p.radius);
        
        if(hits.Length == 0) return;
        Collider col = hits[0];

        Vector3 pos = p.positionX;
        Vector3 closest = col.ClosestPoint(pos);

        bool inside = closest == pos;
        if (inside) return;

        float distance    = Vector3.Distance(pos, closest);
        float penetration = p.radius - distance; 
        Vector3 normal = (pos - closest).normalized;

        p.positionX += penetration * normal;


    }

}
