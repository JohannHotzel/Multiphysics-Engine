using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class XPBDSolver : MonoBehaviour
{

    [Header("Simulation Settings")]
    public float dt = 0.02f;
    public int substeps = 10;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float maxVelocity = 100f;
    [HideInInspector] public float dts;
    [HideInInspector] public float dts2;
    [HideInInspector] public List<Particle> particles;
    [HideInInspector] public List<DistanceConstraint> constraints;
    [HideInInspector] public List<CollisionConstraint> collisionConstraints;
    [HideInInspector] public List<MultiphysicsCloth> cloths;

    private int[] order;
    private System.Random rng = new System.Random();


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
        collisionConstraints = new List<CollisionConstraint>();
        cloths = new List<MultiphysicsCloth>();

        registerCloth();

        order = new int[constraints.Count];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
    }
    void LateUpdate()
    {
        render();
    }


    void FixedUpdate()
    {
        findCollisions();
        shuffleOrderOfConstraints();
        for (int i = 0; i < substeps; i++)
        {
            integrate();
            solveConstraints();
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

        for (int k = 0; k < collisionConstraints.Count; k++)
            collisionConstraints[k].solve();

        for (int k = 0; k < order.Length; k++)
            constraints[order[k]].solve();

    }
    private void findCollisions()
    {
        collisionConstraints.Clear();

        foreach (Particle p in particles)
        {
            if (p.w == 0) continue;

            Vector3 force = gravity * p.m;
            Vector3 acceleration = force * p.w;
            Vector3 velocity = p.velocity + acceleration * dt;
            Vector3 pos = p.positionX + velocity * dt;

            CollisionConstraint cc = CollisionDetector.detectCollisions(p, pos, GetComponent<XPBDSolver>());
            if (cc != null)
            {
                collisionConstraints.Add(cc);
            }
        }
    }

    private void updateVelocities()
    {
        foreach (Particle p in particles)
        {
            Vector3 newVel = (p.positionX - p.positionP) / dts;
            p.velocity = Vector3.ClampMagnitude(newVel, maxVelocity);
        }
    }
    private void shuffleOrderOfConstraints()
    {
        //Shuffle indices is faster than shuffling constraints directly
        int n = order.Length;
        for (int i = 0; i < n; i++)
        {
            int j = rng.Next(i, n);
            int tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
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

        for (int offset = 0; offset < count; offset += batchSize)
        {
            int len = Mathf.Min(batchSize, count - offset);
            if (matrices == null || matrices.Length < len)
                matrices = new Matrix4x4[len];

            for (int i = 0; i < len; i++)
            {
                Vector3 pos = particles[offset + i].positionX;
                matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * particles[offset + i].radius * 2);
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
