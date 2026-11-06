using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Scene-local, read-only diagnostics for the I3.2 autonomous patrol scenario.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class PredatorPatrolI32Diagnostics : MonoBehaviour
{
    private const float SampleInterval = 0.25f;
    private const float MovementThreshold = 0.005f;
    private const int MaximumTransitions = 4;

    private static readonly FieldInfo NodeReachDistanceField = FindField(
        typeof(GraphNavigationAgent), "nodeReachDistance");
    private static readonly FieldInfo AgentPathIndexField = FindField(typeof(GraphNavigationAgent), "pathIndex");
    private static readonly FieldInfo AgentPathField = FindField(typeof(GraphNavigationAgent), "path");
    private static readonly FieldInfo StoppingDistanceField = FindField(
        typeof(PredatorNavigationMovementController), "stoppingDistance");
    private static readonly FieldInfo CollisionRadiusField = FindField(typeof(PredatorMovement), "collisionRadius");
    private static readonly FieldInfo CollisionHeightField = FindField(typeof(PredatorMovement), "collisionHeight");
    private static readonly FieldInfo ObstacleLayersField = FindField(typeof(PredatorMovement), "obstacleLayers");
    private static readonly FieldInfo PatrolSpeedField = FindField(typeof(PredatorMovement), "patrolSpeed");
    private static readonly FieldInfo ChaseSpeedField = FindField(typeof(PredatorMovement), "chaseSpeed");
    private static readonly FieldInfo ChaseModeField = FindField(
        typeof(PredatorNavigationMovementController), "chase");

    private readonly List<string> transitions = new List<string>(MaximumTransitions);
    private readonly Collider[] overlapBuffer = new Collider[8];
    private readonly StringBuilder overlapDescription = new StringBuilder(128);

    private PredatorFSM predator;
    private PredatorNavigationMovementController navigation;
    private GraphNavigationAgent agent;
    private PredatorMovement movement;
    private Vector2 scrollPosition;

    private float nextSampleTime;
    private float lastSampleTime;
    private float lastMovementTime;
    private Vector3 lastSamplePosition;
    private Vector3 sampledPosition;
    private Vector3 sampledDestination;
    private Vector3 sampledMovementTarget;
    private Vector3 sampledVelocity;
    private float sampledDisplacement;
    private float secondsWithoutMovement;
    private float sampledNodeReachDistance;
    private float sampledStoppingDistance;
    private float effectiveStoppingDistance;
    private float sampledCastDistance;
    private float sampledDistanceToMovementTarget;
    private float sampledCollisionRadius;
    private float sampledCollisionHeight;
    private int sampledObstacleMaskValue;
    private int sampledWaypointIndex = -1;
    private int sampledWaypointCount;
    private int sampledPathIndex = -1;
    private int sampledPathCount;
    private int sampledOverlapCount;
    private int transitionCount;
    private bool sampledHasDestination;
    private bool sampledHasMovementTarget;
    private bool sampledHasPositionChanged;
    private bool sampledHasNodeReachDistance;
    private bool sampledHasAStarPathData;
    private bool sampledHasStoppingDistance;
    private bool sampledHasEffectiveStoppingDistance;
    private bool sampledHasMovementDimensions;
    private bool sampledHasObstacleLayers;
    private bool sampledHasMovementSpeed;
    private bool sampledHasControllerTick;
    private bool sampledLastTickCommandedMovement;
    private bool sampledLastTickAccepted;
    private bool sampledWasBlocked;
    private bool sampledIsFinalMovementTarget;
    private bool sampledHasInitialOverlap;
    private bool sampledCapsuleCastExecuted;
    private bool sampledCapsuleCastHit;
    private bool hasPositionSample;
    private bool hasPreviousDestinationSample;
    private bool previousHasDestination;
    private Vector3 previousDestination;
    private int previousWaypointIndex = -1;
    private GraphNavigationAgent.NavigationStatus sampledStatus;
    private string sampledWaypointName = "no observable";
    private string lastFailure = "none observed";
    private string lastNavigationEvent = "no event observed yet";
    private string initialOverlapColliders = "not sampled";
    private string capsuleHitCollider = "not sampled";
    private string capsuleCastNote = "not sampled";

    private void Awake()
    {
        predator = GetComponent<PredatorFSM>();
        navigation = GetComponent<PredatorNavigationMovementController>();
        agent = GetComponent<GraphNavigationAgent>();
        movement = GetComponent<PredatorMovement>();
    }

    private void Update()
    {
        if (predator == null || navigation == null) return;

        if (navigation.Status == GraphNavigationAgent.NavigationStatus.NoPath ||
            navigation.Status == GraphNavigationAgent.NavigationStatus.MissingProvider)
            lastFailure = navigation.Status.ToString();

        if (Time.unscaledTime < nextSampleTime) return;

        nextSampleTime = Time.unscaledTime + SampleInterval;
        CaptureSample();
    }

    private void CaptureSample()
    {
        lastSampleTime = Time.unscaledTime;
        sampledPosition = transform.position;
        sampledHasDestination = navigation.HasDestination;
        sampledDestination = navigation.RequestedDestination;
        sampledStatus = navigation.Status;
        sampledWaypointCount = predator.Waypoints.Count;
        sampledWaypointIndex = ResolveWaypointIndex(sampledDestination, sampledHasDestination);
        sampledWaypointName = sampledWaypointIndex >= 0
            ? predator.Waypoints[sampledWaypointIndex].name
            : sampledHasDestination ? "destination does not match a configured waypoint" : "no active destination";

        if (sampledStatus == GraphNavigationAgent.NavigationStatus.NoPath ||
            sampledStatus == GraphNavigationAgent.NavigationStatus.MissingProvider)
            lastFailure = sampledStatus.ToString();

        CaptureNavigationEvent();
        CapturePositionProgress();
        CaptureMovementAndPathData();
        CapturePhysicsProbe();
    }

    private void CaptureNavigationEvent()
    {
        if (!hasPreviousDestinationSample)
        {
            lastNavigationEvent = sampledHasDestination
                ? "initial sample: active destination already existed; request time unknown"
                : "no request/cancel change observed";
            hasPreviousDestinationSample = true;
        }
        else if (sampledHasDestination &&
            (!previousHasDestination || previousDestination != sampledDestination))
        {
            lastNavigationEvent = $"request observed at {lastSampleTime:0.0}s for {sampledWaypointName}";
            if (previousWaypointIndex >= 0 && sampledWaypointIndex >= 0 &&
                previousWaypointIndex != sampledWaypointIndex)
            {
                transitionCount++;
                transitions.Insert(0,
                    $"{lastSampleTime:0.0}s: {predator.Waypoints[previousWaypointIndex].name} → {sampledWaypointName}");
                if (transitions.Count > MaximumTransitions)
                    transitions.RemoveAt(transitions.Count - 1);
            }
        }
        else if (!sampledHasDestination && previousHasDestination)
        {
            lastNavigationEvent = sampledStatus == GraphNavigationAgent.NavigationStatus.Arrived
                ? $"destination completion observed at {lastSampleTime:0.0}s"
                : $"destination cleared at {lastSampleTime:0.0}s; exact cause is not exposed (status: {sampledStatus})";
        }

        previousHasDestination = sampledHasDestination;
        previousDestination = sampledDestination;
        previousWaypointIndex = sampledWaypointIndex;
    }

    private void CapturePositionProgress()
    {
        if (!hasPositionSample)
        {
            hasPositionSample = true;
            lastMovementTime = lastSampleTime;
            lastSamplePosition = sampledPosition;
            sampledDisplacement = 0f;
            sampledHasPositionChanged = false;
        }
        else
        {
            sampledDisplacement = Vector3.ProjectOnPlane(
                sampledPosition - lastSamplePosition, Vector3.up).magnitude;
            sampledHasPositionChanged = sampledDisplacement >= MovementThreshold;
            if (sampledHasPositionChanged) lastMovementTime = lastSampleTime;
            lastSamplePosition = sampledPosition;
        }

        secondsWithoutMovement = Mathf.Max(0f, lastSampleTime - lastMovementTime);
    }

    private void CaptureMovementAndPathData()
    {
        sampledHasMovementTarget = agent != null && agent.HasMovementTarget;
        sampledMovementTarget = navigation.MovementTarget;
        sampledHasAStarPathData = TryReadPathProgress(agent, out sampledPathIndex, out sampledPathCount);
        sampledDistanceToMovementTarget = sampledHasMovementTarget
            ? Vector3.ProjectOnPlane(sampledMovementTarget - sampledPosition, Vector3.up).magnitude
            : 0f;
        sampledIsFinalMovementTarget = sampledHasMovementTarget && agent.IsFinalMovementTarget;
        sampledHasNodeReachDistance = TryReadFloat(NodeReachDistanceField, agent, out sampledNodeReachDistance);
        sampledHasStoppingDistance = TryReadFloat(StoppingDistanceField, navigation, out sampledStoppingDistance);
        sampledHasEffectiveStoppingDistance = sampledHasMovementTarget &&
            (!sampledIsFinalMovementTarget || sampledHasStoppingDistance);
        effectiveStoppingDistance = sampledIsFinalMovementTarget && sampledHasStoppingDistance
            ? sampledStoppingDistance
            : 0f;

        sampledHasControllerTick = navigation.TickCount > 0;
        sampledLastTickCommandedMovement = navigation.LastTickCommandedMovement;
        sampledLastTickAccepted = navigation.LastTickAccepted;
        sampledWasBlocked = movement != null && movement.WasBlocked;
        sampledVelocity = movement != null ? movement.Velocity : Vector3.zero;
    }

    private void CapturePhysicsProbe()
    {
        sampledHasInitialOverlap = false;
        sampledOverlapCount = 0;
        initialOverlapColliders = "not available";
        sampledCapsuleCastExecuted = false;
        sampledCapsuleCastHit = false;
        sampledCastDistance = 0f;
        capsuleHitCollider = "none";
        capsuleCastNote = "not available";

        sampledHasMovementDimensions = TryReadFloat(CollisionRadiusField, movement, out sampledCollisionRadius) &&
            TryReadFloat(CollisionHeightField, movement, out sampledCollisionHeight);
        sampledHasObstacleLayers = TryReadLayerMask(ObstacleLayersField, movement, out LayerMask obstacleMask);
        sampledObstacleMaskValue = sampledHasObstacleLayers ? obstacleMask.value : 0;

        if (movement == null || !sampledHasMovementDimensions || !sampledHasObstacleLayers)
        {
            capsuleCastNote = "movement capsule settings are unavailable";
            return;
        }

        float radius = Mathf.Max(0.05f, sampledCollisionRadius);
        float height = Mathf.Max(radius * 2f, sampledCollisionHeight);
        Vector3 bottom = sampledPosition + Vector3.up * radius;
        Vector3 top = sampledPosition + Vector3.up * (height - radius);
        System.Array.Clear(overlapBuffer, 0, overlapBuffer.Length);
        sampledOverlapCount = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer,
            obstacleMask, QueryTriggerInteraction.Ignore);
        sampledHasInitialOverlap = sampledOverlapCount > 0;
        initialOverlapColliders = DescribeOverlapColliders();

        if (!sampledHasMovementTarget)
        {
            capsuleCastNote = "not run: no active GraphNavigationAgent movement target";
            return;
        }

        if (!sampledLastTickCommandedMovement)
        {
            capsuleCastNote = "not run: controller did not command movement on its last tick";
            return;
        }

        if (!TryReadBool(ChaseModeField, navigation, out bool chase) ||
            !TryReadFloat(chase ? ChaseSpeedField : PatrolSpeedField, movement, out float speed) ||
            (sampledIsFinalMovementTarget && !sampledHasStoppingDistance))
        {
            capsuleCastNote = "not run: movement mode/speed or required stopping distance unavailable";
            return;
        }

        Vector3 offset = Vector3.ProjectOnPlane(sampledMovementTarget - sampledPosition, Vector3.up);
        float targetDistance = offset.magnitude;
        sampledCastDistance = Mathf.Min(speed * Time.deltaTime,
            Mathf.Max(0f, targetDistance - effectiveStoppingDistance));
        if (sampledCastDistance <= 0f || targetDistance <= 0f)
        {
            capsuleCastNote = "not run: predicted physical step is zero";
            return;
        }

        sampledCapsuleCastExecuted = true;
        if (Physics.CapsuleCast(bottom, top, radius, offset / targetDistance,
            out RaycastHit hit, sampledCastDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            sampledCapsuleCastHit = true;
            capsuleHitCollider = hit.collider != null
                ? $"{hit.collider.name} at {hit.distance:0.000}m"
                : "hit collider unavailable";
        }

        capsuleCastNote = sampledCapsuleCastHit
            ? "hit on a diagnostic cast for the predicted movement step"
            : "no hit on a diagnostic cast for the predicted movement step";
    }

    private string DescribeOverlapColliders()
    {
        if (sampledOverlapCount == 0) return "none";

        int displayedCount = Mathf.Min(sampledOverlapCount, overlapBuffer.Length);
        overlapDescription.Length = 0;
        for (int i = 0; i < displayedCount; i++)
        {
            if (overlapBuffer[i] == null) continue;
            if (overlapDescription.Length > 0) overlapDescription.Append(", ");
            overlapDescription.Append(overlapBuffer[i].name);
        }

        string suffix = sampledOverlapCount >= overlapBuffer.Length ? ", buffer may be full" : string.Empty;
        if (overlapDescription.Length == 0) return "collider name unavailable" + suffix;
        overlapDescription.Append(suffix);
        return overlapDescription.ToString();
    }

    private int ResolveWaypointIndex(Vector3 destination, bool hasDestination)
    {
        if (!hasDestination) return -1;

        for (int i = 0; i < predator.Waypoints.Count; i++)
        {
            Transform waypoint = predator.Waypoints[i];
            if (waypoint != null && (waypoint.position - destination).sqrMagnitude <= 0.0001f)
                return i;
        }

        return -1;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || predator == null || navigation == null) return;

        float panelHeight = Mathf.Min(560f, Mathf.Max(300f, Screen.height - 32f));
        GUILayout.BeginArea(new Rect(16f, 16f, 420f, panelHeight), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("PATRULLA I3.2 — MUESTRA DE DIAGNÓSTICO");
        GUILayout.Label($"FSM: {predator.CurrentState} (API)");
        string routeMode = sampledStatus == GraphNavigationAgent.NavigationStatus.FollowingPath
            ? "A*"
            : sampledStatus == GraphNavigationAgent.NavigationStatus.FollowingDirect
                ? "direct"
                : sampledStatus.ToString();
        GUILayout.Label($"NavigationStatus: {sampledStatus}; route: {routeMode} (API)");
        GUILayout.Label($"Waypoint activo: {sampledWaypointName}");
        GUILayout.Label(sampledWaypointIndex >= 0
            ? $"Waypoint index/count: {sampledWaypointIndex}/{sampledWaypointCount} (derived from destination; 0-based)"
            : $"Waypoint index/count: not available/{sampledWaypointCount}");
        GUILayout.Label(sampledHasAStarPathData
            ? $"A* path index/count: {sampledPathIndex}/{sampledPathCount} (private path data)"
            : "A* path index/count: not available");
        GUILayout.Label(sampledHasDestination
            ? $"Destino solicitado: {sampledDestination:F2} (HasDestination=true)"
            : $"HasDestination=false; destino retenido: {sampledDestination:F2}");
        GUILayout.Label(sampledHasMovementTarget
            ? $"MovementTarget: {sampledMovementTarget:F2} (API)"
            : $"MovementTarget: no activo; la propiedad devuelve posición actual");
        GUILayout.Label(sampledHasMovementTarget
            ? $"Distancia horizontal al MovementTarget: {sampledDistanceToMovementTarget:0.000}m (calculada)"
            : "Distancia al MovementTarget: n/a, no hay target activo");
        GUILayout.Label(sampledHasNodeReachDistance
            ? $"NodeReachDistance: {sampledNodeReachDistance:0.000}m (campo serializado, lectura privada)"
            : "NodeReachDistance: not available");
        GUILayout.Label(!sampledHasMovementTarget
            ? "StoppingDistance efectiva: n/a, no hay segmento activo"
            : sampledHasEffectiveStoppingDistance
                ? $"Segmento final: {sampledIsFinalMovementTarget}; StoppingDistance efectiva: {effectiveStoppingDistance:0.000}m (calculada)"
                : "StoppingDistance efectiva: not available");
        GUILayout.Label($"Posición: {sampledPosition:F2} (API Transform)");
        GUILayout.Label($"Posición cambió desde muestra: {sampledHasPositionChanged}; desplazamiento: {sampledDisplacement:0.000}m");
        GUILayout.Label($"Tiempo aprox. sin desplazamiento: {secondsWithoutMovement:0.0}s (muestras de {SampleInterval:0.00}s)");
        GUILayout.Label($"Muestra: {lastSampleTime:0.00}s; transiciones observadas: {transitionCount}");
        GUILayout.Label($"Último evento nav observado: {lastNavigationEvent}");
        GUILayout.Label($"Último fallo observado: {lastFailure}");

        GUILayout.Space(6f);
        GUILayout.Label("MOVIMIENTO");
        GUILayout.Label(movement != null
            ? $"WasBlocked: {sampledWasBlocked} (último MoveTowards; puede quedar obsoleto si no hubo comando)"
            : "WasBlocked: not available");
        GUILayout.Label(sampledHasControllerTick
            ? $"Último tick: accepted={sampledLastTickAccepted}; movement commanded={sampledLastTickCommandedMovement} (API)"
            : "Último tick del controlador: not available");
        GUILayout.Label($"Velocity: {sampledVelocity:F2} (API; cero no distingue Stop de bloqueo)");

        GUILayout.Space(6f);
        GUILayout.Label("COLISIONES — SONDAS READ-ONLY A 4 Hz");
        GUILayout.Label(sampledHasMovementDimensions
            ? $"Cápsula física: radius={sampledCollisionRadius:0.000}, height={sampledCollisionHeight:0.000}m (campos serializados)"
            : "Cápsula física: settings not available");
        GUILayout.Label(sampledHasObstacleLayers
            ? $"Obstacle layer mask: {ReadObstacleMaskDescription()}"
            : "Obstacle layer mask: not available");
        GUILayout.Label(sampledHasMovementDimensions && sampledHasObstacleLayers
            ? $"Solapamiento inicial: {sampledHasInitialOverlap}; colliders: {initialOverlapColliders}"
            : "Solapamiento inicial: not available");
        GUILayout.Label($"CapsuleCast: {(sampledCapsuleCastExecuted ? (sampledCapsuleCastHit ? "HIT" : "no hit") : "not run")}; {capsuleCastNote}");
        GUILayout.Label($"Collider del cast: {capsuleHitCollider}; distancia consultada: {sampledCastDistance:0.000}m");
        GUILayout.Label("Nota: dimensiones y velocidades privadas se leen por reflexión, sin modificarlas.");

        GUILayout.Space(6f);
        GUILayout.Label("TRANSICIONES OBSERVADAS ENTRE WAYPOINTS");
        if (transitions.Count == 0)
            GUILayout.Label("(ninguna transición observada todavía)");
        else
            for (int i = 0; i < transitions.Count; i++) GUILayout.Label(transitions[i]);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private string ReadObstacleMaskDescription()
    {
        return sampledHasObstacleLayers ? $"0x{sampledObstacleMaskValue:X8}" : "not available";
    }

    private static FieldInfo FindField(System.Type type, string fieldName)
    {
        return type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private static bool TryReadFloat(FieldInfo field, object instance, out float value)
    {
        object raw = field != null && instance != null ? field.GetValue(instance) : null;
        if (raw is float readValue && !float.IsNaN(readValue) && !float.IsInfinity(readValue))
        {
            value = readValue;
            return true;
        }

        value = 0f;
        return false;
    }

    private static bool TryReadBool(FieldInfo field, object instance, out bool value)
    {
        object raw = field != null && instance != null ? field.GetValue(instance) : null;
        if (raw is bool readValue)
        {
            value = readValue;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryReadLayerMask(FieldInfo field, object instance, out LayerMask value)
    {
        object raw = field != null && instance != null ? field.GetValue(instance) : null;
        if (raw is LayerMask readValue)
        {
            value = readValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryReadPathProgress(GraphNavigationAgent graphAgent, out int pathIndex, out int pathCount)
    {
        object rawIndex = AgentPathIndexField != null && graphAgent != null
            ? AgentPathIndexField.GetValue(graphAgent)
            : null;
        object rawPath = AgentPathField != null && graphAgent != null
            ? AgentPathField.GetValue(graphAgent)
            : null;
        if (rawIndex is int index && rawPath is ICollection path)
        {
            pathIndex = index;
            pathCount = path.Count;
            return true;
        }

        pathIndex = -1;
        pathCount = 0;
        return false;
    }
}
