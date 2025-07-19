using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class XPBDSolver : MonoBehaviour
{

    [Header("Simulation Settings")]
    public float dt = 0.02f;
    public int substeps = 10;
    public int iterations = 5;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float vMax;
    public float muS;
    public float muK;
    public float tearingThreshold = 0.1f;
    [HideInInspector] public float dts;
    [HideInInspector] public float dts2;
    [HideInInspector] public List<Particle> particles;
    [HideInInspector] public List<DistanceConstraint> distanceConstraints;
    [HideInInspector] public List<DistanceConstraint> brokenDistanceConstraints;
    [HideInInspector] public List<CollisionConstraint> collisionConstraints;
    [HideInInspector] public List<AttachmentConstraint> attachmentConstraints; 
    [HideInInspector] public List<MultiphysicsCloth> cloths;

    private System.Random rng = new System.Random();


    [Header("Rendering Settings")]
    public bool showParticles = true;
    public bool showConstraints = true;
    public bool showCloths = true;

    //Draw Paricles
    public Mesh particleMesh;
    public Material particleMat;
    private Matrix4x4[] matrices;
    private const int batchSize = 1023;
    
    //Draw Distance Constraints
    public Material lineMaterial; // Unlit/Color-Shader
    private Mesh lineMesh;
    private Vector3[] verts;
    private int[] indices;



    void Start()
    {
        dts = dt / substeps;
        dts2 = dts * dts;

        particles = new List<Particle>();
        distanceConstraints = new List<DistanceConstraint>();
        brokenDistanceConstraints = new List<DistanceConstraint>();
        collisionConstraints = new List<CollisionConstraint>();
        attachmentConstraints = new List<AttachmentConstraint>();
        cloths = new List<MultiphysicsCloth>();

        registerCloth();
    }
    void LateUpdate()
    {
        render();
    }

    void FixedUpdate()
    {
        ShuffleDistanceConstraints(distanceConstraints);
        findCollisionsOutsideSubStep();

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
        for(int i = 0; i < iterations; i++)
        {
            foreach (var con in distanceConstraints) con.solve();
            foreach (var con in collisionConstraints) con.solve();
            foreach (var ac in attachmentConstraints) ac.solve();
        }

       // distanceConstraints.RemoveAll(d => Mathf.Abs(d.lambda) > tearingThreshold);
  
        for (int k = distanceConstraints.Count - 1; k >= 0; k--)
        {
            var d = distanceConstraints[k];
            if (Mathf.Abs(d.lambda) > tearingThreshold)
            {
                brokenDistanceConstraints.Add(d);
                distanceConstraints.RemoveAt(k);
            }
        }
       
    }
    private void updateVelocities()
    {
        foreach (Particle p in particles)
        {
            Vector3 newVel = (p.positionX - p.positionP) / dts;
            p.velocity = newVel;
        }
    }
    private void findCollisionsOutsideSubStep()
    {
        collisionConstraints.Clear();

        foreach (Particle p in particles)
        {
            if (p.w == 0) continue;

            Vector3 force = gravity * p.m;
            Vector3 acceleration = force * p.w;
            Vector3 velocity = p.velocity + acceleration * dt;
            Vector3 pos = p.positionX + velocity * dt;

            CollisionConstraint collisionConstraint = CollisionDetector.detectCollisionSubstepRadiusNormal(p, pos, GetComponent<XPBDSolver>());
            if (collisionConstraint != null)
                collisionConstraints.Add(collisionConstraint);

        }
    }
    private void findCollisionsInsideSubStep()
    {
        collisionConstraints.Clear();

        foreach (Particle p in particles)
        {
            if (p.w == 0) continue;

            CollisionConstraint collisionConstraint = CollisionDetector.detectCollisionSubstepRadiusNormal(p, p.positionX, GetComponent<XPBDSolver>());
            if (collisionConstraint != null)
                collisionConstraints.Add(collisionConstraint);
        }
    }

    private void ShuffleConstraints(List<IConstraint> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
    private void ShuffleDistanceConstraints(List<DistanceConstraint> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }




    //------------------------------------------------------------------------------------------------------------------------------------------//
    //----------------- Register Physical Objects ----------------------------------------------------------------------------------------------//
    //------------------------------------------------------------------------------------------------------------------------------------------//
    private void registerCloth()
    {
        foreach (MultiphysicsCloth cloth in FindObjectsByType<MultiphysicsCloth>(FindObjectsSortMode.None))
        {
            cloth.buildCloth(this);
            cloths.Add(cloth);
        }
    }


    //------------------------------------------------------------------------------------------------------------------------------------------//
    //----------------- Rendering --------------------------------------------------------------------------------------------------------------//
    //------------------------------------------------------------------------------------------------------------------------------------------//
    private void render()
    {
        if (showParticles)
            renderParticles();

        if (showConstraints)
            renderDistanceConstraints();

        if (showCloths)
        {
            foreach (MultiphysicsCloth cloth in cloths)
            {
                cloth.renderClothSolid(this);
            }
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
        int cCount = distanceConstraints.Count;
        if (lineMesh == null)
        {
            lineMesh = new Mesh();
            lineMesh.MarkDynamic();
        }

        if (verts == null || verts.Length != cCount * 2) verts = new Vector3[cCount * 2];
        if (indices == null || indices.Length != cCount * 2) indices = new int[cCount * 2];

        for (int i = 0; i < cCount; i++)
        {
            var con = distanceConstraints[i];
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
