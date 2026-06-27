using System.Collections;
using UnityEngine;

public class CombatProjectile : MonoBehaviour
{
    public float DefaultSpeed = 60f;
    public float DefaultArcHeight = 0f;
    public float DefaultImpactRadius = 0.55f;
    public Color DefaultTint = new Color(1f, 0.78f, 0.24f, 1f);
    public bool Spin = true;

    Vector3 start;
    Vector3 target;
    float speed;
    float arcHeight;
    float impactRadius;
    Color tint;
    float elapsed;
    float duration = 0.25f;
    bool initialized;
    TrailRenderer trail;
    ProjectileType projectileType;
    float spinSpeed = 720f;
    Vector3 baseScale = Vector3.one;

    /// <summary>
    /// Configures the projectile's trajectory, tint, and impact profile before flight begins.
    /// </summary>
    public void Initialize(Vector3 startPosition, Vector3 targetPosition, Color projectileTint,
        float projectileSpeed, float projectileArcHeight, float projectileImpactRadius)
    {
        Initialize(startPosition, targetPosition, projectileTint, projectileSpeed, projectileArcHeight, projectileImpactRadius, null);
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPosition, Color projectileTint,
        float projectileSpeed, float projectileArcHeight, float projectileImpactRadius, ProjectileType? explicitType)
    {
        start = startPosition;
        target = targetPosition;
        tint = projectileTint;
        speed = Mathf.Max(1f, projectileSpeed > 0f ? projectileSpeed : DefaultSpeed);
        arcHeight = projectileArcHeight;
        impactRadius = Mathf.Max(0.12f, projectileImpactRadius > 0f ? projectileImpactRadius : DefaultImpactRadius);

        transform.position = start;
        transform.rotation = DirectionRotation(target - start);
        duration = Mathf.Clamp(Vector3.Distance(start, target) / speed, 0.08f, 1.8f);
        initialized = true;
        projectileType = explicitType ?? ProjectileVisualProfile.ResolveType(gameObject.name, arcHeight, impactRadius, tint);

        ApplyTint();
        ConfigureFlightStyle();
        EnsureTrail();
    }

    /// <summary>
    /// Falls back to a short forward shot when the projectile is spawned without explicit initialization.
    /// </summary>
    void Start()
    {
        if (!initialized)
            Initialize(transform.position, transform.position + transform.forward * 3f, DefaultTint, DefaultSpeed, DefaultArcHeight, DefaultImpactRadius);
    }

    /// <summary>
    /// Advances the projectile along its lerped or arced path and triggers impact once travel completes.
    /// </summary>
    void Update()
    {
        if (!initialized) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Vector3 pos = Vector3.Lerp(start, target, t);
        if (arcHeight > 0f)
            pos += Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);

        Vector3 delta = pos - transform.position;
        transform.position = pos;
        if (delta.sqrMagnitude > 0.0001f)
            transform.rotation = DirectionRotation(delta);

        if (Spin)
        {
            switch (projectileType)
            {
                case ProjectileType.Bomb:
                    transform.Rotate(new Vector3(0.85f, 0.15f, 0.45f) * (spinSpeed * Time.deltaTime), Space.Self);
                    break;
                case ProjectileType.Shell:
                case ProjectileType.Bullet:
                    transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
                    break;
            }
        }

