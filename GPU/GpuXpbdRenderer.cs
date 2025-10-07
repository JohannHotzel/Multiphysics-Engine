using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Transform))]
public class GpuXpbdRenderer : MonoBehaviour
{
    [Header("Source")]
    public GpuXpbdSolver solver;

    [Header("Rendering")]
    public Mesh visualMesh;
    public Material particleMaterial;
    public float visualParticleSize = 0.05f;
    public Color particleColor = Color.blue;

    [Header("Bounds")]
    public Bounds manualBounds = new Bounds(Vector3.zero, Vector3.one * 50f);

    readonly uint[] args = new uint[5];
    ComputeBuffer argsBuffer;
    Bounds drawBounds;

    ComputeBuffer cachedParticleBuffer;
    int cachedParticleCount;

    void OnEnable()
    {
        TryInitMaterialAndMesh();
        Rebind();
        UpdateBounds();
    }

    void OnDisable()
    {
        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
    }

    void Update()
    {
        if (solver == null || solver.Buffers == null) return;
        if (particleMaterial == null || visualMesh == null) return;

        var buf = solver.Buffers.ParticleBuffer;
        var count = solver.Buffers.ParticleCount;

        if (buf == null || count <= 0) return;

        if (cachedParticleBuffer != buf || cachedParticleCount != count)
            Rebind();

        Graphics.DrawMeshInstancedIndirect(
            visualMesh, 0, particleMaterial, drawBounds, argsBuffer,
            0, null, ShadowCastingMode.On, receiveShadows: false, layer: gameObject.layer
        );
    }

    void TryInitMaterialAndMesh()
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
    }
    void Rebind()
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

        if (argsBuffer == null)
            argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

        args[0] = visualMesh != null ? (uint)visualMesh.GetIndexCount(0) : 0u;
        args[1] = (uint)cachedParticleCount;
        args[2] = visualMesh != null ? (uint)visualMesh.GetIndexStart(0) : 0u;
        args[3] = visualMesh != null ? (uint)visualMesh.GetBaseVertex(0) : 0u;
        args[4] = 0u;
        argsBuffer.SetData(args);
    }
    void UpdateBounds()
    {
        drawBounds = manualBounds;
    }
}
