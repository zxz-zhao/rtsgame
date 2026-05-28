using UnityEngine;

/// <summary>
/// 战斗音效（程序化合成）：爆炸/枪声/命中。
/// 自动按相机距离做音量衰减，且每帧节流防止同帧大量触发刺耳。
/// </summary>
public class _CombatSfx : MonoBehaviour
{
    private static _CombatSfx _inst;
    private AudioSource _src;
    private AudioClip _bigBoom, _smallBoom, _shot, _hit;

    [Range(0f, 1f)] public float MasterVolume = 0.55f;
    public int MaxPerFrame = 4;
    public float MaxAudibleDistance = 90f;

    private int _budgetThisFrame;

    public static _CombatSfx I
    {
        get
        {
            if (_inst != null) return _inst;
            var go = new GameObject("_CombatSfx");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<_CombatSfx>();
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
        _src.volume = MasterVolume * GetVolumeScale();

        _bigBoom   = MakeBigExplosion();
        _smallBoom = MakeSmallExplosion();
        _shot      = MakeShot();
        _hit       = MakeHit();
    }

    void LateUpdate() { _budgetThisFrame = 0; }

    public static void PlayBigExplosion(Vector3 pos)   => I._Play(I._bigBoom,   pos, 0.95f, 1.0f);
    public static void PlaySmallExplosion(Vector3 pos) => I._Play(I._smallBoom, pos, 0.55f, 1.0f);
    public static void PlayShot(Vector3 pos)           => I._Play(I._shot,      pos, 0.30f, Random.Range(0.95f, 1.08f));
    public static void PlayHit(Vector3 pos)            => I._Play(I._hit,       pos, 0.40f, Random.Range(0.95f, 1.06f));

    void _Play(AudioClip clip, Vector3 worldPos, float baseVol, float pitch)
    {
        if (clip == null || _src == null) return;
        if (_budgetThisFrame >= MaxPerFrame) return;
        var cam = Camera.main;
        if (cam == null) return;
        float dist = Vector3.Distance(cam.transform.position, worldPos);
        if (dist > MaxAudibleDistance) return;
        float att = Mathf.Clamp01(1f - dist / MaxAudibleDistance);
        att = att * att; // 平方衰减更自然
        _src.pitch = pitch;
        _src.PlayOneShot(clip, baseVol * att * MasterVolume * GetVolumeScale());
        _budgetThisFrame++;
    }

    public static float GetVolumeScale()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat("sfx_vol", 1f));
    }

    public static void SetVolumeScale(float value)
    {
        float scale = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("sfx_vol", scale);
        if (_inst != null && _inst._src != null)
            _inst._src.volume = _inst.MasterVolume * scale;
    }

    // ── 合成 ─────────────────────────────────────────────────
    static AudioClip MakeBigExplosion()
    {
        const int sr = 22050;
        int n = sr * 800 / 1000;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            // 低频 sub bass 冲击 + 长衰减
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(80f, 30f, t / 0.8f) * t) * Mathf.Exp(-t * 2.2f);
            // 中频 boom（180→60Hz pitch down）
            float mid = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 60f, t / 0.4f) * t) * Mathf.Exp(-t * 4f);
            // 白噪声爆炸"shhh"
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 5.5f);
            d[i] = (sub * 0.55f + mid * 0.35f + noise * 0.45f) * 0.85f;
            d[i] = Mathf.Clamp(d[i], -0.95f, 0.95f);
        }
        var c = AudioClip.Create("bigBoom", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    static AudioClip MakeSmallExplosion()
    {
        const int sr = 22050;
        int n = sr * 350 / 1000;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float mid = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 90f, t / 0.35f) * t) * Mathf.Exp(-t * 6f);
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 9f);
            d[i] = (mid * 0.45f + noise * 0.40f) * 0.85f;
            d[i] = Mathf.Clamp(d[i], -0.9f, 0.9f);
        }
        var c = AudioClip.Create("smallBoom", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    static AudioClip MakeShot()
    {
        const int sr = 22050;
        int n = sr * 120 / 1000;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 32f);
            float bod = Mathf.Sin(2f * Mathf.PI * 800f * t) * Mathf.Exp(-t * 22f);
            d[i] = (crack * 0.55f + bod * 0.35f) * 0.8f;
        }
        var c = AudioClip.Create("shot", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }

    static AudioClip MakeHit()
    {
        const int sr = 22050;
        int n = sr * 100 / 1000;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float bod = Mathf.Sin(2f * Mathf.PI * 360f * t) * Mathf.Exp(-t * 25f);
            float tick = (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f);
            d[i] = (bod * 0.4f + tick * 0.45f) * 0.8f;
        }
        var c = AudioClip.Create("hit", n, 1, sr, false);
        c.SetData(d, 0); return c;
    }
}
