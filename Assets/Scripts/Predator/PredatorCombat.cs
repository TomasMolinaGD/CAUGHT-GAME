using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorCombat : MonoBehaviour
{
    public enum CaptureAttemptResult
    {
        None,
        Succeeded,
        Failed
    }

    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    private float cooldownRemaining;

    public float CooldownRemaining => cooldownRemaining;
    public bool IsAttackReady => cooldownRemaining <= 0f;
    public CaptureAttemptResult LastCaptureAttemptResult { get; private set; }
    public int CaptureAttemptCount { get; private set; }

    public void Tick(float deltaTime)
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public bool TryCapture(GameObject target, float captureRadius, float currentDistance,
        bool allowExplicitThreat = false)
    {
        if (!IsAttackReady) return false;

        bool captured = CanCapture(target, captureRadius, currentDistance, allowExplicitThreat) &&
            PredatorCaptureRegistry.TryCapture(target, allowExplicitThreat);
        cooldownRemaining = attackCooldown;
        CaptureAttemptCount++;
        LastCaptureAttemptResult = captured
            ? CaptureAttemptResult.Succeeded
            : CaptureAttemptResult.Failed;
        return captured;
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
