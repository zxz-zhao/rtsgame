using System;
using System.Text;
using UnityEngine;

public abstract class UpgradeableBuilding : RTSBuilding
{
    protected struct UpgradeRequirement
    {
        public readonly Type BuildingType;
        public readonly int RequiredCount;
        public readonly int RequiredLevel;
        public readonly string DisplayName;

        public UpgradeRequirement(Type buildingType, int requiredCount, string displayName, int requiredLevel = 1)
        {
            BuildingType = buildingType;
            RequiredCount = Mathf.Max(0, requiredCount);
            RequiredLevel = Mathf.Max(1, requiredLevel);
            DisplayName = displayName ?? string.Empty;
        }
    }

    protected struct UpgradeLevelDefinition
    {
        public readonly int Level;
        public readonly int MaxHP;
        public readonly int UpgradeCost;
        public readonly float ProductionSpeedMultiplier;
        public readonly int PopCapBonus;
        public readonly int PowerProvide;
        public readonly int GoldIncomeAmount;
        public readonly int TurretDamage;
        public readonly float TurretRange;
        public readonly float TurretInterval;
        public readonly UpgradeRequirement[] Requirements;

        public UpgradeLevelDefinition(
            int level,
            int maxHP,
            int upgradeCost,
            float productionSpeedMultiplier = 1f,
            int popCapBonus = 0,
            int powerProvide = 0,
            int goldIncomeAmount = 0,
            int turretDamage = 0,
            float turretRange = 0f,
            float turretInterval = 0f,
            params UpgradeRequirement[] requirements)
        {
            Level = Mathf.Max(1, level);
            MaxHP = Mathf.Max(1, maxHP);
            UpgradeCost = Mathf.Max(0, upgradeCost);
            ProductionSpeedMultiplier = Mathf.Max(0.01f, productionSpeedMultiplier);
            PopCapBonus = popCapBonus;
            PowerProvide = powerProvide;
            GoldIncomeAmount = goldIncomeAmount;
            TurretDamage = turretDamage;
            TurretRange = turretRange;
            TurretInterval = turretInterval;
            Requirements = requirements ?? Array.Empty<UpgradeRequirement>();
        }
    }

    float _productionSpeedMultiplier = 1f;

    protected override float ProductionSpeedMultiplier => _productionSpeedMultiplier;
    protected abstract UpgradeLevelDefinition[] GetUpgradeLevelDefinitions();

    public override bool SupportsBuildingUpgrade => GetSafeUpgradeLevelDefinitions().Length > 0;

    public override int GetUpgradeButtonSlotIndex()
    {
        int index = 0;
        if (ProductionUnits == null)
            return index;

        for (int i = 0; i < ProductionUnits.Length; i++)
            if (ProductionUnits[i] != null)
                index = i + 1;
        return index;
    }

    public override string GetUpgradeButtonText()
    {
        if (!TryGetLevelDefinition(BuildingLevel + 1, out UpgradeLevelDefinition next))
            return $"Lv.{Mathf.Max(1, BuildingLevel)}\n已满级";

        return $"升级Lv.{next.Level}\n({next.UpgradeCost}金)";
    }

