using UnityEngine;

public class DistanceConstraint : IConstraint
{
    public Particle p1;
    public Particle p2;
    public float restLength;
    public float stiffness;
    public float damping;
    public XPBDSolver solver;
    public float lambda;

    public DistanceConstraint(Particle p1, Particle p2, float stiffness, float damping, XPBDSolver solver)
    {
        this.p1 = p1;
        this.p2 = p2;
        this.stiffness = stiffness;
        this.damping = damping;
        this.solver = solver;
        restLength = Vector3.Distance(p1.positionX, p2.positionX);
    }

    public void solve()
    {
        // Calculate the stiffness and damping coefficients
        float alpha = stiffness / solver.dts2;
        float beta = damping * solver.dts2;
        float gamma = (alpha * beta) / solver.dts;


        Vector3 delta = p2.positionX - p1.positionX;
        float currentLength = delta.magnitude;
        Vector3 direction = delta / currentLength;
        float c = currentLength - restLength;

        Vector3 dx1 = p1.positionX - p1.positionP;
        Vector3 dx2 = p2.positionX - p2.positionP;
        float gradDotDX = Vector3.Dot(direction, dx2 - dx1);

        float wSum = p1.w + p2.w;
        float numerator = -c - alpha * lambda - gamma * gradDotDX;
        float denominator = (1f + gamma) * wSum + alpha;
        lambda = numerator / denominator;

        Vector3 corr = direction * lambda;
        if (p1.w != 0f) p1.positionX -= corr * p1.w;
        if (p2.w != 0f) p2.positionX += corr * p2.w;
    }

}
