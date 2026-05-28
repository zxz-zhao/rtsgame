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

    public void Initialize(Vector3 startPosition, Vector3 targetPosition, Color projectileTint,
        float projectileSpeed, float projectileArcHeight, float projectileImpactRadius)
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

        ApplyTint();
        EnsureTrail();
    }

    void Start()
    {
        if (!initialized)
            Initialize(transform.position, transform.position + transform.forward * 3f, DefaultTint, DefaultSpeed, DefaultArcHeight, DefaultImpactRadius);
    }

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
            transform.Rotate(Vector3.forward, 720f * Time.deltaTime, Space.Self);

        if (t >= 1f)
            StartCoroutine(ImpactAndDestroy());
    }

    IEnumerator ImpactAndDestroy()
    {
        initialized = false;
        // 优先使用 EffectsManager 资源（Cartoon FX / Particle Pack）
        var hitDir = (target - start).normalized;
        var type = ClassifyTypeByVisual();
        EffectsManager.PlayImpact(target, -hitDir, type);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;
        if (trail != null)
            trail.emitting = false;

        yield return new WaitForSeconds(0.35f);
        Destroy(gameObject);
    }

    void ApplyTint()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty("_Color"))
                    materials[i].color = Color.Lerp(materials[i].color, tint, 0.58f);
            }
        }
    }

    void EnsureTrail()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        trail.material = new Material(shader) { color = new Color(tint.r, tint.g, tint.b, 0.74f) };
        trail.time = 0.12f;
        trail.startWidth = Mathf.Clamp(impactRadius * 0.18f, 0.05f, 0.32f);
        trail.endWidth = 0f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    ProjectileType ClassifyTypeByVisual()
    {
        // 基于弹道形态简单判定：弧线高+爆炸半径大 → 火箭/榴弹；纯橙色长拖尾 → 火焰；否则子弹/炮弹
        if (arcHeight > 1.2f && impactRadius >= 0.6f) return ProjectileType.Rocket;
        if (impactRadius >= 0.55f) return ProjectileType.Shell;
        if (tint.r > 0.85f && tint.g < 0.55f && tint.b < 0.25f && trail != null && trail.time >= 0.15f)
            return ProjectileType.Fire;
        return ProjectileType.Bullet;
    }

    static Quaternion DirectionRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity;
        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

}
