using UnityEngine;

public class DistanceConstraint : IConstraint
{
    public Particle p1;
    public Particle p2;
    public float restLength;
    public float stiffness;
    public XPBDSolver solver;
    public float lambda;

    public DistanceConstraint(Particle p1, Particle p2, float stiffness, XPBDSolver solver)
    {
        this.p1 = p1;
        this.p2 = p2;
        this.stiffness = stiffness;
        this.solver = solver;
        restLength = Vector3.Distance(p1.positionX, p2.positionX);
    }

    public void solve()
    {
        Vector3 delta = p2.positionX - p1.positionX;
        float currentLength = delta.magnitude;
        if (currentLength == 0) return;
        Vector3 direction = delta.normalized;

        float alpha = stiffness / solver.dts2;

        float c = currentLength - restLength;
        lambda = c / (p1.w + p2.w + alpha);

        if (p1.w != 0)
            p1.positionX += direction * lambda * p1.w;

        if (p2.w != 0)
            p2.positionX -= direction * lambda * p2.w;

    }

}
