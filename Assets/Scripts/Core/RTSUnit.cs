using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// RTS单位基类，所有单位继承此类
public class RTSUnit : MonoBehaviour
{
    [Header("基础属性")]
    public string DisplayName = "单位";
    public int MaxHP = 200;
    public int AttackDamage = 25;
    public float AttackRange = 10f;
    public float AttackInterval = 1f;
    public float SightRange = 20f;
    public int GoldCost = 100;
    public int PopCost = 1;
    public float MoveSpeed = 5f;

    [Header("溅射伤害")]
    public float SplashRadius = 0f;
    public float SplashFalloff = 0.5f;

    [Header("Combat VFX")]
    public string ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
    public float ProjectileSpeed = 70f;
    public float ProjectileArcHeight = 0f;
    public float ProjectileImpactRadius = 0.45f;
    public Color ProjectileTint = new Color(1f, 0.78f, 0.24f, 1f);
    public float TracerDuration = 0.045f;

    protected virtual float MoveStoppingDistance => 0.35f;
    protected virtual float CombatStoppingDistance => Mathf.Max(0.5f, AttackRange * 0.8f);

    [Header("状态")]
    public bool bPlayerOwned = false;
    public bool bFlying = false;
    [System.NonSerialized] public int NetId = 0;  // 联机局唯一ID
    [System.NonSerialized] public bool NetSyncIncoming = false; // 防止接收同步时递归
    [System.NonSerialized] public int Kills = 0; // 击杀数（老兵晋级用）
    private TextMesh _veteranLabel;

    // 运行时状态
    protected int CurrentHP;
    protected RTSUnit AttackTarget;
    protected float AttackTimer = 0f;
    protected NavMeshAgent Agent;
    protected RTSPlayerController OwnerPC;

    // 命令类型
    public enum CommandType { None, Move, Attack, AttackMove, Stop, Patrol, Guard }
    protected CommandType CurrentCommand = CommandType.None;
    protected Vector3 CommandTarget;
    protected RTSUnit CommandTargetUnit;
    private const float MoveArrivalPadding = 0.55f;

    protected virtual void Awake()
    {
        CaptureBaseStats();
        CurrentHP = MaxHP;
        Agent = GetComponent<NavMeshAgent>();
        if (Agent != null)
        {
            Agent.speed = MoveSpeed;
            UseMoveStoppingDistance();
        }
        // 阵营球和小地图标记在 Start() 创建（bPlayerOwned 由外部在 Awake 之后设置）
    }

    void CaptureBaseStats()
    {
        _baseMaxHP = Mathf.Max(1, MaxHP);
        _baseAttackDamage = Mathf.Max(1, AttackDamage);
        _baseAttackRange = Mathf.Max(0.1f, AttackRange);
        _baseAttackInterval = Mathf.Max(0.05f, AttackInterval);
        _baseSightRange = Mathf.Max(0.1f, SightRange);
        _baseMoveSpeed = Mathf.Max(0.1f, MoveSpeed);
    }

    protected void UseMoveStoppingDistance()
    {
        if (Agent != null)
            Agent.stoppingDistance = MoveStoppingDistance;
    }

    protected void UseCombatStoppingDistance()
    {
        if (Agent != null)
            Agent.stoppingDistance = CombatStoppingDistance;
    }

    // 头顶血条
    private WorldHealthBar healthBar;
    // 选中光环
    private GameObject selectionRing;
    private GameObject factionRing;
    public bool IsSelected { get; private set; }
    // 攻击连线
    private LineRenderer _attackLine;
    // 死亡保护
    private bool _isDying = false;
    // 受伤闪烁
    private Color _bodyColor;
    private int   _flashCount = 0;
    // 低血量脉动警告
    private Renderer[] _allRenderers;
    private Color[]    _origRenderColors;
    private bool       _lowHpPulseActive = false;
    private UnitVisualAnimator _visualAnimator;
    // 建筑攻击目标
    protected RTSBuilding buildingTarget;
    // 溢射攻击缓冲（避免 OverlapSphere 逐次堆分配）
    private static readonly Collider[] _splashBuffer = new Collider[32];
    private struct RuntimeTechBuff
    {
        public string SourceId;
        public float EndTime;
        public float MoveMultiplier;
        public float DamageMultiplier;
        public float AttackRangeBonus;
        public float AttackIntervalMultiplier;
        public float DefenseReduction;
        public float SightRangeBonus;
        public float RegenPerSecond;
        public Color Tint;
    }

    private readonly List<RuntimeTechBuff> _techBuffs = new List<RuntimeTechBuff>(8);
    private int _baseMaxHP;
    private int _baseAttackDamage;
    private float _baseAttackRange;
    private float _baseAttackInterval;
    private float _baseSightRange;
    private float _baseMoveSpeed;
    private float _techDamageReduction;
    private float _techRegenPerSecond;
    private float _techRegenCarry;
    private GameObject _techBuffRing;
    private Renderer _techBuffRingRenderer;
    // 敌方扫描节流（避免 FindNearestEnemy/FindNearestEnemyBuilding 每帧执行）
    private float _enemyScanTimer = 0f;
    private float _bldgScanTimer  = 0f;
    private const float EnemyScanInterval = 0.4f;

    // 子类 override 指定可视尺寸（米）。0 = 不做尺寸标定，沿用 prefab 原始尺寸。
    /// <summary>单位期望可视高度（Y，米）。</summary>
    protected virtual float DesiredVisualHeight => 0f;
    /// <summary>单位期望可视占地直径（X/Z 最大边，米）。</summary>
    protected virtual float DesiredVisualFootprint => 0f;
    protected virtual bool IncludeAttachmentVisualsInScale => false;
    protected virtual float HealthBarHeight => bFlying ? 5f : 2.5f;
    protected virtual float UnitLabelHeight => 2.0f;
    protected virtual float SelectionRingRadius => bFlying ? 2.2f : 1.5f;

