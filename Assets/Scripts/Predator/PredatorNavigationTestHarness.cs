using System.Collections;
using System.Reflection;
using CaughtGame.Navigation;
using UnityEngine;

/// <summary>
/// Optional scene-only driver for exercising one Predator navigation route.
/// It delegates destination requests and never writes the Predator transform.
/// </summary>
[DisallowMultipleComponent]
public sealed class PredatorNavigationTestHarness : MonoBehaviour
{
    [SerializeField] private PredatorNavigationMovementController controller;
    [SerializeField] private Transform destination;
    [SerializeField] private Transform obstacleDestination;
    [SerializeField] private Transform alternateDestination;
    [SerializeField] private Transform safeZoneDestination;
    [SerializeField] private Transform outOfGridDestination;
    [SerializeField] private bool requestOnStart;
    [SerializeField] private bool showRuntimeDiagnostics = true;
    [SerializeField] private Rect diagnosticsRect = new Rect(12f, 12f, 390f, 220f);
    [SerializeField] private float physicsProbeRadius = 0.35f;
    [SerializeField] private float physicsProbeHeight = 1.6f;
    [SerializeField] private LayerMask physicsProbeLayers = 1 << 12;

    public PredatorNavigationMovementController Controller => controller;

    public Vector3 ConfiguredDestination => destination != null ? destination.position : transform.position;
    public Vector3 AlternateDestination => alternateDestination != null
        ? alternateDestination.position
        : ConfiguredDestination;

