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

        if (cachedRenderer != null && cachedRenderer.material != null && cachedRenderer.material.HasProperty("_Color"))
        {
            Color color = cachedRenderer.material.color;
            color.a = Mathf.Lerp(0.9f, 0f, t);
            cachedRenderer.material.color = color;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
