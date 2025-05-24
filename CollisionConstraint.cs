using Unity.Mathematics;
using UnityEngine;

public class CollisionConstraint : IConstraint
{
    Particle p;
    Vector3 pos;
    Vector3 q;
    Vector3 n;
    float stiffness;
    XPBDSolver solver;

    public CollisionConstraint(Particle p, Vector3 q, Vector3 n, float stiffness, XPBDSolver solver)
    {
        this.p = p;
        this.q = q;
        this.n = n;
        this.stiffness = stiffness;
        this.solver = solver;
    }

    public void solve()
    {
        if (p.w == 0) return;

        Vector3 delta = p.positionX - q;
        float dist = delta.magnitude;

        if (dist <= p.radius)
        {
            float alpha = stiffness / solver.dts2;
            float penetration = (p.radius - dist) / (p.w + alpha);
            p.positionX += n * penetration * p.w;
        }
    }

}
