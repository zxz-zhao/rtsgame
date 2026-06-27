using System;
using System.Text;
using UnityEngine;

public class MainBase : RTSBuilding
{
    struct UpgradeRequirement
    {
        public readonly Type BuildingType;
        public readonly int RequiredCount;
        public readonly string DisplayName;

        public UpgradeRequirement(Type buildingType, int requiredCount, string displayName)
        {
            BuildingType = buildingType;
            RequiredCount = Mathf.Max(0, requiredCount);
            DisplayName = displayName ?? string.Empty;
        }
    }

    struct BaseLevelDefinition
    {
        public readonly int Level;
        public readonly int MaxHP;
        public readonly int PopCapBonus;
        public readonly int GoldIncomeAmount;
        public readonly int UpgradeCost;
        public readonly UpgradeRequirement[] Requirements;

        public BaseLevelDefinition(
            int level,
            int maxHP,
            int popCapBonus,
            int goldIncomeAmount,
            int upgradeCost,
            params UpgradeRequirement[] requirements)
        {
            Level = Mathf.Max(1, level);
            MaxHP = Mathf.Max(1, maxHP);
            PopCapBonus = Mathf.Max(0, popCapBonus);
            GoldIncomeAmount = Mathf.Max(0, goldIncomeAmount);
            UpgradeCost = Mathf.Max(0, upgradeCost);
            Requirements = requirements ?? Array.Empty<UpgradeRequirement>();
        }
    }

    struct BuildLimitDefinition
    {
        public readonly Type BuildingType;
        public readonly string DisplayName;
        public readonly int[] MaxCountsByBaseLevel;

        public BuildLimitDefinition(Type buildingType, string displayName, params int[] maxCountsByBaseLevel)
        {
            BuildingType = buildingType;
            DisplayName = displayName ?? string.Empty;
            MaxCountsByBaseLevel = maxCountsByBaseLevel ?? Array.Empty<int>();
        }

        public int GetMaxCount(int mainBaseLevel)
        {
            if (MaxCountsByBaseLevel == null || MaxCountsByBaseLevel.Length == 0)
                return int.MaxValue;

            int idx = Mathf.Clamp(mainBaseLevel, 1, MaxCountsByBaseLevel.Length) - 1;
            return Mathf.Max(0, MaxCountsByBaseLevel[idx]);
        }

        public int GetRequiredMainBaseLevel()
        {
            if (MaxCountsByBaseLevel == null)
                return 1;

            for (int i = 0; i < MaxCountsByBaseLevel.Length; i++)
                if (MaxCountsByBaseLevel[i] > 0)
                    return i + 1;

            return int.MaxValue;
        }
    }

    private static readonly BaseLevelDefinition[] LevelDefinitions =
    {
        new BaseLevelDefinition(
            1,
            3000,
            10,
            30,
            0),
        new BaseLevelDefinition(
            2,
            4200,
            15,
            40,
            800,
            new UpgradeRequirement(typeof(Barracks), 1, "兵工厂"),
            new UpgradeRequirement(typeof(GoldMine), 2, "金矿"),
            new UpgradeRequirement(typeof(PowerPlant), 2, "电厂")),
        new BaseLevelDefinition(
            3,
            5600,
            20,
            55,
            1400,
            new UpgradeRequirement(typeof(Barracks), 1, "兵工厂"),
            new UpgradeRequirement(typeof(GoldMine), 3, "金矿"),
            new UpgradeRequirement(typeof(PowerPlant), 3, "电厂"),
            new UpgradeRequirement(typeof(AirFactory), 1, "飞机厂"),
            new UpgradeRequirement(typeof(TankFactory), 1, "特需厂"),
            new UpgradeRequirement(typeof(ArmorFactory), 1, "坦克厂")),
    };

    private static readonly BuildLimitDefinition[] BuildLimitDefinitions =
    {
        new BuildLimitDefinition(typeof(Barracks), "兵工厂", 1, 2, 3),
        new BuildLimitDefinition(typeof(AirFactory), "飞机厂", 1, 2, 3),
        new BuildLimitDefinition(typeof(Airfield), "停机场", 1, 2, 3),
        new BuildLimitDefinition(typeof(TankFactory), "特需厂", 1, 2, 3),
        new BuildLimitDefinition(typeof(ArmorFactory), "坦克厂", 1, 2, 3),
        new BuildLimitDefinition(typeof(Turret), "炮塔", 2, 4, 6),
        new BuildLimitDefinition(typeof(GoldMine), "金矿", 2, 3, 4),
        new BuildLimitDefinition(typeof(PowerPlant), "电厂", 2, 3, 4),
        new BuildLimitDefinition(typeof(NavalYard), "鑸瑰潪", 1, 2, 3),
    };

