using UnityEngine;

[DisallowMultipleComponent]
public sealed class HunterAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.1f)] private float attackAnimationDuration = 0.85f;

    private bool hasRunParameter;
    private bool hasAttackParameter;

    public Animator Animator => animator;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        InitializeAnimator();
    }

    public void ConfigureAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        InitializeAnimator();
    }

    private void InitializeAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        CacheParameters();
    }

    public void SetMoving(bool isMoving)
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

    public void SetAttacking(bool isAttacking)
    {
        if (animator == null || !hasAttackParameter)
        {
            return;
        }

        bool wasAttacking = animator.GetBool("isAttack");
        animator.SetBool("isAttack", isAttacking);
        if (isAttacking && !wasAttacking)
        {
            animator.CrossFade("Attack", 0.08f);
        }
    }

    public bool IsAttackFinished(float elapsedTime, ref bool attackStateObserved)
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

        return elapsedTime >= Mathf.Max(attackAnimationDuration, 3.25f);
    }

    private void CacheParameters()
    {
        hasRunParameter = false;
        hasAttackParameter = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasRunParameter |= parameter.name == "Run" &&
                parameter.type == AnimatorControllerParameterType.Bool;
            hasAttackParameter |= parameter.name == "isAttack" &&
                parameter.type == AnimatorControllerParameterType.Bool;
        }
    }

    private void OnValidate()
    {
        attackAnimationDuration = Mathf.Max(0.1f, attackAnimationDuration);
    }
}
