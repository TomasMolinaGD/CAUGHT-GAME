using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PredatorMovement), typeof(PredatorPerception), typeof(PredatorCombat))]
[RequireComponent(typeof(BoidThreat), typeof(SphereCollider), typeof(AudioSource))]
public sealed class PredatorFSM : MonoBehaviour
{
    private static AudioClip generatedAlertClip;
    [Header("Patrol")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float patrolSpeed = 2f;
    [SerializeField, Min(0.1f)] private float waypointCheckDistance = 0.6f;

    [Header("Pursuit")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Min(0.1f)] private float captureRadius = 1.5f;
    [SerializeField, Range(0.1f, 0.99f)] private float speedBelowHunterRatio = 0.9f;

    [Header("Alert")]
    [SerializeField] private AudioSource alertAudioSource;
    [SerializeField] private AudioClip alertHowl;

    private StateMachine stateMachine;
    private PredatorMovement movement;
    private PredatorPerception perception;
    private PredatorCombat combat;

    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointCheckDistance => waypointCheckDistance;
    public float CaptureRadius => captureRadius;
    public PredatorMovement Movement => movement;
    public PredatorPerception Perception => perception;
    public PredatorCombat Combat => combat;
    public FSMAgent CurrentTarget { get; private set; }
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

    public void SetTarget(FSMAgent target) { CurrentTarget = target; }

    private void Awake()
    {
        CacheDependencies();
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
    }

    private void ConfigureComponents()
    {
        if (alertAudioSource == null) alertAudioSource = GetComponent<AudioSource>();
        alertAudioSource.spatialBlend = 1f;
        SphereCollider threatTrigger = GetComponent<SphereCollider>();
        threatTrigger.isTrigger = true;
        threatTrigger.radius = perception.ViewDistance;
    }

    private void InitializeStateMachine()
    {
        stateMachine = new StateMachine();
        stateMachine.RegisterState(PredatorStateId.Patrol, new PredatorPatrolState(this, stateMachine));
        stateMachine.RegisterState(PredatorStateId.Search, new PredatorSearchState(this, stateMachine));
        stateMachine.RegisterState(PredatorStateId.Chase, new PredatorChaseState(this, stateMachine));
        stateMachine.RegisterState(PredatorStateId.Attack, new PredatorAttackState(this, stateMachine));
        stateMachine.ChangeState(PredatorStateId.Patrol);
    }

    private void Start()
    {
        FSMAgent hunter = perception.Hunter != null ? perception.Hunter : FindFirstObjectByType<FSMAgent>();
        float hunterMaxSpeed = hunter != null
            ? hunter.BaseSpeed * hunter.PursuitSpeedMultiplier
            : float.PositiveInfinity;
        movement.Configure(patrolSpeed, chaseSpeed, turnSpeed,
            hunterMaxSpeed * speedBelowHunterRatio);
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

        if (perception != null && perception.CanSeeHunter)
        {
            CurrentTarget = perception.Hunter;
            if (perception.NewlyDetected)
            {
                if (alertAudioSource != null)
                    alertAudioSource.PlayOneShot(alertHowl != null ? alertHowl : GetGeneratedAlertClip());
                PredatorAlertSystem.Broadcast(this, CurrentTarget, perception.LastSeenPosition);
            }
            if (CurrentState != PredatorStateId.Attack)
                stateMachine.ChangeState(PredatorStateId.Chase);
        }

        if (CurrentTarget != null &&
            (CurrentTarget.IsDead || SafeZoneVolume.IsPositionSafe(CurrentTarget.transform.position)))
        {
            ClearTargetAndReturnToPatrol();
        }

        stateMachine?.Update();
    }

    private void ReceiveAlert(PredatorFSM source, FSMAgent target, Vector3 lastKnownPosition)
    {
        if (source == this || target == null || target.IsDead ||
            SafeZoneVolume.IsPositionSafe(target.transform.position)) return;
        CurrentTarget = target;
        perception.ReceiveAlert(target, lastKnownPosition);
        if (!perception.CanSeeHunter) stateMachine.ChangeState(PredatorStateId.Search);
    }

    public bool CanStartAttack()
    {
        return CurrentTarget != null && !CurrentTarget.IsDead && combat != null &&
            combat.IsAttackReady && HorizontalDistanceTo(CurrentTarget.transform.position) <= captureRadius;
    }

    public float HorizontalDistanceTo(Vector3 position)
    {
        return Vector3.ProjectOnPlane(position - transform.position, Vector3.up).magnitude;
    }

    public void ClearTargetAndReturnToPatrol()
    {
        CurrentTarget = null;
        perception.ClearTarget();
        movement.InvalidatePath();
        stateMachine?.ChangeState(PredatorStateId.Patrol);
    }

    public bool TryReachLastKnownPosition()
    {
        if (!HasLastKnownPosition) return false;
        return movement.MoveTowards(LastKnownPosition, true, waypointCheckDistance);
    }

    private void OnValidate()
    {
        patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
        chaseSpeed = Mathf.Max(0.1f, chaseSpeed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        waypointCheckDistance = Mathf.Max(0.1f, waypointCheckDistance);
        captureRadius = Mathf.Max(0.1f, captureRadius);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.1f, 0.99f);
        SphereCollider trigger = GetComponent<SphereCollider>();
        PredatorPerception sensor = GetComponent<PredatorPerception>();
        if (trigger != null && sensor != null)
        {
            trigger.isTrigger = true;
            trigger.radius = sensor.ViewDistance;
        }
    }

    private static AudioClip GetGeneratedAlertClip()
    {
        if (generatedAlertClip != null) return generatedAlertClip;
        const int sampleRate = 22050;
        const float duration = 1.4f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Sin(Mathf.PI * i / (sampleCount - 1));
            float phase = 2f * Mathf.PI * (520f * time - 72f * time * time +
                14f * Mathf.Sin(2f * Mathf.PI * 3f * time));
            samples[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2.01f)) *
                envelope * envelope * 0.3f;
        }
        generatedAlertClip = AudioClip.Create("Predator Alert", sampleCount, 1, sampleRate, false);
        generatedAlertClip.SetData(samples, 0);
        return generatedAlertClip;
    }
}

public enum PredatorStateId
{
    Patrol,
    Search,
    Chase,
    Attack
}
