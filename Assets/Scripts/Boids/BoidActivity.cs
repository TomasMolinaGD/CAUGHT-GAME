using UnityEngine;

[RequireComponent(typeof(BoidLife), typeof(BoidAgent), typeof(BoidSensor))]
public class BoidActivity : MonoBehaviour
{
    private BoidLife life;
    private BoidAgent agent;
    private BoidSensor sensor;
    private BoidInteraction interaction;
    private BoidAnimation boidAnimation;
    private Animator animator;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool[] rendererEnabledStates;
    private bool[] colliderEnabledStates;

    private void Awake()
    {
        life = GetComponent<BoidLife>();
        agent = GetComponent<BoidAgent>();
        sensor = GetComponent<BoidSensor>();
        interaction = GetComponent<BoidInteraction>();
        boidAnimation = GetComponent<BoidAnimation>();
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rendererEnabledStates = CaptureEnabledStates(renderers);
        colliderEnabledStates = CaptureEnabledStates(colliders);

        life.Died += HandleDeath;
        life.Collected += HandleCollection;
        life.Respawned += HandleRespawn;
    }

    private void OnDestroy()
    {
        life.Died -= HandleDeath;
        life.Collected -= HandleCollection;
        life.Respawned -= HandleRespawn;
    }

    private void HandleDeath(BoidLife _)
    {
        SetAutonomousSystemsEnabled(false);

        if (animator != null)
        {
            animator.speed = 0f;
        }
    }

    private void HandleCollection(BoidLife _)
    {
        SetVisibleAndCollidable(false);
    }

    private void HandleRespawn(BoidLife _)
    {
        SetVisibleAndCollidable(true);
        SetAutonomousSystemsEnabled(true);

        if (animator != null)
        {
            animator.speed = 1f;
        }
    }

    private void SetAutonomousSystemsEnabled(bool value)
    {
        sensor.enabled = value;
        agent.enabled = value;

        if (interaction != null)
        {
            interaction.enabled = value;
        }

        if (boidAnimation != null)
        {
            boidAnimation.enabled = value;
        }
    }

    private void SetVisibleAndCollidable(bool value)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = value && rendererEnabledStates[i];
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = value && colliderEnabledStates[i];
        }
    }

    private static bool[] CaptureEnabledStates(Renderer[] components)
    {
        bool[] states = new bool[components.Length];
        for (int i = 0; i < components.Length; i++)
        {
            states[i] = components[i].enabled;
        }

        return states;
    }

    private static bool[] CaptureEnabledStates(Collider[] components)
    {
        bool[] states = new bool[components.Length];
        for (int i = 0; i < components.Length; i++)
        {
            states[i] = components[i].enabled;
        }

        return states;
    }
}
