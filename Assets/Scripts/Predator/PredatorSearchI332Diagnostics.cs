using System.Text;
using UnityEngine;

/// <summary>
/// Public-API-only HUD for the isolated I3.3.2 Search validation scene.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class PredatorSearchI332Diagnostics : MonoBehaviour
{
    private const float DestinationTolerance = 0.01f;

    [SerializeField] private PredatorFSM predator;
    [SerializeField] private PredatorNavigationMovementController navigationController;
    [SerializeField] private PredatorMovement movement;
    [SerializeField] private PredatorSearchI332Harness harness;

    private Vector2 scrollPosition;
    private PredatorStateId previousState;
    private bool hasPreviousState;
    private bool hasObservedSearchDestination;
    private Vector3 previousSearchDestination;
    private int observedSearchDestinationOrdinal;
    private string lastTransition = "not observed yet";

    public void Configure(PredatorFSM configuredPredator,
        PredatorNavigationMovementController configuredNavigationController,
        PredatorMovement configuredMovement,
        PredatorSearchI332Harness configuredHarness)
    {
        predator = configuredPredator;
        navigationController = configuredNavigationController;
        movement = configuredMovement;
        harness = configuredHarness;
    }

    private void Update()
    {
        if (predator == null || navigationController == null) return;

        PredatorStateId currentState = predator.CurrentState;
        if (!hasPreviousState)
        {
            previousState = currentState;
            hasPreviousState = true;
        }
        else if (previousState != currentState)
        {
            lastTransition = $"{previousState} -> {currentState} at {Time.unscaledTime:0.0}s";
            if (currentState == PredatorStateId.Search)
            {
                observedSearchDestinationOrdinal = 0;
                hasObservedSearchDestination = false;
            }
            previousState = currentState;
        }

        if (currentState != PredatorStateId.Search &&
            harness.ActiveScenario == PredatorSearchI332Harness.Scenario.None)
        {
            observedSearchDestinationOrdinal = 0;
            hasObservedSearchDestination = false;
        }

        if (currentState != PredatorStateId.Search || !navigationController.HasDestination) return;

        Vector3 destination = navigationController.RequestedDestination;
        if (!hasObservedSearchDestination ||
            Vector3.ProjectOnPlane(destination - previousSearchDestination, Vector3.up).sqrMagnitude >
            DestinationTolerance * DestinationTolerance)
        {
            observedSearchDestinationOrdinal++;
            previousSearchDestination = destination;
            hasObservedSearchDestination = true;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || predator == null || navigationController == null || harness == null)
            return;

        float panelHeight = Mathf.Min(520f, Mathf.Max(300f, Screen.height - 32f));
        GUILayout.BeginArea(new Rect(16f, 16f, 420f, panelHeight), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("PREDATOR SEARCH I3.3.2 — PUBLIC API DIAGNOSTICS");
        GUILayout.Label($"FSM: {predator.CurrentState} (API)");
        GUILayout.Label($"Last transition: {lastTransition} (observed)");
        GUILayout.Label($"Scenario: {harness.ActiveScenario}; phase: {harness.Phase} (harness)");

        GraphNavigationAgent.NavigationStatus status = navigationController.Status;
        string route = status == GraphNavigationAgent.NavigationStatus.FollowingPath
            ? "A*"
            : status == GraphNavigationAgent.NavigationStatus.FollowingDirect
                ? "Direct"
                : status.ToString();
        GUILayout.Label($"NavigationStatus: {status}; route: {route} (API)");
        GUILayout.Label($"HasDestination: {navigationController.HasDestination} (API)");
        GUILayout.Label($"Requested destination: {navigationController.RequestedDestination:F2} (API)");
        GUILayout.Label($"MovementTarget: {navigationController.MovementTarget:F2} (API)");
        GUILayout.Label($"Velocity: {(movement != null ? movement.Velocity : Vector3.zero):F2} (API)");
        GUILayout.Label($"Horizontal speed: {(movement != null ? Vector3.ProjectOnPlane(movement.Velocity, Vector3.up).magnitude : 0f):0.00} m/s (calculated)");

        GUILayout.Space(6f);
        GUILayout.Label("SEARCH PROGRESS");
        GUILayout.Label(observedSearchDestinationOrdinal <= 0
            ? "Search destination ordinal: not observed"
            : observedSearchDestinationOrdinal == 1
                ? "Search destination ordinal: 1 (LastKnownPosition, derived)"
                : $"Search destination ordinal: {observedSearchDestinationOrdinal} (radial #{observedSearchDestinationOrdinal - 1}, derived)");
        GUILayout.Label("Exact private Search index: unavailable by design (no reflection/API added).");
        GUILayout.Label($"LastKnownPosition: {predator.LastKnownPosition:F2} (API)");
        GUILayout.Label($"CanSeeTarget: {predator.Perception.CanSeeTarget} (API)");

        GUILayout.Space(6f);
        GUILayout.Label("OBSERVED ROUTE / TERMINAL HISTORY");
        GUILayout.Label($"FollowingPath observed: {harness.SawFollowingPath}");
        GUILayout.Label($"FollowingDirect observed: {harness.SawFollowingDirect}");
        GUILayout.Label($"Arrived observed: {harness.SawArrived}");
        GUILayout.Label($"NoPath observed: {harness.SawNoPath}");
        GUILayout.Label($"MissingProvider observed: {harness.SawMissingProvider}");
        GUILayout.Label($"Search -> Pursuit observed: {harness.SawPursuitAfterSearch}");
        GUILayout.Label($"Search -> Patrol observed: {harness.SawPatrolAfterSearch}");
        GUILayout.Label($"Terminal sequence: {BuildTerminalHistory()}");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private string BuildTerminalHistory()
    {
        if (harness.TerminalHistory.Count == 0) return "(none observed)";

        var builder = new StringBuilder();
        for (int i = 0; i < harness.TerminalHistory.Count; i++)
        {
            if (i > 0) builder.Append(" -> ");
            builder.Append(harness.TerminalHistory[i]);
        }
        return builder.ToString();
    }
}