    public Vector3 ObstacleDestination => obstacleDestination != null
        ? obstacleDestination.position
        : ConfiguredDestination;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PredatorNavigationMovementController>();
    }

    private void Start()
    {
        if (requestOnStart) RequestConfiguredDestination();
    }

    public void RequestConfiguredDestination()
    {
        if (controller == null || destination == null) return;
        controller.RequestDestination(destination.position);
    }

    public void RequestAlternateDestination()
    {
        if (controller == null || alternateDestination == null) return;
        controller.RequestDestination(alternateDestination.position);
    }

    public void RequestObstacleDestination()
    {
        if (controller == null || obstacleDestination == null) return;
        controller.RequestDestination(obstacleDestination.position);
    }

    public void RequestSafeZoneDestination()
    {
        if (controller == null || safeZoneDestination == null) return;
        controller.RequestDestination(safeZoneDestination.position);
    }

    public void RequestOutOfGridDestination()
    {
        if (controller == null || outOfGridDestination == null) return;
        controller.RequestDestination(outOfGridDestination.position);
    }

    public void CancelNavigation()
    {
        controller?.CancelDestination();
    }

    private void OnDrawGizmos()
    {
        if (destination != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(destination.position, 0.35f);
            Gizmos.DrawLine(transform.position, destination.position);
        }

        if (alternateDestination != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(alternateDestination.position, 0.35f);
        }

        DrawMarker(obstacleDestination, Color.yellow);
        DrawMarker(safeZoneDestination, Color.cyan);
        DrawMarker(outOfGridDestination, Color.red);
    }

    private void OnGUI()
    {
        if (!showRuntimeDiagnostics || !Application.isPlaying || controller == null) return;

        GraphNavigationAgent agent = controller.GetComponent<GraphNavigationAgent>();
        PredatorMovement movement = controller.GetComponent<PredatorMovement>();
        string status = controller.Status.ToString();
        string movementTarget = agent != null ? agent.MovementTarget.ToString("F2") : "<none>";
        string position = controller.transform.position.ToString("F2");
        string destinationValue = controller.RequestedDestination.ToString("F2");
        bool reached = controller.Status == GraphNavigationAgent.NavigationStatus.Arrived;
        GetWaypointDiagnostics(agent, out int waypointIndex, out int waypointCount);
        ProbeCapsule(agent, out bool capsuleBlocked, out bool initiallyOverlapping, out string colliderName);
        bool providerLineOfSight = ProbeProviderLineOfSight(agent);

        GUILayout.BeginArea(diagnosticsRect, GUI.skin.window);
        GUILayout.Label("Predator Navigation I2.3");
        GUILayout.Label($"Status: {status}");
        GUILayout.Label($"Destination active: {controller.HasDestination}");
        GUILayout.Label($"Position: {position}");
        GUILayout.Label($"Requested destination: {destinationValue}");
        GUILayout.Label($"Movement target: {movementTarget}");
        GUILayout.Label($"WasBlocked: {(movement != null && movement.WasBlocked)}");
        GUILayout.Label($"TickNavigation count: {controller.TickCount}");
        GUILayout.Label($"Last tick accepted: {controller.LastTickAccepted}");
        GUILayout.Label($"Movement command sent: {controller.LastTickCommandedMovement}");
        GUILayout.Label($"Destination reached: {reached}");
        GUILayout.Label($"Waypoint index/count: {waypointIndex}/{waypointCount}");
        GUILayout.Label($"CapsuleCast blocked: {capsuleBlocked}");
        GUILayout.Label($"Capsule initially overlapping: {initiallyOverlapping}");
        GUILayout.Label($"Blocking collider: {colliderName}");
        GUILayout.Label($"Provider LOS for current segment: {providerLineOfSight}");
        if (GUILayout.Button("Request obstacle target")) RequestObstacleDestination();
        if (GUILayout.Button("Request alternate target")) RequestAlternateDestination();
        if (GUILayout.Button("Request Safe Zone target")) RequestSafeZoneDestination();
        if (GUILayout.Button("Request out-of-grid target")) RequestOutOfGridDestination();
        if (GUILayout.Button("Cancel navigation")) CancelNavigation();
        GUILayout.EndArea();
    }

    private static void GetWaypointDiagnostics(GraphNavigationAgent agent, out int index, out int count)
    {
        index = -1;
        count = 0;
        if (agent == null) return;

        FieldInfo indexField = typeof(GraphNavigationAgent).GetField("pathIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo pathField = typeof(GraphNavigationAgent).GetField("path",
            BindingFlags.Instance | BindingFlags.NonPublic);
        index = indexField != null ? (int)indexField.GetValue(agent) : -1;
        object path = pathField?.GetValue(agent);
        count = path is ICollection collection ? collection.Count : 0;
    }

    private void ProbeCapsule(GraphNavigationAgent agent, out bool blocked,
        out bool initiallyOverlapping, out string colliderName)
    {
        blocked = false;
        initiallyOverlapping = false;
        colliderName = "<none>";
        if (agent == null || !agent.HasMovementTarget) return;

        Vector3 position = transform.position;
        Vector3 target = agent.MovementTarget;
        Vector3 direction = Vector3.ProjectOnPlane(target - position, Vector3.up);
        float distance = direction.magnitude;
        if (distance <= 0f) return;

        float radius = Mathf.Max(0.05f, physicsProbeRadius);
        float height = Mathf.Max(radius * 2f, physicsProbeHeight);
        Vector3 bottom = position + Vector3.up * radius;
        Vector3 top = position + Vector3.up * (height - radius);
        initiallyOverlapping = Physics.CheckCapsule(bottom, top, radius, physicsProbeLayers,
            QueryTriggerInteraction.Ignore);
        if (Physics.CapsuleCast(bottom, top, radius, direction / distance,
            out RaycastHit hit, distance, physicsProbeLayers, QueryTriggerInteraction.Ignore))
        {
            blocked = true;
            colliderName = hit.collider != null ? hit.collider.name : "<unknown>";
        }
    }

    private bool ProbeProviderLineOfSight(GraphNavigationAgent agent)
    {
        if (agent == null || !agent.HasMovementTarget) return false;
        FieldInfo providerField = typeof(PredatorNavigationMovementController).GetField("graphProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MonoBehaviour provider = providerField?.GetValue(controller) as MonoBehaviour;
        IAStarGraphProvider graph = provider as IAStarGraphProvider;
        return graph != null && graph.HasDirectLineOfSight(
            transform.position, agent.MovementTarget, physicsProbeRadius, physicsProbeHeight);
    }

    private static void DrawMarker(Transform marker, Color color)
    {
        if (marker == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(marker.position, 0.35f);
    }
}
