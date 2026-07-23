using Godot;

public partial class CombatProjectile : Node3D
{
    static Texture2D? radialGradientTex;
    static readonly QuadMesh particleQuad = new QuadMesh { Size = Vector2.One };

    static Texture2D GetRadialGradientTexture()
    {
        if (radialGradientTex is not null)
            return radialGradientTex;
        var grad = new Gradient();
        grad.SetColor(0, Colors.White);
        grad.SetColor(1, new Color(1f, 1f, 1f, 0f));
        radialGradientTex = new GradientTexture2D
        {
            Gradient = grad,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 1.0f),
            Width = 64,
            Height = 64
        };
        return radialGradientTex;
    }

    static StandardMaterial3D CreateParticleMaterial(Color baseColor, bool unshaded)
    {
        return new StandardMaterial3D
        {
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            AlbedoColor = baseColor,
            AlbedoTexture = GetRadialGradientTexture(),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f
        };
    }


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
        bool isFlame = attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower");
        if (isFlame)
        {
            color = new Color(1.2f, 0.32f, 0.05f, 1f); // 强烈的发光暖红色温
            
            // 发射 3 股扇形散射的火焰流（中间、偏左 12 度、偏右 12 度）
            // 左右两侧辅助火流造成 0 伤害，避免产生伤害重叠 Bug
            projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, areaRadius, falloff, false, playerOwned);
            root.AddChild(projectile);
            projectile.GlobalPosition = source;

            var dir = (destination - source).Normalized();
            
            var leftDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-12f));
            var leftDest = source + leftDir * source.DistanceTo(destination);
            var leftProj = new CombatProjectile();
            leftProj.Configure(source, attacker, null, leftDest, 0f, color, range, 0f, 0f, true, playerOwned);
            root.AddChild(leftProj);
            leftProj.GlobalPosition = source;

            var rightDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(12f));
            var rightDest = source + rightDir * source.DistanceTo(destination);
            var rightProj = new CombatProjectile();
            rightProj.Configure(source, attacker, null, rightDest, 0f, color, range, 0f, 0f, true, playerOwned);
            root.AddChild(rightProj);
            rightProj.GlobalPosition = source;
        }
        else
        {
            projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, areaRadius, falloff, false, playerOwned);
            root.AddChild(projectile);
            projectile.GlobalPosition = source;
        }
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
        bool isFlame = attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower");
        if (isFlame)
        {
            color = new Color(1.2f, 0.32f, 0.05f, 1f); // 强烈的发光暖红色温
            
            // 发射 3 股扇形散射的火焰流（中间、偏左 12 度、偏右 12 度）
            // 左右两侧辅助火流造成 0 伤害，避免产生伤害重叠 Bug
            projectile.Configure(source, attacker, null, destination, damageAmount, color, range, areaRadius, falloff, true, playerOwned);
            root.AddChild(projectile);
            projectile.GlobalPosition = source;

            var dir = (destination - source).Normalized();
            
            var leftDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-12f));
            var leftDest = source + leftDir * source.DistanceTo(destination);
            var leftProj = new CombatProjectile();
            leftProj.Configure(source, attacker, null, leftDest, 0f, color, range, 0f, 0f, true, playerOwned);
            root.AddChild(leftProj);
            leftProj.GlobalPosition = source;

            var rightDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(12f));
            var rightDest = source + rightDir * source.DistanceTo(destination);
            var rightProj = new CombatProjectile();
            rightProj.Configure(source, attacker, null, rightDest, 0f, color, range, 0f, 0f, true, playerOwned);
            root.AddChild(rightProj);
            rightProj.GlobalPosition = source;
        }
        else
        {
            projectile.Configure(source, attacker, null, destination, damageAmount, color, range, areaRadius, falloff, true, playerOwned);
            root.AddChild(projectile);
            projectile.GlobalPosition = source;
        }
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
        
        bool isFlame = attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower");
        var direction = (lastTarget - start).Normalized();
        SpawnMuzzleFlash(this, start, direction, tint, isFlame);
        var longRange = arcHeight > 2.5f;

        if (isFlame)
        {
            // 喷火兵发射火焰：不使用硬质的 CapsuleMesh Tracer，完全通过世界坐标的粒子轨迹呈现逼真喷火流
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
                Amount = 80, // 增加粒子数量以形成连续的喷火柱
                Lifetime = 0.45f, // 延长生存时间使火舌延伸更长
                Spread = 12f, // 适当散布，使火焰形状自然
                Direction = new Vector3(0f, 0f, -1f), // 沿枪口朝向正前方喷射
                Gravity = new Vector3(0f, 0.4f, 0f), // 略微的热空气上升
                InitialVelocityMin = 5.0f, // 提高初始射速以体现强烈的喷射动力
                InitialVelocityMax = 10.0f,
                ScaleAmountMin = 0.25f,
                ScaleAmountMax = 0.75f,
                LocalCoords = false // 使用世界坐标，使喷射出的火焰在行进轨迹上自然散布
            };
            
            trail.Mesh = particleQuad;
            trail.MaterialOverride = CreateParticleMaterial(new Color(1.0f, 1.0f, 1.0f, 1.0f), true);

            // 火焰膨胀消散曲线：喷射出时较窄，在空中迅速受热膨胀，随后快速燃尽消散
            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.3f));
            scaleCurve.AddPoint(new Vector2(0.4f, 1.8f));
            scaleCurve.AddPoint(new Vector2(1f, 0.2f));
            trail.ScaleAmountCurve = scaleCurve;

            // 强烈的高温喷火色温渐变：明亮金黄-橙 -> 炽热红 -> 迅速燃尽微弱红灰烟雾
            var colorRamp = new Gradient();
            colorRamp.AddPoint(0f, new Color(1.8f, 0.6f, 0.1f, 1f)); // 炽热亮黄/橙 (发光)
            colorRamp.AddPoint(0.2f, new Color(1.5f, 0.32f, 0.05f, 0.95f)); // 橙红火光
            colorRamp.AddPoint(0.5f, new Color(1.1f, 0.15f, 0.02f, 0.8f)); // 深红燃烧
            colorRamp.AddPoint(0.8f, new Color(0.6f, 0.05f, 0.01f, 0.4f)); // 逐渐熄灭的暗红
            colorRamp.AddPoint(1.0f, new Color(0.1f, 0.1f, 0.1f, 0f)); // 燃尽淡化
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
                Amount = 60,
                Lifetime = 0.45f,
                Spread = 15f,
                Gravity = new Vector3(0f, 0.2f, 0f),
                InitialVelocityMin = 0.1f,
                InitialVelocityMax = 0.6f,
                ScaleAmountMin = 0.1f,
                ScaleAmountMax = 0.45f,
                LocalCoords = false
            };

            trail.Mesh = particleQuad;
            trail.MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.85f, 0.55f), false);

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

        // 解除火焰拖尾粒子节点的父子绑定，以防随着子弹销毁 (QueueFree) 导致空中的粒子瞬间凭空消失
        if (GetNodeOrNull<CpuParticles3D>("FlameTrail") is { } trail)
        {
            RemoveChild(trail);
            GetTree().CurrentScene.AddChild(trail);
            trail.Emitting = false;
            var t = trail.CreateTween();
            t.TweenInterval(trail.Lifetime);
            t.TweenCallback(Callable.From(trail.QueueFree));
        }

        Node3D? directTarget = null;
        var damageAmount = Mathf.RoundToInt(damage);
        if (!groundAttack && GodotObject.IsInstanceValid(target) && target!.HasMethod("ApplyDamage"))
        {
            if (target is RtsUnit targetUnit)
            {
                var wasDeadBefore = targetUnit.IsDead || targetUnit.Health <= 0f;
                string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : "";
                float multiplier = string.IsNullOrEmpty(attackerKey) ? 1.0f : BattleUnitCatalog.GetDamageMultiplier(attackerKey, targetUnit.UnitKey);
                float finalDamage = damage * multiplier;
                damageAmount = Mathf.RoundToInt(finalDamage);

                BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetUnit.PlayerOwned, damageAmount);
                targetUnit.ApplyDamage(finalDamage);
                var isDeadNow = targetUnit.IsDead || targetUnit.Health <= 0f;
                if (!wasDeadBefore && isDeadNow)
                {
                    ProcessKillGoldLoot(targetUnit);
                }
            }
            else
            {
                if (target is RtsBuilding targetBuilding)
                {
                    string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : "";
                    float multiplier = string.IsNullOrEmpty(attackerKey) ? 1.0f : BattleUnitCatalog.GetDamageMultiplierToBuilding(attackerKey);
                    float finalDamage = damage * multiplier;
                    damageAmount = Mathf.RoundToInt(finalDamage);

                    BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetBuilding.PlayerOwned, damageAmount);
                    target.Call("ApplyDamage", finalDamage);
                }
                else
                {
                    target.Call("ApplyDamage", damage);
                }
            }
            directTarget = target;
        }

        if (splashRadius > 0.05f)
            ApplyAreaDamage(lastTarget, directTarget);

        bool isNuke = false;
        if (GodotObject.IsInstanceValid(attacker))
        {
            if (attacker is RtsUnit attUnit)
            {
                isNuke = attUnit.UnitKey == "nuke" || attUnit.UnitKey == "nuclear_missile";
            }
            else if (attacker is RtsBuilding attBuilding)
            {
                isNuke = attBuilding.BuildKey == "nuke_silo" || attBuilding.BuildKey == "nuclear_silo" || attBuilding.BuildKey == "nuke" || attBuilding.BuildKey == "nuclear_silo_building";
            }
        }

        if (isNuke)
        {
            float shakePower = Mathf.Clamp(splashRadius * 0.18f, 0.22f, 0.95f);
            float shakeDuration = Mathf.Clamp(splashRadius * 0.12f, 0.25f, 0.65f);
            BattleGameManager.Instance?.TriggerCameraShake(shakePower, shakeDuration);
        }

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
            string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : "";
            float multiplier = string.IsNullOrEmpty(attackerKey) ? 1.0f : BattleUnitCatalog.GetDamageMultiplier(attackerKey, unit.UnitKey);
            dealt *= multiplier;

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
            string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : "";
            float multiplier = string.IsNullOrEmpty(attackerKey) ? 1.0f : BattleUnitCatalog.GetDamageMultiplierToBuilding(attackerKey);
            dealt *= multiplier;

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

    static void SpawnMuzzleFlash(Node owner, Vector3 position, Vector3 direction, Color color, bool isFlame)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var container = new Node3D { Name = "MuzzleFlashEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;
        var normalizedDir = direction.Normalized();
        var particleQuad = new QuadMesh { Size = Vector2.One };

        if (isFlame)
        {
            var fireParticles = new CpuParticles3D
            {
                Name = "MuzzleFire",
                Amount = 25,
                Lifetime = 0.25f,
                OneShot = true,
                Explosiveness = 0.88f,
                Direction = normalizedDir,
                Spread = 20f,
                Gravity = new Vector3(0f, 0.8f, 0f),
                InitialVelocityMin = 3.5f,
                InitialVelocityMax = 5.5f,
                ScaleAmountMin = 0.18f,
                ScaleAmountMax = 0.6f
            };

            fireParticles.Mesh = particleQuad;
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.5f));
            scaleCurve.AddPoint(new Vector2(0.4f, 1.6f));
            scaleCurve.AddPoint(new Vector2(1f, 0.1f));
            fireParticles.ScaleAmountCurve = scaleCurve;

            var fireRamp = new Gradient();
            fireRamp.AddPoint(0f, new Color(1.2f, 0.35f, 0.05f, 1f)); // Muzzle flash matches warm red-orange
            fireRamp.AddPoint(0.5f, new Color(0.98f, 0.18f, 0.02f, 0.8f));
            fireRamp.AddPoint(1.0f, new Color(0.75f, 0.05f, 0.01f, 0f));
            fireParticles.ColorRamp = fireRamp;

            container.AddChild(fireParticles);
            fireParticles.Emitting = true;
        }
        else
        {
            var fireParticles = new CpuParticles3D
            {
                Name = "MuzzleFire",
                Amount = 15,
                Lifetime = 0.16f,
                OneShot = true,
                Explosiveness = 0.95f,
                Direction = normalizedDir,
                Spread = 30f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 4f,
                InitialVelocityMax = 7f,
                ScaleAmountMin = 0.15f,
                ScaleAmountMax = 0.45f
            };

            fireParticles.Mesh = particleQuad;
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

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

            var smokeParticles = new CpuParticles3D
            {
                Name = "MuzzleSmoke",
                Amount = 25,
                Lifetime = 0.95f,
                OneShot = true,
                Explosiveness = 0.92f,
                Direction = normalizedDir + Vector3.Up * 0.35f,
                Spread = 40f,
                Gravity = new Vector3(0f, 0.8f, 0f),
                InitialVelocityMin = 1.5f,
                InitialVelocityMax = 3.5f,
                ScaleAmountMin = 0.38f,
                ScaleAmountMax = 1.5f
            };
            smokeParticles.Mesh = particleQuad;
            smokeParticles.MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.85f, 0.68f), false);

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
        }

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
        var particleQuad = new QuadMesh { Size = Vector2.One };

        var fireParticles = new CpuParticles3D
        {
            Name = "FireParticles",
            Amount = heavy ? 35 : 20,
            Lifetime = 0.45f,
            OneShot = true,
            Explosiveness = 0.85f,
            Direction = Vector3.Up,
            Spread = 60f,
            Gravity = new Vector3(0f, 1.8f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.2f,
            ScaleAmountMax = 0.7f
        };

        fireParticles.Mesh = particleQuad;
        fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

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
            Amount = heavy ? 30 : 15,
            Lifetime = 0.85f,
            OneShot = true,
            Explosiveness = 0.9f,
            Direction = Vector3.Up,
            Spread = 45f,
            Gravity = new Vector3(0f, 1.5f, 0f),
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3.5f,
            ScaleAmountMin = 0.3f,
            ScaleAmountMax = 1.1f
        };
        smokeParticles.Mesh = particleQuad;
        smokeParticles.MaterialOverride = CreateParticleMaterial(Colors.White, false);
        smokeParticles.ScaleAmountCurve = scaleCurve;

        var smokeColorRamp = new Gradient();
        smokeColorRamp.AddPoint(0f, new Color(0.28f, 0.28f, 0.28f, 0.6f));
        smokeColorRamp.AddPoint(1f, new Color(0.12f, 0.12f, 0.12f, 0f));
        smokeParticles.ColorRamp = smokeColorRamp;

        container.AddChild(smokeParticles);
        smokeParticles.Emitting = true;

        var sphere = new SphereMesh
        {
            Radius = 0.25f,
            Height = 0.5f,
            RadialSegments = 6,
            Rings = 4
        };

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
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(1f, 1f, 1f, 1f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        sparkParticles.MaterialOverride = sparkMat;
        sparkParticles.ScaleAmountCurve = scaleCurve;

        var sparkColorRamp = new Gradient();
        sparkColorRamp.AddPoint(0f, new Color(1.0f, 0.9f, 0.6f, 1f));
        sparkColorRamp.AddPoint(0.5f, new Color(0.95f, 0.45f, 0.1f, 0.8f));
        sparkColorRamp.AddPoint(1.0f, new Color(0.85f, 0.08f, 0.02f, 0f));
        sparkParticles.ColorRamp = sparkColorRamp;

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
