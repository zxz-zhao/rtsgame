using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// RTS建筑基类
public class RTSBuilding : MonoBehaviour
{
    [Header("基础属性")]
    public string DisplayName = "建筑";
    public int MaxHP = 800;
    public int GoldCost = 300;
    public int PowerCost = 20;
    public int PopCapBonus = 0;
    public int PowerProvide = 0;
    public int BuildingLevel = 1;
    public bool bIsMainBase = false;
    public bool bIsPowerPlant = false;
    public bool bIsGoldMine = false;
    public bool bPlayerOwned = false;

    [Header("建造")]
    public float ConstructionTime = 12f;

    [Header("生产")]
    public GameObject[] ProductionUnits;      // 可生产的单位Prefab
    public float[] ProductionTimes;           // 对应生产时间
    public int[] ProductionCosts;             // 对应金币消耗

    [Header("收入")]
    public int GoldIncomeAmount = 0;
    public float GoldIncomeInterval = 5f;

    [Header("自动攻击（炮塔）")]
    public bool bAutoAttack = false;
    public float TurretRange = 20f;
    public int TurretDamage = 55;
    public float TurretInterval = 1.2f;

    // 运行时状态
    protected int CurrentHP;
    public bool bUnderConstruction = false;
    [System.NonSerialized] public int NetId = 0;  // 联机局唯一ID
    [System.NonSerialized] public bool NetSyncIncoming = false;
    protected RTSPlayerController OwnerPC;
    protected RTSPlayerState OwnerState;

    // 生产队列
    public List<int> ProductionQueue = new List<int>();
    public float ProductionProgress = 0f;
    public float ProductionDisplayProgress = 0f;
    public float ConstructionProgress = 0f;

    private float goldTimer = 0f;
    private float turretTimer = 0f;
    private float turretScanTimer = 0f;
    private float productionDisplayElapsed = 0f;
    private float productionDisplayTotal = 0f;
    private float productionDisplayFloor = 0f;
    private bool _ownerBonusesApplied = false;
    private bool _constructionRecorded = false;
    private ParticleSystem _constructionDustPS;
    private readonly Dictionary<Transform, Vector3> _constructionOriginalScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> _constructionOriginalPositions = new Dictionary<Transform, Vector3>();
    private const float TurretScanInterval = 0.5f;
    private RTSUnit turretTarget = null;
    private LineRenderer _turretLine;
    private bool _isDying = false;
    private Color _bodyColor;
    private int   _flashCount = 0;
    // 低血量脉动警告（同 RTSUnit）
    private Renderer[] _allRenderers;
    private Color[]    _origRenderColors;
    private bool       _lowHpPulseActive = false;

    // 子类 override 指定可视尺寸（米）。0 = 不做尺寸标定，沿用 prefab 原始尺寸。
    /// <summary>建筑期望可视高度（Y，米）。</summary>
    protected virtual float DesiredVisualHeight => 0f;
    /// <summary>建筑期望可视占地直径（X/Z 最大边，米）。</summary>
    protected virtual float DesiredVisualFootprint => 0f;

    protected virtual void Awake()
    {
        ApplyDefinitionDefaults();
        PrepareVisualScaleForRuntime();
    }

    /// <summary>应用建筑的静态建造数据。HUD 读取 prefab 元数据时也会调用，避免依赖 prefab 序列化旧值。</summary>
    public virtual void ApplyDefinitionDefaults() { }

    // 头顶血条
    private WorldHealthBar healthBar;
    // 头顶生产进度条（仅己方有生产能力的建筑）
    private WorldProductionBar productionBar;
    // 施工进度条（新放置建筑）
    private WorldProductionBar constructionBar;
    // 选中光环
    private GameObject selectionRing;
    // 生产中烟囱蒸汽粒子（仅己方生产建筑）
    private ParticleSystem _steamPS;
    // 受损黑烟（HP < 50% 时持续）
    private ParticleSystem _damageSmokePS;
    private const string BuildingLabelPrefix = "Label_";
    private const string BuildingLabelOutlinePrefix = "LabelOutline_";
    private const float BuildingLabelBaseWorldHeight = 2.75f;
    private const float BuildingLabelMinWorldHeight = 2.55f;
    private const float BuildingLabelMaxWorldHeight = 7.25f;
    private const float BuildingLabelFontSize = 60f;
    private const float BuildingLabelCharacterSize = 0.95f;
    private const float BuildingLabelMaxZoomMultiplier = 1.75f;
    private const float BuildingLabelZoomEasePower = 0.75f;
    private const float BuildingLabelScaleRefreshThreshold = 0.015f;
    private float _lastBuildingLabelZoomMultiplier = -1f;
    private static readonly Vector3[] BuildingLabelOutlineOffsets = new Vector3[]
    {
        new Vector3(-1.35f, 0f, 0f),
        new Vector3(1.35f, 0f, 0f),
        new Vector3(0f, -1.35f, 0f),
        new Vector3(0f, 1.35f, 0f),
        new Vector3(-0.95f, -0.95f, 0f),
        new Vector3(-0.95f, 0.95f, 0f),
        new Vector3(0.95f, -0.95f, 0f),
        new Vector3(0.95f, 0.95f, 0f)
    };
    private bool _started = false;

    protected virtual void Start()
    {
        ApplyDefinitionDefaults();
        _started = true;
        ConstructionTime = Mathf.Max(0.1f, ConstructionTime);
        ConstructionProgress = bUnderConstruction ? Mathf.Clamp01(ConstructionProgress) : 1f;
        CurrentHP = bUnderConstruction ? GetConstructionHPForProgress() : MaxHP;
        PrepareVisualScaleForRuntime();
        if (bPlayerOwned)
        {
            OwnerPC    = RTSPlayerController.Instance;
            OwnerState = RTSPlayerState.Instance;
            if (OwnerState != null)
            {
                if (!bUnderConstruction)
                    ApplyOwnerBonusesIfNeeded();
            }
        }
        GameManager.Instance?.RegisterBuilding(this);
        float topOffset = GetReadableTopOffset(bIsMainBase ? 6f : 4f);
        RefreshBuildingLabels(topOffset + 0.8f);
        // 创建头顶血条（建筑血条偏移更大）
        healthBar = WorldHealthBar.Create(transform, topOffset);
        healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
        // 己方有生产能力的建筑：头顶生产进度条
        if (bPlayerOwned && ProductionUnits != null && ProductionUnits.Length > 0)
        {
            productionBar = WorldProductionBar.Create(transform, 0.15f);  // 贴地平铺
            CreateSteamParticles(Mathf.Max(3.6f, topOffset - 0.7f));
        }
        // 所有建筑都创建受损烟雾粒子（玩家和敌方都需要"快毁了"的视觉提示）
        CreateDamageSmokeParticles(Mathf.Max(3.2f, topOffset - 1.0f));
        // 自动添加小地图标记
        var marker = gameObject.AddComponent<MinimapMarker>();
        marker.MarkerColor  = bPlayerOwned
            ? new Color(0.3f, 0.6f, 1f)        // 玩家建筑：蓝色
            : new Color(1f, 0.5f, 0.1f);       // 敌方建筑：橙色
        marker.MarkerSize   = bIsMainBase ? 20f : 14f;
        marker.HeightOffset = 80f;
        // 选中光环（建筑比单位大）
        selectionRing = CreateSelectionRing(transform, GetFootprintRingRadius());
        // 敌方建筑：挂战雾隐藏组件（视野外不显示）
        if (!bPlayerOwned && GetComponent<FogHideable>() == null)
            gameObject.AddComponent<FogHideable>();
        // 炮塔攻击连线
        if (bAutoAttack) _turretLine = CreateTurretLine();
        // 记录初始颜色
        var _rend = GetComponent<Renderer>();
        Color bodyColor;
        if (_rend != null && RendererColorUtil.TryGetColor(_rend, out bodyColor))
            _bodyColor = bodyColor;
        // 军事配色：己方军绿，敌方沙漠黄
        ApplyMilitaryTint();
        if (bUnderConstruction)
            PrepareConstructionVisuals(Mathf.Max(2.4f, topOffset - 1.1f));
    }

