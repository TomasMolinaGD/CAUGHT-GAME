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
    [Header("Diagnostics")]
    [SerializeField] private bool logCaptureDiagnostics;
    private float cooldownRemaining;
    private PredatorNavigationMovementController navigationController;

    public float CooldownRemaining => cooldownRemaining;
    public bool IsAttackReady => cooldownRemaining <= 0f;
    public CaptureAttemptResult LastCaptureAttemptResult { get; private set; }
    public int CaptureAttemptCount { get; private set; }

    private void Awake()
    {
        navigationController = GetComponent<PredatorNavigationMovementController>();
    }

    public void Tick(float deltaTime)
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public bool TryCapture(GameObject target, float captureRadius, float currentDistance,
        bool allowExplicitThreat = false)
    {
        float cooldownAtAttempt = cooldownRemaining;
        bool targetInSafeZone = target != null && IsPositionSafe(target.transform.position);
        string captureAvailability = GetCaptureAvailability(target);
        string rejectionReason = GetCaptureRejectionReason(
            target, captureRadius, currentDistance, allowExplicitThreat, targetInSafeZone);
        if (!IsAttackReady)
        {
            LogCaptureDiagnostic(target, captureRadius, currentDistance, cooldownAtAttempt,
                targetInSafeZone, captureAvailability, "Rejected", rejectionReason);
            return false;
        }

        bool captured = rejectionReason == null &&
            PredatorCaptureRegistry.TryCapture(target, allowExplicitThreat);
        if (!captured && rejectionReason == null)
            rejectionReason = "capture receiver or registry rejected the request";
        cooldownRemaining = attackCooldown;
        CaptureAttemptCount++;
        LastCaptureAttemptResult = captured
            ? CaptureAttemptResult.Succeeded
            : CaptureAttemptResult.Failed;
        LogCaptureDiagnostic(target, captureRadius, currentDistance, cooldownAtAttempt,
            targetInSafeZone, captureAvailability, captured ? "Succeeded" : "Failed",
            captured ? "capture accepted" : rejectionReason);
        return captured;
    }

    public void ReportAttackEndedWithoutCapture(GameObject target, float captureRadius,
        float currentDistance, string reason)
    {
        if (!logCaptureDiagnostics) return;

        bool targetInSafeZone = target != null && IsPositionSafe(target.transform.position);
        LogCaptureDiagnostic(target, captureRadius, currentDistance, cooldownRemaining,
            targetInSafeZone, GetCaptureAvailability(target), "Skipped", reason);
    }

    private string GetCaptureRejectionReason(GameObject target, float captureRadius,
        float currentDistance, bool allowExplicitThreat, bool targetInSafeZone)
    {
        if (!IsAttackReady) return "cooldown is active";
        if (target == null) return "target is null";
        if (currentDistance > captureRadius) return "target is outside captureRadius";
        if (targetInSafeZone) return "target is inside a Safe Zone";

        bool supportedTarget = PredatorCaptureRegistry.IsSupportedTarget(target);
        bool explicitThreat = allowExplicitThreat && PredatorCaptureRegistry.HasThreatComponent(target);
        if (!supportedTarget && !explicitThreat) return "target is not capturable";
        if (PredatorCaptureRegistry.IsCaptured(target, allowExplicitThreat))
            return "target is captured or IsCaptureAvailable is false";
        return null;
    }

    private static string GetCaptureAvailability(GameObject target)
    {
        if (target == null) return "n/a";
        ICapturableTarget receiver = target.GetComponent<ICapturableTarget>();
        if (receiver == null)
            receiver = target.GetComponentInChildren<ICapturableTarget>(true);
        return receiver != null ? receiver.IsCaptureAvailable.ToString() : "n/a";
    }

    private void LogCaptureDiagnostic(GameObject target, float captureRadius,
        float currentDistance, float cooldown, bool targetInSafeZone,
        string captureAvailability, string result, string reason)
    {
        if (!logCaptureDiagnostics) return;

        Debug.Log(
            $"Wolf capture diagnostic: wolf='{name}', target='{(target != null ? target.name : "<null>")}', " +
            $"distance={currentDistance:F3}, captureRadius={captureRadius:F3}, cooldown={cooldown:F3}, " +
            $"safeZone={targetInSafeZone}, IsCaptureAvailable={captureAvailability}, " +
            $"result={result}, reason='{reason}'.",
            this);
    }

    private bool IsPositionSafe(Vector3 position)
    {
        if (SafeZoneVolume.IsPositionSafe(position)) return true;
        if (navigationController == null)
            navigationController = GetComponent<PredatorNavigationMovementController>();
        return navigationController != null &&
            navigationController.IsPositionInSafeZone(position);
    }

    private void OnValidate() { attackCooldown = Mathf.Max(0.1f, attackCooldown); }
}
