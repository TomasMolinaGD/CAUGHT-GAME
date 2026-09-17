using UnityEngine;

[RequireComponent(typeof(BoidLife), typeof(BoidAgent))]
public sealed class BoidHitReaction : MonoBehaviour
{
    [SerializeField, Min(0f)] private float hitStunDuration = 1.25f;

    private BoidLife life;
    private BoidAgent agent;

    private void Awake()
    {
        life = GetComponent<BoidLife>();
        agent = GetComponent<BoidAgent>();
        life.Damaged += HandleDamage;
    }

    private void OnDestroy()
    {
        if (life != null)
        {
            life.Damaged -= HandleDamage;
        }
    }

    private void HandleDamage(BoidLife _, float damage)
    {
        if (damage > 0f)
        {
            agent.PauseMovement(hitStunDuration);
        }
    }

    private void OnValidate()
    {
        hitStunDuration = Mathf.Max(0f, hitStunDuration);
    }
}
