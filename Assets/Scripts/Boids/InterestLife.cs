using UnityEngine;

public sealed class InterestLife : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maximumLife = 60f;
    [SerializeField] private float currentLife;

    public float CurrentLife => currentLife;
    public float MaximumLife => maximumLife;
    public bool IsAlive => currentLife > 0f;

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

        if (!IsAlive)
        {
            Destroy(gameObject);
        }
    }

    public void ResetLife()
    {
        currentLife = maximumLife;
    }

    private void OnValidate()
    {
        maximumLife = Mathf.Max(1f, maximumLife);

        if (!Application.isPlaying)
        {
            currentLife = maximumLife;
        }
    }
}
