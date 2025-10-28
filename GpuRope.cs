using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GpuRope : GpuMassAggregate
{
    [Header("Rope Geometry")]
    [Min(2)] public int segments = 32;     
    public float length = 5f;                 
    public float totalMass = 1f;
    private float particleMass => totalMass / (segments + 1);

    [Header("Constraints")]
    public float structuralCompliance = 0f;    // 0 = steif
    public bool useFlexion = true;
    public float flexionCompliance = 0f;

    [Header("Pinning")]
    public bool pinStart = true;
    public bool pinEnd = false;

    [Header("Rendering")]
    public bool renderRope = true;
    public Material ropeMaterial;
    [Range(0.001f, 0.2f)] public float visualRadius = 0.03f;

    private Mesh _mesh;
    private bool _meshBuilt;
    private MaterialPropertyBlock _mpb;
    private GpuXpbdSolver _solver;


    public override void Build(out GpuParticle[] particles, out GpuDistanceConstraint[] constraints, float solverDefaultRadius)
    {
        int n = Mathf.Max(2, segments + 1);
        particles = new GpuParticle[n];

        float dx = length / (n - 1);
        Vector3 dir = transform.right;
        Vector3 start = transform.position - dir * (length * 0.5f);

        for (int i = 0; i < n; i++)
        {
            Vector3 p = start + dir * (dx * i);
            particles[i] = new GpuParticle(p, particleMass, solverDefaultRadius);
        }

        if (pinStart)
        {
            var p0 = particles[0];
            p0.m = 0f; p0.w = 0f;
            particles[0] = p0;
        }
        if (pinEnd)
        {
            var pn = particles[n - 1];
            pn.m = 0f; pn.w = 0f;
            particles[n - 1] = pn;
        }


        var cons = new List<GpuDistanceConstraint>(n - 1 + (useFlexion ? n - 2 : 0));
        
        for (int i = 0; i < n - 1; i++)
            cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 1), dx, structuralCompliance));

        if (useFlexion && n >= 3)
        {
            float rest = 2f * dx;
            for (int i = 0; i < n - 2; i++)
                cons.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 2), rest, flexionCompliance));
        }

        constraints = cons.ToArray();
    }


    public override void InitRenderer(GpuXpbdSolver solver)
    {
        _solver = solver;
        _mpb ??= new MaterialPropertyBlock();
        BuildRibbonMesh();
    }
    private void BuildRibbonMesh()
    {
        if (_meshBuilt) return;

        int n = Mathf.Max(2, count);
        int vCount = n * 2;
        int segs = n - 1;
        int idxCount = segs * 6;

        _mesh = new Mesh();
        _mesh.indexFormat = (vCount > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16;

        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var indices = new int[idxCount];

        for (int i = 0; i < n; i++)
        {
            float s = (n > 1) ? (float)i / (n - 1) : 0f;
            uvs[2 * i + 0] = new Vector2(s, 0f);
            uvs[2 * i + 1] = new Vector2(s, 1f);
        }

        int t = 0;

        for (int i = 0; i < segs; i++)
        {
            int a0 = 2 * i + 0;
            int a1 = 2 * i + 1;
            int b0 = 2 * (i + 1) + 0;
            int b1 = 2 * (i + 1) + 1;

            indices[t++] = a0; indices[t++] = b0; indices[t++] = a1;
            indices[t++] = a1; indices[t++] = b0; indices[t++] = b1;
        }

        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        _mesh.RecalculateBounds();

        _meshBuilt = true;
    }


    private void LateUpdate()
    {
        if (!renderRope || ropeMaterial == null || !_meshBuilt || _solver == null) return;
        var buffers = _solver.Buffers;
        if (buffers == null || buffers.ParticleBuffer == null || count <= 1) return;

        ropeMaterial.SetBuffer("_Particles", buffers.ParticleBuffer);

        _mpb.SetInt("_StartIndex", startIndex);
        _mpb.SetInt("_Count", count);
        _mpb.SetFloat("_Radius", visualRadius);

        Graphics.DrawMesh(
            _mesh,
            Matrix4x4.identity,
            ropeMaterial,
            gameObject.layer,
            null,
            0,
            _mpb,
            true,  
            true,
            false
        );
    }



    private void OnDrawGizmos()
    {
        Vector3 sizeLocal = new Vector3(length, Mathf.Max(visualRadius * 2f, 0.01f), Mathf.Max(visualRadius * 2f, 0.01f));

        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, sizeLocal);
        Gizmos.matrix = Matrix4x4.identity;

        var b = CurrentBounds;
        if (b.size.sqrMagnitude > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.10f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 sizeLocal = new Vector3(length, Mathf.Max(visualRadius * 2f, 0.01f), Mathf.Max(visualRadius * 2f, 0.01f));

        Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, sizeLocal);
        Gizmos.matrix = Matrix4x4.identity;

        var b = CurrentBounds;
        if (b.size.sqrMagnitude > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.10f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }






}
