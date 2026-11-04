using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEngine;

/// <summary>
/// Scene-local driver for the I3.3.2 Search validation scenarios.
/// It only configures the target and provider; the production FSM owns all transitions and movement.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class PredatorSearchI332Harness : MonoBehaviour
{
    public enum Scenario
    {
        None,
        NormalAStar,
        NoPath,
        MissingProvider
    }

    public enum ScenarioPhase
    {
        Ready,
        WaitingForPursuit,
        WaitingForSearch,
        Searching,
        WaitingForReacquisition,
        Reacquired,
        Completed
    }

    [SerializeField] private PredatorFSM predator;
    [SerializeField] private PredatorNavigationMovementController navigationController;
    [SerializeField] private NavigationGridGraphProvider graphProvider;
    [SerializeField] private GameObject target;
    [SerializeField] private Transform normalPredatorSpawn;
    [SerializeField] private Transform normalLastKnownPosition;
    [SerializeField] private Transform noPathPredatorSpawn;
    [SerializeField] private Transform noPathLastKnownPosition;
    [SerializeField] private Transform hiddenTargetPosition;

    private readonly List<GraphNavigationAgent.NavigationStatus> terminalHistory =
        new List<GraphNavigationAgent.NavigationStatus>(16);
    private ScenarioPhase phase = ScenarioPhase.Ready;
    private PredatorStateId previousState;
    private bool hasPreviousState;
    private bool searchObserved;
    private bool sawFollowingPath;
    private bool sawFollowingDirect;
    private bool sawArrived;
    private bool sawNoPath;
    private bool sawMissingProvider;
    private bool sawPatrolAfterSearch;
    private bool sawPursuitAfterSearch;
    private bool initialized;
    private bool hasRecordedTerminal;
    private Vector3 lastTerminalDestination;
    private GraphNavigationAgent.NavigationStatus lastTerminalStatus;
    private Vector2 controlsScroll;

    public Scenario ActiveScenario { get; private set; }
    public ScenarioPhase Phase => phase;
    public IReadOnlyList<GraphNavigationAgent.NavigationStatus> TerminalHistory => terminalHistory;
    public bool SawFollowingPath => sawFollowingPath;
    public bool SawFollowingDirect => sawFollowingDirect;
    public bool SawArrived => sawArrived;
    public bool SawNoPath => sawNoPath;
    public bool SawMissingProvider => sawMissingProvider;
    public bool SawPatrolAfterSearch => sawPatrolAfterSearch;
    public bool SawPursuitAfterSearch => sawPursuitAfterSearch;

    public void Configure(
        PredatorFSM configuredPredator,
        PredatorNavigationMovementController configuredNavigationController,
        NavigationGridGraphProvider configuredGraphProvider,
        GameObject configuredTarget,
        Transform configuredNormalPredatorSpawn,
        Transform configuredNormalLastKnownPosition,
        Transform configuredNoPathPredatorSpawn,
        Transform configuredNoPathLastKnownPosition,
        Transform configuredHiddenTargetPosition)
    {
        predator = configuredPredator;
        navigationController = configuredNavigationController;
        graphProvider = configuredGraphProvider;
        target = configuredTarget;
        normalPredatorSpawn = configuredNormalPredatorSpawn;
        normalLastKnownPosition = configuredNormalLastKnownPosition;
        noPathPredatorSpawn = configuredNoPathPredatorSpawn;
        noPathLastKnownPosition = configuredNoPathLastKnownPosition;
        hiddenTargetPosition = configuredHiddenTargetPosition;
    }

    private void Awake()
    {
        if (predator == null || navigationController == null || graphProvider == null || target == null)
        {
            enabled = false;
            Debug.LogError("The I3.3.2 Search harness is missing required scene references.", this);
            return;
        }
    }

    private System.Collections.IEnumerator Start()
    {
        yield return null;
        predator.ConfigureTargets(new[] { target });
        ResetScenario();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        CaptureNavigationStatus();
        CaptureStateTransition();

        if (phase == ScenarioPhase.WaitingForPursuit && predator.CurrentState == PredatorStateId.Pursuit)
        {
            MoveTarget(hiddenTargetPosition.position);
            phase = ScenarioPhase.WaitingForSearch;
        }
        else if (phase == ScenarioPhase.WaitingForSearch && predator.CurrentState == PredatorStateId.Search)
        {
            searchObserved = true;
            phase = ScenarioPhase.Searching;
        }
        else if (phase == ScenarioPhase.WaitingForReacquisition &&
            predator.CurrentState == PredatorStateId.Pursuit)
        {
            sawPursuitAfterSearch = true;
            phase = ScenarioPhase.Reacquired;
        }
        else if (searchObserved && predator.CurrentState == PredatorStateId.Patrol &&
            phase != ScenarioPhase.Ready)
        {
            sawPatrolAfterSearch = true;
            phase = ScenarioPhase.Completed;
        }
    }

    public void StartNormalAStarScenario()
    {
        StartScenario(Scenario.NormalAStar, normalPredatorSpawn.position,
            normalLastKnownPosition.position, true);
    }

    public void StartNoPathScenario()
    {
        StartScenario(Scenario.NoPath, noPathPredatorSpawn.position,
            noPathLastKnownPosition.position, true);
    }

    public void StartMissingProviderScenario()
    {
        StartScenario(Scenario.MissingProvider, normalPredatorSpawn.position,
            normalLastKnownPosition.position, false);
    }

    public void ReacquireTarget()
    {
        if (predator == null || target == null || predator.CurrentState != PredatorStateId.Search)
            return;

        Vector3 visiblePosition = FindVisibleReacquisitionPosition();
        MoveTarget(visiblePosition);
        phase = ScenarioPhase.WaitingForReacquisition;
    }

    public void ResetScenario()
    {
        if (predator == null || navigationController == null || target == null ||
            normalPredatorSpawn == null || hiddenTargetPosition == null)
            return;

        navigationController.ConfigureGraphProvider(graphProvider);
        predator.ClearTargetAndReturnToPatrol();
        predator.transform.SetPositionAndRotation(normalPredatorSpawn.position,
            Quaternion.LookRotation(Vector3.forward, Vector3.up));
        predator.ConfigureTargets(new[] { target });
        MoveTarget(hiddenTargetPosition.position);

        ActiveScenario = Scenario.None;
        phase = ScenarioPhase.Ready;
        terminalHistory.Clear();
        hasPreviousState = false;
        searchObserved = false;
        sawFollowingPath = false;
        sawFollowingDirect = false;
        sawArrived = false;
        sawNoPath = false;
        sawMissingProvider = false;
        sawPatrolAfterSearch = false;
        sawPursuitAfterSearch = false;
        hasRecordedTerminal = false;
    }

    private void StartScenario(Scenario scenario, Vector3 predatorPosition,
        Vector3 lastKnownPosition, bool connectProvider)
    {
        ResetScenario();
        ActiveScenario = scenario;
        navigationController.ConfigureGraphProvider(connectProvider ? graphProvider : null);
        predator.transform.SetPositionAndRotation(predatorPosition,
            HorizontalLookRotation(predatorPosition, lastKnownPosition));
        MoveTarget(lastKnownPosition);
        phase = ScenarioPhase.WaitingForPursuit;
    }

    private void CaptureNavigationStatus()
    {
        if (ActiveScenario == Scenario.None || phase == ScenarioPhase.Ready ||
            phase == ScenarioPhase.Completed)
            return;

        GraphNavigationAgent.NavigationStatus status = navigationController.Status;
        sawFollowingPath |= status == GraphNavigationAgent.NavigationStatus.FollowingPath;
        sawFollowingDirect |= status == GraphNavigationAgent.NavigationStatus.FollowingDirect;

        if (status == GraphNavigationAgent.NavigationStatus.Arrived)
        {
            sawArrived = true;
            RecordTerminal(status);
        }
        else if (status == GraphNavigationAgent.NavigationStatus.NoPath)
        {
            sawNoPath = true;
            RecordTerminal(status);
        }
        else if (status == GraphNavigationAgent.NavigationStatus.MissingProvider)
        {
            sawMissingProvider = true;
            RecordTerminal(status);
        }
    }

    private void RecordTerminal(GraphNavigationAgent.NavigationStatus status)
    {
        Vector3 destination = navigationController.RequestedDestination;
        if (hasRecordedTerminal && lastTerminalStatus == status &&
            Vector3.ProjectOnPlane(destination - lastTerminalDestination, Vector3.up).sqrMagnitude <= 0.0001f)
            return;

        terminalHistory.Add(status);
        if (terminalHistory.Count > 16) terminalHistory.RemoveAt(0);
        hasRecordedTerminal = true;
        lastTerminalStatus = status;
        lastTerminalDestination = destination;
    }

    private void CaptureStateTransition()
    {
        PredatorStateId currentState = predator.CurrentState;
        if (!hasPreviousState)
        {
            previousState = currentState;
            hasPreviousState = true;
            return;
        }

        if (previousState == PredatorStateId.Search && currentState == PredatorStateId.Pursuit)
            sawPursuitAfterSearch = true;
        if (previousState == PredatorStateId.Search && currentState == PredatorStateId.Patrol)
            sawPatrolAfterSearch = true;
        previousState = currentState;
    }

    private Vector3 FindVisibleReacquisitionPosition()
    {
        Vector3 origin = predator.transform.position;
        Vector3[] directions =
        {
            predator.transform.forward,
            predator.transform.right,
            -predator.transform.right,
            -predator.transform.forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = origin + Vector3.ProjectOnPlane(directions[i], Vector3.up).normalized * 4f;
            Vector3 eyeOrigin = origin + Vector3.up * 1.5f;
            Vector3 eyeTarget = candidate + Vector3.up * 1.5f;
            if (!Physics.Linecast(eyeOrigin, eyeTarget, predator.Perception.ObstacleLayers,
                QueryTriggerInteraction.Ignore))
                return candidate;
        }

        return origin + Vector3.up * 0.1f;
    }

    private void MoveTarget(Vector3 position)
    {
        target.transform.position = position;
        Physics.SyncTransforms();
    }

    private static Quaternion HorizontalLookRotation(Vector3 origin, Vector3 targetPosition)
    {
        Vector3 direction = Vector3.ProjectOnPlane(targetPosition - origin, Vector3.up);
        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !enabled || !initialized) return;

        float panelHeight = Mathf.Min(300f, Mathf.Max(220f, Screen.height - 32f));
        GUILayout.BeginArea(new Rect(Mathf.Max(456f, Screen.width - 336f), 16f, 320f, panelHeight),
            GUI.skin.box);
        controlsScroll = GUILayout.BeginScrollView(controlsScroll);
        GUILayout.Label("I3.3.2 SEARCH SCENARIOS");
        GUILayout.Label($"Scenario: {ActiveScenario}; phase: {phase}");
        if (GUILayout.Button("Start normal A* Search")) StartNormalAStarScenario();
        if (GUILayout.Button("Start NoPath Search")) StartNoPathScenario();
        if (GUILayout.Button("Start MissingProvider Search")) StartMissingProviderScenario();
        GUI.enabled = predator.CurrentState == PredatorStateId.Search;
        if (GUILayout.Button("Reacquire target during Search")) ReacquireTarget();
        GUI.enabled = true;
        if (GUILayout.Button("Reset scenario")) ResetScenario();
        GUILayout.Space(6f);
        GUILayout.Label("The harness moves only the target and resets scene configuration.");
        GUILayout.Label("FSM transitions, navigation and Predator movement remain production-owned.");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
