using System;
using UnityEngine;

public class MultiphysicsCloth : MonoBehaviour
{
    [Header("Cloth Settings")]
    public int numParticlesX = 10;
    public int numParticlesY = 10;
    public float width = 1f;
    public float height = 1f;
    public float particleMass = 1f;
    public float stiffness = 1f;
    public float radius = 0.1f;
    public bool shearConstraints = true; 
    [HideInInspector] public Particle[,] particles;

    void Awake()
    {

    }

    public void buildCloth(XPBDSolver solver)
    {
        particles = new Particle[numParticlesX, numParticlesY];

        for (int i = 0; i < numParticlesX; i++)
        {
            for (int j = 0; j < numParticlesY; j++)
            {
                float x = i * width / (numParticlesX - 1);
                float y = j * height / (numParticlesY - 1);
                Vector3 positionLocal = new Vector3(x, y, 0);
                Vector3 position = transform.TransformPoint(positionLocal);

                Particle p = new Particle(position, particleMass, radius);
                particles[i, j] = p;
                solver.particles.Add(p);

                if (j == numParticlesY - 1)
                    p.w = 0;
            }
        }


        for (int i = 0; i < numParticlesX; i++)
        {
            for (int j = 0; j < numParticlesY; j++)
            {
                Particle p = particles[i, j];

                if (i + 1 < numParticlesX)
                {
                    var pr = particles[i + 1, j];
                    var c = new DistanceConstraint(p, pr, stiffness, solver);
                    solver.distanceConstraints.Add(c);
                }

                if (j + 1 < numParticlesY)
                {
                    var pu = particles[i, j + 1];
                    var c = new DistanceConstraint(p, pu, stiffness, solver);
                    solver.distanceConstraints.Add(c);
                }

                if(!shearConstraints) 
                    continue;

                if (i + 1 < numParticlesX && j + 1 < numParticlesY)
                {
                    Particle pd1 = particles[i + 1, j + 1];
                    solver.distanceConstraints.Add(new DistanceConstraint(p, pd1, stiffness, solver));

                    Particle pd2 = particles[i + 1, j];
                    Particle pd3 = particles[i, j + 1];
                    solver.distanceConstraints.Add(new DistanceConstraint(pd2, pd3, stiffness, solver));
                }


            }
        }




    }
    public void renderClothSolid()
    {

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Matrix4x4 oldMat = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(
            new Vector3(width * 0.5f, height * 0.5f, 0f),
            new Vector3(width, height, 0f)
        );
        Gizmos.matrix = oldMat;
    }

}
