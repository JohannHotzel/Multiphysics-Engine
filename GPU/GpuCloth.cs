using System.Collections.Generic;
using UnityEngine;

public class GpuCloth : MonoBehaviour
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





    [HideInInspector] public int startIndex;  
    [HideInInspector] public int count;      

    [HideInInspector] public Vector3 aabbMin = Vector3.positiveInfinity;
    [HideInInspector] public Vector3 aabbMax = Vector3.negativeInfinity;

    public Bounds CurrentBounds
    {
        get
        {
            if (float.IsInfinity(aabbMin.x) || float.IsInfinity(aabbMax.x))
                return new Bounds(transform.position, Vector3.zero);
            return new Bounds((aabbMin + aabbMax) * 0.5f, aabbMax - aabbMin);
        }
    }

    public void Build(out GpuParticle[] particles, out GpuDistanceConstraint[] constraints, float radius)
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, b.size);
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
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}
