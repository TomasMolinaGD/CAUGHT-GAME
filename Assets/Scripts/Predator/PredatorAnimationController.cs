using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorAnimationController : MonoBehaviour
{
    private static readonly int AttackParameter = Animator.StringToHash("pAttack");
    private static readonly int VerticalBlendParameter = Animator.StringToHash("Y");
    private static readonly int BlendParameter = Animator.StringToHash("Blend");
    private static readonly int LocomotionPlaybackParameter =
        Animator.StringToHash("LocomotionPlayback");

    [SerializeField, Min(0.1f)] private float attackDurationFallback = 1.5f;
    [SerializeField, Range(0.05f, 0.95f)] private float attackImpactNormalizedTime = 0.2f;
    [SerializeField, Min(0.01f)] private float locomotionReferenceSpeed = 5.4f;
    [SerializeField, Min(0f)] private float locomotionPlaybackDamping = 0.1f;
    [SerializeField, Min(0.01f)] private float minimumLocomotionPlayback = 0.35f;

    private Animator animator;
    private bool hasAttackParameter;
    private bool hasVerticalBlendParameter;
    private bool hasBlendParameter;
    private bool hasLocomotionPlaybackParameter;

    public Animator Animator => animator;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        InitializeAnimator();
    }

    public void ConfigureAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        InitializeAnimator();
    }

    public void ConfigureAttackDuration(float duration)
    {
        attackDurationFallback = Mathf.Max(0.1f, duration);
    }

    public void ConfigureAttackImpactNormalizedTime(float normalizedTime)
    {
        attackImpactNormalizedTime = Mathf.Clamp(normalizedTime, 0.05f, 0.95f);
    }

    public void SetMoving(bool moving)
    {
        SetLocomotion(moving, moving ? locomotionReferenceSpeed : 0f);
    }

    public void SetMovementVelocity(Vector3 velocity)
    {
        float horizontalSpeed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
        SetLocomotion(horizontalSpeed * horizontalSpeed > 0.0001f, horizontalSpeed);
    }

    private void SetLocomotion(bool moving, float horizontalSpeed)
    {
        if (animator == null) return;
        if (hasAttackParameter && animator.GetBool(AttackParameter)) return;

        float movementBlend = moving ? 1f : 0f;
        if (hasVerticalBlendParameter)
            animator.SetFloat(VerticalBlendParameter, movementBlend);
        else if (hasBlendParameter)
            animator.SetFloat(BlendParameter, movementBlend);

        if (!hasLocomotionPlaybackParameter) return;

        float playback = moving
            ? Mathf.Max(minimumLocomotionPlayback,
                horizontalSpeed / Mathf.Max(0.01f, locomotionReferenceSpeed))
            : 1f;
        animator.SetFloat(LocomotionPlaybackParameter, playback,
            locomotionPlaybackDamping, Time.deltaTime);
    }

    public void SetAttacking(bool attacking)
    {
        if (animator == null || !hasAttackParameter) return;
        if (attacking) SetMoving(false);
        animator.SetBool(AttackParameter, attacking);
        if (attacking)
        {
            animator.CrossFade("Attack", 0.08f, 0);
        }
    }

    public bool IsAttackFinished(float elapsedTime, ref bool attackStateObserved)
    {
        if (animator == null || !hasAttackParameter)
            return elapsedTime >= attackDurationFallback;

        bool transitioning = animator.IsInTransition(0);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool currentIsAttack = current.IsName("Attack");
        bool nextIsAttack = transitioning && animator.GetNextAnimatorStateInfo(0).IsName("Attack");
        attackStateObserved |= currentIsAttack || nextIsAttack;

        if (attackStateObserved && currentIsAttack && !transitioning)
            return current.normalizedTime >= 0.98f;

        return elapsedTime >= attackDurationFallback;
    }

    public bool IsAttackImpactReached(float elapsedTime, ref bool attackStateObserved)
    {
        if (animator == null || !hasAttackParameter)
            return elapsedTime >= attackDurationFallback * attackImpactNormalizedTime;

        bool transitioning = animator.IsInTransition(0);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = transitioning
            ? animator.GetNextAnimatorStateInfo(0)
            : default;
        bool currentIsAttack = current.IsName("Attack");
        bool nextIsAttack = transitioning && next.IsName("Attack");
        attackStateObserved |= currentIsAttack || nextIsAttack;

        if (currentIsAttack)
            return current.normalizedTime >= attackImpactNormalizedTime;
        if (nextIsAttack)
            return next.normalizedTime >= attackImpactNormalizedTime;

        return elapsedTime >= attackDurationFallback * attackImpactNormalizedTime;
    }

    private void InitializeAnimator()
    {
        hasAttackParameter = false;
        hasVerticalBlendParameter = false;
        hasBlendParameter = false;
        hasLocomotionPlaybackParameter = false;
        if (animator == null) return;

        animator.applyRootMotion = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == AttackParameter && parameter.type == AnimatorControllerParameterType.Bool)
                hasAttackParameter = true;
            else if (parameter.nameHash == VerticalBlendParameter && parameter.type == AnimatorControllerParameterType.Float)
                hasVerticalBlendParameter = true;
            else if (parameter.nameHash == BlendParameter && parameter.type == AnimatorControllerParameterType.Float)
                hasBlendParameter = true;
            else if (parameter.nameHash == LocomotionPlaybackParameter &&
                parameter.type == AnimatorControllerParameterType.Float)
                hasLocomotionPlaybackParameter = true;
        }

        if (hasAttackParameter) animator.SetBool(AttackParameter, false);
        if (hasLocomotionPlaybackParameter) animator.SetFloat(LocomotionPlaybackParameter, 1f);
        SetMoving(false);
    }

    private void OnValidate()
    {
        attackDurationFallback = Mathf.Max(0.1f, attackDurationFallback);
        attackImpactNormalizedTime = Mathf.Clamp(attackImpactNormalizedTime, 0.05f, 0.95f);
        locomotionReferenceSpeed = Mathf.Max(0.01f, locomotionReferenceSpeed);
        locomotionPlaybackDamping = Mathf.Max(0f, locomotionPlaybackDamping);
        minimumLocomotionPlayback = Mathf.Max(0.01f, minimumLocomotionPlayback);
    }
}
