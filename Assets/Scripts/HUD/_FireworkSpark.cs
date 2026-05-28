using UnityEngine;
using UnityEngine.UI;

/// <summary>烟花火花：径向飞出 + 重力下坠 + 拖尾长方形 + 渐隐。</summary>
internal class _FireworkSpark : MonoBehaviour
{
    Vector2 velocity;
    Vector2 startPos;
    float life;
    float t;
    RectTransform rt;
    Image img;
    Color startColor;

    public void Init(Vector2 initialVelocity, float lifeSeconds)
    {
        velocity = initialVelocity;
        life = Mathf.Max(0.2f, lifeSeconds);
        rt = GetComponent<RectTransform>();
        img = GetComponent<Image>();
        if (rt != null) startPos = rt.anchoredPosition;
        if (img != null) startColor = img.color;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        // 重力 + 阻尼
        velocity.y -= 380f * Time.unscaledDeltaTime;       // 重力
        velocity *= (1f - 0.6f * Time.unscaledDeltaTime);  // 空气阻力
        if (rt != null) rt.anchoredPosition += velocity * Time.unscaledDeltaTime;
        if (img != null)
        {
            float r = Mathf.Clamp01(t / life);
            var c = startColor;
            c.a = startColor.a * (1f - r);
            img.color = c;
        }
        // 让火花朝运动方向旋转 + 拉成线状
        if (rt != null && velocity.sqrMagnitude > 1f)
        {
            float ang = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, ang);
            float speed = velocity.magnitude;
            float w = Mathf.Clamp(speed * 0.06f, 6f, 28f);
            rt.sizeDelta = new Vector2(w, 3.5f);
        }
        if (t >= life) Destroy(gameObject);
    }
}
