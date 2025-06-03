using Unity.Mathematics;
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

        float d = Vector3.Dot(p.positionX - q, n) - radius;

        float d0 = -d;
        if (d0 < 0f)
            d0 = 0f;
        
        float clamp = Mathf.Max(d0 - solver.vMax * solver.dts, 0f);

        float c = d + clamp;
        if (c >= 0f)
            return;

        Vector3 displacement = -c * n;
        p.positionX += displacement;

        Vector3 impulse = displacement / solver.dts * p.m;
        if (collidingRigidbody != null)
            collidingRigidbody.AddForceAtPosition(-impulse, q, ForceMode.Impulse);

    }

}
