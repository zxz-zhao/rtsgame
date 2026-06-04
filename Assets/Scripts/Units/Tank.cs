using UnityEngine;
public class Tank : RTSUnit
{
    [Header("坦克属性")]
    public float HullRotSpeed = 55f;
    public float TurretRotSpeed = 50f;
    // 二战风 RTS 没有主动技能；仅保留字段供 GetArmorPierceCooldown() 兼容旧 HUD/网络协议读取
    private float armorPierceCooldown = 0f;

    // 履带印记
    private Vector3 _lastTrackPos;
    private bool _trackInit = false;
    private const float TrackStep = 2.2f;       // 每移动 2.2m 生成一对履带印
    private const float TrackHalfWidth = 0.8f;  // 左右轮距
    private const float TrackLifetime = 12f;

    protected override float DesiredVisualHeight => 3.0f;
    protected override float DesiredVisualFootprint => 3.6f;
    protected override float HealthBarHeight => 3.6f;
    protected override float UnitLabelHeight => 3.25f;
    protected override float SelectionRingRadius => 1.9f;

    protected override void Awake()
    {
        DisplayName = "坦克";
        MaxHP = 500; AttackDamage = 90; AttackRange = 13f;
        AttackInterval = 1.5f; SightRange = 18f;
        GoldCost = 450; PopCost = 3; MoveSpeed = 5f;
        SplashRadius = 3.5f; SplashFalloff = 0.45f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = 46f; ProjectileArcHeight = 1.8f; ProjectileImpactRadius = 1.25f;
        ProjectileTint = new Color(1f, 0.56f, 0.18f, 1f);
        TracerDuration = 0.06f;
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        UpdateTrackMarks();
    }

    /// <summary>移动时每隔 TrackStep 米生成一对履带印。</summary>
    void UpdateTrackMarks()
    {
        if (IsDead()) return;
        if (!_trackInit)
        {
            _lastTrackPos = transform.position;
            _trackInit = true;
            return;
        }
        Vector3 cur = transform.position;
        if ((cur - _lastTrackPos).sqrMagnitude < TrackStep * TrackStep) return;
        // 当前坦克朝向（用 Agent.velocity 或 forward）
        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude < 0.001f) fwd = (cur - _lastTrackPos).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        EffectsManager.SpawnTrackMark(cur + right * TrackHalfWidth, fwd, TrackLifetime);
        EffectsManager.SpawnTrackMark(cur - right * TrackHalfWidth, fwd, TrackLifetime);
        _lastTrackPos = cur;
    }

    /// <summary>
    /// 二战风 RTS 没有主动技能：函数保留以保持 API 兼容（HUD/网络协议旧调用），但内部短路返回 false，
    /// 不再触发任何伤害或网络指令。如需移除可同步删除 GameNetworkSync 内的 stype=0 处理。
    /// </summary>
    public bool UseArmorPierce(RTSUnit target, bool ignoreCooldown = false) => false;

    public float GetArmorPierceCooldown() => armorPierceCooldown;
}
