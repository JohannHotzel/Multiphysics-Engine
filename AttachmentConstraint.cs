using UnityEngine;

public class AttachmentConstraint : IConstraint
{
    Particle particle;
    GameObject parent;
    Vector3 parentPosition;
    Rigidbody rb;
    XPBDSolver solver;

    public AttachmentConstraint(Particle particle, GameObject parent, Vector3 parentPosition, XPBDSolver solver)
    {
        this.particle = particle;
        this.parent = parent;
        this.parentPosition = parentPosition;
        this.solver = solver;
        rb = parent.GetComponent<Rigidbody>();
    }

    public void solve()
    {
        Vector3 position = parent.transform.TransformPoint(parentPosition);
        Vector3 delta = position - particle.positionX;
        particle.positionX = position;

        if (rb != null)
        {
            Debug.Log("HasRB" + rb.name);
            Vector3 impulse = delta / solver.dts * 0.05f;
            rb.AddForceAtPosition(-impulse, position, ForceMode.Impulse);
        }

    }
}
