using UnityEngine;

public sealed class HunterProjectile : MonoBehaviour
{
    private static readonly Color ProjectileColor = new Color(0.1f, 0.85f, 1f, 1f);

    private BoidLife target;
    private float damage;
    private float speed;
    private float lifetimeRemaining;
    private Material runtimeMaterial;

    public static HunterProjectile Create(
        Vector3 position,
        BoidLife target,
        float damage,
        float speed,
        float lifetime)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Hunter Ranged Projectile";
        projectileObject.transform.position = position;
        projectileObject.transform.localScale = Vector3.one * 0.28f;

        Collider primitiveCollider = projectileObject.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            Destroy(primitiveCollider);
        }

        HunterProjectile projectile = projectileObject.AddComponent<HunterProjectile>();
        projectile.Initialize(target, damage, speed, lifetime);
        return projectile;
    }

    private void Initialize(BoidLife newTarget, float newDamage, float newSpeed, float lifetime)
    {
        target = newTarget;
        damage = Mathf.Max(0f, newDamage);
        speed = Mathf.Max(0.1f, newSpeed);
        lifetimeRemaining = Mathf.Max(0.1f, lifetime);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            runtimeMaterial = new Material(shader) { color = ProjectileColor };
            Renderer projectileRenderer = GetComponent<Renderer>();
            if (projectileRenderer != null)
            {
                projectileRenderer.sharedMaterial = runtimeMaterial;
            }

            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = runtimeMaterial;
            trail.startColor = ProjectileColor;
            trail.endColor = new Color(ProjectileColor.r, ProjectileColor.g, ProjectileColor.b, 0f);
            trail.startWidth = 0.2f;
            trail.endWidth = 0.02f;
            trail.time = 0.25f;
        }

        Light glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = ProjectileColor;
        glow.range = 3f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        lifetimeRemaining -= Time.deltaTime;
        if (lifetimeRemaining <= 0f || target == null || !target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * 1.15f;
        Vector3 offset = targetPosition - transform.position;
        float travelDistance = speed * Time.deltaTime;
        if (offset.sqrMagnitude <= travelDistance * travelDistance)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        transform.position += offset.normalized * travelDistance;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}
