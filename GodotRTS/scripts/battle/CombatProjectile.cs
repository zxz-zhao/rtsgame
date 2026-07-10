using Godot;

public partial class CombatProjectile : Node3D
{
    Node3D? target;
    Node3D? attacker;
    Vector3 start;
    Vector3 lastTarget;
    Color tint;
    float damage;
    float elapsed;
    float duration = 0.25f;
    float arcHeight;
    float impactRadius = 0.65f;
    float splashRadius;
    float splashFalloff = 0.5f;
    bool groundAttack;
    bool projectilePlayerOwned;
    bool applied;

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range)
        => Spawn(owner, attacker, targetNode, damageAmount, playerOwned, range, 0f, 0.5f);

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        var source = attacker.GlobalPosition + Vector3.Up * 1.1f;
        var destination = targetNode.GlobalPosition + Vector3.Up * 0.9f;
        var color = playerOwned
            ? new Color(1f, 0.82f, 0.26f, 1f)
            : new Color(1f, 0.32f, 0.18f, 1f);
        if (attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower"))
        {
            color = new Color(1f, 0.45f, 0.05f, 1f);
        }
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, areaRadius, falloff, false, playerOwned);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    public static void SpawnGround(Node owner, Node3D attacker, Vector3 groundPoint, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        var source = attacker.GlobalPosition + Vector3.Up * 1.1f;
        var destination = new Vector3(groundPoint.X, 0.08f, groundPoint.Z);
        var color = playerOwned
            ? new Color(1f, 0.68f, 0.22f, 1f)
            : new Color(1f, 0.26f, 0.16f, 1f);
        if (attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower"))
        {
            color = new Color(1f, 0.45f, 0.05f, 1f);
        }
        projectile.Configure(source, attacker, null, destination, damageAmount, color, range, areaRadius, falloff, true, playerOwned);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    public static void SpawnImpactEffect(Node owner, Vector3 position, Color color, float radius = 0.8f)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var node = new Node3D
        {
            Name = "ImpactFlash",
            Scale = Vector3.One * 0.2f
        };

        var disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.06f,
                RadialSegments = 24
            },
            MaterialOverride = MakeMaterial(new Color(color.R, color.G, color.B, 0.42f), true)
        };
        node.AddChild(disc);
        root.AddChild(node);
        node.GlobalPosition = position + Vector3.Up * 0.08f;

        var tween = node.CreateTween();
        tween.TweenProperty(node, "scale", Vector3.One, 0.12);
        tween.TweenProperty(node, "scale", Vector3.One * 1.35f, 0.12);
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    void Configure(Vector3 source, Node3D sourceAttacker, Node3D? targetNode, Vector3 destination, float damageAmount, Color projectileTint, float range, float areaRadius, float falloff, bool isGroundAttack, bool sourcePlayerOwned)
    {
        start = source;
        target = targetNode;
        attacker = sourceAttacker;
        lastTarget = destination;
        damage = damageAmount;
        tint = projectileTint;
        splashRadius = Mathf.Max(0f, areaRadius);
        splashFalloff = Mathf.Clamp(falloff, 0f, 1f);
        groundAttack = isGroundAttack;
        projectilePlayerOwned = sourcePlayerOwned;
        arcHeight = range > 10f ? 5.5f : 0.8f;
        var areaFlashRadius = splashRadius > 0.05f ? Mathf.Clamp(splashRadius * 0.32f, 0.45f, 2.35f) : 0f;
        impactRadius = Mathf.Max(range > 10f ? 1.25f : 0.72f, areaFlashRadius);
        duration = Mathf.Clamp(source.DistanceTo(destination) / (range > 10f ? 34f : 48f), 0.10f, 0.9f);
    }

    public override void _Ready()
    {
        Name = "CombatProjectile";
        BattleFeedback.WeaponFire(this, start, ResolveWeaponAudioProfile(), splashRadius > 0.05f || arcHeight > 2.5f);
        
        var direction = (lastTarget - start).Normalized();
        SpawnMuzzleFlash(this, start, direction, tint);

        bool isFlame = attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower");
        var longRange = arcHeight > 2.5f;

        if (isFlame)
        {
            AddChild(new MeshInstance3D
            {
                Name = "Tracer",
                Mesh = new CapsuleMesh { Radius = 0.18f, Height = 0.95f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = MakeMaterial(tint, true)
            });
        }
        else
        {
            // 加载来自 Kenney TowerDefense 套件的写实穿甲炮弹模型，替代原有的纯颜色几何球体
            var shellVisual = TryLoadBulletMesh();
            if (shellVisual is not null)
            {
                AddChild(shellVisual);
            }
            else
            {
                AddChild(new MeshInstance3D
                {
                    Name = "Tracer",
                    Mesh = longRange
                        ? (Mesh)new CapsuleMesh { Radius = 0.11f, Height = 0.76f, RadialSegments = 8, Rings = 4 }
                        : (Mesh)new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 8, Rings = 4 },
                    MaterialOverride = MakeMaterial(tint, true)
                });
            }
        }

        AddChild(new OmniLight3D
        {
            Name = "Glow",
            LightColor = tint,
            LightEnergy = 0.35f,
            OmniRange = 4f
        });

        if (isFlame)
        {
            var trail = new CpuParticles3D
            {
                Name = "FlameTrail",
                Amount = 25,
                Lifetime = 0.22f,
                Spread = 20f,
                Gravity = new Vector3(0f, 0.4f, 0f),
                InitialVelocityMin = 0.4f,
                InitialVelocityMax = 1.2f,
                ScaleAmountMin = 0.18f,
                ScaleAmountMax = 0.50f
            };
            
            var flameSphere = new SphereMesh
            {
                Radius = 0.22f,
                Height = 0.44f,
                RadialSegments = 6,
                Rings = 4
            };
            trail.Mesh = flameSphere;

            var trailMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1.0f, 0.38f, 0.05f, 0.9f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            trail.MaterialOverride = trailMat;

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 1f));
            scaleCurve.AddPoint(new Vector2(1f, 0.2f));
            trail.ScaleAmountCurve = scaleCurve;

            var colorRamp = new Gradient();
            colorRamp.AddPoint(0f, new Color(1.0f, 0.58f, 0.08f, 0.95f));
            colorRamp.AddPoint(0.5f, new Color(0.98f, 0.22f, 0.04f, 0.65f));
            colorRamp.AddPoint(1.0f, new Color(0.24f, 0.08f, 0.02f, 0f));
            trail.ColorRamp = colorRamp;

            AddChild(trail);
            trail.Emitting = true;
        }
        else
        {
            // 为所有普通炮弹/弹丸添加白灰色飞行轨迹烟雾，增强空中弹道轨迹视觉效果
            var trail = new CpuParticles3D
            {
                Name = "SmokeTrail",
                Amount = 20,
                Lifetime = 0.35f,
                Spread = 10f,
                Gravity = new Vector3(0f, 0.15f, 0f),
                InitialVelocityMin = 0.1f,
                InitialVelocityMax = 0.5f,
                ScaleAmountMin = 0.08f,
                ScaleAmountMax = 0.32f
            };

            var smokeSphere = new SphereMesh
            {
                Radius = 0.25f,
                Height = 0.5f,
                RadialSegments = 6,
                Rings = 4
            };
            trail.Mesh = smokeSphere;

            var trailMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.85f, 0.85f, 0.85f, 0.45f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            trail.MaterialOverride = trailMat;

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.4f));
            scaleCurve.AddPoint(new Vector2(1f, 1.6f));
            trail.ScaleAmountCurve = scaleCurve;

            var colorRamp = new Gradient();
            colorRamp.AddPoint(0f, new Color(0.88f, 0.88f, 0.88f, 0.45f));
            colorRamp.AddPoint(0.6f, new Color(0.82f, 0.82f, 0.82f, 0.2f));
            colorRamp.AddPoint(1.0f, new Color(0.78f, 0.78f, 0.78f, 0f));
            trail.ColorRamp = colorRamp;

            AddChild(trail);
            trail.Emitting = true;
        }
    }

    public override void _Process(double delta)
    {
        if (applied || !IsInsideTree())
            return;

        elapsed += (float)delta;
        if (GodotObject.IsInstanceValid(target))
            lastTarget = target!.GlobalPosition + Vector3.Up * 0.9f;

        var t = Mathf.Clamp(elapsed / duration, 0f, 1f);
        var next = start.Lerp(lastTarget, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * arcHeight);
        var travel = next - GlobalPosition;
        GlobalPosition = next;
        if (travel.LengthSquared() > 0.0001f && IsInsideTree())
            LookAt(GlobalPosition + travel.Normalized(), Vector3.Up, true);

        if (t >= 1f)
            Impact();
    }

    void Impact()
    {
        applied = true;
        Node3D? directTarget = null;
        var damageAmount = Mathf.RoundToInt(damage);
        if (!groundAttack && GodotObject.IsInstanceValid(target) && target!.HasMethod("ApplyDamage"))
        {
            if (target is RtsUnit targetUnit)
            {
                var wasDeadBefore = targetUnit.IsDead || targetUnit.Health <= 0f;
                BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetUnit.PlayerOwned, damageAmount);
                targetUnit.ApplyDamage(damage);
                var isDeadNow = targetUnit.IsDead || targetUnit.Health <= 0f;
                if (!wasDeadBefore && isDeadNow)
                {
                    ProcessKillGoldLoot(targetUnit);
                }
            }
            else
            {
                if (target is RtsBuilding targetBuilding)
                    BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetBuilding.PlayerOwned, damageAmount);
                target.Call("ApplyDamage", damage);
            }
            directTarget = target;
        }

        if (splashRadius > 0.05f)
            ApplyAreaDamage(lastTarget, directTarget);

        SpawnExplosion(this, lastTarget, tint, splashRadius > 0.05f || arcHeight > 2.5f);
        SpawnImpactEffect(this, lastTarget, tint, impactRadius);
        BattleFeedback.Impact(this, lastTarget, splashRadius > 0.05f || arcHeight > 2.5f);
        QueueFree();
    }

    void ApplyAreaDamage(Vector3 center, Node3D? directTarget)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        foreach (var unit in manager.GetUnits(!projectilePlayerOwned))
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.IsDead || unit == attacker || unit == directTarget)
                continue;
            if (BattleUnitCatalog.IsAirUnit(unit.UnitKey))
                continue;

            var distance = GroundDistance(center, unit.GlobalPosition);
            if (distance > splashRadius)
                continue;

            var dealt = DamageAtDistance(distance);
            var wasDeadBefore = unit.IsDead || unit.Health <= 0f;
            BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, unit.PlayerOwned, Mathf.RoundToInt(dealt));
            unit.ApplyDamage(dealt);
            var isDeadNow = unit.IsDead || unit.Health <= 0f;
            if (!wasDeadBefore && isDeadNow)
            {
                ProcessKillGoldLoot(unit);
            }
        }

        foreach (var building in manager.GetBuildings(!projectilePlayerOwned))
        {
            if (!GodotObject.IsInstanceValid(building) || building.Health <= 0f || building == directTarget)
                continue;

            var distance = DistanceToBuildingGround(center, building);
            if (distance > splashRadius)
                continue;

            var dealt = DamageAtDistance(distance);
            BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, building.PlayerOwned, Mathf.RoundToInt(dealt));
            building.ApplyDamage(dealt);
        }
    }

    float DamageAtDistance(float distance)
    {
        var t = splashRadius <= 0.05f ? 0f : Mathf.Clamp(distance / splashRadius, 0f, 1f);
        return damage * Mathf.Lerp(1f, splashFalloff, t);
    }

    static float GroundDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float DistanceToBuildingGround(Vector3 point, RtsBuilding building)
    {
        if (building.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape is BoxShape3D box)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(point.X - building.GlobalPosition.X) - box.Size.X * 0.5f);
            var dz = Mathf.Max(0f, Mathf.Abs(point.Z - building.GlobalPosition.Z) - box.Size.Z * 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        return GroundDistance(point, building.GlobalPosition);
    }

    static StandardMaterial3D MakeMaterial(Color color, bool emissive)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.58f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = emissive,
            Emission = color,
            EmissionEnergyMultiplier = emissive ? 1.8f : 0f
        };
    }

    string ResolveWeaponAudioProfile()
    {
        if (attacker is RtsUnit unit)
        {
            if (unit.UnitKey == "fighter")
                return "machinegun";
            if (unit.UnitKey == "anti_air_gun")
                return "aa";
            if (unit.UnitKey == "bomber")
                return "bomb";
            if (BattleUnitCatalog.IsInfantryLike(unit.UnitKey))
            {
                if (unit.UnitKey is "infantry_flamethrower" or "flamethrower")
                    return "flame";
                if (unit.UnitKey == "infantry")
                    return "rifle";
                return "artillery";
            }

            if (BattleUnitCatalog.IsNavalUnit(unit.UnitKey))
                return "naval";
            if (BattleUnitCatalog.IsArtilleryLike(unit.UnitKey))
                return "artillery";
        }

        if (attacker is RtsBuilding building && building.IsDefenseTurret)
            return "artillery";

        return splashRadius > 0.05f || arcHeight > 2.5f ? "artillery" : "cannon";
    }

    void ProcessKillGoldLoot(RtsUnit deadUnit)
    {
        var starterKey = projectilePlayerOwned 
            ? (GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank")
            : "tank";
            
        var faction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(starterKey);
        if (faction.Key == "resistance_army")
        {
            var def = BattleUnitCatalog.Get(deadUnit.UnitKey);
            var lootAmount = Mathf.RoundToInt(def.GoldCost * 0.20f);
            if (lootAmount > 0)
            {
                if (BattleGameManager.Instance is { } manager)
                {
                    manager.AddGold(projectilePlayerOwned, lootAmount);
                    if (projectilePlayerOwned)
                    {
                        BattleFeedback.Loot(deadUnit, deadUnit.GlobalPosition, lootAmount);
                    }
                }
            }
        }
    }

    static void SpawnMuzzleFlash(Node owner, Vector3 position, Vector3 direction, Color color)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        
        var container = new Node3D { Name = "MuzzleFlashEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        var normalizedDir = direction.Normalized();

        var fireParticles = new CpuParticles3D
        {
            Name = "MuzzleFire",
            Amount = 10,
            Lifetime = 0.16f,
            OneShot = true,
            Explosiveness = 0.95f,
            Direction = normalizedDir,
            Spread = 30f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 4f,
            InitialVelocityMax = 7f,
            ScaleAmountMin = 0.15f,
            ScaleAmountMax = 0.4f
        };

        var sphere = new SphereMesh
        {
            Radius = 0.2f,
            Height = 0.4f,
            RadialSegments = 6,
            Rings = 4
        };
        fireParticles.Mesh = sphere;

        var fireMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        fireParticles.MaterialOverride = fireMat;

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 1f));
        scaleCurve.AddPoint(new Vector2(1f, 0f));
        fireParticles.ScaleAmountCurve = scaleCurve;

        var fireRamp = new Gradient();
        fireRamp.AddPoint(0f, new Color(color.R, color.G, color.B, 1f));
        fireRamp.AddPoint(1f, new Color(color.R * 0.4f, color.G * 0.2f, color.B * 0.05f, 0f));
        fireParticles.ColorRamp = fireRamp;

        container.AddChild(fireParticles);
        fireParticles.Emitting = true;

        // 创建写实的大团开火排烟特效，增加炮口排烟的体积感和消散动画
        var smokeParticles = new CpuParticles3D
        {
            Name = "MuzzleSmoke",
            Amount = 16,
            Lifetime = 0.95f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = normalizedDir + Vector3.Up * 0.35f,
            Spread = 40f,
            Gravity = new Vector3(0f, 0.8f, 0f),
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3.5f,
            ScaleAmountMin = 0.38f,
            ScaleAmountMax = 1.35f
        };
        smokeParticles.Mesh = sphere;

        var smokeMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.85f, 0.85f, 0.85f, 0.68f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        smokeParticles.MaterialOverride = smokeMat;

        var smokeScaleCurve = new Curve();
        smokeScaleCurve.AddPoint(new Vector2(0f, 0.5f));
        smokeScaleCurve.AddPoint(new Vector2(1f, 2.2f));
        smokeParticles.ScaleAmountCurve = smokeScaleCurve;

        var smokeRamp = new Gradient();
        smokeRamp.AddPoint(0f, new Color(0.88f, 0.88f, 0.88f, 0.65f));
        smokeRamp.AddPoint(0.5f, new Color(0.82f, 0.82f, 0.82f, 0.35f));
        smokeRamp.AddPoint(1f, new Color(0.78f, 0.78f, 0.78f, 0f));
        smokeParticles.ColorRamp = smokeRamp;

        container.AddChild(smokeParticles);
        smokeParticles.Emitting = true;

        var timer = container.CreateTween();
        timer.TweenInterval(1.2);
        timer.TweenCallback(Callable.From(container.QueueFree));
    }

    static void SpawnExplosion(Node owner, Vector3 position, Color color, bool heavy)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        
        var container = new Node3D { Name = "ExplosionEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        var fireParticles = new CpuParticles3D
        {
            Name = "FireParticles",
            Amount = heavy ? 24 : 12,
            Lifetime = 0.45f,
            OneShot = true,
            Explosiveness = 0.85f,
            Direction = Vector3.Up,
            Spread = 60f,
            Gravity = new Vector3(0f, 1.8f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.18f,
            ScaleAmountMax = 0.55f
        };

        var sphere = new SphereMesh
        {
            Radius = 0.25f,
            Height = 0.5f,
            RadialSegments = 6,
            Rings = 4
        };
        fireParticles.Mesh = sphere;

        var fireMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        fireParticles.MaterialOverride = fireMat;

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 1f));
        scaleCurve.AddPoint(new Vector2(1f, 0f));
        fireParticles.ScaleAmountCurve = scaleCurve;
        
        var colorRamp = new Gradient();
        colorRamp.AddPoint(0f, new Color(color.R, color.G, color.B, 1f));
        colorRamp.AddPoint(0.4f, new Color(color.R * 0.8f, color.G * 0.35f, color.B * 0.1f, 0.8f));
        colorRamp.AddPoint(1f, new Color(0.18f, 0.18f, 0.18f, 0f));
        fireParticles.ColorRamp = colorRamp;

        container.AddChild(fireParticles);
        fireParticles.Emitting = true;

        var smokeParticles = new CpuParticles3D
        {
            Name = "SmokeParticles",
            Amount = heavy ? 20 : 10,
            Lifetime = 0.75f,
            OneShot = true,
            Explosiveness = 0.9f,
            Direction = Vector3.Up,
            Spread = 45f,
            Gravity = new Vector3(0f, 1.2f, 0f),
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 3.0f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.72f
        };
        smokeParticles.Mesh = sphere;

        var smokeMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.22f, 0.22f, 0.22f, 0.65f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        smokeParticles.MaterialOverride = smokeMat;
        smokeParticles.ScaleAmountCurve = scaleCurve;

        var smokeColorRamp = new Gradient();
        smokeColorRamp.AddPoint(0f, new Color(0.28f, 0.28f, 0.28f, 0.6f));
        smokeColorRamp.AddPoint(1f, new Color(0.12f, 0.12f, 0.12f, 0f));
        smokeParticles.ColorRamp = smokeColorRamp;

        container.AddChild(smokeParticles);
        smokeParticles.Emitting = true;

        var sparkParticles = new CpuParticles3D
        {
            Name = "SparkParticles",
            Amount = heavy ? 16 : 8,
            Lifetime = 0.35f,
            OneShot = true,
            Explosiveness = 0.95f,
            Direction = Vector3.Up,
            Spread = 80f,
            Gravity = new Vector3(0f, -9.8f, 0f),
            InitialVelocityMin = 3.5f,
            InitialVelocityMax = 7.0f,
            ScaleAmountMin = 0.04f,
            ScaleAmountMax = 0.12f
        };
        sparkParticles.Mesh = sphere;

        var sparkMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.88f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        sparkParticles.MaterialOverride = sparkMat;
        sparkParticles.ScaleAmountCurve = scaleCurve;

        container.AddChild(sparkParticles);
        sparkParticles.Emitting = true;

        var timer = container.CreateTween();
        timer.TweenInterval(0.9);
        timer.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 加载高品质穿甲炮弹 3D 模型 (.fbx)，并旋转对齐飞行弹道，替换基础球体
    /// </summary>
    Node3D? TryLoadBulletMesh()
    {
        const string path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-bullet.fbx";
        try
        {
            if (ResourceLoader.Exists(path))
            {
                var scene = ResourceLoader.Load<PackedScene>(path);
                if (scene is not null)
                {
                    var wrapper = new Node3D { Name = "ShellWrapper" };
                    var instance = scene.Instantiate<Node3D>();
                    
                    // Kenney的weapon-ammo-bullet模型在FBX中是立着的(Y轴向上)
                    // 为了让其贴合Godot的LookAt朝向（-Z为向前），需要将其绕X轴旋转-90度
                    instance.Rotation = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
                    
                    // 将模型尺寸缩放到适当的大小
                    instance.Scale = Vector3.One * 0.75f;
                    
                    wrapper.AddChild(instance);
                    
                    // 遍历模型子孙，为其添加带自发光的暗钢拟真金属材质，让其看起来像一枚高速行进的炽热穿甲弹
                    SetShellMaterialModulateRecursive(instance, tint);
                    
                    return wrapper;
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[CombatProjectile] Failed to load custom bullet mesh: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 递归为炮弹网格覆盖高品质拟真暗金属发光材质，赋予破空时尾迹的炽热流光感
    /// </summary>
    void SetShellMaterialModulateRecursive(Node node, Color emissionColor)
    {
        if (node is MeshInstance3D geom)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.18f, 0.18f, 0.18f),
                Roughness = 0.22f,
                Metallic = 0.85f,
                EmissionEnabled = true,
                Emission = emissionColor.Lightened(0.12f),
                EmissionEnergyMultiplier = 1.6f
            };
            geom.MaterialOverride = mat;
        }

        foreach (var child in node.GetChildren())
        {
            SetShellMaterialModulateRecursive(child, emissionColor);
        }
    }
}
