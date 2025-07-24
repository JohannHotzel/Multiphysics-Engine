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

    float lambdaN = 0f;
    float lambdaF1 = 0f;
    float lambdaF2 = 0f;

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

        Vector3 normalCorrection = -c * n;
        Vector3 posX = p.positionX;
        Vector3 posP = p.positionP;
        p.positionX += normalCorrection;

        Vector3 posDiff = posX - posP;
        Vector3 tangentialDiff = posDiff - Vector3.Dot(posDiff, n) * n;
        float tangentialLength = tangentialDiff.magnitude;
        Vector3 tangentialCorrection = Vector3.zero;
        float penetration = Mathf.Abs(c);

        if (tangentialLength < solver.muS * penetration)
            tangentialCorrection = tangentialDiff;              
        else
        {
            float k = solver.muK * penetration;
            float factor = Mathf.Min(k / tangentialLength, 1f);
            tangentialCorrection = tangentialDiff * factor;
        }

        p.positionX -= tangentialCorrection;

        Vector3 completeCorrection = normalCorrection + tangentialCorrection;

        Vector3 impulse = completeCorrection / solver.dts * p.m;
        if (collidingRigidbody != null)
            collidingRigidbody.AddForceAtPosition(-impulse, p.positionX, ForceMode.Impulse);

    }

}

