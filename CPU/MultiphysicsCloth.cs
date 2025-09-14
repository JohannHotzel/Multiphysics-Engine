
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MultiphysicsCloth : MonoBehaviour
{
    [Header("Cloth Settings")]
    public int numParticlesX = 10;
    public int numParticlesY = 10;
    public float width = 1f;
    public float height = 1f;
    public float particleMass = 1f;
    public float stiffness = 1f;
    public float damping = 0f; 
    public float radius = 0.05f;
    public bool shearConstraints = true;
    public bool fixTop = true;

    [HideInInspector] public Particle[,] particles;
    [HideInInspector] List<DistanceConstraint> distanceConstraints;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshGeometry geom;

    private float previousStiffness;
    private float previousDamping;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        distanceConstraints = new List<DistanceConstraint>();

        previousStiffness = stiffness;
        previousDamping = damping;
    }

    void Update()
    {
        if (Mathf.Abs(previousStiffness - stiffness) > Mathf.Epsilon || Mathf.Abs(previousDamping - damping) > Mathf.Epsilon)
        {

            if (distanceConstraints != null)
            {
                foreach (var dc in distanceConstraints)
                {
                    dc.stiffness = stiffness;
                    dc.damping = damping;
                }
            }

            previousStiffness = stiffness;
            previousDamping = damping;
        }
    }

    //------------------------------------------------------------------------------------------------------------------------//
    //---------------------------------------- Build/Initialize and Render Cloth ---------------------------------------------//
    //------------------------------------------------------------------------------------------------------------------------//
    public void BuildCloth(XPBDSolver solver)
    {
        particles = new Particle[numParticlesX, numParticlesY];

        //---------------------------------- Initialize particles -----------------------------------------------------------//
        for (int i = 0; i < numParticlesX; i++)
        for (int j = 0; j < numParticlesY; j++)
            {
                float x = i * width / (numParticlesX - 1);
                float y = j * height / (numParticlesY - 1);
                Vector3 worldPos = transform.TransformPoint(new Vector3(x, y, 0));

                Particle p = new Particle(worldPos, particleMass, radius);
                particles[i, j] = p;
                solver.particles.Add(p);

                if (fixTop && j == numParticlesY - 1)
                {
                    p.w = 0f;
                    p.solveForCollision = false;
                }

                Collider[] hits = Physics.OverlapSphere(worldPos, radius);
                if (hits.Length > 0)
                {
                    var parent = hits[0].gameObject;
                    var localPos = parent.transform.InverseTransformPoint(worldPos);
                    var atc = new AttachmentConstraint(p, parent, localPos, solver);
                    solver.attachmentConstraints.Add(atc);
                    p.solveForCollision = false;
                }
            }
        

        //---------------------------------- Create mesh geometry -----------------------------------------------------------//
        geom = new MeshGeometry();
        geom.transform = transform;
        Vertex[,] geoVerts = new Vertex[numParticlesX, numParticlesY];

        for (int i = 0; i < numParticlesX; i++)
            for (int j = 0; j < numParticlesY; j++)
            {
                Vector3 localPos = transform.InverseTransformPoint(particles[i, j].positionX);
                var v = geom.AddVertex(localPos);
                v.Particle = particles[i, j];
                geoVerts[i, j] = v;
            }

        bool flip = true;
        for (int i = 0; i < numParticlesX - 1; i++)
        {
            flip = !flip;
            for (int j = 0; j < numParticlesY - 1; j++)
            {
                flip = !flip;
                var v00 = geoVerts[i, j];
                var v10 = geoVerts[i + 1, j];
                var v01 = geoVerts[i, j + 1];
                var v11 = geoVerts[i + 1, j + 1];

                if (flip)
                {
                    geom.AddTriangle(v00, v11, v10);
                    geom.AddTriangle(v00, v01, v11);
                }
                else
                {
                    geom.AddTriangle(v00, v01, v10);
                    geom.AddTriangle(v10, v01, v11);
                }
            }
        }

        if (solver.showCloths)
            meshFilter.mesh = geom.BuildUnityMesh();


        //---------------------------------- Create constraints --------------------------------------------------------------//
        foreach (var edge in geom.edges) 
        {
            List<Vertex> vertices = edge.GetAdjacentQuadVertices();
                 
            if(vertices != null && vertices.Count == 4)
            {
                var pA = vertices[0].Particle;
                var pB = vertices[1].Particle;
                var pC = vertices[2].Particle;
                var pD = vertices[3].Particle;

                DistanceConstraint dc1 = solver.addUniqueDistanceConstraint(pA, pB, stiffness, damping);
                DistanceConstraint dc2 = solver.addUniqueDistanceConstraint(pC, pD, stiffness, damping); //Simple Bending Constraints (Distance Constraints)

                if (dc1 != null) distanceConstraints.Add(dc1);
                if (dc2 != null) distanceConstraints.Add(dc2);
            }
            
            else
            {              
                var pA = edge.V0.Particle;
                var pB = edge.V1.Particle;
                DistanceConstraint dc3 = solver.addUniqueDistanceConstraint(pA, pB, stiffness, damping);
                if (dc3 != null) distanceConstraints.Add(dc3);
            }
        }

    }


    public void RenderClothTearing(XPBDSolver solver)
    {
        if (geom == null) return;

        List<Edge> tornEdges = new List<Edge>();
        foreach (DistanceConstraint dc in solver.brokenDistanceConstraints)
        {
            Edge edge = geom.GetEdge(dc.p1, dc.p2);
            if (edge != null && !tornEdges.Contains(edge))
            {
                tornEdges.Add(edge);
            }
        }

        meshFilter.mesh = geom.BuildUnityMesh(tornEdges); 
    }

    public void RenderCloth(XPBDSolver solver)
    {
        if (geom == null) return;
        meshFilter.mesh = geom.BuildUnityMesh();
    }


    //--------------------------------------------------------------------------------------------------------------------------------------------//
    //---------------------------------- Display Gizmos ------------------------------------------------------------------------------------------//
    //--------------------------------------------------------------------------------------------------------------------------------------------//
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        DrawClothGizmos();
    }
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        DrawClothGizmos();
    }
    private void DrawClothGizmos()
    {
        Gizmos.color = Color.green;
        var old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(
            new Vector3(width * 0.5f, height * 0.5f, 0f),
            new Vector3(width, height, 0f)
        );
        Gizmos.matrix = old;

        for (int i = 0; i < numParticlesX; i++)
            for (int j = 0; j < numParticlesY; j++)
            {
                float x = i * width / (numParticlesX - 1);
                float y = j * height / (numParticlesY - 1);
                Vector3 p = transform.TransformPoint(new Vector3(x, y, 0));
                Gizmos.color = (fixTop && j == numParticlesY - 1) ? Color.red : Color.green;
                Gizmos.DrawSphere(p, 0.05f);
                Gizmos.DrawWireSphere(p, radius);
            }
    }
}
