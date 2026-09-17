using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HunterMovement), typeof(HunterPerception), typeof(HunterInterestSpawner))]
[RequireComponent(typeof(AlienVisualGrounding))]
public class FSMAgent : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float speed = 3f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Min(0.1f)] private float waypointCheckDistance = 0.5f;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float TBA = 4f;
    [SerializeField, Min(0.1f)] private float RangeAttackRadius = 8f;
    [SerializeField, Min(0.1f)] private float MeleeAttackRadius = 2.2f;
    [SerializeField, Min(0.01f)] private float meleeDamage = 35f;
    [SerializeField, Min(0.01f)] private float rangedDamage = 35f;
    [SerializeField, Min(0.1f)] private float rangedProjectileSpeed = 30f;
    [SerializeField, Min(0.1f)] private float rangedProjectileLifetime = 3f;
    [SerializeField] private float rangedProjectileSpawnHeight = 1.35f;
    [SerializeField, Min(0f)] private float rangedAttackWindup = 0.12f;
    [SerializeField, Min(0f)] private float attackWindup = 0.25f;
    [SerializeField, Min(0.1f)] private float attackAnimationDuration = 0.85f;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 2f;

    [Header("Gather")]
    [SerializeField, Min(0.1f)] private float collectionDistance = 1.4f;
    [SerializeField, Min(0.1f)] private float collectionDuration = 2f;

    // Kept only to migrate the data serialized by the original implementation.
    [SerializeField] private PatrolData dataPatrol;

    private StateMachine stateMachine;
    private HunterMovement movement;
    private HunterPerception perception;
    private HunterInterestSpawner interestSpawner;
    private bool hasRunParameter;
    private bool hasAttackParameter;
    private float attackCooldownRemaining;
    private bool requiresMeleeFollowUp;

    public Animator Animator => animator;
    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointCheckDistance => waypointCheckDistance;
    public HunterMovement Movement => movement;
    public HunterPerception Perception => perception;
    public HunterInterestSpawner InterestSpawner => interestSpawner;
    public float TimeBetweenAttacks => TBA;
    public float RangedAttackRadius => RangeAttackRadius;
    public float MeleeRadius => MeleeAttackRadius;
    public float AttackWindup => attackWindup;
    public float RangedAttackWindup => rangedAttackWindup;
    public float AttackAnimationDuration => attackAnimationDuration;
    public float PursuitSpeedMultiplier => pursuitSpeedMultiplier;
    public float CollectionDistance => collectionDistance;
    public float CollectionDuration => collectionDuration;
    public float AttackCooldownRemaining => attackCooldownRemaining;
    public bool IsAttackReady => attackCooldownRemaining <= 0f;
    public bool RequiresMeleeFollowUp => requiresMeleeFollowUp;
    public BoidLife CurrentTarget { get; private set; }
    public PoliceState CurrentState => stateMachine?.CurrentStateKey is PoliceState state
        ? state
        : PoliceState.idle;

    private void Awake()
    {
        MigratePatrolData();
        movement = GetComponent<HunterMovement>();
        if (movement == null)
        {
            movement = gameObject.AddComponent<HunterMovement>();
        }

        perception = GetComponent<HunterPerception>();
        if (perception == null)
        {
            perception = gameObject.AddComponent<HunterPerception>();
        }
        interestSpawner = GetComponent<HunterInterestSpawner>();
        if (interestSpawner == null)
        {
            interestSpawner = gameObject.AddComponent<HunterInterestSpawner>();
        }
        movement.Configure(speed, turnSpeed);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            CacheAnimatorParameters();
        }

        stateMachine = new StateMachine();
        stateMachine.RegisterState(PoliceState.idle, new IdleState(stateMachine));
        stateMachine.RegisterState(PoliceState.Patrol, new PatrolState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Pursuit, new PursuitState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Attack, new AttackState(this, stateMachine));
        stateMachine.RegisterState(PoliceState.Gather, new GatherState(this, stateMachine));
        stateMachine.ChangeState(PoliceState.Patrol);
    }

    private void Update()
    {
        // Perception is refreshed before evaluating transitions so the FSM
        // always makes decisions from the current frame's local sensor data.
        perception?.RefreshDetections();
        attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - Time.deltaTime);
        stateMachine?.Update();
    }

    private void OnDisable()
    {
        stateMachine?.Stop();
        movement?.Stop();
    }

    public void ChangeState(PoliceState state)
    {
        stateMachine?.ChangeState(state);
    }

    public void SetMovingAnimation(bool isMoving)
    {
        if (animator == null)
        {
            return;
        }

        if (hasRunParameter)
        {
            bool wasMoving = animator.GetBool("Run");
            animator.SetBool("Run", isMoving);
            if (isMoving && !wasMoving)
            {
                animator.CrossFade("Mutant Run", 0.08f);
            }
        }

        if (hasAttackParameter && isMoving)
        {
            animator.SetBool("isAttack", false);
        }
    }

    public void SetAttackAnimation(bool isAttacking)
    {
        if (animator != null && hasAttackParameter)
        {
            bool wasAttacking = animator.GetBool("isAttack");
            animator.SetBool("isAttack", isAttacking);
            if (isAttacking && !wasAttacking)
            {
                animator.CrossFade("Attack", 0.08f);
            }
        }
    }

    public bool CanStartAttack()
    {
        return IsAttackReady && perception != null && perception.ClosestLivingBoid != null;
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
            requiresMeleeFollowUp = false;
        }
    }

    public bool PerformAttack(BoidLife target, bool useMeleeAttack)
    {
        if (!IsAttackReady || target == null || !target.IsAlive)
        {
            return false;
        }

        target.TakeDamage(useMeleeAttack ? meleeDamage : rangedDamage);
        attackCooldownRemaining = TBA;
        if (useMeleeAttack)
        {
            requiresMeleeFollowUp = false;
        }
        return true;
    }

    public bool PerformRangedAttack(BoidLife target)
    {
        if (!IsAttackReady || target == null || !target.IsAlive)
        {
            return false;
        }

        Vector3 spawnPosition = transform.position +
            transform.forward * 0.65f +
            Vector3.up * rangedProjectileSpawnHeight;
        HunterProjectile.Create(
            spawnPosition,
            target,
            rangedDamage,
            rangedProjectileSpeed,
            rangedProjectileLifetime);
        attackCooldownRemaining = TBA;
        requiresMeleeFollowUp = true;
        return true;
    }

    public bool IsAttackAnimationFinished(float elapsedTime, ref bool attackStateObserved)
    {
        if (animator == null || !hasAttackParameter)
        {
            return elapsedTime >= attackAnimationDuration;
        }

        bool isTransitioning = animator.IsInTransition(0);
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextState = isTransitioning
            ? animator.GetNextAnimatorStateInfo(0)
            : default;
        bool currentIsAttack = currentState.IsName("Attack");
        bool nextIsAttack = isTransitioning && nextState.IsName("Attack");

        attackStateObserved |= currentIsAttack || nextIsAttack;
        if (attackStateObserved && currentIsAttack && !isTransitioning)
        {
            return currentState.normalizedTime >= 0.98f;
        }

        // Prevent a controller configuration error from trapping the FSM
        // forever, while still allowing enough time for the 79-frame clip.
        return elapsedTime >= Mathf.Max(attackAnimationDuration, 3.25f);
    }

    private void MigratePatrolData()
    {
        if ((waypoints == null || waypoints.Count == 0) && dataPatrol?.wayPoints != null)
        {
            waypoints = dataPatrol.wayPoints;
        }

        waypoints ??= new List<Transform>();
    }

    private void CacheAnimatorParameters()
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasRunParameter |= parameter.name == "Run" && parameter.type == AnimatorControllerParameterType.Bool;
            hasAttackParameter |= parameter.name == "isAttack" && parameter.type == AnimatorControllerParameterType.Bool;
        }
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        waypointCheckDistance = Mathf.Max(0.1f, waypointCheckDistance);
        TBA = Mathf.Max(0.1f, TBA);
        MeleeAttackRadius = Mathf.Max(0.1f, MeleeAttackRadius);
        RangeAttackRadius = Mathf.Max(MeleeAttackRadius, RangeAttackRadius);
        meleeDamage = Mathf.Max(0.01f, meleeDamage);
        rangedDamage = Mathf.Max(0.01f, rangedDamage);
        rangedProjectileSpeed = Mathf.Max(0.1f, rangedProjectileSpeed);
        rangedProjectileLifetime = Mathf.Max(0.1f, rangedProjectileLifetime);
        rangedAttackWindup = Mathf.Max(0f, rangedAttackWindup);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackAnimationDuration = Mathf.Max(attackWindup, attackAnimationDuration);
        pursuitSpeedMultiplier = Mathf.Max(1f, pursuitSpeedMultiplier);
        collectionDistance = Mathf.Max(0.1f, collectionDistance);
        collectionDuration = Mathf.Max(0.1f, collectionDuration);
    }
}

[System.Serializable]
public class PatrolData
{
    public List<Transform> wayPoints;
    public Transform transform;
    public float wayPointCheckDistance = 0.1f;
}
