using UnityEngine;

public class ProjectileImpactFx : MonoBehaviour
{
    float life;
    Vector3 startScale;
    Renderer cachedRenderer;

    void Start()
    {
        startScale = transform.localScale;
        cachedRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        life += Time.deltaTime;
        float t = Mathf.Clamp01(life / 0.22f);
        transform.localScale = startScale * Mathf.Lerp(0.65f, 1.8f, t);

        if (RendererColorUtil.TryGetColor(cachedRenderer, out Color color))
        {
            color.a = Mathf.Lerp(0.9f, 0f, t);
            RendererColorUtil.TrySetColor(cachedRenderer, color);
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
