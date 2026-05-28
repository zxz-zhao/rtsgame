using UnityEngine;

/// <summary>选中圈呼吸/脉动效果：缩放在基础值上下浮动，颜色明度同步呼吸。</summary>
internal class _SelectionRingPulse : MonoBehaviour
{
    [Tooltip("脉动幅度（缩放系数 ±）")]
    public float Amplitude = 0.07f;
    [Tooltip("脉动频率")]
    public float Frequency = 2.4f;
    [Tooltip("颜色亮度幅度")]
    public float ColorAmplitude = 0.18f;

    Vector3 baseScale;
    Color baseColor;
    Renderer rd;
    bool initialized;

    // 瞬时选中脉动（SetSelected 调用）
    float _impulseTimer = 0f;
    const float ImpulseDuration = 0.55f;
    const float ImpulseScaleMax = 1.55f; // 选中瞬间放大到 1.55 倍

    void OnEnable()
    {
        // 每次启用都重新缓存（外部可能重设了基准缩放）
        baseScale = transform.localScale;
        rd = GetComponent<Renderer>();
        if (rd != null && rd.material != null)
            baseColor = rd.material.color;
        initialized = true;
    }

    /// <summary>外部调用：选中瞬间触发强力脉动 0.55 秒。</summary>
    public void TriggerSelectImpulse()
    {
        _impulseTimer = ImpulseDuration;
    }

    void Update()
    {
        if (!initialized) return;
        // 持续呼吸
        float s = Mathf.Sin(Time.time * Frequency * Mathf.PI * 2f);
        float breathK = 1f + s * Amplitude;
        // 选中瞬时脉动（线性衰减）
        float impulseK = 1f;
        if (_impulseTimer > 0f)
        {
            _impulseTimer -= Time.deltaTime;
            float r = Mathf.Clamp01(_impulseTimer / ImpulseDuration);
            // 用 r^0.4 让前期保持大、后期快速回落（视觉冲击感）
            impulseK = 1f + (ImpulseScaleMax - 1f) * Mathf.Pow(r, 0.4f);
        }
        float totalK = breathK * impulseK;
        // 整体三轴缩放：兼容 Cylinder（Y=0.01 极扁，缩放后视觉无差）和 Quad（X/Y 都是径向，必须同时缩）。
        transform.localScale = baseScale * totalK;
        if (rd != null && rd.material != null && rd.material.HasProperty("_Color"))
        {
            // 呼吸亮度 + 选中瞬间亮度 boost
            float bright = 1f + s * ColorAmplitude;
            if (_impulseTimer > 0f)
            {
                float r = Mathf.Clamp01(_impulseTimer / ImpulseDuration);
                bright += 0.45f * r;
            }
            var c = baseColor;
            c.r = Mathf.Clamp01(baseColor.r * bright);
            c.g = Mathf.Clamp01(baseColor.g * bright);
            c.b = Mathf.Clamp01(baseColor.b * bright);
            rd.material.color = c;
        }
    }
}
