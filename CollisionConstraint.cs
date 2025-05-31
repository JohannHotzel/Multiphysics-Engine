using Unity.Mathematics;
using UnityEngine;

public class CollisionConstraint : IConstraint
{
    Particle p;
    Vector3 pos;
    Vector3 q;
    Vector3 n;
    float d;
    float stiffness;
    float radius;
    XPBDSolver solver;

    public CollisionConstraint(Particle p, Vector3 q, Vector3 n, float stiffness, float radius, XPBDSolver solver)
    {
        this.p = p;
        this.q = q;
        this.n = n;
        this.stiffness = stiffness;
        this.radius = radius;
        this.solver = solver;
    }

    public void solve()
    {
        if (p.w == 0) return;

        float d = Vector3.Dot(p.positionX - q, n) - radius;

        float d0 = -d;
        if (d0 < 0f)
        {
            d0 = 0f;
        }

        float clamp = Mathf.Max(d0 - solver.vMax * solver.dts, 0f);

        float c = d + clamp;
        if (c >= 0f)
            return;

        p.positionX += -c * n;
    }

}



/*
float c = Vector3.Dot(p.positionX - q, n);
if (c >= 0) return;

float alpha = stiffness / solver.dts2;
float lambda = -c / (p.w + alpha);

p.positionX += lambda * p.w * n;
*/