using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorAnimationController : MonoBehaviour
{
    private static readonly int AttackParameter = Animator.StringToHash("pAttack");
    private static readonly int VerticalBlendParameter = Animator.StringToHash("Y");
    private static readonly int BlendParameter = Animator.StringToHash("Blend");

    [SerializeField, Min(0.1f)] private float attackDurationFallback = 1.5f;

    private Animator animator;
    private bool hasAttackParameter;
    private bool hasVerticalBlendParameter;
    private bool hasBlendParameter;

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

    public void SetMoving(bool moving)
    {
        if (animator == null) return;
        if (hasAttackParameter && animator.GetBool(AttackParameter)) return;

        float movementBlend = moving ? 1f : 0f;
        if (hasVerticalBlendParameter)
            animator.SetFloat(VerticalBlendParameter, movementBlend);
        else if (hasBlendParameter)
            animator.SetFloat(BlendParameter, movementBlend);
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

    private void InitializeAnimator()
    {
        hasAttackParameter = false;
        hasVerticalBlendParameter = false;
        hasBlendParameter = false;
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
        }

        if (hasAttackParameter) animator.SetBool(AttackParameter, false);
        SetMoving(false);
    }

    private void OnValidate()
    {
        attackDurationFallback = Mathf.Max(0.1f, attackDurationFallback);
    }
}
