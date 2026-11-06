using System;
using UnityEngine;

public readonly struct PredatorAlert
{
    public readonly PredatorFSM Source;
    public readonly GameObject Target;
    public readonly Vector3 LastKnownPosition;
    public readonly float Timestamp;

    public PredatorAlert(PredatorFSM source, GameObject target, Vector3 lastKnownPosition, float timestamp)
    {
        Source = source;
        Target = target;
        LastKnownPosition = lastKnownPosition;
        Timestamp = timestamp;
    }
}

public sealed class PredatorAlertSystem : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float alertLifetime = 5f;

    public event Action<PredatorAlert> AlertRaised;

    public void Broadcast(PredatorFSM source, GameObject target, Vector3 lastKnownPosition)
    {
        if (source == null || target == null || !target.activeInHierarchy ||
            SafeZoneVolume.IsPositionSafe(target.transform.position)) return;

        PredatorAlert alert = new PredatorAlert(source, target, lastKnownPosition, Time.time);
        lastAlert = alert;
        hasAlert = true;
        AlertRaised?.Invoke(alert);
    }

    public bool TryGetRecentAlert(out PredatorAlert alert)
    {
        alert = default;
        if (!hasAlert || !IsRecent(lastAlert)) return false;

        alert = lastAlert;
        return true;
    }

    public bool IsRecent(PredatorAlert alert)
    {
        float age = Time.time - alert.Timestamp;
        return age >= 0f && age <= alertLifetime && alert.Target != null &&
            alert.Target.activeInHierarchy && !SafeZoneVolume.IsPositionSafe(alert.Target.transform.position);
    }

    public void Configure(float lifetime)
    {
        alertLifetime = Mathf.Max(0.1f, lifetime);
    }

    private PredatorAlert lastAlert;
    private bool hasAlert;

    private void OnValidate() { alertLifetime = Mathf.Max(0.1f, alertLifetime); }
}
