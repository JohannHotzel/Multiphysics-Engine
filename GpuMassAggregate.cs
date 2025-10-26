using UnityEngine;

public abstract class GpuMassAggregate : MonoBehaviour
{
    public int startIndex { get; set; }
    public int count { get; set; }

    [HideInInspector] public Vector3 aabbMin = Vector3.positiveInfinity;
    [HideInInspector] public Vector3 aabbMax = Vector3.negativeInfinity;

    public Bounds CurrentBounds =>
        (float.IsInfinity(aabbMin.x) || float.IsInfinity(aabbMax.x))
            ? new Bounds(transform.position, Vector3.zero)
            : new Bounds((aabbMin + aabbMax) * 0.5f, aabbMax - aabbMin);

    public abstract void Build(out GpuParticle[] particles, out GpuDistanceConstraint[] constraints, float radius);
    public abstract void InitRenderer(GpuXpbdSolver solver);

}
