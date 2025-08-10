using UnityEngine;
using UnityEngine.Rendering;

public class GpuXpbdSolver : MonoBehaviour
{

    public ComputeShader compute;
    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;  
    public float height = 10f;  

    public float wallZ = 0f;

    public Vector3 boundsMin = new Vector3(-5f, -5f, -5f);
    public Vector3 boundsMax = new Vector3(5f, 5f, 5f);
    
    public float gravity = 9.81f;

    public float gizmoSize = 0.05f;

    ComputeBuffer particleBuffer;
    Particle[] cpuParticles;

    int kUpdate;
    const int THREADS = 256;
    int particleCount;

    struct Particle
    {
        public Vector3 pos;
        public Vector3 vel;
    }

    void Start()
    {
        particleCount = numParticlesX * numParticlesY;
        cpuParticles = new Particle[particleCount];

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

                cpuParticles[idx].pos = worldPos;
                cpuParticles[idx].vel = Vector3.zero;
                idx++;
            }
        }

        if (particleBuffer != null) particleBuffer.Release();
        particleBuffer = new ComputeBuffer(particleCount, sizeof(float) * 6);
        particleBuffer.SetData(cpuParticles);

        kUpdate = compute.FindKernel("Update");
        compute.SetBuffer(kUpdate, "_Particles", particleBuffer);


        compute.SetVector("_BoundsMin", boundsMin);
        compute.SetVector("_BoundsMax", boundsMax);
        compute.SetFloat("_Gravity", gravity);
        compute.SetInt("_ParticleCount", particleCount);
    }

    void FixedUpdate()
    {
        compute.SetFloat("_DeltaTime", 0.02f);

        int groups = Mathf.CeilToInt(particleCount / (float)THREADS);
        compute.Dispatch(kUpdate, groups, 1, 1);

        particleBuffer.GetData(cpuParticles);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);

        if (cpuParticles == null) return;

        Gizmos.color = Color.yellow;
        foreach (var p in cpuParticles)
        {
            Gizmos.DrawSphere(p.pos, gizmoSize);
        }

    }

    void OnDestroy()
    {
        if (particleBuffer != null) particleBuffer.Release();
    }
}
