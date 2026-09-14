using UnityEngine;

/// <summary>
/// Identifies an object that flock agents can approach and interact with.
/// The hunter can generate any prefab carrying this marker.
/// </summary>
[RequireComponent(typeof(InterestLife))]
public sealed class BoidInterest : MonoBehaviour
{
}
