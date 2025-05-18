using System.Collections.Generic;
using System.Xml.Serialization;
using Unity.VisualScripting;
using UnityEngine;

public class XPBDSolver : MonoBehaviour
{

    public float dt = 0.02f;
    public int substeps = 10;
    private float dts;
    private float dts2;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    //public float mu;
    //public float muSp;
    //public float muKp;

    private List<Particle> particles;
    private List<DistnaceConstraint> constraints;


    void Start()
    {
        dts = dt / substeps;
        dts2 = dts * dts;
        particles = new List<Particle>();
        constraints = new List<DistnaceConstraint>();

        Particle p1 = new Particle(new Vector3(0, 0, 0), 1f);
        Particle p2 = new Particle(new Vector3(0, 1, 0), 1f);
        particles.Add(p1);
        particles.Add(p2);
    }

    void FixedUpdate()
    {
        for (int i = 0; i < substeps; i++)
        {
            integrate();
            solveConstraints();
            solveCollisions();
            updateVelocities();
        }
    }

    private void integrate()
    {
        foreach (Particle p in particles)
        {
            Vector3 force = gravity * p.m;
            Vector3 acceleration = force * p.w;
            p.velocity += acceleration * dts;
            p.positionP = p.positionX;
            p.positionX += p.velocity * dts;
        }
    }
    private void solveConstraints()
    {
        /*
        foreach (DistnaceConstraint c in constraints)
        {
            c.solve();
        }
        */
    }
    private void solveCollisions()
    {
        /*
        foreach (Collision c in collisions)
        {
            c.solve();
        }
        */
    }
    private void updateVelocities()
    {
        foreach (Particle p in particles)
        {
            p.velocity = (p.positionX - p.positionP) / dts;
        }
    }


    void OnDrawGizmosSelected()
    {

        if (particles == null)
            return;

        Gizmos.color = Color.red;
        foreach (Particle p in particles)
        {
            Gizmos.DrawSphere(p.positionP, 0.1f);
            Gizmos.DrawSphere(p.positionX, 0.1f);
        }
    }

}
