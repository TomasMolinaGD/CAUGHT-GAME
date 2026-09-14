using System;
using UnityEngine;

public enum BoidLifeState
{
    Alive,
    Dead,
    Respawning
}

public class BoidLife : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maximumLife = 100f;
    [SerializeField] private float currentLife;
    [SerializeField] private BoidLifeState state = BoidLifeState.Alive;

    public float CurrentLife => currentLife;
    public float MaximumLife => maximumLife;
    public BoidLifeState State => state;
    public bool IsAlive => state == BoidLifeState.Alive;
    public bool CanBeCollected => state == BoidLifeState.Dead;

    public event Action<BoidLife> Died;
    public event Action<BoidLife> Collected;
    public event Action<BoidLife> Respawned;

    private void Awake()
    {
        ResetLife();
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        currentLife = Mathf.Max(0f, currentLife - amount);
        if (currentLife <= 0f)
        {
            Die();
        }
    }

    public void Kill()
    {
        if (!IsAlive)
        {
            return;
        }

        currentLife = 0f;
        Die();
    }

    public bool Collect()
    {
        if (!CanBeCollected)
        {
            return false;
        }

        state = BoidLifeState.Respawning;
        Collected?.Invoke(this);
        return true;
    }

    internal void Respawn()
    {
        ResetLife();
        Respawned?.Invoke(this);
    }

    private void Die()
    {
        state = BoidLifeState.Dead;
        Died?.Invoke(this);
    }

    private void ResetLife()
    {
        currentLife = maximumLife;
        state = BoidLifeState.Alive;
    }

    [ContextMenu("Debug/Kill")]
    private void DebugKill()
    {
        Kill();
    }

    [ContextMenu("Debug/Collect")]
    private void DebugCollect()
    {
        Collect();
    }

    private void OnValidate()
    {
        maximumLife = Mathf.Max(1f, maximumLife);

        if (!Application.isPlaying)
        {
            ResetLife();
        }
    }
}
