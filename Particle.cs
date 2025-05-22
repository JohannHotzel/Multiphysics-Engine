using UnityEngine;

public class Particle
{
    public Vector3 positionP;
    public Vector3 positionX;
    public Vector3 velocity;
    public float m;
    public float w;
    public float radius;

    //float radius;
    //Color color;

    public Particle(Vector3 position, float m, float radius)
    {
        this.positionP = position;
        this.positionX = position;
        this.velocity = Vector3.zero;
        this.m = m;
        this.w = m == 0 ? 0 : 1 / m;
        this.radius = radius;
    }






}
