using UnityEngine;

[RequireComponent(typeof(BoidSensor), typeof(EmergencySeparationBehaviour))]
public class BoidGizmos : MonoBehaviour
{
    [SerializeField] private bool drawDetectionRadii = true;
    [SerializeField] private bool drawDetectedTargets = true;

    private BoidSensor sensor;
    private EmergencySeparationBehaviour emergencySeparation;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    private void OnDrawGizmosSelected()
    {
        CacheComponents();

        if (sensor == null || emergencySeparation == null)
        {
            return;
        }

        if (drawDetectionRadii)
        {
            DrawDetectionRadii();
        }

        if (drawDetectedTargets)
        {
            DrawDetectedTargets();
        }
    }

    private void DrawDetectionRadii()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sensor.PerceptionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sensor.SeparationRadius);

        Gizmos.color = new Color(0.65f, 0f, 0f);
        Gizmos.DrawWireSphere(transform.position, emergencySeparation.EmergencyRadius);

        Gizmos.color = new Color(1f, 0.35f, 0f);
        Gizmos.DrawWireSphere(transform.position, sensor.ThreatDetectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, sensor.InterestDetectionRadius);
    }

    private void DrawDetectedTargets()
    {
        Gizmos.color = Color.cyan;
        foreach (BoidAgent neighbor in sensor.Neighbors)
        {
            if (neighbor != null)
            {
                Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }

        if (sensor.CurrentThreat != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, sensor.CurrentThreat.transform.position);
        }

        if (sensor.CurrentInterest != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, sensor.CurrentInterest.transform.position);
        }
    }

    private void CacheComponents()
    {
        if (sensor == null)
        {
            sensor = GetComponent<BoidSensor>();
        }

        if (emergencySeparation == null)
        {
            emergencySeparation = GetComponent<EmergencySeparationBehaviour>();
        }
    }
}
