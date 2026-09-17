using UnityEngine;

[DisallowMultipleComponent]
public sealed class HunterCombat : MonoBehaviour
{
    [Header("Attack Ranges")]
    [SerializeField, Min(0.1f)] private float timeBetweenAttacks = 4f;
    [SerializeField, Min(0.1f)] private float rangedAttackRadius = 8f;
    [SerializeField, Min(0.1f)] private float meleeAttackRadius = 2.2f;

    [Header("Damage")]
    [SerializeField, Min(0.01f)] private float meleeDamage = 35f;
    [SerializeField, Min(0.01f)] private float rangedDamage = 35f;

    [Header("Projectile")]
    [SerializeField, Min(0.1f)] private float rangedProjectileSpeed = 30f;
    [SerializeField, Min(0.1f)] private float rangedProjectileLifetime = 3f;
    [SerializeField] private float rangedProjectileSpawnHeight = 1.35f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float rangedAttackWindup = 0.12f;
    [SerializeField, Min(0f)] private float meleeAttackWindup = 0.25f;

    private float cooldownRemaining;
    private bool requiresMeleeFollowUp;

    public float RangedAttackRadius => rangedAttackRadius;
    public float MeleeAttackRadius => meleeAttackRadius;
    public float RangedAttackWindup => rangedAttackWindup;
    public float MeleeAttackWindup => meleeAttackWindup;
    public float CooldownRemaining => cooldownRemaining;
    public bool IsAttackReady => cooldownRemaining <= 0f;
    public bool RequiresMeleeFollowUp => requiresMeleeFollowUp;

    public void Tick(float deltaTime)
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public bool PerformMeleeAttack(BoidLife target)
    {
        if (!CanDamage(target))
        {
            return false;
        }

        target.TakeDamage(meleeDamage);
        StartCooldown();
        requiresMeleeFollowUp = false;
        return true;
    }

    public bool PerformRangedAttack(BoidLife target)
    {
        if (!CanDamage(target))
        {
            return false;
        }

        Vector3 spawnPosition = transform.position +
            transform.forward * 0.65f +
            Vector3.up * rangedProjectileSpawnHeight;
        HunterProjectile.Create(
            spawnPosition,
            target,
            rangedDamage,
            rangedProjectileSpeed,
            rangedProjectileLifetime);
        StartCooldown();
        requiresMeleeFollowUp = true;
        return true;
    }

    public void ClearTargetCommitment()
    {
        requiresMeleeFollowUp = false;
    }

    private bool CanDamage(BoidLife target)
    {
        return IsAttackReady && target != null && target.IsAlive;
    }

    private void StartCooldown()
    {
        cooldownRemaining = timeBetweenAttacks;
    }

    private void OnValidate()
    {
        timeBetweenAttacks = Mathf.Max(0.1f, timeBetweenAttacks);
        meleeAttackRadius = Mathf.Max(0.1f, meleeAttackRadius);
        rangedAttackRadius = Mathf.Max(meleeAttackRadius, rangedAttackRadius);
        meleeDamage = Mathf.Max(0.01f, meleeDamage);
        rangedDamage = Mathf.Max(0.01f, rangedDamage);
        rangedProjectileSpeed = Mathf.Max(0.1f, rangedProjectileSpeed);
        rangedProjectileLifetime = Mathf.Max(0.1f, rangedProjectileLifetime);
        rangedAttackWindup = Mathf.Max(0f, rangedAttackWindup);
        meleeAttackWindup = Mathf.Max(0f, meleeAttackWindup);
    }
}
