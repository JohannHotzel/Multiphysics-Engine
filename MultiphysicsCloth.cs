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


    [HideInInspector] public Particle[,] particles;

    void Awake()
    {

    }

    public void buildCloth(XPBDSolver solver)
    {
        particles = new Particle[numParticlesX, numParticlesY];

        for (int i = 0; i < numParticlesX; i++)
            for (int j = 0; j < numParticlesY; j++)
            {
                float x = i * width / (numParticlesX - 1);
                float y = j * height / (numParticlesY - 1);
                Vector3 positionLocal = new Vector3(x, y, 0);
                Vector3 position = transform.TransformPoint(positionLocal);

                particles[i, j] = new Particle(position, particleMass);
                solver.particles.Add(particles[i, j]);

                if (j == numParticlesY - 1)
                    particles[i, j].w = 0;

            }

            
    }

    public void renderCloth()
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
