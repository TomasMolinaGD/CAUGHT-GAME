using UnityEngine;

public abstract class SteeringBehaviour : MonoBehaviour
{
    [SerializeField, Min(0f)] private float weight = 1f;

    public float Weight => weight;

    public abstract Vector3 CalculateSteering();

    protected virtual void OnValidate()
    {
        weight = Mathf.Max(0f, weight);
    }
}
