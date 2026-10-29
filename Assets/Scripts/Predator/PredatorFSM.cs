using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PredatorMovement), typeof(PredatorPerception), typeof(PredatorCombat))]
[RequireComponent(typeof(PredatorAnimationController))]
[RequireComponent(typeof(BoidThreat))]
public sealed class PredatorFSM : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject alienTarget;

    [Header("Patrol")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float patrolSpeed = 6f;
    [SerializeField, Min(0.1f)] private float waypointCheckDistance = 0.6f;

    [Header("Pursuit")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 7.6f;
    [SerializeField, Min(0.1f)] private float hunterMaximumSpeed = 8f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Range(0.1f, 1f)] private float captureRadius = 0.9f;
    [SerializeField, Range(0.1f, 0.99f)] private float speedBelowHunterRatio = 0.95f;

    private StateMachine stateMachine;
    private PredatorMovement movement;
    private PredatorPerception perception;
    private PredatorCombat combat;
    private PredatorAnimationController animationController;

    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointCheckDistance => waypointCheckDistance;
    public float CaptureRadius => captureRadius;
    public PredatorMovement Movement => movement;
    public PredatorPerception Perception => perception;
    public PredatorCombat Combat => combat;
    public PredatorAnimationController Animation => animationController;
    public GameObject CurrentTarget { get; private set; }
    public Vector3 LastKnownPosition => perception != null ? perception.LastSeenPosition : transform.position;
    public bool HasLastKnownPosition => perception != null && perception.HasLastKnownPosition;
    public PredatorStateId CurrentState => stateMachine?.CurrentStateKey is PredatorStateId state
        ? state
        : PredatorStateId.Patrol;

    public void ConfigureWaypoints(IReadOnlyList<Transform> newWaypoints)
    {
        waypoints.Clear();
        if (newWaypoints == null) return;
        for (int i = 0; i < newWaypoints.Count; i++) waypoints.Add(newWaypoints[i]);
    }

    public void ConfigureTargets(IReadOnlyList<GameObject> targets)
    {
        perception.ConfigureTargets(targets);
    }

    public void Configure(float patrolMovementSpeed, float pursuitMovementSpeed,
        float maximumHunterSpeed, float rotationSpeed, float targetCaptureRadius,
        float maximumSpeedRatio, float viewDistance, float viewAngle, LayerMask alienLayers,
        LayerMask obstacleLayers, float perceptionRefreshInterval, float eyeHeight,
        float attackFallbackDuration)
    {
        hunterMaximumSpeed = Mathf.Max(0.1f, maximumHunterSpeed);
        speedBelowHunterRatio = Mathf.Clamp(maximumSpeedRatio, 0.95f, 0.99f);
        patrolSpeed = patrolMovementSpeed;
        chaseSpeed = pursuitMovementSpeed;
        turnSpeed = Mathf.Max(0.1f, rotationSpeed);
        captureRadius = Mathf.Clamp(targetCaptureRadius, 0.1f, 1f);
        ClampMovementSpeeds();
        perception.Configure(viewDistance, viewAngle, alienLayers, obstacleLayers,
            perceptionRefreshInterval, eyeHeight);
        animationController.ConfigureAttackDuration(attackFallbackDuration);
    }

    public void SetTarget(GameObject target) { CurrentTarget = target; }

    private void Awake()
    {
        CacheDependencies();
        if (alienTarget != null)
            perception.AddTarget(alienTarget);
        ConfigureComponents();
        InitializeStateMachine();
        if (GetComponent<PredatorDebugFeedback>() == null)
            gameObject.AddComponent<PredatorDebugFeedback>();
    }

    private void CacheDependencies()
    {
        movement = GetComponent<PredatorMovement>();
        perception = GetComponent<PredatorPerception>();
        combat = GetComponent<PredatorCombat>();
        animationController = GetComponent<PredatorAnimationController>();
    }

    private void ConfigureComponents()
    {
        animationController.ConfigureAnimator(GetComponentInChildren<Animator>());
    }

    private void InitializeStateMachine()
    {
        stateMachine = new StateMachine();
        stateMachine.RegisterState(PredatorStateId.Patrol, new PredatorPatrolState(this, stateMachine));
        stateMachine.RegisterState(PredatorStateId.Search, new PredatorSearchState(this, stateMachine));
        stateMachine.RegisterState(PredatorStateId.Pursuit, new PredatorChaseState(this, stateMachine));
        stateMachine.RegisterState(PredatorStateId.Attack, new PredatorAttackState(this, stateMachine));
        stateMachine.ChangeState(PredatorStateId.Patrol);
    }

    private void Start()
    {
        movement.Configure(patrolSpeed, chaseSpeed, turnSpeed,
            hunterMaximumSpeed * speedBelowHunterRatio);
    }

    private void OnEnable()
    {
        PredatorAlertSystem.AlertRaised += ReceiveAlert;
        if (stateMachine?.CurrentStateKey == null)
            stateMachine?.ChangeState(PredatorStateId.Patrol);
    }

    private void OnDisable()
    {
        PredatorAlertSystem.AlertRaised -= ReceiveAlert;
        stateMachine?.Stop();
        movement?.Stop();
    }

    private void Update()
    {
        perception?.Tick(Time.deltaTime);
        combat?.Tick(Time.deltaTime);

        if (perception != null && perception.CanSeeTarget)
        {
            CurrentTarget = perception.Target;
            if (perception.NewlyDetected)
            {
                PredatorAlertSystem.Broadcast(this, CurrentTarget, perception.LastSeenPosition);
            }
        }

        if (!IsTargetAvailable(CurrentTarget) && CurrentTarget != null)
        {
            ClearTargetAndReturnToPatrol();
            return;
        }

        stateMachine?.Update();
    }

    private void ReceiveAlert(PredatorFSM source, GameObject target, Vector3 lastKnownPosition)
    {
        if (source == this || !IsTargetAvailable(target)) return;
        CurrentTarget = target;
        perception.ReceiveAlert(target, lastKnownPosition);
        if (!perception.CanSeeTarget) stateMachine.ChangeState(PredatorStateId.Search);
    }

    public bool CanStartAttack()
    {
        return IsTargetAvailable(CurrentTarget) && perception != null &&
            perception.CanTrackTarget(CurrentTarget) && combat != null &&
            combat.IsAttackReady && HorizontalDistanceTo(CurrentTarget.transform.position) <= captureRadius;
    }

    public bool IsTargetAvailable(GameObject target)
    {
        return target != null && target.activeInHierarchy &&
            !PredatorCaptureRegistry.IsCaptured(target) &&
            !SafeZoneVolume.IsPositionSafe(target.transform.position);
    }

    public float HorizontalDistanceTo(Vector3 position)
    {
        return Vector3.ProjectOnPlane(position - transform.position, Vector3.up).magnitude;
    }

    public void ClearTargetAndReturnToPatrol()
    {
        CurrentTarget = null;
        perception.ClearTarget();
        stateMachine?.ChangeState(PredatorStateId.Patrol);
    }

    private void OnValidate()
    {
        ClampMovementSpeeds();
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        waypointCheckDistance = Mathf.Max(0.1f, waypointCheckDistance);
        captureRadius = Mathf.Clamp(captureRadius, 0.1f, 1f);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.95f, 0.99f);
    }

    private void ClampMovementSpeeds()
    {
        hunterMaximumSpeed = Mathf.Max(8f, hunterMaximumSpeed);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.95f, 0.99f);
        float maximumPredatorSpeed = hunterMaximumSpeed * speedBelowHunterRatio;
        float minimumPursuitSpeed = Mathf.Min(hunterMaximumSpeed * 0.9f, maximumPredatorSpeed);
        chaseSpeed = Mathf.Clamp(chaseSpeed, minimumPursuitSpeed, maximumPredatorSpeed);
        float maximumPatrolSpeed = chaseSpeed * 0.9f;
        float minimumPatrolSpeed = Mathf.Min(hunterMaximumSpeed * 0.75f, maximumPatrolSpeed);
        patrolSpeed = Mathf.Clamp(patrolSpeed, minimumPatrolSpeed, maximumPatrolSpeed);
    }

}

public enum PredatorStateId
{
    Patrol,
    Search,
    Pursuit,
    Attack
}