    public override string GetUpgradePanelStatusText(bool detailed)
    {
        if (!TryGetLevelDefinition(BuildingLevel + 1, out UpgradeLevelDefinition next))
            return $"<color=#7CE28F>{DisplayName} Lv.{BuildingLevel} 已满级</color>";

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

    public override bool TryUpgradeBuilding(out string failureReason)
    {
        if (!EvaluateUpgrade(out UpgradeLevelDefinition next, out failureReason, true))
            return false;

        if (OwnerState == null)
            OwnerState = RTSPlayerState.Instance;
        if (OwnerState == null || !OwnerState.SpendGold(next.UpgradeCost))
        {
            failureReason = BuildUpgradeRequirementMessage(next, $"金币不足：升级需要 {next.UpgradeCost}", true);
            return false;
        }

        ApplyUpgradeLevel(next.Level, true);
        EffectsManager.PlayLevelUp(transform.position + Vector3.up * 1.2f);
        RTSHUD.Instance?.ShowAlert($"{DisplayName}宸插崌绾ц嚦 Lv.{BuildingLevel}");

        var sync = GameNetworkSync.Instance;
        if (sync != null && sync.IsNetworkGame && NetId != 0)
            sync.SendBuildingUpgrade(NetId, BuildingLevel);

        failureReason = null;
        return true;
    }

    public override void ForceUpgradeToLevel(int level)
    {
        if (level <= BuildingLevel)
            return;
        if (!TryGetLevelDefinition(level, out _))
            return;
        if (GetHP() <= 0)
            return;

        ApplyUpgradeLevel(level, true);
        EffectsManager.PlayLevelUp(transform.position + Vector3.up * 1.2f);
    }

    protected void ApplyConfiguredUpgradeLevel(bool preserveHealthRatio)
    {
        if (TryGetLevelDefinition(Mathf.Max(1, BuildingLevel), out UpgradeLevelDefinition definition))
            ApplyUpgradeLevel(definition.Level, preserveHealthRatio);
        else
            _productionSpeedMultiplier = 1f;
    }

    protected virtual void OnUpgradeApplied(UpgradeLevelDefinition definition, int previousLevel)
    {
    }

    UpgradeLevelDefinition[] GetSafeUpgradeLevelDefinitions()
    {
        return GetUpgradeLevelDefinitions() ?? Array.Empty<UpgradeLevelDefinition>();
    }

    bool EvaluateUpgrade(out UpgradeLevelDefinition next, out string failureReason, bool checkGold)
    {
        next = default;
        failureReason = null;

        if (!SupportsBuildingUpgrade)
        {
            failureReason = "该建筑不可升级";
            return false;
        }
        if (!bPlayerOwned)
        {
            failureReason = "只能升级己方建筑";
            return false;
        }
        if (GetHP() <= 0)
        {
            failureReason = $"{DisplayName}已损毁";
            return false;
        }
        if (bUnderConstruction)
        {
            failureReason = "建造中无法升级";
            return false;
        }
        if (!TryGetLevelDefinition(BuildingLevel + 1, out next))
        {
            failureReason = $"{DisplayName}已满级";
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

    bool MeetsRequirements(UpgradeLevelDefinition next, out string failureReason)
    {
        failureReason = null;
        bool allMet = true;
        if (RequiresLevelTwoMainBaseForUpgrade(next))
        {
            if (!HasFactionMainBaseLevel(2))
                allMet = false;
        }

        if (next.Requirements != null)
        {
            for (int i = 0; i < next.Requirements.Length; i++)
            {
                UpgradeRequirement requirement = next.Requirements[i];
                int haveCount = CountQualifiedBuildings(requirement);
                if (haveCount < requirement.RequiredCount)
                    allMet = false;
            }
        }

        if (!allMet)
            failureReason = BuildUpgradeRequirementMessage(next, "升级条件", true);
        return allMet;
    }

    int CountQualifiedBuildings(UpgradeRequirement requirement)
    {
        if (requirement.BuildingType == null)
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
            if (!requirement.BuildingType.IsInstanceOfType(building)) continue;
            if (building.BuildingLevel < requirement.RequiredLevel) continue;
            count++;
        }
        return count;
    }

    void ApplyUpgradeLevel(int targetLevel, bool preserveHealthRatio)
    {
        if (!TryGetLevelDefinition(targetLevel, out UpgradeLevelDefinition definition))
            return;

        int previousLevel = BuildingLevel;
        int oldMaxHP = Mathf.Max(1, MaxHP);
        int oldHP = CurrentHP;
        float healthRatio = oldMaxHP > 0 ? Mathf.Clamp01((float)oldHP / oldMaxHP) : 1f;

        BuildingLevel = definition.Level;
        MaxHP = definition.MaxHP;
        PopCapBonus = definition.PopCapBonus;
        PowerProvide = definition.PowerProvide;
        GoldIncomeAmount = definition.GoldIncomeAmount;
        _productionSpeedMultiplier = definition.ProductionSpeedMultiplier;

        if (definition.TurretDamage > 0)
            TurretDamage = definition.TurretDamage;
        if (definition.TurretRange > 0f)
            TurretRange = definition.TurretRange;
        if (definition.TurretInterval > 0f)
            TurretInterval = definition.TurretInterval;

        if (CurrentHP > 0 || bUnderConstruction)
        {
            CurrentHP = preserveHealthRatio
                ? Mathf.Clamp(Mathf.RoundToInt(MaxHP * healthRatio), 1, MaxHP)
                : Mathf.Clamp(CurrentHP, 1, MaxHP);
            ReapplyOwnerBonuses();
            RefreshBuildingRuntimePresentation();
            RefreshProductionRuntimeState();
        }

        OnUpgradeApplied(definition, previousLevel);
    }

    void AppendRequirementStatus(StringBuilder sb, UpgradeLevelDefinition next, bool richText, bool includeGold)
    {
        if (sb == null)
            return;

        if (includeGold)
        {
            int currentGold = GetAvailableUpgradeGold();
            AppendRequirementLine(sb, "金币", currentGold, next.UpgradeCost, currentGold >= next.UpgradeCost, richText);
        }

        if (RequiresLevelTwoMainBaseForUpgrade(next) && !HasMainBaseLevelRequirement(next, 2))
        {
            UpgradeRequirement requirement = CreateLevelTwoMainBaseRequirement();
            int haveCount = CountQualifiedBuildings(requirement);
            bool met = haveCount >= requirement.RequiredCount;
            AppendRequirementLine(sb, GetRequirementLabel(requirement), haveCount, requirement.RequiredCount, met, richText);
        }

        if (next.Requirements != null)
        {
            for (int i = 0; i < next.Requirements.Length; i++)
            {
                UpgradeRequirement requirement = next.Requirements[i];
                int haveCount = CountQualifiedBuildings(requirement);
                bool met = haveCount >= requirement.RequiredCount;
                AppendRequirementLine(sb, GetRequirementLabel(requirement), haveCount, requirement.RequiredCount, met, richText);
            }
        }
    }

    bool RequiresLevelTwoMainBaseForUpgrade(UpgradeLevelDefinition next)
    {
        return BuildingLevel == 1
            && next.Level == 2;
    }

    bool HasFactionMainBaseLevel(int requiredLevel)
    {
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null)
            return false;

        for (int i = 0; i < buildings.Count; i++)
        {
            RTSBuilding building = buildings[i];
            if (building == null) continue;
            if (!building.bIsMainBase) continue;
            if (building.bPlayerOwned != bPlayerOwned) continue;
            if (building.bUnderConstruction) continue;
            if (building.GetHP() <= 0) continue;
            if (building.BuildingLevel >= requiredLevel)
                return true;
        }
        return false;
    }

    bool HasMainBaseLevelRequirement(UpgradeLevelDefinition next, int requiredLevel)
    {
        if (next.Requirements == null)
            return false;

        for (int i = 0; i < next.Requirements.Length; i++)
        {
            UpgradeRequirement requirement = next.Requirements[i];
            if (requirement.BuildingType == null) continue;
            if (!typeof(MainBase).IsAssignableFrom(requirement.BuildingType)) continue;
            if (requirement.RequiredLevel >= requiredLevel)
                return true;
        }
        return false;
    }

    static UpgradeRequirement CreateLevelTwoMainBaseRequirement()
    {
        return new UpgradeRequirement(typeof(MainBase), 1, "主基地", 2);
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

    string BuildUpgradeRequirementMessage(UpgradeLevelDefinition next, string title, bool includeGold)
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

    string GetRequirementLabel(UpgradeRequirement requirement)
    {
        if (requirement.RequiredLevel > 1)
            return $"{requirement.DisplayName} Lv.{requirement.RequiredLevel}";
        return requirement.DisplayName;
    }

    bool TryGetLevelDefinition(int level, out UpgradeLevelDefinition definition)
    {
        UpgradeLevelDefinition[] definitions = GetSafeUpgradeLevelDefinitions();
        for (int i = 0; i < definitions.Length; i++)
            if (definitions[i].Level == level)
            {
                definition = definitions[i];
                return true;
            }

        definition = default;
        return false;
    }
}







