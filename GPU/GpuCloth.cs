using System.Collections.Generic;
using UnityEngine;

public class GpuCloth : MonoBehaviour
{
    [Header("Grid")]
    public int numParticlesX = 21;
    public int numParticlesY = 21;
    public float width = 10f;
    public float height = 10f;

    [Header("Pinning")]
    public bool pinTopRow = true;

    [Header("Overrides (optional)")]
    public float? particleRadiusOverride;
    public float? complianceOverride;

    public ClothData Build(float defaultRadius, float defaultCompliance)
    {
        int nX = Mathf.Max(1, numParticlesX);
        int nY = Mathf.Max(1, numParticlesY);
        int particleCount = nX * nY;

        var particles = new GpuParticle[particleCount];

        float dx = (nX > 1) ? width / (nX - 1) : 0f;
        float dy = (nY > 1) ? height / (nY - 1) : 0f;
        float offsetX = (nX > 1) ? width * 0.5f : 0f;
        float offsetY = (nY > 1) ? height * 0.5f : 0f;

        float radius = particleRadiusOverride ?? defaultRadius;
        float compliance = complianceOverride ?? defaultCompliance;

        int idx = 0;
        for (int j = 0; j < nY; j++)
            for (int i = 0; i < nX; i++)
            {
                Vector3 localPos = new Vector3(i * dx - offsetX, j * dy - offsetY, 0f);
                Vector3 worldPos = transform.TransformPoint(localPos);
                particles[idx++] = new GpuParticle(worldPos, 1f, radius);
            }

        if (pinTopRow)
        {
            for (int x = 0; x < nX; x++)
            {
                int idTop = (nY - 1) * nX + x;
                var p = particles[idTop]; p.m = 0f; p.w = 0f; particles[idTop] = p;
            }
        }

        var consTmp = new List<GpuDistanceConstraint>();
        for (int y = 0; y < nY; y++)
            for (int x = 0; x < nX; x++)
            {
                int i = y * nX + x;
                if (x + 1 < nX) consTmp.Add(new GpuDistanceConstraint((uint)i, (uint)(i + 1), dx, compliance));
                if (y + 1 < nY) consTmp.Add(new GpuDistanceConstraint((uint)i, (uint)(i + nX), dy, compliance));
            }

        return new ClothData(particles, consTmp.ToArray());
    }
}

public struct ClothData
{
    public GpuParticle[] particles;
    public GpuDistanceConstraint[] constraints;
    public ClothData(GpuParticle[] p, GpuDistanceConstraint[] c) { particles = p; constraints = c; }
}
