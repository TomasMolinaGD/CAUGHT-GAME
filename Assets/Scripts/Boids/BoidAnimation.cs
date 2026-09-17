using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public sealed class BoidAnimation : MonoBehaviour
{
    [SerializeField] private string speedParameter = "Blend";
    [SerializeField, Min(0f)] private float damping = 0.15f;
    [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.05f;
    [SerializeField, Min(0.01f)] private float fullWalkSpeed = 1f;
    [SerializeField, Min(0.1f)] private float minimumWalkingPlaybackSpeed = 0.9f;
    [SerializeField, Min(0.1f)] private float playbackSpeedAtMaximumMovement = 2.25f;

    private BoidAgent owner;
    private Animator animator;
    private int speedParameterHash;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        animator = GetComponentInChildren<Animator>();
        speedParameterHash = Animator.StringToHash(speedParameter);

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        float movementSpeed = owner.ActualVelocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(movementSpeed / owner.MaxSpeed);
        float walkBlend = Mathf.InverseLerp(
            idleSpeedThreshold,
            fullWalkSpeed,
            movementSpeed);

        animator.SetFloat(
            speedParameterHash,
            walkBlend,
            damping,
            Time.deltaTime);

        animator.speed = movementSpeed <= idleSpeedThreshold
            ? 1f
            : Mathf.Lerp(
                minimumWalkingPlaybackSpeed,
                playbackSpeedAtMaximumMovement,
                normalizedSpeed);
    }

    private void OnValidate()
    {
        damping = Mathf.Max(0f, damping);
        idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
        fullWalkSpeed = Mathf.Max(idleSpeedThreshold + 0.01f, fullWalkSpeed);
        minimumWalkingPlaybackSpeed = Mathf.Max(0.1f, minimumWalkingPlaybackSpeed);
        playbackSpeedAtMaximumMovement = Mathf.Max(
            minimumWalkingPlaybackSpeed,
            playbackSpeedAtMaximumMovement);
    }
}
