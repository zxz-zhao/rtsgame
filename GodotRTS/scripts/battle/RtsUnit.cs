using Godot;

using System.Collections.Generic;
using System.Linq;

public partial class RtsUnit : CharacterBody3D
{
    public const float BomberMinBombingRunLength = 10f;
    public const float BomberMaxBombingRunLength = 24f;
    public const float BomberBombingRunLaneSpacing = 4f;
    public const float DefaultBattleFuelSeconds = 40f;
    public const float DefaultLowFuelReturnRatio = 0.10f;

    sealed class RuntimeTechBuff
    {
        public string SourceId { get; set; } = "";
        public float Remaining { get; set; }
        public float MoveMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;
        public float AttackRangeBonus { get; set; }
        public float AttackCooldownMultiplier { get; set; } = 1f;
        public float DefenseReduction { get; set; }
        public float VisionBonus { get; set; }
        public float RegenPerSecond { get; set; }
        public Color Tint { get; set; } = Colors.White;
    }

    enum InfantryVisualPartKind
    {
        Body,
        LeftLeg,
        RightLeg,
        LeftArm,
        RightArm
    }

    sealed class AnimatedVisualPart
    {
        public Node3D Node { get; init; } = null!;
        public Vector3 RestPosition { get; init; }
        public Vector3 RestRotation { get; init; }
        public float Phase { get; init; }
        public InfantryVisualPartKind Kind { get; init; }
    }

    [Signal]
    public delegate void DiedEventHandler(RtsUnit unit);

    public const uint ForestCollisionLayer = 4u;

    public float Shield { get; private set; }
    public float MaxShield { get; private set; }
    float secondsSinceLastDamage = 99f;

    [Export] public string UnitKey { get; set; } = "tank";
    [Export] public string DisplayName { get; set; } = "Unit";
    [Export] public bool PlayerOwned { get; set; } = true;
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float MoveSpeed { get; set; } = 6f;
    [Export] public float TurnSpeed { get; set; } = 9f;
    [Export] public float AttackDamage { get; set; } = 10f;
    [Export] public float AttackRange { get; set; } = 6f;
    [Export] public float AttackCooldown { get; set; } = 1f;
    [Export] public float SplashRadius { get; set; }
    [Export] public float SplashFalloff { get; set; } = 0.5f;
    [Export] public int GoldCost { get; set; } = 100;
    [Export] public int PopCost { get; set; } = 1;
    [Export] public float CruiseHeight { get; set; }

    public int NetId { get; set; }
    public float Health { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Node3D? AttackTarget { get; private set; }
    public bool Selected { get; private set; }
    public bool IsDead => Health <= 0f;
    public bool AttackMoving { get; private set; }
    public bool AttackGrounding { get; private set; }
    public bool Patrolling { get; private set; }
    public bool Guarding { get; private set; }
    public bool CanAttackGroundPoint => !BattleUnitCatalog.IsAirUnit(UnitKey) && SplashRadius > 0.05f && AttackRange > 0.5f && AttackDamage > 0f;
    public bool FogRevealed { get; private set; } = true;
    /// <summary>曾经被玩家视野探索过（战争迷雾记忆）。</summary>
    public bool FogExplored { get; private set; } = true;
    public float VisionBonus { get; private set; }
    public bool SupportsBombingRun => UnitKey == "bomber" && !IsDead;
    public bool SupportsAirfieldParking => BattleUnitCatalog.RequiresAirfieldSlot(UnitKey) && !IsDead;
    public float CurrentFuel { get; private set; }
    public float FuelRatio => maxFuelSeconds <= 0.01f ? 1f : Mathf.Clamp(CurrentFuel / maxFuelSeconds, 0f, 1f);
    public bool IsReturningToRefuel => parkingAtAirfield;
    public bool IsReturningToPark => parkingAtAirfield;
    public bool IsParkedAtAirfield => parkedAtAirfield;

    const float BomberApproachLead = 9f;
    const float BomberExitLead = 7f;
    const float BomberArrivalThreshold = 1.25f;
    const float BomberFinishThreshold = 1.5f;
    const float BomberReleaseProgress = 0.58f;
    const float BomberReleaseSpacingForward = 2.2f;
    const float BomberReleaseSpacingSide = 2.0f;
    const float BomberDamageMultiplier = 0.58f;
    const float BomberImpactJitter = 0.55f;
    const float AirfieldParkingHeight = 0.72f;
    const float ReturnFuelReserveSeconds = 2f;
    float cooldownLeft;
    float acquireTimer;
    float baseMaxHealth;
    float baseMoveSpeed;
    float baseAttackDamage;
    float baseAttackRange;
    float baseAttackCooldown;
    float baseSplashRadius;
    float baseSplashFalloff;
    float techDamageReduction;
    float techRegenPerSecond;
    float techRegenCarry;
    float maxFuelSeconds;
    float lowFuelReturnRatio;
    float refuelSeconds;
    Vector3 attackGroundTarget;
    Vector3 patrolA;
    Vector3 patrolB;
    bool patrolHeadingToB;
    RtsUnit? guardTarget;
    uint originalCollisionLayer;
    uint originalCollisionMask;
    MeshInstance3D? selectionRing;
    MeshInstance3D? techRing;
    Node3D? visualRoot;
    Node3D? weaponYawNode;
    Node3D? weaponPitchNode;
    bool weaponPitchIsPivot;
    Vector3 weaponYawLookOffset;
    Vector3 weaponPitchLookOffset;
    Vector3 weaponPitchPivotOffset;
    AnimationPlayer? visualAnimationPlayer;
    readonly List<AnimationPlayer> visualAnimationPlayers = new();
    string walkAnimationName = "";
    string idleAnimationName = "";
    string fireAnimationName = "";
    string currentVisualAnimationName = "";
    float visualAnimationLockRemaining;
    float stationaryDuration;
    readonly List<AnimatedVisualPart> infantryVisualParts = new();
    CpuParticles3D? bowWaveParticles;
    CpuParticles3D? sternWakeParticles;
    readonly List<Node3D> propellerNodes = new();
    bool holdPosition;
    bool parkingAtAirfield;
    bool parkedAtAirfield;
    Vector3 parkedPosition;
    bool warnedLowFuel;
    bool warnedNoAirfield;
    bool bombingRunActive;
    bool bombingRunOnAttackLeg;
    Vector3 bombingRunStart;
    Vector3 bombingRunApproach;
    Vector3 bombingRunExit;
    Vector3 bombingRunDirection;
    Vector3 bombingRunPerpendicular;
    float bombingRunLength;
    float bombingRunReleaseDistance;
    bool bombingRunVolleyReleased;
    string visualKey = "";
    float bodyFacingYawOffset;
    float visualMotionTime;
    readonly Queue<Vector3> queuedMoveTargets = new();
    readonly List<RuntimeTechBuff> techBuffs = new();

    public override void _Ready()
    {
        CaptureBaseStats();
        Health = MaxHealth;
        TargetPosition = GlobalPosition;
        InitializeAirUnitState();
        originalCollisionLayer = CollisionLayer;
        originalCollisionMask = CollisionMask;
        EnsureCombatOverlays();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        visualMotionTime += dt;
        if (cooldownLeft > 0f)
            cooldownLeft -= dt;
        UpdateTechBuffs(dt);

        if (UpdateAirUnitFuelState(dt))
        {
            MaintainMovementLayer();
            UpdateVisualState(dt);
            return;
        }

        if (parkedAtAirfield)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            MaintainMovementLayer();
            UpdateVisualState(dt);
            return;
        }

        if (bombingRunActive)
        {
            ProcessBombingRun(dt);
            UpdateVisualState(dt);
            return;
        }

        if (GodotObject.IsInstanceValid(AttackTarget))
            ProcessAttack(dt);
        else
        {
            if (AttackGrounding)
                ProcessAttackGround(dt);
            else if (Patrolling)
                ProcessPatrol(dt);
            else if (Guarding)
                ProcessGuard(dt);
            else if (AttackMoving)
                AcquireAttackMoveTarget(dt);
            else if (!holdPosition && IsIdle())
                AcquireGuardTarget(dt);
            ProcessMove(dt);
        }

        UpdateVisualState(dt);
    }

    public void MoveTo(Vector3 worldPos)
    {
        ClearSpecialOrders();
        ClearQueuedMovement();
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = false;
        TargetPosition = NormalizeCommandPosition(worldPos);
    }

    public void AttackMoveTo(Vector3 worldPos)
    {
        ClearSpecialOrders();
        ClearQueuedMovement();
        AttackTarget = null;
        AttackMoving = true;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = false;
        TargetPosition = NormalizeCommandPosition(worldPos);
        acquireTimer = 0f;
    }

    public void Attack(Node3D node)
    {
        ClearSpecialOrders();
        ClearQueuedMovement();
        AttackTarget = node;
        AttackMoving = false;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = false;
    }

    public bool AttackGround(Vector3 worldPos)
    {
        if (!CanAttackGroundPoint)
            return false;

        ClearSpecialOrders();
        ClearQueuedMovement();
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = true;
        Patrolling = false;
        Guarding = false;
        guardTarget = null;
        attackGroundTarget = NormalizeAttackGroundTarget(worldPos);
        TargetPosition = NormalizeCommandPosition(attackGroundTarget);
        return true;
    }

    public void PatrolTo(Vector3 worldPos)
        => PatrolBetween(GlobalPosition, worldPos);

    public void PatrolBetween(Vector3 pointA, Vector3 pointB)
    {
        ClearSpecialOrders();
        ClearQueuedMovement();
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = false;
        Guarding = false;
        Patrolling = true;
        guardTarget = null;
        patrolA = NormalizeCommandPosition(pointA);
        patrolB = NormalizeCommandPosition(pointB);
        patrolHeadingToB = true;
        TargetPosition = patrolB;
        acquireTimer = 0f;
    }

