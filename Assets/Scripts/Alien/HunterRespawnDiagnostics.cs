using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FSMAgent), typeof(HunterMovement))]
public sealed class HunterRespawnDiagnostics : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float sampleInterval = 0.25f;
    [SerializeField, Min(0.001f)] private float movementThreshold = 0.01f;
    [SerializeField, Min(360f)] private float panelWidth = 470f;

    private FSMAgent agent;
    private HunterMovement movement;
    private PoliceState previousState;
    private string latestTransition;
    private string exitCompletionObservation = "No observado: todavía no ocurrió Exit -> Search.";
    private string searchRequestObservation = "No observado: todavía no se muestreó Search.";
    private Vector3 sampledPosition;
    private Vector3 activeDestination;
    private ulong activeRequestId;
    private HunterNavigationControllerStatus controllerStatus;
    private int pathCalculationCount;
    private int currentWaypointIndex;
    private ulong routeVersion;
    private bool hasActiveRequest;
    private bool positionChanged;
    private float horizontalDisplacement;
    private float lastMovementTime;
    private float nextSampleTime;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle labelStyle;
    private GUIStyle warningStyle;
    private Vector2 scrollPosition;

    private void Awake()
    {
        agent = GetComponent<FSMAgent>();
        movement = GetComponent<HunterMovement>();
        previousState = agent.CurrentState;
        latestTransition = $"Inicio -> {previousState}";
        sampledPosition = transform.position;
        lastMovementTime = Time.unscaledTime;
        Sample();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSampleTime)
        {
            return;
        }

        Sample();
    }

    private void Sample()
    {
        nextSampleTime = Time.unscaledTime + sampleInterval;

        PoliceState currentState = agent.CurrentState;
        if (currentState != previousState)
        {
            latestTransition = $"{previousState} -> {currentState}";
            if (previousState == PoliceState.Exit && currentState == PoliceState.Search)
            {
                exitCompletionObservation =
                    "Inferido: se observó Exit -> Search. El ID completado de Exit no está expuesto.";
            }

            previousState = currentState;
        }

        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - sampledPosition;
        horizontalDisplacement = new Vector2(delta.x, delta.z).magnitude;
        positionChanged = horizontalDisplacement >= movementThreshold;
        if (positionChanged)
        {
            lastMovementTime = Time.unscaledTime;
        }

        sampledPosition = currentPosition;

        HunterNavigationIntent intent = agent.NavigationIntent;
        HunterNavigationRequest request = default;
        hasActiveRequest = intent != null && intent.TryGetCurrent(out request);
        activeRequestId = hasActiveRequest ? request.Id : 0;
        activeDestination = hasActiveRequest ? request.Destination : default;

        HunterNavigationController controller = agent.NavigationController;
        controllerStatus = controller != null
            ? controller.Status
            : HunterNavigationControllerStatus.Disabled;
        pathCalculationCount = controller != null ? controller.PathCalculationCount : 0;
        currentWaypointIndex = controller != null ? controller.CurrentWaypointIndex : -1;
        routeVersion = controller != null ? controller.CurrentRouteVersion : 0;

        if (currentState == PoliceState.Search)
        {
            searchRequestObservation = hasActiveRequest
                ? $"Observado: solicitud #{activeRequestId}; controlador={controllerStatus}."
                : "Observado: Search no tiene una solicitud de navegación activa.";
        }
    }

    private void OnGUI()
    {
        if (agent == null || movement == null)
        {
            return;
        }

        EnsureStyles();
        float actualWidth = Mathf.Min(panelWidth, Mathf.Max(280f, Screen.width - 32f));
        float panelHeight = Mathf.Min(620f, Mathf.Max(280f, Screen.height - 32f));
        float panelX = Mathf.Max(16f, Screen.width - actualWidth - 16f);
        Rect panelRect = new Rect(panelX, 16f, actualWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        GUILayout.BeginArea(new Rect(
            panelRect.x + 14f,
            panelRect.y + 10f,
            panelRect.width - 28f,
            panelRect.height - 20f));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);

        GUILayout.Label("DIAGNÓSTICO TEMPORAL — RESPAWN HUNTER", titleStyle);
        DrawSection("FSM / RESPAWN");
        DrawValue("Estado FSM", agent.CurrentState.ToString());
        DrawValue("Última transición", latestTransition);
        DrawValue("Solicitud Exit completada", exitCompletionObservation);

        DrawSection("NAVEGACIÓN");
        DrawValue("Solicitud activa", hasActiveRequest ? $"Sí — #{activeRequestId}" : "No");
        DrawValue("Destino solicitado", hasActiveRequest ? FormatVector(activeDestination) : "No disponible");
        DrawValue("Estado controlador", agent.NavigationController != null
            ? controllerStatus.ToString()
            : "Controlador no disponible");
        DrawValue("Ruta", $"versión {routeVersion}; waypoint {currentWaypointIndex}; cálculos {pathCalculationCount}");
        DrawValue("Resultado solicitud Search", searchRequestObservation);
        DrawValue("SearchRegion", agent.HuntingGroundRegion != null ? "Disponible" : "Faltante");
        DrawValue("SourceGrid", agent.HuntingGroundRegion != null &&
            agent.HuntingGroundRegion.SourceGrid != null ? "Disponible" : "Faltante");
        DrawValue("NavigationGrid", agent.NavigationGrid != null ? "Disponible" : "Faltante");
        DrawValue("Fallo observable", GetNavigationFailureLabel(), IsNavigationFailure());

        DrawSection("ACTIVACIÓN / MOVIMIENTO");
        DrawValue("GameObject", gameObject.activeInHierarchy ? "Activo" : "Inactivo");
        DrawValue("FSMAgent", agent.enabled ? "Enabled" : "Disabled");
        DrawValue("HunterMovement", movement.enabled ? "Enabled" : "Disabled");
        DrawValue("Controller", agent.NavigationController != null
            ? (agent.NavigationController.Enabled ? "Enabled" : "Disabled")
            : "No disponible");
        DrawValue("Posición", FormatVector(sampledPosition));
        DrawValue("Velocidad horizontal", HorizontalSpeed().ToString("0.000") + " m/s");
        DrawValue("Cambió desde muestra", positionChanged
            ? $"Sí — {horizontalDisplacement:0.000} m"
            : $"No — {horizontalDisplacement:0.000} m");
        DrawValue("Tiempo sin movimiento", $"{Mathf.Max(0f, Time.unscaledTime - lastMovementTime):0.0} s");

        GUILayout.FlexibleSpace();
        GUILayout.Label(
            "Directo: APIs públicas. Inferido: transición y salto de posición. " +
            "No disponible: ownership interno de Search e ID completado de Exit.",
            labelStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private string GetNavigationFailureLabel()
    {
        if (agent.NavigationController == null)
        {
            return "Controlador faltante";
        }

        if (agent.NavigationGrid == null)
        {
            return "NavigationGrid faltante";
        }

        return IsNavigationFailure()
            ? controllerStatus.ToString()
            : "Ninguno observado";
    }

    private bool IsNavigationFailure()
    {
        return controllerStatus == HunterNavigationControllerStatus.NoPath ||
               controllerStatus == HunterNavigationControllerStatus.InvalidGrid ||
               controllerStatus == HunterNavigationControllerStatus.InvalidStart ||
               controllerStatus == HunterNavigationControllerStatus.InvalidDestination ||
               controllerStatus == HunterNavigationControllerStatus.StartNotTraversable ||
               controllerStatus == HunterNavigationControllerStatus.DestinationNotTraversable;
    }

    private float HorizontalSpeed()
    {
        Vector3 velocity = movement.Velocity;
        return new Vector2(velocity.x, velocity.z).magnitude;
    }

    private void DrawSection(string text)
    {
        GUILayout.Space(5f);
        GUILayout.Label(text, sectionStyle);
    }

    private void DrawValue(string label, string value, bool warning = false)
    {
        GUILayout.Label($"{label}: {value}", warning ? warningStyle : labelStyle);
    }

    private static string FormatVector(Vector3 value) =>
        $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";

    private void EnsureStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10)
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.4f, 0.85f, 1f) }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        warningStyle = new GUIStyle(labelStyle)
        {
            normal = { textColor = new Color(1f, 0.45f, 0.3f) }
        };
    }
}
