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

    public bool TryCapture(GameObject target, float captureRadius, float currentDistance,
        bool allowExplicitThreat = false)
    {
        if (!CanCapture(target, captureRadius, currentDistance, allowExplicitThreat)) return false;

        if (!PredatorCaptureRegistry.TryCapture(target, allowExplicitThreat))
        {
            cooldownRemaining = attackCooldown;
            return false;
        }
        cooldownRemaining = attackCooldown;
        return true;
    }

    private bool CanCapture(GameObject target, float captureRadius, float currentDistance,
        bool allowExplicitThreat)
    {
        if (!IsAttackReady || target == null || currentDistance > captureRadius ||
            SafeZoneVolume.IsPositionSafe(target.transform.position)) return false;

        bool supportedTarget = PredatorCaptureRegistry.IsSupportedTarget(target);
        bool explicitThreat = allowExplicitThreat && PredatorCaptureRegistry.HasThreatComponent(target);
        return (supportedTarget || explicitThreat) &&
            !PredatorCaptureRegistry.IsCaptured(target, allowExplicitThreat);
    }

    private void OnValidate() { attackCooldown = Mathf.Max(0.1f, attackCooldown); }
}