    public void Guard(RtsUnit ally)
    {
        if (!GodotObject.IsInstanceValid(ally) || ally == this || ally.PlayerOwned != PlayerOwned)
            return;

        ClearSpecialOrders();
        ClearQueuedMovement();
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = true;
        guardTarget = ally;
        acquireTimer = 0f;
        TargetPosition = NormalizeCommandPosition(GuardFollowPosition());
    }

    public void Stop()
    {
        CancelBombingRun();
        if (!parkedAtAirfield)
            parkingAtAirfield = false;
        ClearQueuedMovement();
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = false;
        guardTarget = null;
        TargetPosition = parkedAtAirfield
            ? new Vector3(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z)
            : GlobalPosition;
        Velocity = Vector3.Zero;
    }

    public void MoveAlongPath(IEnumerable<Vector3> worldPath, bool holdAtEnd = false)
    {
        ClearSpecialOrders();
        ClearQueuedMovement();
        var path = worldPath
            .Select(NormalizeCommandPosition)
            .Where(point => point.DistanceSquaredTo(GlobalPosition) > 0.04f)
            .ToArray();
        if (path.Length == 0)
        {
            holdPosition = holdAtEnd;
            return;
        }

        TargetPosition = path[0];
        for (var i = 1; i < path.Length; i++)
            queuedMoveTargets.Enqueue(path[i]);
        holdPosition = holdAtEnd;
    }

    public void BeginAirfieldParking(IEnumerable<Vector3> worldPath, Vector3 parkingTarget)
    {
        if (!SupportsAirfieldParking)
            return;

        CancelBombingRun();
        ClearQueuedMovement();
        parkingAtAirfield = true;
        parkedAtAirfield = false;
        parkedPosition = NormalizeCommandPosition(parkingTarget);
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = false;
        guardTarget = null;

        var path = worldPath
            .Select(NormalizeCommandPosition)
            .Where(point => point.DistanceSquaredTo(GlobalPosition) > 0.04f)
            .ToArray();
        if (path.Length == 0)
        {
            TargetPosition = parkedPosition;
            ParkAtAirfield();
            return;
        }

        TargetPosition = path[0];
        for (var i = 1; i < path.Length; i++)
            queuedMoveTargets.Enqueue(path[i]);
        holdPosition = true;
    }

    public void ApplyBombingRunCommand(Vector3 start, Vector3 end)
    {
        if (!SupportsBombingRun)
            return;

        ClearQueuedMovement();
        parkingAtAirfield = false;
        parkedAtAirfield = false;
        AttackTarget = null;
        AttackMoving = false;
        AttackGrounding = false;
        Patrolling = false;
        Guarding = false;
        guardTarget = null;
        holdPosition = false;

        var flatStart = new Vector3(start.X, 0f, start.Z);
        var flatEnd = new Vector3(end.X, 0f, end.Z);
        var flatDelta = flatEnd - flatStart;
        if (flatDelta.LengthSquared() < 0.01f)
        {
            var fallback = -Basis.Z;
            fallback.Y = 0f;
            if (fallback.LengthSquared() < 0.01f)
                fallback = Vector3.Forward;
            flatDelta = fallback.Normalized() * BomberMinBombingRunLength;
        }

        bombingRunDirection = flatDelta.Normalized();
        bombingRunLength = Mathf.Clamp(flatDelta.Length(), BomberMinBombingRunLength, BomberMaxBombingRunLength);
        bombingRunPerpendicular = Vector3.Up.Cross(bombingRunDirection).Normalized();

        var height = CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
        bombingRunStart = new Vector3(flatStart.X, height, flatStart.Z);
        bombingRunApproach = bombingRunStart - bombingRunDirection * BomberApproachLead;
        var bombingRunEnd = bombingRunStart + bombingRunDirection * bombingRunLength;
        bombingRunExit = bombingRunEnd + bombingRunDirection * BomberExitLead;
        bombingRunReleaseDistance = Mathf.Clamp(
            bombingRunLength * BomberReleaseProgress,
            BomberReleaseSpacingForward,
            Mathf.Max(BomberReleaseSpacingForward, bombingRunLength - BomberReleaseSpacingForward));
        bombingRunVolleyReleased = false;
        bombingRunOnAttackLeg = false;
        bombingRunActive = true;
        TargetPosition = NormalizeCommandPosition(bombingRunApproach);
    }

    public void ApplyCatalogCombatProfile()
    {
        if (SplashRadius <= 0.05f)
            SplashRadius = BattleUnitCatalog.SplashRadius(UnitKey);
        if (SplashFalloff <= 0f || SplashFalloff > 1f)
            SplashFalloff = BattleUnitCatalog.SplashFalloff(UnitKey);
        CaptureBaseStats();
    }

    public void ConfigureFromDefinition(BattleUnitDefinition def, bool playerOwned, string displayName)
    {
        var preserveHealthRatio = Health > 0f && MaxHealth > 0.01f;
        var healthRatio = preserveHealthRatio
            ? Mathf.Clamp(Health / MaxHealth, 0f, 1f)
            : 1f;

        UnitKey = def.Key;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? def.DisplayName : displayName.Trim();
        PlayerOwned = playerOwned;
        MaxHealth = def.MaxHealth;
        MoveSpeed = def.MoveSpeed;
        AttackDamage = def.AttackDamage;
        AttackRange = def.AttackRange;
        AttackCooldown = def.AttackCooldown;
        SplashRadius = BattleUnitCatalog.SplashRadius(def.Key);
        SplashFalloff = BattleUnitCatalog.SplashFalloff(def.Key);
        GoldCost = def.GoldCost;
        PopCost = def.PopCost;
        CruiseHeight = BattleUnitCatalog.SpawnHeight(def.Key);

        ResetCapturedBaseStats();
        if (techBuffs.Count > 0)
            RecalculateTechBuffStats();
        else
            CaptureBaseStats();

        Health = preserveHealthRatio
            ? Mathf.Clamp(MaxHealth * healthRatio, 1f, MaxHealth)
            : MaxHealth;

        if (def.Key is "heavy_tank" or "anti_air_gun" or "fighter" or "bomber")
        {
            MaxShield = def.MaxHealth * 0.4f;
            Shield = MaxShield;
        }
        else
        {
            MaxShield = 0f;
            Shield = 0f;
        }
        secondsSinceLastDamage = 99f;

        InitializeAirUnitState();
        RefreshCombatOverlays();
    }

    void InitializeAirUnitState()
    {
        if (!BattleUnitCatalog.IsAirUnit(UnitKey))
            return;

        maxFuelSeconds = UnitKey switch
        {
            "bomber" => 120f,
            _ => DefaultBattleFuelSeconds
        };
        lowFuelReturnRatio = DefaultLowFuelReturnRatio;
        refuelSeconds = UnitKey switch
        {
            "bomber" => 8f,
            "fighter" => 12f,
            "scout_plane" => 10f,
            _ => 14f
        };
        if (CurrentFuel <= 0f || CurrentFuel > maxFuelSeconds)
            CurrentFuel = maxFuelSeconds;
    }

    bool UpdateAirUnitFuelState(float delta)
    {
        if (!BattleUnitCatalog.IsAirUnit(UnitKey))
            return false;

        if (!SupportsAirfieldParking)
        {
            CurrentFuel = maxFuelSeconds;
            warnedLowFuel = false;
            warnedNoAirfield = false;
            return false;
        }

        if (parkedAtAirfield)
        {
            RefuelWhileParked(delta);
            return true;
        }

        CurrentFuel = Mathf.Max(0f, CurrentFuel - delta);
        if (CurrentFuel <= 0f)
        {
            CrashFromFuelLoss();
            return true;
        }

        if (!parkingAtAirfield && ShouldReturnForFuel())
        {
            var manager = BattleGameManager.Instance;
            var message = "";
            if (manager is not null && manager.TryBeginAircraftRefuelReturn(this, true, out message))
            {
                if (PlayerOwned && !warnedLowFuel)
                {
                    FindHud()?.ShowAlert(message);
                    warnedLowFuel = true;
                }
                warnedNoAirfield = false;
                return false;
            }

            if (PlayerOwned && !warnedNoAirfield && !string.IsNullOrWhiteSpace(message))
            {
                FindHud()?.ShowAlert(message);
                warnedNoAirfield = true;
            }
        }

        return false;
    }

    bool ShouldReturnForFuel()
    {
        if (FuelRatio <= lowFuelReturnRatio)
            return true;

        if (BattleGameManager.Instance?.ResolveNearestAirfield(PlayerOwned, GlobalPosition) is not { } airfield)
            return false;

        var flatSelf = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
        var flatTarget = new Vector3(airfield.GlobalPosition.X, 0f, airfield.GlobalPosition.Z);
        var returnSpeed = Mathf.Max(0.1f, MoveSpeed * 1.08f);
        var fuelNeededToReturn = flatSelf.DistanceTo(flatTarget) / returnSpeed + ReturnFuelReserveSeconds;
        return CurrentFuel <= fuelNeededToReturn;
    }

    void RefuelWhileParked(float delta)
    {
        var rate = maxFuelSeconds / Mathf.Max(2f, refuelSeconds);
        CurrentFuel = Mathf.Min(maxFuelSeconds, CurrentFuel + rate * delta);
        if (CurrentFuel >= maxFuelSeconds * 0.98f)
        {
            warnedLowFuel = false;
            warnedNoAirfield = false;
        }
    }

    void CrashFromFuelLoss()
    {
        if (IsDead)
            return;

        if (PlayerOwned)
            FindHud()?.ShowAlert($"{DisplayName} 燃油耗尽，已坠毁");
        CurrentFuel = 0f;
        ApplyDamage(Health + 9999f);
    }

