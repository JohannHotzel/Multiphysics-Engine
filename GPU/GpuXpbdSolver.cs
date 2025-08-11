using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.GPUSort;

public class GpuXpbdSolver : MonoBehaviour
{

    [Header("Simulation")]
    public ComputeShader compute;
    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;
    public float height = 10f;
    public float gravity = 9.81f;


    public Vector3 boundsMin = new Vector3(-5f, -5f, -5f);
    public Vector3 boundsMax = new Vector3(5f, 5f, 5f);

    [Header("Rendering")]
    public Mesh quadMesh;               
    public Material particleMaterial;      
    public float particleSize = 0.05f;    
    public Color particleColor = Color.blue;

    const int THREADS = 256;
    int particleCount;
    int kUpdate;

    struct Particle { 
        public Vector3 positionP; // 3 floats
        public Vector3 positionX; // 3 floats
        public Vector3 velocity;  // 3 floats
        public float m;           // 1 float
        public float w;           // 1 float
        public float radius;      // 1 float

        public Particle(Vector3 pos, float mass, float rad)
        {
            positionP = pos;
            positionX = pos;
            velocity = Vector3.zero;
            m = mass;
            w = (mass == 0) ? 0 : 1 / mass;
            radius = rad;
        }
    }

    

    ComputeBuffer particleBuffer;          // StructuredBuffer<Particle>
    ComputeBuffer argsBuffer;              // Indirect draw args
    readonly uint[] args = new uint[5];
    Bounds drawBounds;

    void Start()
    {
        particleCount = numParticlesX * numParticlesY;
        var cpuParticles = new Particle[particleCount];

        float dx = (numParticlesX > 1) ? width / (numParticlesX - 1) : 0f;
        float dy = (numParticlesY > 1) ? height / (numParticlesY - 1) : 0f;

        float offsetX = (numParticlesX > 1) ? width * 0.5f : 0f;
        float offsetY = (numParticlesY > 1) ? height * 0.5f : 0f;

        int idx = 0;
        for (int j = 0; j < numParticlesY; j++)
        {
            for (int i = 0; i < numParticlesX; i++)
            {
                Vector3 localPos = new Vector3(i * dx - offsetX, j * dy - offsetY, 0f);
                Vector3 worldPos = transform.TransformPoint(localPos);
                cpuParticles[idx] = new Particle(worldPos, 1f, particleSize * 0.5f);
                idx++;
            }
        }

        // --- GPU Buffer ---------------------------------------------------------------------------------
        if (particleBuffer != null) particleBuffer.Release();
        int stride = 12 * sizeof(float);
        particleBuffer = new ComputeBuffer(particleCount, stride, ComputeBufferType.Structured);
        particleBuffer.SetData(cpuParticles);

        // --- Compute Shader Setup ---------------------------------------------------------------------------------
        kUpdate = compute.FindKernel("Update");
        compute.SetBuffer(kUpdate, "_Particles", particleBuffer);
        compute.SetVector("_BoundsMin", boundsMin);
        compute.SetVector("_BoundsMax", boundsMax);
        compute.SetFloat("_Gravity", gravity);
        compute.SetInt("_ParticleCount", particleCount);



        // --- Rendering Setup ---------------------------------------------------------------------------------
        if (quadMesh == null)
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        if (particleMaterial == null)
        {
            Debug.LogError("Bitte ein Material mit Shader 'Unlit/GPUParticlesBillboard' zuweisen.");
            enabled = false; return;
        }

        particleMaterial.EnableKeyword("UNITY_PROCEDURAL_INSTANCING_ENABLED");
        particleMaterial.SetBuffer("_ParticleBuffer", particleBuffer);
        particleMaterial.SetFloat("_Size", particleSize);
        particleMaterial.SetColor("_BaseColor", particleColor);

        // Indirect args: [indexCount, instanceCount, startIndex, baseVertex, startInstance]
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        args[0] = quadMesh != null ? (uint)quadMesh.GetIndexCount(0) : 0u;
        args[1] = (uint)particleCount;
        args[2] = quadMesh != null ? (uint)quadMesh.GetIndexStart(0) : 0u;
        args[3] = quadMesh != null ? (uint)quadMesh.GetBaseVertex(0) : 0u;
        args[4] = 0u;
        argsBuffer.SetData(args);

        var center = (boundsMin + boundsMax) * 0.5f;
        var size = (boundsMax - boundsMin) + Vector3.one * (particleSize * 4f);
        drawBounds = new Bounds(center, size * 2f);
    }

    void Update()
    {
        compute.SetFloat("_DeltaTime", Time.deltaTime);
        int groups = Mathf.CeilToInt(particleCount / (float)THREADS);
        compute.Dispatch(kUpdate, groups, 1, 1);

        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 0, particleMaterial, drawBounds, argsBuffer,
            0, null, ShadowCastingMode.Off, receiveShadows: false, layer: gameObject.layer
        );
    }

    void OnDestroy()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
    }
}
