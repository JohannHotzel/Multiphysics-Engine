using UnityEngine;
using UnityEngine.LowLevelPhysics;


[RequireComponent(typeof(MeshCollider))]
public class MeshColliderTest : MonoBehaviour
{


    private MeshCollider meshCollider;

    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
    }

    void Update()
    {
 

    }
}