    BattleHud? FindHud()
        => GetTree().CurrentScene?.GetNodeOrNull<BattleHud>("HUD");

    public void ApplyDamage(float amount)
    {
        if (IsDead)
            return;

        float beforeShield = Shield;

        if (amount > 0f)
        {
            secondsSinceLastDamage = 0f;

            // 1. 联盟阵地战减伤特技：如果是联盟核心兵种且在己方防御塔或基地 16 米内，伤害减少 15%
            if (UnitKey is "tank" or "medium_tank" or "artillery" or "infantry")
            {
                if (BattleGameManager.Instance is { } manager)
                {
                    bool nearDefense = false;
                    foreach (var building in manager.GetBuildings(PlayerOwned))
                    {
                        if (GodotObject.IsInstanceValid(building) && building.Health > 0f && (building.IsDefenseTurret || building.IsMainBase))
                        {
                            if (GlobalPosition.DistanceTo(building.GlobalPosition) <= 16f)
                            {
                                nearDefense = true;
                                break;
                            }
                        }
                    }
                    if (nearDefense)
                    {
                        amount *= 0.85f;
                    }
                }
            }

            // 2. 科技伤害减免（科技树升级加成）
            if (techDamageReduction > 0f)
                amount *= Mathf.Clamp(1f - techDamageReduction, 0.25f, 1f);

            // 3. 智能军等离子护盾吸收
            if (MaxShield > 0f && Shield > 0f)
            {
                if (Shield >= amount)
                {
                    Shield -= amount;
                    amount = 0f;
                }
                else
                {
                    amount -= Shield;
                    Shield = 0f;
                }
            }
        }

        var before = Health;
        Health = Mathf.Max(0f, Health - amount);

        var shieldDealt = beforeShield - Shield;
        var totalDealt = (before - Health) + shieldDealt;

        BattleFeedback.Damage(this, GlobalPosition, totalDealt, PlayerOwned, Health <= 0f);
        if (Health <= 0f)
        {
            GameState.Instance?.DeselectNode(this);
            BattleFeedback.Destroyed(this, GlobalPosition, !BattleUnitCatalog.IsInfantryLike(UnitKey));
            EmitSignal(SignalName.Died, this);
            QueueFree();
        }
    }

    public float Repair(float amount, bool showFeedback = true)
    {
        if (IsDead || amount <= 0f)
            return 0f;

        var before = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        var repaired = Health - before;
        if (showFeedback)
            BattleFeedback.Repair(this, GlobalPosition, repaired);
        return repaired;
    }

    public void SetHealthFraction(float ratio)
    {
        if (IsDead)
            return;

        Health = Mathf.Clamp(MaxHealth * Mathf.Clamp(ratio, 0.01f, 1f), 1f, MaxHealth);
        RefreshCombatOverlays();
    }

    public void ApplyTechBuff(
        string sourceId,
        float duration,
        float moveMultiplier,
        float damageMultiplier,
        float attackRangeBonus,
        float attackCooldownMultiplier,
        float defenseReduction,
        float visionBonus,
        float regenPerSecond,
        Color tint)
    {
        CaptureBaseStats();
        techBuffs.RemoveAll(buff => buff.SourceId == sourceId);
        techBuffs.Add(new RuntimeTechBuff
        {
            SourceId = sourceId,
            Remaining = Mathf.Max(0.1f, duration),
            MoveMultiplier = Mathf.Max(0.01f, moveMultiplier),
            DamageMultiplier = Mathf.Max(0.01f, damageMultiplier),
            AttackRangeBonus = attackRangeBonus,
            AttackCooldownMultiplier = Mathf.Max(0.01f, attackCooldownMultiplier),
            DefenseReduction = Mathf.Max(0f, defenseReduction),
            VisionBonus = visionBonus,
            RegenPerSecond = Mathf.Max(0f, regenPerSecond),
            Tint = tint
        });
        RecalculateTechBuffStats();
    }

    public void ConfigureVisualRig(string unitKey, Node3D? root)
    {
        visualKey = unitKey ?? "";
        bodyFacingYawOffset = 0f;
        visualRoot = root;
        weaponYawNode = null;
        weaponPitchNode = null;
        weaponPitchIsPivot = false;
        weaponYawLookOffset = Vector3.Zero;
        weaponPitchLookOffset = Vector3.Zero;
        weaponPitchPivotOffset = Vector3.Zero;
        visualAnimationPlayer = null;
        visualAnimationPlayers.Clear();
        walkAnimationName = "";
        idleAnimationName = "";
        fireAnimationName = "";
        currentVisualAnimationName = "";
        visualAnimationLockRemaining = 0f;
        infantryVisualParts.Clear();
        propellerNodes.Clear();

        if (root is null)
            return;

        visualAnimationPlayers.AddRange(root.FindChildren("*", "AnimationPlayer", true, false)
            .OfType<AnimationPlayer>()
            .ToArray());
        visualAnimationPlayer = visualAnimationPlayers.FirstOrDefault();
        ResolveVisualAnimationNames();

        switch (unitKey)
        {
            case "heavy_tank":
                weaponYawNode = root.FindChild("misc_a", true, false) as Node3D
                    ?? root.FindChild("mount2", true, false) as Node3D
                    ?? root;
                weaponPitchNode = root.FindChild("weapon", true, false) as Node3D
                    ?? root.FindChild("mount2", true, false) as Node3D
                    ?? root.FindChild("misc_b", true, false) as Node3D;
                weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                break;
            case "light_tank":
                weaponYawNode = root.FindChild("base", true, false) as Node3D ?? root;
                weaponPitchNode = root.FindChild("gun_elevate", true, false) as Node3D
                    ?? root.FindChild("coax", true, false) as Node3D;
                break;
            case "tank":
            case "medium_tank":
                weaponYawNode = root.FindChild("turret_exterior", true, false) as Node3D
                    ?? root.FindChild("mantlet_inner", true, false) as Node3D
                    ?? root;
                weaponPitchNode = root.FindChild("barrel", true, false) as Node3D
                    ?? root.FindChild("gun", true, false) as Node3D;
                weaponPitchIsPivot = false;
                HidePanzerInteriorVisuals(root);
                PreparePanzerTurretRig(root);
                weaponYawNode = root.GetNodeOrNull<Node3D>("TurretPivot") ?? weaponYawNode;
                weaponPitchNode = root.GetNodeOrNull<Node3D>("TurretPivot/GunPivot")
                    ?? root.GetNodeOrNull<Node3D>("GunPivot")
                    ?? weaponPitchNode;
                if (weaponPitchNode == root || weaponPitchNode == weaponYawNode)
                    weaponPitchNode = null;
                weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                break;
            case "artillery":
                weaponYawNode = root;
                PrepareArtilleryGunRig(root);
                weaponPitchNode = root.GetNodeOrNull<Node3D>("GunPivot") ?? root;
                weaponPitchIsPivot = weaponPitchNode.Name == "GunPivot";
                weaponPitchPivotOffset = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f);
                break;
            case "anti_air_gun":
                weaponYawNode = root.FindChild("cannon", true, false) as Node3D
                    ?? root.FindChild("weapon", true, false) as Node3D
                    ?? root;
                weaponPitchNode = root.FindChild("barrel", true, false) as Node3D
                    ?? root.FindChild("gun", true, false) as Node3D
                    ?? weaponYawNode;
                weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                break;
        }

