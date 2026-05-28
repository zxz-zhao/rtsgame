using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD 浮动 delta 文字：向上飘 + 渐隐 + 缩放，适用于资源 +/- 反馈。</summary>
internal class _HudFloatingDelta : MonoBehaviour
{
    float life;
    float t;
    float floatDistance;
    Vector2 startAnchored;
    RectTransform rt;
    Text text;
    Color startColor;

    public void Init(float lifeSeconds, float floatPixels)
    {
        life = Mathf.Max(0.1f, lifeSeconds);
        floatDistance = floatPixels;
        rt = GetComponent<RectTransform>();
        text = GetComponent<Text>();
        if (rt != null) startAnchored = rt.anchoredPosition;
        if (text != null) startColor = text.color;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float r = Mathf.Clamp01(t / life);
        // 缓出曲线
        float ease = 1f - Mathf.Pow(1f - r, 2.4f);
        if (rt != null)
            rt.anchoredPosition = startAnchored + new Vector2(0f, floatDistance * ease);
        if (text != null)
        {
            // 0~0.18 由小到大，0.18~1 渐隐
            float scaleR = Mathf.Clamp01(r / 0.18f);
            float scale = Mathf.Lerp(0.6f, 1.15f, scaleR);
            transform.localScale = Vector3.one * scale;
            var c = startColor;
            c.a = startColor.a * (1f - r);
            text.color = c;
        }
        if (r >= 1f) Destroy(gameObject);
    }
}
