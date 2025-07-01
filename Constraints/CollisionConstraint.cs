using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class CollisionConstraint : IConstraint
{
    Particle p;
    Vector3 pos;
    Vector3 q;
    Vector3 n;
    float radius;
    XPBDSolver solver;
    Rigidbody collidingRigidbody;

    public CollisionConstraint(Particle p, Vector3 q, Vector3 n, float radius, XPBDSolver solver, Rigidbody collidingRigidbody)
    {
        this.p = p;
        this.q = q;
        this.n = n;
        this.radius = radius;
        this.solver = solver;
        this.collidingRigidbody = collidingRigidbody;
    }

    public void solve()
    {
        if (p.w == 0) return;


        float bias = 0.001f;        
        float d = Vector3.Dot(p.positionX - q, n) - (radius + bias);

        float d0 = -d;
        if (d0 < 0f) d0 = 0f;

        float clamp = Mathf.Max(d0 - solver.vMax * solver.dts, 0f);

        float c = d + clamp;

        if (c >= 0f) return;

        float w = p.w;
        float deltaLambdaN = -c / w;

        Vector3 displacement = -c * n;
        p.positionX += displacement;

        /*
        //Friction
        Vector3 t1;
        if (Mathf.Abs(n.x) > 0.7071f)
        {
            t1 = new Vector3(n.y, -n.x, 0f).normalized;
        }
        else
        {
            t1 = new Vector3(0f, n.z, -n.y).normalized;
        }

        Vector3 t2 = Vector3.Cross(n, t1).normalized;

        //Vector3 xContact = q + radius * n;
        //Vector3 xContact = q ;

        float Cf1 = Vector3.Dot(p.positionX - q, t1);
        float Cf2 = Vector3.Dot(p.positionX - q, t2);
        float deltaLambdaF1 = -Cf1 / w;
        float deltaLambdaF2 = -Cf2 / w;

        Vector2 rawF = new Vector2(deltaLambdaF1, deltaLambdaF2);
        float maxFric = solver.mu * -deltaLambdaN;
        Vector2 clampedF;

        if (rawF.magnitude > maxFric)
            clampedF = rawF.normalized * maxFric;

        else
            clampedF = rawF;

        float appliedf1 = clampedF.x;
        float appliedf2 = clampedF.y;

        Vector3 correctionF = w * (appliedf1 * t1 + appliedf2 * t2);

        Vector3 completeCorrection = displacement + correctionF;

        p.positionX += completeCorrection;


        Vector3 impulse = completeCorrection / solver.dts * p.m;
        if (collidingRigidbody != null)
            collidingRigidbody.AddForceAtPosition(-impulse, q, ForceMode.Impulse);
        */

    }

}