        CaptureInfantryVisualParts(root);
        CapturePropellerNodes(root);
    }

    public void SetSelected(bool value)
    {
        Selected = value;
    }

    public void SetFogRevealed(bool revealed)
    {
        bool wasRevealed = FogRevealed;
        FogRevealed = PlayerOwned || revealed;
        if (FogRevealed)
            FogExplored = true;

        if (PlayerOwned)
        {
            // 己方单位始终全展
            Visible = true;
            CollisionLayer = originalCollisionLayer;
            CollisionMask = originalCollisionMask;
        }
        else if (FogRevealed)
        {
            // 敌方单位在视野内：完全可见
            Visible = true;
            SetVisualModulate(Colors.White);
            CollisionLayer = originalCollisionLayer;
            CollisionMask = originalCollisionMask;
        }
        else if (FogExplored)
        {
            // 敌方单位已探索但当前不在视野：半透明幽灵
            Visible = true;
            SetVisualModulate(new Color(0.72f, 0.82f, 1f, 0.28f));
            CollisionLayer = 0;
            CollisionMask = 0;
            SetSelected(false);
        }
        else
        {
            // 敌方单位未探索：完全隐藏
            Visible = false;
            SetVisualModulate(Colors.White);
            CollisionLayer = 0;
            CollisionMask = 0;
            SetSelected(false);
        }
    }

    void ProcessMove(float delta)
    {
        var flatDelta = TargetPosition - GlobalPosition;
        flatDelta.Y = 0f;
        if (flatDelta.Length() <= 0.52f)
        {
            if (queuedMoveTargets.Count > 0)
            {
                TargetPosition = queuedMoveTargets.Dequeue();
                return;
            }

            AttackMoving = false;
            Velocity = Vector3.Zero;
            MoveAndSlide();
            MaintainMovementLayer();
            if (parkingAtAirfield)
                ParkAtAirfield();
            return;
        }

        var direction = flatDelta.Normalized();
        Velocity = direction * MoveSpeed;
        FaceDirection(direction, delta);
        MoveAndSlide();
        MaintainMovementLayer();
    }

    void ProcessAttack(float delta)
    {
        if (AttackTarget is null || !IsLiveEnemyTarget(AttackTarget))
        {
            AttackTarget = null;
            if (Patrolling)
                TargetPosition = patrolHeadingToB ? patrolB : patrolA;
            else if (Guarding)
                TargetPosition = NormalizeCommandPosition(GuardFollowPosition());
            return;
        }

        var toTarget = AttackTarget.GlobalPosition - GlobalPosition;
        toTarget.Y = 0f;
        if (toTarget.Length() > AttackRange)
        {
            TargetPosition = NormalizeCommandPosition(AttackTarget.GlobalPosition);
            ProcessMove(delta);
            return;
        }

        Velocity = Vector3.Zero;
        MoveAndSlide();
        if (toTarget.Length() > 0.01f && ShouldBodyFaceAttackTarget())
            FaceDirection(toTarget, delta);
        UpdateWeaponAim(AttackTarget.GlobalPosition, false, delta);
        if (!IsWeaponYawAligned(AttackTarget.GlobalPosition))
            return;

        if (cooldownLeft <= 0f)
        {
            cooldownLeft = AttackCooldown;
            PlayFireVisualAnimation();
            CombatProjectile.Spawn(this, this, AttackTarget, AttackDamage, PlayerOwned, AttackRange, SplashRadius, SplashFalloff);
        }
    }

    void ProcessAttackGround(float delta)
    {
        if (!CanAttackGroundPoint)
        {
            AttackGrounding = false;
            return;
        }

        var toTarget = attackGroundTarget - GlobalPosition;
        toTarget.Y = 0f;
        if (toTarget.Length() > AttackRange)
        {
            TargetPosition = NormalizeCommandPosition(attackGroundTarget);
            ProcessMove(delta);
            return;
        }

        Velocity = Vector3.Zero;
        MoveAndSlide();
        if (toTarget.LengthSquared() > 0.0001f && ShouldBodyFaceAttackTarget())
            FaceDirection(toTarget, delta);
        UpdateWeaponAim(attackGroundTarget, true, delta);
        if (!IsWeaponYawAligned(attackGroundTarget))
            return;

        if (cooldownLeft <= 0f)
        {
            cooldownLeft = AttackCooldown;
            PlayFireVisualAnimation();
            CombatProjectile.SpawnGround(this, this, attackGroundTarget, AttackDamage, PlayerOwned, AttackRange, SplashRadius, SplashFalloff);
        }
    }

    Vector3 NormalizeCommandPosition(Vector3 worldPos)
    {
        if (BattleGameManager.Instance is { } manager)
            return manager.NormalizeUnitTarget(this, worldPos);
        return BattleMapCatalog.ClampToMap(worldPos, 4f);
    }

    static Vector3 NormalizeAttackGroundTarget(Vector3 worldPos)
    {
        if (BattleGameManager.Instance is { } manager)
        {
            var target = manager.ClampToPlayableMap(worldPos);
            return new Vector3(target.X, 0f, target.Z);
        }

        var clamped = BattleMapCatalog.ClampToMap(worldPos, 4f);
        return new Vector3(clamped.X, 0f, clamped.Z);
    }

    void MaintainMovementLayer()
    {
        if (BattleUnitCatalog.IsAirUnit(UnitKey))
        {
            var height = parkedAtAirfield
                ? AirfieldParkingHeight
                : CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
            GlobalPosition = new Vector3(GlobalPosition.X, height, GlobalPosition.Z);
        }
        else if (BattleUnitCatalog.IsNavalUnit(UnitKey))
        {
            GlobalPosition = new Vector3(GlobalPosition.X, BattleUnitCatalog.SpawnHeight(UnitKey), GlobalPosition.Z);
        }
    }

    void ProcessBombingRun(float delta)
    {
        var target = bombingRunOnAttackLeg ? bombingRunExit : bombingRunApproach;
        TargetPosition = NormalizeCommandPosition(target);
        ProcessMove(delta);

        var flatSelf = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
        if (!bombingRunOnAttackLeg)
        {
            var flatApproach = new Vector3(bombingRunApproach.X, 0f, bombingRunApproach.Z);
            if (flatSelf.DistanceTo(flatApproach) <= BomberArrivalThreshold)
                bombingRunOnAttackLeg = true;
            return;
        }

        var runOffset = flatSelf - new Vector3(bombingRunStart.X, 0f, bombingRunStart.Z);
        var runProgress = runOffset.Dot(bombingRunDirection);
        if (!bombingRunVolleyReleased && runProgress + 0.3f >= bombingRunReleaseDistance)
            ReleaseBombingRunVolley();

        var flatExit = new Vector3(bombingRunExit.X, 0f, bombingRunExit.Z);
        if (flatSelf.DistanceTo(flatExit) <= BomberFinishThreshold)
        {
            CancelBombingRun();
            Stop();
        }
    }

    void AcquireAttackMoveTarget(float delta)
    {
        acquireTimer -= delta;
        if (acquireTimer > 0f || BattleGameManager.Instance is not { } manager)
            return;

        acquireTimer = 0.35f;
        var target = FindBestTarget(manager, AttackRange);
        if (target is not null)
            AttackTarget = target;
    }

    void AcquireGuardTarget(float delta)
    {
        acquireTimer -= delta;
        if (acquireTimer > 0f || BattleGameManager.Instance is not { } manager)
            return;

        acquireTimer = 0.45f;
        AttackTarget = FindBestTarget(manager, AttackRange);
    }

    void ProcessPatrol(float delta)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        acquireTimer -= delta;
        if (acquireTimer <= 0f)
        {
            acquireTimer = 0.35f;
            var target = FindBestTarget(manager, AttackRange * 1.45f);
            if (target is not null)
            {
                AttackTarget = target;
                return;
            }
        }

        if (IsIdle())
        {
            patrolHeadingToB = !patrolHeadingToB;
            TargetPosition = patrolHeadingToB ? patrolB : patrolA;
        }
    }

    void ProcessGuard(float delta)
    {
        if (guardTarget is null || !GodotObject.IsInstanceValid(guardTarget) || guardTarget.IsDead)
        {
            Stop();
            return;
        }

        if (BattleGameManager.Instance is not { } manager)
            return;

        acquireTimer -= delta;
        if (acquireTimer <= 0f)
        {
            acquireTimer = 0.35f;
            var target = FindBestTarget(manager, AttackRange * 1.8f);
            if (target is not null && target.GlobalPosition.DistanceTo(guardTarget.GlobalPosition) <= 22f)
            {
                AttackTarget = target;
                return;
            }
        }

        if (GlobalPosition.DistanceTo(guardTarget.GlobalPosition) > 6f || IsIdle())
            TargetPosition = NormalizeCommandPosition(GuardFollowPosition());
    }

    Vector3 GuardFollowPosition()
    {
        if (guardTarget is null || !GodotObject.IsInstanceValid(guardTarget))
            return GlobalPosition;

        var toSelf = GlobalPosition - guardTarget.GlobalPosition;
        toSelf.Y = 0f;
        var direction = toSelf.LengthSquared() > 0.1f ? toSelf.Normalized() : Vector3.Right;
        return guardTarget.GlobalPosition + direction * 4f;
    }

    Node3D? FindBestTarget(BattleGameManager manager, float maxRange)
    {
        Node3D? target = null;
        var maxDistanceSquared = maxRange * maxRange;
        var bestScore = float.NegativeInfinity;

        foreach (var unit in manager.GetUnits(!PlayerOwned))
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.IsDead)
                continue;
            var distanceSquared = unit.GlobalPosition.DistanceSquaredTo(GlobalPosition);
            if (distanceSquared > maxDistanceSquared)
                continue;
            var score = TargetPriorityScore(unit, distanceSquared);
            if (score <= bestScore)
                continue;
            bestScore = score;
            target = unit;
        }

        foreach (var building in manager.GetBuildings(!PlayerOwned))
        {
            if (!GodotObject.IsInstanceValid(building) || building.Health <= 0f)
                continue;
            var distanceSquared = building.GlobalPosition.DistanceSquaredTo(GlobalPosition);
            if (distanceSquared > maxDistanceSquared)
                continue;
            var score = TargetPriorityScore(building, distanceSquared);
            if (score <= bestScore)
                continue;
            bestScore = score;
            target = building;
        }

        return target;
    }

    float TargetPriorityScore(Node3D target, float distanceSquared)
    {
        var score = 1000f - Mathf.Sqrt(distanceSquared) * 6f;
        if (target is RtsUnit unit)
        {
            score += 260f; // 优先攻击所有能反击的敌方移动单位

            var targetIsAir = BattleUnitCatalog.IsAirUnit(unit.UnitKey);
            var targetIsInfantry = BattleUnitCatalog.IsInfantryLike(unit.UnitKey);
            var targetIsNaval = BattleUnitCatalog.IsNavalUnit(unit.UnitKey);

            if (targetIsAir)
                score += UnitKey is "anti_air_gun" or "fighter" ? 520f : -380f;
            if (UnitKey is "infantry_flamethrower" or "flamethrower" && targetIsInfantry)
                score += 430f;
            if (BattleUnitCatalog.IsInfantryLike(UnitKey) && targetIsInfantry)
                score += 120f;
            if (BattleUnitCatalog.IsNavalUnit(UnitKey) && targetIsNaval)
                score += 300f;
            if (UnitKey == "fighter" && targetIsAir)
                score += 220f;
        }
        else if (target is RtsBuilding building)
        {
            score += -120f; // 大幅降低对非防御性建筑的默认攻击权重
            if (UnitKey is "artillery" or "bomber" or "destroyer_ship" or "transport_ship")
                score += 550f; // 攻城和轰炸单位依然优先轰炸建筑
            if (UnitKey is "anti_air_gun" or "fighter")
                score -= 360f;
            if (building.IsDefenseTurret)
                score += 280f; // 具有反击能力的炮塔，权重高一些
        }

        return score;
    }

    bool IsIdle()
    {
        var flatDelta = TargetPosition - GlobalPosition;
        flatDelta.Y = 0f;
        return flatDelta.LengthSquared() <= 0.05f;
    }

    bool ShouldBodyFaceAttackTarget()
        => BattleUnitCatalog.IsAirUnit(UnitKey)
            || BattleUnitCatalog.IsInfantryLike(UnitKey)
            || BattleUnitCatalog.IsNavalUnit(UnitKey)
            || weaponYawNode is null;

    public void FaceDirectionImmediate(Vector3 direction)
    {
        direction.Y = 0f;
        if (direction.LengthSquared() <= 0.0001f)
            return;

        Rotation = new Vector3(Rotation.X, ResolveFacingYaw(direction), Rotation.Z);
    }

    float ResolveFacingYaw(Vector3 direction)
        => Mathf.Atan2(direction.X, direction.Z) + bodyFacingYawOffset;

    void FaceDirection(Vector3 direction, float delta)
    {
        direction.Y = 0f;
        if (direction.LengthSquared() <= 0.0001f)
            return;

        var targetYaw = ResolveFacingYaw(direction);
        var currentYaw = Rotation.Y;
        var turnStep = Mathf.Clamp(TurnSpeed * delta, 0f, 1f);
        Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(currentYaw, targetYaw, turnStep), Rotation.Z);
    }

    void UpdateWeaponAim(Vector3 target, bool groundAttack, float delta)
    {
        if (weaponYawNode is null && weaponPitchNode is null)
            return;

        if (!IsNodeReadyForWorldAim(weaponYawNode) || !IsNodeReadyForWorldAim(weaponPitchNode))
            return;

        var aimTarget = target;
        aimTarget.Y = GlobalPosition.Y + (BattleUnitCatalog.IsArtilleryLike(visualKey) ? 2.3f : 0.65f);

        if (weaponYawNode is not null)
        {
            var yawTarget = aimTarget;
            yawTarget.Y = weaponYawNode.GlobalPosition.Y;
            ApplySmoothLookAt(weaponYawNode, yawTarget, weaponYawLookOffset, delta, 8.5f);
        }

        if (weaponPitchNode is not null)
        {
            var pitchTarget = aimTarget;
            if (BattleUnitCatalog.IsArtilleryLike(visualKey) || groundAttack)
                pitchTarget.Y += Mathf.Clamp(AttackRange * 0.08f, 0.85f, 2.8f);
            if (weaponPitchIsPivot)
            {
                var localTarget = weaponPitchNode.ToLocal(pitchTarget);
                var desired = new Vector3(Mathf.Atan2(localTarget.Y, -localTarget.Z), 0f, 0f) + weaponPitchPivotOffset;
                weaponPitchNode.Rotation = SmoothEuler(weaponPitchNode.Rotation, desired, delta, 7.5f);
            }
            else
            {
                ApplySmoothLookAt(weaponPitchNode, pitchTarget, weaponPitchLookOffset, delta, 7.5f);
            }
        }
    }

    bool IsWeaponYawAligned(Vector3 target)
    {
        // 如果没有独立的武器旋转炮塔（如普通步兵），身体在射程内即允许随时攻击，避免拥挤摩擦导致卡住无法开火
        if (weaponYawNode is null)
            return true;

        var yawNode = weaponYawNode;
        if (!IsNodeReadyForWorldAim(yawNode))
            return false;

        var toTarget = target - yawNode.GlobalPosition;
        toTarget.Y = 0f;
        if (toTarget.LengthSquared() <= 0.0001f)
            return true;

        var forward = ResolveAimForward(yawNode, weaponYawLookOffset);
        forward.Y = 0f;
        if (forward.LengthSquared() <= 0.0001f)
            return true;

        // 放宽旋转对齐容差（点积从 0.965f 降到 0.82f，即夹角约 35 度以内均可开火）
        return forward.Normalized().Dot(toTarget.Normalized()) >= 0.82f;
    }

    static Vector3 ResolveAimForward(Node3D node, Vector3 lookOffset)
    {
        var localForward = Basis.FromEuler(lookOffset) * new Vector3(0f, 0f, -1f);
        return node.GlobalTransform.Basis * localForward;
    }

    static void ApplySmoothLookAt(Node3D node, Vector3 target, Vector3 offset, float delta, float speed)
    {
        if (!IsNodeReadyForWorldAim(node))
            return;

        var previous = node.GlobalTransform;
        node.LookAt(target, Vector3.Up, true);
        var desiredRotation = node.Rotation + offset;
        node.GlobalTransform = previous;
        node.Rotation = SmoothEuler(node.Rotation, desiredRotation, delta, speed);
    }

    static bool IsNodeReadyForWorldAim(Node3D? node)
        => node is not null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();

    static Vector3 SmoothEuler(Vector3 current, Vector3 desired, float delta, float speed)
    {
        var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * delta);
        return new Vector3(
            Mathf.LerpAngle(current.X, desired.X, t),
            Mathf.LerpAngle(current.Y, desired.Y, t),
            Mathf.LerpAngle(current.Z, desired.Z, t));
    }

    void ResolveVisualAnimationNames()
    {
        if (visualAnimationPlayers.Count == 0)
            return;

        foreach (var player in visualAnimationPlayers)
        {
            player.PlaybackDefaultBlendTime = 0.2f;
            foreach (var animation in player.GetAnimationList())
            {
                var name = animation.ToString();
                var lower = name.ToLowerInvariant();
                if (string.IsNullOrEmpty(walkAnimationName)
                    && (lower.Contains("walk") || lower.Contains("run") || lower.Contains("move")))
                    walkAnimationName = name;
                if (string.IsNullOrEmpty(idleAnimationName) && lower.Contains("idle"))
                    idleAnimationName = name;
                if (string.IsNullOrEmpty(fireAnimationName)
                    && (lower.Contains("fire") || lower.Contains("shoot") || lower.Contains("attack")))
                    fireAnimationName = name;
            }
        }
    }

    void CaptureInfantryVisualParts(Node3D root)
    {
        if (!BattleUnitCatalog.IsInfantryLike(visualKey))
            return;

        var allNodes = root.FindChildren("*", "Node3D", true, false)
            .OfType<Node3D>()
            .ToArray();
        var bodyNodes = allNodes
            .Where(node => IsInfantryBodyNode(node, root))
            .Where(node => !HasInfantryBodyAncestor(node, root))
            .ToList();
        if (bodyNodes.Count == 0)
            bodyNodes.Add(root);

        for (var i = 0; i < bodyNodes.Count; i++)
            AddAnimatedInfantryPart(bodyNodes[i], InfantryVisualPartKind.Body, i * 1.7f);

        foreach (var node in allNodes)
        {
            if (!TryResolveInfantryPartKind(node.Name.ToString(), out var kind))
                continue;
            var bodyIndex = FindInfantryBodyIndex(node, bodyNodes);
            AddAnimatedInfantryPart(node, kind, bodyIndex * 1.7f);
        }
    }

    void AddAnimatedInfantryPart(Node3D node, InfantryVisualPartKind kind, float phase)
    {
        if (infantryVisualParts.Any(part => part.Node == node))
            return;

        infantryVisualParts.Add(new AnimatedVisualPart
        {
            Node = node,
            RestPosition = node.Position,
            RestRotation = node.Rotation,
            Phase = phase,
            Kind = kind
        });
    }

    static bool IsInfantryBodyNode(Node3D node, Node3D root)
    {
        if (node == root)
            return false;

        var lower = node.Name.ToString().ToLowerInvariant();
        return !lower.Contains("squad")
            && (lower.StartsWith("infantry_")
                || lower.StartsWith("soldier")
                || lower.StartsWith("rifleman")
                || lower.StartsWith("trooper"));
    }

    static bool HasInfantryBodyAncestor(Node3D node, Node3D root)
    {
        var parent = node.GetParent();
        while (parent is Node3D parent3D && parent3D != root)
        {
            if (IsInfantryBodyNode(parent3D, root))
                return true;
            parent = parent.GetParent();
        }
        return false;
    }

    static bool TryResolveInfantryPartKind(string name, out InfantryVisualPartKind kind)
    {
        var lower = name.ToLowerInvariant().Replace("_", "").Replace("-", "");
        if (lower.Contains("strideleftleg") || lower.Contains("leftleg"))
        {
            kind = InfantryVisualPartKind.LeftLeg;
            return true;
        }

        if (lower.Contains("striderightleg") || lower.Contains("rightleg"))
        {
            kind = InfantryVisualPartKind.RightLeg;
            return true;
        }

        if (lower.Contains("strideleftarm") || lower.Contains("leftarm"))
        {
            kind = InfantryVisualPartKind.LeftArm;
            return true;
        }

        if (lower.Contains("striderightarm") || lower.Contains("rightarm"))
        {
            kind = InfantryVisualPartKind.RightArm;
            return true;
        }

        kind = InfantryVisualPartKind.Body;
        return false;
    }

    static int FindInfantryBodyIndex(Node3D node, IReadOnlyList<Node3D> bodyNodes)
    {
        var parent = node.GetParent();
        while (parent is Node3D parent3D)
        {
            for (var i = 0; i < bodyNodes.Count; i++)
            {
                if (bodyNodes[i] == parent3D)
                    return i;
            }
            parent = parent.GetParent();
        }
        return Mathf.Max(0, bodyNodes.Count - 1);
    }

    void CapturePropellerNodes(Node3D root)
    {
        if (!BattleUnitCatalog.IsAirUnit(visualKey))
            return;

        var allNodes = root.FindChildren("*", "Node3D", true, false)
            .OfType<Node3D>()
            .ToArray();
        var primaryNodes = allNodes.Where(IsPrimaryPropellerNode).ToArray();
        foreach (var node in primaryNodes)
        {
            if (!HasCapturedPropellerAncestor(node))
                propellerNodes.Add(node);
        }

        if (propellerNodes.Count > 0)
            return;

        foreach (var node in allNodes.Where(IsFallbackPropellerNode))
        {
            if (!HasCapturedPropellerAncestor(node))
                propellerNodes.Add(node);
        }
    }

    bool HasCapturedPropellerAncestor(Node3D node)
    {
        var parent = node.GetParent();
        while (parent is Node3D parent3D)
        {
            if (propellerNodes.Contains(parent3D))
                return true;
            parent = parent.GetParent();
        }
        return false;
    }

    static bool IsPrimaryPropellerNode(Node3D node)
    {
        var lower = node.Name.ToString().ToLowerInvariant();
        return !lower.Contains("blade")
            && !lower.Contains("hub")
            && !lower.Contains("blur")
            && (lower.Contains("propeller")
                || lower.Contains("rotor")
                || lower == "prop"
                || lower.StartsWith("prop_")
                || lower.StartsWith("prop-"));
    }

    static bool IsFallbackPropellerNode(Node3D node)
    {
        var lower = node.Name.ToString().ToLowerInvariant();
        return lower.Contains("propeller")
            || lower.Contains("prop")
            || lower.Contains("rotor")
            || lower.Contains("blade");
    }

    void UpdateVisualState(float delta)
    {
        var actuallyMoving = Velocity.LengthSquared() > 0.08f || bombingRunActive || parkingAtAirfield;
        if (actuallyMoving)
        {
            stationaryDuration = 0f;
        }
        else
        {
            stationaryDuration += delta;
        }

        var moving = actuallyMoving || (stationaryDuration < 0.22f);
        UpdateAnimationPlayback(moving, delta);
        UpdateInfantryStride(moving);
        UpdatePropellers(delta, moving);

        if (BattleUnitCatalog.IsNavalUnit(UnitKey))
        {
            UpdateNavalWaves(actuallyMoving, delta);
        }
    }

    void UpdateAnimationPlayback(bool moving, float delta)
    {
        if (visualAnimationPlayers.Count == 0)
            return;

        if (visualAnimationLockRemaining > 0f)
        {
            visualAnimationLockRemaining = Mathf.Max(0f, visualAnimationLockRemaining - delta);
            return;
        }

        var next = moving && !string.IsNullOrEmpty(walkAnimationName)
            ? walkAnimationName
            : idleAnimationName;
        if (string.IsNullOrEmpty(next) || next == currentVisualAnimationName)
            return;

        foreach (var player in visualAnimationPlayers)
        {
            if (player.HasAnimation(next))
                player.Play(next);
        }
        currentVisualAnimationName = next;
    }

    void PlayFireVisualAnimation()
    {
        if (visualAnimationPlayers.Count == 0 || string.IsNullOrEmpty(fireAnimationName))
            return;

        foreach (var player in visualAnimationPlayers)
        {
            if (player.HasAnimation(fireAnimationName))
                player.Play(fireAnimationName);
        }
        currentVisualAnimationName = fireAnimationName;
        var animation = visualAnimationPlayers
            .Select(player => player.HasAnimation(fireAnimationName) ? player.GetAnimation(fireAnimationName) : null)
            .FirstOrDefault(candidate => candidate is not null);
        visualAnimationLockRemaining = animation is null
            ? 0.18f
            : Mathf.Max(0.12f, (float)animation.Length * 0.92f);
    }

    void UpdateInfantryStride(bool moving)
    {
        if (infantryVisualParts.Count == 0)
            return;

        // 根据实际运动速度按比例调节步伐周期
        float speedRatio = Velocity.Length() / Mathf.Max(0.1f, MoveSpeed);
        var stride = moving ? visualMotionTime * (6.5f + 4.0f * speedRatio) : 0f;

        foreach (var part in infantryVisualParts)
        {
            var wave = Mathf.Sin(stride + part.Phase);
            var counterWave = Mathf.Sin(stride + part.Phase + Mathf.Pi);
            switch (part.Kind)
            {
                case InfantryVisualPartKind.LeftLeg:
                    ApplyInfantryLimbStride(part, moving, wave, 0.42f);
                    break;
                case InfantryVisualPartKind.RightLeg:
                    ApplyInfantryLimbStride(part, moving, counterWave, 0.42f);
                    break;
                case InfantryVisualPartKind.LeftArm:
                    ApplyInfantryLimbStride(part, moving, counterWave, 0.30f);
                    break;
                case InfantryVisualPartKind.RightArm:
                    ApplyInfantryLimbStride(part, moving, wave, 0.30f);
                    break;
                default:
                    // 躯干上下起伏与偏航/侧倾 - 适当改小以防止士兵产生夸张的“弹簧震动”
                    var bob = moving ? Mathf.Abs(wave) * 0.022f : 0f;
                    var forwardStep = moving ? Mathf.Cos(stride + part.Phase) * 0.012f : 0f;
                    var sideSway = moving ? Mathf.Sin(stride * 0.5f + part.Phase) * 0.006f : 0f;
                    var lean = moving ? wave * 0.022f : 0f;
                    part.Node.Position = part.RestPosition + new Vector3(sideSway, bob, forwardStep);
                    part.Node.Rotation = part.RestRotation + new Vector3(lean * 0.45f, lean * 0.65f, lean);
                    break;
            }
        }
    }

    static void ApplyInfantryLimbStride(AnimatedVisualPart part, bool moving, float wave, float swing)
    {
        if (!moving)
        {
            part.Node.Position = part.RestPosition;
            part.Node.Rotation = part.RestRotation;
            return;
        }

        // 仅在 Y 轴做极小的提足抬升（最大 0.015m），移除 Z 轴前后平移以防肢体关节脱臼分离
        float yOffset = (part.Kind == InfantryVisualPartKind.LeftLeg || part.Kind == InfantryVisualPartKind.RightLeg)
            ? Mathf.Abs(wave) * 0.015f
            : 0f;

        part.Node.Position = part.RestPosition + new Vector3(0f, yOffset, 0f);
        // 主轴在 X 轴前后摆动，并提供微小的 Z 轴 Roll 偏角以保持身形协调而不再外八字外翻
        part.Node.Rotation = part.RestRotation + new Vector3(wave * swing, 0f, wave * swing * 0.04f);
    }

    void UpdatePropellers(float delta, bool moving)
    {
        if (propellerNodes.Count == 0)
            return;

        var speed = parkedAtAirfield ? 11f : moving ? 58f : 36f;
        foreach (var propeller in propellerNodes)
            propeller.RotateObjectLocal(Vector3.Forward, speed * delta);
    }

    void ClearQueuedMovement()
    {
        queuedMoveTargets.Clear();
        holdPosition = false;
    }

    void ClearSpecialOrders()
    {
        CancelBombingRun();
        parkingAtAirfield = false;
        parkedAtAirfield = false;
    }

    void CancelBombingRun()
    {
        bombingRunActive = false;
        bombingRunOnAttackLeg = false;
        bombingRunVolleyReleased = false;
        bombingRunReleaseDistance = 0f;
    }

    void ParkAtAirfield()
    {
        parkingAtAirfield = false;
        parkedAtAirfield = true;
        TargetPosition = parkedPosition;
        Velocity = Vector3.Zero;
        GlobalPosition = new Vector3(parkedPosition.X, AirfieldParkingHeight, parkedPosition.Z);
    }

    void ReleaseBombingRunVolley()
    {
        bombingRunVolleyReleased = true;
        var releaseCenter = bombingRunStart + bombingRunDirection * bombingRunReleaseDistance;
        var bombDamage = Mathf.Max(1f, AttackDamage * BomberDamageMultiplier);
        var splashRadius = Mathf.Max(1.5f, SplashRadius);

        var bombIndex = 0;
        for (var row = 0; row < 2; row++)
        {
            var forwardOffset = row == 0 ? -BomberReleaseSpacingForward * 0.5f : BomberReleaseSpacingForward * 0.5f;
            for (var col = 0; col < 2; col++)
            {
                var sideOffset = col == 0 ? -BomberReleaseSpacingSide * 0.5f : BomberReleaseSpacingSide * 0.5f;
                var impactPoint = releaseCenter
                    + bombingRunDirection * forwardOffset
                    + bombingRunPerpendicular * sideOffset
                    + ComputeBombingRunJitter(bombIndex);
                impactPoint.Y = 0f;
                CombatProjectile.SpawnGround(this, this, impactPoint, bombDamage, PlayerOwned, AttackRange, splashRadius, SplashFalloff);
                bombIndex++;
            }
        }
    }

    Vector3 ComputeBombingRunJitter(int bombIndex)
    {
        var seed = NetId != 0 ? NetId : (int)(GetInstanceId() & 0x7fffffff);
        var noiseA = Mathf.Sin((seed * 0.731f + bombIndex * 1.913f + 0.37f) * 12.9898f);
        var noiseB = Mathf.Sin((seed * 1.117f + bombIndex * 2.357f + 1.19f) * 78.233f);
        return bombingRunDirection * (noiseA * BomberImpactJitter)
             + bombingRunPerpendicular * (noiseB * BomberImpactJitter);
    }

    void PreparePanzerTurretRig(Node3D root)
    {
        if (root.GetNodeOrNull<Node3D>("TurretPivot") is not null)
            return;

        var turretAnchor = root.FindChild("turret_exterior", true, false) as Node3D
            ?? root.FindChild("mantlet_inner", true, false) as Node3D;
        var gunPivotAnchor = root.FindChild("mantlet_inner", true, false) as Node3D
            ?? root.FindChild("barrel", true, false) as Node3D;
        var gunAnchor = root.FindChild("barrel", true, false) as Node3D;
        if (turretAnchor is null || gunPivotAnchor is null || gunAnchor is null)
            return;

        var turretPivot = new Node3D
        {
            Name = "TurretPivot",
            Position = turretAnchor.Position
        };
        root.AddChild(turretPivot);

        var gunPivot = new Node3D
        {
            Name = "GunPivot",
            Position = gunPivotAnchor.Position - turretAnchor.Position
        };
        turretPivot.AddChild(gunPivot);

        foreach (var child in root.GetChildren().OfType<Node3D>().ToArray())
        {
            if (child == turretPivot)
                continue;
            if (!BelongsToPanzerTurret(child.Name))
                continue;

            var originalPosition = child.Position;
            root.RemoveChild(child);
            if (BelongsToPanzerGun(child.Name))
            {
                gunPivot.AddChild(child);
                child.Position = originalPosition - turretAnchor.Position - gunPivot.Position;
            }
            else
            {
                turretPivot.AddChild(child);
                child.Position = originalPosition - turretAnchor.Position;
            }
        }
    }

    static void HidePanzerInteriorVisuals(Node3D root)
    {
        foreach (var mesh in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (IsPanzerInteriorVisual(mesh.Name))
                mesh.Visible = false;
        }
    }

    static bool IsPanzerExteriorTurretDetail(string lower)
    {
        return lower is "turret_exterior"
            || lower.StartsWith("mantlet")
            || lower.StartsWith("barrel");
    }

    static bool IsPanzerInteriorVisual(string name)
    {
        var lower = name.ToLowerInvariant();
        if (IsPanzerExteriorTurretDetail(lower))
            return false;

        return lower is "gun"
            or "floor"
            or "floor_circle"
            or "recoil_guard"
            or "electricals"
            or "gyro"
            or "trigger"
            or "wires"
            or "driver_panel"
            or "cupola_details"
            or "gunner_sight"
            or "driver_block"
            or "driver_slit"
            or "radioman_hatch"
            or "driver_hatch"
            or "hull_sideport_driver"
            or "cupola_exterior"
            or "cupola_rim"
            or "gunner_front_port"
            or "loader_front_port"
            || lower.StartsWith("turret_interior")
            || lower.StartsWith("turret_details")
            || lower.StartsWith("cmdr_hatch_")
            || lower.StartsWith("turret_door_")
            || lower.StartsWith("turret_sideslit_")
            || lower.StartsWith("breech")
            || lower.StartsWith("ammo_bin_")
            || lower.StartsWith("shell.")
            || lower.StartsWith("mg_ammo_")
            || lower.StartsWith("lever_")
            || lower.StartsWith("switch_")
            || lower.StartsWith("gunner_")
            || lower.StartsWith("loader_");
    }

    void PrepareArtilleryGunRig(Node3D root)
    {
        if (root.GetNodeOrNull<Node3D>("GunPivot") is not null)
            return;

        MeshInstance3D? barrelCandidate = root.GetNodeOrNull<MeshInstance3D>("object_6");
        var bestScore = 0f;
        foreach (var mesh in root.GetChildren().OfType<MeshInstance3D>())
        {
            if (mesh.Mesh is null)
                continue;

            var size = mesh.Mesh.GetAabb().Size.Abs();
            var dims = new[] { size.X, size.Y, size.Z }.OrderByDescending(value => value).ToArray();
            if (dims[1] <= 0.0001f)
                continue;

            var score = dims[0] / dims[1];
            if (dims[0] < 2f || score <= bestScore || (barrelCandidate is not null && mesh != barrelCandidate))
                continue;

            bestScore = score;
            barrelCandidate = mesh;
        }

        if (barrelCandidate is null)
            return;

        var pivot = new Node3D
        {
            Name = "GunPivot",
            Position = barrelCandidate.Position
        };
        root.AddChild(pivot);
        root.RemoveChild(barrelCandidate);
        pivot.AddChild(barrelCandidate);
        barrelCandidate.Position = Vector3.Zero;
    }

    static bool BelongsToPanzerTurret(string name)
    {
        var lower = name.ToLowerInvariant();
        return !IsPanzerInteriorVisual(lower) && IsPanzerExteriorTurretDetail(lower);
    }

    static bool BelongsToPanzerGun(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.StartsWith("barrel")
            || lower.StartsWith("mantlet");
    }

    public void DebugDumpPanzerVisibleNodes()
    {
        if (visualRoot is not Node3D root)
            return;

        if (visualKey is not "tank" and not "medium_tank")
            return;

        foreach (var node in EnumerateDebugNodes(root))
        {
            if (node is not VisualInstance3D visual || !visual.Visible)
                continue;

            var lower = node.Name.ToString().ToLowerInvariant();
            if (!lower.Contains("turret")
                && !lower.Contains("cupola")
                && !lower.Contains("hatch")
                && !lower.Contains("door")
                && !lower.Contains("barrel")
                && !lower.Contains("mantlet")
                && !lower.Contains("floor")
                && !lower.Contains("breech")
                && !lower.Contains("gunner")
                && !lower.Contains("loader")
                && !lower.Contains("driver"))
                continue;

            GD.Print($"PANZER_VISIBLE {Name} :: {node.Name} class={node.GetClass()} parent={node.GetParent()?.Name} pos={node.Position} gpos={node.GlobalPosition}");
        }

        static IEnumerable<Node3D> EnumerateDebugNodes(Node3D parent)
        {
            foreach (var child in parent.GetChildren().OfType<Node3D>())
            {
                yield return child;
                foreach (var descendant in EnumerateDebugNodes(child))
                    yield return descendant;
            }
        }
    }

    bool IsLiveEnemyTarget(Node3D target)
        => target switch
        {
            RtsUnit unit => unit.PlayerOwned != PlayerOwned && !unit.IsDead && unit.FogRevealed,
            RtsBuilding building => building.PlayerOwned != PlayerOwned && building.Health > 0f && building.FogRevealed,
            _ => GodotObject.IsInstanceValid(target)
        };

    float GetUnitHealthBarHeight()
    {
        if (BattleUnitCatalog.IsAirUnit(UnitKey))
            return 2.95f;
        if (UnitKey.StartsWith("infantry"))
            return 1.15f; // 步兵血条降低至1.15m高度，紧贴头顶
        if (UnitKey.Contains("light_tank"))
            return 1.65f;
        return 2.05f; // 战车血条降为2.05m高度
    }

    float GetUnitHealthBarWidth()
    {
        if (BattleUnitCatalog.IsAirUnit(UnitKey))
            return 2.8f;
        if (UnitKey.StartsWith("infantry"))
            return 0.9f; // 步兵血条缩窄为0.9m宽，匹配身形
        return 2.2f;
    }

    void EnsureCombatOverlays()
    {
        if (GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar") is null)
        {
            var isAir = BattleUnitCatalog.IsAirUnit(UnitKey);
            var bar = new WorldHealthBar3D
            {
                Name = "WorldHealthBar",
                Width = GetUnitHealthBarWidth(),
                HeightOffset = GetUnitHealthBarHeight(),
                Depth = isAir ? 0.20f : 0.18f
            };
            AddChild(bar);
        }

        selectionRing = GetNodeOrNull<MeshInstance3D>("SelectionRing");
        if (selectionRing is null)
        {
            selectionRing = new MeshInstance3D
            {
                Name = "SelectionRing",
                Position = SelectionRingPosition(),
                Mesh = new TorusMesh
                {
                    InnerRadius = SelectionRingRadius(),
                    OuterRadius = SelectionRingRadius() + 0.10f,
                    RingSegments = 48
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = PlayerOwned
                        ? new Color(0.20f, 0.82f, 1f, 0.86f)
                        : new Color(1f, 0.34f, 0.20f, 0.86f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                },
                Visible = false
            };
            AddChild(selectionRing);
        }

        techRing = GetNodeOrNull<MeshInstance3D>("TechRing");
        if (techRing is null)
        {
            techRing = new MeshInstance3D
            {
                Name = "TechRing",
                Position = TechRingPosition(),
                Mesh = new TorusMesh
                {
                    InnerRadius = SelectionRingRadius() + 0.18f,
                    OuterRadius = SelectionRingRadius() + 0.28f,
                    RingSegments = 48
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.38f, 1f, 0.50f, 0.54f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                },
                Visible = false
            };
            AddChild(techRing);
        }
    }

    void RefreshCombatOverlays()
    {
        if (GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar") is { } worldHealthBar)
            worldHealthBar.Position = new Vector3(0f, GetUnitHealthBarHeight(), 0f);

        selectionRing ??= GetNodeOrNull<MeshInstance3D>("SelectionRing");
        if (selectionRing?.Mesh is TorusMesh selectionMesh)
        {
            selectionRing.Position = SelectionRingPosition();
            selectionMesh.InnerRadius = SelectionRingRadius();
            selectionMesh.OuterRadius = SelectionRingRadius() + 0.10f;
        }

        techRing ??= GetNodeOrNull<MeshInstance3D>("TechRing");
        if (techRing?.Mesh is TorusMesh techMesh)
        {
            techRing.Position = TechRingPosition();
            techMesh.InnerRadius = SelectionRingRadius() + 0.18f;
            techMesh.OuterRadius = SelectionRingRadius() + 0.28f;
        }
    }

    void ResetCapturedBaseStats()
    {
        baseMaxHealth = 0f;
        baseMoveSpeed = 0f;
        baseAttackDamage = 0f;
        baseAttackRange = 0f;
        baseAttackCooldown = 0f;
        baseSplashRadius = 0f;
        baseSplashFalloff = 0f;
    }

    void CaptureBaseStats()
    {
        if (baseMaxHealth > 0f)
            return;

        baseMaxHealth = MaxHealth;
        baseMoveSpeed = MoveSpeed;
        baseAttackDamage = AttackDamage;
        baseAttackRange = AttackRange;
        baseAttackCooldown = AttackCooldown;
        if (SplashRadius <= 0.05f)
            SplashRadius = BattleUnitCatalog.SplashRadius(UnitKey);
        if (SplashFalloff <= 0f || SplashFalloff > 1f)
            SplashFalloff = BattleUnitCatalog.SplashFalloff(UnitKey);
        baseSplashRadius = SplashRadius;
        baseSplashFalloff = SplashFalloff;
    }

    void UpdateTechBuffs(float delta)
    {
        if (techBuffs.Count == 0)
            return;

        var changed = false;
        for (var i = techBuffs.Count - 1; i >= 0; i--)
        {
            techBuffs[i].Remaining -= delta;
            if (techBuffs[i].Remaining > 0f)
                continue;
            techBuffs.RemoveAt(i);
            changed = true;
        }

        if (techRegenPerSecond > 0f && Health < MaxHealth)
        {
            techRegenCarry += techRegenPerSecond * delta;
            if (techRegenCarry >= 1f)
            {
                var repair = Mathf.Floor(techRegenCarry);
                techRegenCarry -= repair;
                Repair(repair, false);
            }
        }
        else
        {
            techRegenCarry = 0f;
        }

        if (changed)
            RecalculateTechBuffStats();
    }

    void RecalculateTechBuffStats()
    {
        CaptureBaseStats();
        var healthRatio = MaxHealth > 0f ? Mathf.Clamp(Health / MaxHealth, 0f, 1f) : 1f;
        var moveMultiplier = 1f;
        var damageMultiplier = 1f;
        var attackRangeBonus = 0f;
        var attackCooldownMultiplier = 1f;
        var defenseReduction = 0f;
        var visionBonus = 0f;
        var regenPerSecond = 0f;

        for (var i = 0; i < techBuffs.Count; i++)
        {
            var buff = techBuffs[i];
            moveMultiplier *= buff.MoveMultiplier;
            damageMultiplier *= buff.DamageMultiplier;
            attackRangeBonus += buff.AttackRangeBonus;
            attackCooldownMultiplier *= buff.AttackCooldownMultiplier;
            defenseReduction += buff.DefenseReduction;
            visionBonus += buff.VisionBonus;
            regenPerSecond += buff.RegenPerSecond;
        }

        MaxHealth = baseMaxHealth;
        MoveSpeed = baseMoveSpeed * moveMultiplier;
        AttackDamage = Mathf.Round(baseAttackDamage * damageMultiplier);
        AttackRange = baseAttackRange + attackRangeBonus;
        AttackCooldown = Mathf.Max(0.12f, baseAttackCooldown * attackCooldownMultiplier);
        SplashRadius = baseSplashRadius;
        SplashFalloff = baseSplashFalloff;
        VisionBonus = visionBonus;
        techDamageReduction = Mathf.Clamp(defenseReduction, 0f, 0.75f);
        techRegenPerSecond = regenPerSecond;
        Health = Mathf.Clamp(MaxHealth * healthRatio, 1f, MaxHealth);

        if (techBuffs.Count > 0 && techRing?.MaterialOverride is StandardMaterial3D ringMaterial)
            ringMaterial.AlbedoColor = techBuffs[^1].Tint;
        SetTechRingVisible(techBuffs.Count > 0);
    }

    void SetTechRingVisible(bool visible)
    {
        if (techRing is not null)
            techRing.Visible = visible;
    }

    Vector3 TechRingPosition()
    {
        if (BattleUnitCatalog.IsAirUnit(UnitKey))
            return new Vector3(0f, -1.54f, 0f);
        return new Vector3(0f, 0.055f, 0f);
    }

    Vector3 SelectionRingPosition()
    {
        if (BattleUnitCatalog.IsAirUnit(UnitKey))
            return new Vector3(0f, -1.55f, 0f);
        return new Vector3(0f, 0.04f, 0f);
    }

    float SelectionRingRadius()
    {
        if (BattleUnitCatalog.IsAirUnit(UnitKey))
            return 1.55f;
        if (BattleUnitCatalog.IsNavalUnit(UnitKey))
            return 1.45f;
        return 1.18f;
    }

    public override void _Process(double delta)
    {
        if (selectionRing is not null)
            selectionRing.Visible = Selected;

        if (MaxShield > 0f && Shield < MaxShield && !IsDead)
        {
            secondsSinceLastDamage += (float)delta;
            if (secondsSinceLastDamage >= 5.0f)
            {
                Shield = Mathf.Min(MaxShield, Shield + MaxShield * 0.1f * (float)delta);
            }
        }
    }

    /// <summary>
    /// 通过对 visualRoot 下的所有 GeometryInstance3D 子节点临时叠加材质，
    /// 模拟战争迷雾的颜色调制效果（3D 节点不支持 Modulate）。
    /// </summary>
    void SetVisualModulate(Color color)
    {
        var root = visualRoot ?? this as Node3D;
        if (root is null)
            return;
        SetNodeModulateRecursive(root, color);
    }

    static void SetNodeModulateRecursive(Node3D node, Color color)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is GeometryInstance3D geom)
            {
                if (color == Colors.White)
                {
                    // 恢复正常：移除覆盖材质
                    geom.MaterialOverlay = null;
                }
                else
                {
                    // 叠加半透明幽灵材质
                    if (geom.MaterialOverlay is not StandardMaterial3D overlay
                        || overlay.ResourceName != "_fog_ghost_overlay")
                    {
                        overlay = new StandardMaterial3D
                        {
                            ResourceName = "_fog_ghost_overlay",
                            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                            NoDepthTest = false
                        };
                        geom.MaterialOverlay = overlay;
                    }
                    overlay.AlbedoColor = color;
                }
            }
            if (child is Node3D child3d)
                SetNodeModulateRecursive(child3d, color);
        }
    }

    void EnsureNavalWaveParticles()
    {
        if (bowWaveParticles is null)
        {
            var splashMesh = new SphereMesh
            {
                Radius = 0.22f,
                Height = 0.44f,
                RadialSegments = 6,
                Rings = 3
            };

            var splashMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.9f, 0.95f, 1f, 0.72f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };

            var splashScale = new Curve();
            splashScale.AddPoint(new Vector2(0f, 0.5f));
            splashScale.AddPoint(new Vector2(1f, 1.5f));

            var splashColor = new Gradient();
            splashColor.AddPoint(0f, new Color(0.9f, 0.95f, 1f, 0.78f));
            splashColor.AddPoint(0.7f, new Color(0.85f, 0.92f, 0.96f, 0.35f));
            splashColor.AddPoint(1f, new Color(0.8f, 0.9f, 0.95f, 0f));

            var halfLength = 1.1f;
            if (UnitKey == "destroyer_ship")
                halfLength = 1.6f;
            else if (UnitKey == "transport_ship")
                halfLength = 1.9f;

            bowWaveParticles = new CpuParticles3D
            {
                Name = "BowWaveParticles",
                Amount = 14,
                Lifetime = 0.55f,
                OneShot = false,
                Direction = new Vector3(0f, 0.2f, 0.8f),
                Spread = 35f,
                Gravity = new Vector3(0f, -2.5f, 0f),
                InitialVelocityMin = 1.8f,
                InitialVelocityMax = 3.2f,
                ScaleAmountMin = 0.4f,
                ScaleAmountMax = 1.0f,
                Mesh = splashMesh,
                MaterialOverride = splashMat,
                ScaleAmountCurve = splashScale,
                ColorRamp = splashColor,
                Position = new Vector3(0f, 0.05f, -halfLength),
                Emitting = false
            };
            AddChild(bowWaveParticles);
        }

        if (sternWakeParticles is null)
        {
            var rippleMesh = new QuadMesh
            {
                Size = new Vector2(1f, 1f),
                Orientation = PlaneMesh.OrientationEnum.Y
            };

            var rippleMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.8f, 0.92f, 0.98f, 0.42f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };

            var rippleScale = new Curve();
            rippleScale.AddPoint(new Vector2(0f, 0.8f));
            rippleScale.AddPoint(new Vector2(1f, 3.4f));

            var rippleColor = new Gradient();
            rippleColor.AddPoint(0f, new Color(0.85f, 0.95f, 1f, 0.52f));
            rippleColor.AddPoint(0.5f, new Color(0.8f, 0.9f, 0.96f, 0.22f));
            rippleColor.AddPoint(1f, new Color(0.75f, 0.85f, 0.9f, 0f));

            var halfLength = 1.1f;
            if (UnitKey == "destroyer_ship")
                halfLength = 1.6f;
            else if (UnitKey == "transport_ship")
                halfLength = 1.9f;

            sternWakeParticles = new CpuParticles3D
            {
                Name = "SternWakeParticles",
                Amount = 10,
                Lifetime = 0.92f,
                OneShot = false,
                Direction = new Vector3(0f, 0f, 1f),
                Spread = 12f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 1.0f,
                InitialVelocityMax = 2.0f,
                ScaleAmountMin = 0.8f,
                ScaleAmountMax = 1.4f,
                Mesh = rippleMesh,
                MaterialOverride = rippleMat,
                ScaleAmountCurve = rippleScale,
                ColorRamp = rippleColor,
                Position = new Vector3(0f, 0.03f, halfLength),
                Emitting = false
            };
            AddChild(sternWakeParticles);
        }
    }

    void UpdateNavalWaves(bool moving, float delta)
    {
        EnsureNavalWaveParticles();
        if (bowWaveParticles is not null && bowWaveParticles.Emitting != moving)
        {
            bowWaveParticles.Emitting = moving;
        }
        if (sternWakeParticles is not null && sternWakeParticles.Emitting != moving)
        {
            sternWakeParticles.Emitting = moving;
        }
    }
}
