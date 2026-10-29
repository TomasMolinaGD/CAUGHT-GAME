using System.Collections.Generic;
using UnityEngine;
using CaughtGame.Navigation;

[RequireComponent(typeof(HunterMovement), typeof(HunterPerception), typeof(HunterInterestSpawner))]
[RequireComponent(typeof(HunterCombat), typeof(HunterAnimationController))]
[RequireComponent(typeof(AlienVisualGrounding))]
public class FSMAgent : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float speed = 3f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Min(0.1f)] private float waypointCheckDistance = 0.5f;

    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 2f;

    [Header("Gather")]
    [SerializeField, Min(0.1f)] private float collectionDistance = 1.4f;
    [SerializeField, Min(0.1f)] private float collectionDuration = 2f;
    [SerializeField, Min(1)] private int requiredGathers = 3;

    [Header("Grid Navigation")]
    [SerializeField] private NavigationGrid navigationGrid;
    [SerializeField] private HuntingGroundRegion huntingGroundRegion;
    [SerializeField] private bool enableGridNavigation;
    [Header("Search Exploration")]
    [SerializeField] private int searchRandomSeed = 417;
    [SerializeField, Min(0f)] private float searchMinimumDistanceMultiplier = 0.75f;
    [SerializeField, Min(0.1f)] private float searchMaximumDistanceMultiplier = 2f;
    [SerializeField, Min(1)] private int searchSelectionAttempts = 24;

    private StateMachine stateMachine;
    private HunterMovement movement;
    private HunterPerception perception;
    private HunterInterestSpawner interestSpawner;
    private HunterCombat combat;
    private HunterAnimationController animationController;
    private ExitState exitState;
    private Renderer[] visualRenderers;
    private HunterNavigationIntent navigationIntent;
    private HunterNavigationController navigationController;
    private bool exitRestartDiagnosticReported;

    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointCheckDistance => waypointCheckDistance;
    public HunterMovement Movement => movement;
    public HunterPerception Perception => perception;
    public HunterInterestSpawner InterestSpawner => interestSpawner;
    public HunterCombat Combat => combat;
    public HunterAnimationController Animation => animationController;
    public HunterNavigationIntent NavigationIntent => navigationIntent ?? (navigationIntent = new HunterNavigationIntent());
    public HunterNavigationController NavigationController => navigationController;
    public NavigationGrid NavigationGrid => navigationGrid;
    public HuntingGroundRegion HuntingGroundRegion => huntingGroundRegion;
    public bool GridNavigationEnabled => enableGridNavigation;
    public float PursuitSpeedMultiplier => pursuitSpeedMultiplier;
    public float CollectionDistance => collectionDistance;
    public float CollectionDuration => collectionDuration;
    public int RequiredGathers => requiredGathers;
    public int CompletedGathers { get; private set; }
    public bool HasReachedRequiredGathers => CompletedGathers >= requiredGathers;
    public PoliceState EngagementReturnState => enableGridNavigation ? PoliceState.Search : PoliceState.Patrol;
    public BoidLife CurrentTarget { get; private set; }
    public PoliceState CurrentState => stateMachine?.CurrentStateKey is PoliceState state
        ? state
        : PoliceState.Patrol;

    private void Awake()
    {
        CacheDependencies();
        ConfigureComponents();
        ConfigureNavigationController();
        navigationController.SetEnabled(enableGridNavigation);
        InitializeStateMachine();
        visualRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        TickAgentSystems();
        movement?.BeginMovementFrame();
        navigationController?.Tick();
        ExecuteNavigationMovement();
        stateMachine?.Update();
        TryRestartAfterExit();
    }

    private void OnEnable()
    {
        if (stateMachine != null && stateMachine.CurrentState == null)
        {
            stateMachine.ChangeState(enableGridNavigation ? PoliceState.Search : PoliceState.Patrol);
        }

        navigationController?.SetEnabled(enableGridNavigation);
    }

    private void CacheDependencies()
    {
        movement = GetComponent<HunterMovement>();
        perception = GetComponent<HunterPerception>();
        interestSpawner = GetComponent<HunterInterestSpawner>();
        combat = GetComponent<HunterCombat>();
        animationController = GetComponent<HunterAnimationController>();
    }

    private void ConfigureComponents()
    {
        movement.Configure(speed, turnSpeed);
    }

    private void InitializeStateMachine()
    {
        stateMachine = new StateMachine();
        stateMachine.RegisterState(PoliceState.Patrol, new PatrolState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Search, new SearchState(
            this, stateMachine, huntingGroundRegion, searchRandomSeed,
            searchMinimumDistanceMultiplier, searchMaximumDistanceMultiplier, searchSelectionAttempts));
        stateMachine.RegisterState(PoliceState.Pursuit, new PursuitState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Attack, new AttackState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Gather, new GatherState(this, stateMachine));
        exitState = new ExitState(this, stateMachine);
        stateMachine.RegisterState(PoliceState.Exit, exitState);
        stateMachine.ChangeState(enableGridNavigation ? PoliceState.Search : PoliceState.Patrol);
    }

    private void TickAgentSystems()
    {
        // Perception updates on its own interval and keeps the latest results
        // available to the FSM between physics queries.
        perception?.Tick(Time.deltaTime);
        combat?.Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        stateMachine?.Stop();
        movement?.Stop();
        navigationController?.SetEnabled(false);
        navigationIntent?.CancelAll();
    }

    public void ConfigureNavigation(NavigationGrid grid, bool enabled)
    {
        navigationGrid = grid;
        enableGridNavigation = enabled;
        ConfigureNavigationController();
        navigationController.SetEnabled(enabled);
    }

    public bool CanStartAttack()
    {
        return combat != null && combat.IsAttackReady &&
            perception != null && perception.ClosestLivingBoid != null;
    }

    public bool CanStartGather()
    {
        return perception != null && perception.ClosestDeadBoid != null;
    }

    public void SetCurrentTarget(BoidLife target)
    {
        CurrentTarget = target;
        if (target == null)
        {
            combat?.ClearTargetCommitment();
        }
    }

    /// <summary>Resets the Hunter's gather progress for a future run or respawn.</summary>
    public void ResetGatherProgress()
    {
        CompletedGathers = 0;
    }

    /// <summary>Records one successful collection performed by GatherState.</summary>
    internal void RegisterGatherCompletion()
    {
        if (CompletedGathers < requiredGathers)
        {
            CompletedGathers++;
        }
    }

    public ulong RequestNavigation(Vector3 destination, float stoppingDistance, float speedMultiplier = 1f) =>
        NavigationIntent.RequestNavigation(destination, stoppingDistance, speedMultiplier);

    public bool ReplaceNavigation(
        ulong expectedId,
        Vector3 destination,
        float stoppingDistance,
        float speedMultiplier,
        out ulong newId) =>
        NavigationIntent.ReplaceNavigation(
            expectedId, destination, stoppingDistance, speedMultiplier, out newId);

    public bool CancelNavigation(ulong expectedId) => NavigationIntent.CancelNavigation(expectedId);

    private void TryRestartAfterExit()
    {
        if (!enableGridNavigation || !Equals(stateMachine?.CurrentStateKey, PoliceState.Exit) ||
            exitState == null || !exitState.TryGetCompletedRequestId(out ulong completedRequestId) ||
            !NavigationIntent.TryGetCurrent(out HunterNavigationRequest request) ||
            request.Id != completedRequestId)
        {
            if (!Equals(stateMachine?.CurrentStateKey, PoliceState.Exit))
            {
                exitRestartDiagnosticReported = false;
            }
            return;
        }

        if (navigationGrid == null || movement == null ||
            !HunterSpawnSelector.TrySelectSpawn(navigationGrid, movement.GroundHeight, out Vector3 spawnPosition))
        {
            ReportExitRestartFailure("No valid Hunter Spawn is available.");
            return;
        }

        exitState.PrepareForRestart();
        navigationController?.SetEnabled(false);
        NavigationIntent.CancelAll();

        SetVisualsVisible(false);
        HunterMovement.PlacementResult placement = movement.TryStopAndPlace(spawnPosition);
        if (placement != HunterMovement.PlacementResult.Placed)
        {
            SetVisualsVisible(true);
            ReportExitRestartFailure("Hunter Spawn placement was rejected.");
            return;
        }

        SetCurrentTarget(null);
        ResetGatherProgress();
        movement.Stop();
        Animation.SetMoving(false);
        Animation.SetAttacking(false);
        perception?.ClearDetections();
        navigationController?.SetEnabled(true);
        exitRestartDiagnosticReported = false;
        SetVisualsVisible(true);
        stateMachine.ChangeState(PoliceState.Search);
    }

    private void ReportExitRestartFailure(string message)
    {
        if (exitRestartDiagnosticReported)
        {
            return;
        }

        exitRestartDiagnosticReported = true;
        Debug.LogWarning($"Hunter Exit restart deferred: {message}", this);
    }

    private void SetVisualsVisible(bool visible)
    {
        if (visualRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in visualRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private void ConfigureNavigationController()
    {
        navigationController = new HunterNavigationController(transform, NavigationIntent, navigationGrid);
    }

    private void ExecuteNavigationMovement()
    {
        if (!enableGridNavigation || navigationController == null || movement == null ||
            navigationController.Status != HunterNavigationControllerStatus.InProgress ||
            !NavigationIntent.TryGetCurrent(out HunterNavigationRequest request))
        {
            return;
        }

        ulong requestId = request.Id;
        ulong routeVersion = navigationController.CurrentRouteVersion;
        int waypointIndex = navigationController.CurrentWaypointIndex;
        Vector3 target;
        bool isWaypoint = navigationController.TryGetNextWaypoint(out target);
        if (!isWaypoint && !navigationController.TryGetTerminalDestination(
                requestId, routeVersion, out target))
        {
            navigationController.Tick();
            return;
        }

        movement.MoveTowardsNavigation(target, request.StoppingDistance, request.SpeedMultiplier);

        if (isWaypoint && IsWithinHorizontalDistance(target, request.StoppingDistance))
        {
            navigationController.TryAdvanceWaypoint(requestId, routeVersion, waypointIndex);
        }

        navigationController.Tick();
    }

    private bool IsWithinHorizontalDistance(Vector3 target, float stoppingDistance)
    {
        Vector3 delta = transform.position - target;
        delta.y = 0f;
        return delta.sqrMagnitude <= stoppingDistance * stoppingDistance;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        waypointCheckDistance = Mathf.Max(0.1f, waypointCheckDistance);
        pursuitSpeedMultiplier = Mathf.Max(1f, pursuitSpeedMultiplier);
        collectionDistance = Mathf.Max(0.1f, collectionDistance);
        collectionDuration = Mathf.Max(0.1f, collectionDuration);
        requiredGathers = Mathf.Max(1, requiredGathers);
        searchMinimumDistanceMultiplier = Mathf.Max(0f, searchMinimumDistanceMultiplier);
        searchMaximumDistanceMultiplier = Mathf.Max(0.1f, searchMaximumDistanceMultiplier);
        searchSelectionAttempts = Mathf.Max(1, searchSelectionAttempts);
    }
}
