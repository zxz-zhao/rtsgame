using UnityEngine;

/// <summary>
/// UI 点击音效（程序化合成，无外部资源）。
/// 提供 4 种声音：
///  - PlayClick    短促"咔嗒"，普通按钮
///  - PlayConfirm  低沉"咚"，确认/建造下单
///  - PlayDeny     双音"嘟嘟"，禁用/钱不够
///  - PlayWarn     紧迫"嗡嗡"，警报
/// </summary>
public class _UiClickAudio : MonoBehaviour
{
    private static _UiClickAudio _inst;
    private const float BaseVolume = 0.55f;
    private AudioSource _src;
    private AudioClip _click, _confirm, _deny, _warn, _chime;

    static _UiClickAudio I
    {
        get
        {
            if (_inst != null) return _inst;
            var go = new GameObject("_UiClickAudio");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<_UiClickAudio>();
            return _inst;
        }
    }

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;
        _src.volume = BaseVolume * GetVolumeScale();
        // 预生成 4 种音效
        _click   = MakeClick();
        _confirm = MakeConfirm();
        _deny    = MakeDeny();
        _warn    = MakeWarn();
        _chime   = MakeChime();
    }

    public static void PlayClick()   => I._src?.PlayOneShot(I._click);
    public static void PlayConfirm() => I._src?.PlayOneShot(I._confirm);
    public static void PlayDeny()    => I._src?.PlayOneShot(I._deny);
    public static void PlayWarn()    => I._src?.PlayOneShot(I._warn);
    public static void PlayChime()   => I._src?.PlayOneShot(I._chime, 0.7f);

    public static float GetVolumeScale()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat("sfx_vol", 1f));
    }

    public static void SetVolumeScale(float value)
    {
        float scale = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("sfx_vol", scale);
        if (_inst != null && _inst._src != null)
            _inst._src.volume = BaseVolume * scale;
    }

    /// <summary>清脆"叮咚"两音（生产完成、建造完成等正面提示）。</summary>
    static AudioClip MakeChime()
    {
        const int sr = 22050;
        int n = sr * 380 / 1000;
        var d = new float[n];
        int sep = sr * 110 / 1000;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float t2 = (i - sep) / (float)sr;
            // 第一音 880Hz（A5）+ 第二音 1320Hz（E6），明亮和声
            float env1 = i < sep ? Mathf.Exp(-t * 5f) : Mathf.Exp(-(t - sep / (float)sr) * 4f);
            float env2 = i >= sep ? Mathf.Exp(-t2 * 4f) : 0f;
            float a = Mathf.Sin(2f * Mathf.PI * 880f * t) * env1 * 0.45f;
            float b = Mathf.Sin(2f * Mathf.PI * 1320f * t) * env2 * 0.40f;
            d[i] = (a + b);
        }
        var c = AudioClip.Create("chime", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    /// <summary>0.06 秒，高频 (1200Hz) 短衰减。</summary>
    static AudioClip MakeClick()
    {
        const int sr = 22050;
        int n = sr * 60 / 1000; // 60ms
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 50f);
            d[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * env * 0.45f;
        }
        var c = AudioClip.Create("click", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    /// <summary>0.18 秒，中低频 (220→160Hz pitch down) 确认感。</summary>
    static AudioClip MakeConfirm()
    {
        const int sr = 22050;
        int n = sr * 180 / 1000;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float r = t / 0.18f;
            float freq = Mathf.Lerp(220f, 160f, r);
            float env = Mathf.Exp(-t * 8f);
            d[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
        }
        var c = AudioClip.Create("confirm", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    /// <summary>双音 down-down 拒绝音。</summary>
    static AudioClip MakeDeny()
    {
        const int sr = 22050;
        int n = sr * 220 / 1000;
        var d = new float[n];
        int sep = sr * 60 / 1000; // 第二音延迟 60ms
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float t2 = (i - sep) / (float)sr;
            float env1 = i < sep ? Mathf.Exp(-t * 20f) : 0f;
            float env2 = i >= sep ? Mathf.Exp(-t2 * 20f) : 0f;
            float a = Mathf.Sin(2f * Mathf.PI * 320f * t) * env1;
            float b = Mathf.Sin(2f * Mathf.PI * 220f * t) * env2;
            d[i] = (a + b) * 0.45f;
        }
        var c = AudioClip.Create("deny", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    /// <summary>0.4 秒紧迫嗡嗡声（用于警报）。</summary>
    static AudioClip MakeWarn()
    {
        const int sr = 22050;
        int n = sr * 400 / 1000;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            // 振荡频率快速变化（警报感）
            float modFreq = 480f + 120f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f)); // 起伏包络
            d[i] = Mathf.Sin(2f * Mathf.PI * modFreq * t) * env * 0.45f;
        }
        var c = AudioClip.Create("warn", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }
}