    protected virtual void Start()
    {
        // 归属玩家
        if (bPlayerOwned)
        {
            OwnerPC = RTSPlayerController.Instance;
        }
        // 注册到GameManager
        GameManager.Instance?.RegisterUnit(this);
        // 添加单位浮动名称标签（在 Start 里，DisplayName 已由子类 Awake 设置好）
        // 创建头顶血条（初始满血时隐藏）
        healthBar = WorldHealthBar.Create(transform, HealthBarHeight);
        // 单位名称浮动标签
        AddUnitLabel();
        // 选中光环
        selectionRing = CreateSelectionRing(transform, SelectionRingRadius);
        // 攻击连线
        _attackLine = CreateAttackLine(bPlayerOwned);
        // 阵营标识球（Start 时 bPlayerOwned 已由外部正确设置）
        ApplyUnitBodyColor(bPlayerOwned ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.85f, 0.2f, 0.2f));
        // 记录初始身体颜色（供受伤闪烁恢复用）
        var _rend = GetComponent<Renderer>();
        Color bodyColor;
        if (_rend != null && RendererColorUtil.TryGetColor(_rend, out bodyColor))
            _bodyColor = bodyColor;
        // 小地图标记（只添加一次，颜色正确）
        var marker = gameObject.AddComponent<MinimapMarker>();
        marker.MarkerColor  = bPlayerOwned ? new Color(0.3f, 0.75f, 1f) : new Color(1f, 0.3f, 0.25f);
        marker.MarkerSize   = bFlying ? 9f : 7f;
        marker.HeightOffset = 80f;
        _visualAnimator = GetComponent<UnitVisualAnimator>();
        // 先清理违和的小装饰（旗子、瞄准镜、子弹、油桶、散件箱），避免它们影响后续缩放包围盒计算
        UnitVisualPolish.Polish(gameObject);
        // 子类显式指定目标尺寸时，强制把可视模型标定到统一比例（解决各 Kenney prefab 尺寸杂乱）
        if (DesiredVisualHeight > 0f && DesiredVisualFootprint > 0f)
            UnitScaleNormalizer.Normalize(transform, DesiredVisualHeight, DesiredVisualFootprint, IncludeAttachmentVisualsInScale);
        // 军事配色：用 MaterialPropertyBlock 叠加颜色，不产生新材质实例
        ApplyMilitaryTint();
        // 敌方单位：挂战雾隐藏组件（视野外不显示）
        if (!bPlayerOwned && GetComponent<FogHideable>() == null)
            gameObject.AddComponent<FogHideable>();
    }

    protected virtual void ApplyMilitaryTint()
    {
        // Ground units keep the WW2 palette; aircraft need stronger separation from the teal battlefield.
        Color tint = bPlayerOwned
            ? (bFlying ? new Color(0.24f, 0.50f, 0.68f) : new Color(0.28f, 0.38f, 0.17f))
            : (bFlying ? new Color(0.72f, 0.24f, 0.18f) : new Color(0.65f, 0.50f, 0.28f));
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", tint);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            string n = r.gameObject.name;
            if (n == "FactionRing" || n == "FactionDot" || n == "SelectionCircle") continue;
            if (n == "HPLabel" || n == "UnitLabel" || n == "AttackLine") continue;
            if (n.StartsWith("FactionStripe") || n.StartsWith("AircraftForwardStripe")) continue;
            // 排除所有地面光环/圆盘效果（避免半径大的圆盘被染色后覆盖整个屏幕）
            Transform cur = r.transform;
            bool skip = false;
            while (cur != null && cur != transform)
            {
                if (cur.name.EndsWith("Aura") || cur.name.EndsWith("Disc")
                    || cur.name.EndsWith("Ring") || cur.name == "AirShadow")
                { skip = true; break; }
                cur = cur.parent;
            }
            if (skip) continue;
            r.SetPropertyBlock(block);
        }
    }

    // 创建扁平圆盘光环，radius 为世界单位半径
    static GameObject CreateSelectionRing(Transform parent, float radius)
    {
        // 金色（与 HUD 金边一致），用 Quad+圆环贴图替代旧 Cylinder
        var ring = FxResources.MakeGroundDisc(parent, "SelectionRing", radius,
            new Color(0.95f, 0.78f, 0.25f, 1f), FxResources.DiscStyle.ThinRing, 0.04f);
        ring.AddComponent<_SelectionRingPulse>();
        ring.SetActive(false);
        return ring;
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (factionRing != null)
            factionRing.SetActive(selected);
        if (!selectionRing) return;
        bool wasActive = selectionRing.activeSelf;
        selectionRing.SetActive(selected);
        // 从未选 → 选中时触发瞬时强脉动（视觉反馈）
        if (selected && !wasActive)
        {
            var pulse = selectionRing.GetComponent<_SelectionRingPulse>();
            if (pulse != null) pulse.TriggerSelectImpulse();
        }
    }

    LineRenderer CreateAttackLine(bool playerOwned)
    {
        var lr = gameObject.AddComponent<LineRenderer>();
        var sh = Shader.Find("Unlit/Color");
        lr.material = sh != null ? new Material(sh) : new Material(Shader.Find("Standard"));
        Color lineColor = playerOwned
            ? (bFlying ? new Color(0.62f, 0.96f, 1f, 1f) : new Color(0.3f, 0.88f, 1f, 1f))
            : (bFlying ? new Color(1f, 0.56f, 0.22f, 1f) : new Color(1f, 0.42f, 0.08f, 1f));
        RendererColorUtil.TrySetColor(lr.material, lineColor);
        lr.startWidth    = bFlying ? 0.20f : 0.14f;
        lr.endWidth      = bFlying ? 0.07f : 0.04f;
        lr.positionCount = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    protected System.Collections.IEnumerator FlashAttackLine(Vector3 targetPos)
    {
        if (_attackLine == null) yield break;
        Vector3 origin = GetAttackOrigin();
        Vector3 impact = targetPos + Vector3.up * 1.0f;
        _attackLine.SetPosition(0, origin);
        _attackLine.SetPosition(1, impact);
        _attackLine.enabled = true;
        SpawnProjectileVisual(origin, impact);
        yield return new WaitForSeconds(Mathf.Clamp(TracerDuration, 0.02f, 0.14f));
        if (_attackLine != null) _attackLine.enabled = false;
    }

    Vector3 GetAttackOrigin()
    {
        if (bFlying)
            return transform.position + transform.forward * 0.95f + Vector3.up * 0.18f;

        Transform hardpoint = FindWeaponHardpoint(transform);
        if (hardpoint != null)
            return hardpoint.position + hardpoint.forward * 0.35f;
        return transform.position + Vector3.up * (bFlying ? 1.8f : 1.2f);
    }

    Quaternion GetAttackRotation(Vector3 fallbackDirection)
    {
        if (bFlying)
        {
            if (fallbackDirection.sqrMagnitude < 0.0001f)
                fallbackDirection = transform.forward;
            fallbackDirection.y = 0f;
            if (fallbackDirection.sqrMagnitude < 0.0001f)
                fallbackDirection = transform.forward;
            return Quaternion.LookRotation(fallbackDirection.normalized, Vector3.up);
        }

        Transform hardpoint = FindWeaponHardpoint(transform);
        if (hardpoint != null)
            return hardpoint.rotation;
        if (fallbackDirection.sqrMagnitude < 0.0001f)
            fallbackDirection = transform.forward;
        return Quaternion.LookRotation(fallbackDirection.normalized, Vector3.up);
    }

    Transform FindWeaponHardpoint(Transform root)
    {
        string[] priorityNames =
        {
            "Muzzle", "Nozzle", "Barrel", "KenneyWeapon", "Rifle", "GunShield"
        };

        for (int i = 0; i < priorityNames.Length; i++)
        {
            Transform match = FindChildByNameToken(root, priorityNames[i]);
            if (match != null) return match;
        }
        return null;
    }

    Transform FindChildByNameToken(Transform root, string token)
    {
        if (root != transform && !root.gameObject.activeInHierarchy)
            return null;
        if (root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildByNameToken(child, token);
            if (found != null) return found;
        }
        return null;
    }

    void SpawnProjectileVisual(Vector3 origin, Vector3 targetPos)
    {
        GameObject prefab = string.IsNullOrEmpty(ProjectilePrefabPath)
            ? null
            : Resources.Load<GameObject>(ProjectilePrefabPath);

        if (prefab == null)
        {
            SpawnImpactFlash(targetPos, ProjectileTint, Mathf.Max(ProjectileImpactRadius, SplashRadius * 0.32f));
            return;
        }

        GameObject projectile = Instantiate(prefab, origin, Quaternion.identity);
        var visual = projectile.GetComponent<CombatProjectile>();
        if (visual == null)
            visual = projectile.AddComponent<CombatProjectile>();

        float radius = ProjectileImpactRadius > 0f
            ? ProjectileImpactRadius
            : (SplashRadius > 0f ? Mathf.Clamp(SplashRadius * 0.32f, 0.45f, 1.8f) : 0.45f);
        visual.Initialize(origin, targetPos, ProjectileTint, ProjectileSpeed, ProjectileArcHeight, radius);
    }

    void SpawnImpactFlash(Vector3 targetPos, Color color, float radius)
    {
        EffectsManager.PlayImpact(targetPos, Vector3.up, ProjectileType.Bullet);
    }

    void ApplyUnitBodyColor(Color c)
    {
        // 脚下阵营光圈只在选中时显示，避免未选中单位脚底常亮。
        factionRing = FxResources.MakeGroundDisc(transform, "FactionRing", 0.55f,
            new Color(c.r, c.g, c.b, 0.85f),
            FxResources.DiscStyle.MediumRing, 0.05f);
        factionRing.SetActive(IsSelected);
    }

    void AddUnitLabel()
    {
        string label = RTSHUD.GetUnitDisplayNameStatic(this);
        var labelGO = new GameObject("UnitLabel");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, UnitLabelHeight, 0f);
        labelGO.transform.localScale = new Vector3(0.055f, 0.055f, 0.055f);
        var tm = labelGO.AddComponent<TextMesh>();
        tm.text          = label;
        tm.fontSize      = 48;
        tm.characterSize = 1.0f;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = bPlayerOwned ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 0.5f, 0.4f);
        tm.fontStyle     = FontStyle.Bold;
        labelGO.AddComponent<BillboardLabel>();
        var mr = labelGO.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    // 卡住检测（非玩家单位专用）
    private float  stuckCheckTimer  = 0f;
    private Vector3 lastCheckPos    = Vector3.zero;
    private const float StuckCheckInterval = 20f; // 每 20s 检查一次
    private const float StuckMinDistance   = 1f;  // 低于此距离视为卡住

    protected virtual void Update()
    {
        if (IsDead()) return;
        AttackTimer -= Time.deltaTime;
        UpdateTechBuffs();
        UpdateAI();
        if (!bPlayerOwned) CheckStuck();
        UpdateLowHpPulse();
    }

    void UpdateTechBuffs()
    {
        bool changed = false;
        for (int i = _techBuffs.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _techBuffs[i].EndTime)
            {
                _techBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            RecalculateTechBuffStats();

        if (_techRegenPerSecond > 0f && CurrentHP < MaxHP)
        {
            _techRegenCarry += _techRegenPerSecond * Time.deltaTime;
            int heal = Mathf.FloorToInt(_techRegenCarry);
            if (heal > 0)
            {
                _techRegenCarry -= heal;
                CurrentHP = Mathf.Min(MaxHP, CurrentHP + heal);
                healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
            }
        }

        UpdateTechBuffRingVisual();
    }

    /// <summary>HP &lt; 25% 时整个 model 持续脉动红光，恢复后还原。</summary>
    void UpdateLowHpPulse()
    {
        if (_isDying || _flashCount > 0) return; // 受击瞬间让 HitFlash 控制颜色
        bool shouldPulse = MaxHP > 0 && CurrentHP > 0 && (float)CurrentHP / MaxHP < 0.25f;

        if (shouldPulse && !_lowHpPulseActive)
        {
            // 第一次进入：缓存所有 renderer 原色
            CacheRenderersForPulse();
            _lowHpPulseActive = true;
        }
        else if (!shouldPulse && _lowHpPulseActive)
        {
            // 退出：恢复原色
            RestoreRendererColors();
            _lowHpPulseActive = false;
            return;
        }

        if (!_lowHpPulseActive || _allRenderers == null) return;

        // 脉动：sin 周期 4Hz，强度 0~0.55
        float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f; // 0..1
        Color warning = new Color(1f, 0.20f, 0.15f);
        for (int i = 0; i < _allRenderers.Length; i++)
        {
            if (_allRenderers[i] == null || _allRenderers[i].sharedMaterial == null) continue;
            string n = _allRenderers[i].gameObject.name;
            if (n == "FactionRing" || n == "FactionDot" || n == "SelectionRing" || n == "TechBuffRing" || n == "UnitLabel") continue;
            RendererColorUtil.TrySetColor(_allRenderers[i], Color.Lerp(_origRenderColors[i], warning, 0.55f * pulse));
        }
    }

    void CacheRenderersForPulse()
    {
        _allRenderers = GetComponentsInChildren<Renderer>();
        if (_allRenderers == null) { _origRenderColors = null; return; }
        _origRenderColors = new Color[_allRenderers.Length];
        for (int i = 0; i < _allRenderers.Length; i++)
        {
            Color color;
            _origRenderColors[i] = RendererColorUtil.TryGetColor(_allRenderers[i], out color) ? color : Color.white;
        }
    }

    void RestoreRendererColors()
    {
        if (_allRenderers == null || _origRenderColors == null) return;
        for (int i = 0; i < _allRenderers.Length; i++)
            RendererColorUtil.TrySetColor(_allRenderers[i], _origRenderColors[i]);
    }

    void CheckStuck()
    {
        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer < StuckCheckInterval) return;
        stuckCheckTimer = 0f;
        // 若本轮位移不足 1m 且还在攻击移动中，且当前没有攻击目标 → 才视为卡住并自毁
        float moved = Vector3.Distance(transform.position, lastCheckPos);
        bool hasCombatTarget = (AttackTarget != null && !AttackTarget.IsDead())
                             || (buildingTarget != null && buildingTarget.GetHP() > 0);
        if (moved < StuckMinDistance && CurrentCommand == CommandType.AttackMove && !hasCombatTarget)
        {
            OnDeath();
            return;
        }
        lastCheckPos = transform.position;
    }

    protected virtual void UpdateAI()
    {
        switch (CurrentCommand)
        {
            case CommandType.Move:
                UpdateMove();
                break;
            case CommandType.Attack:
                UpdateAttack();
                break;
            case CommandType.AttackMove:
                UpdateAttackMove();
                break;
            case CommandType.Stop:
                SafeResetPath();
                break;
            case CommandType.Patrol:
                UpdatePatrol();
                break;
            case CommandType.Guard:
                UpdateGuard();
                break;
        }
    }

    void UpdateMove()
    {
        UseMoveStoppingDistance();
        if (HasReachedMoveTarget())
            CompleteMoveCommand();
    }

    bool HasReachedMoveTarget()
    {
        Vector3 flatDelta = CommandTarget - transform.position;
        flatDelta.y = 0f;
        float directDistance = flatDelta.magnitude;
        float arriveDistance = Mathf.Max(MoveStoppingDistance + MoveArrivalPadding, 0.85f);
        if (directDistance <= arriveDistance)
            return true;

        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh || Agent.pathPending)
            return false;

        float agentArrival = Mathf.Max(Agent.stoppingDistance + 0.2f, 0.6f);
        if (!float.IsInfinity(Agent.remainingDistance) && Agent.remainingDistance <= agentArrival)
            return true;

        // 多个单位挤在同一个集结点时，避障会让 remainingDistance 卡在略大于 stoppingDistance。
        return Agent.velocity.sqrMagnitude < 0.04f && directDistance <= arriveDistance + 0.65f;
    }

    void CompleteMoveCommand()
    {
        CurrentCommand = CommandType.None;
        if (SafeResetPath())
        {
            Agent.velocity = Vector3.zero;
        }
    }

    bool SafeResetPath()
    {
        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            return false;

        Agent.ResetPath();
        return true;
    }

    void UpdateAttack()
    {
        if (AttackTarget == null || AttackTarget.IsDead())
        {
            AttackTarget = null;
            CurrentCommand = CommandType.None;
            return;
        }
        float dist = Vector3.Distance(transform.position, AttackTarget.transform.position);
        if (dist <= AttackRange)
        {
            SafeResetPath();
            // 朝向目标
            Vector3 dir = (AttackTarget.transform.position - transform.position).normalized;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
            // 攻击
            if (AttackTimer <= 0f)
            {
                DoAttack(AttackTarget);
                AttackTimer = AttackInterval;
            }
        }
        else
        {
            UseCombatStoppingDistance();
            if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(AttackTarget.transform.position);
        }
    }

    void UpdateAttackMove()
    {
        // 优先攻击附近单位（每 0.4s 扫描一次，避免逐帧 O(n) 遍历）
        if (AttackTarget == null || AttackTarget.IsDead())
        {
            AttackTarget = null;
            _enemyScanTimer -= Time.deltaTime;
            if (_enemyScanTimer <= 0f)
            {
                AttackTarget = FindNearestEnemy();
                _enemyScanTimer = EnemyScanInterval;
            }
        }
        if (AttackTarget != null)
        {
            float dist = Vector3.Distance(transform.position, AttackTarget.transform.position);
            if (dist <= AttackRange)
            {
                SafeResetPath();
                Vector3 dir = (AttackTarget.transform.position - transform.position).normalized;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), Time.deltaTime * 5f);
                if (AttackTimer <= 0f)
                {
                    DoAttack(AttackTarget);
                    AttackTimer = AttackInterval;
                }
            }
            else
            {
                UseCombatStoppingDistance();
                if (Agent != null && Agent.isOnNavMesh)
                    Agent.SetDestination(AttackTarget.transform.position);
            }
        }
        else
        {
            // 无单位目标时攻击最近的敌方建筑（每 0.4s 扫描一次）
            if (buildingTarget == null || buildingTarget.GetHP() <= 0)
            {
                _bldgScanTimer -= Time.deltaTime;
                if (_bldgScanTimer <= 0f)
                {
                    buildingTarget = FindNearestEnemyBuilding();
                    _bldgScanTimer = EnemyScanInterval;
                }
            }
            if (buildingTarget != null)
            {
                float dist = Vector3.Distance(transform.position, buildingTarget.transform.position);
                if (dist <= AttackRange)
                {
                    SafeResetPath();
                    Vector3 dir = (buildingTarget.transform.position - transform.position).normalized;
                    if (dir != Vector3.zero)
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(dir), Time.deltaTime * 5f);
                    if (AttackTimer <= 0f)
                    {
                        PlayAttackVisuals(buildingTarget.transform.position);
                        AttackTimer = AttackInterval;
                        var _ns2 = GameNetworkSync.Instance;
                        if (_ns2 == null || !_ns2.IsNetworkGame || _ns2.IsHost)
                        {
                            int prevHP = buildingTarget.GetHP();
                            buildingTarget.TakeDamage(AttackDamage);
                            // 摧毁建筑算 1 击杀（与单位击杀同等晋升）
                            if (prevHP > 0 && buildingTarget.GetHP() <= 0) AddKill();
                        }
                    }
                }
                else
                {
                    UseCombatStoppingDistance();
                    if (Agent != null && Agent.isOnNavMesh)
                        Agent.SetDestination(buildingTarget.transform.position);
                }
            }
            else
            {
                UseMoveStoppingDistance();
                if (Agent != null && Agent.isOnNavMesh)
                    Agent.SetDestination(CommandTarget);
                UpdateMove();
            }
        }
    }

    protected RTSBuilding FindNearestEnemyBuilding()
    {
        RTSBuilding nearest = null;
        float minDist = SightRange * 2f;  // 建筑搜索范围更大
        var all = GameManager.Instance?.GetAllBuildings();
        if (all == null) return null;
        foreach (var b in all)
        {
            if (b == null || b.GetHP() <= 0) continue;
            if (b.bPlayerOwned == bPlayerOwned) continue;
            float d = Vector3.Distance(transform.position, b.transform.position);
            if (d < minDist) { minDist = d; nearest = b; }
        }
        return nearest;
    }

    protected virtual void DoAttack(RTSUnit target)
    {
        PlayAttackVisuals(target.transform.position);
        // 联机模式：只有 Host（权威端）计算伤害，Guest 只播放攻击特效
        var _ns = GameNetworkSync.Instance;
        if (_ns != null && _ns.IsNetworkGame && !_ns.IsHost) return;
        if (SplashRadius > 0f)
        {
            // 溢射伤害（仅敌方，不伤友军）
            int cnt = Physics.OverlapSphereNonAlloc(target.transform.position, SplashRadius, _splashBuffer);
            for (int i = 0; i < cnt; i++)
            {
                RTSUnit u = _splashBuffer[i].GetComponent<RTSUnit>();
                if (u == null || u == this || u.bPlayerOwned == bPlayerOwned) continue;
                float dist = Vector3.Distance(target.transform.position, u.transform.position);
                float mult = Mathf.Lerp(1f, SplashFalloff, dist / SplashRadius);
                bool wasAlive = !u.IsDead();
                u.TakeDamage(Mathf.RoundToInt(AttackDamage * mult));
                if (wasAlive && u.IsDead()) AddKill();
            }
        }
        else
        {
            bool wasAlive = !target.IsDead();
            target.TakeDamage(AttackDamage);
            if (wasAlive && target.IsDead()) AddKill();
        }
    }

    protected void PlayAttackVisuals(Vector3 targetPosition)
    {
        _visualAnimator?.TriggerFireAnimation();
        StartCoroutine(FlashAttackLine(targetPosition));
        // 枪口火焰特效（取单位前方约 1m 处作为枪口位置）
        Vector3 dir = (targetPosition - transform.position).normalized;
        Vector3 muzzlePos = GetAttackOrigin();
        Quaternion muzzleRot = GetAttackRotation(dir);
        EffectsManager.PlayMuzzleFlash(muzzlePos, muzzleRot, transform);
        if (bFlying)
            RTSCamera.Shake(0.045f, 0.055f);
    }

    /// <summary>击杀 +1，3 杀晋升老兵（头顶亮星），5 杀晋升精英（金星）。</summary>
    public void AddKill()
    {
        int prev = Kills;
        Kills++;
        UpdateVeteranLabel();
        // 晋升节点（1 杀首星、3 杀双星、5 杀精英）触发金光
        bool promoted = (prev == 0 && Kills == 1) || (prev == 2 && Kills == 3) || (prev == 4 && Kills == 5);
        if (promoted && bPlayerOwned)
        {
            EffectsManager.PlayLevelUp(transform.position + Vector3.up * transform.localScale.y * 1.3f);
            StartCoroutine(VeteranPromoteFlash());
        }
    }

    System.Collections.IEnumerator VeteranPromoteFlash()
    {
        if (_veteranLabel == null) yield break;
        Vector3 origin = _veteranLabel.transform.localScale;
        float t = 0f;
        const float dur = 0.55f;
        while (t < dur && _veteranLabel != null)
        {
            t += Time.deltaTime;
            float k = t / dur;
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.7f;
            _veteranLabel.transform.localScale = origin * s;
            yield return null;
        }
        if (_veteranLabel != null) _veteranLabel.transform.localScale = origin;
    }

    void UpdateVeteranLabel()
    {
        if (Kills <= 0) return;
        if (_veteranLabel == null)
        {
            var go = new GameObject("VeteranLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, transform.localScale.y * 1.55f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.10f;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            // 始终面向相机
            go.AddComponent<BillboardLabel>();
            // 让文字穿透遮挡显示
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
                mr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _veteranLabel = tm;
        }
        // 1 杀=★，3 杀=★★，5 杀+=★★★
        int starCount = Kills >= 5 ? 3 : Kills >= 3 ? 2 : 1;
        _veteranLabel.text = new string('★', starCount);
        // 颜色：1 星银 → 2 星金 → 3 星亮金（精英）
        Color col = starCount == 3 ? new Color(1f, 0.85f, 0.22f)
                  : starCount == 2 ? new Color(1f, 0.92f, 0.50f)
                  :                  new Color(0.85f, 0.88f, 0.95f);
        _veteranLabel.color = col;
    }

    public virtual void TakeDamage(int dmg)
    {
        if (_isDying) return;
        if (dmg > 0 && _techDamageReduction > 0f)
            dmg = Mathf.Max(1, Mathf.CeilToInt(dmg * (1f - _techDamageReduction)));
        int prev = CurrentHP;
        CurrentHP = Mathf.Clamp(CurrentHP - dmg, 0, MaxHP);
        if (CurrentHP == prev) return; // HP 无实际变化（如对满血单位治疗），跳过同步
        if (dmg > 0)
            GameManager.Instance?.RecordDamage(!bPlayerOwned, bPlayerOwned, prev - CurrentHP);
        healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
        if (dmg > 0)
        {
            StartCoroutine(HitFlash());
            bool crit = dmg >= MaxHP / 4;
            _visualAnimator?.TriggerHitReaction(crit);
            DamageNumber.Spawn(
                transform.position + Vector3.up * transform.localScale.y, dmg, crit, gameObject);
            // 受击火星粒子（命中点取单位中心）
            EffectsManager.PlayHitSpark(
                transform.position + Vector3.up * transform.localScale.y * 0.6f, crit);
        }
        // 联机 Host → Guest: 同步当前HP（含治疗）
        if (!NetSyncIncoming)
        {
            var _s = GameNetworkSync.Instance;
            if (_s != null && _s.IsNetworkGame && _s.IsHost && NetId != 0)
                _s.SendCmd($"{{\"action\":\"hp\",\"id\":{NetId},\"hp\":{CurrentHP},\"bldg\":0}}");
        }
        if (CurrentHP <= 0) OnDeath();
    }

    // 接收端直接设置HP（不触发重复同步）
    public void ForceSetHP(int hp)
    {
        NetSyncIncoming = true;
        TakeDamage(CurrentHP - Mathf.Max(0, hp));
        NetSyncIncoming = false;
    }

    public void ApplyTechBuff(string sourceId, float duration, float moveMultiplier, float damageMultiplier,
        float attackRangeBonus, float attackIntervalMultiplier, float defenseReduction, float sightRangeBonus,
        float regenPerSecond, Color tint)
    {
        if (IsDead() || duration <= 0f) return;
        if (_baseMaxHP <= 0) CaptureBaseStats();

        RuntimeTechBuff buff = new RuntimeTechBuff
        {
            SourceId = string.IsNullOrEmpty(sourceId) ? "Tech" : sourceId,
            EndTime = Time.time + duration,
            MoveMultiplier = moveMultiplier > 0.01f ? moveMultiplier : 1f,
            DamageMultiplier = damageMultiplier > 0.01f ? damageMultiplier : 1f,
            AttackRangeBonus = attackRangeBonus,
            AttackIntervalMultiplier = attackIntervalMultiplier > 0.01f ? attackIntervalMultiplier : 1f,
            DefenseReduction = Mathf.Max(0f, defenseReduction),
            SightRangeBonus = sightRangeBonus,
            RegenPerSecond = Mathf.Max(0f, regenPerSecond),
            Tint = tint.a > 0.01f ? tint : new Color(0.35f, 0.85f, 1f, 0.75f)
        };

        _techBuffs.Add(buff);
        RecalculateTechBuffStats();
        EnsureTechBuffRing(buff.Tint);
    }

    void RecalculateTechBuffStats()
    {
        if (_baseMaxHP <= 0) CaptureBaseStats();

        float moveMult = 1f;
        float damageMult = 1f;
        float intervalMult = 1f;
        float rangeBonus = 0f;
        float defense = 0f;
        float sightBonus = 0f;
        float regen = 0f;

        for (int i = 0; i < _techBuffs.Count; i++)
        {
            RuntimeTechBuff buff = _techBuffs[i];
            moveMult *= buff.MoveMultiplier;
            damageMult *= buff.DamageMultiplier;
            intervalMult *= buff.AttackIntervalMultiplier;
            rangeBonus += buff.AttackRangeBonus;
            defense += buff.DefenseReduction;
            sightBonus += buff.SightRangeBonus;
            regen += buff.RegenPerSecond;
        }

        MaxHP = _baseMaxHP;
        AttackDamage = Mathf.Max(1, Mathf.RoundToInt(_baseAttackDamage * damageMult));
        AttackRange = Mathf.Max(0.5f, _baseAttackRange + rangeBonus);
        AttackInterval = Mathf.Max(0.12f, _baseAttackInterval * intervalMult);
        SightRange = Mathf.Max(1f, _baseSightRange + sightBonus);
        MoveSpeed = Mathf.Clamp(_baseMoveSpeed * moveMult, 0.1f, _baseMoveSpeed * 3f);
        _techDamageReduction = Mathf.Clamp(defense, 0f, 0.75f);
        _techRegenPerSecond = regen;

        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        if (Agent != null && Agent.enabled)
            Agent.speed = MoveSpeed;

        healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);

        if (_techBuffs.Count == 0 && _techBuffRing != null)
        {
            Destroy(_techBuffRing);
            _techBuffRing = null;
            _techBuffRingRenderer = null;
            _techRegenCarry = 0f;
        }
    }

    void EnsureTechBuffRing(Color color)
    {
        if (_techBuffRing != null) return;

        float radius = Mathf.Max(0.75f, SelectionRingRadius * 1.22f);
        _techBuffRing = FxResources.MakeGroundDisc(transform, "TechBuffRing", radius, color,
            FxResources.DiscStyle.MediumRing, 0.075f);
        _techBuffRingRenderer = _techBuffRing.GetComponent<Renderer>();
    }

    void UpdateTechBuffRingVisual()
    {
        if (_techBuffs.Count == 0 || _techBuffRing == null) return;

        RuntimeTechBuff latest = _techBuffs[_techBuffs.Count - 1];
        float pulse = (Mathf.Sin(Time.time * 7.5f) + 1f) * 0.5f;
        float stackBoost = Mathf.Min(0.32f, (_techBuffs.Count - 1) * 0.055f);
        float radius = Mathf.Max(0.75f, SelectionRingRadius * (1.16f + stackBoost + pulse * 0.06f));
        _techBuffRing.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        if (_techBuffRingRenderer != null)
        {
            Color c = latest.Tint;
            c.a = Mathf.Lerp(0.34f, 0.62f, pulse);
            RendererColorUtil.TrySetColor(_techBuffRingRenderer, c);
        }
    }

    System.Collections.IEnumerator HitFlash()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) yield break;
        _flashCount++;
        var origColors = new Color[renderers.Length];
        var changed = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].material == null) continue;
            string n = renderers[i].gameObject.name;
            if (n == "FactionRing" || n == "FactionDot" || n == "SelectionRing" || n == "TechBuffRing" || n == "UnitLabel") continue;
            if (n.StartsWith("FactionStripe") || n.StartsWith("AircraftForwardStripe")) continue;
            Color color;
            if (!RendererColorUtil.TryGetColor(renderers[i], out color)) continue;
            origColors[i] = color;
            changed[i] = true;
            RendererColorUtil.TrySetColor(renderers[i], new Color(1f, 0.55f, 0.30f));
        }
        yield return new WaitForSeconds(0.07f);
        _flashCount--;
        if (!_isDying && _flashCount == 0)
            for (int i = 0; i < renderers.Length; i++)
                if (changed[i])
                    RendererColorUtil.TrySetColor(renderers[i], origColors[i]);
    }

    protected virtual void OnDeath()
    {
        if (_isDying) return;
        _isDying = true;
        _visualAnimator?.TriggerDeathAnimation();
        // 重型单位（PopCost≥2）死亡触发镜头震动
        if (PopCost >= 2)
            RTSCamera.Shake(0.5f + PopCost * 0.15f, 0.18f + PopCost * 0.03f);
        if (NetId != 0) NetIdTracker.Unregister(NetId);
        // 联机 Host → Guest: 广播死亡
        if (!NetSyncIncoming)
        {
            var _s = GameNetworkSync.Instance;
            if (_s != null && _s.IsNetworkGame && _s.IsHost && NetId != 0)
                _s.SendCmd($"{{\"action\":\"death\",\"id\":{NetId},\"bldg\":0}}");
        }
        GameManager.Instance?.UnregisterUnit(this);
        if (bPlayerOwned && RTSPlayerState.Instance != null)
            RTSPlayerState.Instance.PopUsed = Mathf.Max(0, RTSPlayerState.Instance.PopUsed - PopCost);
        // 从玩家选中列表移除，防止 HUD 访问已销毁对象
        RTSPlayerController.Instance?.RemoveFromSelection(this);
        if (healthBar != null) Destroy(healthBar.gameObject);
        SetSelected(false);
        // 停止寻路和碰撞，避免死亡动画期间仍参与游戏逻辑
        if (Agent != null) Agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        EffectsManager.PlayDeath(transform.position, PopCost);
        StartCoroutine(DeathEffect());
    }

    System.Collections.IEnumerator DeathEffect()
    {
        float dur = 0.35f, t = 0f;
        var renderers = GetComponentsInChildren<Renderer>();
        while (t < dur)
        {
            t += Time.deltaTime;
            float ratio = t / dur;
            Color dc = Color.Lerp(new Color(1f, 0.42f, 0.08f), new Color(0.1f, 0.04f, 0.04f), ratio);
            foreach (var r in renderers)
            {
                if (r == null || r.material == null) continue;
                string n = r.gameObject.name;
                if (n == "FactionRing" || n == "FactionDot" || n == "SelectionRing" || n == "TechBuffRing") continue;
                RendererColorUtil.TrySetColor(r, dc);
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // 寻找最近敌人
    protected RTSUnit FindNearestEnemy()
    {
        RTSUnit nearest = null;
        float minDist = SightRange;
        var all = GameManager.Instance?.GetAllUnits();
        if (all == null) return null;
        foreach (var u in all)
        {
            if (u == null || u.IsDead()) continue;
            if (u.bPlayerOwned == bPlayerOwned) continue; // 同阵营跳过
            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d < minDist) { minDist = d; nearest = u; }
        }
        return nearest;
    }

    // 执行命令
    public virtual void ApplyMoveCommand(Vector3 dest)
    {
        CurrentCommand = CommandType.Move;
        CommandTarget = dest;
        AttackTarget = null;
        buildingTarget = null;
        UseMoveStoppingDistance();
        if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(dest);
    }

    public void ApplyAttackCommand(RTSUnit target)
    {
        CurrentCommand = CommandType.Attack;
        AttackTarget = target;
        buildingTarget = null;
        UseCombatStoppingDistance();
    }

    public void ApplyAttackMoveCommand(Vector3 dest)
    {
        CurrentCommand = CommandType.AttackMove;
        CommandTarget = dest;
        buildingTarget = null;
        AttackTarget = null;
        _enemyScanTimer = 0f;   // 新命令时立即扫描
        _bldgScanTimer  = 0f;
        UseMoveStoppingDistance();
        if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(dest);
    }

    public void ApplyAttackBuildingCommand(RTSBuilding target)
    {
        if (target == null) return;
        CurrentCommand = CommandType.AttackMove;
        buildingTarget = target;
        AttackTarget = null;
        CommandTarget = target.transform.position;
        UseCombatStoppingDistance();
        if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(target.transform.position);
    }

    public void ApplyStopCommand()
    {
        CurrentCommand = CommandType.Stop;
        AttackTarget = null;
        SafeResetPath();
    }

    // 巡逻：在 A、B 两点之间往返，途中敌人进入 SightRange 自动反击
    private Vector3 _patrolA, _patrolB;
    private bool _patrolTowardsB = true;
    public void ApplyPatrolCommand(Vector3 a, Vector3 b)
    {
        _patrolA = a; _patrolB = b;
        _patrolTowardsB = true;
        CurrentCommand = CommandType.Patrol;
        AttackTarget = null;
        buildingTarget = null;
        CommandTarget = b;
        _enemyScanTimer = 0f;
        _bldgScanTimer = 0f;
        UseMoveStoppingDistance();
        if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(b);
    }

    void UpdatePatrol()
    {
        // 优先反击：附近有敌人 → 转入战斗（杀完后会回 None，下一帧用 _patrolPersist 重新进入巡逻）
        if (AttackTarget == null || AttackTarget.IsDead())
        {
            _enemyScanTimer -= Time.deltaTime;
            if (_enemyScanTimer <= 0f)
            {
                AttackTarget = FindNearestEnemy();
                _enemyScanTimer = 0.4f;
            }
        }
        if (AttackTarget != null && !AttackTarget.IsDead())
        {
            float dist = Vector3.Distance(transform.position, AttackTarget.transform.position);
            if (dist <= AttackRange)
            {
                SafeResetPath();
                Vector3 dir = (AttackTarget.transform.position - transform.position).normalized;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
                if (AttackTimer <= 0f) { DoAttack(AttackTarget); AttackTimer = AttackInterval; }
            }
            else if (dist <= SightRange)
            {
                UseCombatStoppingDistance();
                if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(AttackTarget.transform.position);
            }
            else
            {
                AttackTarget = null;
                ResumePatrolMove();
            }
            return;
        }
        // 无敌人 → 在 A/B 之间往返
        Vector3 target = _patrolTowardsB ? _patrolB : _patrolA;
        if (Agent != null && Agent.isOnNavMesh)
        {
            UseMoveStoppingDistance();
            if (Agent.destination != target) Agent.SetDestination(target);
            if (!Agent.pathPending && Agent.remainingDistance < 1.0f)
            {
                _patrolTowardsB = !_patrolTowardsB;
                Agent.SetDestination(_patrolTowardsB ? _patrolB : _patrolA);
            }
        }
        else
        {
            // 无 NavMeshAgent（飞行单位）：直线往返
            transform.position = Vector3.MoveTowards(transform.position, target, MoveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) < 1.0f)
                _patrolTowardsB = !_patrolTowardsB;
        }
    }

    void ResumePatrolMove()
    {
        Vector3 target = _patrolTowardsB ? _patrolB : _patrolA;
        UseMoveStoppingDistance();
        if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(target);
    }

    // 守卫：跟随目标友军 + 在守卫范围内反击靠近的敌人
    private RTSUnit _guardTarget;
    private const float GuardFollowDistance = 6f;   // 跟随保持的距离
    private const float GuardLeashRange = 18f;      // 距守卫者超过此值则放弃追击敌人，回守目标
    public void ApplyGuardCommand(RTSUnit ally)
    {
        if (ally == null || ally == this || ally.IsDead()) return;
        _guardTarget = ally;
        CurrentCommand = CommandType.Guard;
        AttackTarget = null;
        buildingTarget = null;
        _enemyScanTimer = 0f;
    }

    void UpdateGuard()
    {
        // 守卫目标死亡 → 终止
        if (_guardTarget == null || _guardTarget.IsDead())
        {
            _guardTarget = null;
            CurrentCommand = CommandType.None;
            SafeResetPath();
            return;
        }
        Vector3 guardPos = _guardTarget.transform.position;
        // 优先反击：守卫范围内的敌人
        if (AttackTarget == null || AttackTarget.IsDead())
        {
            _enemyScanTimer -= Time.deltaTime;
            if (_enemyScanTimer <= 0f)
            {
                AttackTarget = FindNearestEnemy();
                _enemyScanTimer = 0.4f;
            }
        }
        if (AttackTarget != null && !AttackTarget.IsDead())
        {
            float distToGuard = Vector3.Distance(transform.position, guardPos);
            float distToEnemy = Vector3.Distance(transform.position, AttackTarget.transform.position);
            // 离守卫目标过远 → 放弃敌人，回守
            if (distToGuard > GuardLeashRange)
            {
                AttackTarget = null;
            }
            else if (distToEnemy <= AttackRange)
            {
                SafeResetPath();
                Vector3 dir = (AttackTarget.transform.position - transform.position).normalized;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
                if (AttackTimer <= 0f) { DoAttack(AttackTarget); AttackTimer = AttackInterval; }
                return;
            }
            else if (distToEnemy <= SightRange)
            {
                UseCombatStoppingDistance();
                if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(AttackTarget.transform.position);
                return;
            }
            else
            {
                AttackTarget = null;
            }
        }
        // 跟随：若距离守卫目标 > GuardFollowDistance 则前往
        float d = Vector3.Distance(transform.position, guardPos);
        if (d > GuardFollowDistance)
        {
            UseMoveStoppingDistance();
            if (Agent != null && Agent.isOnNavMesh) Agent.SetDestination(guardPos);
        }
        else
        {
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh && !Agent.pathPending)
                SafeResetPath();
        }
    }

    // 属性访问器
    public int GetHP() => CurrentHP;
    public int GetMaxHP() => MaxHP;
    public RTSUnit GetAttackTarget() => AttackTarget;
    public bool IsDead() => CurrentHP <= 0;
    public bool IsPlayerOwned() => bPlayerOwned;
    public void SetOwnerPC(RTSPlayerController pc)
    {
        OwnerPC = pc; bPlayerOwned = true;
        // 更新阵营球颜色为蓝
        var fb = transform.Find("FactionBall");
        if (fb != null) { var r = fb.GetComponent<Renderer>(); if (r != null) RendererColorUtil.TrySetColor(r, new Color(0.25f, 0.55f, 0.95f)); }
        // 已变成己方，移除战雾隐藏（如有）
        var fh = GetComponent<FogHideable>();
        if (fh != null) { fh.SetVisible(true); Destroy(fh); }
    }
}
