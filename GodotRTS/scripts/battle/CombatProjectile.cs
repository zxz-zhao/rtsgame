using Godot;
using GodotRTS.Battle.Combat;

public partial class CombatProjectile : Node3D
{
    static Texture2D? radialGradientTex;
    static Texture2D? shockwaveRingTex;
    static readonly QuadMesh particleQuad = new QuadMesh { Size = Vector2.One };

    public static Texture2D GetRadialGradientTexture()
    {
        if (radialGradientTex is not null)
            return radialGradientTex;

        const int size = 64;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp(1.0f - dist, 0.0f, 1.0f);
                // 平滑三次 Hermite 衰减，RGB 永远为纯白 (1,1,1)，彻底根除 GPU 双线性滤波产生的黑边/黑圈
                alpha = alpha * alpha * (3.0f - 2.0f * alpha);
                img.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, alpha));
            }
        }
        radialGradientTex = ImageTexture.CreateFromImage(img);
        return radialGradientTex;
    }

    public static Gradient CreateRamp(Color start, Color end)
    {
        var g = new Gradient();
        g.SetColor(0, start);
        g.SetColor(1, end);
        return g;
    }

    public static Gradient CreateRamp(Color start, float midT, Color mid, Color end)
    {
        var g = new Gradient();
        g.SetColor(0, start);
        g.SetColor(1, end);
        g.AddPoint(midT, mid);
        return g;
    }

    public static Gradient CreateParticleMaterialRamp(Color start, float midT, Color mid, Color end)
    {
        return CreateRamp(start, midT, mid, end);
    }

    public static Gradient CreateRamp4(Color c0, float t1, Color c1, float t2, Color c2, Color c3)
    {
        var g = new Gradient();
        g.SetColor(0, c0);
        g.SetColor(1, c3);
        g.AddPoint(t1, c1);
        g.AddPoint(t2, c2);
        return g;
    }

    static Texture2D GetShockwaveRingTexture()
    {
        if (shockwaveRingTex is not null)
            return shockwaveRingTex;
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 1f, 1f, 0f));
        grad.SetColor(1, new Color(1f, 1f, 1f, 0f));
        grad.AddPoint(0.55f, new Color(1f, 1f, 1f, 0.05f));
        grad.AddPoint(0.80f, new Color(1f, 1f, 1f, 0.95f));
        grad.AddPoint(0.95f, new Color(1f, 1f, 1f, 1f));
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

    public static StandardMaterial3D CreateParticleMaterial(Color baseColor, bool unshaded = true, bool isAdditive = false)
    {
        return new StandardMaterial3D
        {
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            AlbedoColor = baseColor,
            AlbedoTexture = GetRadialGradientTexture(),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = isAdditive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f
        };
    }

    public static StandardMaterial3D CreateShockwaveMaterial(Color baseColor)
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
    bool isTorpedoProjectile;
    bool isBombProjectile;
    public bool IsPooled { get; set; }
    Node3D? projectileVisualNode;

    public void ResetForReuse()
    {
        applied = true;
        SetProcess(false);
        Visible = false;
        elapsed = 0f;
        duration = 0.5f;
        arcHeight = 0f;
        splashRadius = 0f;
        groundAttack = false;
        isGrenadeProjectile = false;
        isMissileProjectile = false;
        isTorpedoProjectile = false;
        isBombProjectile = false;
        target = null;
        attacker = null;

        // 清理所有旧的子节点（模型、光效、临时粒子），立即脱离父子关系
        for (int i = GetChildCount() - 1; i >= 0; i--)
        {
            var child = GetChild(i);
            RemoveChild(child);
            child.QueueFree();
        }
        projectileVisualNode = null;
    }

    static (CombatProjectile proj, bool isPooled) AllocateProjectile(Node owner)
    {
        EffectPool.Ensure(owner);
        if (EffectPool.Instance is not null)
        {
            return (EffectPool.Instance.AcquireProjectile(), true);
        }
        var root = owner.GetTree()?.CurrentScene ?? owner;
        var p = new CombatProjectile();
        root.AddChild(p);
        return (p, false);
    }

    static void FinalizeLaunch(CombatProjectile projectile, Vector3 source, bool isPooled)
    {
        projectile.GlobalPosition = source;
        projectile.applied = false;
        projectile.Visible = true;
        projectile.SetProcess(true);
        projectile.SetupLaunch();
    }

    public static Vector3 ResolveMuzzlePosition(Node3D attacker, Vector3 targetPos)
    {
        if (attacker is RtsUnit unit)
        {
            return unit.GetMuzzleTransform(targetPos).Position;
        }

        return attacker.GlobalPosition + Vector3.Up * 0.75f;
    }

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range)
        => Spawn(owner, attacker, targetNode, damageAmount, playerOwned, range, 0f, 0.5f);

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var (projectile, isPooled) = AllocateProjectile(owner);

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
            FinalizeLaunch(projectile, source, isPooled);

            var dir = (destination - source).Normalized();
            
            var leftDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-12f));
            var leftDest = source + leftDir * source.DistanceTo(destination);
            var (leftProj, leftPooled) = AllocateProjectile(owner);
            leftProj.Configure(source, attacker, null, leftDest, 0f, color, range, 0f, 0f, true, playerOwned);
            FinalizeLaunch(leftProj, source, leftPooled);

            var rightDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(12f));
            var rightDest = source + rightDir * source.DistanceTo(destination);
            var (rightProj, rightPooled) = AllocateProjectile(owner);
            rightProj.Configure(source, attacker, null, rightDest, 0f, color, range, 0f, 0f, true, playerOwned);
            FinalizeLaunch(rightProj, source, rightPooled);
        }
        else
        {
            projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, areaRadius, falloff, false, playerOwned);
            FinalizeLaunch(projectile, source, isPooled);
        }
    }

    public static void SpawnGrenade(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range)
    {
        var (projectile, isPooled) = AllocateProjectile(owner);
        var destination = targetNode.GlobalPosition + Vector3.Up * 0.15f;
        var source = attacker.GlobalPosition + Vector3.Up * 0.85f + (-attacker.GlobalTransform.Basis.Z) * 0.45f;
        var color = new Color(0.98f, 0.62f, 0.16f, 1f);

        // 步兵战术手榴弹：3D 抛物线高弹道 (3.2m)、4.5 米 AOE 爆炸溅射范围伤害
        projectile.isGrenadeProjectile = true;
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, 4.5f, 0.45f, false, playerOwned);
        projectile.arcHeight = 3.2f;
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 14f, 0.45f, 1.1f);
        FinalizeLaunch(projectile, source, isPooled);
    }

    /// <summary>
    /// 发射高品质 3D 拟真制导导弹（搭载真实 9M311 导弹网格、火箭发动机马赫焰、高密度滚滚尾烟与剧烈爆炸波）
    /// </summary>
    public static void SpawnMissile(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float splashRadius = 0f)
    {
        var (projectile, isPooled) = AllocateProjectile(owner);
        float targetH = (targetNode is RtsUnit tu && BattleUnitCatalog.IsAirUnit(tu.UnitKey)) ? 0.0f : 0.65f;
        var destination = targetNode.GlobalPosition + Vector3.Up * targetH;
        var source = ResolveMuzzlePosition(attacker, destination);
        var color = playerOwned ? new Color(1.0f, 0.72f, 0.22f) : new Color(1.0f, 0.32f, 0.16f);

        projectile.isMissileProjectile = true;
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, splashRadius, 0.35f, false, playerOwned);
        projectile.arcHeight = 1.5f;
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 30f, 0.28f, 1.25f);
        FinalizeLaunch(projectile, source, isPooled);
    }

    public static void SpawnGround(Node owner, Node3D attacker, Vector3 groundPoint, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var (projectile, isPooled) = AllocateProjectile(owner);
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
            FinalizeLaunch(projectile, source, isPooled);

            var dir = (destination - source).Normalized();
            
            var leftDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-12f));
            var leftDest = source + leftDir * source.DistanceTo(destination);
            var (leftProj, leftPooled) = AllocateProjectile(owner);
            leftProj.Configure(source, attacker, null, leftDest, 0f, color, range, 0f, 0f, true, playerOwned);
            FinalizeLaunch(leftProj, source, leftPooled);

            var rightDir = dir.Rotated(Vector3.Up, Mathf.DegToRad(12f));
            var rightDest = source + rightDir * source.DistanceTo(destination);
            var (rightProj, rightPooled) = AllocateProjectile(owner);
            rightProj.Configure(source, attacker, null, rightDest, 0f, color, range, 0f, 0f, true, playerOwned);
            FinalizeLaunch(rightProj, source, rightPooled);
        }
        else
        {
            projectile.Configure(source, attacker, null, destination, damageAmount, color, range, areaRadius, falloff, true, playerOwned);
            FinalizeLaunch(projectile, source, isPooled);
        }
    }

    public static void SpawnAerialBomb(Node owner, Node3D attacker, Vector3 impactPoint, float damageAmount, bool playerOwned)
    {
        var (projectile, isPooled) = AllocateProjectile(owner);

        // 从轰炸机腹部弹舱释放写实航空重磅航弹，重力加速度俯冲砸向地面
        var source = attacker.GlobalPosition + Vector3.Down * 1.0f;
        var destination = new Vector3(impactPoint.X, 0.15f, impactPoint.Z);
        var color = new Color(1.0f, 0.72f, 0.18f, 1f);

        projectile.isBombProjectile = true;
        // 重型航空炸弹：超大 6.8 米 AOE 地毯式轰炸爆炸波、垂直重力抛物线落体
        // targetNode 显式传 null，确保绝对不索敌，纯固定地点/地面范围杀伤
        projectile.Configure(source, attacker, null, destination, damageAmount, color, 15f, 6.8f, 0.38f, true, playerOwned);
        projectile.arcHeight = -0.6f;
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 17f, 1.15f, 1.85f);
        FinalizeLaunch(projectile, source, isPooled);

        // 触发机腹弹舱投弹特效（冷凝气环、弹舱顺流排气羽流与脱钩微光）与投弹释放音效及坠地尖啸声
        var forward = attacker is not null ? -attacker.GlobalTransform.Basis.Z : (destination - source).Normalized();
        SpawnBombBayReleaseEffect(attacker ?? owner, source, forward);
        BattleFeedback.BombRelease(source);
        BattleFeedback.BombWhistle(destination, projectile.duration);
    }

    /// <summary>
    /// 潜艇水下发射 533mm 重型反舰鱼雷（水线贴水高速推进、白色空泡白浪尾迹、命中敌舰掀起冲天海水巨浪水柱）
    /// </summary>
    public static void SpawnTorpedo(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float splashRadius = 0f)
    {
        var (projectile, isPooled) = AllocateProjectile(owner);

        // 鱼雷贴合水线水面 (Y = 0.05m) 保持水平航行
        var destination = new Vector3(targetNode.GlobalPosition.X, 0.05f, targetNode.GlobalPosition.Z);
        var sourcePos = attacker.GlobalPosition;
        var forward = (destination - sourcePos);
        forward.Y = 0f;
        forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : -attacker.GlobalTransform.Basis.Z;
        var source = new Vector3(sourcePos.X + forward.X * 1.8f, 0.05f, sourcePos.Z + forward.Z * 1.8f);

        var color = playerOwned ? new Color(0.40f, 0.85f, 1.0f) : new Color(1.0f, 0.40f, 0.20f);

        projectile.isTorpedoProjectile = true;
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, splashRadius, 0.40f, false, playerOwned);
        projectile.arcHeight = 0f; // 贴水面平直推进
        projectile.duration = Mathf.Clamp(source.DistanceTo(destination) / 22f, 0.45f, 1.6f);
        FinalizeLaunch(projectile, source, isPooled);
    }

    /// <summary>
    /// 生成写实打击地面冲击波光环与地面爆破瞬日光晕（彻底去除原有 CylinderMesh 几何模型）
    /// </summary>
    public static void SpawnImpactEffect(Node owner, Vector3 position, Color color, float radius = 0.8f)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnImpactWave(position, color, radius);
            return;
        }

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
            float horizDist = source.DistanceTo(destination);
            bool isHeavyShell = splashRadius > 0.05f || weaponProfile is "mortar" or "cannon";
            if (isHeavyShell)
            {
                // 大口径曲射火炮/榴弹/火箭弹：高抛物线弹道，弹道顶点与飞行时间随射程动态抬升
                arcHeight = Mathf.Clamp(horizDist * 0.35f, 4.0f, 16.5f);
                duration = Mathf.Clamp(horizDist / 32f, 0.35f, 1.8f);
            }
            else
            {
                arcHeight = range > 10f ? 5.5f : 0.8f;
                duration = Mathf.Clamp(horizDist / (range > 10f ? 34f : 48f), 0.10f, 0.9f);
            }
        }

        var areaFlashRadius = splashRadius > 0.05f ? Mathf.Clamp(splashRadius * 0.32f, 0.45f, 2.35f) : 0f;
        impactRadius = Mathf.Max(range > 10f ? 1.25f : 0.72f, areaFlashRadius);
    }

    public override void _Ready()
    {
        // SetupLaunch 由 FinalizeLaunch 统一在 Configure 配置完成后显式调用
    }

    public void SafeLookAt(Vector3 targetPos)
    {
        if (!IsInsideTree()) return;
        var dir = targetPos - GlobalPosition;
        if (dir.LengthSquared() < 0.00001f) return;
        dir = dir.Normalized();
        // 航弹等接近垂直落体时，如果目标方向与 Up 向量共线会导致 LookAt 退化崩溃，在此平滑切换参考向量
        var up = Mathf.Abs(dir.Y) > 0.92f ? Vector3.Forward : Vector3.Up;
        LookAt(GlobalPosition + dir, up, false);
    }

    public void SetupLaunch()
    {
        Name = "CombatProjectile";
        var weaponProfile = ResolveWeaponAudioProfile();
        var isHeavy = splashRadius > 0.05f || arcHeight > 2.5f || isBombProjectile;

        if (isBombProjectile)
        {
            BattleFeedback.BombWhistle(start, duration);
        }
        else
        {
            BattleFeedback.WeaponFire(this, start, weaponProfile, isHeavy);
        }

        if (start.DistanceSquaredTo(lastTarget) > 0.0001f)
            SafeLookAt(lastTarget);

        bool isFlame = attacker is RtsUnit unit && (unit.UnitKey is "infantry_flamethrower" or "flamethrower");
        bool isBullet = !isMissileProjectile && !isGrenadeProjectile && !isBombProjectile && (weaponProfile is "rifle" or "machinegun" or "aa");
        var direction = (lastTarget - start).Normalized();

        if (!isBombProjectile)
        {
            if (attacker is not RtsUnit)
            {
                SpawnMuzzleFlash(this, start, direction, tint, isFlame, weaponProfile);
            }
        }

        if (isFlame)
        {
            // 喷火兵发射火焰：完全通过世界坐标粒子流呈现逼真喷火流
        }
        else if (isBullet)
        {
            // 小口径枪弹高速流光弹丸 (Tracer)，无巨大 3D 模型与滚滚浓烟干扰视线
            AddChild(CreateBulletTracer(tint, weaponProfile == "aa"));
        }
        else if (isBombProjectile)
        {
            // 航空重磅航弹：高拟真写实 3D 航空炸弹资产模型
            projectileVisualNode = CreateHeavyAerialBombVisual();
            AddChild(projectileVisualNode);
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

            // 🌟 现代战争硬核坦克/重型火炮弹道特效：弹底白炽曳光药柱 (Tracer Flare) + 超音速切空气动激波尾流 (Mach Vapor Wake)
            if (!isGrenadeProjectile && !isTorpedoProjectile && !isBombProjectile)
            {
                AddChild(CreateArtilleryTracerTrail(tint, isHeavy));
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
                Amount = 28,
                Lifetime = 0.09f,
                Spread = 6f,
                Direction = new Vector3(0f, 0f, 1f),
                Gravity = Vector3.Zero,
                InitialVelocityMin = 8.0f,
                InitialVelocityMax = 15.0f,
                ScaleAmountMin = 0.18f,
                ScaleAmountMax = 0.42f,
                Position = new Vector3(0f, 0f, 1.30f)
            };
            rocketFlame.Mesh = particleQuad;
            rocketFlame.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
            var fCurve = new Curve();
            fCurve.AddPoint(new Vector2(0f, 1f));
            fCurve.AddPoint(new Vector2(1f, 0.1f));
            rocketFlame.ScaleAmountCurve = fCurve;
            rocketFlame.ColorRamp = CreateRamp(new Color(2.0f, 1.8f, 1.2f, 1f), 0.4f, new Color(1.8f, 0.65f, 0.08f, 0.9f), new Color(0.8f, 0.15f, 0.02f, 0f));
            AddChild(rocketFlame);
            rocketFlame.Emitting = true;

            // 2. 真实火箭巡航高密度白色烟雾尾迹 (Rocket Dense Smoke Plume) - 采用纯白半透明无光照烟雾，根除暗部黑球
            var rocketSmoke = new CpuParticles3D
            {
                Name = "RocketSmoke",
                Amount = 65,
                Lifetime = 0.65f,
                Spread = 7f,
                Direction = new Vector3(0f, 0f, 1f),
                Gravity = new Vector3(0f, 0.4f, 0f),
                InitialVelocityMin = 1.2f,
                InitialVelocityMax = 3.5f,
                ScaleAmountMin = 0.22f,
                ScaleAmountMax = 0.95f,
                Position = new Vector3(0f, 0f, 1.30f),
                LocalCoords = false
            };
            rocketSmoke.Mesh = particleQuad;
            rocketSmoke.MaterialOverride = CreateParticleMaterial(new Color(0.95f, 0.95f, 0.96f, 0.65f), true, false);
            var sCurve = new Curve();
            sCurve.AddPoint(new Vector2(0f, 0.35f));
            sCurve.AddPoint(new Vector2(1f, 2.0f));
            rocketSmoke.ScaleAmountCurve = sCurve;
            rocketSmoke.ColorRamp = CreateRamp(new Color(0.96f, 0.96f, 0.96f, 0.65f), 0.4f, new Color(0.90f, 0.90f, 0.92f, 0.40f), new Color(0.85f, 0.85f, 0.88f, 0f));
            AddChild(rocketSmoke);
            rocketSmoke.Emitting = true;
        }

        if (isBombProjectile)
        {
            // 1. 航空炸弹下坠密集白色气切冷凝尾羽与滚滚破空浓烟 (High-Visibility Falling Vapor Plume)
            var vaporTrail = new CpuParticles3D
            {
                Name = "BombVaporTrail",
                Amount = 85,
                Lifetime = 0.95f,
                Spread = 8f,
                Direction = new Vector3(0f, 0f, 1f),
                Gravity = new Vector3(0f, 0.4f, 0f),
                InitialVelocityMin = 1.0f,
                InitialVelocityMax = 2.8f,
                ScaleAmountMin = 0.55f,
                ScaleAmountMax = 2.6f,
                Position = new Vector3(0f, 0f, 1.35f),
                LocalCoords = false
            };
            vaporTrail.Mesh = particleQuad;
            vaporTrail.MaterialOverride = CreateParticleMaterial(new Color(0.98f, 0.98f, 1.0f, 0.90f), true, false);
            var vCurve = new Curve();
            vCurve.AddPoint(new Vector2(0f, 0.5f));
            vCurve.AddPoint(new Vector2(0.35f, 1.6f));
            vCurve.AddPoint(new Vector2(1f, 3.2f));
            vaporTrail.ScaleAmountCurve = vCurve;
            vaporTrail.ColorRamp = CreateRamp(new Color(1f, 1f, 1f, 0.90f), 0.35f, new Color(0.95f, 0.95f, 0.98f, 0.60f), new Color(0.88f, 0.90f, 0.95f, 0f));
            AddChild(vaporTrail);
            vaporTrail.Emitting = true;

            // 2. 弹尾高温发光药柱与空泡曳光指示器 (Bomb Base Tracer Flare) - 耀眼暖光指示，方便玩家一眼看清投弹下坠全过程
            var tailFlare = new MeshInstance3D
            {
                Name = "BombTailFlare",
                Mesh = particleQuad,
                Scale = Vector3.One * 0.85f,
                Position = new Vector3(0f, 0f, 1.45f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(2.6f, 1.8f, 0.8f, 1f),
                    AlbedoTexture = GetRadialGradientTexture(),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                }
            };
            AddChild(tailFlare);

            // 3. 弹头撞发引信激活高光与破空自发光
            var fuzeGlow = new OmniLight3D
            {
                Name = "FuzeGlow",
                LightColor = new Color(1.0f, 0.75f, 0.22f),
                LightEnergy = 4.0f,
                OmniRange = 9.5f,
                Position = new Vector3(0f, 0f, -1.35f)
            };
            AddChild(fuzeGlow);
        }

        if (isTorpedoProjectile)
        {
            // 1. 3D 拟真反舰鱼雷本体 (533mm Heavy Torpedo Model)
            var torpedoMesh = new MeshInstance3D
            {
                Name = "TorpedoBody",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.085f,
                    BottomRadius = 0.085f,
                    Height = 0.92f,
                    RadialSegments = 16
                },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = Vector3.Zero,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.18f, 0.22f, 0.26f),
                    Metallic = 0.88f,
                    Roughness = 0.22f
                }
            };
            AddChild(torpedoMesh);

            // 头部反舰战备警戒发光环
            var bandMesh = new MeshInstance3D
            {
                Name = "TorpedoBand",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.088f,
                    BottomRadius = 0.088f,
                    Height = 0.12f,
                    RadialSegments = 16
                },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = new Vector3(0f, 0f, -0.28f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = projectilePlayerOwned ? new Color(0.15f, 0.65f, 1.0f) : new Color(1.0f, 0.35f, 0.15f),
                    EmissionEnabled = true,
                    Emission = projectilePlayerOwned ? new Color(0.15f, 0.65f, 1.0f) : new Color(1.0f, 0.35f, 0.15f),
                    EmissionEnergyMultiplier = 1.4f
                }
            };
            AddChild(bandMesh);

            // 2. 真实水面空泡白浪尾迹 (Torpedo Frothing Bubble Wake)
            var bubbleWake = new CpuParticles3D
            {
                Name = "BubbleWake",
                Amount = 85,
                Lifetime = 0.95f,
                Spread = 8f,
                Direction = new Vector3(0f, 0f, 1f),
                Gravity = new Vector3(0f, 0.15f, 0f),
                InitialVelocityMin = 0.8f,
                InitialVelocityMax = 2.2f,
                ScaleAmountMin = 0.18f,
                ScaleAmountMax = 0.85f,
                Position = new Vector3(0f, 0f, 0.48f),
                LocalCoords = false
            };
            bubbleWake.Mesh = particleQuad;
            bubbleWake.MaterialOverride = CreateParticleMaterial(new Color(0.95f, 0.98f, 1.0f, 0.85f), true, false);
            var bCurve = new Curve();
            bCurve.AddPoint(new Vector2(0f, 0.35f));
            bCurve.AddPoint(new Vector2(0.3f, 1.4f));
            bCurve.AddPoint(new Vector2(1f, 2.2f));
            bubbleWake.ScaleAmountCurve = bCurve;
            bubbleWake.ColorRamp = CreateRamp(new Color(0.96f, 0.98f, 1.0f, 0.85f), 0.5f, new Color(0.88f, 0.94f, 1.0f, 0.55f), new Color(0.80f, 0.90f, 1.0f, 0f));
            AddChild(bubbleWake);
            bubbleWake.Emitting = true;
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
            trail.MaterialOverride = CreateParticleMaterial(new Color(1.0f, 1.0f, 1.0f, 1.0f), true, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.3f));
            scaleCurve.AddPoint(new Vector2(0.4f, 1.8f));
            scaleCurve.AddPoint(new Vector2(1f, 0.2f));
            trail.ScaleAmountCurve = scaleCurve;

            trail.ColorRamp = CreateRamp4(new Color(1.8f, 0.6f, 0.1f, 1f), 0.2f, new Color(1.5f, 0.32f, 0.05f, 0.95f), 0.6f, new Color(1.0f, 0.15f, 0.02f, 0.6f), new Color(0.1f, 0.1f, 0.1f, 0f));

            AddChild(trail);
            trail.Emitting = true;
        }
        // 重型火炮炮弹与实体弹丸采用 3D 金属合金模型与高速白炽发光渲染，保持弹道干净利落，杜绝粒子黑环污染
    }

    public override void _Process(double delta)
    {
        if (applied || !IsInsideTree())
            return;

        elapsed += (float)delta;
        if (!isBombProjectile && GodotObject.IsInstanceValid(target))
        {
            float targetHeight = (target is RtsUnit tu && BattleUnitCatalog.IsAirUnit(tu.UnitKey)) ? 0.2f : 0.65f;
            lastTarget = target!.GlobalPosition + Vector3.Up * targetHeight;
        }

        var t = Mathf.Clamp(elapsed / duration, 0f, 1f);
        Vector3 next;
        if (isTorpedoProjectile)
        {
            next = start.Lerp(lastTarget, t);
            next.Y = 0.05f;
        }
        else if (isBombProjectile)
        {
            // 航空炸弹水平匀速滑行，垂直方向受重力加速度二次曲线下坠 (物理真实抛物线，固定着弹点绝不索敌追踪)
            var xzStart = new Vector2(start.X, start.Z);
            var xzTarget = new Vector2(lastTarget.X, lastTarget.Z);
            var xzCurrent = xzStart.Lerp(xzTarget, t);

            float dropProgress = Mathf.Pow(t, 2.0f);
            float yCurrent = Mathf.Lerp(start.Y, lastTarget.Y, dropProgress);

            next = new Vector3(xzCurrent.X, yCurrent, xzCurrent.Y);
        }
        else
        {
            next = start.Lerp(lastTarget, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * arcHeight);
        }
        var travel = next - GlobalPosition;
        GlobalPosition = next;

        if (travel.LengthSquared() > 0.0001f && IsInsideTree())
            SafeLookAt(GlobalPosition + travel);

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

        // 解除鱼雷空泡水浪尾迹绑定，让其在海面自然漂浮散去
        if (GetNodeOrNull<CpuParticles3D>("BubbleWake") is { } bWake)
        {
            RemoveChild(bWake);
            GetTree().CurrentScene.AddChild(bWake);
            bWake.Emitting = false;
            var tw = bWake.CreateTween();
            tw.TweenInterval(bWake.Lifetime);
            tw.TweenCallback(Callable.From(bWake.QueueFree));
        }

        // 解除航弹白烟尾羽绑定，自然飘散淡出
        if (GetNodeOrNull<CpuParticles3D>("BombVaporTrail") is { } vTrail)
        {
            RemoveChild(vTrail);
            GetTree().CurrentScene.AddChild(vTrail);
            vTrail.Emitting = false;
            var tv = vTrail.CreateTween();
            tv.TweenInterval(vTrail.Lifetime);
            tv.TweenCallback(Callable.From(vTrail.QueueFree));
        }

        // 解除火炮/坦克超音速弹丸激波尾流绑定，自然消散
        if (FindChild("MachVaporTrail", true, false) is CpuParticles3D mTrail)
        {
            mTrail.GetParent()?.RemoveChild(mTrail);
            GetTree().CurrentScene.AddChild(mTrail);
            mTrail.Emitting = false;
            var tm = mTrail.CreateTween();
            tm.TweenInterval(mTrail.Lifetime);
            tm.TweenCallback(Callable.From(mTrail.QueueFree));
        }

        Node3D? directTarget = null;
        var damageAmount = Mathf.RoundToInt(damage);
        if (!groundAttack && GodotObject.IsInstanceValid(target) && target!.HasMethod("ApplyDamage"))
        {
            if (target is RtsUnit targetUnit)
            {
                var wasDeadBefore = targetUnit.IsDead || targetUnit.Health <= 0f;
                string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : attacker is RtsBuilding attB ? attB.BuildKey : "";
                Vector3 attackerPos = attacker is Node3D attNode ? attNode.GlobalPosition : GlobalPosition;
                Vector3 targetPos = targetUnit.GlobalPosition;
                Vector3 targetForward = -targetUnit.GlobalTransform.Basis.Z;

                var result = CombatDamageModel.CalculateDamage(
                    attackerKey, targetUnit.UnitKey, damage, attackerPos, targetPos, targetForward, false);

                damageAmount = Mathf.RoundToInt(result.FinalDamage);
                BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetUnit.PlayerOwned, damageAmount);
                targetUnit.ApplyDamage(result.FinalDamage, attacker is Node3D aNode ? aNode : null, result.IsCritical, result.IsRearHit, result.IsRicochet);

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
                    string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : attacker is RtsBuilding attB ? attB.BuildKey : "";
                    Vector3 attackerPos = attacker is Node3D attNode ? attNode.GlobalPosition : GlobalPosition;
                    Vector3 targetPos = targetBuilding.GlobalPosition;
                    Vector3 targetForward = -targetBuilding.GlobalTransform.Basis.Z;

                    var result = CombatDamageModel.CalculateDamage(
                        attackerKey, targetBuilding.BuildKey, damage, attackerPos, targetPos, targetForward, true);

                    damageAmount = Mathf.RoundToInt(result.FinalDamage);
                    BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetBuilding.PlayerOwned, damageAmount);
                    targetBuilding.ApplyDamage(result.FinalDamage, result.IsCritical);
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

        bool isHeavy = splashRadius > 0.05f || arcHeight > 2.5f || arcHeight < 0f || isMissileProjectile || isTorpedoProjectile || isBombProjectile;
        var weaponProfile = ResolveWeaponAudioProfile();
        bool isBullet = !isMissileProjectile && !isGrenadeProjectile && !isTorpedoProjectile && !isBombProjectile && (weaponProfile is "rifle" or "machinegun" or "aa");
        bool isFlame = !isMissileProjectile && !isGrenadeProjectile && !isTorpedoProjectile && !isBombProjectile && (weaponProfile is "flame");

        if (isNuke)
        {
            float shakePower = Mathf.Clamp(splashRadius * 0.05f, 0.06f, 0.12f);
            float shakeDuration = Mathf.Clamp(splashRadius * 0.06f, 0.25f, 0.45f);
            BattleGameManager.Instance?.TriggerCameraShake(shakePower, shakeDuration);
        }
        else if (isHeavy)
        {
            // 重炮/航弹/鱼雷/高爆手雷/防空导弹命中时给镜头适度的轻微低频震颤，杜绝高频剧烈晃动
            float shakePower = isBombProjectile ? 0.08f : (isTorpedoProjectile ? 0.06f : (arcHeight < 0f ? 0.05f : (isMissileProjectile ? 0.04f : Mathf.Clamp(splashRadius * 0.015f, 0.03f, 0.06f))));
            BattleGameManager.Instance?.TriggerCameraShake(shakePower, isBombProjectile ? 0.24f : 0.18f);
        }

        bool isWater = BattleGameManager.Instance is not null && BattleGameManager.Instance.IsWaterPoint(lastTarget);
        if (isBombProjectile)
        {
            if (isWater)
            {
                SpawnWaterSpoutExplosion(this, lastTarget, true);
                BattleFeedback.WaterSpout(this, lastTarget);
            }
            else
            {
                SpawnAerialBombExplosion(this, lastTarget, tint);
                BattleFeedback.Impact(this, lastTarget, true);
            }
        }
        else if (isTorpedoProjectile)
        {
            SpawnWaterSpoutExplosion(this, lastTarget, true);
            BattleFeedback.WaterSpout(this, lastTarget);
        }
        else if (isBullet)
        {
            var flightDir = (lastTarget - start).LengthSquared() > 0.001f ? (lastTarget - start).Normalized() : Vector3.Forward;
            if (weaponProfile == "aa")
            {
                SpawnFlakAirburst(this, lastTarget, tint);
                BattleFeedback.FlakAirburst(this, lastTarget);
            }
            else
            {
                SpawnBulletImpact(this, lastTarget, tint, flightDir);
                BattleFeedback.Impact(this, lastTarget, false);
            }
        }
        else if (isFlame)
        {
            var flightDir = (lastTarget - start).LengthSquared() > 0.001f ? (lastTarget - start).Normalized() : Vector3.Forward;
            SpawnBulletImpact(this, lastTarget, new Color(1.0f, 0.45f, 0.12f), flightDir);
            BattleFeedback.Impact(this, lastTarget, false);
        }
        else
        {
            if (isWater)
            {
                SpawnWaterSpoutExplosion(this, lastTarget, isHeavy);
                BattleFeedback.WaterSpout(this, lastTarget);
            }
            else
            {
                SpawnExplosion(this, lastTarget, tint, isHeavy);
                if (isHeavy)
                {
                    SpawnImpactEffect(this, lastTarget, tint, impactRadius);
                }
                BattleFeedback.Impact(this, lastTarget, isHeavy);
            }
        }

        if (IsPooled && EffectPool.Instance is not null)
        {
            EffectPool.Instance.ReturnProjectile(this);
        }
        else
        {
            QueueFree();
        }
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
            string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : attacker is RtsBuilding attB ? attB.BuildKey : "";
            Vector3 blastCenter = center;
            Vector3 targetPos = unit.GlobalPosition;
            Vector3 targetForward = -unit.GlobalTransform.Basis.Z;

            var result = CombatDamageModel.CalculateDamage(
                attackerKey, unit.UnitKey, dealt, blastCenter, targetPos, targetForward, false);

            var wasDeadBefore = unit.IsDead || unit.Health <= 0f;
            BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, unit.PlayerOwned, Mathf.RoundToInt(result.FinalDamage));
            unit.ApplyDamage(result.FinalDamage, attacker is Node3D attNodeSplash ? attNodeSplash : null, result.IsCritical, result.IsRearHit, result.IsRicochet);
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
            string attackerKey = attacker is RtsUnit attUnit ? attUnit.UnitKey : attacker is RtsBuilding attB ? attB.BuildKey : "";
            Vector3 blastCenter = center;
            Vector3 targetPos = building.GlobalPosition;
            Vector3 targetForward = -building.GlobalTransform.Basis.Z;

            var result = CombatDamageModel.CalculateDamage(
                attackerKey, building.BuildKey, dealt, blastCenter, targetPos, targetForward, true);

            BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, building.PlayerOwned, Mathf.RoundToInt(result.FinalDamage));
            building.ApplyDamage(result.FinalDamage, result.IsCritical);
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
            if (unit.UnitKey is "fighter" or "scout_plane" or "scout_helicopter")
                return "machinegun";
            if (unit.UnitKey == "anti_air_gun")
                return "aa";
            if (unit.UnitKey == "bomber")
                return "bomb";
            if (BattleUnitCatalog.IsInfantryLike(unit.UnitKey))
            {
                if (unit.UnitKey is "infantry_flamethrower" or "flamethrower")
                    return "flame";
                if (unit.UnitKey == "infantry_artillery")
                    return "mortar";
                return "rifle";
            }

            if (unit.UnitKey == "aircraft_carrier")
                return "aa";
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

    public static void SpawnMuzzleFlash(Node owner, Vector3 position, Vector3 direction, Color color, bool isFlame, string weaponProfile)
    {
        if (EffectPool.Instance is not null)
        {
            var effectiveProfile = isFlame ? "flame" : weaponProfile;
            EffectPool.Instance.SpawnMuzzleFlash(position, direction, color, effectiveProfile);
            return;
        }

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
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.5f));
            scaleCurve.AddPoint(new Vector2(0.4f, 1.6f));
            scaleCurve.AddPoint(new Vector2(1f, 0.1f));
            fireParticles.ScaleAmountCurve = scaleCurve;

            fireParticles.ColorRamp = CreateRamp(new Color(1.8f, 0.65f, 0.15f, 1f), 0.5f, new Color(1.2f, 0.28f, 0.04f, 0.8f), new Color(0.75f, 0.05f, 0.01f, 0f));

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
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 1f));
            scaleCurve.AddPoint(new Vector2(1f, 0f));
            fireParticles.ScaleAmountCurve = scaleCurve;

            fireParticles.ColorRamp = CreateRamp(new Color(color.R * 2.5f, color.G * 2.2f, color.B * 1.5f, 1f), 0.5f, new Color(color.R * 1.5f, color.G * 0.8f, color.B * 0.2f, 0.8f), new Color(color.R * 0.4f, color.G * 0.1f, 0f, 0f));

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
            smokeParticles.MaterialOverride = CreateParticleMaterial(new Color(0.88f, 0.88f, 0.88f, 0.40f), true);
            container.AddChild(smokeParticles);
            smokeParticles.Emitting = true;
        }
        else
        {
            // 🌟 1. 炮口中心超高速穿透火焰喷射锥 (Central Hypervelocity Flame Dart)
            var fireParticles = new CpuParticles3D
            {
                Name = "MuzzleFire",
                Amount = 28,
                Lifetime = 0.16f,
                OneShot = true,
                Explosiveness = 0.96f,
                Direction = normalizedDir,
                Spread = 18f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 6.5f,
                InitialVelocityMax = 14.5f,
                ScaleAmountMin = 0.45f,
                ScaleAmountMax = 1.85f
            };
            fireParticles.Mesh = particleQuad;
            fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 0.6f));
            scaleCurve.AddPoint(new Vector2(0.2f, 1.4f));
            scaleCurve.AddPoint(new Vector2(1f, 0.05f));
            fireParticles.ScaleAmountCurve = scaleCurve;
            fireParticles.ColorRamp = CreateRamp4(
                new Color(2.8f, 2.5f, 1.6f, 1f), 0.20f,
                new Color(2.2f, 1.2f, 0.18f, 1f), 0.60f,
                new Color(1.5f, 0.35f, 0.05f, 0.85f),
                new Color(0.3f, 0.05f, 0.01f, 0f));
            container.AddChild(fireParticles);
            fireParticles.Emitting = true;

            // 🌟 2. 双室/多孔炮口制退器高速侧向排气焰羽 (Dual-Baffle Muzzle Brake Lateral Jets)
            var right = normalizedDir.Cross(Vector3.Up).Normalized();
            if (right.LengthSquared() < 0.001f) right = Vector3.Right;
            var leftDir = (-right * 0.95f + normalizedDir * 0.25f).Normalized();
            var rightDir = (right * 0.95f + normalizedDir * 0.25f).Normalized();

            var brakeLeft = new CpuParticles3D
            {
                Name = "BrakeLeft",
                Amount = 18,
                Lifetime = 0.14f,
                OneShot = true,
                Explosiveness = 0.96f,
                Direction = leftDir,
                Spread = 14f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 5.5f,
                InitialVelocityMax = 11.5f,
                ScaleAmountMin = 0.30f,
                ScaleAmountMax = 1.25f,
                Mesh = particleQuad,
                MaterialOverride = CreateParticleMaterial(Colors.White, true, true),
                ScaleAmountCurve = scaleCurve,
                ColorRamp = CreateRamp(new Color(2.6f, 2.2f, 1.4f, 1f), 0.35f, new Color(2.0f, 1.0f, 0.15f, 0.9f), new Color(0.6f, 0.1f, 0.02f, 0f))
            };
            container.AddChild(brakeLeft);
            brakeLeft.Emitting = true;

            var brakeRight = new CpuParticles3D
            {
                Name = "BrakeRight",
                Amount = 18,
                Lifetime = 0.14f,
                OneShot = true,
                Explosiveness = 0.96f,
                Direction = rightDir,
                Spread = 14f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 5.5f,
                InitialVelocityMax = 11.5f,
                ScaleAmountMin = 0.30f,
                ScaleAmountMax = 1.25f,
                Mesh = particleQuad,
                MaterialOverride = CreateParticleMaterial(Colors.White, true, true),
                ScaleAmountCurve = scaleCurve,
                ColorRamp = CreateRamp(new Color(2.6f, 2.2f, 1.4f, 1f), 0.35f, new Color(2.0f, 1.0f, 0.15f, 0.9f), new Color(0.6f, 0.1f, 0.02f, 0f))
            };
            container.AddChild(brakeRight);
            brakeRight.Emitting = true;

            // 🌟 3. 炮口超压激波地面扬尘扩散 (Ground Concussion Dust Shockwave)
            var groundDust = new CpuParticles3D
            {
                Name = "MuzzleGroundDust",
                Amount = 24,
                Lifetime = 0.65f,
                OneShot = true,
                Explosiveness = 0.95f,
                Direction = normalizedDir,
                Spread = 65f,
                Gravity = new Vector3(0f, -1.5f, 0f),
                InitialVelocityMin = 4.0f,
                InitialVelocityMax = 8.5f,
                ScaleAmountMin = 0.65f,
                ScaleAmountMax = 2.4f,
                Position = new Vector3(0f, -0.45f, 0.5f),
                Mesh = particleQuad,
                MaterialOverride = CreateParticleMaterial(new Color(0.72f, 0.68f, 0.60f, 0.55f), true)
            };
            var gdCurve = new Curve();
            gdCurve.AddPoint(new Vector2(0f, 0.4f));
            gdCurve.AddPoint(new Vector2(0.35f, 1.6f));
            gdCurve.AddPoint(new Vector2(1f, 2.6f));
            groundDust.ScaleAmountCurve = gdCurve;
            groundDust.ColorRamp = CreateRamp(new Color(0.72f, 0.68f, 0.60f, 0.55f), 0.4f, new Color(0.60f, 0.56f, 0.50f, 0.30f), new Color(0.48f, 0.45f, 0.40f, 0f));
            container.AddChild(groundDust);
            groundDust.Emitting = true;

            // 🌟 4. 滚滚出膛灰色硝烟 (Barrel Smoke Plume)
            var smokeParticles = new CpuParticles3D
            {
                Name = "MuzzleSmoke",
                Amount = 28,
                Lifetime = 1.05f,
                OneShot = true,
                Explosiveness = 0.92f,
                Direction = normalizedDir + Vector3.Up * 0.35f,
                Spread = 35f,
                Gravity = new Vector3(0f, 1.2f, 0f),
                InitialVelocityMin = 2.2f,
                InitialVelocityMax = 5.5f,
                ScaleAmountMin = 0.65f,
                ScaleAmountMax = 2.2f,
                Mesh = particleQuad,
                MaterialOverride = CreateParticleMaterial(new Color(0.75f, 0.75f, 0.76f, 0.65f), true),
                ScaleAmountCurve = gdCurve,
                ColorRamp = CreateRamp(new Color(0.75f, 0.75f, 0.76f, 0.65f), 0.45f, new Color(0.65f, 0.65f, 0.66f, 0.35f), new Color(0.50f, 0.50f, 0.52f, 0f))
            };
            container.AddChild(smokeParticles);
            smokeParticles.Emitting = true;

            muzzleLight.LightEnergy = 5.5f;
            muzzleLight.OmniRange = 10.0f;
        }

        var timer = container.CreateTween();
        timer.SetParallel(true);
        timer.TweenProperty(muzzleLight, "light_energy", 0.0f, isBullet ? 0.06 : 0.14).SetEase(Tween.EaseType.In);
        timer.Chain().TweenInterval(1.1);
        timer.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 生成高品质多层次爆炸特效（火球、浓烟、炽热破片火星、地面扬尘，彻底移除 SphereMesh 几何体）
    /// </summary>
    public static void SpawnExplosion(Node owner, Vector3 position, Color color, bool heavy)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnExplosion(position, color, heavy);
            return;
        }

        var root = owner.GetTree().CurrentScene ?? owner;
        
        var container = new Node3D { Name = "ExplosionEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        // 🌟 1. 贴地激波高压环 (Ground Shockwave Ring)
        var shockwave = new MeshInstance3D
        {
            Name = "Shockwave",
            Mesh = new PlaneMesh { Size = Vector2.One * (heavy ? 3.5f : 1.8f) },
            Position = Vector3.Up * 0.04f,
            MaterialOverride = CreateShockwaveMaterial(new Color(1.0f, 0.52f, 0.15f, heavy ? 0.65f : 0.0f)),
            Visible = heavy
        };
        container.AddChild(shockwave);
        shockwave.Scale = Vector3.One * 0.1f;

        // 🌟 2. 泥土/地表碎屑冲天喷泉 (Excavated Earth / Dirt Geyser)
        var dirtGeyser = new CpuParticles3D
        {
            Name = "DirtGeyser",
            Amount = heavy ? 42 : 22,
            Lifetime = 0.85f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 45f,
            Gravity = new Vector3(0f, -16.0f, 0f),
            InitialVelocityMin = heavy ? 8.0f : 5.5f,
            InitialVelocityMax = heavy ? 15.5f : 10.5f,
            ScaleAmountMin = 0.22f,
            ScaleAmountMax = heavy ? 0.55f : 0.35f,
            Mesh = particleQuad,
            MaterialOverride = CreateParticleMaterial(new Color(0.42f, 0.32f, 0.22f, 0.90f), true, false)
        };
        dirtGeyser.ColorRamp = CreateRamp(
            new Color(0.42f, 0.32f, 0.22f, 0.90f), 0.55f,
            new Color(0.26f, 0.18f, 0.12f, 0.55f),
            new Color(0.18f, 0.12f, 0.08f, 0f));
        container.AddChild(dirtGeyser);
        dirtGeyser.Emitting = true;

        // 🌟 3. 核心高压火球粒子 (Fireball)
        var fireParticles = new CpuParticles3D
        {
            Name = "FireParticles",
            Amount = heavy ? 45 : 26,
            Lifetime = 0.50f,
            OneShot = true,
            Explosiveness = 0.94f,
            Direction = Vector3.Up,
            Spread = 65f,
            Gravity = new Vector3(0f, 2.5f, 0f),
            InitialVelocityMin = 4.5f,
            InitialVelocityMax = heavy ? 11.5f : 8.0f,
            ScaleAmountMin = 0.55f,
            ScaleAmountMax = heavy ? 2.2f : 1.4f,
            Mesh = particleQuad,
            MaterialOverride = CreateParticleMaterial(Colors.White, true, false)
        };
        var fCurve = new Curve();
        fCurve.AddPoint(new Vector2(0f, 0.5f));
        fCurve.AddPoint(new Vector2(0.2f, 1.4f));
        fCurve.AddPoint(new Vector2(1f, 0.05f));
        fireParticles.ScaleAmountCurve = fCurve;
        fireParticles.ColorRamp = CreateRamp4(
            new Color(1.0f, 0.72f, 0.16f, 1f), 0.15f,
            new Color(1.0f, 0.38f, 0.03f, 0.95f), 0.48f,
            new Color(0.75f, 0.12f, 0.01f, 0.75f),
            new Color(0.12f, 0.05f, 0.02f, 0f));
        container.AddChild(fireParticles);
        fireParticles.Emitting = true;

        // 🌟 4. 滚滚升腾浓烟粒子 (Deep Slate-Grey Smoke Plume)
        var smokeParticles = new CpuParticles3D
        {
            Name = "SmokeParticles",
            Amount = heavy ? 38 : 22,
            Lifetime = 1.45f,
            OneShot = true,
            Explosiveness = 0.90f,
            Direction = Vector3.Up,
            Spread = 38f,
            Gravity = new Vector3(0f, 2.2f, 0f),
            InitialVelocityMin = 2.6f,
            InitialVelocityMax = heavy ? 6.2f : 4.5f,
            ScaleAmountMin = 0.75f,
            ScaleAmountMax = heavy ? 2.8f : 1.8f,
            Mesh = particleQuad,
            MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.86f, 0.70f), true, false)
        };
        var smokeScaleCurve = new Curve();
        smokeScaleCurve.AddPoint(new Vector2(0f, 0.35f));
        smokeScaleCurve.AddPoint(new Vector2(0.4f, 1.8f));
        smokeScaleCurve.AddPoint(new Vector2(1f, 3.6f));
        smokeParticles.ScaleAmountCurve = smokeScaleCurve;
        smokeParticles.ColorRamp = CreateRamp(
            new Color(0.22f, 0.22f, 0.24f, 0.80f), 0.45f,
            new Color(0.16f, 0.16f, 0.18f, 0.50f),
            new Color(0.10f, 0.10f, 0.12f, 0f));
        container.AddChild(smokeParticles);
        smokeParticles.Emitting = true;

        // 🌟 5. 四散迸射的炽热破片/火星 (Burning Shrapnel Sparks)
        var sparkParticles = new CpuParticles3D
        {
            Name = "SparkParticles",
            Amount = heavy ? 35 : 20,
            Lifetime = 0.55f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = Vector3.Up,
            Spread = 88f,
            Gravity = new Vector3(0f, -12.0f, 0f),
            InitialVelocityMin = 6.5f,
            InitialVelocityMax = heavy ? 14.5f : 9.5f,
            ScaleAmountMin = 0.10f,
            ScaleAmountMax = 0.26f,
            Mesh = particleQuad,
            MaterialOverride = CreateParticleMaterial(Colors.White, true, true)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 1f));
        sCurve.AddPoint(new Vector2(1f, 0f));
        sparkParticles.ScaleAmountCurve = sCurve;
        sparkParticles.ColorRamp = CreateRamp(
            new Color(1.0f, 0.68f, 0.10f, 1f), 0.35f,
            new Color(0.95f, 0.28f, 0.02f, 0.90f),
            new Color(0.40f, 0.05f, 0.0f, 0f));
        container.AddChild(sparkParticles);
        sparkParticles.Emitting = true;

        // 🌟 6. 地面高温灼烧弹坑遗留面片 (Persistent Scorched Crater Decal - 柔和圆形羽化弹坑，杜绝突兀黑方块)
        var scorchDecal = new MeshInstance3D
        {
            Name = "Scorch",
            Mesh = new PlaneMesh { Size = Vector2.One * (heavy ? 3.6f : 2.2f) },
            Position = Vector3.Up * 0.02f,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.14f, 0.12f, 0.10f, 0.55f),
                AlbedoTexture = GetRadialGradientTexture(),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
            }
        };
        container.AddChild(scorchDecal);

        // 🌟 7. 爆炸瞬间全景强闪光
        var explosionLight = new OmniLight3D
        {
            Name = "ExplosionFlashLight",
            LightColor = new Color(1.0f, 0.58f, 0.16f),
            LightEnergy = heavy ? 6.5f : 3.8f,
            OmniRange = heavy ? 16.0f : 9.5f
        };
        container.AddChild(explosionLight);

        var timer = container.CreateTween();
        timer.SetParallel(true);
        if (heavy)
        {
            timer.TweenProperty(shockwave, "scale", Vector3.One * 1.35f, 0.35).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            timer.TweenProperty(shockwave, "transparency", 1.0f, 0.35).SetEase(Tween.EaseType.In);
        }
        timer.TweenProperty(explosionLight, "light_energy", 0.0f, 0.12).SetEase(Tween.EaseType.In);
        timer.TweenProperty(scorchDecal, "transparency", 1.0f, 2.4).SetEase(Tween.EaseType.In).SetDelay(0.6);
        timer.Chain().TweenInterval(2.8);
        timer.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 高炮近炸引信与防空穿甲高爆弹空中爆破产生的黑烟破片爆轰特效 (Flak Airburst)
    /// </summary>
    public static void SpawnFlakAirburst(Node owner, Vector3 position, Color color)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnFlakAirburst(position, color);
            return;
        }

        SpawnExplosion(owner, position, color, false);
    }

    /// <summary>
    /// 鱼雷与反舰重弹击中水面战舰水线时，掀起冲天海水巨浪水柱 (Water Spout / Geyser) 与白浪环涌
    /// </summary>
    public static void SpawnWaterSpoutExplosion(Node owner, Vector3 position, bool heavy = true)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnWaterSpout(position, heavy);
            return;
        }

        var root = owner.GetTree().CurrentScene ?? owner;
        var container = new Node3D { Name = "WaterSpoutEffect" };
        root.AddChild(container);
        container.GlobalPosition = new Vector3(position.X, 0.02f, position.Z);

        // 1. 冲天垂直喷涌巨浪水柱 (Vertical Water Column Geyser)
        var spout = new CpuParticles3D
        {
            Name = "WaterSpoutGeyser",
            Amount = heavy ? 70 : 40,
            Lifetime = 1.05f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 16f,
            Gravity = new Vector3(0f, -14.0f, 0f),
            InitialVelocityMin = heavy ? 12.0f : 8.5f,
            InitialVelocityMax = heavy ? 16.5f : 12.5f,
            ScaleAmountMin = 0.40f,
            ScaleAmountMax = heavy ? 1.75f : 1.20f
        };
        spout.Mesh = particleQuad;
        spout.MaterialOverride = CreateParticleMaterial(new Color(0.92f, 0.96f, 1.0f, 0.90f), true, false);
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.4f));
        sCurve.AddPoint(new Vector2(0.35f, 1.8f));
        sCurve.AddPoint(new Vector2(1f, 0.2f));
        spout.ScaleAmountCurve = sCurve;
        spout.ColorRamp = CreateRamp(new Color(0.95f, 0.98f, 1.0f, 0.92f), 0.6f, new Color(0.85f, 0.92f, 0.98f, 0.65f), new Color(0.75f, 0.85f, 0.95f, 0f));
        container.AddChild(spout);
        spout.Emitting = true;

        // 2. 浪花泡沫四散飞溅环 (Water Splash Mist Ring)
        var splashRing = new CpuParticles3D
        {
            Name = "WaterSplashRing",
            Amount = heavy ? 50 : 28,
            Lifetime = 0.80f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = Vector3.Up,
            Spread = 85f,
            Gravity = new Vector3(0f, -9.0f, 0f),
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 9.5f,
            ScaleAmountMin = 0.50f,
            ScaleAmountMax = heavy ? 1.5f : 1.0f
        };
        splashRing.Mesh = particleQuad;
        splashRing.MaterialOverride = CreateParticleMaterial(new Color(0.90f, 0.95f, 1.0f, 0.80f), true, false);
        splashRing.ScaleAmountCurve = sCurve;
        splashRing.ColorRamp = CreateRamp(new Color(0.92f, 0.96f, 1.0f, 0.88f), 0.5f, new Color(0.80f, 0.90f, 0.98f, 0.45f), new Color(0.70f, 0.82f, 0.94f, 0f));
        container.AddChild(splashRing);
        splashRing.Emitting = true;

        // 3. 贴水面高强度环形冲击水浪波纹 (Water Shockwave Ring)
        var waterShockwave = new MeshInstance3D
        {
            Name = "WaterShockwaveRing",
            Mesh = new PlaneMesh { Size = Vector2.One * (heavy ? 8.5f : 5.5f) },
            MaterialOverride = CreateShockwaveMaterial(new Color(0.85f, 0.95f, 1.0f, 0.90f))
        };
        container.AddChild(waterShockwave);
        waterShockwave.Scale = Vector3.One * 0.1f;

        // 4. 水下爆鸣水下蓝白闪光 (Underwater Flash Light)
        var flash = new OmniLight3D
        {
            Name = "UnderwaterFlash",
            LightColor = new Color(0.55f, 0.85f, 1.0f),
            LightEnergy = 3.5f,
            OmniRange = 14.0f
        };
        container.AddChild(flash);

        var tween = container.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(waterShockwave, "scale", Vector3.One * 1.85f, 0.48).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(waterShockwave, "transparency", 1.0f, 0.48).SetEase(Tween.EaseType.In);
        tween.TweenProperty(flash, "light_energy", 0.0f, 0.28).SetEase(Tween.EaseType.In);
        tween.Chain().TweenInterval(1.1);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 轰炸机机腹弹舱投弹特效（高空气流冷凝扩散环、弹舱解脱白雾与顺流排气羽流）
    /// </summary>
    public static void SpawnBombBayReleaseEffect(Node owner, Vector3 bayPosition, Vector3 bomberForward)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnBombBayRelease(bayPosition, bomberForward);
            return;
        }

        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;

        var container = new Node3D { Name = "BombBayReleaseEffect" };
        root.AddChild(container);
        container.GlobalPosition = bayPosition;

        // 1. 机腹高空冷凝环 (Vapor Condensation Ring) - 沿水平面迅速膨胀淡出
        var vaporRing = new MeshInstance3D
        {
            Name = "VaporRing",
            Mesh = new PlaneMesh { Size = new Vector2(1.8f, 1.8f) },
            MaterialOverride = CreateShockwaveMaterial(new Color(0.92f, 0.95f, 1.0f, 0.75f))
        };
        container.AddChild(vaporRing);
        vaporRing.Scale = Vector3.One * 0.2f;

        // 2. 弹舱解脱顺流冷凝白雾 (Bomb Bay Slipstream Vapor Plume)
        var slipstream = new CpuParticles3D
        {
            Name = "BaySlipstream",
            Amount = 14,
            Lifetime = 0.32f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = (-bomberForward * 1.5f + Vector3.Down * 0.8f).Normalized(),
            Spread = 22f,
            Gravity = new Vector3(0f, 0.2f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.35f,
            ScaleAmountMax = 0.95f
        };
        slipstream.Mesh = particleQuad;
        slipstream.MaterialOverride = CreateParticleMaterial(new Color(0.95f, 0.96f, 0.98f, 0.65f), true, false);
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.4f));
        sCurve.AddPoint(new Vector2(0.5f, 1.2f));
        sCurve.AddPoint(new Vector2(1f, 0.1f));
        slipstream.ScaleAmountCurve = sCurve;
        slipstream.ColorRamp = CreateRamp(new Color(0.95f, 0.96f, 0.98f, 0.70f), 0.5f, new Color(0.88f, 0.92f, 0.96f, 0.35f), new Color(0.80f, 0.85f, 0.90f, 0f));
        container.AddChild(slipstream);
        slipstream.Emitting = true;

        // 3. 弹舱机械解脱指示微光
        var releaseFlash = new OmniLight3D
        {
            Name = "ReleaseFlash",
            LightColor = new Color(1.0f, 0.75f, 0.35f),
            LightEnergy = 1.6f,
            OmniRange = 4.5f
        };
        container.AddChild(releaseFlash);

        // 播放投弹脱钩音效
        BattleFeedback.BombRelease(bayPosition);

        var tween = container.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(vaporRing, "scale", Vector3.One * 2.4f, 0.32).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(vaporRing, "transparency", 1.0f, 0.32).SetEase(Tween.EaseType.In);
        tween.TweenProperty(releaseFlash, "light_energy", 0.0f, 0.14).SetEase(Tween.EaseType.In);
        tween.Chain().TweenInterval(0.35);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 重型航空炸弹（航弹地毯式轰炸）巨型地表爆炸冲击波与烈焰浓烟蘑菇云
    /// </summary>
    public static void SpawnAerialBombExplosion(Node owner, Vector3 position, Color color)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnAerialBombExplosion(position, color);
            return;
        }

        var root = owner.GetTree().CurrentScene ?? owner;
        var container = new Node3D { Name = "AerialBombExplosionEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        // 1. 巨型地面高能超压冲击波环 (Mega Ground Shockwave Ring)
        var shockwave = new MeshInstance3D
        {
            Name = "BombShockwave",
            Mesh = new PlaneMesh { Size = new Vector2(10.5f, 10.5f) },
            MaterialOverride = CreateShockwaveMaterial(new Color(1.0f, 0.78f, 0.35f, 0.90f))
        };
        container.AddChild(shockwave);
        shockwave.Scale = Vector3.One * 0.12f;

        // 2. 升腾翻滚爆心火柱粒子 (Towering Fireball Column)
        var fireParticles = new CpuParticles3D
        {
            Name = "BombFireball",
            Amount = 56,
            Lifetime = 0.65f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = Vector3.Up,
            Spread = 60f,
            Gravity = new Vector3(0f, 2.8f, 0f),
            InitialVelocityMin = 5.5f,
            InitialVelocityMax = 11.5f,
            ScaleAmountMin = 0.55f,
            ScaleAmountMax = 1.85f
        };
        fireParticles.Mesh = particleQuad;
        fireParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
        var fCurve = new Curve();
        fCurve.AddPoint(new Vector2(0f, 0.5f));
        fCurve.AddPoint(new Vector2(0.2f, 1.4f));
        fCurve.AddPoint(new Vector2(1f, 0.05f));
        fireParticles.ScaleAmountCurve = fCurve;
        fireParticles.ColorRamp = CreateRamp4(
            new Color(2.4f, 2.1f, 1.4f, 1f), 0.2f,
            new Color(1.9f, 0.95f, 0.15f, 1f), 0.55f,
            new Color(1.4f, 0.32f, 0.04f, 0.85f),
            new Color(0.25f, 0.05f, 0.01f, 0f));
        container.AddChild(fireParticles);
        fireParticles.Emitting = true;

        // 3. 巨型升腾黑烟蘑菇云 (Mushroom Smoke Cloud)
        var smokeParticles = new CpuParticles3D
        {
            Name = "BombSmokePlume",
            Amount = 46,
            Lifetime = 1.35f,
            OneShot = true,
            Explosiveness = 0.90f,
            Direction = Vector3.Up,
            Spread = 38f,
            Gravity = new Vector3(0f, 2.2f, 0f),
            InitialVelocityMin = 2.8f,
            InitialVelocityMax = 6.2f,
            ScaleAmountMin = 0.85f,
            ScaleAmountMax = 2.65f
        };
        smokeParticles.Mesh = particleQuad;
        smokeParticles.MaterialOverride = CreateParticleMaterial(new Color(0.85f, 0.85f, 0.86f, 0.70f), true, false);
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.35f));
        sCurve.AddPoint(new Vector2(0.4f, 1.8f));
        sCurve.AddPoint(new Vector2(1f, 3.2f));
        smokeParticles.ScaleAmountCurve = sCurve;
        smokeParticles.ColorRamp = CreateRamp(
            new Color(0.88f, 0.88f, 0.90f, 0.75f), 0.45f,
            new Color(0.72f, 0.72f, 0.75f, 0.42f),
            new Color(0.60f, 0.60f, 0.65f, 0f));
        container.AddChild(smokeParticles);
        smokeParticles.Emitting = true;

        // 4. 泥土碎石崩溅 (Dirt / Rock Debris Geyser)
        var dirtParticles = new CpuParticles3D
        {
            Name = "DirtGeyser",
            Amount = 32,
            Lifetime = 0.85f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 50f,
            Gravity = new Vector3(0f, -16.0f, 0f),
            InitialVelocityMin = 8.5f,
            InitialVelocityMax = 15.0f,
            ScaleAmountMin = 0.18f,
            ScaleAmountMax = 0.48f
        };
        dirtParticles.Mesh = particleQuad;
        dirtParticles.MaterialOverride = CreateParticleMaterial(new Color(0.35f, 0.28f, 0.20f, 0.85f), true, false);
        var dCurve = new Curve();
        dCurve.AddPoint(new Vector2(0f, 1f));
        dCurve.AddPoint(new Vector2(1f, 0.4f));
        dirtParticles.ScaleAmountCurve = dCurve;
        dirtParticles.ColorRamp = CreateRamp(new Color(0.38f, 0.30f, 0.22f, 0.85f), 0.6f, new Color(0.28f, 0.22f, 0.16f, 0.50f), new Color(0.20f, 0.15f, 0.10f, 0f));
        container.AddChild(dirtParticles);
        dirtParticles.Emitting = true;

        // 5. 赤红高爆破片火花 (Shrapnel Sparks)
        var sparkParticles = new CpuParticles3D
        {
            Name = "BombSparks",
            Amount = 36,
            Lifetime = 0.55f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = Vector3.Up,
            Spread = 88f,
            Gravity = new Vector3(0f, -12.0f, 0f),
            InitialVelocityMin = 7.0f,
            InitialVelocityMax = 14.0f,
            ScaleAmountMin = 0.10f,
            ScaleAmountMax = 0.28f
        };
        sparkParticles.Mesh = particleQuad;
        sparkParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
        sparkParticles.ColorRamp = CreateRamp(new Color(2.4f, 2.0f, 1.2f, 1f), 0.35f, new Color(1.8f, 0.7f, 0.1f, 0.9f), new Color(0.9f, 0.10f, 0.02f, 0f));
        container.AddChild(sparkParticles);
        sparkParticles.Emitting = true;

        // 6. 强力地面焦黑弹坑灼烧印记 (Ground Scorch Crater Decal - 柔和圆形羽化弹坑)
        var scorch = new MeshInstance3D
        {
            Name = "GroundScorch",
            Mesh = new PlaneMesh { Size = new Vector2(4.2f, 4.2f) },
            Position = Vector3.Up * 0.02f,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.14f, 0.12f, 0.10f, 0.60f),
                AlbedoTexture = GetRadialGradientTexture(),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
            }
        };
        container.AddChild(scorch);

        // 7. 巨型爆心闪光
        var flash = new OmniLight3D
        {
            Name = "BombFlashLight",
            LightColor = new Color(1.0f, 0.72f, 0.35f),
            LightEnergy = 4.2f,
            OmniRange = 16.0f
        };
        container.AddChild(flash);

        var tween = container.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(shockwave, "scale", Vector3.One * 2.2f, 0.38).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(shockwave, "transparency", 1.0f, 0.38).SetEase(Tween.EaseType.In);
        tween.TweenProperty(flash, "light_energy", 0.0f, 0.24).SetEase(Tween.EaseType.In);
        tween.TweenProperty(scorch, "transparency", 1.0f, 3.2).SetEase(Tween.EaseType.In).SetDelay(0.8);
        tween.Chain().TweenInterval(4.0);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }

    /// <summary>
    /// 步兵行进步伐轻柔扬尘特效（自然沙土微尘、柔和淡入淡出、极轻性能损耗）
    /// </summary>
    public static void SpawnFootstepDust(Node owner, Vector3 position)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnFootstepDust(position);
            return;
        }

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
        dust.MaterialOverride = CreateParticleMaterial(new Color(0.78f, 0.73f, 0.60f, 0.32f), true);

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 0.35f));
        scaleCurve.AddPoint(new Vector2(1f, 1.8f));
        dust.ScaleAmountCurve = scaleCurve;

        dust.ColorRamp = CreateRamp(new Color(0.80f, 0.75f, 0.62f, 0.28f), 0.5f, new Color(0.76f, 0.71f, 0.58f, 0.16f), new Color(0.72f, 0.67f, 0.55f, 0f));

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
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnBackblast(position, forwardDir);
            return;
        }

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
        flame.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
        flame.ColorRamp = CreateRamp(new Color(2.0f, 1.4f, 0.4f, 1f), 0.4f, new Color(1.8f, 0.45f, 0.05f, 0.85f), new Color(0.6f, 0.1f, 0.01f, 0f));
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
        smoke.MaterialOverride = CreateParticleMaterial(new Color(0.88f, 0.88f, 0.88f, 0.65f), true);
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
            // 高精写真制导导弹：搭载真实苏联 9M311 导弹网格与迷彩涂装，尾部发射马赫羽焰
            if (ResourceLoader.Exists("res://assets/models/missile.glb"))
            {
                path = "res://assets/models/missile.glb";
                scaleMultiplier = 0.95f;
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
            // 航空重磅高爆航弹：流线型尖头穿甲航弹模型 (weapon-ammo-bullet.fbx)，杜绝太空火箭平顶大黑圆桶
            path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-bullet.fbx";
            scaleMultiplier = 1.35f;
            rotationOffset = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
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
            // 重型穿甲火炮 / 舰炮 / 自行火炮炮弹：使用现代尖头 155mm 炮弹模型 (weapon-ammo-bullet.fbx)，绝不使用 18 世纪黑色海盗实心铁球
            path = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/weapon-ammo-bullet.fbx";
            scaleMultiplier = 1.45f;
            rotationOffset = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
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

                    // 导弹 (missile.glb) 拥有高精度苏联迷彩材质与弹身标识，绝不强制覆盖纯黑暗材质；
                    // 仅为火炮炮弹与炸弹赋予写实军工金属光泽与破空自发光
                    if (!isMissile)
                    {
                        ApplyProjectileMaterialModulate(instance, tint, isBomb, isHeavy || weaponProfile == "artillery" || weaponProfile == "naval");
                    }
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
    /// 构建高细节军工写实重型航空炸弹 (Heavy Aerial Bomb / 500kg 航空高爆航弹)
    /// 包含：流线型整流弹体、高爆黄色警示环、十字尾翼舵面、环形导流尾环、撞发引信
    /// </summary>
    Node3D CreateHeavyAerialBombVisual()
    {
        var bombRoot = new Node3D { Name = "HeavyAerialBomb" };
        bombRoot.Scale = Vector3.One * 2.85f;

        var bodyMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.24f, 0.28f, 0.22f), // 经典军工战术橄榄绿 (Olive Drab)
            Roughness = 0.32f,
            Metallic = 0.75f
        };

        var hazardYellowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.88f, 0.05f), // 高爆战备醒目高对比黄色识别环
            Roughness = 0.20f,
            Metallic = 0.40f,
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.88f, 0.05f),
            EmissionEnergyMultiplier = 2.8f
        };

        var darkSteelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.16f, 0.18f, 0.20f), // 尾翼高强度合金钢
            Roughness = 0.28f,
            Metallic = 0.85f
        };

        var fuzeSilverMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.95f, 0.98f), // 弹尖引信银色金属
            Roughness = 0.15f,
            Metallic = 0.95f,
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.75f, 0.25f),
            EmissionEnergyMultiplier = 2.4f
        };

        // 1. 弹体中段圆柱 (Main Body Cylinder) - 沿 Z 轴布置 (Z 负方向为前方)
        var body = new MeshInstance3D
        {
            Name = "BombBody",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.22f,
                BottomRadius = 0.22f,
                Height = 1.05f,
                RadialSegments = 16
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Position = new Vector3(0f, 0f, 0.05f),
            MaterialOverride = bodyMat
        };
        bombRoot.AddChild(body);

        // 2. 弹头钝圆整流罩 (Nose Cone) - 位于 Z 前方 (-0.50m)
        var nose = new MeshInstance3D
        {
            Name = "BombNose",
            Mesh = new SphereMesh
            {
                Radius = 0.22f,
                Height = 0.48f,
                RadialSegments = 16,
                Rings = 8
            },
            Position = new Vector3(0f, 0f, -0.48f),
            MaterialOverride = bodyMat
        };
        bombRoot.AddChild(nose);

        // 3. 头部与弹腰双段高爆黄色警戒环 (Dual Hazard Warning Stripes)
        var hazardRingFront = new MeshInstance3D
        {
            Name = "HazardRingFront",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.225f,
                BottomRadius = 0.225f,
                Height = 0.16f,
                RadialSegments = 16
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Position = new Vector3(0f, 0f, -0.32f),
            MaterialOverride = hazardYellowMat
        };
        bombRoot.AddChild(hazardRingFront);

        var hazardRingMid = new MeshInstance3D
        {
            Name = "HazardRingMid",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.225f,
                BottomRadius = 0.225f,
                Height = 0.12f,
                RadialSegments = 16
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Position = new Vector3(0f, 0f, 0.15f),
            MaterialOverride = hazardYellowMat
        };
        bombRoot.AddChild(hazardRingMid);

        // 4. 弹头撞发引信风帽 (Nose Arming Fuze)
        var fuze = new MeshInstance3D
        {
            Name = "BombFuze",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.035f,
                BottomRadius = 0.065f,
                Height = 0.14f,
                RadialSegments = 12
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Position = new Vector3(0f, 0f, -0.72f),
            MaterialOverride = fuzeSilverMat
        };
        bombRoot.AddChild(fuze);

        // 5. 尾部收束锥段 (Boat-tail Section) - 位于 Z 后方 (+0.58m)
        var tailCone = new MeshInstance3D
        {
            Name = "TailCone",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.22f,
                BottomRadius = 0.13f,
                Height = 0.45f,
                RadialSegments = 16
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Position = new Vector3(0f, 0f, 0.72f),
            MaterialOverride = bodyMat
        };
        bombRoot.AddChild(tailCone);

        // 6. 十字尾翼舵面 (Cruciform Tail Fins: 垂直舵面 + 水平舵面)
        var finVertical = new MeshInstance3D
        {
            Name = "FinVertical",
            Mesh = new BoxMesh { Size = new Vector3(0.024f, 0.68f, 0.55f) },
            Position = new Vector3(0f, 0f, 0.98f),
            MaterialOverride = darkSteelMat
        };
        bombRoot.AddChild(finVertical);

        var finHorizontal = new MeshInstance3D
        {
            Name = "FinHorizontal",
            Mesh = new BoxMesh { Size = new Vector3(0.68f, 0.024f, 0.55f) },
            Position = new Vector3(0f, 0f, 0.98f),
            MaterialOverride = darkSteelMat
        };
        bombRoot.AddChild(finHorizontal);

        // 7. 环形导流尾环 (Tail Shroud Ring - 经典重型航弹标志性尾部构型)
        var shroudRing = new MeshInstance3D
        {
            Name = "ShroudRing",
            Mesh = new CylinderMesh
            {
                TopRadius = 0.28f,
                BottomRadius = 0.28f,
                Height = 0.18f,
                RadialSegments = 16
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Position = new Vector3(0f, 0f, 1.08f),
            MaterialOverride = darkSteelMat
        };
        bombRoot.AddChild(shroudRing);

        return bombRoot;
    }

    /// <summary>
    /// 为小口径枪弹（步枪、机枪、高射炮）创建高速白炽发光流光弹道，避免巨大 3D 模型与烟雾阻挡视线
    /// 防空高射炮与舰载密集阵自动渲染双联装高初速钨芯曳光弹幕 (Twin-Tracer)
    /// </summary>
    Node3D CreateBulletTracer(Color color, bool isAA)
    {
        var wrapper = new Node3D { Name = "BulletTracerWrapper" };
        var coreColor = new Color(2.6f, 2.5f, 2.2f, 1f);

        float[] xOffsets = isAA ? new float[] { -0.09f, 0.09f } : new float[] { 0f };

        foreach (var ox in xOffsets)
        {
            // 1. 白炽高温核心 (Incandescent Core)
            var coreMesh = new MeshInstance3D
            {
                Name = ox != 0f ? $"TracerCore_{(ox > 0 ? "R" : "L")}" : "TracerCore",
                Position = new Vector3(ox, 0f, 0f),
                Mesh = new CylinderMesh
                {
                    TopRadius = isAA ? 0.038f : 0.024f,
                    BottomRadius = isAA ? 0.038f : 0.024f,
                    Height = isAA ? 0.95f : 0.65f,
                    RadialSegments = 5
                },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = coreColor,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    EmissionEnabled = true,
                    Emission = color.Lightened(0.25f),
                    EmissionEnergyMultiplier = 4.8f
                }
            };
            wrapper.AddChild(coreMesh);

            // 2. 破空外层光带 (Glow Halo Ribbon)
            var glowMesh = new MeshInstance3D
            {
                Name = ox != 0f ? $"TracerGlow_{(ox > 0 ? "R" : "L")}" : "TracerGlow",
                Position = new Vector3(ox, 0f, 0f),
                Mesh = new QuadMesh
                {
                    Size = new Vector2(isAA ? 0.20f : 0.16f, isAA ? 1.25f : 0.85f)
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
        }

        return wrapper;
    }

    /// <summary>
    /// 生成步兵步枪/轻机枪子弹击中装甲或地面的跳弹火花与击打扬尘（彻底取代全尺寸火球爆炸）
    /// </summary>
    public static void SpawnBulletImpact(Node owner, Vector3 position, Color color, Vector3 flightDir)
    {
        if (EffectPool.Instance is not null)
        {
            EffectPool.Instance.SpawnBulletImpact(position, color, flightDir);
            return;
        }

        var root = owner.GetTree().CurrentScene ?? owner;
        var container = new Node3D { Name = "BulletImpactEffect" };
        root.AddChild(container);
        container.GlobalPosition = position;

        var ricochetDir = (-flightDir + Vector3.Up * 0.85f).Normalized();

        // 1. 穿甲击穿/跳弹高温金属破片溅射流 (Molten Armor Spall Spray)
        var spallParticles = new CpuParticles3D
        {
            Name = "ArmorSpall",
            Amount = 18,
            Lifetime = 0.28f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = ricochetDir,
            Spread = 38f,
            Gravity = new Vector3(0f, -14.0f, 0f),
            InitialVelocityMin = 8.5f,
            InitialVelocityMax = 18.0f,
            ScaleAmountMin = 0.06f,
            ScaleAmountMax = 0.20f
        };
        spallParticles.Mesh = particleQuad;
        spallParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
        var spallCurve = new Curve();
        spallCurve.AddPoint(new Vector2(0f, 1f));
        spallCurve.AddPoint(new Vector2(1f, 0.08f));
        spallParticles.ScaleAmountCurve = spallCurve;
        spallParticles.ColorRamp = CreateRamp4(
            new Color(2.8f, 2.5f, 1.4f, 1f), 0.25f,
            new Color(2.2f, 1.1f, 0.18f, 0.95f), 0.65f,
            new Color(1.4f, 0.25f, 0.04f, 0.70f),
            new Color(0.4f, 0.05f, 0.01f, 0f));
        container.AddChild(spallParticles);
        spallParticles.Emitting = true;

        // 2. 动能擦碰四散高亮火星 (Ricochet Sparks)
        var sparkParticles = new CpuParticles3D
        {
            Name = "RicochetSparks",
            Amount = 16,
            Lifetime = 0.25f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = ricochetDir,
            Spread = 62f,
            Gravity = new Vector3(0f, -10.5f, 0f),
            InitialVelocityMin = 6.0f,
            InitialVelocityMax = 12.5f,
            ScaleAmountMin = 0.07f,
            ScaleAmountMax = 0.22f
        };
        sparkParticles.Mesh = particleQuad;
        sparkParticles.MaterialOverride = CreateParticleMaterial(Colors.White, true, true);
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 1f));
        sCurve.AddPoint(new Vector2(1f, 0.1f));
        sparkParticles.ScaleAmountCurve = sCurve;
        sparkParticles.ColorRamp = CreateParticleMaterialRamp(new Color(2.6f, 2.3f, 1.3f, 1f), 0.4f, new Color(2.0f, 0.85f, 0.15f, 0.9f), new Color(0.9f, 0.15f, 0.02f, 0f));
        container.AddChild(sparkParticles);
        sparkParticles.Emitting = true;

        // 3. 水泥地面/泥土微细碎屑与粉尘烟雾 (Concrete/Earth Debris & Dust)
        var dustParticles = new CpuParticles3D
        {
            Name = "ImpactDust",
            Amount = 8,
            Lifetime = 0.42f,
            OneShot = true,
            Explosiveness = 0.94f,
            Direction = Vector3.Up,
            Spread = 50f,
            Gravity = new Vector3(0f, 0.4f, 0f),
            InitialVelocityMin = 1.0f,
            InitialVelocityMax = 2.8f,
            ScaleAmountMin = 0.20f,
            ScaleAmountMax = 0.65f
        };
        dustParticles.Mesh = particleQuad;
        dustParticles.MaterialOverride = CreateParticleMaterial(new Color(0.80f, 0.78f, 0.74f, 0.65f), true);
        var dCurve = new Curve();
        dCurve.AddPoint(new Vector2(0f, 0.35f));
        dCurve.AddPoint(new Vector2(0.4f, 1.2f));
        dCurve.AddPoint(new Vector2(1f, 1.8f));
        dustParticles.ScaleAmountCurve = dCurve;
        dustParticles.ColorRamp = CreateRamp(new Color(0.82f, 0.80f, 0.76f, 0.65f), 0.45f, new Color(0.68f, 0.66f, 0.62f, 0.35f), new Color(0.55f, 0.54f, 0.50f, 0f));
        container.AddChild(dustParticles);
        dustParticles.Emitting = true;

        // 4. 撞击瞬间超高速发光闪烁 (Impact Micro-Flash)
        var flash = new OmniLight3D
        {
            Name = "ImpactFlash",
            LightColor = color,
            LightEnergy = 2.4f,
            OmniRange = 4.2f
        };
        container.AddChild(flash);

        var tween = container.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(flash, "light_energy", 0.0f, 0.06).SetEase(Tween.EaseType.In);
        tween.Chain().TweenInterval(0.42);
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
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                AlbedoTexture = GetRadialGradientTexture(),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
            }
        };
        return ribbon;
    }

    /// <summary>
    /// 现代战争硬核坦克/重型火炮弹道特效：弹底白炽曳光药柱 (Tracer Flare) + 超音速切空气动激波尾流 (Mach Vapor Wake)
    /// </summary>
    Node3D CreateArtilleryTracerTrail(Color color, bool heavy)
    {
        var wrapper = new Node3D { Name = "ArtilleryTracerWrapper" };

        // 1. 弹底白炽高温发光药柱 (Base Tracer Flare) - 面向摄像机的高亮度白炽圆形辉光（必须配径向羽化贴图，杜绝纯白方块）
        var flare = new MeshInstance3D
        {
            Name = "BaseTracerFlare",
            Mesh = particleQuad,
            Scale = Vector3.One * (heavy ? 0.48f : 0.32f),
            Position = new Vector3(0f, 0f, 0.45f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(2.8f, 2.3f, 1.4f, 1f),
                AlbedoTexture = GetRadialGradientTexture(),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
            }
        };
        wrapper.AddChild(flare);

        // 2. 超音速弹丸切空气动激波冷凝尾流 (Mach Vapor Wake)
        var vapor = new CpuParticles3D
        {
            Name = "MachVaporTrail",
            Amount = heavy ? 36 : 22,
            Lifetime = 0.22f,
            Spread = 3.5f,
            Direction = new Vector3(0f, 0f, 1f),
            Gravity = new Vector3(0f, 0.15f, 0f),
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 2.8f,
            ScaleAmountMin = 0.10f,
            ScaleAmountMax = heavy ? 0.38f : 0.24f,
            Position = new Vector3(0f, 0f, 0.50f),
            LocalCoords = false,
            Mesh = particleQuad,
            MaterialOverride = CreateParticleMaterial(new Color(0.94f, 0.96f, 1.0f, 0.50f), true, false)
        };
        var vCurve = new Curve();
        vCurve.AddPoint(new Vector2(0f, 0.5f));
        vCurve.AddPoint(new Vector2(0.3f, 1.5f));
        vCurve.AddPoint(new Vector2(1f, 0.1f));
        vapor.ScaleAmountCurve = vCurve;
        vapor.ColorRamp = CreateRamp(
            new Color(0.96f, 0.98f, 1.0f, 0.65f), 0.40f,
            new Color(0.88f, 0.92f, 0.98f, 0.35f),
            new Color(0.80f, 0.85f, 0.95f, 0f));
        wrapper.AddChild(vapor);
        vapor.Emitting = true;

        return wrapper;
    }

    /// <summary>
    /// 为不同武器类型的弹药模型（航弹、重炮穿甲弹、常规弹药）配置高拟真写实金属材质与破空自发光
    /// </summary>
    void ApplyProjectileMaterialModulate(Node node, Color emissionColor, bool isBomb, bool isArtillery)
    {
        if (node is MeshInstance3D geom)
        {
            StandardMaterial3D mat;
            if (isBomb)
            {
                // 航空重磅炸弹：战术橄榄绿金属涂装，带高爆弹头亮黄色警示环与发光
                mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.42f, 0.50f, 0.36f),
                    Roughness = 0.25f,
                    Metallic = 0.75f,
                    EmissionEnabled = true,
                    Emission = new Color(1.2f, 0.85f, 0.2f),
                    EmissionEnergyMultiplier = 1.2f
                };
            }
            else if (isArtillery)
            {
                // 155mm 现代高爆穿甲弹药：黄铜合金弹带与高硬度合金钢弹体，尖端带超音速破空气动加热流光
                mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.85f, 0.70f, 0.38f),
                    Roughness = 0.22f,
                    Metallic = 0.88f,
                    EmissionEnabled = true,
                    Emission = emissionColor.Lightened(0.35f),
                    EmissionEnergyMultiplier = 1.6f
                };
            }
            else
            {
                // 标准动能穿甲弹丸：精钢金属质感
                mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.72f, 0.74f, 0.78f),
                    Roughness = 0.28f,
                    Metallic = 0.85f,
                    EmissionEnabled = true,
                    Emission = emissionColor.Lightened(0.2f),
                    EmissionEnergyMultiplier = 1.2f
                };
            }
            geom.MaterialOverride = mat;
        }

        foreach (var child in node.GetChildren())
        {
            ApplyProjectileMaterialModulate(child, emissionColor, isBomb, isArtillery);
        }
    }
}
