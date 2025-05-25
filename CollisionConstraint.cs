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

        float c = Vector3.Dot(p.positionX - q, n) - p.radius;
        if (c > 0) return;

        float alpha = stiffness / solver.dts2;
        float lambda = -c / (p.w + alpha);

        p.positionX += lambda * p.w * n;
    }

}
