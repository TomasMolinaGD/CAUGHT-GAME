using System.Collections.Generic;
using UnityEngine;

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

    private StateMachine stateMachine;
    private HunterMovement movement;
    private HunterPerception perception;
    private HunterInterestSpawner interestSpawner;
    private HunterCombat combat;
    private HunterAnimationController animationController;

    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointCheckDistance => waypointCheckDistance;
    public HunterMovement Movement => movement;
    public HunterPerception Perception => perception;
    public HunterInterestSpawner InterestSpawner => interestSpawner;
    public HunterCombat Combat => combat;
    public HunterAnimationController Animation => animationController;
    public float PursuitSpeedMultiplier => pursuitSpeedMultiplier;
    public float CollectionDistance => collectionDistance;
    public float CollectionDuration => collectionDuration;
    public BoidLife CurrentTarget { get; private set; }
    public PoliceState CurrentState => stateMachine?.CurrentStateKey is PoliceState state
        ? state
        : PoliceState.Patrol;

    private void Awake()
    {
        CacheDependencies();
        ConfigureComponents();
        InitializeStateMachine();
    }

    private void Update()
    {
        TickAgentSystems();
        stateMachine?.Update();
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
        stateMachine.RegisterState(PoliceState.Pursuit, new PursuitState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Attack, new AttackState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Gather, new GatherState(this, stateMachine));
        stateMachine.ChangeState(PoliceState.Patrol);
    }

    private void TickAgentSystems()
    {
        // Perception is refreshed before evaluating transitions so the FSM
        // always makes decisions from the current frame's local sensor data.
        perception?.RefreshDetections();
        combat?.Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        stateMachine?.Stop();
        movement?.Stop();
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

    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        waypointCheckDistance = Mathf.Max(0.1f, waypointCheckDistance);
        pursuitSpeedMultiplier = Mathf.Max(1f, pursuitSpeedMultiplier);
        collectionDistance = Mathf.Max(0.1f, collectionDistance);
        collectionDuration = Mathf.Max(0.1f, collectionDuration);
    }
}
