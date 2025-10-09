using UnityEngine;
using UnityEngine.Rendering;




[RequireComponent(typeof(Transform))]
public class GpuXpbdRenderer : MonoBehaviour
{
    [Header("Source")]
    public GpuXpbdSolver solver;

    [Header("Particles")]
    public bool drawParticles = true;
    public Mesh visualMesh;
    public Material particleMaterial;         
    public float visualParticleSize = 0.05f;
    public Color particleColor = Color.blue;

    [Header("Constraints (thin lines)")]
    public bool drawConstraints = true;
    public Material constraintMaterial;      
    public Color constraintColor = new Color(0f, 0.8f, 1f, 1f);

    [Header("Bounds")]
    public Bounds manualBounds = new Bounds(Vector3.zero, Vector3.one * 50f);

    // ---- Particles (indirect instancing) ----
    readonly uint[] argsParticles = new uint[5];
    ComputeBuffer argsBufferParticles;

    // ---- Constraints (procedural lines) ----
    readonly uint[] argsLines = new uint[4];   // [vertexCountPerInstance, instanceCount, startVertex, startInstance]
    ComputeBuffer argsBufferLines;

    Bounds drawBounds;

    ComputeBuffer cachedParticleBuffer;
    int cachedParticleCount;
    int cachedConstraintCount = -1;

    void OnEnable()
    {
        TryInitMaterialsAndMesh();
        RebindAll();
        UpdateBounds();
    }

    void OnDisable()
    {
        if (argsBufferParticles != null) { argsBufferParticles.Release(); argsBufferParticles = null; }
        if (argsBufferLines != null) { argsBufferLines.Release(); argsBufferLines = null; }
    }

    void Update()
    {
        if (solver == null || solver.Buffers == null) return;

        // --- Particles ---
        if (drawParticles && particleMaterial != null && visualMesh != null) 
        {
            var pBuf = solver.Buffers.ParticleBuffer;
            var pCount = solver.Buffers.ParticleCount;

            if (pBuf != null && pCount > 0)
            {
                if (cachedParticleBuffer != pBuf || cachedParticleCount != pCount)
                    RebindParticles();

                Graphics.DrawMeshInstancedIndirect(
                    visualMesh, 0, particleMaterial, drawBounds, argsBufferParticles,
                    0, null, ShadowCastingMode.On, receiveShadows: false, layer: gameObject.layer
                );
            }
        }

        // --- Constraints (thin GPU lines) ---
        if (drawConstraints && constraintMaterial != null)
        {
            var cBuf = solver.Buffers.ConstraintBuffer;
            int cCount = solver.Buffers.ConstraintCount;
            var pBuf = solver.Buffers.ParticleBuffer;

            if (cBuf != null && cCount > 0 && pBuf != null)
            {
                if (argsBufferLines == null)
                    argsBufferLines = new ComputeBuffer(1, sizeof(uint) * 4, ComputeBufferType.IndirectArguments);

                if (cCount != cachedConstraintCount)
                {
                    argsLines[0] = (uint)(cCount * 2); // 2 Vertices per Constraint (i->j)
                    argsLines[1] = 1;
                    argsLines[2] = 0;
                    argsLines[3] = 0;
                    argsBufferLines.SetData(argsLines);
                    cachedConstraintCount = cCount;
                }

                constraintMaterial.SetBuffer("_ParticleBuffer", pBuf);
                constraintMaterial.SetBuffer("_Constraints", cBuf);
                constraintMaterial.SetInt("_ConstraintCount", cCount);
                constraintMaterial.SetColor("_BaseColor", constraintColor);

                Graphics.DrawProceduralIndirect(
                    constraintMaterial, drawBounds, MeshTopology.Lines, argsBufferLines, 0,
                    null, null, ShadowCastingMode.Off, receiveShadows: false, layer: gameObject.layer
                );
            }
        }
    }

    void TryInitMaterialsAndMesh()
    {
        if (visualMesh == null)
            visualMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        if (particleMaterial == null)
        {
            Debug.LogError("[GpuXpbdRenderer] Missing particle material.");
            enabled = false;
            return;
        }

        particleMaterial.EnableKeyword("UNITY_PROCEDURAL_INSTANCING_ENABLED");

        particleMaterial.SetFloat("_Size", visualParticleSize);
        particleMaterial.SetColor("_BaseColor", particleColor);
    }

    void RebindAll()
    {
        RebindParticles();
        // Lines: Args are set dynamically in Update()
    }

    void RebindParticles()
    {
        if (solver == null || solver.Buffers == null) return;

        var buf = solver.Buffers.ParticleBuffer;
        var count = solver.Buffers.ParticleCount;
        if (buf == null || count <= 0) return;

        cachedParticleBuffer = buf;
        cachedParticleCount = count;

        particleMaterial.SetBuffer("_ParticleBuffer", cachedParticleBuffer);
        particleMaterial.SetFloat("_Size", visualParticleSize);
        particleMaterial.SetColor("_BaseColor", particleColor);

        if (argsBufferParticles == null)
            argsBufferParticles = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

        argsParticles[0] = visualMesh != null ? (uint)visualMesh.GetIndexCount(0) : 0u;
        argsParticles[1] = (uint)cachedParticleCount;
        argsParticles[2] = visualMesh != null ? (uint)visualMesh.GetIndexStart(0) : 0u;
        argsParticles[3] = visualMesh != null ? (uint)visualMesh.GetBaseVertex(0) : 0u;
        argsParticles[4] = 0u;
        argsBufferParticles.SetData(argsParticles);
    }

    void UpdateBounds()
    {
        drawBounds = manualBounds;
    }
}
