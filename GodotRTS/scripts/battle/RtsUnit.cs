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
    string walkAnimationName = "";
    string idleAnimationName = "";
    string currentVisualAnimationName = "";
    readonly List<AnimatedVisualPart> infantryVisualParts = new();
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

        if (amount > 0f && techDamageReduction > 0f)
            amount *= Mathf.Clamp(1f - techDamageReduction, 0.25f, 1f);

        var before = Health;
        Health = Mathf.Max(0f, Health - amount);
        BattleFeedback.Damage(this, GlobalPosition, before - Health, PlayerOwned, Health <= 0f);
        if (Health <= 0f)
        {
            EmitSignal(SignalName.Died, this);
            QueueFree();
        }
    }

    public float Repair(float amount)
    {
        if (IsDead || amount <= 0f)
            return 0f;

        var before = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        var repaired = Health - before;
        BattleFeedback.Repair(this, GlobalPosition, repaired);
        return repaired;
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
        visualRoot = root;
        weaponYawNode = null;
        weaponPitchNode = null;
        weaponPitchIsPivot = false;
        weaponYawLookOffset = Vector3.Zero;
        weaponPitchLookOffset = Vector3.Zero;
        weaponPitchPivotOffset = Vector3.Zero;
        visualAnimationPlayer = null;
        walkAnimationName = "";
        idleAnimationName = "";
        currentVisualAnimationName = "";
        infantryVisualParts.Clear();
        propellerNodes.Clear();

        if (root is null)
            return;

        visualAnimationPlayer = root.FindChildren("*", "AnimationPlayer", true, false)
            .OfType<AnimationPlayer>()
            .FirstOrDefault();
        ResolveVisualAnimationNames();

        switch (unitKey)
        {
            case "heavy_tank":
                weaponYawNode = root.FindChild("misc_a", true, false) as Node3D
                    ?? root.FindChild("mount2", true, false) as Node3D
                    ?? root;
                weaponPitchNode = root.FindChild("weapon", true, false) as Node3D
                    ?? root.FindChild("mount2", true, false) as Node3D
                    ?? root.FindChild("misc_b", true, false) as Node3D
                    ?? weaponYawNode;
                weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
                break;
            case "light_tank":
                weaponYawNode = root.FindChild("base", true, false) as Node3D ?? root;
                weaponPitchNode = root.FindChild("gun_elevate", true, false) as Node3D
                    ?? root.FindChild("coax", true, false) as Node3D
                    ?? weaponYawNode;
                break;
            case "tank":
            case "medium_tank":
                weaponYawNode = root.FindChild("turret_exterior", true, false) as Node3D
                    ?? root.FindChild("mantlet_inner", true, false) as Node3D
                    ?? root;
                weaponPitchNode = root.FindChild("barrel", true, false) as Node3D
                    ?? root.FindChild("gun", true, false) as Node3D
                    ?? weaponYawNode;
                weaponPitchIsPivot = false;
                PreparePanzerTurretRig(root);
                weaponYawNode = root.GetNodeOrNull<Node3D>("TurretPivot") ?? weaponYawNode;
                weaponPitchNode = root.GetNodeOrNull<Node3D>("TurretPivot/GunPivot")
                    ?? root.GetNodeOrNull<Node3D>("GunPivot")
                    ?? weaponPitchNode;
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
        FogRevealed = PlayerOwned || revealed;
        Visible = FogRevealed;
        CollisionLayer = FogRevealed ? originalCollisionLayer : 0;
        CollisionMask = FogRevealed ? originalCollisionMask : 0;
        if (!FogRevealed)
            SetSelected(false);
    }

    void ProcessMove(float delta)
    {
        var flatDelta = TargetPosition - GlobalPosition;
        flatDelta.Y = 0f;
        if (flatDelta.Length() <= 0.15f)
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

        if (cooldownLeft <= 0f)
        {
            cooldownLeft = AttackCooldown;
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

        if (cooldownLeft <= 0f)
        {
            cooldownLeft = AttackCooldown;
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
        var score = 1000f - Mathf.Sqrt(distanceSquared) * 4f;
        if (target is RtsUnit unit)
        {
            var targetIsAir = BattleUnitCatalog.IsAirUnit(unit.UnitKey);
            var targetIsInfantry = BattleUnitCatalog.IsInfantryLike(unit.UnitKey);
            var targetIsNaval = BattleUnitCatalog.IsNavalUnit(unit.UnitKey);

            if (targetIsAir)
                score += UnitKey is "anti_air_gun" or "fighter" ? 520f : -180f;
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
            score += 120f;
            if (UnitKey is "artillery" or "bomber" or "destroyer_ship" or "transport_ship")
                score += 430f;
            if (UnitKey is "anti_air_gun" or "fighter")
                score -= 260f;
            if (building.IsDefenseTurret)
                score += 180f;
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

    void FaceDirection(Vector3 direction, float delta)
    {
        direction.Y = 0f;
        if (direction.LengthSquared() <= 0.0001f)
            return;

        var targetYaw = Mathf.Atan2(direction.X, direction.Z);
        var currentYaw = Rotation.Y;
        var turnStep = Mathf.Clamp(TurnSpeed * delta, 0f, 1f);
        Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(currentYaw, targetYaw, turnStep), Rotation.Z);
    }

    void UpdateWeaponAim(Vector3 target, bool groundAttack, float delta)
    {
        if (weaponYawNode is null && weaponPitchNode is null)
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

    static void ApplySmoothLookAt(Node3D node, Vector3 target, Vector3 offset, float delta, float speed)
    {
        var previous = node.GlobalTransform;
        node.LookAt(target, Vector3.Up, true);
        var desiredRotation = node.Rotation + offset;
        node.GlobalTransform = previous;
        node.Rotation = SmoothEuler(node.Rotation, desiredRotation, delta, speed);
    }

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
        if (visualAnimationPlayer is null)
            return;

        foreach (var animation in visualAnimationPlayer.GetAnimationList())
        {
            var name = animation.ToString();
            var lower = name.ToLowerInvariant();
            if (string.IsNullOrEmpty(walkAnimationName)
                && (lower.Contains("walk") || lower.Contains("run") || lower.Contains("move")))
                walkAnimationName = name;
            if (string.IsNullOrEmpty(idleAnimationName) && lower.Contains("idle"))
                idleAnimationName = name;
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
        var moving = Velocity.LengthSquared() > 0.08f || bombingRunActive || parkingAtAirfield;
        UpdateAnimationPlayback(moving);
        UpdateInfantryStride(moving);
        UpdatePropellers(delta, moving);
    }

    void UpdateAnimationPlayback(bool moving)
    {
        if (visualAnimationPlayer is null)
            return;

        var next = moving && !string.IsNullOrEmpty(walkAnimationName)
            ? walkAnimationName
            : idleAnimationName;
        if (string.IsNullOrEmpty(next) || next == currentVisualAnimationName)
            return;

        visualAnimationPlayer.Play(next);
        currentVisualAnimationName = next;
    }

    void UpdateInfantryStride(bool moving)
    {
        if (infantryVisualParts.Count == 0)
            return;

        var stride = moving ? visualMotionTime * 10.5f : 0f;
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
                    var bob = moving ? Mathf.Abs(wave) * 0.070f : 0f;
                    var forwardStep = moving ? Mathf.Cos(stride + part.Phase) * 0.035f : 0f;
                    var sideSway = moving ? Mathf.Sin(stride * 0.5f + part.Phase) * 0.018f : 0f;
                    var lean = moving ? wave * 0.045f : 0f;
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

        part.Node.Position = part.RestPosition + new Vector3(0f, Mathf.Abs(wave) * 0.018f, wave * 0.050f);
        part.Node.Rotation = part.RestRotation + new Vector3(wave * swing, 0f, wave * swing * 0.18f);
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
            ?? root.FindChild("mantlet_inner", true, false) as Node3D
            ?? root.FindChild("gun", true, false) as Node3D;
        var gunAnchor = root.FindChild("barrel", true, false) as Node3D
            ?? root.FindChild("gun", true, false) as Node3D;
        if (turretAnchor is null || gunAnchor is null)
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
            Position = gunAnchor.Position - turretAnchor.Position
        };
        turretPivot.AddChild(gunPivot);
        if (gunAnchor.GetParent() is Node gunParent)
            gunParent.RemoveChild(gunAnchor);
        gunPivot.AddChild(gunAnchor);
        gunAnchor.Position = Vector3.Zero;

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
        return lower.StartsWith("turret")
            || lower.StartsWith("barrel")
            || lower == "gun"
            || lower.StartsWith("gun.")
            || lower.StartsWith("gun_")
            || lower.StartsWith("mantlet")
            || lower.StartsWith("cupola")
            || lower.StartsWith("cmdr_")
            || lower.StartsWith("lever_")
            || lower.StartsWith("loader_")
            || lower.StartsWith("gunner_");
    }

    static bool BelongsToPanzerGun(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.StartsWith("barrel")
            || lower == "gun"
            || lower.StartsWith("gun.")
            || lower.StartsWith("gun_")
            || lower.StartsWith("mantlet");
    }

    bool IsLiveEnemyTarget(Node3D target)
        => target switch
        {
            RtsUnit unit => unit.PlayerOwned != PlayerOwned && !unit.IsDead && unit.FogRevealed,
            RtsBuilding building => building.PlayerOwned != PlayerOwned && building.Health > 0f && building.FogRevealed,
            _ => GodotObject.IsInstanceValid(target)
        };

    void EnsureCombatOverlays()
    {
        if (GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar") is null)
        {
            var isAir = BattleUnitCatalog.IsAirUnit(UnitKey);
            var bar = new WorldHealthBar3D
            {
                Name = "WorldHealthBar",
                Width = isAir ? 2.8f : 2.2f,
                HeightOffset = isAir ? 2.95f : 2.55f,
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
            worldHealthBar.Position = new Vector3(0f, BattleUnitCatalog.IsAirUnit(UnitKey) ? 2.95f : 2.55f, 0f);

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
                Repair(repair);
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
    }
}