    protected virtual void ApplyMilitaryTint()
    {
        Color tint = bPlayerOwned
            ? new Color(0.32f, 0.40f, 0.22f)   // 己方：军绿灰
            : new Color(0.58f, 0.44f, 0.26f);   // 敌方：沙漠棕
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", tint);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            string n = r.gameObject.name;
            // 排除所有地面效果圆盘、UI 元素、伤害贴花
            if (IsBuildingLabelRenderer(r)) continue;
            if (n == "SelectionRing" || n == "HPLabel" || n == "DamageStain") continue;
            if (n == "HealAura" || n == "GroundDisc" || n == "RangeDisc"
                || n == "AoEDisc" || n == "FogDisc" || n == "ScanDisc") continue;
            // 排除父节点名称为 HealAura 的所有子渲染器
            Transform cur = r.transform;
            bool skip = false;
            while (cur != null && cur != transform)
            {
                if (cur.name == "HealAura" || cur.name.EndsWith("Disc")
                    || cur.name.EndsWith("Aura") || cur.name.EndsWith("Ring"))
                { skip = true; break; }
                cur = cur.parent;
            }
            if (skip) continue;
            r.SetPropertyBlock(block);
        }
    }

    public void PrepareVisualScaleForRuntime()
    {
        // 先清理违和的小装饰（旗子、瞄准镜、子弹、油桶、散件箱），避免它们影响后续缩放包围盒计算
        UnitVisualPolish.Polish(gameObject);
        HideLegacyGeneratedVisualsIfExternalModelPresent();
        NormalizeBuildingVisualScale();
        // 子类显式指定目标尺寸时，强制把可视模型标定到统一比例（解决各 Kenney prefab 尺寸杂乱）
        if (DesiredVisualHeight > 0f && DesiredVisualFootprint > 0f)
            UnitScaleNormalizer.Normalize(transform, DesiredVisualHeight, DesiredVisualFootprint);
    }

    void NormalizeBuildingVisualScale()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Bounds visualBounds;
        if (!TryGetVisualBounds(out visualBounds)) return;

        float currentFootprint = Mathf.Max(visualBounds.size.x, visualBounds.size.z);
        float targetFootprint = Mathf.Max(col.bounds.size.x, col.bounds.size.z) * 0.56f;
        if (currentFootprint <= 0.01f || currentFootprint >= targetFootprint * 0.88f)
            return;

        float scale = Mathf.Clamp(targetFootprint / currentFootprint, 1f, 1.85f);
        foreach (Transform child in transform)
        {
            if (ShouldIgnoreVisualChild(child)) continue;
            Vector3 p = child.localPosition;
            child.localPosition = new Vector3(p.x * scale, p.y * scale, p.z * scale);
            child.localScale *= scale;
        }
    }

    bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
            if (ShouldIgnoreVisualChild(renderer.transform)) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return hasBounds;
    }

    bool ShouldIgnoreVisualChild(Transform child)
    {
        if (child == null) return true;
        string n = child.name;
        return IsBuildingLabelTransform(child)
            || n == "HealthBar"
            || n == "ProductionBar"
            || n == "SelectionRing"
            || n == "MinimapDot";
    }

    void HideLegacyGeneratedVisualsIfExternalModelPresent()
    {
        Transform visualRoot = UnitScaleNormalizer.ResolveVisualRoot(transform);
        if (visualRoot == null || !HasExternalBuildingVisual(visualRoot)) return;

        foreach (Transform child in visualRoot)
        {
            if (child == null || !IsLegacyGeneratedBuildingPart(child.name)) continue;
            child.gameObject.SetActive(false);
        }
    }

    bool HasExternalBuildingVisual(Transform visualRoot)
    {
        var children = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child == null || child == visualRoot) continue;
            if (child.name.StartsWith("WW2", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    bool IsLegacyGeneratedBuildingPart(string name)
    {
        switch (name)
        {
            case "Platform":
            case "Body":
            case "Roof":
            case "Tower":
            case "Dome":
            case "Hangar":
            case "Antenna":
            case "Stripe":
            case "Bay":
            case "Door":
            case "Pole":
            case "Flag":
            case "Chimney":
            case "Chimney1":
            case "Chimney2":
            case "CoolerL":
            case "CoolerR":
            case "SteamL":
            case "SteamR":
            case "Drill":
            case "OreA":
            case "OreB":
            case "OreC":
            case "Base":
            case "BarrelL":
            case "BarrelR":
            case "Radar":
                return true;
            default:
                return name.StartsWith("Win", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    float GetReadableTopOffset(float fallback)
    {
        Bounds visualBounds;
        if (!TryGetVisualBounds(out visualBounds)) return fallback;
        float top = visualBounds.max.y - transform.position.y + 0.85f;
        return Mathf.Clamp(top, fallback, bIsMainBase ? 8.4f : 6.8f);
    }

    float GetFootprintRingRadius()
    {
        var col = GetComponent<Collider>();
        if (col == null) return bIsMainBase ? 5.4f : 3.8f;
        float radius = Mathf.Max(col.bounds.size.x, col.bounds.size.z) * 0.55f;
        return Mathf.Clamp(radius, bIsMainBase ? 5.2f : 3.2f, bIsMainBase ? 7.2f : 5.6f);
    }

    void RefreshBuildingLabels(float yOffset)
    {
        foreach (Transform child in transform)
        {
            if (child == null || !child.name.StartsWith(BuildingLabelPrefix)) continue;
            child.localPosition = new Vector3(0f, yOffset, 0f);
            var text = child.GetComponent<TextMesh>();
            if (text != null)
                StyleBuildingLabel(child, text);
        }
    }

    void StyleBuildingLabel(Transform labelRoot, TextMesh text)
    {
        text.fontStyle = FontStyle.Bold;
        text.fontSize = Mathf.Max(text.fontSize, Mathf.RoundToInt(BuildingLabelFontSize));
        text.characterSize = BuildingLabelCharacterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = GetReadableBuildingLabelColor(text.color);
        ApplyBuildingLabelScale(labelRoot);
        ConfigureBuildingLabelRenderer(text, 31);
        EnsureBuildingLabelOutline(labelRoot, text);
    }

    void ApplyBuildingLabelScale(Transform labelRoot)
    {
        if (labelRoot == null) return;

        float worldHeight = BuildingLabelBaseWorldHeight
            * GetBuildingLabelScaleMultiplier()
            * GetBuildingLabelCameraZoomMultiplier();
        worldHeight = Mathf.Clamp(worldHeight, BuildingLabelMinWorldHeight, BuildingLabelMaxWorldHeight);

        Vector3 parentScale = transform.lossyScale;
        labelRoot.localScale = new Vector3(
            SafeDivide(worldHeight, BuildingLabelFontSize * BuildingLabelCharacterSize * Mathf.Max(0.001f, parentScale.x)),
            SafeDivide(worldHeight, BuildingLabelFontSize * BuildingLabelCharacterSize * Mathf.Max(0.001f, parentScale.y)),
            SafeDivide(worldHeight, BuildingLabelFontSize * BuildingLabelCharacterSize * Mathf.Max(0.001f, parentScale.z)));
    }

    float GetBuildingLabelScaleMultiplier()
    {
        Bounds visualBounds;
        if (!TryGetVisualBounds(out visualBounds))
            return bIsMainBase ? 1.35f : 1f;

        float footprint = Mathf.Max(visualBounds.size.x, visualBounds.size.z);
        float height = visualBounds.size.y;
        float sizeScore = Mathf.Max(footprint / 7.5f, height / 5.2f);
        return Mathf.Clamp(Mathf.Lerp(0.95f, 1.55f, sizeScore), 0.95f, bIsMainBase ? 1.62f : 1.45f);
    }

    float GetBuildingLabelCameraZoomMultiplier()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return 1f;

        float zoomT;
        var rtsCamera = RTSCamera.Instance;
        if (cam.orthographic)
        {
            float minSize = rtsCamera != null ? Mathf.Max(2f, rtsCamera.MinY) : 2f;
            float maxSize = rtsCamera != null ? Mathf.Max(minSize, rtsCamera.MaxY) : Mathf.Max(minSize, cam.orthographicSize);
            zoomT = Mathf.InverseLerp(minSize, maxSize, cam.orthographicSize);
        }
        else
        {
            float minY = rtsCamera != null ? rtsCamera.MinY : 10f;
            float maxY = rtsCamera != null ? Mathf.Max(minY, rtsCamera.MaxY) : 200f;
            zoomT = Mathf.InverseLerp(minY, maxY, cam.transform.position.y);
        }

        zoomT = Mathf.Pow(Mathf.Clamp01(zoomT), BuildingLabelZoomEasePower);
        return Mathf.Lerp(1f, BuildingLabelMaxZoomMultiplier, zoomT);
    }

    void UpdateBuildingLabelScalesForCamera()
    {
        float zoomMultiplier = GetBuildingLabelCameraZoomMultiplier();
        if (_lastBuildingLabelZoomMultiplier > 0f
            && Mathf.Abs(zoomMultiplier - _lastBuildingLabelZoomMultiplier) < BuildingLabelScaleRefreshThreshold)
            return;

        _lastBuildingLabelZoomMultiplier = zoomMultiplier;
        foreach (Transform child in transform)
        {
            if (child == null || !child.name.StartsWith(BuildingLabelPrefix)) continue;
            ApplyBuildingLabelScale(child);
        }
    }

    static float SafeDivide(float numerator, float denominator)
    {
        return denominator > 0.0001f ? numerator / denominator : numerator;
    }

    void EnsureBuildingLabelOutline(Transform labelRoot, TextMesh source)
    {
        for (int i = 0; i < BuildingLabelOutlineOffsets.Length; i++)
        {
            string outlineName = BuildingLabelOutlinePrefix + i;
            Transform outline = labelRoot.Find(outlineName);
            if (outline == null)
            {
                var outlineGO = new GameObject(outlineName);
                outlineGO.transform.SetParent(labelRoot, false);
                outline = outlineGO.transform;
            }

            outline.localPosition = BuildingLabelOutlineOffsets[i];
            outline.localRotation = Quaternion.identity;
            outline.localScale = Vector3.one;

            var outlineText = outline.GetComponent<TextMesh>();
            if (outlineText == null)
                outlineText = outline.gameObject.AddComponent<TextMesh>();

            outlineText.text = source.text;
            outlineText.font = source.font;
            outlineText.fontSize = source.fontSize;
            outlineText.characterSize = source.characterSize;
            outlineText.anchor = source.anchor;
            outlineText.alignment = source.alignment;
            outlineText.fontStyle = source.fontStyle;
            outlineText.lineSpacing = source.lineSpacing;
            outlineText.tabSize = source.tabSize;
            outlineText.richText = source.richText;
            outlineText.color = new Color(0.015f, 0.012f, 0.01f, 0.95f);
            ConfigureBuildingLabelRenderer(outlineText, 30);
        }
    }

    Color GetReadableBuildingLabelColor(Color color)
    {
        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (max > 0.001f && max < 0.9f)
        {
            float boost = 0.9f / max;
            color.r = Mathf.Clamp01(color.r * boost);
            color.g = Mathf.Clamp01(color.g * boost);
            color.b = Mathf.Clamp01(color.b * boost);
        }
        color.a = 1f;
        return color;
    }

    void ConfigureBuildingLabelRenderer(TextMesh text, int sortingOrder)
    {
        var renderer = text.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var material = renderer.material;
        if (material == null) return;
        material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        material.renderQueue = 4000;
    }

    bool IsBuildingLabelRenderer(Renderer renderer)
    {
        return renderer != null && IsBuildingLabelTransform(renderer.transform);
    }

    bool IsBuildingLabelTransform(Transform node)
    {
        Transform cur = node;
        while (cur != null && cur != transform)
        {
            string n = cur.name;
            if (n.StartsWith(BuildingLabelPrefix) || n.StartsWith(BuildingLabelOutlinePrefix))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    // Guest 翻转归属后补注电力/人口/Owner 引用
    public void ReinitAfterOwnershipFlip()
    {
        if (!bPlayerOwned) return;
        OwnerPC    = RTSPlayerController.Instance;
        OwnerState = RTSPlayerState.Instance;
        if (OwnerState != null && !bUnderConstruction)
            ApplyOwnerBonusesIfNeeded();
        // 小地图标记颜色也同步更新
        var marker = GetComponent<MinimapMarker>();
        if (marker != null) marker.MarkerColor = new Color(0.3f, 0.6f, 1f);
        // 血条颜色刷新
        healthBar?.SetHP(CurrentHP, MaxHP, false);
        // 已变成己方，移除战雾隐藏（如有）
        var fh = GetComponent<FogHideable>();
        if (fh != null) { fh.SetVisible(true); Destroy(fh); }
    }

    public void BeginConstruction(float duration = -1f)
    {
        ApplyDefinitionDefaults();
        ConstructionTime = duration > 0f ? duration : GetDefaultConstructionTime();
        ConstructionProgress = 0f;
        bUnderConstruction = true;
        _constructionRecorded = false;
        if (ProductionQueue == null) ProductionQueue = new List<int>();
        ProductionQueue.Clear();
        ProductionProgress = 0f;
        ResetProductionDisplayProgress();

        if (_started)
        {
            CurrentHP = GetConstructionHPForProgress();
            healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
            PrepareConstructionVisuals(GetReadableTopOffset(bIsMainBase ? 6f : 4f));
        }
    }

    float GetDefaultConstructionTime()
    {
        if (ConstructionTime > 0f) return ConstructionTime;
        return Mathf.Clamp(GoldCost / 30f, 8f, 18f);
    }

    int GetConstructionHPForProgress()
    {
        float ratio = Mathf.Lerp(0.18f, 1f, Mathf.Clamp01(ConstructionProgress));
        return Mathf.Clamp(Mathf.RoundToInt(MaxHP * ratio), 1, MaxHP);
    }

    void ApplyOwnerBonusesIfNeeded()
    {
        if (_ownerBonusesApplied || !bPlayerOwned || OwnerState == null) return;
        OwnerState.PopCap += PopCapBonus;
        if (bIsPowerPlant) OwnerState.PowerCap += PowerProvide;
        else if (PowerCost > 0) OwnerState.PowerUsed += PowerCost;
        _ownerBonusesApplied = true;
    }

    void RemoveOwnerBonusesIfNeeded()
    {
        if (!_ownerBonusesApplied || !bPlayerOwned || OwnerState == null) return;
        OwnerState.PopCap -= PopCapBonus;
        if (bIsPowerPlant) OwnerState.PowerCap -= PowerProvide;
        else if (PowerCost > 0) OwnerState.PowerUsed -= PowerCost;
        _ownerBonusesApplied = false;
    }

    LineRenderer CreateTurretLine()
    {
        var lr = gameObject.AddComponent<LineRenderer>();
        var sh = Shader.Find("Unlit/Color");
        lr.material = sh != null ? new Material(sh) : new Material(Shader.Find("Standard"));
        RendererColorUtil.TrySetColor(lr.material, bPlayerOwned ? new Color(0.4f, 1f, 0.55f) : new Color(1f, 0.35f, 0.1f));
        lr.startWidth = 0.18f; lr.endWidth = 0.06f;
        lr.positionCount = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    System.Collections.IEnumerator FlashTurretLine(Vector3 targetPos)
    {
        if (_turretLine == null) yield break;
        Vector3 origin = GetTurretAttackOrigin();
        Vector3 impact = targetPos + Vector3.up * 1f;
        _turretLine.SetPosition(0, origin);
        _turretLine.SetPosition(1, impact);
        _turretLine.enabled = true;
        Vector3 dir = (impact - origin).normalized;
        EffectsManager.PlayMuzzleFlash(origin, GetTurretAttackRotation(dir), transform);
        SpawnTurretProjectile(origin, impact);
        yield return new WaitForSeconds(0.12f);
        if (_turretLine != null) _turretLine.enabled = false;
    }

    void SpawnTurretProjectile(Vector3 origin, Vector3 impact)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Projectiles/BattleProjectile_Shell");
        if (prefab == null) return;

        GameObject projectile = Instantiate(prefab, origin, Quaternion.identity);
        var visual = projectile.GetComponent<CombatProjectile>();
        if (visual == null)
            visual = projectile.AddComponent<CombatProjectile>();

        Color tint = bPlayerOwned ? new Color(0.42f, 1f, 0.62f, 1f) : new Color(1f, 0.38f, 0.08f, 1f);
        visual.Initialize(origin, impact, tint, 55f, 1.4f, 0.9f);
    }

    Vector3 GetTurretAttackOrigin()
    {
        Transform hardpoint = FindTurretHardpoint(transform);
        if (hardpoint != null)
            return hardpoint.position + hardpoint.forward * 0.15f;
        return transform.position + Vector3.up * (transform.localScale.y * 0.5f + 0.5f);
    }

    Quaternion GetTurretAttackRotation(Vector3 fallbackDirection)
    {
        Transform hardpoint = FindTurretHardpoint(transform);
        if (hardpoint != null)
            return hardpoint.rotation;
        if (fallbackDirection.sqrMagnitude < 0.0001f)
            fallbackDirection = transform.forward;
        return Quaternion.LookRotation(fallbackDirection.normalized, Vector3.up);
    }

    Transform FindTurretHardpoint(Transform root)
    {
        string[] priorityNames =
        {
            "Muzzle", "WW2TurretCannon", "KenneyCannon", "Barrel"
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

    static GameObject CreateSelectionRing(Transform parent, float radius)
    {
        // 金色（与 HUD 金边一致）；改用 FX 工厂的 Quad+圆环贴图，2 三角形 vs 旧 Cylinder ~80 三角形
        var ring = FxResources.MakeGroundDisc(parent, "SelectionRing", radius,
            new Color(0.95f, 0.78f, 0.25f, 1f), FxResources.DiscStyle.ThinRing, 0.04f);
        ring.AddComponent<_SelectionRingPulse>();
        ring.SetActive(false);
        return ring;
    }

    public void SetSelected(bool selected)
    {
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

    protected virtual void Update()
    {
        if (bUnderConstruction)
        {
            UpdateConstruction();
            return;
        }

        UpdateProduction();
        UpdateIncome();
        if (bAutoAttack) UpdateTurret();
        // 同步头顶生产进度条
        if (productionBar != null)
        {
            int qc = ProductionQueue != null ? ProductionQueue.Count : 0;
            string unitName = null;
            if (qc > 0 && ProductionUnits != null)
            {
                int idx = ProductionQueue[0];
                if (idx >= 0 && idx < ProductionUnits.Length && ProductionUnits[idx] != null)
                    unitName = ProductionUnits[idx].name;
            }
            productionBar.SetProgress(ProductionDisplayProgress, qc, unitName);
        }
        // 蒸汽粒子：有生产队列时持续发射
        if (_steamPS != null)
        {
            bool busy = ProductionQueue != null && ProductionQueue.Count > 0;
            var emission = _steamPS.emission;
            emission.enabled = busy;
            if (busy && !_steamPS.isPlaying) _steamPS.Play();
            else if (!busy && _steamPS.isPlaying) _steamPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        // 受损黑烟：HP < 50% 时启动，越低越浓
        if (_damageSmokePS != null && MaxHP > 0)
        {
            float ratio = (float)CurrentHP / MaxHP;
            bool damaged = ratio < 0.5f && CurrentHP > 0;
            var emission = _damageSmokePS.emission;
            emission.enabled = damaged;
            if (damaged)
            {
                // HP 50% → 4 颗/s，HP 0% → 14 颗/s
                emission.rateOverTime = Mathf.Lerp(14f, 4f, ratio / 0.5f);
                if (!_damageSmokePS.isPlaying) _damageSmokePS.Play();
            }
            else if (_damageSmokePS.isPlaying)
                _damageSmokePS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        UpdateLowHpPulse();
    }

    protected virtual void LateUpdate()
    {
        UpdateBuildingLabelScalesForCamera();
    }

    void UpdateConstruction()
    {
        if (_isDying) return;

        ConstructionTime = Mathf.Max(0.1f, ConstructionTime);
        float prev = Mathf.Clamp01(ConstructionProgress);
        ConstructionProgress = Mathf.Clamp01(ConstructionProgress + Time.deltaTime / ConstructionTime);

        int constructionHp = GetConstructionHPForProgress();
        if (CurrentHP < constructionHp || Mathf.Approximately(prev, 0f))
        {
            CurrentHP = Mathf.Clamp(constructionHp, 1, MaxHP);
            healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
        }

        UpdateConstructionVisuals();
        constructionBar?.SetProgress(ConstructionProgress, 1, DisplayName);

        if (ConstructionProgress >= 1f)
            CompleteConstruction();
    }

    void CompleteConstruction()
    {
        if (!bUnderConstruction) return;

        bUnderConstruction = false;
        ConstructionProgress = 1f;
        CurrentHP = MaxHP;
        RestoreConstructionVisuals();
        healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
        constructionBar?.SetProgress(0f, 0, null);
        if (constructionBar != null)
        {
            Destroy(constructionBar.gameObject);
            constructionBar = null;
        }
        if (_constructionDustPS != null)
        {
            _constructionDustPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Destroy(_constructionDustPS.gameObject, 1.5f);
            _constructionDustPS = null;
        }

        if (bPlayerOwned)
        {
            OwnerPC = RTSPlayerController.Instance;
            OwnerState = RTSPlayerState.Instance;
            ApplyOwnerBonusesIfNeeded();
        }

        if (!_constructionRecorded)
        {
            GameManager.Instance?.RecordBuildingConstructed(bPlayerOwned);
            _constructionRecorded = true;
        }

        ApplyMilitaryTint();
        EffectsManager.PlayBuildComplete(transform.position + Vector3.up * 0.7f);
        var marker = GetComponent<MinimapMarker>();
        if (marker != null) marker.Pulse(1.0f, 2.0f);
    }

    void PrepareConstructionVisuals(float heightHint)
    {
        if (constructionBar == null)
            constructionBar = WorldProductionBar.Create(transform, 0.20f);
        constructionBar.SetProgress(ConstructionProgress, 1, DisplayName);

        if (_constructionDustPS == null)
            CreateConstructionDustParticles(Mathf.Max(0.8f, heightHint * 0.18f));
        if (_constructionDustPS != null && !_constructionDustPS.isPlaying)
            _constructionDustPS.Play();

        CacheConstructionTransforms();
        UpdateConstructionVisuals();
    }

    void CacheConstructionTransforms()
    {
        _constructionOriginalScales.Clear();
        _constructionOriginalPositions.Clear();
        foreach (Transform child in transform)
        {
            if (ShouldIgnoreConstructionChild(child)) continue;
            _constructionOriginalScales[child] = child.localScale;
            _constructionOriginalPositions[child] = child.localPosition;
        }
    }

    bool ShouldIgnoreConstructionChild(Transform child)
    {
        if (child == null) return true;
        if (ShouldIgnoreVisualChild(child)) return true;
        string n = child.name;
        return n == "DamageSmoke"
            || n == "ProductionSteam"
            || n == "ConstructionDust"
            || n == "PlacementFootprint";
    }

    void UpdateConstructionVisuals()
    {
        float r = Mathf.Clamp01(ConstructionProgress);
        if (_constructionOriginalScales.Count == 0)
            CacheConstructionTransforms();

        foreach (var kv in _constructionOriginalScales)
        {
            Transform child = kv.Key;
            if (child == null) continue;
            Vector3 scale = kv.Value;
            Vector3 pos = _constructionOriginalPositions.TryGetValue(child, out var p) ? p : child.localPosition;
            float vertical = Mathf.Lerp(0.38f, 1f, r);
            float footprint = Mathf.Lerp(0.86f, 1f, r);
            child.localScale = new Vector3(scale.x * footprint, scale.y * vertical, scale.z * footprint);
            child.localPosition = new Vector3(pos.x, Mathf.Lerp(pos.y - 0.35f, pos.y, r), pos.z);
        }

        Color finishedTint = bPlayerOwned
            ? new Color(0.32f, 0.40f, 0.22f)
            : new Color(0.58f, 0.44f, 0.26f);
        Color tint = Color.Lerp(new Color(0.24f, 0.24f, 0.22f), finishedTint, Mathf.Lerp(0.25f, 1f, r));
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer) continue;
            if (IsBuildingLabelRenderer(renderer)) continue;
            if (renderer.gameObject.name == "SelectionRing" || renderer.gameObject.name == "MinimapDot") continue;
            RendererColorUtil.TrySetColor(renderer, tint);
        }

        if (_constructionDustPS != null)
        {
            var emission = _constructionDustPS.emission;
            emission.enabled = bUnderConstruction;
            emission.rateOverTime = Mathf.Lerp(10f, 3f, r);
        }
    }

    void RestoreConstructionVisuals()
    {
        foreach (var kv in _constructionOriginalScales)
        {
            if (kv.Key == null) continue;
            kv.Key.localScale = kv.Value;
            if (_constructionOriginalPositions.TryGetValue(kv.Key, out var pos))
                kv.Key.localPosition = pos;
        }
        _constructionOriginalScales.Clear();
        _constructionOriginalPositions.Clear();
    }

    void CreateConstructionDustParticles(float height)
    {
        var go = new GameObject("ConstructionDust");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = 1.0f;
        main.startSpeed = 0.9f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
        main.startColor = new Color(0.52f, 0.48f, 0.38f, 0.45f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 45;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = Mathf.Max(0.7f, GetFootprintRingRadius() * 0.35f);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.68f, 0.61f, 0.45f), 0f),
                new GradientColorKey(new Color(0.36f, 0.34f, 0.30f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.38f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = grad;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.85f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(sh);
            RendererColorUtil.TrySetColor(renderer.material, new Color(0.52f, 0.48f, 0.38f, 0.45f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 3;
        }

        _constructionDustPS = ps;
    }

    /// <summary>受损时屋顶冒持续黑烟。HP 越低越浓。</summary>
    void CreateDamageSmokeParticles(float topHeight)
    {
        var go = new GameObject("DamageSmoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, topHeight, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 2.4f;
        main.startSpeed = 1.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);
        main.gravityModifier = -0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.55f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.85f, 0.30f, 0.10f), 0f),  // 内部火光
                new GradientColorKey(new Color(0.18f, 0.18f, 0.18f), 0.4f),// 黑烟
                new GradientColorKey(new Color(0.10f, 0.10f, 0.10f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.15f),
                new GradientAlphaKey(0.50f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = grad;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.4f, 1.2f),
            new Keyframe(1f, 2.0f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var velOverLife = ps.velocityOverLifetime;
        velOverLife.enabled = true;
        velOverLife.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        velOverLife.y = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
        velOverLife.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(sh);
            RendererColorUtil.TrySetColor(renderer.material, new Color(0.18f, 0.18f, 0.18f, 0.9f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 5;
        }

        _damageSmokePS = ps;
    }

    /// <summary>HP &lt; 25% 时建筑整体脉动红光警告。</summary>
    void UpdateLowHpPulse()
    {
        if (_isDying || _flashCount > 0) return;
        bool shouldPulse = MaxHP > 0 && CurrentHP > 0 && (float)CurrentHP / MaxHP < 0.25f;

        if (shouldPulse && !_lowHpPulseActive)
        {
            CacheRenderersForPulse();
            _lowHpPulseActive = true;
        }
        else if (!shouldPulse && _lowHpPulseActive)
        {
            RestoreRendererColors();
            _lowHpPulseActive = false;
            return;
        }

        if (!_lowHpPulseActive || _allRenderers == null) return;

        float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
        Color warning = new Color(1f, 0.20f, 0.15f);
        for (int i = 0; i < _allRenderers.Length; i++)
        {
            if (_allRenderers[i] == null || _allRenderers[i].sharedMaterial == null) continue;
            string n = _allRenderers[i].gameObject.name;
            if (n == "SelectionRing") continue;
            if (IsBuildingLabelRenderer(_allRenderers[i])) continue;
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
        {
            if (_allRenderers[i] == null || IsBuildingLabelRenderer(_allRenderers[i])) continue;
            RendererColorUtil.TrySetColor(_allRenderers[i], _origRenderColors[i]);
        }
    }

    /// <summary>建筑顶部生产中冒蒸汽。世界空间发射，柔和灰白烟雾。</summary>
    void CreateSteamParticles(float topHeight)
    {
        var go = new GameObject("ProductionSteam");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, topHeight, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 1.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.55f);
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.enabled = false;            // 默认关，有队列时再打开
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.35f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.92f, 0.92f, 0.92f), 0f),
                new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.18f),
                new GradientAlphaKey(0.30f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = grad;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.4f, 1.0f),
            new Keyframe(1f, 1.6f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var velOverLife = ps.velocityOverLifetime;
        velOverLife.enabled = true;
        velOverLife.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        velOverLife.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        velOverLife.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(sh);
            RendererColorUtil.TrySetColor(renderer.material, new Color(0.85f, 0.85f, 0.85f, 0.55f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 4;
        }

        _steamPS = ps;
    }

    void UpdateProduction()
    {
        if (ProductionQueue == null || ProductionQueue.Count == 0 || ProductionUnits == null || ProductionUnits.Length == 0)
        {
            ResetProductionDisplayProgress();
            return;
        }

        EnsureProductionDisplayWork();
        int idx = ProductionQueue[0];
        if (idx < 0) { ProductionQueue.RemoveAt(0); RebuildProductionDisplayWorkFromQueue(); return; }

        float prodTime = GetProductionDuration(idx);
        float prevProgress = Mathf.Clamp01(ProductionProgress);
        ProductionProgress = Mathf.Clamp01(ProductionProgress + Time.deltaTime / prodTime);
        AdvanceProductionDisplayProgress((ProductionProgress - prevProgress) * prodTime);
        if (ProductionProgress >= 1f)
        {
            // 人口上限：等到有空位再出兵
            if (bPlayerOwned && OwnerState != null && ProductionUnits[idx] != null)
            {
                RTSUnit proto = ProductionUnits[idx].GetComponent<RTSUnit>();
                int pop = proto != null ? proto.PopCost : 1;
                if (!OwnerState.HasPopRoom(pop)) return;
            }
            if (!CanCompleteProductionUnit(idx)) return;
            SpawnUnit(idx);
            ProductionQueue.RemoveAt(0);
            ProductionProgress = 0f;
            if (ProductionQueue.Count == 0)
                ResetProductionDisplayProgress();
        }
    }

    float GetProductionDuration(int idx)
    {
        float prodTime = (ProductionTimes != null && idx >= 0 && idx < ProductionTimes.Length) ? ProductionTimes[idx] : 5f;
        return Mathf.Max(0.05f, prodTime);
    }

    void EnsureProductionDisplayWork()
    {
        if (productionDisplayTotal > 0f) return;
        RebuildProductionDisplayWorkFromQueue();
    }

    void AddProductionDisplayWork(int idx, bool resetFirst)
    {
        if (resetFirst)
        {
            ResetProductionDisplayProgress();
        }
        else
        {
            productionDisplayFloor = Mathf.Max(productionDisplayFloor, ProductionDisplayProgress);
        }

        productionDisplayTotal += GetProductionDuration(idx);
        RefreshProductionDisplayProgress();
    }

    void RemoveProductionDisplayWork(int idx)
    {
        productionDisplayTotal = Mathf.Max(0f, productionDisplayTotal - GetProductionDuration(idx));
        productionDisplayElapsed = Mathf.Min(productionDisplayElapsed, productionDisplayTotal);
        RefreshProductionDisplayProgress();
    }

    void AdvanceProductionDisplayProgress(float seconds)
    {
        if (productionDisplayTotal <= 0f) return;
        productionDisplayElapsed = Mathf.Clamp(productionDisplayElapsed + Mathf.Max(0f, seconds), 0f, productionDisplayTotal);
        RefreshProductionDisplayProgress();
    }

    void RefreshProductionDisplayProgress()
    {
        if (productionDisplayTotal <= 0f)
        {
            ProductionDisplayProgress = 0f;
            productionDisplayFloor = 0f;
            return;
        }

        float actualRatio = Mathf.Clamp01(productionDisplayElapsed / productionDisplayTotal);
        if (actualRatio >= productionDisplayFloor)
            productionDisplayFloor = 0f;
        ProductionDisplayProgress = Mathf.Max(actualRatio, productionDisplayFloor);
    }

    void ResetProductionDisplayProgress()
    {
        ProductionDisplayProgress = 0f;
        productionDisplayElapsed = 0f;
        productionDisplayTotal = 0f;
        productionDisplayFloor = 0f;
    }

    void RebuildProductionDisplayWorkFromQueue()
    {
        productionDisplayElapsed = 0f;
        productionDisplayTotal = 0f;
        productionDisplayFloor = 0f;

        if (ProductionQueue == null || ProductionQueue.Count == 0)
        {
            ProductionProgress = 0f;
            ProductionDisplayProgress = 0f;
            return;
        }

        for (int i = 0; i < ProductionQueue.Count; i++)
            productionDisplayTotal += GetProductionDuration(ProductionQueue[i]);

        int currentIdx = ProductionQueue[0];
        productionDisplayElapsed = GetProductionDuration(currentIdx) * Mathf.Clamp01(ProductionProgress);
        RefreshProductionDisplayProgress();
    }

    // 集结点（仅己方建筑使用）：新生产的单位 spawn 后自动 Move 到此处
    [System.NonSerialized] public Vector3 RallyPoint;
    [System.NonSerialized] public bool HasRallyPoint = false;
    private int _rallySpawnSlot = 0;

    public void SetRallyPoint(Vector3 worldPos)
    {
        RallyPoint = new Vector3(worldPos.x, 0f, worldPos.z);
        HasRallyPoint = true;
        _rallySpawnSlot = 0;
    }
    public void ClearRallyPoint() { HasRallyPoint = false; _rallySpawnSlot = 0; }

    void SpawnUnit(int idx)
    {
        if (ProductionUnits == null || idx >= ProductionUnits.Length) return;
        if (ProductionUnits[idx] == null) return;

        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        // 先在建筑旁出生（忽略 NavMesh 警告），下一帧用 Warp 强制贴 NavMesh
        Vector3 spawnPos = new Vector3(transform.position.x, 0f, transform.position.z) + fwd * 4f;
        GameObject go = Instantiate(ProductionUnits[idx], spawnPos, Quaternion.identity);
        RTSUnit unit = go.GetComponent<RTSUnit>();
        if (unit != null)
        {
            // 继承建筑归属（联机 Guest 生产 _E Prefab 时须翻转为己方）
            unit.bPlayerOwned = bPlayerOwned;
            GameManager.Instance?.RecordUnitProduced(bPlayerOwned);
            // 分配网络 ID
            int nid = NetIdTracker.NextLocalId();
            NetIdTracker.RegisterUnit(nid, unit);
            // 联机模式：持有方才广播 spawn（防止 remote 端产生重复单位）
            var sync = GameNetworkSync.Instance;
            if (sync != null && sync.IsNetworkGame && bPlayerOwned)
                sync.SendSpawn(nid, ProductionUnits[idx].name, spawnPos, true);
            if (bPlayerOwned)
            {
                unit.SetOwnerPC(OwnerPC);
                OwnerPC?.OnUnitSpawned(unit);
            }
            // 集结点：让新单位自动行进
            if (HasRallyPoint && bPlayerOwned)
                StartCoroutine(IssueRallyMoveNextFrame(unit, GetNextRallyMoveDestination()));
            OnUnitSpawnedFromProduction(unit, idx);
        }
        // 下一帧将 agent warp 到最近 NavMesh 点，消除 "not close enough" 警告
        var agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) StartCoroutine(WarpToNavMesh(agent, spawnPos));
        // 生产完成特效（己方才显示，避免 AI 大量生产时刷屏）
        if (bPlayerOwned)
        {
            EffectsManager.PlayBuildComplete(spawnPos + Vector3.up * 0.6f);
            // 小地图标记闪烁（等一帧让 RTSUnit.Start 添加 MinimapMarker）
            StartCoroutine(PulseMinimapNextFrame(go));
        }
    }

    IEnumerator PulseMinimapNextFrame(GameObject unitGO)
    {
        yield return null;
        if (unitGO == null) yield break;
        var marker = unitGO.GetComponent<MinimapMarker>();
        if (marker != null) marker.Pulse(1.0f, 2.2f);
    }

    IEnumerator IssueRallyMoveNextFrame(RTSUnit unit, Vector3 dest)
    {
        // 等 2 帧让 NavMeshAgent.Warp 完成
        yield return null;
        yield return null;
        if (unit == null || unit.IsDead()) yield break;
        unit.ApplyMoveCommand(dest);
        var sync = GameNetworkSync.Instance;
        if (sync != null && sync.IsNetworkGame && unit.NetId != 0)
            sync.SendMove(unit.NetId, dest);
    }

    Vector3 GetNextRallyMoveDestination()
    {
        Vector3 basePoint = RallyPoint;
        int slot = _rallySpawnSlot++ % 33;
        if (slot == 0)
            return SampleRallyNavMeshPoint(basePoint, basePoint);

        int ring = 1 + (slot - 1) / 8;
        int indexInRing = (slot - 1) % 8;
        float angle = (indexInRing / 8f) * Mathf.PI * 2f + ring * 0.35f;
        float radius = 1.6f * ring;
        Vector3 candidate = basePoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        return SampleRallyNavMeshPoint(candidate, basePoint);
    }

    Vector3 SampleRallyNavMeshPoint(Vector3 candidate, Vector3 fallback)
    {
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;
        if (NavMesh.SamplePosition(fallback, out hit, 5f, NavMesh.AllAreas))
            return hit.position;
        return fallback;
    }

    IEnumerator WarpToNavMesh(NavMeshAgent agent, Vector3 pos)
    {
        yield return null; // 等一帧
        if (agent != null && agent.enabled &&
            NavMesh.SamplePosition(pos, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    void UpdateIncome()
    {
        if (GoldIncomeAmount <= 0) return;
        goldTimer += Time.deltaTime;
        if (goldTimer >= GoldIncomeInterval)
        {
            goldTimer = 0f;
            if (OwnerState != null)
            {
                OwnerState.Gold += GoldIncomeAmount;
                GameManager.Instance?.RecordGoldIncome(bPlayerOwned, GoldIncomeAmount);
                // 玩家金矿弹出金色收入数字
                if (bPlayerOwned)
                {
                    DamageNumber.Spawn(
                        transform.position + Vector3.up * (transform.localScale.y + 1.5f),
                        -GoldIncomeAmount,
                        false,
                        null,
                        0.70f);  // 负值 → DamageNumber.Spawn 显示绿色 "+"
                }
            }
        }
    }

    void UpdateTurret()
    {
        turretTimer -= Time.deltaTime;
        if (turretTarget == null || turretTarget.IsDead())
        {
            turretTarget = null;
            turretScanTimer -= Time.deltaTime;
            if (turretScanTimer <= 0f)
            {
                turretTarget = FindNearestEnemy();
                turretScanTimer = TurretScanInterval;
            }
        }
        if (turretTarget != null)
        {
            // 炮塔平滑旋转面向目标（仅 Y 轴）
            Vector3 dir = turretTarget.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, 90f * Time.deltaTime);
            }
            if (turretTimer <= 0f)
            {
                StartCoroutine(FlashTurretLine(turretTarget.transform.position));
                turretTimer = TurretInterval;
                // 联机：只有 Host 计算炮塔伤害
                var _ts = GameNetworkSync.Instance;
                if (_ts == null || !_ts.IsNetworkGame || _ts.IsHost)
                    turretTarget.TakeDamage(TurretDamage);
            }
        }
    }

    RTSUnit FindNearestEnemy()
    {
        RTSUnit nearest = null;
        float minDist = TurretRange;
        var all = GameManager.Instance?.GetAllUnits();
        if (all == null) return null;
        foreach (var u in all)
        {
            if (u == null || u.IsDead()) continue;
            if (u.IsPlayerOwned() == bPlayerOwned) continue;
            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d < minDist) { minDist = d; nearest = u; }
        }
        return nearest;
    }

    // 添加到生产队列
    public bool EnqueueUnit(int idx)
    {
        if (bUnderConstruction) return false;
        if (ProductionUnits == null || idx < 0 || idx >= ProductionUnits.Length) return false;
        if (OwnerState == null) return false;
        if (ProductionQueue == null) ProductionQueue = new List<int>();
        int cost = (ProductionCosts != null && idx < ProductionCosts.Length) ? ProductionCosts[idx] : 0;
        if (OwnerState.Gold < cost) return false;
        bool wasIdle = ProductionQueue == null || ProductionQueue.Count == 0;
        // 已排队计入预占人口，防止无限排队
        if (bPlayerOwned && ProductionUnits[idx] != null)
        {
            RTSUnit proto = ProductionUnits[idx].GetComponent<RTSUnit>();
            int pop = proto != null ? proto.PopCost : 1;
            int queuedPop = 0;
            foreach (var qi in ProductionQueue)
            {
                if (qi >= 0 && qi < ProductionUnits.Length && ProductionUnits[qi] != null)
                {
                    RTSUnit qp = ProductionUnits[qi].GetComponent<RTSUnit>();
                    queuedPop += qp != null ? qp.PopCost : 1;
                }
            }
            if (OwnerState.PopUsed + queuedPop + pop > OwnerState.PopCap) return false;
        }
        if (!CanEnqueueProductionUnit(idx)) return false;
        OwnerState.Gold -= cost;
        GameManager.Instance?.RecordGoldSpent(bPlayerOwned, cost);
        ProductionQueue.Add(idx);
        AddProductionDisplayWork(idx, wasIdle);
        return true;
    }

    protected virtual bool CanEnqueueProductionUnit(int idx)
    {
        return true;
    }

    protected virtual bool CanCompleteProductionUnit(int idx)
    {
        return true;
    }

    protected virtual void OnUnitSpawnedFromProduction(RTSUnit unit, int idx)
    {
    }

    public void CancelLast()
    {
        if (ProductionQueue == null || ProductionQueue.Count == 0) return;
        bool cancellingCurrent = ProductionQueue.Count == 1;
        int idx = ProductionQueue[ProductionQueue.Count - 1];
        ProductionQueue.RemoveAt(ProductionQueue.Count - 1);
        if (cancellingCurrent)
        {
            ProductionProgress = 0f;
            ResetProductionDisplayProgress();
        }
        else
        {
            RemoveProductionDisplayWork(idx);
        }
        if (OwnerState != null && ProductionCosts != null && idx >= 0 && idx < ProductionCosts.Length)
        {
            OwnerState.Gold += ProductionCosts[idx];
            GameManager.Instance?.RecordGoldRefund(bPlayerOwned, ProductionCosts[idx]);
        }
    }

    public virtual void TakeDamage(int dmg)
    {
        if (_isDying) return;
        int prev = CurrentHP;
        CurrentHP = Mathf.Clamp(CurrentHP - dmg, 0, MaxHP);
        if (CurrentHP == prev) return; // HP 无实际变化，跳过
        if (dmg > 0)
            GameManager.Instance?.RecordDamage(!bPlayerOwned, bPlayerOwned, prev - CurrentHP);
        healthBar?.SetHP(CurrentHP, MaxHP, !bPlayerOwned);
        if (dmg > 0)
        {
            StartCoroutine(HitFlash());
            float top = transform.position.y + transform.localScale.y + 1f;
            bool crit = dmg >= MaxHP / 5;
            DamageNumber.Spawn(
                new Vector3(transform.position.x, top, transform.position.z), dmg, crit, gameObject);
            ApplyDamageStain((float)CurrentHP / MaxHP);
            // 己方主基地受攻：触发屏幕边缘红光警报
            if (bPlayerOwned && bIsMainBase && RTSHUD.Instance != null)
                RTSHUD.Instance.NotifyBaseUnderAttack();
        }
        // 联机 Host → Guest: 同步当前HP（含修复/治疗）
        if (!NetSyncIncoming)
        {
            var _s = GameNetworkSync.Instance;
            if (_s != null && _s.IsNetworkGame && _s.IsHost && NetId != 0)
                _s.SendCmd($"{{\"action\":\"hp\",\"id\":{NetId},\"hp\":{CurrentHP},\"bldg\":1}}");
        }
        if (CurrentHP <= 0) OnDestroyed();
    }

    void ApplyDamageStain(float hpRatio)
    {
        // 仅在低血量段生效，避免频繁调用
        if (hpRatio > 0.6f) return;
        // 暗化系数：0.6HP→1.0，0.0HP→0.45
        float dark = Mathf.Lerp(0.45f, 1.0f, hpRatio / 0.6f);
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || r.material == null) continue;
            if (r.gameObject.name == "SelectionRing") continue;
            if (IsBuildingLabelRenderer(r)) continue;
            Color c;
            if (RendererColorUtil.TryGetColor(r, out c))
                RendererColorUtil.TrySetColor(r, new Color(c.r * dark, c.g * dark, c.b * dark, c.a));
        }
    }

    System.Collections.IEnumerator HitFlash()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) yield break;
        _flashCount++;
        var origColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].material == null) continue;
            if (renderers[i].gameObject.name == "SelectionRing") continue;
            if (IsBuildingLabelRenderer(renderers[i])) continue;
            Color color;
            if (!RendererColorUtil.TryGetColor(renderers[i], out color)) continue;
            origColors[i] = color;
            RendererColorUtil.TrySetColor(renderers[i], new Color(1f, 0.82f, 0.5f));
        }
        yield return new WaitForSeconds(0.09f);
        _flashCount--;
        if (!_isDying && _flashCount == 0)
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].material == null) continue;
                if (IsBuildingLabelRenderer(renderers[i])) continue;
                RendererColorUtil.TrySetColor(renderers[i], origColors[i]);
            }
    }

    protected virtual void OnDestroyed()
    {
        if (_isDying) return;
        _isDying = true;
        if (NetId != 0) NetIdTracker.Unregister(NetId);
        // 联机 Host → Guest: 广播建筑死亡
        if (!NetSyncIncoming)
        {
            var _s = GameNetworkSync.Instance;
            if (_s != null && _s.IsNetworkGame && _s.IsHost && NetId != 0)
                _s.SendCmd($"{{\"action\":\"death\",\"id\":{NetId},\"bldg\":1}}");
        }
        RTSCamera.Shake(bIsMainBase ? 1.8f : 0.9f, bIsMainBase ? 0.45f : 0.28f);
        RemoveOwnerBonusesIfNeeded();
        // \u9000\u8fd8\u6392\u961f\u4e2d\u7684\u751f\u4ea7\u91d1\u5e01
        if (bPlayerOwned && OwnerState != null)
        {
            foreach (var qi in ProductionQueue)
                if (ProductionCosts != null && qi >= 0 && qi < ProductionCosts.Length)
                {
                    OwnerState.Gold += ProductionCosts[qi];
                    GameManager.Instance?.RecordGoldRefund(bPlayerOwned, ProductionCosts[qi]);
                }
        }
        ProductionQueue.Clear();
        ProductionProgress = 0f;
        ResetProductionDisplayProgress();
        // 敌方建筑被摘除计入击杀
        if (!bPlayerOwned) GameManager.Instance?.AddPlayerBuildingKill(DisplayName);
        GameManager.Instance?.UnregisterBuilding(this);
        if (healthBar != null) Destroy(healthBar.gameObject);
        if (productionBar != null) Destroy(productionBar.gameObject);
        if (constructionBar != null) Destroy(constructionBar.gameObject);
        SetSelected(false);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        // 大爆炸 + 碎片飞散
        Vector3 size = col != null ? col.bounds.size : Vector3.one * 3f;
        EffectsManager.PlayBuildingDeath(transform.position, size);
        SpawnDestructionDebris(size);
        // 屏幕闪光：主基地强（橙色 0.65），普通建筑弱（暖白 0.30）
        if (RTSHUD.Instance != null)
        {
            if (bIsMainBase)
                RTSHUD.Instance.FlashScreen(new Color(1f, 0.55f, 0.20f), 0.65f, 0.30f);
            else
                RTSHUD.Instance.FlashScreen(new Color(1f, 0.85f, 0.55f), 0.30f, 0.18f);
        }
        StartCoroutine(BuildingDeathEffect());
    }

    /// <summary>建筑炸毁时抛出下载资源里的砖瓦废墟，避免再出现方块碎片。</summary>
    void SpawnDestructionDebris(Vector3 size)
    {
        GameObject debrisPrefab = Resources.Load<GameObject>("Prefabs/Props/BattlefieldProp_BrickRubble");
        if (debrisPrefab == null)
        {
            Debug.LogWarning("[RTSBuilding] Downloaded brick rubble prefab missing; destruction debris skipped.");
            return;
        }

        int count = bIsMainBase ? 10 : 6;
        float footprint = Mathf.Max(size.x, size.z);
        for (int i = 0; i < count; i++)
        {
            var debris = Instantiate(debrisPrefab);
            debris.name = "BuildingDebris";
            float fragScale = Mathf.Clamp(footprint * 0.055f * Random.Range(0.65f, 1.15f), 0.45f, 1.25f);
            debris.transform.localScale = Vector3.one * fragScale;
            debris.transform.position = transform.position + Vector3.up * (size.y * 0.4f) +
                new Vector3(Random.Range(-0.5f, 0.5f) * size.x, 0f, Random.Range(-0.5f, 0.5f) * size.z);
            debris.transform.rotation = Random.rotation;

            // 物理：抛飞 + 旋转
            if (debris.GetComponent<Collider>() == null)
            {
                var debrisCollider = debris.AddComponent<SphereCollider>();
                debrisCollider.radius = 1.0f;
            }
            var rb = debris.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            Vector3 launch = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.7f, 1.7f),
                Random.Range(-1f, 1f)).normalized * Random.Range(4f, 8f);
            rb.AddForce(launch, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * Random.Range(8f, 16f), ForceMode.VelocityChange);

            foreach (var rd in debris.GetComponentsInChildren<Renderer>())
            {
                if (rd == null) continue;
                rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rd.receiveShadows = false;
            }
            // 自动销毁（连碎片淡出）
            debris.AddComponent<_DebrisFader>().Init(2.4f);
        }
    }

    System.Collections.IEnumerator BuildingDeathEffect()
    {
        float dur = 0.5f, t = 0f;
        Vector3 startScale = transform.localScale;
        var renderers = GetComponentsInChildren<Renderer>();
        while (t < dur)
        {
            t += Time.deltaTime;
            float ratio = t / dur;
            transform.localScale = startScale * (1f + ratio * 0.4f);
            Color dc = Color.Lerp(new Color(1f, 0.5f, 0.1f), new Color(0.06f, 0.03f, 0.03f), ratio);
            foreach (var r in renderers)
            {
                if (r == null || r.material == null) continue;
                if (r.gameObject.name == "SelectionRing") continue;
                RendererColorUtil.TrySetColor(r, dc);
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    public void ForceSetHP(int hp)
    {
        NetSyncIncoming = true;
        TakeDamage(CurrentHP - Mathf.Max(0, hp));
        NetSyncIncoming = false;
    }

    public int GetHP() => CurrentHP;
    public int GetMaxHP() => MaxHP;
}
