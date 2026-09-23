using Godot;

public partial class CombatProjectile : Node3D
{
    static Texture2D? radialGradientTex;
    static Texture2D? shockwaveRingTex;
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

    static Texture2D GetShockwaveRingTexture()
    {
        if (shockwaveRingTex is not null)
            return shockwaveRingTex;
        var grad = new Gradient();
        grad.AddPoint(0.0f, new Color(1f, 1f, 1f, 0f));
        grad.AddPoint(0.55f, new Color(1f, 1f, 1f, 0.05f));
        grad.AddPoint(0.80f, new Color(1f, 1f, 1f, 0.95f));
        grad.AddPoint(0.95f, new Color(1f, 1f, 1f, 1f));
        grad.AddPoint(1.0f, new Color(1f, 1f, 1f, 0f));
        shockwaveRingTex = new GradientTexture2D
        {
            Gradient = grad,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 1.0f),
            Width = 128,
            Height = 128
        };
        return shockwaveRingTex;
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

    static StandardMaterial3D CreateShockwaveMaterial(Color baseColor)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = baseColor,
            AlbedoTexture = GetShockwaveRingTexture(),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
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
    bool isGrenadeProjectile;
    bool isMissileProjectile;
    Node3D? projectileVisualNode;

    public static Vector3 ResolveMuzzlePosition(Node3D attacker, Vector3 targetPos)
    {
        if (attacker is RtsUnit unit)
        {
            var forward = (targetPos - attacker.GlobalPosition);
            forward.Y = 0f;
            forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : -attacker.GlobalTransform.Basis.Z;
            var right = forward.Cross(Vector3.Up).Normalized();

            if (BattleUnitCatalog.IsAirUnit(unit.UnitKey))
            {
                return attacker.GlobalPosition + forward * 1.5f + Vector3.Down * 0.15f;
            }

            if (BattleUnitCatalog.IsInfantryLike(unit.UnitKey))
            {
                // 步兵枪口位置：位于胸前持枪高度 (0.72m)，向前延伸 0.62m，稍偏右侧 (0.15m)
                return attacker.GlobalPosition + Vector3.Up * 0.72f + forward * 0.62f - right * 0.15f;
            }

            // 载具类：优先沿主炮仰角节点朝前延伸炮管长度 (1.35m)
            if (unit.WeaponPitchNode is not null && GodotObject.IsInstanceValid(unit.WeaponPitchNode))
            {
                return unit.WeaponPitchNode.GlobalPosition + forward * 1.35f;
            }

            return attacker.GlobalPosition + Vector3.Up * 0.95f + forward * 1.6f;
        }

        return attacker.GlobalPosition + Vector3.Up * 0.75f;
    }

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range)
        => Spawn(owner, attacker, targetNode, damageAmount, playerOwned, range, 0f, 0.5f);

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();

        float targetH = (targetNode is RtsUnit tu && BattleUnitCatalog.IsAirUnit(tu.UnitKey)) ? 0.1f : 0.65f;
        var destination = targetNode.GlobalPosition + Vector3.Up * targetH;
        var source = ResolveMuzzlePosition(attacker, destination);
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

    public static void SpawnGrenade(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        var destination = targetNode.GlobalPosition + Vector3.Up * 0.15f;
        var source = attacker.GlobalPosition + Vector3.Up * 0.85f + (-attacker.GlobalTransform.Basis.Z) * 0.45f;
        var color = new Color(0.98f, 0.62f, 0.16f, 1f);

        // 步兵战术手榴弹：3D 抛物线高弹道 (3.2m)、4.5 米 AOE 爆炸溅射范围伤害
        projectile.isGrenadeProjectile = true;
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, 4.5f, 0.45f, false, playerOwned);
        projectile.arcHeight = 3.2f;
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 14f, 0.45f, 1.1f);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    /// <summary>
    /// 发射高品质 3D 拟真制导导弹（搭载真实 9M311 导弹网格、火箭发动机马赫焰、高密度滚滚尾烟与剧烈爆炸波）
    /// </summary>
    public static void SpawnMissile(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float splashRadius = 3.8f)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        float targetH = (targetNode is RtsUnit tu && BattleUnitCatalog.IsAirUnit(tu.UnitKey)) ? 0.0f : 0.65f;
        var destination = targetNode.GlobalPosition + Vector3.Up * targetH;
        var source = ResolveMuzzlePosition(attacker, destination);
        var color = playerOwned ? new Color(1.0f, 0.72f, 0.22f) : new Color(1.0f, 0.32f, 0.16f);

        projectile.isMissileProjectile = true;
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, splashRadius, 0.35f, false, playerOwned);
        projectile.arcHeight = 1.5f;
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 30f, 0.28f, 1.25f);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    public static void SpawnGround(Node owner, Node3D attacker, Vector3 groundPoint, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        var destination = new Vector3(groundPoint.X, 0.08f, groundPoint.Z);
        var source = ResolveMuzzlePosition(attacker, destination);
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

    public static void SpawnAerialBomb(Node owner, Node3D attacker, Vector3 impactPoint, float damageAmount, bool playerOwned)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();

        // 从轰炸机腹部弹舱 (Y ≈ 14.0m) 释放写实航空重磅航弹，垂直重力加速度砸向地面
        var source = attacker.GlobalPosition + Vector3.Down * 0.9f;
        var destination = new Vector3(impactPoint.X, 0.15f, impactPoint.Z);
        var color = new Color(1.0f, 0.58f, 0.14f, 1f);

        // 重型航空炸弹：超大 6.8 米 AOE 地毯式轰炸爆炸波、垂直重力加速度落体
        projectile.Configure(source, attacker, null, destination, damageAmount, color, 15f, 6.8f, 0.38f, true, playerOwned);
        projectile.arcHeight = -0.6f; // 下垂落体轨迹
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 22f, 0.35f, 0.75f);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    /// <summary>
    /// 生成写实打击地面冲击波光环与地面爆破瞬日光晕（彻底去除原有 CylinderMesh 几何模型）
    /// </summary>
    public static void SpawnImpactEffect(Node owner, Vector3 position, Color color, float radius = 0.8f)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var node = new Node3D
        {
            Name = "ImpactWaveEffect"
        };
        root.AddChild(node);
        node.GlobalPosition = position + Vector3.Up * 0.05f;

        // 1. 水平贴地冲击波圆环（采用平滑渐变无缝纹理，替代突兀的 3D 圆柱）
        var shockwave = new MeshInstance3D
        {
            Name = "ShockwaveRing",
            Mesh = new PlaneMesh
            {
                Size = Vector2.One * (radius * 1.8f)
            },
            MaterialOverride = CreateShockwaveMaterial(new Color(color.R, color.G, color.B, 0.85f))
        };
        node.AddChild(shockwave);
        shockwave.Scale = Vector3.One * 0.15f;

        // 2. 贴地爆炸中心高光瞬间闪光
        var flash = new OmniLight3D
        {
            Name = "ImpactFlashLight",
            LightColor = color,
            LightEnergy = 1.8f,
            OmniRange = Mathf.Max(3.5f, radius * 3.0f)
        };
        node.AddChild(flash);

        var tween = node.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(shockwave, "scale", Vector3.One * 1.55f, 0.22).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(shockwave, "transparency", 1.0f, 0.22).SetEase(Tween.EaseType.In);
        tween.TweenProperty(flash, "light_energy", 0.0f, 0.18).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(node.QueueFree));
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

        var weaponProfile = ResolveWeaponAudioProfile();
        bool isBullet = weaponProfile is "rifle" or "machinegun" or "aa";
        if (isBullet)
        {
            // 枪弹高速直线平直弹道，无抛物线弧度
            arcHeight = 0f;
            duration = Mathf.Clamp(source.DistanceTo(destination) / 82f, 0.05f, 0.32f);
        }
        else
        {
            arcHeight = range > 10f ? 5.5f : 0.8f;
            duration = Mathf.Clamp(source.DistanceTo(destination) / (range > 10f ? 34f : 48f), 0.10f, 0.9f);
        }

        var areaFlashRadius = splashRadius > 0.05f ? Mathf.Clamp(splashRadius * 0.32f, 0.45f, 2.35f) : 0f;
        impactRadius = Mathf.Max(range > 10f ? 1.25f : 0.72f, areaFlashRadius);
    }

    public override void _Ready()
    {
        Name = "CombatProjectile";
        var weaponProfile = ResolveWeaponAudioProfile();
        var isHeavy = splashRadius > 0.05f || arcHeight > 2.5f;
        BattleFeedback.WeaponFire(this, start, weaponProfile, isHeavy);

        if (start.DistanceSquaredTo(lastTarget) > 0.0001f)
            LookAt(lastTarget, Vector3.Up, true);

        bool isFlame = attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower");
        bool isBullet = weaponProfile is "rifle" or "machinegun" or "aa";
        var direction = (lastTarget - start).Normalized();
        SpawnMuzzleFlash(this, start, direction, tint, isFlame, weaponProfile);

        if (isFlame)
        {
            // 喷火兵发射火焰：完全通过世界坐标粒子流呈现逼真喷火流
        }
        else if (isBullet)
        {
            // 小口径枪弹高速流光弹丸 (Tracer)，无巨大 3D 模型与滚滚浓烟干扰视线
            AddChild(CreateBulletTracer(tint, weaponProfile == "aa"));
        }
        else
        {
            // 彻底去除手画 3D 几何模型 (CylinderMesh/SphereMesh/CapsuleMesh)，加载真实写实武器弹药资产
            bool isBomb = arcHeight < 0f;
            projectileVisualNode = TryLoadProjectileVisual(weaponProfile, isHeavy, isBomb, isGrenadeProjectile, isMissileProjectile);
            if (projectileVisualNode is not null)
            {
                AddChild(projectileVisualNode);
            }
            else
            {
                // 备用光子流光粒子/尾迹光带（使用 QuadMesh Billboard，不使用粗糙 3D 几何球体/圆柱）
                AddChild(CreateTracerRibbon(tint, isHeavy));
            }
        }

        AddChild(new OmniLight3D
        {
            Name = "Glow",
            LightColor = tint,
            LightEnergy = isMissileProjectile ? 1.8f : 0.85f,
            OmniRange = isMissileProjectile ? 7.5f : 5.0f
        });

        if (isMissileProjectile)
        {
            // 1. 火箭发动机核心高温马赫尾焰 (Rocket Afterburner Plume)
            var rocketFlame = new CpuParticles3D
            {
                Name = "RocketFlame",
                Amount = 24,
                Lifetime = 0.08f,
                Spread = 6f,
                Direction = new Vector3(0f, 0f, 1f),
                Gravity = Vector3.Zero,
                InitialVelocityMin = 8.0f,
                InitialVelocityMax = 15.0f,
                ScaleAmountMin = 0.18f,
                ScaleAmountMax = 0.42f,
                Position = new Vector3(0f, 0f, 1.25f)
            };
            rocketFlame.Mesh = particleQuad;
            rocketFlame.MaterialOverride = CreateParticleMaterial(Colors.White, true);
            var fCurve = new Curve();
            fCurve.AddPoint(new Vector2(0f, 1f));
            fCurve.AddPoint(new Vector2(1f, 0.1f));
            rocketFlame.ScaleAmountCurve = fCurve;
            var fRamp = new Gradient();
            fRamp.AddPoint(0f, new Color(2.0f, 1.8f, 1.2f, 1f));
            fRamp.AddPoint(0.4f, new Color(1.8f, 0.65f, 0.08f, 0.9f));
            fRamp.AddPoint(1f, new Color(0.8f, 0.15f, 0.02f, 0f));
            rocketFlame.ColorRamp = fRamp;
            AddChild(rocketFlame);
            rocketFlame.Emitting = true;

            // 2. 真实火箭巡航高密度白色烟雾尾迹 (Rocket Dense Smoke Plume)
            var rocketSmoke = new CpuParticles3D
            {
                Name = "RocketSmoke",
                Amount = 75,
                Lifetime = 0.65f,
                Spread = 8f,
                Direction = new Vector3(0f, 0f, 1f),
                Gravity = new Vector3(0f, 0.5f, 0f),
                InitialVelocityMin = 1.2f,
                InitialVelocityMax = 3.5f,
                ScaleAmountMin = 0.28f,
                ScaleAmountMax = 1.25f,
                Position = new Vector3(0f, 0f, 1.25f),
                LocalCoords = false
            };
            rocketSmoke.Mesh = particleQuad;
            rocketSmoke.MaterialOverride = CreateParticleMaterial(new Color(0.92f, 0.92f, 0.92f, 0.70f), false);
            var sCurve = new Curve();
            sCurve.AddPoint(new Vector2(0f, 0.35f));
            sCurve.AddPoint(new Vector2(1f, 2.2f));
            rocketSmoke.ScaleAmountCurve = sCurve;
            var sRamp = new Gradient();
            sRamp.AddPoint(0f, new Color(0.95f, 0.95f, 0.95f, 0.75f));
            sRamp.AddPoint(0.5f, new Color(0.85f, 0.85f, 0.85f, 0.40f));
            sRamp.AddPoint(1f, new Color(0.75f, 0.75f, 0.75f, 0f));
            rocketSmoke.ColorRamp = sRamp;
            AddChild(rocketSmoke);
            rocketSmoke.Emitting = true;
        }

        if (isFlame)
        {
            var trail = new CpuParticles3D
            {
                Name = "FlameTrail",
                Amount = 80,
                Lifetime = 0.45f,
                Spread = 12f,
                Direction = new Vector3(0f, 0f, -1f),
                Gravity = new Vector3(0f, 0.4f, 0f),
                InitialVelocityMin = 5.0f,
                InitialVelocityMax = 10.0f,
                ScaleAmountMin = 0.25f,
                ScaleAmountMax = 0.75f,
                LocalCoords = false
            };
            
            trail.Mesh = particleQuad;
            trail.MaterialOverride = CreateParticleMaterial(new Color(1.0f, 1.0f, 1.0f, 1.0f), true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.3f));
            scaleCurve.AddPoint(new Vector2(0.4f, 1.8f));
            scaleCurve.AddPoint(new Vector2(1f, 0.2f));
            trail.ScaleAmountCurve = scaleCurve;

            var colorRamp = new Gradient();
            colorRamp.AddPoint(0f, new Color(1.8f, 0.6f, 0.1f, 1f));
            colorRamp.AddPoint(0.2f, new Color(1.5f, 0.32f, 0.05f, 0.95f));
            colorRamp.AddPoint(0.5f, new Color(1.1f, 0.15f, 0.02f, 0.8f));
            colorRamp.AddPoint(0.8f, new Color(0.6f, 0.05f, 0.01f, 0.4f));
            colorRamp.AddPoint(1.0f, new Color(0.1f, 0.1f, 0.1f, 0f));
            trail.ColorRamp = colorRamp;

            AddChild(trail);
            trail.Emitting = true;
        }
        if (!isFlame && !isBullet)
        {
            // 仅为重型火炮炮弹/弹丸添加白灰色飞行轨迹烟雾，小口径子弹绝不冒滚滚浓烟
            var trail = new CpuParticles3D
            {
                Name = "SmokeTrail",
                Amount = isHeavy ? 70 : 40,
                Lifetime = isHeavy ? 0.55f : 0.35f,
                Spread = 15f,
                Gravity = new Vector3(0f, 0.2f, 0f),
                InitialVelocityMin = 0.1f,
                InitialVelocityMax = 0.6f,
                ScaleAmountMin = 0.1f,
                ScaleAmountMax = isHeavy ? 0.65f : 0.35f,
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
        {
            float targetHeight = (target is RtsUnit tu && BattleUnitCatalog.IsAirUnit(tu.UnitKey)) ? 0.2f : 0.65f;
            lastTarget = target!.GlobalPosition + Vector3.Up * targetHeight;
        }

        var t = Mathf.Clamp(elapsed / duration, 0f, 1f);
        var next = start.Lerp(lastTarget, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * arcHeight);
        var travel = next - GlobalPosition;
        GlobalPosition = next;

        if (travel.LengthSquared() > 0.0001f && IsInsideTree())
            LookAt(GlobalPosition + travel.Normalized(), Vector3.Up, true);

        // 如果是手榴弹，空中飞行时呈现 3D 翻滚旋转物理动态
        if (isGrenadeProjectile && projectileVisualNode is not null && GodotObject.IsInstanceValid(projectileVisualNode))
        {
            projectileVisualNode.RotateX((float)delta * 14f);
            projectileVisualNode.RotateY((float)delta * 8f);
        }

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
                targetUnit.ApplyDamage(finalDamage, attacker is Node3D attNode ? attNode : null);
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

        bool isHeavy = splashRadius > 0.05f || arcHeight > 2.5f || arcHeight < 0f;
        var weaponProfile = ResolveWeaponAudioProfile();
        bool isBullet = weaponProfile is "rifle" or "machinegun" or "aa";

        if (isNuke)
        {
            float shakePower = Mathf.Clamp(splashRadius * 0.18f, 0.22f, 0.95f);
            float shakeDuration = Mathf.Clamp(splashRadius * 0.12f, 0.25f, 0.65f);
            BattleGameManager.Instance?.TriggerCameraShake(shakePower, shakeDuration);
        }
        else if (isHeavy)
        {
            // 重炮/航弹/高爆手雷命中时给镜头适度的震颤反馈，极大增强炮弹打击沉浸感
            float shakePower = arcHeight < 0f ? 0.28f : Mathf.Clamp(splashRadius * 0.05f, 0.08f, 0.22f);
            BattleGameManager.Instance?.TriggerCameraShake(shakePower, 0.20f);
        }

        if (isBullet)
        {
            var flightDir = (lastTarget - start).LengthSquared() > 0.001f ? (lastTarget - start).Normalized() : Vector3.Forward;
            SpawnBulletImpact(this, lastTarget, tint, flightDir);
            BattleFeedback.Impact(this, lastTarget, false);
        }
        else
        {
            SpawnExplosion(this, lastTarget, tint, isHeavy);
            SpawnImpactEffect(this, lastTarget, tint, impactRadius);
            BattleFeedback.Impact(this, lastTarget, isHeavy);
        }
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
            unit.ApplyDamage(dealt, attacker is Node3D attNodeSplash ? attNodeSplash : null);
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

    static void SpawnMuzzleFlash(Node owner, Vector3 position, Vector3 direction, Color color, bool isFlame, string weaponProfile)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var container = new Node3D { Name = "MuzzleFlashEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;
        var normalizedDir = direction.Normalized();

        bool isBullet = weaponProfile is "rifle" or "machinegun";

        // 🌟 枪口瞬间高光照明点光源
        var muzzleLight = new OmniLight3D
        {
            Name = "MuzzleLight",
            LightColor = color,
            LightEnergy = isFlame ? 1.5f : (isBullet ? 2.0f : 3.6f),
            OmniRange = isFlame ? 4.5f : (isBullet ? 4.2f : 8.5f)
        };
        container.AddChild(muzzleLight);

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
                ScaleAmountMin = 0.22f,
                ScaleAmountMax = 0.75f
            };

            fireParticles.Mesh = particleQuad;
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.5f));
            scaleCurve.AddPoint(new Vector2(0.4f, 1.6f));
            scaleCurve.AddPoint(new Vector2(1f, 0.1f));
            fireParticles.ScaleAmountCurve = scaleCurve;

            var fireRamp = new Gradient();
            fireRamp.AddPoint(0f, new Color(1.8f, 0.65f, 0.15f, 1f));
            fireRamp.AddPoint(0.5f, new Color(1.2f, 0.28f, 0.04f, 0.8f));
            fireRamp.AddPoint(1.0f, new Color(0.75f, 0.05f, 0.01f, 0f));
            fireParticles.ColorRamp = fireRamp;

            container.AddChild(fireParticles);
            fireParticles.Emitting = true;
        }
        else if (isBullet)
        {
            // 小口径枪口火星星芒：极短、小巧、干净，绝不遮挡视线
            var fireParticles = new CpuParticles3D
            {
                Name = "MuzzleFire",
                Amount = 8,
                Lifetime = 0.08f,
                OneShot = true,
                Explosiveness = 0.98f,
                Direction = normalizedDir,
                Spread = 25f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 3.0f,
                InitialVelocityMax = 6.0f,
                ScaleAmountMin = 0.12f,
                ScaleAmountMax = 0.28f
            };

            fireParticles.Mesh = particleQuad;
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 1f));
            scaleCurve.AddPoint(new Vector2(1f, 0f));
            fireParticles.ScaleAmountCurve = scaleCurve;

            var fireRamp = new Gradient();
            fireRamp.AddPoint(0f, new Color(color.R * 2.5f, color.G * 2.2f, color.B * 1.5f, 1f));
            fireRamp.AddPoint(0.5f, new Color(color.R * 1.5f, color.G * 0.8f, color.B * 0.2f, 0.8f));
            fireRamp.AddPoint(1f, new Color(color.R * 0.4f, color.G * 0.1f, 0f, 0f));
            fireParticles.ColorRamp = fireRamp;

            container.AddChild(fireParticles);
            fireParticles.Emitting = true;

            // 微弱枪口余烟
            var smokeParticles = new CpuParticles3D
            {
                Name = "MuzzleSmoke",
                Amount = 3,
                Lifetime = 0.35f,
                OneShot = true,
                Explosiveness = 0.90f,
                Direction = normalizedDir + Vector3.Up * 0.5f,
                Spread = 30f,
                Gravity = new Vector3(0f, 0.6f, 0f),
                InitialVelocityMin = 0.5f,
                InitialVelocityMax = 1.2f,
                ScaleAmountMin = 0.12f,
                ScaleAmountMax = 0.32f
            };
            smokeParticles.Mesh = particleQuad;
            smokeParticles.MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.85f, 0.35f), false);
            container.AddChild(smokeParticles);
            smokeParticles.Emitting = true;
        }
        else
        {
            var fireParticles = new CpuParticles3D
            {
                Name = "MuzzleFire",
                Amount = 22,
                Lifetime = 0.18f,
                OneShot = true,
                Explosiveness = 0.95f,
                Direction = normalizedDir,
                Spread = 32f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 5.5f,
                InitialVelocityMax = 11.5f,
                ScaleAmountMin = 0.35f,
                ScaleAmountMax = 1.15f
            };

            fireParticles.Mesh = particleQuad;
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 1f));
            scaleCurve.AddPoint(new Vector2(1f, 0f));
            fireParticles.ScaleAmountCurve = scaleCurve;

            var fireRamp = new Gradient();
            fireRamp.AddPoint(0f, new Color(color.R * 2.2f, color.G * 1.8f, color.B * 1.2f, 1f));
            fireRamp.AddPoint(0.4f, new Color(color.R * 1.5f, color.G * 0.8f, color.B * 0.2f, 0.85f));
            fireRamp.AddPoint(1f, new Color(color.R * 0.4f, color.G * 0.2f, color.B * 0.05f, 0f));
            fireParticles.ColorRamp = fireRamp;

            container.AddChild(fireParticles);
            fireParticles.Emitting = true;

            var smokeParticles = new CpuParticles3D
            {
                Name = "MuzzleSmoke",
                Amount = 24,
                Lifetime = 0.90f,
                OneShot = true,
                Explosiveness = 0.92f,
                Direction = normalizedDir + Vector3.Up * 0.35f,
                Spread = 40f,
                Gravity = new Vector3(0f, 0.8f, 0f),
                InitialVelocityMin = 1.8f,
                InitialVelocityMax = 4.2f,
                ScaleAmountMin = 0.45f,
                ScaleAmountMax = 1.6f
            };
            smokeParticles.Mesh = particleQuad;
            smokeParticles.MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.85f, 0.65f), false);

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
        timer.SetParallel(true);
        timer.TweenProperty(muzzleLight, "light_energy", 0.0f, isBullet ? 0.06 : 0.14).SetEase(Tween.EaseType.In);
        timer.Chain().TweenInterval(1.0);
        timer.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 生成高品质多层次爆炸特效（火球、浓烟、炽热破片火星、地面扬尘，彻底移除 SphereMesh 几何体）
    /// </summary>
    static void SpawnExplosion(Node owner, Vector3 position, Color color, bool heavy)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        
        var container = new Node3D { Name = "ExplosionEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        // 1. 核心火球粒子 (Fireball)
        var fireParticles = new CpuParticles3D
        {
            Name = "FireParticles",
            Amount = heavy ? 38 : 22,
            Lifetime = 0.45f,
            OneShot = true,
            Explosiveness = 0.88f,
            Direction = Vector3.Up,
            Spread = 65f,
            Gravity = new Vector3(0f, 1.8f, 0f),
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 6.5f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = heavy ? 1.0f : 0.65f
        };

        fireParticles.Mesh = particleQuad;
        fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 1f));
        scaleCurve.AddPoint(new Vector2(1f, 0f));
        fireParticles.ScaleAmountCurve = scaleCurve;
        
        var colorRamp = new Gradient();
        colorRamp.AddPoint(0f, new Color(1.8f, 1.6f, 1.2f, 1f)); // 核心白热光
        colorRamp.AddPoint(0.2f, new Color(color.R * 1.5f, color.G * 1.2f, color.B * 0.6f, 1f)); // 炽热橙黄
        colorRamp.AddPoint(0.6f, new Color(color.R * 0.8f, color.G * 0.25f, color.B * 0.05f, 0.8f)); // 深红火光
        colorRamp.AddPoint(1f, new Color(0.15f, 0.15f, 0.15f, 0f)); // 燃尽黑烟
        fireParticles.ColorRamp = colorRamp;

        container.AddChild(fireParticles);
        fireParticles.Emitting = true;

        // 2. 升腾浓烟粒子 (Smoke Plume)
        var smokeParticles = new CpuParticles3D
        {
            Name = "SmokeParticles",
            Amount = heavy ? 32 : 18,
            Lifetime = 0.95f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = Vector3.Up,
            Spread = 40f,
            Gravity = new Vector3(0f, 1.6f, 0f),
            InitialVelocityMin = 1.8f,
            InitialVelocityMax = 4.2f,
            ScaleAmountMin = 0.4f,
            ScaleAmountMax = heavy ? 1.4f : 0.85f
        };
        smokeParticles.Mesh = particleQuad;
        smokeParticles.MaterialOverride = CreateParticleMaterial(Colors.White, false);

        var smokeScaleCurve = new Curve();
        smokeScaleCurve.AddPoint(new Vector2(0f, 0.4f));
        smokeScaleCurve.AddPoint(new Vector2(1f, 2.2f));
        smokeParticles.ScaleAmountCurve = smokeScaleCurve;

        var smokeColorRamp = new Gradient();
        smokeColorRamp.AddPoint(0f, new Color(0.32f, 0.32f, 0.32f, 0.70f));
        smokeColorRamp.AddPoint(0.5f, new Color(0.24f, 0.24f, 0.24f, 0.45f));
        smokeColorRamp.AddPoint(1f, new Color(0.12f, 0.12f, 0.12f, 0f));
        smokeParticles.ColorRamp = smokeColorRamp;

        container.AddChild(smokeParticles);
        smokeParticles.Emitting = true;

        // 3. 四散迸射的炽热破片/火星 (Sparks - 彻底去除 SphereMesh，使用平滑发光火花粒子)
        var sparkParticles = new CpuParticles3D
        {
            Name = "SparkParticles",
            Amount = heavy ? 24 : 12,
            Lifetime = 0.40f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 85f,
            Gravity = new Vector3(0f, -9.8f, 0f),
            InitialVelocityMin = 4.5f,
            InitialVelocityMax = 8.5f,
            ScaleAmountMin = 0.08f,
            ScaleAmountMax = 0.20f
        };
        sparkParticles.Mesh = particleQuad;
        sparkParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);
        sparkParticles.ScaleAmountCurve = scaleCurve;

        var sparkColorRamp = new Gradient();
        sparkColorRamp.AddPoint(0f, new Color(2.0f, 1.8f, 1.0f, 1f));
        sparkColorRamp.AddPoint(0.4f, new Color(1.6f, 0.65f, 0.1f, 0.9f));
        sparkColorRamp.AddPoint(1.0f, new Color(0.9f, 0.10f, 0.02f, 0f));
        sparkParticles.ColorRamp = sparkColorRamp;

        container.AddChild(sparkParticles);
        sparkParticles.Emitting = true;

        // 4. 爆炸瞬间点光源辐射光芒
        var explosionLight = new OmniLight3D
        {
            Name = "ExplosionFlashLight",
            LightColor = color,
            LightEnergy = heavy ? 2.5f : 1.5f,
            OmniRange = heavy ? 9.0f : 5.5f
        };
        container.AddChild(explosionLight);

        var timer = container.CreateTween();
        timer.SetParallel(true);
        timer.TweenProperty(explosionLight, "light_energy", 0.0f, 0.18).SetEase(Tween.EaseType.In);
        timer.Chain().TweenInterval(1.0);
        timer.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 步兵行进步伐轻柔扬尘特效（自然沙土微尘、柔和淡入淡出、极轻性能损耗）
    /// </summary>
    public static void SpawnFootstepDust(Node owner, Vector3 position)
    {
        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;

        var container = new Node3D { Name = "FootstepDust" };
        root.AddChild(container);
        container.GlobalPosition = position;

        var dust = new CpuParticles3D
        {
            Name = "Puff",
            Amount = 4,
            Lifetime = 0.38f,
            OneShot = true,
            Explosiveness = 0.88f,
            Direction = Vector3.Up + new Vector3((float)GD.RandRange(-0.35, 0.35), 0.25f, (float)GD.RandRange(-0.35, 0.35)).Normalized(),
            Spread = 40f,
            Gravity = new Vector3(0f, 0.25f, 0f),
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 0.55f,
            ScaleAmountMin = 0.12f,
            ScaleAmountMax = 0.30f
        };

        dust.Mesh = particleQuad;
        dust.MaterialOverride = CreateParticleMaterial(new Color(0.78f, 0.73f, 0.60f, 0.32f), false);

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 0.35f));
        scaleCurve.AddPoint(new Vector2(1f, 1.8f));
        dust.ScaleAmountCurve = scaleCurve;

        var colorRamp = new Gradient();
        colorRamp.AddPoint(0f, new Color(0.80f, 0.75f, 0.62f, 0.28f));
        colorRamp.AddPoint(0.5f, new Color(0.76f, 0.71f, 0.58f, 0.16f));
        colorRamp.AddPoint(1.0f, new Color(0.72f, 0.67f, 0.55f, 0f));
        dust.ColorRamp = colorRamp;

        container.AddChild(dust);
        dust.Emitting = true;

        var tween = container.CreateTween();
        tween.TweenInterval(0.42);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 步枪/机枪步兵开火右侧高速抛壳特效（仿真黄铜弹壳微粒、翻转抛物线掉落与地面轻弹）
    /// </summary>
    public static void SpawnCartridgeEjection(Node owner, Vector3 muzzlePos, Vector3 forwardDir, Vector3 rightDir)
    {
        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;

        var shell = new EjectedCartridge
        {
            Name = "CartridgeShell",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.012f,
                BottomRadius = 0.012f,
                Height = 0.042f
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.80f, 0.32f),
                Metallic = 0.95f,
                Roughness = 0.22f,
                EmissionEnabled = true,
                Emission = new Color(0.55f, 0.42f, 0.08f),
                EmissionEnergyMultiplier = 0.4f
            }
        };

        root.AddChild(shell);
        var spawnPos = muzzlePos - forwardDir * 0.25f + rightDir * 0.08f + Vector3.Up * 0.02f;

        float jitterX = (float)GD.RandRange(-0.15, 0.25);
        float jitterY = (float)GD.RandRange(-0.2, 0.3);
        float jitterZ = (float)GD.RandRange(-0.15, 0.25);
        var vel = (rightDir * 1.85f + Vector3.Up * 1.6f - forwardDir * 0.4f + new Vector3(jitterX, jitterY, jitterZ)).Normalized() * (float)GD.RandRange(1.8, 2.6);

        var rotAxis = new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized();
        float rotSpeed = (float)GD.RandRange(18f, 32f);

        shell.Initialize(spawnPos, vel, rotAxis, rotSpeed);
    }

    /// <summary>
    /// 重装步兵/火箭兵/迫击炮发射时强烈的尾焰冲击气浪与地面震波 (Backblast Plume)
    /// </summary>
    public static void SpawnBackblastEffect(Node owner, Vector3 position, Vector3 forwardDir)
    {
        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;

        var container = new Node3D { Name = "BackblastEffect" };
        root.AddChild(container);

        var backDir = -forwardDir.Normalized();
        var backPos = position + backDir * 0.65f + Vector3.Up * 0.35f;
        container.GlobalPosition = backPos;

        // 1. 尾焰冲击锥形火光粒子 (Jet Flame Cone)
        var flame = new CpuParticles3D
        {
            Name = "BackFlame",
            Amount = 14,
            Lifetime = 0.12f,
            OneShot = true,
            Explosiveness = 0.95f,
            Direction = backDir,
            Spread = 22f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 5.5f,
            InitialVelocityMax = 9.5f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.65f
        };
        flame.Mesh = particleQuad;
        flame.MaterialOverride = CreateParticleMaterial(Colors.White, true);
        var fRamp = new Gradient();
        fRamp.AddPoint(0f, new Color(2.0f, 1.4f, 0.4f, 1f));
        fRamp.AddPoint(0.4f, new Color(1.8f, 0.45f, 0.05f, 0.85f));
        fRamp.AddPoint(1.0f, new Color(0.6f, 0.1f, 0.01f, 0f));
        flame.ColorRamp = fRamp;
        container.AddChild(flame);
        flame.Emitting = true;

        // 2. 尾喷滚滚浓烟气浪 (Backblast Smoke Jet)
        var smoke = new CpuParticles3D
        {
            Name = "BackSmoke",
            Amount = 18,
            Lifetime = 0.45f,
            OneShot = true,
            Explosiveness = 0.90f,
            Direction = backDir + Vector3.Up * 0.2f,
            Spread = 38f,
            Gravity = new Vector3(0f, 0.8f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.35f,
            ScaleAmountMax = 1.15f
        };
        smoke.Mesh = particleQuad;
        smoke.MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.85f, 0.65f), false);
        container.AddChild(smoke);
        smoke.Emitting = true;

        // 3. 地面冲击波圆环 (Ground Blast Shockwave)
        SpawnImpactEffect(owner, backPos, new Color(1.0f, 0.72f, 0.25f), 1.25f);

        var tween = container.CreateTween();
        tween.TweenInterval(0.6);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }

    public partial class EjectedCartridge : MeshInstance3D
    {
        Vector3 velocity;
        Vector3 rotAxis;
        float rotSpeed;
        float age;
        float groundY = 0.02f;
        bool bounced;

        public void Initialize(Vector3 spawnPos, Vector3 initialVel, Vector3 axis, float speed)
        {
            GlobalPosition = spawnPos;
            velocity = initialVel;
            rotAxis = axis;
            rotSpeed = speed;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            age += dt;

            if (age > 1.2f)
            {
                QueueFree();
                return;
            }

            if (age > 0.85f)
            {
                Transparency = Mathf.Clamp((age - 0.85f) / 0.35f, 0f, 1f);
            }

            if (velocity.LengthSquared() > 0.001f)
            {
                velocity.Y -= 9.8f * dt;
                var pos = GlobalPosition + velocity * dt;
                if (pos.Y <= groundY)
                {
                    pos.Y = groundY;
                    if (!bounced)
                    {
                        velocity.Y = -velocity.Y * 0.35f;
                        velocity.X *= 0.55f;
                        velocity.Z *= 0.55f;
                        rotSpeed *= 0.4f;
                        bounced = true;
                    }
                    else
                    {
                        velocity = Vector3.Zero;
                        rotSpeed = 0f;
                    }
                }
                GlobalPosition = pos;
            }

            if (rotSpeed > 0.01f && rotAxis.LengthSquared() > 0.001f)
            {
                Rotate(rotAxis.Normalized(), rotSpeed * dt);
            }
        }
    }

    /// <summary>
    /// 加载高品质 3D 弹药/炮弹/炸弹/制导导弹模型资产 (.glb / .fbx)，并旋转对齐飞行弹道
    /// </summary>
    Node3D? TryLoadProjectileVisual(string weaponProfile, bool isHeavy, bool isBomb, bool isGrenade, bool isMissile)
    {
        string path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-bullet.fbx";
        float scaleMultiplier = 0.85f;
        Vector3 rotationOffset = new Vector3(-Mathf.Pi / 2f, 0f, 0f);

        if (isMissile)
        {
            // 高精写真制导导弹
            if (ResourceLoader.Exists("res://assets/models/missile.glb"))
            {
                path = "res://assets/models/missile.glb";
                scaleMultiplier = 0.92f;
                rotationOffset = Vector3.Zero;
            }
            else if (ResourceLoader.Exists("res://assets/unity_migrated/Assets/External/Kenney/SpaceKit/Models/FBX format/rocket_topA.fbx"))
            {
                path = "res://assets/unity_migrated/Assets/External/Kenney/SpaceKit/Models/FBX format/rocket_topA.fbx";
                scaleMultiplier = 0.95f;
                rotationOffset = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
            }
        }
        else if (isBomb)
        {
            // 航空重磅炸弹
            if (ResourceLoader.Exists("res://assets/unity_migrated/Assets/External/Kenney/SpaceKit/Models/FBX format/rocket_topA.fbx"))
            {
                path = "res://assets/unity_migrated/Assets/External/Kenney/SpaceKit/Models/FBX format/rocket_topA.fbx";
                scaleMultiplier = 1.10f;
                rotationOffset = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
            }
            else
            {
                path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-cannonball.fbx";
                scaleMultiplier = 1.35f;
            }
        }
        else if (isGrenade)
        {
            // 战术手榴弹
            if (ResourceLoader.Exists("res://assets/unity_migrated/Assets/External/Kenney/BlasterKit/Models/FBX format/grenade-a.fbx"))
            {
                path = "res://assets/unity_migrated/Assets/External/Kenney/BlasterKit/Models/FBX format/grenade-a.fbx";
                scaleMultiplier = 0.95f;
                rotationOffset = Vector3.Zero;
            }
        }
        else if (isHeavy || weaponProfile == "naval" || weaponProfile == "artillery")
        {
            // 重型穿甲火炮 / 舰炮 / 自行火炮炮弹
            if (ResourceLoader.Exists("res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-cannonball.fbx"))
            {
                path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-cannonball.fbx";
                scaleMultiplier = 1.15f;
            }
            else
            {
                path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-bullet.fbx";
                scaleMultiplier = 1.20f;
            }
        }

        try
        {
            if (ResourceLoader.Exists(path))
            {
                var scene = ResourceLoader.Load<PackedScene>(path);
                if (scene is not null)
                {
                    var wrapper = new Node3D { Name = "ProjectileMeshWrapper" };
                    var instance = scene.Instantiate<Node3D>();
                    instance.Rotation = rotationOffset;
                    instance.Scale = Vector3.One * scaleMultiplier;
                    wrapper.AddChild(instance);

                    // 为模型递归赋予写实拟真暗金属与破空自发光材质
                    SetShellMaterialModulateRecursive(instance, tint);
                    return wrapper;
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[CombatProjectile] Failed to load custom projectile mesh ({path}): {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 为小口径枪弹（步枪、机枪、高射炮）创建高速白炽发光流光弹道，避免巨大 3D 模型与烟雾阻挡视线
    /// </summary>
    Node3D CreateBulletTracer(Color color, bool isAA)
    {
        var wrapper = new Node3D { Name = "BulletTracerWrapper" };

        // 1. 白炽高温核心 (Incandescent Core)
        var coreColor = new Color(2.6f, 2.5f, 2.2f, 1f);
        var coreMesh = new MeshInstance3D
        {
            Name = "TracerCore",
            Mesh = new CylinderMesh
            {
                TopRadius = isAA ? 0.045f : 0.024f,
                BottomRadius = isAA ? 0.045f : 0.024f,
                Height = isAA ? 0.95f : 0.65f,
                RadialSegments = 5
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = coreColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = color.Lightened(0.2f),
                EmissionEnergyMultiplier = 4.5f
            }
        };
        wrapper.AddChild(coreMesh);

        // 2. 破空外层光带 (Glow Halo Ribbon)
        var glowMesh = new MeshInstance3D
        {
            Name = "TracerGlow",
            Mesh = new QuadMesh
            {
                Size = new Vector2(isAA ? 0.24f : 0.16f, isAA ? 1.25f : 0.85f)
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(color.R, color.G, color.B, 0.75f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        wrapper.AddChild(glowMesh);

        return wrapper;
    }

    /// <summary>
    /// 生成步兵步枪/轻机枪子弹击中装甲或地面的跳弹火花与击打扬尘（彻底取代全尺寸火球爆炸）
    /// </summary>
    static void SpawnBulletImpact(Node owner, Vector3 position, Color color, Vector3 flightDir)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var container = new Node3D { Name = "BulletImpactEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        var ricochetDir = (-flightDir + Vector3.Up * 0.85f).Normalized();

        // 1. 跳弹火花粒子 (High-velocity ricochet sparks)
        var sparkParticles = new CpuParticles3D
        {
            Name = "RicochetSparks",
            Amount = 14,
            Lifetime = 0.22f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = ricochetDir,
            Spread = 55f,
            Gravity = new Vector3(0f, -9.8f, 0f),
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 10.5f,
            ScaleAmountMin = 0.08f,
            ScaleAmountMax = 0.24f
        };
        sparkParticles.Mesh = particleQuad;
        sparkParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true);
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 1f));
        sCurve.AddPoint(new Vector2(1f, 0.1f));
        sparkParticles.ScaleAmountCurve = sCurve;
        var sRamp = new Gradient();
        sRamp.AddPoint(0f, new Color(2.5f, 2.2f, 1.2f, 1f));
        sRamp.AddPoint(0.4f, new Color(2.0f, 0.85f, 0.15f, 0.9f));
        sRamp.AddPoint(1.0f, new Color(0.9f, 0.15f, 0.02f, 0f));
        sparkParticles.ColorRamp = sRamp;
        container.AddChild(sparkParticles);
        sparkParticles.Emitting = true;

        // 2. 击打微量扬尘 (Ground impact dust puff)
        var dustParticles = new CpuParticles3D
        {
            Name = "ImpactDust",
            Amount = 5,
            Lifetime = 0.32f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = Vector3.Up,
            Spread = 45f,
            Gravity = new Vector3(0f, 0.5f, 0f),
            InitialVelocityMin = 0.8f,
            InitialVelocityMax = 2.2f,
            ScaleAmountMin = 0.15f,
            ScaleAmountMax = 0.38f
        };
        dustParticles.Mesh = particleQuad;
        dustParticles.MaterialOverride = CreateParticleMaterial(new Color(0.82f, 0.80f, 0.76f, 0.5f), false);
        var dCurve = new Curve();
        dCurve.AddPoint(new Vector2(0f, 0.4f));
        dCurve.AddPoint(new Vector2(1f, 1.4f));
        dustParticles.ScaleAmountCurve = dCurve;
        container.AddChild(dustParticles);
        dustParticles.Emitting = true;

        // 3. 极速微光照
        var flash = new OmniLight3D
        {
            Name = "ImpactFlash",
            LightColor = color,
            LightEnergy = 1.2f,
            OmniRange = 2.4f
        };
        container.AddChild(flash);

        var tween = container.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(flash, "light_energy", 0.0f, 0.06).SetEase(Tween.EaseType.In);
        tween.Chain().TweenInterval(0.35);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 备用流光光带（使用平滑 Billboard QuadMesh，不使用突兀的 3D 球体/圆柱几何模型）
    /// </summary>
    Node3D CreateTracerRibbon(Color color, bool heavy)
    {
        var ribbon = new MeshInstance3D
        {
            Name = "TracerRibbon",
            Mesh = particleQuad,
            Scale = new Vector3(heavy ? 0.35f : 0.22f, heavy ? 0.35f : 0.22f, heavy ? 0.95f : 0.65f),
            MaterialOverride = MakeMaterial(color, true)
        };
        return ribbon;
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
                AlbedoColor = new Color(0.20f, 0.21f, 0.22f),
                Roughness = 0.25f,
                Metallic = 0.85f,
                EmissionEnabled = true,
                Emission = emissionColor.Lightened(0.25f),
                EmissionEnergyMultiplier = 2.4f
            };
            geom.MaterialOverride = mat;
        }

        foreach (var child in node.GetChildren())
        {
            SetShellMaterialModulateRecursive(child, emissionColor);
        }
    }
}
