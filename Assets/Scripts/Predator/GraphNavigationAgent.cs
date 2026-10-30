using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GraphNavigationAgent : MonoBehaviour
{
    public enum NavigationStatus
    {
        Idle,
        FollowingDirect,
        FollowingPath,
        Arrived,
        NoPath,
        MissingProvider
    }

    private MonoBehaviour graphComponent;
    [SerializeField] private bool avoidSafeZones;
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.1f)] private float agentRadius = 0.4f;
    [SerializeField, Min(0.1f)] private float agentHeight = 1.8f;
    [SerializeField, Min(0.1f)] private float nodeReachDistance = 0.5f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Min(0f)] private float replanCooldown = 0.25f;

    private readonly List<Vector3> path = new List<Vector3>();
    private IAStarGraphProvider graph;
    private Vector3 requestedDestination;
    private int pathIndex;
    private bool pathValid;
    private bool warningShown;
    private bool hasRequestedDestination;
    private bool hasFailedDestination;
    private Vector3 failedDestination;
    private float nextReplanTime;

    public NavigationStatus Status { get; private set; } = NavigationStatus.Idle;
    public bool HasMovementTarget => pathValid && pathIndex < path.Count;
    public Vector3 MovementTarget => HasMovementTarget ? path[pathIndex] : transform.position;

    private void Awake() { ResolveGraph(); }

    public void Configure(bool excludeSafeZones, float radius, float height)
    {
        avoidSafeZones = excludeSafeZones;
        agentRadius = Mathf.Max(0.1f, radius);
        agentHeight = Mathf.Max(agentRadius * 2f, height);
    }

    public void ConfigureGraphComponent(MonoBehaviour component)
    {
        graphComponent = component;
        graph = component as IAStarGraphProvider;
        if (component != null && graph == null)
        {
            Debug.LogError("The assigned graph component must implement IAStarGraphProvider.", component);
            warningShown = true;
        }
    }

    public bool TryMoveTowards(Vector3 destination, float speed, float stoppingDistance, out bool moving)
    {
        moving = false;
        if (!IsFinite(destination))
        {
            InvalidatePath();
            Status = NavigationStatus.NoPath;
            return false;
        }

        if (avoidSafeZones && SafeZoneVolume.IsPositionSafe(destination))
        {
            InvalidatePath();
            Status = NavigationStatus.NoPath;
            return false;
        }

        ResolveGraph();
        if (graph == null)
        {
            InvalidatePath();
            Status = NavigationStatus.MissingProvider;
            if (!warningShown)
            {
                Debug.LogWarning("No scene component implements IAStarGraphProvider; navigation is paused until the existing graph is connected.", this);
                warningShown = true;
            }
            return false;
        }

        bool destinationChanged = !hasRequestedDestination ||
            (destination - requestedDestination).sqrMagnitude > 1f;
        if (destinationChanged)
        {
            requestedDestination = destination;
            hasRequestedDestination = true;
            pathValid = false;
            hasFailedDestination = false;
        }

        float stopping = Mathf.Max(0f, stoppingDistance);
        if (Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).magnitude <= stopping)
        {
            InvalidatePath();
            Status = NavigationStatus.Arrived;
            return true;
        }

        if (!pathValid)
        {
            if (hasFailedDestination && (destination - failedDestination).sqrMagnitude <= 1f &&
                Time.realtimeSinceStartup < nextReplanTime)
            {
                Status = NavigationStatus.NoPath;
                return false;
            }

            if (graph.HasDirectLineOfSight(transform.position, destination, agentRadius, agentHeight))
            {
                path.Clear();
                path.Add(destination);
                pathIndex = 0;
                pathValid = true;
                Status = NavigationStatus.FollowingDirect;
            }
            else
            {
                path.Clear();
                pathValid = graph.TryFindPath(transform.position, destination, avoidSafeZones, path);
                pathIndex = path.Count > 1 ? 1 : 0;
                if (!pathValid || path.Count == 0)
                {
                    hasFailedDestination = true;
                    failedDestination = destination;
                    nextReplanTime = Time.realtimeSinceStartup + replanCooldown;
                    Status = NavigationStatus.NoPath;
                    path.Clear();
                    return false;
                }
                Status = NavigationStatus.FollowingPath;
            }
        }

        while (pathIndex < path.Count - 1 &&
            Vector3.ProjectOnPlane(path[pathIndex] - transform.position, Vector3.up).magnitude <= nodeReachDistance)
            pathIndex++;

        if (pathIndex >= path.Count)
        {
            Status = NavigationStatus.Arrived;
            pathValid = false;
            return true;
        }

        Vector3 offset = Vector3.ProjectOnPlane(path[pathIndex] - transform.position, Vector3.up);
        if (pathIndex == path.Count - 1 && offset.magnitude <= stopping)
        {
            InvalidatePath();
            Status = NavigationStatus.Arrived;
            return true;
        }

        moving = offset.sqrMagnitude > 0.0001f;
        return true;
    }

    public void InvalidatePath()
    {
        pathValid = false;
        path.Clear();
        pathIndex = 0;
        hasRequestedDestination = false;
        Status = NavigationStatus.Idle;
    }

    private void ResolveGraph()
    {
        if (graph != null || graphComponent == null) return;
        graph = graphComponent as IAStarGraphProvider;
        if (graph == null && !warningShown)
        {
            Debug.LogError("The assigned graph component must implement IAStarGraphProvider.", this);
            warningShown = true;
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private void OnValidate()
    {
        agentRadius = Mathf.Max(0.1f, agentRadius);
        agentHeight = Mathf.Max(agentRadius * 2f, agentHeight);
        nodeReachDistance = Mathf.Max(0.1f, nodeReachDistance);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        replanCooldown = Mathf.Max(0f, replanCooldown);
    }

    private void OnDrawGizmosSelected()
    {
        if (!pathValid || path.Count == 0) return;
        Gizmos.color = avoidSafeZones ? Color.cyan : Color.yellow;
        Vector3 previous = transform.position;
        for (int i = pathIndex; i < path.Count; i++)
        {
            Gizmos.DrawLine(previous, path[i]);
            Gizmos.DrawSphere(path[i], 0.12f);
            previous = path[i];
        }
    }
}
