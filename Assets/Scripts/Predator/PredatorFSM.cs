using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PredatorMovement), typeof(PredatorPerception), typeof(PredatorCombat))]
[RequireComponent(typeof(PredatorAnimationController))]
[RequireComponent(typeof(BoidThreat))]
public sealed class PredatorFSM : MonoBehaviour
{
    [Header("Target")]
    [FormerlySerializedAs("alienTarget")]
    [SerializeField] private GameObject targetReference;

    [Header("Patrol")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float patrolSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float waypointCheckDistance = 0.6f;

    [Header("Pursuit")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 5.4f;
    [SerializeField, Min(0.1f)] private float hunterPatrolSpeed = 3f;
    [SerializeField, Min(0.1f)] private float hunterMaximumSpeed = 6f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Range(0.1f, 1f)] private float captureRadius = 0.9f;
    [SerializeField, Range(0.1f, 0.99f)] private float speedBelowHunterRatio = 0.9f;
    [SerializeField] private AudioSource howlSource;
    [SerializeField] private AudioClip howlClip;

    private StateMachine stateMachine;
    private PredatorMovement movement;
    private PredatorPerception perception;
    private PredatorCombat combat;
    private PredatorAnimationController animationController;
    private PredatorAlertSystem alertSystem;

    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointCheckDistance => waypointCheckDistance;
    public float CaptureRadius => captureRadius;
    public PredatorMovement Movement => movement;
    public PredatorPerception Perception => perception;
    public PredatorCombat Combat => combat;
    public PredatorAnimationController Animation => animationController;
    public bool ReceivedAlert { get; private set; }
    public int AlertsSent { get; private set; }
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
        if (targetReference != null) perception.AddTarget(targetReference);
    }

    public void ConfigureAlertSystem(PredatorAlertSystem system)
    {
        if (alertSystem == system) return;
        if (alertSystem != null) alertSystem.AlertRaised -= ReceiveAlert;
        alertSystem = system;
        if (isActiveAndEnabled && alertSystem != null)
        {
            alertSystem.AlertRaised += ReceiveAlert;
            if (alertSystem.TryGetRecentAlert(out PredatorAlert recentAlert))
                ReceiveAlert(recentAlert);
        }
    }

    public void Configure(float patrolMovementSpeed, float pursuitMovementSpeed,
        float maximumHunterSpeed, float hunterBaseSpeed, float rotationSpeed, float targetCaptureRadius,
        float maximumSpeedRatio, float viewDistance, float viewAngle, LayerMask alienLayers,
        LayerMask obstacleLayers, float perceptionRefreshInterval, float eyeHeight,
        float attackFallbackDuration, AudioClip alertClip)
    {
        hunterMaximumSpeed = Mathf.Clamp(maximumHunterSpeed, 0.1f, 6f);
        hunterPatrolSpeed = Mathf.Clamp(hunterBaseSpeed, 0.1f, 3f);
        speedBelowHunterRatio = Mathf.Clamp(maximumSpeedRatio, 0.1f, 0.99f);
        patrolSpeed = patrolMovementSpeed;
        chaseSpeed = pursuitMovementSpeed;
        turnSpeed = Mathf.Max(0.1f, rotationSpeed);
        captureRadius = Mathf.Clamp(targetCaptureRadius, 0.1f, 1f);
        ClampMovementSpeeds();
        perception.Configure(viewDistance, viewAngle, alienLayers, obstacleLayers,
            perceptionRefreshInterval, eyeHeight);
        if (alertClip != null) howlClip = alertClip;
        animationController.ConfigureAttackDuration(attackFallbackDuration);
    }

    public void SetTarget(GameObject target) { CurrentTarget = target; }