    private const float HealInterval = 1f;
    private const int HealAmount = 8;
    private const int RepairAmount = 10;
    private const float HealRadius = 50f;
    public const float BuildRangeRadius = 60f;
    private const float RepairRadius = BuildRangeRadius;

    private float healTimer = 0f;
    private float _healPulseImpulse = 0f;
    private GameObject _healAura;
    private Material _healAuraMat;
    private Color _healAuraBaseColor;
    private float _healAuraVisibleTimer = 0f;

    protected override float DesiredVisualHeight => 10f;
    protected override float DesiredVisualFootprint => 12f;
    public override bool SupportsBuildingUpgrade => true;

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "主基地";
        GoldCost = 0;
        PowerCost = 0;
        PowerProvide = 0;
        bIsMainBase = true;
        bIsPowerPlant = false;
        bIsGoldMine = false;
        GoldIncomeInterval = 5f;
        bAutoAttack = false;

        ApplyLevelDefinition(Mathf.Max(1, BuildingLevel), false);
    }

    protected override void Start()
    {
        base.Start();
        if (bPlayerOwned)
            CreateHealAura();
    }

    public static bool CanConstructBuilding(RTSBuilding buildingTemplate, bool playerOwned, out string failureReason)
    {
        if (buildingTemplate == null)
        {
            failureReason = "缂哄皯寤虹瓚瀹氫箟";
            return false;
        }

        return CanConstructBuilding(
            buildingTemplate.GetType(),
            buildingTemplate.DisplayName,
            playerOwned,
            out failureReason);
    }

    public static bool CanConstructBuilding(Type buildingType, string displayName, bool playerOwned, out string failureReason)
    {
        return EvaluateBuildCapacity(buildingType, displayName, playerOwned, out _, out _, out _, out failureReason, true);
    }

    public static bool CanConstructBuilding(RTSBuilding buildingTemplate, bool playerOwned)
    {
        return buildingTemplate != null
            && EvaluateBuildCapacity(buildingTemplate.GetType(), buildingTemplate.DisplayName, playerOwned,
                out _, out _, out _, out _, false);
    }

    public static bool TryGetBuildingCountCapacity(RTSBuilding buildingTemplate, bool playerOwned,
        out int currentCount, out int maxCount, out int mainBaseLevel)
    {
        if (buildingTemplate == null)
        {
            currentCount = 0;
            maxCount = int.MaxValue;
            mainBaseLevel = 0;
            return false;
        }

        return EvaluateBuildCapacity(
            buildingTemplate.GetType(),
            buildingTemplate.DisplayName,
            playerOwned,
            out currentCount,
            out maxCount,
            out mainBaseLevel,
            out _,
            false);
    }

    static bool EvaluateBuildCapacity(Type buildingType, string displayName, bool playerOwned,
        out int currentCount, out int maxCount, out int mainBaseLevel, out string failureReason, bool needFailureReason)
    {
        currentCount = 0;
        maxCount = int.MaxValue;
        mainBaseLevel = 0;
        failureReason = null;

        if (buildingType == null)
        {
            if (needFailureReason)
                failureReason = "缂哄皯寤虹瓚绫诲瀷";
            return false;
        }

        if (!TryGetBuildLimitDefinition(buildingType, out BuildLimitDefinition limitDefinition))
            return true;

        string readableName = GetReadableBuildName(limitDefinition, buildingType, displayName);
        if (!TryGetFactionMainBaseLevel(playerOwned, out mainBaseLevel))
        {
            if (needFailureReason)
                failureReason = playerOwned ? "主基地已失效，无法继续建造" : "敌方主基地已失效，无法继续建造";
            return false;
        }

        maxCount = limitDefinition.GetMaxCount(mainBaseLevel);
        currentCount = CountOwnedBuildings(buildingType, playerOwned);
        if (currentCount < maxCount)
            return true;

        int requiredLevel = limitDefinition.GetRequiredMainBaseLevel();
        if (maxCount <= 0 && needFailureReason)
        {
            failureReason = $"{readableName}闇€瑕佷富鍩哄湴杈惧埌 Lv.{requiredLevel}";
            return false;
        }

        if (needFailureReason)
            failureReason = $"主基地 Lv.{mainBaseLevel} 最多可建 {maxCount} 个{readableName}（当前 {currentCount}/{maxCount}）";
        return false;
    }

    static bool TryGetFactionMainBaseLevel(bool playerOwned, out int mainBaseLevel)
    {
        mainBaseLevel = 0;
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null)
            return false;

        int highestLevel = 0;
        for (int i = 0; i < buildings.Count; i++)
        {
            RTSBuilding building = buildings[i];
            if (building == null) continue;
            if (!building.bIsMainBase || building.bPlayerOwned != playerOwned) continue;
            if (building.GetHP() <= 0) continue;
            highestLevel = Mathf.Max(highestLevel, Mathf.Max(1, building.BuildingLevel));
        }

        if (highestLevel <= 0)
            return false;

        mainBaseLevel = highestLevel;
        return true;
    }

    static int CountOwnedBuildings(Type buildingType, bool playerOwned)
    {
        if (buildingType == null)
            return 0;

        int count = 0;
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null)
            return 0;

        for (int i = 0; i < buildings.Count; i++)
        {
            RTSBuilding building = buildings[i];
            if (building == null) continue;
            if (building.bPlayerOwned != playerOwned) continue;
            if (building.GetHP() <= 0) continue;
            if (!buildingType.IsInstanceOfType(building)) continue;
            count++;
        }

        return count;
    }

    static bool TryGetBuildLimitDefinition(Type buildingType, out BuildLimitDefinition definition)
    {
        for (int i = 0; i < BuildLimitDefinitions.Length; i++)
        {
            if (BuildLimitDefinitions[i].BuildingType == buildingType
                || BuildLimitDefinitions[i].BuildingType.IsAssignableFrom(buildingType))
            {
                definition = BuildLimitDefinitions[i];
                return true;
            }
        }

        definition = default;
        return false;
    }

    static string GetReadableBuildName(BuildLimitDefinition definition, Type buildingType, string displayName)
    {
        if (!string.IsNullOrEmpty(definition.DisplayName))
            return definition.DisplayName;
        if (!string.IsNullOrEmpty(displayName))
            return displayName;
        return buildingType != null ? buildingType.Name : "寤虹瓚";
    }

    public int GetUpgradeCostForNextLevel()
    {
        return TryGetLevelDefinition(BuildingLevel + 1, out BaseLevelDefinition next)
            ? next.UpgradeCost
            : 0;
    }

    public override string GetUpgradeButtonText()
    {
        if (!TryGetLevelDefinition(BuildingLevel + 1, out BaseLevelDefinition next))
            return "主基地\n已满级";

        return $"升级至Lv.{next.Level}\n({next.UpgradeCost}金)";
    }

    public override string GetUpgradePanelStatusText(bool detailed)
    {
        if (!TryGetLevelDefinition(BuildingLevel + 1, out BaseLevelDefinition next))
            return $"<color=#7CE28F>主基地 Lv.{BuildingLevel} 已满级</color>";

        var sb = new StringBuilder();
        sb.Append("<color=#FFD56B>升级至 Lv.")
            .Append(next.Level)
            .Append("</color>  <color=#EAF2FF>")
            .Append(next.UpgradeCost)
            .Append("金</color>");

        if (!detailed)
            return sb.ToString();

        sb.Append('\n')
            .Append("<color=#AFC7D9>升级条件</color>");
        AppendRequirementStatus(sb, next, true, true);
        return sb.ToString();
    }

    public override bool CanUpgradeBuilding(out string failureReason)
    {
        return EvaluateUpgrade(out _, out failureReason, true);
    }

    public bool CanUpgradeBase(out string failureReason)
    {
        return CanUpgradeBuilding(out failureReason);
    }

    public override bool TryUpgradeBuilding(out string failureReason)
    {
        if (!EvaluateUpgrade(out BaseLevelDefinition next, out failureReason, true))
            return false;

        if (OwnerState == null)
            OwnerState = RTSPlayerState.Instance;
        if (OwnerState == null || !OwnerState.SpendGold(next.UpgradeCost))
        {
            failureReason = BuildUpgradeRequirementMessage(next, $"金币不足：升级需要 {next.UpgradeCost}", true);
            return false;
        }

        ApplyLevelDefinition(next.Level, true);
        EffectsManager.PlayLevelUp(transform.position + Vector3.up * 1.2f);
        RTSHUD.Instance?.ShowAlert($"涓诲熀鍦板凡鍗囩骇鑷?Lv.{BuildingLevel}");

        var sync = GameNetworkSync.Instance;
        if (sync != null && sync.IsNetworkGame && NetId != 0)
            sync.SendBuildingUpgrade(NetId, BuildingLevel);

        failureReason = null;
        return true;
    }

    public bool TryUpgradeBase(out string failureReason)
    {
        return TryUpgradeBuilding(out failureReason);
    }

    public override void ForceUpgradeToLevel(int level)
    {
        if (level <= BuildingLevel)
            return;
        if (!TryGetLevelDefinition(level, out _))
            return;
        if (GetHP() <= 0)
            return;

        ApplyLevelDefinition(level, true);
        EffectsManager.PlayLevelUp(transform.position + Vector3.up * 1.2f);
    }

    void CreateHealAura()
    {
        _healAuraBaseColor = new Color(0.42f, 0.56f, 0.30f, 0.045f);
        _healAura = FxResources.MakeGroundDisc(transform, "HealAura", HealRadius, _healAuraBaseColor, FxResources.DiscStyle.SoftDisc, 0.035f);
        _healAuraMat = _healAura.GetComponent<Renderer>().sharedMaterial;
        _healAura.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        var sync = GameNetworkSync.Instance;
        if (sync != null && sync.IsNetworkGame && !bPlayerOwned) return;

        healTimer += Time.deltaTime;
        bool didHealThisTick = false;
        if (healTimer >= HealInterval)
        {
            healTimer = 0f;
            var units = GameManager.Instance?.GetAllUnits();
            if (units != null)
                foreach (var u in units)
                    if (u != null && u.IsPlayerOwned() == bPlayerOwned
                        && Vector3.Distance(transform.position, u.transform.position) < HealRadius)
                    {
                        u.TakeDamage(-HealAmount);
                        didHealThisTick = true;
                    }

            var buildings = GameManager.Instance?.GetAllBuildings();
            if (buildings != null)
                foreach (var b in buildings)
                    if (b != null && b != this && b.bPlayerOwned == bPlayerOwned
                        && b.GetHP() < b.GetMaxHP()
                        && Vector3.Distance(transform.position, b.transform.position) < RepairRadius)
                    {
                        b.TakeDamage(-RepairAmount);
                        didHealThisTick = true;
                    }

            if (didHealThisTick)
            {
                _healPulseImpulse = 1f;
                _healAuraVisibleTimer = 0.8f;
                if (_healAura != null) _healAura.SetActive(true);
            }
        }
        UpdateHealAura();
    }

    bool EvaluateUpgrade(out BaseLevelDefinition next, out string failureReason, bool checkGold)
    {
        next = default;
        failureReason = null;

        if (!bPlayerOwned)
        {
            failureReason = "只能升级己方主基地";
            return false;
        }
        if (GetHP() <= 0)
        {
            failureReason = "主基地已损毁";
            return false;
        }
        if (bUnderConstruction)
        {
            failureReason = "建造中无法升级主基地";
            return false;
        }
        if (!TryGetLevelDefinition(BuildingLevel + 1, out next))
        {
            failureReason = "涓诲熀鍦板凡婊＄骇";
            return false;
        }
        if (!MeetsRequirements(next, out failureReason))
            return false;
        if (!checkGold)
            return true;

        if (OwnerState == null)
            OwnerState = RTSPlayerState.Instance;
        if (OwnerState == null || OwnerState.Gold < next.UpgradeCost)
        {
            failureReason = BuildUpgradeRequirementMessage(next, $"金币不足：升级需要 {next.UpgradeCost}", true);
            return false;
        }
        return true;
    }

    bool MeetsRequirements(BaseLevelDefinition next, out string failureReason)
    {
        failureReason = null;
        if (next.Requirements == null || next.Requirements.Length == 0)
            return true;

        bool allMet = true;
        var sb = new StringBuilder("升级条件不足：");
        for (int i = 0; i < next.Requirements.Length; i++)
        {
            UpgradeRequirement requirement = next.Requirements[i];
            int haveCount = CountQualifiedBuildings(requirement.BuildingType);
            if (i > 0) sb.Append("  ");
            sb.Append(requirement.DisplayName)
                .Append(' ')
                .Append(haveCount)
                .Append('/')
                .Append(requirement.RequiredCount);
            if (haveCount < requirement.RequiredCount)
                allMet = false;
        }

        if (!allMet)
            failureReason = BuildUpgradeRequirementMessage(next, "升级条件", true);
        return allMet;
    }

    int CountQualifiedBuildings(Type buildingType)
    {
        if (buildingType == null)
            return 0;

        int count = 0;
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null)
            return 0;

        foreach (var building in buildings)
        {
            if (building == null) continue;
            if (building.bPlayerOwned != bPlayerOwned) continue;
            if (building.bUnderConstruction) continue;
            if (building.GetHP() <= 0) continue;
            if (!buildingType.IsInstanceOfType(building)) continue;
            count++;
        }
        return count;
    }

    void ApplyLevelDefinition(int targetLevel, bool preserveHealthRatio)
    {
        if (!TryGetLevelDefinition(targetLevel, out BaseLevelDefinition definition))
            return;

        int oldMaxHP = Mathf.Max(1, MaxHP);
        int oldHP = CurrentHP;
        float healthRatio = oldMaxHP > 0 ? Mathf.Clamp01((float)oldHP / oldMaxHP) : 1f;

        BuildingLevel = definition.Level;
        MaxHP = definition.MaxHP;
        PopCapBonus = definition.PopCapBonus;
        GoldIncomeAmount = definition.GoldIncomeAmount;

        if (CurrentHP > 0 || bUnderConstruction)
        {
            CurrentHP = preserveHealthRatio
                ? Mathf.Clamp(Mathf.RoundToInt(MaxHP * healthRatio), 1, MaxHP)
                : Mathf.Clamp(CurrentHP, 1, MaxHP);
            ReapplyOwnerBonuses();
            RefreshBuildingRuntimePresentation();
        }
    }

    void AppendRequirementStatus(StringBuilder sb, BaseLevelDefinition next, bool richText, bool includeGold)
    {
        if (sb == null)
            return;

        if (includeGold)
        {
            int currentGold = GetAvailableUpgradeGold();
            AppendRequirementLine(sb, "金币", currentGold, next.UpgradeCost, currentGold >= next.UpgradeCost, richText);
        }

        if (next.Requirements == null || next.Requirements.Length == 0)
            return;

        for (int i = 0; i < next.Requirements.Length; i++)
        {
            UpgradeRequirement requirement = next.Requirements[i];
            int haveCount = CountQualifiedBuildings(requirement.BuildingType);
            bool met = haveCount >= requirement.RequiredCount;
            AppendRequirementLine(sb, requirement.DisplayName, haveCount, requirement.RequiredCount, met, richText);
        }
    }

    void AppendRequirementLine(StringBuilder sb, string label, int currentCount, int requiredCount, bool met, bool richText)
    {
        if (sb == null)
            return;

        int safeCurrent = Mathf.Max(0, currentCount);
        int safeRequired = Mathf.Max(0, requiredCount);

        sb.Append('\n');
        if (richText)
        {
            sb.Append(met
                ? "<color=#B7FFC1><size=20><b>√</b></size></color> <color=#8CFF96><b>已满足</b></color> <color=#EAF2FF>"
                : "<color=#FF6F61><size=20><b>×</b></size></color> <color=#FF9A86><b>缺少</b></color> <color=#FFD6CF>");
            sb.Append(label)
                .Append(' ')
                .Append(safeCurrent)
                .Append('/')
                .Append(safeRequired)
                .Append("</color>");
            return;
        }

        sb.Append(met ? "√ 已满足 " : "× 缺少 ")
            .Append(label)
            .Append(' ')
            .Append(safeCurrent)
            .Append('/')
            .Append(safeRequired);
    }

    string BuildUpgradeRequirementMessage(BaseLevelDefinition next, string title, bool includeGold)
    {
        var sb = new StringBuilder(title ?? "升级条件");
        AppendRequirementStatus(sb, next, false, includeGold);
        return sb.ToString();
    }

    int GetAvailableUpgradeGold()
    {
        if (OwnerState == null)
            OwnerState = RTSPlayerState.Instance;
        return OwnerState != null ? Mathf.Max(0, OwnerState.Gold) : 0;
    }

    static bool TryGetLevelDefinition(int level, out BaseLevelDefinition definition)
    {
        for (int i = 0; i < LevelDefinitions.Length; i++)
            if (LevelDefinitions[i].Level == level)
            {
                definition = LevelDefinitions[i];
                return true;
            }

        definition = default;
        return false;
    }

    void UpdateHealAura()
    {
        if (_healAura == null || _healAuraMat == null) return;
        if (_healAuraVisibleTimer > 0f)
            _healAuraVisibleTimer = Mathf.Max(0f, _healAuraVisibleTimer - Time.deltaTime);

        if (_healPulseImpulse > 0f)
            _healPulseImpulse = Mathf.Max(0f, _healPulseImpulse - Time.deltaTime / 0.6f);

        if (_healAuraVisibleTimer <= 0f && _healPulseImpulse <= 0f)
        {
            _healAura.SetActive(false);
            return;
        }

        float alpha = Mathf.Clamp(0.018f + _healPulseImpulse * 0.055f, 0.018f, 0.075f);
        _healAuraMat.color = new Color(_healAuraBaseColor.r, _healAuraBaseColor.g, _healAuraBaseColor.b, alpha);
    }
}





