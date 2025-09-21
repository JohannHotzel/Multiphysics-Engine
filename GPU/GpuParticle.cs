using UnityEngine;

public struct GpuParticle
{
    public Vector3 positionP; // previous
    public Vector3 positionX; // current/predicted
    public Vector3 positionPredicted; // predicted (for collision handling)
    public Vector3 velocity;
    public float m;
    public float w;
    public float radius;

    public GpuParticle(Vector3 pos, float mass, float rad)
    {
        positionP = pos;
        positionX = pos;
        positionPredicted = pos;
        velocity = Vector3.zero;
        m = mass;
        w = (mass == 0f) ? 0f : 1f / mass;
        radius = rad;
    }
}