    private void Awake()
    {
        CacheDependencies();
        if (howlSource != null) howlSource.spatialBlend = 1f;
        ConfigureComponents();
        RegisterTarget(targetReference);
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
            hunterMaximumSpeed * speedBelowHunterRatio, perception.ObstacleLayers);
    }

    private void OnEnable()
    {
        if (alertSystem != null) alertSystem.AlertRaised += ReceiveAlert;
        if (stateMachine?.CurrentStateKey == null)
            stateMachine?.ChangeState(PredatorStateId.Patrol);
    }

    private void OnDisable()
    {
        if (alertSystem != null) alertSystem.AlertRaised -= ReceiveAlert;
        stateMachine?.Stop();
        movement?.Stop();
    }

    private void Update()
    {
        UpdateComponents();
        UpdateVisibleTarget();
        if (DiscardUnavailableTarget()) return;
        stateMachine?.Update();
    }

    private void UpdateComponents()
    {
        perception?.Tick(Time.deltaTime);
        combat?.Tick(Time.deltaTime);
    }

    private void UpdateVisibleTarget()
    {
        if (perception == null || !perception.CanSeeTarget) return;

        CurrentTarget = perception.Target;
        if (!perception.NewlyDetected) return;

        PlayHowl();
        if (alertSystem == null) return;

        alertSystem.Broadcast(this, CurrentTarget, perception.LastSeenPosition);
        AlertsSent++;
    }

    private bool DiscardUnavailableTarget()
    {
        if (CurrentTarget == null || IsTargetAvailable(CurrentTarget)) return false;
        ClearTargetAndReturnToPatrol();
        return true;
    }

    private void RegisterTarget(GameObject target)
    {
        if (target != null) perception.AddTarget(target);
    }

    private void ReceiveAlert(PredatorAlert alert)
    {
        if (alert.Source == this || alertSystem == null || !alertSystem.IsRecent(alert) ||
            !IsTargetAvailable(alert.Target)) return;
        if (perception != null && perception.CanSeeTarget) return;
        CurrentTarget = alert.Target;
        ReceivedAlert = true;
        perception.ReceiveAlert(alert.Target, alert.LastKnownPosition);
        if (!perception.CanSeeTarget) stateMachine.ChangeState(PredatorStateId.Search);
    }

    private void PlayHowl()
    {
        if (howlClip == null) return;
        if (howlSource == null) howlSource = GetComponent<AudioSource>();
        if (howlSource == null) howlSource = gameObject.AddComponent<AudioSource>();
        howlSource.spatialBlend = 1f;
        howlSource.PlayOneShot(howlClip);
    }

    public bool CanStartAttack()
    {
        return IsTargetAvailable(CurrentTarget) && perception != null &&
            perception.CanTrackTarget(CurrentTarget) && combat != null &&
            combat.IsAttackReady && HorizontalDistanceTo(CurrentTarget.transform.position) <= captureRadius;
    }

    public bool IsTargetAvailable(GameObject target)
    {
        return perception != null && perception.IsValidTarget(target) && target.activeInHierarchy &&
            !PredatorCaptureRegistry.IsCaptured(target, perception.IsExplicitThreatTarget(target)) &&
            !SafeZoneVolume.IsPositionSafe(target.transform.position);
    }

    public float HorizontalDistanceTo(Vector3 position)
    {
        return Vector3.ProjectOnPlane(position - transform.position, Vector3.up).magnitude;
    }

    public void ClearTargetAndReturnToPatrol()
    {
        CurrentTarget = null;
        ReceivedAlert = false;
        perception.ClearTarget();
        stateMachine?.ChangeState(PredatorStateId.Patrol);
    }

    private void OnValidate()
    {
        ClampMovementSpeeds();
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        waypointCheckDistance = Mathf.Max(0.1f, waypointCheckDistance);
        captureRadius = Mathf.Clamp(captureRadius, 0.1f, 1f);
        hunterPatrolSpeed = Mathf.Max(0.1f, hunterPatrolSpeed);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.1f, 0.99f);
    }

    private void ClampMovementSpeeds()
    {
        hunterMaximumSpeed = Mathf.Clamp(hunterMaximumSpeed, 0.1f, 6f);
        hunterPatrolSpeed = Mathf.Clamp(hunterPatrolSpeed, 0.1f, 3f);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.1f, 0.99f);
        float maximumPredatorSpeed = hunterMaximumSpeed * speedBelowHunterRatio;
        chaseSpeed = Mathf.Clamp(chaseSpeed, 0.1f, maximumPredatorSpeed);
        float maximumPatrolSpeed = Mathf.Min(chaseSpeed * 0.9f, hunterPatrolSpeed * 0.95f);
        patrolSpeed = Mathf.Clamp(patrolSpeed, 0.1f, maximumPatrolSpeed);
    }

}

public enum PredatorStateId
{
    Patrol,
    Search,
    Pursuit,
    Attack
}
