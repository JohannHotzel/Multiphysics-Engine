using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GpuCloth : GpuMassAggregate
{
    [Header("Grid")]
    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;
    public float height = 10f;

    [Header("Mass")]
    public float clothMass = 1f;
    private float particleMass => clothMass / (numParticlesX * numParticlesY);

    [Header("Constraint Types")]
    public bool useStructural = true;
    public bool useShear = true;
    public bool useFlexion = true;
    public float structuralCompliance = 0f;
    public float shearCompliance = 0f;
    public float flexionCompliance = 0f;

    [Header("Pinning")]
    public bool pinTopRow = true;



    [Header("Rendering")]
    public Material material;
    public bool renderCloth = true;

    private Mesh _mesh;
    private MaterialPropertyBlock _mpb;
    private GpuXpbdSolver _solver;    
    private bool _meshBuilt;



    #region === Builder ===
    public override void Build(out GpuParticle[] particles, out GpuDistanceConstraint[] constraints, float radius)
    {
        int nX = Mathf.Max(1, numParticlesX);
        int nY = Mathf.Max(1, numParticlesY);
        int particleCount = nX * nY;

        particles = new GpuParticle[particleCount];

        float dx = (nX > 1) ? width / (nX - 1) : 0f;
        float dy = (nY > 1) ? height / (nY - 1) : 0f;
        float offsetX = (nX > 1) ? width * 0.5f : 0f;
        float offsetY = (nY > 1) ? height * 0.5f : 0f;

        int idx = 0;
        for (int j = 0; j < nY; j++)
            for (int i = 0; i < nX; i++)
            {
                Vector3 localPos = new Vector3(i * dx - offsetX, j * dy - offsetY, 0f);
                Vector3 worldPos = transform.TransformPoint(localPos);
                particles[idx++] = new GpuParticle(worldPos, particleMass, radius);
            }

        if (pinTopRow)
        {
            for (int x = 0; x < nX; x++)
            {
                int idTop = (nY - 1) * nX + x;
                var p = particles[idTop];
                p.m = 0f;
                p.w = 0f;
                particles[idTop] = p;
            }
        }

        var cons = new List<GpuDistanceConstraint>();

        float dDiag = Mathf.Sqrt(dx * dx + dy * dy); 
        float dFlexX = 2f * dx;                       
        float dFlexY = 2f * dy;                     

        for (int y = 0; y < nY; y++)
        {
            for (int x = 0; x < nX; x++)
            {
                int i = y * nX + x;

                if (useStructural)
                {
                    if (x + 1 < nX) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 1), dx, structuralCompliance));
                    if (y + 1 < nY) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + nX), dy, structuralCompliance));
                }


                if (useShear && x + 1 < nX && y + 1 < nY)
                {
                    cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 1 + nX), dDiag, shearCompliance));
                    cons.Add(new GpuDistanceConstraint((uint)(i + 1), (uint)(i + nX), dDiag, shearCompliance));
                }

                if (useFlexion)
                {
                    if (x + 2 < nX) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 2), dFlexX, flexionCompliance));
                    if (y + 2 < nY) cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 2 * nX), dFlexY, flexionCompliance));
                }
            }
        }

        constraints = cons.ToArray();
    }
    #endregion

    #region === Rendering ===
    public override void InitRenderer(GpuXpbdSolver solver)
    {
        _solver = solver;
        _mpb ??= new MaterialPropertyBlock();
        BuildMesh();
    }
    void BuildMesh()
    {
        if (_meshBuilt) return;

        BuildRenderTopology(out var tris, out var uvs);

        int vCount = Mathf.Max(1, count);
        _mesh = new Mesh();
        _mesh.indexFormat = (vCount > 65535)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(new Vector3[vCount]); // Dummy
        _mesh.SetUVs(0, uvs);
        _mesh.SetIndices(tris, MeshTopology.Triangles, 0, false);
        _mesh.RecalculateBounds();

        _meshBuilt = true;
    }
    void BuildRenderTopology(out int[] triangles, out Vector2[] uvs)
    {
        int nX = Mathf.Max(1, numParticlesX);
        int nY = Mathf.Max(1, numParticlesY);
        int vCount = nX * nY;

        uvs = new Vector2[vCount];
        for (int y = 0; y < nY; y++)
        {
            float v = (nY > 1) ? (float)y / (nY - 1) : 0f;
            for (int x = 0; x < nX; x++)
            {
                float u = (nX > 1) ? (float)x / (nX - 1) : 0f;
                uvs[y * nX + x] = new Vector2(u, v);
            }
        }

        if (nX < 2 || nY < 2) { triangles = System.Array.Empty<int>(); return; }

        int quadsX = nX - 1, quadsY = nY - 1;
        triangles = new int[quadsX * quadsY * 6];
        int t = 0;
        for (int y = 0; y < quadsY; y++)
            for (int x = 0; x < quadsX; x++)
            {
                int i0 = y * nX + x;
                int i1 = i0 + 1;
                int i2 = i0 + nX;
                int i3 = i2 + 1;

                triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
            }
    }
    
    void LateUpdate()
    {
        if (!_meshBuilt || _solver == null || material == null || !renderCloth) return;
        var buffers = _solver.Buffers;
        if (buffers == null || buffers.ParticleBuffer == null || count <= 0) return;

        material.SetBuffer("_Particles", buffers.ParticleBuffer);

        _mpb.SetInt("_StartIndex", startIndex);
        _mpb.SetInt("_GridSizeX", numParticlesX);
        _mpb.SetInt("_GridSizeY", numParticlesY);

        Graphics.DrawMesh(
            _mesh,
            Matrix4x4.identity,
            material,
            gameObject.layer,
            null,
            0,
            _mpb,
            true,  // cast shadows
            true,  // receive shadows (URP Forward)
            false
        );
    }
    #endregion

    private void OnDrawGizmos()
    {
        Vector3 size = new Vector3(width, height, 0.01f);

        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;

        var b = CurrentBounds;
        if (b.size.sqrMagnitude > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Vector3 size = new Vector3(width, height, 0.01f);
        Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;

        var b = CurrentBounds;
        if (b.size.sqrMagnitude > 0f)
        {
            Gizmos.color = new Color(1, 0.5f, 0f, 0.1f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }

}
