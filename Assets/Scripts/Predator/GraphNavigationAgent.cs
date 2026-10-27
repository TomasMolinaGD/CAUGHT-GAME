using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GraphNavigationAgent : MonoBehaviour
{
    [SerializeField] private MonoBehaviour graphComponent;
    [SerializeField] private bool avoidSafeZones;
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.1f)] private float agentRadius = 0.4f;
    [SerializeField, Min(0.1f)] private float agentHeight = 1.8f;
    [SerializeField, Min(0.1f)] private float nodeReachDistance = 0.5f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;

    private readonly List<Vector3> path = new List<Vector3>();
    private IAStarGraphProvider graph;
    private Vector3 requestedDestination;
    private int pathIndex;
    private bool pathValid;
    private bool warningShown;

    private void Awake() { ResolveGraph(); }

    public void Configure(bool excludeSafeZones, float radius, float height)
    {
        avoidSafeZones = excludeSafeZones;
        agentRadius = Mathf.Max(0.1f, radius);
        agentHeight = Mathf.Max(agentRadius * 2f, height);
    }

    public bool TryMoveTowards(Vector3 destination, float speed, float stoppingDistance, out bool moving)
    {
        moving = false;
        if (avoidSafeZones && SafeZoneVolume.IsPositionSafe(destination)) return true;
        ResolveGraph();
        if (graph == null)
        {
            if (!HasDirectLineOfSight(destination))
            {
                if (!warningShown)
                {
                    Debug.LogWarning("No scene component implements IAStarGraphProvider; blocked movement is paused until the existing graph is connected.", this);
                    warningShown = true;
                }
                return true;
            }
            InvalidatePath();
            return false;
        }

        if (graph.HasDirectLineOfSight(transform.position, destination, agentRadius, agentHeight))
        {
            InvalidatePath();
            return false;
        }

        if (!pathValid || (destination - requestedDestination).sqrMagnitude > 1f)
        {
            requestedDestination = destination;
            pathValid = graph.TryFindPath(transform.position, destination, avoidSafeZones, path);
            pathIndex = path.Count > 1 ? 1 : 0;
            if (!pathValid) return true;
        }

        while (pathIndex < path.Count &&
            Vector3.ProjectOnPlane(path[pathIndex] - transform.position, Vector3.up).magnitude <= nodeReachDistance)
            pathIndex++;

        if (pathIndex >= path.Count) return true;
        Vector3 offset = Vector3.ProjectOnPlane(path[pathIndex] - transform.position, Vector3.up);
        if (offset.magnitude <= stoppingDistance) return true;
        Vector3 direction = offset.normalized;
        float travel = Mathf.Min(Mathf.Max(0f, speed) * Time.deltaTime, offset.magnitude - stoppingDistance);
        transform.position += direction * travel;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up), turnSpeed * Time.deltaTime);
        moving = travel > 0.0001f;
        return true;
    }

    public void InvalidatePath()
    {
        pathValid = false;
        path.Clear();
        pathIndex = 0;
    }

    private void ResolveGraph()
    {
        if (graphComponent != null)
        {
            graph = graphComponent as IAStarGraphProvider;
            return;
        }

        MonoBehaviour[] components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour component in components)
        {
            if (component is IAStarGraphProvider provider)
            {
                graphComponent = component;
                graph = provider;
                return;
            }
        }
    }

    private bool HasDirectLineOfSight(Vector3 destination)
    {
        Vector3 start = transform.position + Vector3.up * (agentHeight * 0.5f);
        Vector3 end = destination + Vector3.up * (agentHeight * 0.5f);
        return !Physics.Linecast(start, end, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private void OnValidate()
    {
        agentRadius = Mathf.Max(0.1f, agentRadius);
        agentHeight = Mathf.Max(agentRadius * 2f, agentHeight);
        nodeReachDistance = Mathf.Max(0.1f, nodeReachDistance);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
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
