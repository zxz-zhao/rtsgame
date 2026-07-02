using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RtsBuilding : StaticBody3D
{
    const float MainBaseSupportInterval = 1f;
    const float MainBaseHealRadius = 50f;
    const float MainBaseRepairRadius = 60f;
    const float MainBaseHealAmount = 8f;
    const float MainBaseRepairAmount = 10f;

    [Signal]
    public delegate void DiedEventHandler(RtsBuilding building);

    [Signal]
    public delegate void MainBaseStateChangedEventHandler(RtsBuilding building, int state);

    [Signal]
    public delegate void ProductionFinishedEventHandler(RtsBuilding building, string unitKey, Vector3 spawnPosition);

    [Export] public string BuildKey { get; set; } = "main_base";
    [Export] public string DisplayName { get; set; } = "Building";
    [Export] public bool PlayerOwned { get; set; } = true;
    [Export] public float MaxHealth { get; set; } = 500f;
    [Export] public Vector3 RallyOffset { get; set; } = new(4, 0, 0);
    [Export] public Vector3 RallyPoint { get; set; }
    [Export] public bool HasRallyPoint { get; set; }
    [Export] public bool IsMainBase { get; set; }
    [Export] public int PopCapBonus { get; set; } = 10;
    [Export] public int GoldIncomeAmount { get; set; }
    [Export] public float GoldIncomeInterval { get; set; } = 5f;
    [Export] public bool IsDefenseTurret { get; set; }
    [Export] public float AttackDamage { get; set; } = 20f;
    [Export] public float AttackRange { get; set; } = 16f;
    [Export] public float AttackCooldown { get; set; } = 1.5f;
    [Export] public bool UnderConstruction { get; set; }
    [Export] public float ConstructionDuration { get; set; }
    [Export] public bool CanBeRebuilt { get; set; }
    [Export] public float RebuildDuration { get; set; } = 30f;
    [Export] public float RebuildHealthFraction { get; set; } = 0.35f;
    [Export] public int PowerProvided { get; set; }
    [Export] public int PowerUsed { get; set; }
    [Export] public bool Powered { get; set; } = true;
    [Export] public int BuildingLevel { get; set; } = 1;

    public int NetId { get; set; }
    public float Health { get; private set; }
    public bool Selected { get; private set; }
    public bool FogRevealed { get; private set; } = true;
    public MainBaseState MainBaseState { get; private set; } = MainBaseState.Active;
    public bool IsRuined => MainBaseState == MainBaseState.Ruined;
    public bool IsDestroyed => Health <= 0f || MainBaseState == MainBaseState.Ruined;
    public bool IsRebuilding => MainBaseState == MainBaseState.Rebuilding;
    public string CurrentProduction => queue.Count > 0 ? queue.Peek() : "";
    public int QueueCount => queue.Count;
    public float ProductionTimeLeft => productionTimeLeft;
    public float ConstructionTimeLeft => constructionTimeLeft;
    public float RebuildTimeLeft => rebuildTimeLeft;
    public float RebuildProgress => RebuildDuration <= 0f
        ? 1f
        : Mathf.Clamp(1f - rebuildTimeLeft / RebuildDuration, 0f, 1f);
    public float ConstructionProgress => ConstructionDuration <= 0f
        ? 1f
        : Mathf.Clamp(1f - constructionTimeLeft / ConstructionDuration, 0f, 1f);

    readonly Queue<string> queue = new();
    readonly List<string> productionRoster = new();
    float productionTimeLeft;
    float constructionTimeLeft;
    float rebuildTimeLeft;
    float goldTimer;
    float attackTimer;
    float mainBaseSupportTimer;
    float baseMaxHealth;
    int basePopCapBonus;
    int baseGoldIncomeAmount;
    float baseAttackDamage;
    float baseAttackRange;
    float baseAttackCooldown;
    int basePowerProvided;
    float productionSpeedMultiplier = 1f;
    uint originalCollisionLayer;
    uint originalCollisionMask;
    MeshInstance3D? selectionRing;
    Node3D? rallyMarker;
    Label3D? levelBadge;

    public override void _Ready()
    {
        if (Health <= 0f)
            Health = MaxHealth;
        originalCollisionLayer = CollisionLayer;
        originalCollisionMask = CollisionMask;
        EnsureCombatOverlays();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        ProcessConstruction(dt);
        ProcessRebuild(dt);
        if (UnderConstruction || IsRuined || IsRebuilding)
            return;

        ProcessIncome(dt);
        ProcessMainBaseSupport(dt);
        ProcessDefense(dt);
        ProcessProduction(dt);
    }

    public void Configure(BattleBuildingDefinition def, bool playerOwned)
    {
        BuildKey = def.Key;
        DisplayName = playerOwned ? def.DisplayName : $"敌方{def.DisplayName}";
        PlayerOwned = playerOwned;
        MaxHealth = def.MaxHealth;
        Health = MaxHealth;
        baseMaxHealth = def.MaxHealth;
        IsMainBase = def.IsMainBase;
        CanBeRebuilt = def.IsMainBase;
        RebuildDuration = def.IsMainBase ? 45f : 30f;
        RebuildHealthFraction = def.IsMainBase ? 0.45f : 0.35f;
        PopCapBonus = def.PopCapBonus;
        basePopCapBonus = def.PopCapBonus;
        GoldIncomeAmount = def.GoldIncomeAmount;
        baseGoldIncomeAmount = def.GoldIncomeAmount;
        GoldIncomeInterval = def.GoldIncomeInterval;
        IsDefenseTurret = def.IsDefenseTurret;
        AttackDamage = def.AttackDamage;
        baseAttackDamage = def.AttackDamage;
        AttackRange = def.AttackRange;
        baseAttackRange = def.AttackRange;
        AttackCooldown = def.AttackCooldown;
        baseAttackCooldown = def.AttackCooldown;
        PowerProvided = def.PowerProvided;
        basePowerProvided = def.PowerProvided;
        PowerUsed = def.PowerUsed;
        Powered = true;
        MainBaseState = MainBaseState.Active;
        rebuildTimeLeft = 0f;
        mainBaseSupportTimer = 0f;
        ApplyUpgradeLevel(BuildingLevel, false);
        productionRoster.Clear();
        productionRoster.AddRange(def.ProductionRoster);
        if (!HasRallyPoint)
            RallyPoint = GlobalPosition + RallyOffset;
    }

    public void BeginConstruction(float duration)
    {
        ConstructionDuration = Mathf.Max(0f, duration);
        constructionTimeLeft = ConstructionDuration;
        UnderConstruction = ConstructionDuration > 0f;
        MainBaseState = MainBaseState.Active;
        if (UnderConstruction)
            Health = Mathf.Clamp(MaxHealth * 0.28f, 1f, MaxHealth);
    }

    public void CompleteConstruction()
    {
        ConstructionDuration = 0f;
        constructionTimeLeft = 0f;
        UnderConstruction = false;
        Health = MaxHealth;
        MainBaseState = MainBaseState.Active;
        rebuildTimeLeft = 0f;
    }

    public void EnqueueUnit(string unitKey)
    {
        if (UnderConstruction || IsRuined || IsRebuilding)
            return;
        if (RequiresPower() && !Powered)
            return;

        queue.Enqueue(unitKey);
        if (queue.Count == 1)
            productionTimeLeft = ProductionDuration(unitKey);
    }

    public bool CanProduce(string unitKey)
    {
        if (UnderConstruction || IsRuined || IsRebuilding)
            return false;
        if (RequiresPower() && !Powered)
            return false;

        EnsureProductionRoster();
        return productionRoster.Contains(unitKey);
    }

    public string[] GetProductionRoster()
    {
        EnsureProductionRoster();
        return productionRoster.ToArray();
    }

    public void ApplyDamage(float amount)
    {
        if (Health <= 0f || IsRebuilding)
            return;

        var before = Health;
        Health = Mathf.Max(0f, Health - amount);
        BattleFeedback.Damage(this, GlobalPosition, before - Health, PlayerOwned, Health <= 0f);
        if (Health <= 0f)
        {
            if (CanBeRebuilt)
                EnterRuinedState();
            else
            {
                EmitSignal(SignalName.Died, this);
                QueueFree();
            }
        }
    }

    public float Repair(float amount)
    {
        if (IsDestroyed || amount <= 0f)
            return 0f;

        var before = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        var repaired = Health - before;
        BattleFeedback.Repair(this, GlobalPosition, repaired);
        return repaired;
    }

    public bool CanStartRebuild()
        => CanBeRebuilt && MainBaseState == MainBaseState.Ruined;

    public bool StartRebuild(float? rebuildDuration = null)
    {
        if (!CanStartRebuild())
            return false;

        MainBaseState = MainBaseState.Rebuilding;
        rebuildTimeLeft = Mathf.Max(0.1f, rebuildDuration ?? RebuildDuration);
        UnderConstruction = false;
        Health = 0f;
        queue.Clear();
        productionTimeLeft = 0f;
        attackTimer = 0f;
        goldTimer = 0f;
        mainBaseSupportTimer = 0f;
        EmitMainBaseStateChanged();
        return true;
    }

    public void CancelRebuild()
    {
        if (!IsRebuilding)
            return;

        MainBaseState = MainBaseState.Ruined;
        rebuildTimeLeft = 0f;
        EmitMainBaseStateChanged();
    }

    public float RepairRuinedBase(float amount)
    {
        if (!IsDestroyed || amount <= 0f)
            return 0f;

        if (MainBaseState == MainBaseState.Ruined && CanBeRebuilt)
        {
            var repaired = Mathf.Min(MaxHealth * 0.65f, amount);
            if (repaired <= 0f)
                return 0f;

            Health = Mathf.Clamp(repaired, MaxHealth * RebuildHealthFraction, MaxHealth);
            if (Health > 0f && MainBaseState == MainBaseState.Ruined)
            {
                MainBaseState = MainBaseState.Active;
                EmitMainBaseStateChanged();
            }
            return repaired;
        }

        return 0f;
    }

    public void SetSelected(bool value)
    {
        Selected = value;
        if (selectionRing is not null)
            selectionRing.Visible = value && FogRevealed;
        if (rallyMarker is not null)
            rallyMarker.Visible = value && HasRallyPoint && FogRevealed;
    }

    public void SetFogRevealed(bool revealed)
    {
        FogRevealed = PlayerOwned || revealed;
        Visible = FogRevealed;
        CollisionLayer = FogRevealed ? originalCollisionLayer : 0;
        CollisionMask = FogRevealed ? originalCollisionMask : 0;
        if (!FogRevealed)
            SetSelected(false);
    }

    void ProcessConstruction(float delta)
    {
        if (!UnderConstruction)
            return;

        constructionTimeLeft -= delta;
        Health = Mathf.Clamp(MaxHealth * Mathf.Lerp(0.28f, 1f, ConstructionProgress), 1f, MaxHealth);
        if (constructionTimeLeft > 0f)
            return;

        UnderConstruction = false;
        constructionTimeLeft = 0f;
        Health = MaxHealth;
    }

    void ProcessRebuild(float delta)
    {
        if (MainBaseState != MainBaseState.Rebuilding)
            return;

        rebuildTimeLeft -= delta;
        if (rebuildTimeLeft > 0f)
            return;

        MainBaseState = MainBaseState.Active;
        rebuildTimeLeft = 0f;
        Health = Mathf.Max(1f, MaxHealth * RebuildHealthFraction);
        EmitMainBaseStateChanged();
    }

    void ProcessIncome(float delta)
    {
        if (GoldIncomeAmount <= 0 || GoldIncomeInterval <= 0f)
            return;
        if (RequiresPower() && !Powered)
            return;

        goldTimer += delta;
        if (goldTimer < GoldIncomeInterval)
            return;

        goldTimer = 0f;
        BattleGameManager.Instance?.AddGold(PlayerOwned, GoldIncomeAmount);
    }

    void ProcessMainBaseSupport(float delta)
    {
        if (!IsMainBase || BattleGameManager.Instance is not { } manager || manager.GameOver)
            return;

        mainBaseSupportTimer += delta;
        if (mainBaseSupportTimer < MainBaseSupportInterval)
            return;

        mainBaseSupportTimer = 0f;

        foreach (var unit in manager.GetUnits(PlayerOwned))
        {
            if (unit.GlobalPosition.DistanceTo(GlobalPosition) > MainBaseHealRadius)
                continue;

            unit.Repair(MainBaseHealAmount);
        }

        foreach (var building in manager.GetBuildings(PlayerOwned))
        {
            if (building == this || building.GlobalPosition.DistanceTo(GlobalPosition) > MainBaseRepairRadius)
                continue;

            building.Repair(MainBaseRepairAmount);
        }
    }

    void ProcessDefense(float delta)
    {
        if (!IsDefenseTurret || Health <= 0f || BattleGameManager.Instance is not { } manager || manager.GameOver)
            return;
        if (RequiresPower() && !Powered)
            return;

        attackTimer -= delta;
        if (attackTimer > 0f)
            return;

        var target = manager.GetUnits(!PlayerOwned)
            .OrderBy(u => u.GlobalPosition.DistanceSquaredTo(GlobalPosition))
            .FirstOrDefault(u => u.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange);
        if (target is null)
            return;

        AimDefenseTarget(target.GlobalPosition);
        attackTimer = AttackCooldown;
        CombatProjectile.Spawn(this, this, target, AttackDamage, PlayerOwned, AttackRange);
    }

    void AimDefenseTarget(Vector3 targetPosition)
    {
        var flat = targetPosition - GlobalPosition;
        flat.Y = 0f;
        if (flat.LengthSquared() <= 0.0001f)
            return;

        var lookTarget = GlobalPosition + flat.Normalized();
        if (GetNodeOrNull<Node3D>("BuildingVisual") is { } visualRoot)
        {
            var turret = visualRoot.FindChild("ImportedCannon", true, false) as Node3D
                ?? visualRoot.FindChild("TurretBase", true, false) as Node3D
                ?? visualRoot.FindChild("Turret", true, false) as Node3D
                ?? visualRoot.FindChild("Cannon", true, false) as Node3D;

            if (turret is not null)
            {
                turret.LookAt(lookTarget, Vector3.Up, true);
                return;
            }
        }

        LookAt(lookTarget, Vector3.Up, true);
    }

    void ProcessProduction(float delta)
    {
        if (queue.Count == 0)
            return;
        if (RequiresPower() && !Powered)
            return;

        productionTimeLeft -= delta;
        if (productionTimeLeft > 0f)
            return;

        var unitKey = queue.Dequeue();
        EmitSignal(SignalName.ProductionFinished, this, unitKey, GlobalPosition + SpawnOffset());
        if (queue.Count > 0)
            productionTimeLeft = ProductionDuration(queue.Peek());
    }

    void EnterRuinedState()
    {
        Health = 0f;
        queue.Clear();
        productionTimeLeft = 0f;
        goldTimer = 0f;
        attackTimer = 0f;
        if (MainBaseState != MainBaseState.Ruined)
            MainBaseState = MainBaseState.Ruined;
        EmitMainBaseStateChanged();
    }

    void EmitMainBaseStateChanged()
    {
        EmitSignal(SignalName.MainBaseStateChanged, this, (int)MainBaseState);
    }

    Vector3 SpawnOffset()
    {
        var target = GetRallyTarget();
        var direction = target - GlobalPosition;
        direction.Y = 0f;
        return direction.Length() > 0.1f ? direction.Normalized() * 4.2f : RallyOffset;
    }

    void EnsureProductionRoster()
    {
        if (productionRoster.Count == 0)
            productionRoster.AddRange(BattleBuildingCatalog.Get(BuildKey).ProductionRoster);
    }

    public float ProductionDuration(string unitKey)
        => BattleUnitCatalog.Get(unitKey).ProductionTime / Mathf.Max(0.05f, productionSpeedMultiplier);

    public bool CanUpgrade()
        => PlayerOwned && !UnderConstruction && !IsRuined && !IsRebuilding
            && BattleBuildingUpgradeCatalog.HasNextLevel(BuildKey, BuildingLevel);

    public int NextUpgradeCost()
        => CanUpgrade() ? BattleBuildingUpgradeCatalog.Get(BuildKey, BuildingLevel + 1).GoldCost : 0;

    public bool TryGetNextUpgrade(out BattleBuildingUpgradeDefinition upgrade, out BattleBuildingRequirementStatus[] requirements)
    {
        if (!BattleBuildingUpgradeCatalog.HasNextLevel(BuildKey, BuildingLevel))
        {
            upgrade = default;
            requirements = Array.Empty<BattleBuildingRequirementStatus>();
            return false;
        }

        upgrade = BattleBuildingUpgradeCatalog.Get(BuildKey, BuildingLevel + 1);
        requirements = BattleBuildingUpgradeCatalog.EvaluateRequirements(
            upgrade.Requirements,
            key => BattleGameManager.Instance?.CountBuildings(key, PlayerOwned, false) ?? 0,
            key => BattleGameManager.Instance?.HighestBuildingLevel(key, PlayerOwned, false) ?? 0);
        return true;
    }

    public void ApplyUpgradeLevel(int level, bool preserveHealthRatio)
    {
        var previousMaxHealth = MaxHealth;
        var healthRatio = preserveHealthRatio && previousMaxHealth > 0f
            ? Mathf.Clamp(Health / previousMaxHealth, 0f, 1f)
            : 1f;
        BuildingLevel = Mathf.Clamp(level, 1, BattleBuildingUpgradeCatalog.GetMaxLevel(BuildKey));
        var upgrade = BattleBuildingUpgradeCatalog.Get(BuildKey, BuildingLevel);
        MaxHealth = upgrade.MaxHealthOverride ?? Mathf.Round(baseMaxHealth * upgrade.HealthMultiplier);
        PopCapBonus = upgrade.PopCapBonusOverride ?? (basePopCapBonus + upgrade.PopCapBonusAdd);
        GoldIncomeAmount = upgrade.GoldIncomeOverride ?? Mathf.RoundToInt(baseGoldIncomeAmount * upgrade.IncomeMultiplier);
        AttackDamage = upgrade.TurretDamageOverride ?? (baseAttackDamage * upgrade.TurretDamageMultiplier);
        AttackRange = upgrade.TurretRangeOverride ?? (baseAttackRange + upgrade.TurretRangeBonus);
        AttackCooldown = upgrade.TurretCooldownOverride ?? Mathf.Max(0.12f, baseAttackCooldown);
        PowerProvided = upgrade.PowerProvidedOverride ?? (basePowerProvided + upgrade.PowerProvidedAdd);
        productionSpeedMultiplier = upgrade.ProductionSpeedMultiplier;
        Health = Mathf.Clamp(MaxHealth * healthRatio, 1f, MaxHealth);
        EnsureLevelBadge();
    }

    public bool RequiresPower()
        => PowerUsed > 0;

    public Vector3 GetRallyTarget()
        => HasRallyPoint ? RallyPoint : GlobalPosition + RallyOffset;

    public bool CanSetRallyPoint()
        => PlayerOwned && !UnderConstruction && !IsRuined && !IsRebuilding && GetProductionRoster().Length > 0;

    public void SetRallyPoint(Vector3 worldPosition)
    {
        if (!CanSetRallyPoint())
            return;

        RallyPoint = NormalizeRallyPoint(worldPosition);
        HasRallyPoint = true;
        EnsureRallyMarker();
        if (rallyMarker is not null)
        {
            rallyMarker.GlobalPosition = RallyPoint + Vector3.Up * 0.08f;
            rallyMarker.Visible = Selected && FogRevealed;
        }
    }

    Vector3 NormalizeRallyPoint(Vector3 worldPosition)
    {
        EnsureProductionRoster();
        if (productionRoster.Count > 0 && BattleGameManager.Instance is { } manager)
            return manager.NormalizeUnitTarget(productionRoster[0], worldPosition);
        return BattleMapCatalog.ClampToMap(new Vector3(worldPosition.X, 0f, worldPosition.Z), 4f);
    }

    void EnsureCombatOverlays()
    {
        if (GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar") is null)
        {
            AddChild(new WorldHealthBar3D
            {
                Name = "WorldHealthBar",
                Width = IsMainBase ? 5.4f : 3.8f,
                HeightOffset = IsMainBase ? 5.0f : 3.4f,
                Depth = 0.26f
            });
        }

        selectionRing = GetNodeOrNull<MeshInstance3D>("SelectionRing");
        if (selectionRing is not null)
            return;

        selectionRing = new MeshInstance3D
        {
            Name = "SelectionRing",
            Position = new Vector3(0f, 0.05f, 0f),
            Scale = Vector3.One * (IsMainBase ? 2.6f : 1.8f),
            Mesh = new TorusMesh
            {
                InnerRadius = 1.75f,
                OuterRadius = 1.88f,
                RingSegments = 64
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = PlayerOwned
                    ? new Color(0.18f, 0.80f, 1f, 0.90f)
                    : new Color(1f, 0.28f, 0.18f, 0.90f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            Visible = false
        };
        AddChild(selectionRing);
        EnsureRallyMarker();
        EnsureLevelBadge();
    }

    void EnsureLevelBadge()
    {
        levelBadge = GetNodeOrNull<Label3D>("LevelBadge");
        if (levelBadge is null)
        {
            levelBadge = new Label3D
            {
                Name = "LevelBadge",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.018f,
                FontSize = 52,
                OutlineSize = 8,
                OutlineModulate = new Color(0.02f, 0.03f, 0.03f, 0.95f),
                Modulate = PlayerOwned ? new Color(0.44f, 0.95f, 1f) : new Color(1f, 0.42f, 0.28f),
                Position = new Vector3(0f, IsMainBase ? 6.2f : 4.4f, 0f)
            };
            AddChild(levelBadge);
        }

        levelBadge.Text = $"{BuildingLevel}级";
        levelBadge.Visible = BuildingLevel > 1;
    }

    void EnsureRallyMarker()
    {
        rallyMarker = GetNodeOrNull<Node3D>("RallyMarker");
        if (rallyMarker is not null)
            return;

        rallyMarker = new Node3D
        {
            Name = "RallyMarker",
            Visible = false
        };
        AddChild(rallyMarker);

        rallyMarker.AddChild(new MeshInstance3D
        {
            Name = "Pole",
            Position = new Vector3(0f, 0.8f, 0f),
            Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 1.6f, RadialSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.08f, 0.10f, 0.10f),
                Roughness = 0.82f
            }
        });
        rallyMarker.AddChild(new MeshInstance3D
        {
            Name = "Flag",
            Position = new Vector3(0.34f, 1.34f, 0f),
            Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.34f, 0.06f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.18f, 0.82f, 1f),
                Roughness = 0.74f
            }
        });
    }
}
