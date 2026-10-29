using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class PredatorPerception : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float viewDistance = 18f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
    [SerializeField] private LayerMask alienLayerMask = (1 << 9) | (1 << 10);
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float eyeHeight = 1.5f;
    private readonly Collider[] results = new Collider[64];
    private readonly HashSet<GameObject> candidateTargets = new HashSet<GameObject>();
    private readonly List<GameObject> targets = new List<GameObject>();
    private float refreshTimer;
    private bool wasVisible;
    private GameObject target;

    public GameObject Target => target;
    public float ViewDistance => viewDistance;
    public bool CanSeeTarget { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public bool NewlyDetected { get; private set; }

    private void OnEnable() { refreshTimer = 0f; }

    public void Configure(float distance, float viewAngle, LayerMask alienLayers,
        LayerMask obstacles, float interval, float height)
    {
        viewDistance = Mathf.Max(0.1f, distance);
        fieldOfView = Mathf.Clamp(viewAngle, 1f, 360f);
        alienLayerMask = alienLayers | (1 << 10);
        obstacleLayers = obstacles;
        refreshInterval = Mathf.Max(0.02f, interval);
        eyeHeight = Mathf.Max(0f, height);
    }

    public void ConfigureTargets(IReadOnlyList<GameObject> targets)
    {
        this.targets.Clear();
        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++)
        {
            AddTarget(targets[i]);
        }
    }

    public void AddTarget(GameObject target)
    {
        if (target != null && !targets.Contains(target))
            targets.Add(target);
    }

    public void Tick(float deltaTime)
    {
        NewlyDetected = false;
        refreshTimer -= Mathf.Max(0f, deltaTime);
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;
        GameObject previousTarget = target;
        target = FindVisibleTarget();
        CanSeeTarget = target != null;

        if (CanSeeTarget)
        {
            LastSeenPosition = target.transform.position;
            HasLastKnownPosition = true;
            NewlyDetected = !wasVisible || previousTarget != target;
        }
        wasVisible = CanSeeTarget;
    }

    public void ReceiveAlert(GameObject alertedTarget, Vector3 position)
    {
        if (alertedTarget == null || PredatorCaptureRegistry.IsCaptured(alertedTarget) ||
            SafeZoneVolume.IsPositionSafe(alertedTarget.transform.position)) return;
        target = alertedTarget;
        LastSeenPosition = position;
        HasLastKnownPosition = true;
    }

    public bool CanTrackTarget(GameObject candidate)
    {
        return candidate != null && candidate.activeInHierarchy &&
            !PredatorCaptureRegistry.IsCaptured(candidate) &&
            !SafeZoneVolume.IsPositionSafe(candidate.transform.position) &&
            IsWithinViewDistance(candidate.transform.position) &&
            HasLineOfSight(candidate.transform.position);
    }

    public void UpdateTrackedPosition(GameObject candidate)
    {
        if (!CanTrackTarget(candidate)) return;
        target = candidate;
        LastSeenPosition = candidate.transform.position;
        HasLastKnownPosition = true;
    }

    public void ClearTarget()
    {
        target = null;
        CanSeeTarget = false;
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

    private bool IsWithinViewDistance(Vector3 targetPosition)
    {
        Vector3 direction = Vector3.ProjectOnPlane(targetPosition - transform.position, Vector3.up);
        return direction.sqrMagnitude <= viewDistance * viewDistance;
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 destination = targetPosition + Vector3.up * eyeHeight;
        return !Physics.Linecast(origin, destination, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private GameObject FindVisibleTarget()
    {
        candidateTargets.Clear();
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            GameObject candidate = targets[i];
            if (candidate == null) targets.RemoveAt(i);
            else candidateTargets.Add(candidate);
        }

        int count = Physics.OverlapSphereNonAlloc(transform.position, viewDistance,
            results, alienLayerMask | (1 << 10), QueryTriggerInteraction.Collide);

        GameObject closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider detectedCollider = results[i];
            GameObject candidate = GetTargetObject(detectedCollider);
            if (candidate != null) candidateTargets.Add(candidate);
        }

        foreach (GameObject candidate in candidateTargets)
        {
            if (candidate == null || !candidate.activeInHierarchy ||
                PredatorCaptureRegistry.IsCaptured(candidate)) continue;

            Vector3 candidatePosition = candidate.transform.position;
            if (SafeZoneVolume.IsPositionSafe(candidatePosition) ||
                !IsInsideView(candidatePosition) || !HasLineOfSight(candidatePosition))
                continue;

            float distance = Vector3.ProjectOnPlane(
                candidatePosition - transform.position, Vector3.up).sqrMagnitude;
            if (distance >= closestDistance) continue;
            closest = candidate;
            closestDistance = distance;
        }
        return closest;
    }

    private static GameObject GetTargetObject(Collider detectedCollider)
    {
        if (detectedCollider == null) return null;
        BoidLife life = detectedCollider.GetComponentInParent<BoidLife>();
        if (life != null) return life.gameObject;
        BoidThreat threat = detectedCollider.GetComponentInParent<BoidThreat>();
        return threat != null ? threat.gameObject : null;
    }

    private void OnValidate()
    {
        alienLayerMask |= 1 << 10;
        viewDistance = Mathf.Max(0.1f, viewDistance);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
        eyeHeight = Mathf.Max(0f, eyeHeight);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CanSeeTarget ? Color.red : new Color(1f, 0.7f, 0f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeight, viewDistance);
        Vector3 forward = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, forward * viewDistance);
        forward = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, forward * viewDistance);
        if (CanSeeTarget && target != null)
            Gizmos.DrawLine(transform.position + Vector3.up * eyeHeight,
                target.transform.position + Vector3.up * eyeHeight);
    }
}
