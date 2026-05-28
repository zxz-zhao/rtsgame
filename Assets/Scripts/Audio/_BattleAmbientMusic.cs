using UnityEngine;

/// <summary>
/// 战场背景音乐：程序化合成低频 drone + 中频 pad 的循环 ambient。
/// 不需要任何外部音频文件。低音量（0.15）作为环境氛围。
/// </summary>
[DisallowMultipleComponent]
public class _BattleAmbientMusic : MonoBehaviour
{
    [Tooltip("整体音量（0-1），不要太大，以免遮盖战斗音效")]
    [Range(0f, 0.5f)] public float Volume = 0.18f;
    [Tooltip("循环长度（秒），越长越耗内存但越不容易察觉到循环")]
    public float LoopSeconds = 24f;

    private AudioSource _src;

    public static _BattleAmbientMusic CreateOnScene()
    {
        var existing = FindObjectOfType<_BattleAmbientMusic>();
        if (existing != null) return existing;
        var go = new GameObject("_BattleAmbientMusic");
        DontDestroyOnLoad(go);
        return go.AddComponent<_BattleAmbientMusic>();
    }

    void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = BuildAmbientClip(LoopSeconds);
        _src.loop = true;
        _src.volume = Volume;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f; // 2D
        _src.priority = 200;    // 低优先级
        _src.Play();
    }

    void Update()
    {
        if (_src != null) _src.volume = Volume;
    }

    /// <summary>合成简单的多层 drone + 慢 LFO 调制 + 远景"鼓点"脉冲。</summary>
    static AudioClip BuildAmbientClip(float seconds)
    {
        const int sampleRate = 22050; // 半采样率即可，节省内存
        int samples = Mathf.RoundToInt(seconds * sampleRate);
        var data = new float[samples];

        // 基本频率（低音 D2/A2/D3）
        const float f1 = 73.42f;   // D2
        const float f2 = 110.00f;  // A2
        const float f3 = 146.83f;  // D3
        const float f4 = 220.00f;  // A3 (pad)
        const float f5 = 293.66f;  // D4 (pad)

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            // 低频 drone（饱满感）
            float bass = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.22f
                       + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.18f
                       + Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.12f;
            // 中频 pad（紧张感）
            float pad = Mathf.Sin(2f * Mathf.PI * f4 * t) * 0.08f
                      + Mathf.Sin(2f * Mathf.PI * f5 * t) * 0.06f;
            // 慢呼吸 LFO（0.12 Hz）
            float breathe = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.12f * t);
            // 极慢宽度调制（让声音不死板）
            float wobble = 1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 0.07f * t + 1.7f);

            // 远处鼓点（每 2.5 秒一次，短促衰减）
            float drum = 0f;
            float beatPhase = (t % 2.5f);
            if (beatPhase < 0.18f)
            {
                float env = Mathf.Exp(-beatPhase * 14f);
                drum = Mathf.Sin(2f * Mathf.PI * 48f * t) * env * 0.18f;
            }

            // 极低强度白噪声（风声）
            float noise = (Mathf.PerlinNoise(t * 12f, 0.5f) - 0.5f) * 0.04f;

            float sample = (bass + pad) * breathe * wobble + drum + noise;
            // 软裁切防爆音
            data[i] = Mathf.Clamp(sample * 0.65f, -0.95f, 0.95f);
        }

        var clip = AudioClip.Create("_BattleAmbient", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
