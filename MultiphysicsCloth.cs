using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MultiphysicsCloth : MonoBehaviour
{
    [Header("Cloth Settings")]
    public int numParticlesX;
    public int numParticlesY;
    public float width;
    public float height;
    public float particleMass;
    public float stiffness;
    public float radius;
    public bool shearConstraints;
    public bool fixTop;
    [HideInInspector] public Particle[,] particles;

    private Mesh clothMesh;
    private Vector3[] vertices;
    private int[] triangles;

    private Dictionary<DistanceConstraint, (int i1, int j1, int i2, int j2)> constraintMap
        = new Dictionary<DistanceConstraint, (int, int, int, int)>();

    private HashSet<(int, int, int, int)> brokenEdges
        = new HashSet<(int, int, int, int)>();


    void Awake()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        clothMesh = new Mesh();
        meshFilter.mesh = clothMesh;
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

                if(fixTop && j == numParticlesY - 1)
                {
                    p.w = 0f;
                    p.solveForCollision = false;
                }

                Collider[] hits = Physics.OverlapSphere(position, radius);
                if (hits.Length > 0)
                {
                    GameObject parent = hits[0].gameObject;
                    Vector3 parentPosition = parent.transform.InverseTransformPoint(position);
                    AttachmentConstraint atc = new AttachmentConstraint(p, parent, parentPosition, solver);
                    solver.attachmentConstraints.Add(atc);              
                    p.solveForCollision = false;
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
                    constraintMap[c] = (i, j, i + 1, j);
                }

                if (j + 1 < numParticlesY)
                {
                    var pu = particles[i, j + 1];
                    var c = new DistanceConstraint(p, pu, stiffness, solver);
                    solver.distanceConstraints.Add(c);
                    constraintMap[c] = (i, j, i, j + 1);
                }

                if (!shearConstraints)
                    continue;

                if (i + 1 < numParticlesX && j + 1 < numParticlesY)
                {
                    if (right)
                    {
                        Particle pd1 = particles[i + 1, j + 1];
                        var c = new DistanceConstraint(p, pd1, stiffness, solver);
                        solver.distanceConstraints.Add(c);
                        constraintMap[c] = (i, j, i + 1, j + 1);
                        right = !right;
                    }

                    else if (!right)
                    {
                        Particle pd2 = particles[i + 1, j];
                        Particle pd3 = particles[i, j + 1];
                        var c = new DistanceConstraint(pd2, pd3, stiffness, solver);
                        solver.distanceConstraints.Add(c);
                        constraintMap[c] = (i + 1, j, i, j + 1);
                        right = !right;
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

    public void renderClothSolid(XPBDSolver solver)
    {
        if (solver.brokenDistanceConstraints.Count > 0)
        {
            foreach (var d in solver.brokenDistanceConstraints)
            {
                if (constraintMap.TryGetValue(d, out var coords))
                {
                    MarkEdgeBroken(coords.i1, coords.j1, coords.i2, coords.j2);
                    constraintMap.Remove(d);
                }
            }
            solver.brokenDistanceConstraints.Clear();
        }

        updateMeshVertices();
        buildTriangles();
        applyMeshData();

        if (clothMesh == null || vertices == null) return;
        clothMesh.vertices = vertices;
        clothMesh.RecalculateNormals();
        clothMesh.RecalculateBounds();
    }
    private void initializeMeshData()
    {
        vertices = new Vector3[numParticlesX * numParticlesY];
        triangles = new int[(numParticlesX - 1) * (numParticlesY - 1) * 6];
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
        int maxTris = (numParticlesX - 1) * (numParticlesY - 1) * 6;
        var temp = new int[maxTris];

        for (int j = 0; j < numParticlesY - 1; j++)
            for (int i = 0; i < numParticlesX - 1; i++)
            {
                int i0 = j * numParticlesX + i;
                int i1 = j * numParticlesX + (i + 1);
                int i2 = (j + 1) * numParticlesX + i;
                int i3 = (j + 1) * numParticlesX + (i + 1);

                bool b01 = EdgeBroken(i, j, i + 1, j);
                bool b02 = EdgeBroken(i, j, i, j + 1);
                bool b12 = EdgeBroken(i + 1, j, i, j + 1);
                if (!b01 && !b02 && !b12)
                {
                    temp[triIdx++] = i0;
                    temp[triIdx++] = i2;
                    temp[triIdx++] = i1;
                }

                bool b13 = EdgeBroken(i + 1, j, i + 1, j + 1);
                bool b23 = EdgeBroken(i, j + 1, i + 1, j + 1);
                if (!b12 && !b13 && !b23)
                {
                    temp[triIdx++] = i1;
                    temp[triIdx++] = i2;
                    temp[triIdx++] = i3;
                }
            }

        Array.Resize(ref temp, triIdx);
        triangles = temp;
    }
    private void applyMeshData()
    {
        clothMesh.Clear();
        clothMesh.vertices = vertices;
        clothMesh.triangles = triangles;
        clothMesh.RecalculateNormals();
        clothMesh.RecalculateBounds();
    }

    private bool EdgeBroken(int i1, int j1, int i2, int j2)
    {
        if (i1 > i2 || (i1 == i2 && j1 > j2))
            (i1, j1, i2, j2) = (i2, j2, i1, j1);
        return brokenEdges.Contains((i1, j1, i2, j2));
    }

    public void MarkEdgeBroken(int i1, int j1, int i2, int j2)
    {
        if (i1 > i2 || (i1 == i2 && j1 > j2))
            (i1, j1, i2, j2) = (i2, j2, i1, j1);
        brokenEdges.Add((i1, j1, i2, j2));
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
                Vector3 position = transform.TransformPoint(positionLocal);

                if(fixTop && j == numParticlesY - 1)               
                    Gizmos.color = Color.red;
                
                else
                    Gizmos.color = Color.green;
                
                Gizmos.DrawSphere(position, 0.05f);
                Gizmos.DrawWireSphere(position, radius);
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
                Vector3 position = transform.TransformPoint(positionLocal);

                if(fixTop && j == numParticlesY - 1)               
                    Gizmos.color = Color.red;
                
                else
                    Gizmos.color = Color.green;

                Gizmos.DrawSphere(position, 0.05f);
                Gizmos.DrawWireSphere(position, radius);
            }
        }
    }

}
