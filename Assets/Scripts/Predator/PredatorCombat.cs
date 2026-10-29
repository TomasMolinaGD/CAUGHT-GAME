using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorCombat : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    private float cooldownRemaining;

    public float CooldownRemaining => cooldownRemaining;
    public bool IsAttackReady => cooldownRemaining <= 0f;

    public void Tick(float deltaTime)
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public bool TryCapture(GameObject target, float captureRadius, float currentDistance)
    {
        if (!IsAttackReady || target == null || PredatorCaptureRegistry.IsCaptured(target) ||
            currentDistance > captureRadius || SafeZoneVolume.IsPositionSafe(target.transform.position))
            return false;

        if (!PredatorCaptureRegistry.TryCapture(target)) return false;
        cooldownRemaining = attackCooldown;
        return true;
    }

    private void OnValidate() { attackCooldown = Mathf.Max(0.1f, attackCooldown); }
}
