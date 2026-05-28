using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 中央特效播放管理器。
/// 用法：
/// 1) 把第三方特效 prefab（Cartoon FX Free / Unity Particle Pack 等）放进
///    Assets/Resources/Effects/ 目录，命名为下面 ResKey 中的常量名。
/// 2) 单位死亡 / 弹道命中 / 开火 时调用 EffectsManager.PlayXxx。
/// 3) 资源缺失时自动 fallback 到内置程序化效果，不会报错。
/// </summary>
public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    public static class ResKey
    {
        // 死亡（按 PopCost / 体积分级）
        public const string DeathSmall  = "Effects/death_small";   // 步兵 / 1 人口
        public const string DeathMedium = "Effects/death_medium";  // 坦克 / 2-3 人口
        public const string DeathLarge  = "Effects/death_large";   // 重型 / 飞机 / 建筑
        // 命中
        public const string ImpactBullet = "Effects/impact_bullet";
        public const string ImpactShell  = "Effects/impact_shell";
        public const string ImpactRocket = "Effects/impact_rocket";
        public const string ImpactFire   = "Effects/impact_fire";
        // 开火
        public const string MuzzleFlash  = "Effects/muzzle_flash";
        // 建造完成 / 升级
        public const string BuildComplete = "Effects/build_complete";
        public const string LevelUp       = "Effects/level_up";
    }

    static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>(32);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static EffectsManager EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("EffectsManager");
            Instance = go.AddComponent<EffectsManager>();
        }
        return Instance;
    }

    static GameObject Load(string resKey)
    {
        if (string.IsNullOrEmpty(resKey)) return null;
        if (_cache.TryGetValue(resKey, out var cached)) return cached; // 允许 null 缓存
        var prefab = Resources.Load<GameObject>(resKey);
        _cache[resKey] = prefab;
        return prefab;
    }

    /// <summary>核心生成 API。pos 为世界坐标，rot 默认朝上。</summary>
    public static GameObject Spawn(string resKey, Vector3 pos, Quaternion rot, float lifeOverride = -1f)
    {
        EnsureInstance();
        var prefab = Load(resKey);
        if (prefab == null) return null;
        var inst = Instantiate(prefab, pos, rot);
        // 自动销毁：优先 ParticleSystem 主时长，否则按 override
        var ps = inst.GetComponentInChildren<ParticleSystem>(true);
        float life = lifeOverride;
        if (life <= 0f)
        {
            if (ps != null)
            {
                var main = ps.main;
                life = main.duration + main.startLifetime.constantMax + 0.4f;
            }
            else life = 2.5f;
        }
        Destroy(inst, life);
        return inst;
    }

    // ── 高层封装 ────────────────────────────────────

    public static void PlayDeath(Vector3 pos, int popCost = 1)
    {
        // 程序化爆炸音效（按距离衰减、每帧节流）
        if (popCost >= 4) _CombatSfx.PlayBigExplosion(pos);
        else _CombatSfx.PlaySmallExplosion(pos);
        // 地面焦痕（弹坑）
        SpawnScorchMark(pos, popCost >= 4 ? 3.2f : popCost >= 2 ? 2.2f : 1.4f);
        // 大型单位（坦克/重炮/轰炸机等 PopCost>=4）爆炸时震屏，按距离衰减
        if (popCost >= 4) TryShakeCamera(pos, 0.35f, 0.18f);
        else if (popCost >= 2) TryShakeCamera(pos, 0.18f, 0.12f);
        string key = popCost >= 4 ? ResKey.DeathLarge
                   : popCost >= 2 ? ResKey.DeathMedium
                   : ResKey.DeathSmall;
        if (Spawn(key, pos + Vector3.up * 0.5f, Quaternion.identity) != null) return;
        // Fallback: 程序化爆炸（火球 + 烟雾 + 冲击波 + 火星）
        float scale = popCost >= 4 ? 2.4f : popCost >= 2 ? 1.6f : 1.0f;
        FallbackExplosion(pos + Vector3.up * 0.4f, scale);
    }

    public static void PlayBuildingDeath(Vector3 pos, Vector3 size)
    {
        // 建筑爆炸音（永远算大爆炸）
        _CombatSfx.PlayBigExplosion(pos);
        // 大焦痕（建筑覆盖范围 ×0.9）
        SpawnScorchMark(pos, Mathf.Max(3.5f, size.magnitude * 0.9f));
        // 建筑爆炸震屏：按建筑体积 + 距离衰减；主基地等大型建筑 magnitude 0.7+
        float bMag = Mathf.Clamp(size.magnitude * 0.06f, 0.30f, 0.85f);
        TryShakeCamera(pos, bMag, 0.30f);
        if (Spawn(ResKey.DeathLarge, pos + Vector3.up * size.y * 0.5f, Quaternion.identity) != null) return;
        FallbackExplosion(pos + Vector3.up * size.y * 0.5f, Mathf.Max(2.0f, size.magnitude * 0.5f));
    }

    /// <summary>距离衰减的震屏：水平距离 0~70m 之间从满强度衰减到 0；超过则不震。</summary>
    static void TryShakeCamera(Vector3 worldPos, float baseMagnitude, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float dx = cp.x - worldPos.x, dz = cp.z - worldPos.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        const float MaxRange = 70f;
        if (dist > MaxRange) return;
        float falloff = 1f - dist / MaxRange; // 1 at center → 0 at edge
        // 远处给更短的震动时间，避免远爆炸震得太久
        float dur = duration * Mathf.Lerp(0.5f, 1f, falloff);
        RTSCamera.Shake(baseMagnitude * falloff, dur);
    }

    public static void PlayImpact(Vector3 pos, Vector3 normal, ProjectileType type)
    {
        string key = type switch
        {
            ProjectileType.Bullet => ResKey.ImpactBullet,
            ProjectileType.Shell  => ResKey.ImpactShell,
            ProjectileType.Rocket => ResKey.ImpactRocket,
            ProjectileType.Fire   => ResKey.ImpactFire,
            _                     => ResKey.ImpactBullet
        };
        var rot = normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        if (Spawn(key, pos, rot) != null) return;
        // Fallback: 命中冲击
        Color tint = type switch
        {
            ProjectileType.Rocket => new Color(1f, 0.55f, 0.20f),
            ProjectileType.Shell  => new Color(1f, 0.78f, 0.30f),
            ProjectileType.Fire   => new Color(1f, 0.40f, 0.10f),
            _                     => new Color(1f, 0.92f, 0.55f)
        };
        float impactScale = type == ProjectileType.Rocket ? 1.3f
                          : type == ProjectileType.Shell  ? 1.0f
                          : type == ProjectileType.Fire   ? 1.1f : 0.55f;
        FallbackImpact(pos, tint, impactScale);
    }

    public static void PlayMuzzleFlash(Vector3 pos, Quaternion rot, Transform parent = null)
    {
        // 枪声（按距离/节流，避免成百上千攻击同帧爆耳）
        _CombatSfx.PlayShot(pos);
        var inst = Spawn(ResKey.MuzzleFlash, pos, rot);
        if (inst != null)
        {
            if (parent != null) inst.transform.SetParent(parent, true);
            return;
        }
        // Fallback：黄色短促小火星 + 闪光
        EnsureInstance();
        BuildParticle(pos, 0.6f, fire: true,
            burstCount: 8,
            startSpeed: 5.5f,
            startSize: 0.10f,
            life: 0.18f,
            startColor: new Color(1f, 0.92f, 0.45f),
            gravity: 0.2f,
            sphereShape: true);
        // 短闪光（黄白色）
        BuildFlash(pos, 0.6f, new Color(1f, 0.95f, 0.55f, 0.85f), 0.10f);
    }

    public static void PlayBuildComplete(Vector3 pos)
    {
        // 清脆叮咚（己方生产/建造完成）
        _UiClickAudio.PlayChime();
        if (Spawn(ResKey.BuildComplete, pos, Quaternion.identity) != null) return;
        FallbackImpact(pos + Vector3.up * 0.3f, new Color(0.55f, 0.95f, 0.55f), 1.4f);
    }

    /// <summary>受击火星粒子（轻量，单位被攻击时触发）。</summary>
    public static void PlayHitSpark(Vector3 pos, bool crit = false)
    {
        _CombatSfx.PlayHit(pos);
        EnsureInstance();
        Color tint = crit ? new Color(1f, 0.45f, 0.20f) : new Color(1f, 0.85f, 0.45f);
        BuildParticle(pos, crit ? 0.7f : 0.45f, fire: true,
            burstCount: crit ? 12 : 7,
            startSpeed: 4.0f,
            startSize: 0.10f,
            life: 0.28f,
            startColor: tint,
            gravity: 0.8f,
            sphereShape: true);
    }

    public static void PlayLevelUp(Vector3 pos)
    {
        if (Spawn(ResKey.LevelUp, pos, Quaternion.identity) != null) return;
        FallbackImpact(pos + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.30f), 1.5f);
    }

    // ── 焦痕（弹坑） ────────────────────────────────

    private static Texture2D _scorchTex;
    private static Material _scorchMat;

    /// <summary>在地面贴一个圆形深色焦痕 quad，缓慢淡出 25 秒。</summary>
    public static void SpawnScorchMark(Vector3 pos, float radius)
    {
        EnsureInstance();
        if (_scorchTex == null) BuildScorchTexture();
        if (_scorchMat == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            _scorchMat = new Material(shader);
            _scorchMat.mainTexture = _scorchTex;
        }
        var go = new GameObject("ScorchMark");
        go.transform.position = new Vector3(pos.x, 0.04f, pos.z);
        go.transform.rotation = Quaternion.Euler(90f, Random.value * 360f, 0f);
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetQuadMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = _scorchMat;

        go.AddComponent<_ScorchFader>();
    }

    static Mesh _quadMesh;
    static Mesh GetQuadMesh()
    {
        if (_quadMesh != null) return _quadMesh;
        _quadMesh = new Mesh { name = "ScorchQuad" };
        _quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f), new Vector3(-0.5f,  0.5f, 0f),
        };
        _quadMesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        _quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        _quadMesh.RecalculateNormals();
        return _quadMesh;
    }

    static void BuildScorchTexture()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = (x - N * 0.5f) / (N * 0.5f);
            float dy = (y - N * 0.5f) / (N * 0.5f);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            // 中心黑→外围淡，边缘 noise 不规则
            float noise = (Mathf.PerlinNoise(x * 0.08f, y * 0.08f) - 0.5f) * 0.18f;
            float darkness = Mathf.Clamp01(1f - r + noise);
            // 中心 0.85 alpha，外缘 0
            byte a = (byte)(Mathf.Clamp01(darkness * darkness) * 220f);
            // 略带焦黄色
            byte b = (byte)(20 + r * 8f);
            px[y * N + x] = new Color32(15, 12, 8, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        _scorchTex = tex;
    }

    // ── 履带印记 ────────────────────────────────────

    private static Texture2D _trackTex;
    private static Mesh _trackQuadMesh;
    private static Material _trackMat;
    private static Transform _trackRoot;
    private const int MaxTrackMarks = 120;
    private static readonly Queue<GameObject> _trackMarks = new Queue<GameObject>(MaxTrackMarks + 1);

    /// <summary>地面贴一个深色小矩形作为坦克履带印。</summary>
    public static void SpawnTrackMark(Vector3 pos, Vector3 forward, float lifetime = 12f)
    {
        EnsureInstance();
        if (_trackTex == null) BuildTrackTexture();
        if (_trackQuadMesh == null) _trackQuadMesh = GetQuadMesh();
        if (_trackMat == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            _trackMat = new Material(shader);
            _trackMat.mainTexture = _trackTex;
        }

        var go = new GameObject("TankTrackMark");
        go.transform.position = new Vector3(pos.x, 0.035f, pos.z);
        // forward 对应 quad 的 +y 轴（朝上时 yaw 由 forward 推算）
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        go.transform.localScale = new Vector3(0.55f, 1.4f, 1f);
        go.transform.SetParent(GetTrackRoot(), true);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = _trackQuadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = _trackMat;

        var fader = go.AddComponent<_ScorchFader>();
        fader.Lifetime = lifetime;
        _trackMarks.Enqueue(go);
        TrimTrackMarks();
    }

    static Transform GetTrackRoot()
    {
        if (_trackRoot != null) return _trackRoot;
        var existing = GameObject.Find("TrackMarks");
        _trackRoot = existing != null ? existing.transform : new GameObject("TrackMarks").transform;
        return _trackRoot;
    }

    static void TrimTrackMarks()
    {
        while (_trackMarks.Count > 0 && _trackMarks.Peek() == null)
            _trackMarks.Dequeue();

        while (_trackMarks.Count > MaxTrackMarks)
        {
            var oldest = _trackMarks.Dequeue();
            if (oldest != null) Destroy(oldest);
        }
    }

    static void BuildTrackTexture()
    {
        const int W = 32, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            // 履带横纹：每 4 像素一条暗色横纹 + 边缘渐隐
            float dx = Mathf.Abs(x - W * 0.5f) / (W * 0.5f);
            float fade = Mathf.Clamp01(1f - dx * 1.2f);
            int rung = y % 6;
            float rungAlpha = (rung < 3) ? 1f : 0.45f;
            byte a = (byte)(fade * rungAlpha * 200f);
            px[y * W + x] = new Color32(20, 16, 12, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        _trackTex = tex;
    }

    // ── Fallback 程序化粒子 ──────────────────────────

    static void FallbackExplosion(Vector3 pos, float scale)
    {
        EnsureInstance();
        // 1. 主粒子：火球
        BuildParticle(pos, scale, fire: true,
            burstCount: Mathf.RoundToInt(28f * scale),
            startSpeed: 5.5f * scale,
            startSize: 0.55f * scale,
            life: 0.55f,
            startColor: new Color(1f, 0.78f, 0.22f, 1f),
            gravity: 0.3f,
            sphereShape: true);
        // 2. 烟雾
        BuildParticle(pos, scale, fire: false,
            burstCount: Mathf.RoundToInt(18f * scale),
            startSpeed: 2.2f * scale,
            startSize: 0.85f * scale,
            life: 1.2f,
            startColor: new Color(0.18f, 0.18f, 0.18f, 0.85f),
            gravity: -0.4f,
            sphereShape: false);
        // 3. 火星
        BuildParticle(pos, scale, fire: true,
            burstCount: Mathf.RoundToInt(20f * scale),
            startSpeed: 9f * scale,
            startSize: 0.12f * scale,
            life: 0.7f,
            startColor: new Color(1f, 0.92f, 0.44f, 1f),
            gravity: 1.2f,
            sphereShape: true);
        // 4. 冲击波环
        BuildShockwaveRing(pos, scale * 1.6f, new Color(1f, 0.68f, 0.20f, 0.78f));
        // 5. 闪光
        BuildFlash(pos + Vector3.up * 0.4f, scale * 2.2f, new Color(1f, 0.88f, 0.50f, 0.85f), 0.18f);
    }

    static void FallbackImpact(Vector3 pos, Color tint, float scale)
    {
        EnsureInstance();
        BuildParticle(pos, scale, fire: true,
            burstCount: Mathf.RoundToInt(14f * scale),
            startSpeed: 4.5f * scale,
            startSize: 0.18f * scale,
            life: 0.35f,
            startColor: tint,
            gravity: 0.6f,
            sphereShape: true);
        BuildShockwaveRing(pos, scale * 0.9f, new Color(tint.r, tint.g, tint.b, 0.6f));
    }

    static GameObject BuildParticle(Vector3 pos, float scale, bool fire, int burstCount,
        float startSpeed, float startSize, float life, Color startColor, float gravity, bool sphereShape)
    {
        var go = new GameObject(fire ? "FxFireParticles" : "FxSmokeParticles");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        var main = ps.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = life;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = startColor;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = burstCount * 2;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = sphereShape ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.15f * scale;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        if (fire)
        {
            grad.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(new Color(0.65f, 0.18f, 0.06f), 0.55f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
        }
        else
        {
            grad.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(new Color(0.32f, 0.32f, 0.32f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0.05f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
        }
        colorOverLife.color = grad;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, fire ? 1.0f : 0.6f),
            new Keyframe(0.5f, fire ? 1.3f : 1.4f),
            new Keyframe(1f, fire ? 0.4f : 1.6f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(shader);
            renderer.material.color = startColor;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        ps.Play();
        Destroy(go, life + 0.5f);
        return go;
    }

    static GameObject BuildShockwaveRing(Vector3 pos, float maxScale, Color color)
    {
        return BuildParticle(pos + Vector3.up * 0.08f, maxScale, fire: true,
            burstCount: Mathf.RoundToInt(18f * Mathf.Max(1f, maxScale)),
            startSpeed: 4.5f * Mathf.Max(1f, maxScale),
            startSize: 0.07f * Mathf.Max(1f, maxScale),
            life: 0.32f,
            startColor: color,
            gravity: 0f,
            sphereShape: false);
    }

    static GameObject BuildFlash(Vector3 pos, float maxScale, Color color, float life)
    {
        return BuildParticle(pos, Mathf.Max(0.6f, maxScale), fire: true,
            burstCount: 10,
            startSpeed: 0.45f * Mathf.Max(1f, maxScale),
            startSize: 0.22f * Mathf.Max(1f, maxScale),
            life: life,
            startColor: color,
            gravity: 0f,
            sphereShape: true);
    }
}

/// <summary>放大 + 淡出动画，用于冲击波环和闪光。</summary>
internal class _FxShockwaveAnimator : MonoBehaviour
{
    float life;
    float t;
    float maxScale;
    Renderer rd;
    Color startColor;

    public void Init(float maxScale, float life)
    {
        this.maxScale = maxScale;
        this.life = Mathf.Max(0.05f, life);
        rd = GetComponent<Renderer>();
        if (rd != null && rd.material != null) startColor = rd.material.color;
    }

    void Update()
    {
        t += Time.deltaTime;
        float r = Mathf.Clamp01(t / life);
        transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maxScale, r);
        if (rd != null && rd.material != null && rd.material.HasProperty("_Color"))
        {
            var c = startColor;
            c.a = startColor.a * (1f - r);
            rd.material.color = c;
        }
        if (r >= 1f) Destroy(gameObject);
    }
}

public enum ProjectileType
{
    Bullet,
    Shell,
    Rocket,
    Fire
}
