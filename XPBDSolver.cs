using System.Collections.Generic;
using UnityEngine;

public class XPBDSolver : MonoBehaviour
{

    [Header("Simulation Settings")]
    public float dt = 0.02f;
    public int substeps = 10;
    public float dts;
    public float dts2;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float  particleRadius = 0.1f;
    [HideInInspector] public List<Particle> particles;
    [HideInInspector] public List<DistanceConstraint> constraints;
    [HideInInspector] public List<MultiphysicsCloth> cloths;


    [Header("Rendering Settings")]
    public Mesh particleMesh;
    public Material particleMat;
    private Matrix4x4[] matrices;
    private const int batchSize = 1023;

    void Start()
    {
        dts = dt / substeps;
        dts2 = dts * dts;

        particles = new List<Particle>();
        constraints = new List<DistanceConstraint>();
        cloths = new List<MultiphysicsCloth>();

        registerCloth();
    }
    void LateUpdate()
    {
        render();
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
        foreach (DistanceConstraint c in constraints)
        {
            c.solve();
        }
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


    private void registerCloth()
    {
        foreach (MultiphysicsCloth cloth in FindObjectsByType<MultiphysicsCloth>(FindObjectsSortMode.None))
        {
            cloth.buildCloth(this);
            cloths.Add(cloth);
        }
    }


    private void render()
    {
        renderParticles();

        foreach (MultiphysicsCloth cloth in cloths)
        {
            cloth.renderClothSolid();
        }
    }
    private void renderParticles()
    {
        int count = particles.Count;
        float scale = particleRadius * 2f;

        for (int offset = 0; offset < count; offset += batchSize)
        {
            int len = Mathf.Min(batchSize, count - offset);
            if (matrices == null || matrices.Length < len)
                matrices = new Matrix4x4[len];

            for (int i = 0; i < len; i++)
            {
                Vector3 pos = particles[offset + i].positionX;
                matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * scale);
            }

            Graphics.DrawMeshInstanced(particleMesh, 0, particleMat, matrices, len, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }
    }


    void OnDrawGizmos()
    {

        if (constraints != null)
        {
            Gizmos.color = Color.black;
            foreach (DistanceConstraint c in constraints)
            {
                Gizmos.DrawLine(c.p1.positionX, c.p2.positionX);
            }
        }

        if (particles != null)
        {
            Gizmos.color = Color.red;
            foreach (Particle p in particles)
            {
              //  Gizmos.DrawSphere(p.positionP, 0.1f);
              //  Gizmos.DrawSphere(p.positionX, 0.1f);
            }
        }
    }

}
