using UnityEngine;

/// <summary>
/// Exposes an interactable interest target to flock agents.
/// </summary>
[RequireComponent(typeof(InterestLife))]
[DisallowMultipleComponent]
public sealed class BoidInterest : MonoBehaviour
{
    private InterestLife life;

    public Vector3 Position => transform.position;
    public bool IsAvailable => isActiveAndEnabled && life != null && life.IsAlive;

    private void Awake()
    {
        life = GetComponent<InterestLife>();
    }

    public bool TryApplyDamage(float amount)
    {
        if (!IsAvailable || amount <= 0f)
        {
            return false;
        }

        life.TakeDamage(amount);
        return true;
    }
}
