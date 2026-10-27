using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorPerception : MonoBehaviour
{
    [SerializeField] private FSMAgent hunter;
    [SerializeField, Min(0.1f)] private float viewDistance = 18f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float eyeHeight = 1.5f;

    private float refreshTimer;
    private bool wasVisible;

    public FSMAgent Hunter => hunter;
    public float ViewDistance => viewDistance;
    public bool CanSeeHunter { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public bool NewlyDetected { get; private set; }

    private void OnEnable() { refreshTimer = 0f; }

    public void Tick(float deltaTime)
    {
        NewlyDetected = false;
        refreshTimer -= Mathf.Max(0f, deltaTime);
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;
        if (hunter == null) hunter = FindFirstObjectByType<FSMAgent>();

        CanSeeHunter = hunter != null && hunter.isActiveAndEnabled &&
            !hunter.IsDead && !SafeZoneVolume.IsPositionSafe(hunter.transform.position) &&
            IsInsideView(hunter.transform.position) && HasLineOfSight(hunter.transform.position);

        if (CanSeeHunter)
        {
            LastSeenPosition = hunter.transform.position;
            HasLastKnownPosition = true;
            NewlyDetected = !wasVisible;
        }
        wasVisible = CanSeeHunter;
    }

    public void ReceiveAlert(FSMAgent target, Vector3 position)
    {
        if (target == null || target.IsDead || SafeZoneVolume.IsPositionSafe(target.transform.position)) return;
        hunter = target;
        LastSeenPosition = position;
        HasLastKnownPosition = true;
    }

    public void ClearTarget()
    {
        hunter = null;
        CanSeeHunter = false;
        HasLastKnownPosition = false;
        NewlyDetected = false;
        wasVisible = false;
    }

    private bool IsInsideView(Vector3 targetPosition)
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 direction = Vector3.ProjectOnPlane(targetPosition - eyePosition, Vector3.up);
        if (direction.sqrMagnitude > viewDistance * viewDistance) return false;
        if (direction.sqrMagnitude < 0.0001f) return true;
        return Vector3.Angle(transform.forward, direction) <= fieldOfView * 0.5f;
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 destination = targetPosition + Vector3.up * eyeHeight;
        return !Physics.Linecast(origin, destination, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private void OnValidate()
    {
        viewDistance = Mathf.Max(0.1f, viewDistance);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
        eyeHeight = Mathf.Max(0f, eyeHeight);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CanSeeHunter ? Color.red : new Color(1f, 0.7f, 0f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeight, viewDistance);
        Vector3 forward = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, forward * viewDistance);
        forward = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, forward * viewDistance);
        if (CanSeeHunter && hunter != null)
            Gizmos.DrawLine(transform.position + Vector3.up * eyeHeight,
                hunter.transform.position + Vector3.up * eyeHeight);
    }
}
