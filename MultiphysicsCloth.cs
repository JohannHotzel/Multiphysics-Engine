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
    public float radius = 0.05f;
    public bool shearConstraints = true;
    public bool fixTop = true;

    [HideInInspector] public Particle[,] particles;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshGeometry geom;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }
    public void BuildCloth(XPBDSolver solver)
    {
        particles = new Particle[numParticlesX, numParticlesY];

        //---------------------------------- Initialize particles -----------------------------------------------------------
        for (int i = 0; i < numParticlesX; i++)
        {
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
        }

        //---------------------------------- Create distance constraints between particles ----------------------------------
        bool flip = true;
        for (int i = 0; i < numParticlesX; i++)
        {
            flip = !flip;

            for (int j = 0; j < numParticlesY; j++)
            {
                var p = particles[i, j];

                if (i + 1 < numParticlesX)
                {
                    var pr = particles[i + 1, j];
                    solver.distanceConstraints.Add(new DistanceConstraint(p, pr, stiffness, solver));
                }

                if (j + 1 < numParticlesY)
                {
                    var pu = particles[i, j + 1];
                    solver.distanceConstraints.Add(new DistanceConstraint(p, pu, stiffness, solver));
                }

                if (shearConstraints && i + 1 < numParticlesX && j + 1 < numParticlesY)
                {
                    flip = !flip;
                    if (flip)
                    {
                        var pd = particles[i + 1, j + 1];
                        solver.distanceConstraints.Add(new DistanceConstraint(p, pd, stiffness, solver));
                    }
                    else
                    {
                        var pd1 = particles[i + 1, j];
                        var pd2 = particles[i, j + 1];
                        solver.distanceConstraints.Add(new DistanceConstraint(pd1, pd2, stiffness, solver));
                    }
                }
            }
        }

        //---------------------------------- Create mesh geometry -----------------------------------------------------------
        if(!solver.showCloths)
            return;

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

        for (int i = 0; i < numParticlesX - 1; i++)
            for (int j = 0; j < numParticlesY - 1; j++)
            {
                var v00 = geoVerts[i, j];
                var v10 = geoVerts[i + 1, j];
                var v01 = geoVerts[i, j + 1];
                var v11 = geoVerts[i + 1, j + 1];

                geom.AddTriangle(v00, v11, v10);
                geom.AddTriangle(v00, v01, v11);
            }

        meshFilter.mesh = geom.BuildUnityMesh();
    }
    public void RenderCloth()
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
