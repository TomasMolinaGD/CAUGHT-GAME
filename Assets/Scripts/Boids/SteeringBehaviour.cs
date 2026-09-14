using UnityEngine;

public abstract class SteeringBehaviour : MonoBehaviour
{
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField, Min(0)] private int priority = 10;

    public float Weight => weight;
    public int Priority => priority;
    public virtual bool IsApplicable => true;

    public abstract Vector3 CalculateSteering();

    protected virtual void OnValidate()
    {
        weight = Mathf.Max(0f, weight);
        priority = Mathf.Max(0, priority);
    }
}
