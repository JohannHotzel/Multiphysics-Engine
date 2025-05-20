using System.Collections.Generic;
using UnityEngine;

public class XPBDSolver : MonoBehaviour
{

    [Header("Simulation Settings")]
    public float dt = 0.02f;
    public int substeps = 10;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float particleRadius = 0.1f;
    [HideInInspector] public float dts;
    [HideInInspector] public float dts2;
    [HideInInspector] public List<Particle> particles;
    [HideInInspector] public List<DistanceConstraint> constraints;
    [HideInInspector] public List<MultiphysicsCloth> cloths;


    [Header("Rendering Settings")]
    public bool showParticles = true;
    public bool showConstraints = true;
    public Mesh particleMesh;
    public Material particleMat;
    private Matrix4x4[] matrices;
    private const int batchSize = 1023;
    public Material lineMaterial;                 // Unlit/Color-Shader
    private Mesh lineMesh;
    private Vector3[] verts;
    private int[] indices;

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
        if (showParticles)
            renderParticles();

        if (showConstraints)
            renderDistanceConstraints();

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
    private void renderDistanceConstraints()
    {
        int cCount = constraints.Count;
        if (lineMesh == null)
        {
            lineMesh = new Mesh { name = "ConstraintLines" };
            lineMesh.MarkDynamic();
        }

        if (verts == null || verts.Length != cCount * 2) verts = new Vector3[cCount * 2];
        if (indices == null || indices.Length != cCount * 2) indices = new int[cCount * 2];

        for (int i = 0; i < cCount; i++)
        {
            var con = constraints[i];
            Vector3 a = con.p1.positionX;
            Vector3 b = con.p2.positionX;
            verts[2 * i] = a;
            verts[2 * i + 1] = b;
            indices[2 * i] = 2 * i;
            indices[2 * i + 1] = 2 * i + 1;
        }

        lineMesh.Clear();
        lineMesh.vertices = verts;
        lineMesh.SetIndices(indices, MeshTopology.Lines, 0);

        Graphics.DrawMesh(lineMesh, Matrix4x4.identity, lineMaterial, 0, null, 0, null, false, false);

    }


    void OnDrawGizmos()
    {

    }

}
