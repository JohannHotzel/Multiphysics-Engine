using UnityEngine;

public class MultiphysicsCloth : MonoBehaviour
{
    [Header("Cloth Settings")]
    public int numParticlesX = 10;
    public int numParticlesY = 10;
    public float width = 1f;
    public float height = 1f;
    public float particleMass = 1f;
    public float stiffness = 1f;


    [HideInInspector] public Particle[,] particles;

    void Awake()
    {

    }

    public void BuildCloth(XPBDSolver solver)
    {
        Debug.Log("Building cloth");
    }

}
