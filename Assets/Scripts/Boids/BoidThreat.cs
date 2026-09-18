using UnityEngine;

/// <summary>
/// Describes a threat without coupling flock agents to a concrete hunter type.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoidThreat : MonoBehaviour
{
    [SerializeField, Min(0f)] private float avoidanceMultiplier = 1f;

    public Vector3 Position => transform.position;
    public bool IsAvailable => isActiveAndEnabled;
    public float AvoidanceMultiplier => avoidanceMultiplier;

    private void OnValidate()
    {
        avoidanceMultiplier = Mathf.Max(0f, avoidanceMultiplier);
    }
}
