using System;
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
    public float radius = 0.1f;
    public bool shearConstraints = true;
    public bool fixTop = true;
    public bool alwaysDrawGizmos = false;
    [HideInInspector] public Particle[,] particles;

    private Mesh clothMesh;
    private Vector3[] vertices;
    private int[] triangles;

    void Awake()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        clothMesh = new Mesh();
        mf.mesh = clothMesh;
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

                if (fixTop && j == numParticlesY - 1)
                    p.w = 0;

                Collider[] hits = Physics.OverlapSphere(position, 0.01f);
                if (hits.Length > 0)
                {
                    GameObject parent = hits[0].gameObject;
                    p.parent = parent;
                    p.parentPosition = parent.transform.InverseTransformPoint(position);
                    p.w = 0;
                }                  
            }
            
        }

        bool right = true;

        for (int i = 0; i < numParticlesX; i++)
        {
            right = !right;

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

                if (!shearConstraints)
                    continue;

                if (i + 1 < numParticlesX && j + 1 < numParticlesY)
                {
                    if (right)
                    {
                        Particle pd1 = particles[i + 1, j + 1];
                        solver.distanceConstraints.Add(new DistanceConstraint(p, pd1, stiffness, solver));
                        right = false;
                    }

                    else if (!right)
                    {
                        Particle pd2 = particles[i + 1, j];
                        Particle pd3 = particles[i, j + 1];
                        solver.distanceConstraints.Add(new DistanceConstraint(pd2, pd3, stiffness, solver));
                        right = true;
                    }
                }
            }
        }

        if (solver.showCloths)
        {
            initializeMeshData();
            updateMeshVertices();
            buildTriangles();
            applyMeshData();
        }
    }
    public void renderClothSolid()
    {
        if (clothMesh == null || particles == null)
            return;

        updateMeshVertices();
        clothMesh.vertices = vertices;
        clothMesh.RecalculateNormals();
        clothMesh.RecalculateBounds();
    }
    private void initializeMeshData()
    {
        int numVerts = numParticlesX * numParticlesY;
        vertices = new Vector3[numVerts];

        int numQuads = (numParticlesX - 1) * (numParticlesY - 1);
        triangles = new int[numQuads * 6];
    }
    private void updateMeshVertices()
    {
        int idx = 0;
        for (int j = 0; j < numParticlesY; j++)
        {
            for (int i = 0; i < numParticlesX; i++)
            {
                idx = j * numParticlesX + i;

                Vector3 worldPos = particles[i, j].positionX;
                vertices[idx] = transform.InverseTransformPoint(worldPos);
            }
        }
    }
    private void buildTriangles()
    {
        int triIdx = 0;
        for (int j = 0; j < numParticlesY - 1; j++)
        {
            for (int i = 0; i < numParticlesX - 1; i++)
            {
                int i0 = j * numParticlesX + i;
                int i1 = j * numParticlesX + (i + 1);
                int i2 = (j + 1) * numParticlesX + i;
                int i3 = (j + 1) * numParticlesX + (i + 1);

                triangles[triIdx++] = i0;
                triangles[triIdx++] = i2;
                triangles[triIdx++] = i1;

                triangles[triIdx++] = i1;
                triangles[triIdx++] = i2;
                triangles[triIdx++] = i3;
            }
        }
    }
    private void applyMeshData()
    {
        clothMesh.Clear();
        clothMesh.vertices = vertices;
        clothMesh.triangles = triangles;
        clothMesh.RecalculateNormals();
    }


    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.green;
        Matrix4x4 oldMat = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(
            new Vector3(width * 0.5f, height * 0.5f, 0f),
            new Vector3(width, height, 0f)
        );
        Gizmos.matrix = oldMat;


        for (int i = 0; i < numParticlesX; i++)
        {
            for (int j = 0; j < numParticlesY; j++)
            {
                float x = i * width / (numParticlesX - 1);
                float y = j * height / (numParticlesY - 1);
                Vector3 positionLocal = new Vector3(x, y, 0);

                if (fixTop && j == numParticlesY - 1)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.green;

                Vector3 position = transform.TransformPoint(positionLocal);
                Gizmos.DrawSphere(position, 0.05f);
            }
        }

    }
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Gizmos.color = Color.green;
        Matrix4x4 oldMat = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(
            new Vector3(width * 0.5f, height * 0.5f, 0f),
            new Vector3(width, height, 0f)
        );
        Gizmos.matrix = oldMat;


        for (int i = 0; i < numParticlesX; i++)
        {
            for (int j = 0; j < numParticlesY; j++)
            {
                float x = i * width / (numParticlesX - 1);
                float y = j * height / (numParticlesY - 1);
                Vector3 positionLocal = new Vector3(x, y, 0);

                if (fixTop && j == numParticlesY - 1)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.green;

                Vector3 position = transform.TransformPoint(positionLocal);
                Gizmos.DrawSphere(position, 0.05f);
            }
        }
    }

}
