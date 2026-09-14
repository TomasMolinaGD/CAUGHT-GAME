using UnityEngine;

[RequireComponent(typeof(BoidSensor))]
public sealed class BoidInteraction : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float interactionRadius = 3.5f;
    [SerializeField, Min(0.01f)] private float damagePerInteraction = 1f;
    [SerializeField, Min(0.05f)] private float interactionInterval = 1f;

    private BoidSensor sensor;
    private BoidInterest trackedInterest;
    private float interactionTimer;

    private void Awake()
    {
        sensor = GetComponent<BoidSensor>();
    }

    private void FixedUpdate()
    {
        BoidInterest interest = sensor.CurrentInterest;
        if (interest == null)
        {
            ResetInteraction();
            return;
        }

        if (interest != trackedInterest)
        {
            trackedInterest = interest;
            interactionTimer = 0f;
        }

        Vector3 offset = interest.transform.position - transform.position;
        offset = Vector3.ProjectOnPlane(offset, Vector3.up);

        if (offset.sqrMagnitude > interactionRadius * interactionRadius)
        {
            interactionTimer = 0f;
            return;
        }

        if (!interest.TryGetComponent(out InterestLife life) || !life.IsAlive)
        {
            ResetInteraction();
            return;
        }

        interactionTimer += Time.fixedDeltaTime;
        if (interactionTimer < interactionInterval)
        {
            return;
        }

        interactionTimer -= interactionInterval;
        life.TakeDamage(damagePerInteraction);
    }

    private void ResetInteraction()
    {
        trackedInterest = null;
        interactionTimer = 0f;
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.1f, interactionRadius);
        damagePerInteraction = Mathf.Max(0.01f, damagePerInteraction);
        interactionInterval = Mathf.Max(0.05f, interactionInterval);
    }
}