        if (t >= 1f)
            StartCoroutine(ImpactAndDestroy());
    }

    /// <summary>
    /// Plays the impact effect, hides the projectile visuals, then destroys the projectile after the trail expires.
    /// </summary>
    IEnumerator ImpactAndDestroy()
    {
        initialized = false;
        var hitDir = (target - start).normalized;
        EffectsManager.PlayImpact(target, -hitDir, projectileType, impactRadius);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;
        if (trail != null)
            trail.emitting = false;

        float cleanupDelay = trail != null
            ? Mathf.Clamp(trail.time + 0.04f, 0.08f, 0.24f)
            : 0.12f;
        yield return new WaitForSeconds(cleanupDelay);
        Destroy(gameObject);
    }

    void ConfigureFlightStyle()
    {
        baseScale = transform.localScale;
        switch (projectileType)
        {
            case ProjectileType.Bomb:
                Spin = true;
                spinSpeed = 240f;
                transform.localScale = baseScale * 1.18f;
                break;
            case ProjectileType.Shell:
                Spin = true;
                spinSpeed = 300f;
                transform.localScale = baseScale * 1.08f;
                break;
            case ProjectileType.Rocket:
                Spin = false;
                transform.localScale = baseScale * 1.12f;
                break;
            case ProjectileType.Fire:
                Spin = false;
                transform.localScale = baseScale * 1.14f;
                break;
            default:
                Spin = true;
                spinSpeed = 1200f;
                break;
        }
    }

    /// <summary>
    /// Blends the projectile tint into every renderer child so different ammo types remain visually distinct.
    /// </summary>
    void ApplyTint()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (RendererColorUtil.TryGetColor(materials[i], out Color baseColor))
                    RendererColorUtil.TrySetColor(materials[i], Color.Lerp(baseColor, tint, 0.58f));
            }
        }
    }

    /// <summary>
    /// Creates or updates the trail renderer to match the projectile's current visual profile.
    /// </summary>
    void EnsureTrail()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        trail.material = new Material(shader) { color = new Color(tint.r, tint.g, tint.b, 0.78f) };

        switch (projectileType)
        {
            case ProjectileType.Bomb:
                trail.time = Mathf.Clamp(0.18f + impactRadius * 0.05f, 0.18f, 0.34f);
                trail.startWidth = Mathf.Clamp(impactRadius * 0.24f, 0.14f, 0.78f);
                trail.startColor = new Color(0.32f, 0.32f, 0.32f, 0.7f);
                trail.endColor = new Color(0.08f, 0.08f, 0.08f, 0f);
                break;
            case ProjectileType.Shell:
                trail.time = Mathf.Clamp(0.10f + impactRadius * 0.04f, 0.10f, 0.22f);
                trail.startWidth = Mathf.Clamp(impactRadius * 0.18f, 0.07f, 0.46f);
                trail.startColor = new Color(1f, 0.72f, 0.24f, 0.82f);
                trail.endColor = new Color(0.26f, 0.24f, 0.22f, 0f);
                break;
            case ProjectileType.Rocket:
                trail.time = Mathf.Clamp(0.15f + impactRadius * 0.05f, 0.16f, 0.3f);
                trail.startWidth = Mathf.Clamp(impactRadius * 0.22f, 0.08f, 0.54f);
                trail.startColor = new Color(1f, 0.62f, 0.18f, 0.86f);
                trail.endColor = new Color(0.22f, 0.22f, 0.22f, 0f);
                break;
            case ProjectileType.Fire:
                trail.time = Mathf.Clamp(0.12f + impactRadius * 0.03f, 0.11f, 0.2f);
                trail.startWidth = Mathf.Clamp(impactRadius * 0.3f, 0.12f, 0.6f);
                trail.startColor = new Color(1f, 0.48f, 0.12f, 0.85f);
                trail.endColor = new Color(0.45f, 0.08f, 0.02f, 0f);
                break;
            default:
                trail.time = Mathf.Clamp(0.05f + impactRadius * 0.025f, 0.05f, 0.12f);
                trail.startWidth = Mathf.Clamp(impactRadius * 0.16f, 0.03f, 0.12f);
                trail.startColor = new Color(tint.r, tint.g, tint.b, 0.85f);
                trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
                break;
        }

        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    /// <summary>
    /// Infers a broad projectile type from trajectory and tint so impact effects can feel appropriate.
    /// </summary>
    ProjectileType ClassifyTypeByVisual()
    {
        return ProjectileVisualProfile.ResolveType(gameObject.name, arcHeight, impactRadius, tint);
    }

    /// <summary>
    /// Builds a forward-facing rotation from a travel direction, returning identity when the vector is too small.
    /// </summary>
    static Quaternion DirectionRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity;
        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
