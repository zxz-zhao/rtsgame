using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 高性能 RTS 战斗战斗特效对象池系统 (EffectPool)
/// 彻底消除 AddChild / QueueFree 在激烈交火时的场景树重构卡顿与 GC 压力
/// </summary>
public partial class EffectPool : Node3D
{
    public static EffectPool? Instance { get; private set; }

    // 各类战斗特效专属对象池
    readonly Stack<PooledExplosion> freeExplosions = new();
    readonly List<PooledExplosion> activeExplosions = new();

    readonly Stack<PooledBombExplosion> freeBombExplosions = new();
    readonly List<PooledBombExplosion> activeBombExplosions = new();

    readonly Stack<PooledBombBayRelease> freeBombBayReleases = new();
    readonly List<PooledBombBayRelease> activeBombBayReleases = new();

    readonly Stack<PooledMuzzleFlash> freeMuzzleFlashes = new();
    readonly List<PooledMuzzleFlash> activeMuzzleFlashes = new();

    readonly Stack<PooledWaterSpout> freeWaterSpouts = new();
    readonly List<PooledWaterSpout> activeWaterSpouts = new();

    readonly Stack<PooledImpactWave> freeImpactWaves = new();
    readonly List<PooledImpactWave> activeImpactWaves = new();

    readonly Stack<PooledBulletImpact> freeBulletImpacts = new();
    readonly List<PooledBulletImpact> activeBulletImpacts = new();

    readonly Stack<PooledFootstepDust> freeFootstepDusts = new();
    readonly List<PooledFootstepDust> activeFootstepDusts = new();

    readonly Stack<PooledBackblast> freeBackblasts = new();
    readonly List<PooledBackblast> activeBackblasts = new();

    readonly Stack<PooledFlakAirburst> freeFlakAirbursts = new();
    readonly List<PooledFlakAirburst> activeFlakAirbursts = new();

    readonly Stack<PooledCatapultSteam> freeCatapultSteams = new();
    readonly List<PooledCatapultSteam> activeCatapultSteams = new();

    readonly Stack<CombatProjectile> freeProjectiles = new();

    Node3D? containerRoot;

    public static void Ensure(Node owner)
    {
        if (Instance is not null && GodotObject.IsInstanceValid(Instance))
            return;

        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;
        if (root.GetNodeOrNull<EffectPool>("EffectPool") is { } existing)
        {
            Instance = existing;
            return;
        }

        var pool = new EffectPool { Name = "EffectPool" };
        if (root.IsNodeReady())
        {
            root.AddChild(pool);
        }
        else
        {
            root.CallDeferred(Node.MethodName.AddChild, pool);
        }
    }

    public override void _Ready()
    {
        Instance = this;
        containerRoot = this;

        // 预热预分配对象池，在加载期完成所有节点构建，杜绝交火时首帧卡顿
        PrewarmPools();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    void PrewarmPools()
    {
        // 预分配常规爆炸池 (24 个)
        for (int i = 0; i < 24; i++)
        {
            var exp = CreateExplosionInstance();
            freeExplosions.Push(exp);
        }

        // 预分配重型航弹地毯轰炸爆炸池 (12 个)
        for (int i = 0; i < 12; i++)
        {
            var bExp = CreateBombExplosionInstance();
            freeBombExplosions.Push(bExp);
        }

        // 预分配机腹弹舱脱钩特效池 (10 个)
        for (int i = 0; i < 10; i++)
        {
            var bay = CreateBombBayReleaseInstance();
            freeBombBayReleases.Push(bay);
        }

        // 预分配枪炮开火炮口火光池 (32 个)
        for (int i = 0; i < 32; i++)
        {
            var mf = CreateMuzzleFlashInstance();
            freeMuzzleFlashes.Push(mf);
        }

        // 预分配战舰水下爆破冲天水柱池 (8 个)
        for (int i = 0; i < 8; i++)
        {
            var ws = CreateWaterSpoutInstance();
            freeWaterSpouts.Push(ws);
        }

        // 预分配地面冲击波光环池 (24 个)
        for (int i = 0; i < 24; i++)
        {
            var wave = CreateImpactWaveInstance();
            freeImpactWaves.Push(wave);
        }

        // 预分配小口径跳弹与击打扬尘池 (32 个)
        for (int i = 0; i < 32; i++)
        {
            var imp = CreateBulletImpactInstance();
            freeBulletImpacts.Push(imp);
        }

        // 预分配步兵行军脚步扬尘池 (24 个)
        for (int i = 0; i < 24; i++)
        {
            var fDust = CreateFootstepDustInstance();
            freeFootstepDusts.Push(fDust);
        }

        // 预分配重装步兵/火箭炮后坐力尾焰冲击池 (12 个)
        for (int i = 0; i < 12; i++)
        {
            var bb = CreateBackblastInstance();
            freeBackblasts.Push(bb);
        }

        // 预分配防空高炮近炸破片空爆池 (16 个)
        for (int i = 0; i < 16; i++)
        {
            var flak = CreateFlakAirburstInstance();
            freeFlakAirbursts.Push(flak);
        }

        // 预分配航母弹射器高压蒸汽池 (8 个)
        for (int i = 0; i < 8; i++)
        {
            var cs = CreateCatapultSteamInstance();
            freeCatapultSteams.Push(cs);
        }

        // 预分配高频复用子弹/炮弹节点 (36 个)
        for (int i = 0; i < 36; i++)
        {
            var proj = new CombatProjectile { Name = $"PooledProjectile_{i}", IsPooled = true };
            containerRoot?.AddChild(proj);
            proj.Visible = false;
            proj.SetProcess(false);
            freeProjectiles.Push(proj);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 集中遍历活跃特效，无额外 Tween 节点创建，0 GC 开销
        for (int i = activeExplosions.Count - 1; i >= 0; i--)
        {
            var exp = activeExplosions[i];
            if (exp.UpdateTick(dt))
            {
                activeExplosions.RemoveAt(i);
                freeExplosions.Push(exp);
            }
        }

        for (int i = activeBombExplosions.Count - 1; i >= 0; i--)
        {
            var bExp = activeBombExplosions[i];
            if (bExp.UpdateTick(dt))
            {
                activeBombExplosions.RemoveAt(i);
                freeBombExplosions.Push(bExp);
            }
        }

        for (int i = activeBombBayReleases.Count - 1; i >= 0; i--)
        {
            var bay = activeBombBayReleases[i];
            if (bay.UpdateTick(dt))
            {
                activeBombBayReleases.RemoveAt(i);
                freeBombBayReleases.Push(bay);
            }
        }

        for (int i = activeMuzzleFlashes.Count - 1; i >= 0; i--)
        {
            var mf = activeMuzzleFlashes[i];
            if (mf.UpdateTick(dt))
            {
                activeMuzzleFlashes.RemoveAt(i);
                freeMuzzleFlashes.Push(mf);
            }
        }

        for (int i = activeWaterSpouts.Count - 1; i >= 0; i--)
        {
            var ws = activeWaterSpouts[i];
            if (ws.UpdateTick(dt))
            {
                activeWaterSpouts.RemoveAt(i);
                freeWaterSpouts.Push(ws);
            }
        }

        for (int i = activeImpactWaves.Count - 1; i >= 0; i--)
        {
            var wave = activeImpactWaves[i];
            if (wave.UpdateTick(dt))
            {
                activeImpactWaves.RemoveAt(i);
                freeImpactWaves.Push(wave);
            }
        }

        for (int i = activeBulletImpacts.Count - 1; i >= 0; i--)
        {
            var imp = activeBulletImpacts[i];
            if (imp.UpdateTick(dt))
            {
                activeBulletImpacts.RemoveAt(i);
                freeBulletImpacts.Push(imp);
            }
        }

        for (int i = activeFootstepDusts.Count - 1; i >= 0; i--)
        {
            var fDust = activeFootstepDusts[i];
            if (fDust.UpdateTick(dt))
            {
                activeFootstepDusts.RemoveAt(i);
                freeFootstepDusts.Push(fDust);
            }
        }

        for (int i = activeBackblasts.Count - 1; i >= 0; i--)
        {
            var bb = activeBackblasts[i];
            if (bb.UpdateTick(dt))
            {
                activeBackblasts.RemoveAt(i);
                freeBackblasts.Push(bb);
            }
        }

        for (int i = activeFlakAirbursts.Count - 1; i >= 0; i--)
        {
            var flak = activeFlakAirbursts[i];
            if (flak.UpdateTick(dt))
            {
                activeFlakAirbursts.RemoveAt(i);
                freeFlakAirbursts.Push(flak);
            }
        }

        for (int i = activeCatapultSteams.Count - 1; i >= 0; i--)
        {
            var cs = activeCatapultSteams[i];
            if (cs.UpdateTick(dt))
            {
                activeCatapultSteams.RemoveAt(i);
                freeCatapultSteams.Push(cs);
            }
        }
    }

    #region Pool Acquires & Returns

    public void SpawnExplosion(Vector3 position, Color color, bool heavy)
    {
        PooledExplosion exp = freeExplosions.Count > 0 ? freeExplosions.Pop() : CreateExplosionInstance();
        exp.Activate(position, color, heavy);
        activeExplosions.Add(exp);
    }

    public void SpawnAerialBombExplosion(Vector3 position, Color color)
    {
        PooledBombExplosion bExp = freeBombExplosions.Count > 0 ? freeBombExplosions.Pop() : CreateBombExplosionInstance();
        bExp.Activate(position, color);
        activeBombExplosions.Add(bExp);
    }

    public void SpawnBombBayRelease(Vector3 position, Vector3 forward)
    {
        PooledBombBayRelease bay = freeBombBayReleases.Count > 0 ? freeBombBayReleases.Pop() : CreateBombBayReleaseInstance();
        bay.Activate(position, forward);
        activeBombBayReleases.Add(bay);
    }

    public void SpawnMuzzleFlash(Vector3 position, Vector3 direction, Color color, string weaponProfile)
    {
        PooledMuzzleFlash mf = freeMuzzleFlashes.Count > 0 ? freeMuzzleFlashes.Pop() : CreateMuzzleFlashInstance();
        mf.Activate(position, direction, color, weaponProfile);
        activeMuzzleFlashes.Add(mf);
    }

    public void SpawnWaterSpout(Vector3 position, bool heavy)
    {
        PooledWaterSpout ws = freeWaterSpouts.Count > 0 ? freeWaterSpouts.Pop() : CreateWaterSpoutInstance();
        ws.Activate(position, heavy);
        activeWaterSpouts.Add(ws);
    }

    public void SpawnImpactWave(Vector3 position, Color color, float radius)
    {
        PooledImpactWave wave = freeImpactWaves.Count > 0 ? freeImpactWaves.Pop() : CreateImpactWaveInstance();
        wave.Activate(position, color, radius);
        activeImpactWaves.Add(wave);
    }

    public void SpawnBulletImpact(Vector3 position, Color color, Vector3 flightDir)
    {
        PooledBulletImpact imp = freeBulletImpacts.Count > 0 ? freeBulletImpacts.Pop() : CreateBulletImpactInstance();
        imp.Activate(position, color, flightDir);
        activeBulletImpacts.Add(imp);
    }

    public void SpawnFootstepDust(Vector3 position)
    {
        PooledFootstepDust fDust = freeFootstepDusts.Count > 0 ? freeFootstepDusts.Pop() : CreateFootstepDustInstance();
        fDust.Activate(position);
        activeFootstepDusts.Add(fDust);
    }

    public void SpawnBackblast(Vector3 position, Vector3 forwardDir)
    {
        PooledBackblast bb = freeBackblasts.Count > 0 ? freeBackblasts.Pop() : CreateBackblastInstance();
        bb.Activate(position, forwardDir);
        activeBackblasts.Add(bb);
    }

    public void SpawnFlakAirburst(Vector3 position, Color color)
    {
        PooledFlakAirburst flak = freeFlakAirbursts.Count > 0 ? freeFlakAirbursts.Pop() : CreateFlakAirburstInstance();
        flak.Activate(position, color);
        activeFlakAirbursts.Add(flak);
    }

    public void SpawnCatapultSteam(Vector3 bowPos, Vector3 forwardDir)
    {
        PooledCatapultSteam cs = freeCatapultSteams.Count > 0 ? freeCatapultSteams.Pop() : CreateCatapultSteamInstance();
        cs.Activate(bowPos, forwardDir);
        activeCatapultSteams.Add(cs);
    }

    public void SpawnRepairSparks(Vector3 position)
    {
        SpawnBulletImpact(position, new Color(2.0f, 1.6f, 0.5f), Vector3.Up);
    }

    public CombatProjectile AcquireProjectile()
    {
        CombatProjectile proj;
        if (freeProjectiles.Count > 0)
        {
            proj = freeProjectiles.Pop();
        }
        else
        {
            proj = new CombatProjectile { IsPooled = true };
            containerRoot?.AddChild(proj);
        }
        proj.ResetForReuse();
        return proj;
    }

    public void ReturnProjectile(CombatProjectile proj)
    {
        proj.SetProcess(false);
        proj.Visible = false;
        proj.ResetForReuse();
        freeProjectiles.Push(proj);
    }

    #endregion

    #region Factory Builders

    PooledExplosion CreateExplosionInstance()
    {
        var node = new PooledExplosion();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledBombExplosion CreateBombExplosionInstance()
    {
        var node = new PooledBombExplosion();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledBombBayRelease CreateBombBayReleaseInstance()
    {
        var node = new PooledBombBayRelease();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledMuzzleFlash CreateMuzzleFlashInstance()
    {
        var node = new PooledMuzzleFlash();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledWaterSpout CreateWaterSpoutInstance()
    {
        var node = new PooledWaterSpout();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledImpactWave CreateImpactWaveInstance()
    {
        var node = new PooledImpactWave();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledBulletImpact CreateBulletImpactInstance()
    {
        var node = new PooledBulletImpact();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledFootstepDust CreateFootstepDustInstance()
    {
        var node = new PooledFootstepDust();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledBackblast CreateBackblastInstance()
    {
        var node = new PooledBackblast();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledFlakAirburst CreateFlakAirburstInstance()
    {
        var node = new PooledFlakAirburst();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    PooledCatapultSteam CreateCatapultSteamInstance()
    {
        var node = new PooledCatapultSteam();
        containerRoot?.AddChild(node);
        node.Visible = false;
        return node;
    }

    #endregion
}

#region Pooled Effect Nodes

public partial class PooledExplosion : Node3D
{
    readonly CpuParticles3D fire;
    readonly CpuParticles3D smoke;
    readonly CpuParticles3D dirtGeyser;
    readonly CpuParticles3D sparks;
    readonly MeshInstance3D shockwave;
    readonly MeshInstance3D scorch;
    readonly OmniLight3D light;
    float elapsed;
    float maxDuration;
    float shockwaveDuration;
    float maxShockwaveScale;

    public PooledExplosion()
    {
        Name = "PooledExplosion";
        var particleQuad = new QuadMesh { Size = new Vector2(1.1f, 1.1f) };

        shockwave = new MeshInstance3D
        {
            Name = "Shockwave",
            Mesh = new PlaneMesh { Size = new Vector2(3.2f, 3.2f) },
            Position = Vector3.Up * 0.04f,
            MaterialOverride = CombatProjectile.CreateShockwaveMaterial(new Color(1.0f, 0.52f, 0.15f, 0.65f))
        };
        AddChild(shockwave);

        fire = new CpuParticles3D
        {
            Name = "Fire",
            Amount = 45,
            Lifetime = 0.55f,
            OneShot = true,
            Explosiveness = 0.94f,
            Direction = Vector3.Up,
            Spread = 65f,
            Gravity = new Vector3(0f, 2.5f, 0f),
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 11.5f,
            ScaleAmountMin = 0.65f,
            ScaleAmountMax = 2.4f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, false)
        };
        var fCurve = new Curve();
        fCurve.AddPoint(new Vector2(0f, 0.5f));
        fCurve.AddPoint(new Vector2(0.2f, 1.4f));
        fCurve.AddPoint(new Vector2(1f, 0.05f));
        fire.ScaleAmountCurve = fCurve;
        fire.ColorRamp = CombatProjectile.CreateRamp4(
            new Color(1.0f, 0.72f, 0.16f, 1f), 0.15f,
            new Color(1.0f, 0.38f, 0.03f, 0.95f), 0.48f,
            new Color(0.75f, 0.12f, 0.01f, 0.75f),
            new Color(0.12f, 0.05f, 0.02f, 0f));
        AddChild(fire);

        dirtGeyser = new CpuParticles3D
        {
            Name = "DirtGeyser",
            Amount = 40,
            Lifetime = 0.85f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 45f,
            Gravity = new Vector3(0f, -17.0f, 0f),
            InitialVelocityMin = 8.0f,
            InitialVelocityMax = 15.5f,
            ScaleAmountMin = 0.22f,
            ScaleAmountMax = 0.55f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.42f, 0.32f, 0.22f, 0.90f), true, false)
        };
        dirtGeyser.ColorRamp = CombatProjectile.CreateRamp(
            new Color(0.42f, 0.32f, 0.22f, 0.90f), 0.55f,
            new Color(0.26f, 0.18f, 0.12f, 0.55f),
            new Color(0.18f, 0.12f, 0.08f, 0f));
        AddChild(dirtGeyser);

        smoke = new CpuParticles3D
        {
            Name = "Smoke",
            Amount = 38,
            Lifetime = 1.45f,
            OneShot = true,
            Explosiveness = 0.90f,
            Direction = Vector3.Up,
            Spread = 38f,
            Gravity = new Vector3(0f, 2.2f, 0f),
            InitialVelocityMin = 2.6f,
            InitialVelocityMax = 6.2f,
            ScaleAmountMin = 0.85f,
            ScaleAmountMax = 2.8f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.85f, 0.85f, 0.86f, 0.70f), true, false)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.35f));
        sCurve.AddPoint(new Vector2(0.4f, 1.8f));
        sCurve.AddPoint(new Vector2(1f, 3.6f));
        smoke.ScaleAmountCurve = sCurve;
        smoke.ColorRamp = CombatProjectile.CreateRamp(
            new Color(0.22f, 0.22f, 0.24f, 0.80f), 0.45f,
            new Color(0.16f, 0.16f, 0.18f, 0.50f),
            new Color(0.10f, 0.10f, 0.12f, 0f));
        AddChild(smoke);

        sparks = new CpuParticles3D
        {
            Name = "Sparks",
            Amount = 35,
            Lifetime = 0.55f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = Vector3.Up,
            Spread = 88f,
            Gravity = new Vector3(0f, -12.0f, 0f),
            InitialVelocityMin = 7.0f,
            InitialVelocityMax = 14.5f,
            ScaleAmountMin = 0.10f,
            ScaleAmountMax = 0.28f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        sparks.ColorRamp = CombatProjectile.CreateRamp(
            new Color(1.0f, 0.68f, 0.10f, 1f), 0.35f,
            new Color(0.95f, 0.28f, 0.02f, 0.90f),
            new Color(0.40f, 0.05f, 0.0f, 0f));
        AddChild(sparks);

        scorch = new MeshInstance3D
        {
            Name = "Scorch",
            Mesh = new PlaneMesh { Size = new Vector2(3.6f, 3.6f) },
            Position = Vector3.Up * 0.02f,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.14f, 0.12f, 0.10f, 0.55f),
                AlbedoTexture = CombatProjectile.GetRadialGradientTexture(),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
            }
        };
        AddChild(scorch);

        light = new OmniLight3D
        {
            Name = "Light",
            LightColor = new Color(1.0f, 0.58f, 0.16f),
            LightEnergy = 5.5f,
            OmniRange = 14.0f
        };
        AddChild(light);
    }

    public void Activate(Vector3 pos, Color color, bool heavy)
    {
        if (pos.Y < 0.08f) pos.Y = 0.08f;
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;
        maxDuration = heavy ? 2.8f : 1.8f;
        shockwaveDuration = heavy ? 0.36f : 0.24f;
        maxShockwaveScale = heavy ? 1.35f : 0.6f;

        shockwave.Visible = heavy;
        shockwave.Scale = heavy ? Vector3.One * 0.15f : Vector3.Zero;
        shockwave.Transparency = 0.15f;

        scorch.Visible = true;
        scorch.Scale = heavy ? Vector3.One * 1.3f : Vector3.One * 0.85f;
        scorch.Transparency = 0.12f;

        light.LightColor = new Color(1.0f, 0.75f, 0.35f);
        light.LightEnergy = heavy ? 6.5f : 3.8f;
        light.OmniRange = heavy ? 16.0f : 10.0f;
        light.Visible = true;

        fire.Amount = heavy ? 48 : 28;
        fire.ScaleAmountMax = heavy ? 2.8f : 1.75f;
        fire.Restart();
        fire.Emitting = true;

        dirtGeyser.Amount = heavy ? 45 : 24;
        dirtGeyser.Restart();
        dirtGeyser.Emitting = true;

        smoke.Amount = heavy ? 42 : 24;
        smoke.ScaleAmountMax = heavy ? 3.8f : 2.4f;
        smoke.Restart();
        smoke.Emitting = true;

        sparks.Amount = heavy ? 38 : 20;
        sparks.Restart();
        sparks.Emitting = true;

        if (heavy)
        {
            BattleGameManager.Instance?.TriggerCameraShake(0.24f, 0.22f);
        }
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;

        // 地面超压扩散冲击波环
        if (elapsed <= shockwaveDuration)
        {
            float st = elapsed / shockwaveDuration;
            float ease = 1f - Mathf.Pow(1f - st, 3);
            shockwave.Scale = Vector3.One * Mathf.Lerp(0.15f, maxShockwaveScale, ease);
            shockwave.Transparency = st;
        }
        else
        {
            shockwave.Visible = false;
        }

        // 点光源前 0.22 秒迅速衰减
        if (elapsed <= 0.22f)
        {
            light.LightEnergy = Mathf.Lerp(light.LightEnergy, 0f, dt * 14f);
        }
        else
        {
            light.Visible = false;
        }

        // 地面焦黑弹坑痕迹持续一段时间后缓缓淡化
        if (elapsed > 0.8f)
        {
            scorch.Transparency = Mathf.Clamp(0.12f + (elapsed - 0.8f) / (maxDuration - 0.8f), 0.12f, 1f);
        }

        if (elapsed >= maxDuration)
        {
            Visible = false;
            fire.Emitting = false;
            dirtGeyser.Emitting = false;
            smoke.Emitting = false;
            sparks.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledBombExplosion : Node3D
{
    readonly MeshInstance3D shockwave;
    readonly CpuParticles3D fireball;
    readonly CpuParticles3D smokePlume;
    readonly CpuParticles3D dirtGeyser;
    readonly CpuParticles3D sparks;
    readonly MeshInstance3D scorch;
    readonly OmniLight3D flashLight;
    float elapsed;
    const float Duration = 3.6f;

    public PooledBombExplosion()
    {
        Name = "PooledBombExplosion";
        var particleQuad = new QuadMesh { Size = new Vector2(1.1f, 1.1f) };

        shockwave = new MeshInstance3D
        {
            Name = "BombShockwave",
            Mesh = new PlaneMesh { Size = new Vector2(16.0f, 16.0f) },
            Position = Vector3.Up * 0.05f,
            MaterialOverride = CombatProjectile.CreateShockwaveMaterial(new Color(1.0f, 0.52f, 0.15f, 0.85f))
        };
        AddChild(shockwave);

        fireball = new CpuParticles3D
        {
            Name = "Fireball",
            Amount = 64,
            Lifetime = 0.75f,
            OneShot = true,
            Explosiveness = 0.94f,
            Direction = Vector3.Up,
            Spread = 60f,
            Gravity = new Vector3(0f, 3.2f, 0f),
            InitialVelocityMin = 6.0f,
            InitialVelocityMax = 14.0f,
            ScaleAmountMin = 0.85f,
            ScaleAmountMax = 2.65f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, false)
        };
        var fCurve = new Curve();
        fCurve.AddPoint(new Vector2(0f, 0.5f));
        fCurve.AddPoint(new Vector2(0.2f, 1.5f));
        fCurve.AddPoint(new Vector2(1f, 0.05f));
        fireball.ScaleAmountCurve = fCurve;
        fireball.ColorRamp = CombatProjectile.CreateRamp4(
            new Color(1.0f, 0.75f, 0.18f, 1f), 0.18f,
            new Color(1.0f, 0.40f, 0.04f, 0.95f), 0.50f,
            new Color(0.78f, 0.14f, 0.02f, 0.75f),
            new Color(0.14f, 0.06f, 0.02f, 0f));
        AddChild(fireball);

        smokePlume = new CpuParticles3D
        {
            Name = "SmokePlume",
            Amount = 56,
            Lifetime = 1.65f,
            OneShot = true,
            Explosiveness = 0.90f,
            Direction = Vector3.Up,
            Spread = 38f,
            Gravity = new Vector3(0f, 2.5f, 0f),
            InitialVelocityMin = 3.5f,
            InitialVelocityMax = 8.5f,
            ScaleAmountMin = 1.2f,
            ScaleAmountMax = 3.8f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.85f, 0.85f, 0.86f, 0.70f), true, false)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.35f));
        sCurve.AddPoint(new Vector2(0.4f, 2.0f));
        sCurve.AddPoint(new Vector2(1f, 4.2f));
        smokePlume.ScaleAmountCurve = sCurve;
        smokePlume.ColorRamp = CombatProjectile.CreateRamp(
            new Color(0.22f, 0.22f, 0.23f, 0.82f), 0.45f,
            new Color(0.18f, 0.18f, 0.19f, 0.55f),
            new Color(0.12f, 0.12f, 0.13f, 0f));
        AddChild(smokePlume);

        dirtGeyser = new CpuParticles3D
        {
            Name = "DirtGeyser",
            Amount = 40,
            Lifetime = 0.95f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 50f,
            Gravity = new Vector3(0f, -16.0f, 0f),
            InitialVelocityMin = 9.5f,
            InitialVelocityMax = 18.0f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.65f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.35f, 0.28f, 0.20f, 0.85f), true, false)
        };
        dirtGeyser.ColorRamp = CombatProjectile.CreateRamp(
            new Color(0.42f, 0.32f, 0.22f, 0.90f), 0.55f,
            new Color(0.30f, 0.22f, 0.15f, 0.55f),
            new Color(0.20f, 0.14f, 0.08f, 0f));
        AddChild(dirtGeyser);

        sparks = new CpuParticles3D
        {
            Name = "Sparks",
            Amount = 48,
            Lifetime = 0.65f,
            OneShot = true,
            Explosiveness = 0.98f,
            Direction = Vector3.Up,
            Spread = 88f,
            Gravity = new Vector3(0f, -12.0f, 0f),
            InitialVelocityMin = 8.0f,
            InitialVelocityMax = 16.0f,
            ScaleAmountMin = 0.12f,
            ScaleAmountMax = 0.35f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        sparks.ColorRamp = CombatProjectile.CreateRamp(
            new Color(1.0f, 0.68f, 0.10f, 1f), 0.35f,
            new Color(0.95f, 0.28f, 0.02f, 0.90f),
            new Color(0.40f, 0.05f, 0.0f, 0f));
        AddChild(sparks);

        scorch = new MeshInstance3D
        {
            Name = "GroundScorch",
            Mesh = new PlaneMesh { Size = new Vector2(4.8f, 4.8f) },
            Position = Vector3.Up * 0.025f,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.14f, 0.12f, 0.10f, 0.60f),
                AlbedoTexture = CombatProjectile.GetRadialGradientTexture(),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
            }
        };
        AddChild(scorch);

        flashLight = new OmniLight3D
        {
            Name = "FlashLight",
            LightColor = new Color(1.0f, 0.72f, 0.35f),
            LightEnergy = 8.5f,
            OmniRange = 26.0f
        };
        AddChild(flashLight);
    }

    public void Activate(Vector3 pos, Color color)
    {
        if (pos.Y < 0.08f) pos.Y = 0.08f;
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;

        shockwave.Visible = true;
        shockwave.Scale = Vector3.One * 0.15f;
        shockwave.Transparency = 0f;

        scorch.Visible = true;
        scorch.Transparency = 0.12f;

        flashLight.Visible = true;
        flashLight.LightEnergy = 8.5f;

        fireball.Restart();
        fireball.Emitting = true;

        smokePlume.Restart();
        smokePlume.Emitting = true;

        dirtGeyser.Restart();
        dirtGeyser.Emitting = true;

        sparks.Restart();
        sparks.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;

        // 冲击波前 0.45 秒快速膨胀淡出
        if (elapsed <= 0.45f)
        {
            float st = elapsed / 0.45f;
            float ease = 1f - Mathf.Pow(1f - st, 3);
            shockwave.Scale = Vector3.One * Mathf.Lerp(0.15f, 2.6f, ease);
            shockwave.Transparency = st;
        }
        else
        {
            shockwave.Visible = false;
        }

        // 点光源前 0.28 秒迅速衰减
        if (elapsed <= 0.28f)
        {
            flashLight.LightEnergy = Mathf.Lerp(8.5f, 0f, elapsed / 0.28f);
        }
        else
        {
            flashLight.Visible = false;
        }

        // 地面弹坑焦黑持续 3.5 秒后缓缓淡化
        if (elapsed > 0.8f)
        {
            scorch.Transparency = Mathf.Clamp(0.12f + (elapsed - 0.8f) / 2.8f, 0.12f, 1f);
        }

        if (elapsed >= Duration)
        {
            Visible = false;
            fireball.Emitting = false;
            smokePlume.Emitting = false;
            dirtGeyser.Emitting = false;
            sparks.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledBombBayRelease : Node3D
{
    readonly MeshInstance3D vaporRing;
    readonly CpuParticles3D slipstream;
    readonly OmniLight3D releaseFlash;
    float elapsed;
    const float Duration = 0.36f;

    public PooledBombBayRelease()
    {
        Name = "PooledBombBayRelease";
        vaporRing = new MeshInstance3D
        {
            Name = "VaporRing",
            Mesh = new PlaneMesh { Size = new Vector2(2.6f, 2.6f) },
            MaterialOverride = CombatProjectile.CreateShockwaveMaterial(new Color(0.92f, 0.95f, 1.0f, 0.75f))
        };
        AddChild(vaporRing);

        slipstream = new CpuParticles3D
        {
            Name = "BaySlipstream",
            Amount = 14,
            Lifetime = 0.32f,
            OneShot = true,
            Explosiveness = 0.92f,
            Spread = 22f,
            Gravity = new Vector3(0f, 0.2f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.35f,
            ScaleAmountMax = 0.95f,
            Mesh = new QuadMesh { Size = new Vector2(0.65f, 0.65f) },
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.95f, 0.96f, 0.98f, 0.65f), true, false)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.4f));
        sCurve.AddPoint(new Vector2(0.5f, 1.2f));
        sCurve.AddPoint(new Vector2(1f, 0.1f));
        slipstream.ScaleAmountCurve = sCurve;
        slipstream.ColorRamp = CombatProjectile.CreateRamp(new Color(0.95f, 0.96f, 0.98f, 0.70f), 0.5f, new Color(0.88f, 0.92f, 0.96f, 0.35f), new Color(0.80f, 0.85f, 0.90f, 0f));
        AddChild(slipstream);

        releaseFlash = new OmniLight3D
        {
            Name = "ReleaseFlash",
            LightColor = new Color(1.0f, 0.75f, 0.35f),
            LightEnergy = 2.4f,
            OmniRange = 6.0f
        };
        AddChild(releaseFlash);
    }

    public void Activate(Vector3 pos, Vector3 forward)
    {
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;

        vaporRing.Scale = Vector3.One * 0.3f;
        vaporRing.Transparency = 0.25f;

        releaseFlash.Visible = true;
        releaseFlash.LightEnergy = 2.4f;

        slipstream.Direction = (-forward * 1.5f + Vector3.Down * 0.8f).Normalized();
        slipstream.Restart();
        slipstream.Emitting = true;

        BattleFeedback.BombRelease(pos);
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;
        float t = elapsed / Duration;

        vaporRing.Scale = Vector3.One * Mathf.Lerp(0.3f, 3.2f, t);
        vaporRing.Transparency = Mathf.Lerp(0.25f, 1f, t);

        if (elapsed <= 0.14f)
            releaseFlash.LightEnergy = Mathf.Lerp(2.4f, 0f, elapsed / 0.14f);
        else
            releaseFlash.Visible = false;

        if (elapsed >= Duration)
        {
            Visible = false;
            slipstream.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledMuzzleFlash : Node3D
{
    readonly OmniLight3D light;
    readonly CpuParticles3D fire;
    readonly CpuParticles3D brakeLeft;
    readonly CpuParticles3D brakeRight;
    readonly CpuParticles3D groundDust;
    readonly CpuParticles3D smoke;
    readonly MeshInstance3D flashBillboard;
    float elapsed;
    float flashBillboardScale;
    bool currentIsBullet;
    const float Duration = 0.65f;

    static StandardMaterial3D? bulletFlashMat;
    static StandardMaterial3D? mortarFlashMat;
    static StandardMaterial3D? flameFlashMat;
    static StandardMaterial3D? cannonFlashMat;

    static StandardMaterial3D GetBulletFlashMaterial()
        => bulletFlashMat ??= CombatProjectile.CreateParticleMaterial(new Color(3.6f, 3.0f, 1.8f, 1.0f), true, true);
    static StandardMaterial3D GetMortarFlashMaterial()
        => mortarFlashMat ??= CombatProjectile.CreateParticleMaterial(new Color(3.4f, 2.6f, 1.4f, 1.0f), true, true);
    static StandardMaterial3D GetFlameFlashMaterial()
        => flameFlashMat ??= CombatProjectile.CreateParticleMaterial(new Color(3.2f, 1.2f, 0.2f, 1.0f), true, true);
    static StandardMaterial3D GetCannonFlashMaterial()
        => cannonFlashMat ??= CombatProjectile.CreateParticleMaterial(new Color(3.2f, 2.4f, 1.2f, 1.0f), true, true);

    public PooledMuzzleFlash()
    {
        Name = "PooledMuzzleFlash";
        var particleQuad = new QuadMesh { Size = new Vector2(0.5f, 0.5f) };

        light = new OmniLight3D
        {
            Name = "MuzzleLight",
            LightEnergy = 4.5f,
            OmniRange = 10.0f
        };
        AddChild(light);

        // 🌟 瞬间超高亮枪口/炮口十字星芒光晕 (Billboard Flash Bloom)
        flashBillboard = new MeshInstance3D
        {
            Name = "FlashBillboard",
            Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
            MaterialOverride = GetCannonFlashMaterial(),
            Visible = false
        };
        AddChild(flashBillboard);

        // 1. 中心穿刺高压火舌 (Central Hypervelocity Flame Dart & Sparks)
        fire = new CpuParticles3D
        {
            Name = "MuzzleFire",
            Amount = 22,
            Lifetime = 0.12f,
            OneShot = true,
            Explosiveness = 0.96f,
            Spread = 22f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 6.0f,
            InitialVelocityMax = 14.0f,
            ScaleAmountMin = 0.35f,
            ScaleAmountMax = 1.65f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        var fCurve = new Curve();
        fCurve.AddPoint(new Vector2(0f, 1f));
        fCurve.AddPoint(new Vector2(1f, 0.05f));
        fire.ScaleAmountCurve = fCurve;
        fire.ColorRamp = CombatProjectile.CreateRamp4(
            new Color(2.8f, 2.4f, 1.5f, 1f), 0.2f,
            new Color(2.0f, 1.2f, 0.15f, 1f), 0.5f,
            new Color(1.4f, 0.4f, 0.03f, 0.8f),
            new Color(0.2f, 0.02f, 0f, 0f));
        AddChild(fire);

        // 2. 双侧炮口制退器侧喷高压羽流 (Left & Right Muzzle Brake Jets)
        brakeLeft = new CpuParticles3D
        {
            Name = "BrakeLeft",
            Amount = 14,
            Lifetime = 0.09f,
            OneShot = true,
            Explosiveness = 0.96f,
            Spread = 16f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 5.5f,
            InitialVelocityMax = 11.5f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.85f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true),
            ScaleAmountCurve = fCurve,
            ColorRamp = CombatProjectile.CreateRamp(new Color(2.8f, 2.2f, 1.2f, 1f), 0.4f, new Color(1.8f, 0.65f, 0.1f, 0.85f), new Color(0.4f, 0.1f, 0.02f, 0f))
        };
        AddChild(brakeLeft);

        brakeRight = new CpuParticles3D
        {
            Name = "BrakeRight",
            Amount = 14,
            Lifetime = 0.09f,
            OneShot = true,
            Explosiveness = 0.96f,
            Spread = 16f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 5.5f,
            InitialVelocityMax = 11.5f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.85f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true),
            ScaleAmountCurve = fCurve,
            ColorRamp = CombatProjectile.CreateRamp(new Color(2.8f, 2.2f, 1.2f, 1f), 0.4f, new Color(1.8f, 0.65f, 0.1f, 0.85f), new Color(0.4f, 0.1f, 0.02f, 0f))
        };
        AddChild(brakeRight);

        // 3. 近地爆震翻滚扬尘浪 (Ground Concussion Dust Blast)
        groundDust = new CpuParticles3D
        {
            Name = "GroundDust",
            Amount = 24,
            Lifetime = 0.65f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = Vector3.Up,
            Spread = 85f,
            Gravity = new Vector3(0f, -2.5f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 6.5f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 2.2f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.72f, 0.65f, 0.52f, 0.65f), true, false)
        };
        var dCurve = new Curve();
        dCurve.AddPoint(new Vector2(0f, 0.4f));
        dCurve.AddPoint(new Vector2(0.3f, 1.8f));
        dCurve.AddPoint(new Vector2(1f, 3.2f));
        groundDust.ScaleAmountCurve = dCurve;
        groundDust.ColorRamp = CombatProjectile.CreateRamp(new Color(0.72f, 0.65f, 0.52f, 0.65f), 0.4f, new Color(0.65f, 0.58f, 0.46f, 0.35f), new Color(0.55f, 0.48f, 0.38f, 0f));
        AddChild(groundDust);

        // 4. 炮口残余浓烟与炮管抽烟气环 (Bore Smoke)
        smoke = new CpuParticles3D
        {
            Name = "MuzzleSmoke",
            Amount = 20,
            Lifetime = 0.75f,
            OneShot = true,
            Explosiveness = 0.90f,
            Spread = 34f,
            Gravity = new Vector3(0f, 0.8f, 0f),
            InitialVelocityMin = 1.6f,
            InitialVelocityMax = 4.2f,
            ScaleAmountMin = 0.4f,
            ScaleAmountMax = 1.8f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.88f, 0.88f, 0.88f, 0.55f), true, false)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.5f));
        sCurve.AddPoint(new Vector2(1f, 2.4f));
        smoke.ScaleAmountCurve = sCurve;
        smoke.ColorRamp = CombatProjectile.CreateRamp(new Color(0.88f, 0.88f, 0.88f, 0.60f), 0.5f, new Color(0.80f, 0.80f, 0.82f, 0.30f), new Color(0.75f, 0.75f, 0.78f, 0f));
        AddChild(smoke);
    }

    public void Activate(Vector3 pos, Vector3 direction, Color color, string weaponProfile)
    {
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;

        bool isBullet = weaponProfile is "rifle" or "machinegun";
        bool isMortar = weaponProfile is "mortar" or "infantry_artillery";
        bool isFlame = weaponProfile is "flame" or "infantry_flamethrower";
        bool isCannon = !isBullet && !isMortar && !isFlame;
        currentIsBullet = isBullet;

        var normalizedDir = direction.Normalized();
        if (normalizedDir.LengthSquared() < 0.001f)
            normalizedDir = -GlobalTransform.Basis.Z;

        // 🌟 1. 枪口/炮口高亮瞬间闪光星芒 (Muzzle Flash Billboard Bloom)
        if (isBullet)
        {
            flashBillboard.MaterialOverride = GetBulletFlashMaterial();
            flashBillboardScale = 0.85f;
            flashBillboard.Scale = Vector3.One * flashBillboardScale;
            flashBillboard.Visible = true;
        }
        else if (isMortar)
        {
            flashBillboard.MaterialOverride = GetMortarFlashMaterial();
            flashBillboardScale = 1.55f;
            flashBillboard.Scale = Vector3.One * flashBillboardScale;
            flashBillboard.Visible = true;
        }
        else if (isFlame)
        {
            flashBillboard.MaterialOverride = GetFlameFlashMaterial();
            flashBillboardScale = 1.30f;
            flashBillboard.Scale = Vector3.One * flashBillboardScale;
            flashBillboard.Visible = true;
        }
        else
        {
            flashBillboard.MaterialOverride = GetCannonFlashMaterial();
            flashBillboardScale = 2.50f;
            flashBillboard.Scale = Vector3.One * flashBillboardScale;
            flashBillboard.Visible = true;
        }

        // 🌟 2. 枪口/炮口动态照亮光源 (Dynamic Muzzle Light)
        if (isBullet)
        {
            light.LightColor = color.R > 0.01f ? color : new Color(1.0f, 0.88f, 0.45f);
            light.LightEnergy = 4.8f;
            light.OmniRange = 6.8f;
        }
        else if (isMortar)
        {
            light.LightColor = new Color(1.0f, 0.82f, 0.40f);
            light.LightEnergy = 6.2f;
            light.OmniRange = 10.5f;
        }
        else if (isFlame)
        {
            light.LightColor = new Color(1.2f, 0.45f, 0.10f);
            light.LightEnergy = 5.2f;
            light.OmniRange = 8.0f;
        }
        else
        {
            light.LightColor = new Color(1.0f, 0.80f, 0.45f);
            light.LightEnergy = 6.8f;
            light.OmniRange = 14.0f;
        }
        light.Visible = true;

        // 🌟 3. 中心火舌/火星粒子 (Center Flame Dart & Sparks)
        if (isBullet)
        {
            fire.Direction = normalizedDir;
            fire.Amount = 14;
            fire.Lifetime = 0.10f;
            fire.Spread = 30f;
            fire.InitialVelocityMin = 1.2f;
            fire.InitialVelocityMax = 3.6f;
            fire.ScaleAmountMin = 0.35f;
            fire.ScaleAmountMax = 0.75f;
        }
        else if (isMortar)
        {
            var mortarDir = (normalizedDir * 0.6f + Vector3.Up * 0.8f).Normalized();
            fire.Direction = mortarDir;
            fire.Amount = 24;
            fire.Lifetime = 0.16f;
            fire.Spread = 28f;
            fire.InitialVelocityMin = 3.5f;
            fire.InitialVelocityMax = 8.5f;
            fire.ScaleAmountMin = 0.5f;
            fire.ScaleAmountMax = 1.6f;
        }
        else if (isFlame)
        {
            fire.Direction = normalizedDir;
            fire.Amount = 26;
            fire.Lifetime = 0.22f;
            fire.Spread = 20f;
            fire.InitialVelocityMin = 2.5f;
            fire.InitialVelocityMax = 5.5f;
            fire.ScaleAmountMin = 0.4f;
            fire.ScaleAmountMax = 1.35f;
        }
        else
        {
            fire.Direction = normalizedDir;
            fire.Amount = 28;
            fire.Lifetime = 0.14f;
            fire.Spread = 18f;
            fire.InitialVelocityMin = 6.0f;
            fire.InitialVelocityMax = 14.0f;
            fire.ScaleAmountMin = 0.45f;
            fire.ScaleAmountMax = 1.85f;
        }
        fire.Restart();
        fire.Emitting = true;

        // 🌟 4. 双侧炮口制退器侧喷排气 (Muzzle Brake Lateral Jets - 仅重装战车与大口径火炮)
        if (isCannon)
        {
            var rightVec = normalizedDir.Cross(Vector3.Up).Normalized();
            if (rightVec.LengthSquared() < 0.001f) rightVec = Vector3.Right;

            brakeLeft.Direction = (-rightVec * 0.85f + normalizedDir * 0.25f).Normalized();
            brakeLeft.Restart();
            brakeLeft.Emitting = true;

            brakeRight.Direction = (rightVec * 0.85f + normalizedDir * 0.25f).Normalized();
            brakeRight.Restart();
            brakeRight.Emitting = true;
        }
        else
        {
            brakeLeft.Emitting = false;
            brakeRight.Emitting = false;
        }

        // 🌟 5. 地面超压冲击扬尘 (Ground Concussion Dust)
        if ((isCannon || isMortar) && pos.Y < 3.2f)
        {
            groundDust.Position = new Vector3(0f, -pos.Y + 0.08f, 0f);
            groundDust.Restart();
            groundDust.Emitting = true;
        }
        else
        {
            groundDust.Emitting = false;
        }

        // 🌟 6. 枪口/炮口出膛硝烟 (Muzzle & Bore Smoke)
        if (isBullet)
        {
            smoke.Direction = normalizedDir + Vector3.Up * 0.45f;
            smoke.Amount = 8;
            smoke.Lifetime = 0.42f;
            smoke.Spread = 32f;
            smoke.InitialVelocityMin = 0.6f;
            smoke.InitialVelocityMax = 2.0f;
            smoke.ScaleAmountMin = 0.35f;
            smoke.ScaleAmountMax = 0.90f;
        }
        else if (isMortar)
        {
            var mortarDir = (normalizedDir * 0.6f + Vector3.Up * 0.8f).Normalized();
            smoke.Direction = mortarDir;
            smoke.Amount = 24;
            smoke.Lifetime = 0.85f;
            smoke.Spread = 38f;
            smoke.InitialVelocityMin = 1.8f;
            smoke.InitialVelocityMax = 4.8f;
            smoke.ScaleAmountMin = 0.6f;
            smoke.ScaleAmountMax = 2.2f;
        }
        else if (isFlame)
        {
            smoke.Direction = normalizedDir + Vector3.Up * 0.35f;
            smoke.Amount = 14;
            smoke.Lifetime = 0.55f;
            smoke.Spread = 28f;
            smoke.InitialVelocityMin = 1.2f;
            smoke.InitialVelocityMax = 3.2f;
            smoke.ScaleAmountMin = 0.4f;
            smoke.ScaleAmountMax = 1.3f;
        }
        else
        {
            smoke.Direction = normalizedDir + Vector3.Up * 0.35f;
            smoke.Amount = 24;
            smoke.Lifetime = 0.85f;
            smoke.Spread = 34f;
            smoke.InitialVelocityMin = 1.6f;
            smoke.InitialVelocityMax = 4.2f;
            smoke.ScaleAmountMin = 0.5f;
            smoke.ScaleAmountMax = 2.2f;
        }
        smoke.Restart();
        smoke.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;

        if (flashBillboardScale > 0f)
        {
            flashBillboardScale = Mathf.Max(0f, flashBillboardScale - dt * 11.0f);
            flashBillboard.Scale = Vector3.One * flashBillboardScale;
            if (flashBillboardScale <= 0.03f)
            {
                flashBillboard.Visible = false;
                flashBillboardScale = 0f;
            }
        }

        float lightFadeSpeed = currentIsBullet ? 32f : 24f;
        light.LightEnergy = Mathf.Max(0f, light.LightEnergy - dt * lightFadeSpeed);

        if (elapsed >= Duration)
        {
            Visible = false;
            light.Visible = false;
            flashBillboard.Visible = false;
            flashBillboardScale = 0f;
            fire.Emitting = false;
            brakeLeft.Emitting = false;
            brakeRight.Emitting = false;
            groundDust.Emitting = false;
            smoke.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledWaterSpout : Node3D
{
    readonly CpuParticles3D spout;
    readonly CpuParticles3D splashRing;
    readonly MeshInstance3D waterShockwave;
    readonly OmniLight3D flash;
    float elapsed;
    const float Duration = 1.2f;

    public PooledWaterSpout()
    {
        Name = "PooledWaterSpout";
        var particleQuad = new QuadMesh { Size = new Vector2(1.2f, 1.2f) };

        spout = new CpuParticles3D
        {
            Name = "WaterSpoutGeyser",
            Amount = 55,
            Lifetime = 1.05f,
            OneShot = true,
            Explosiveness = 0.96f,
            Direction = Vector3.Up,
            Spread = 16f,
            Gravity = new Vector3(0f, -14.0f, 0f),
            InitialVelocityMin = 10.0f,
            InitialVelocityMax = 15.0f,
            ScaleAmountMin = 0.45f,
            ScaleAmountMax = 1.6f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.92f, 0.96f, 1.0f, 0.90f), true, false)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.4f));
        sCurve.AddPoint(new Vector2(0.35f, 1.8f));
        sCurve.AddPoint(new Vector2(1f, 0.2f));
        spout.ScaleAmountCurve = sCurve;
        AddChild(spout);

        splashRing = new CpuParticles3D
        {
            Name = "WaterSplashRing",
            Amount = 38,
            Lifetime = 0.80f,
            OneShot = true,
            Explosiveness = 0.92f,
            Direction = Vector3.Up,
            Spread = 85f,
            Gravity = new Vector3(0f, -9.0f, 0f),
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 9.5f,
            ScaleAmountMin = 0.50f,
            ScaleAmountMax = 1.35f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.90f, 0.95f, 1.0f, 0.80f), true, false),
            ScaleAmountCurve = sCurve
        };
        AddChild(splashRing);

        waterShockwave = new MeshInstance3D
        {
            Name = "WaterShockwaveRing",
            Mesh = new PlaneMesh { Size = new Vector2(7.5f, 7.5f) },
            MaterialOverride = CombatProjectile.CreateShockwaveMaterial(new Color(0.85f, 0.95f, 1.0f, 0.90f))
        };
        AddChild(waterShockwave);

        flash = new OmniLight3D
        {
            Name = "UnderwaterFlash",
            LightColor = new Color(0.55f, 0.85f, 1.0f),
            LightEnergy = 3.5f,
            OmniRange = 14.0f
        };
        AddChild(flash);
    }

    public void Activate(Vector3 pos, bool heavy)
    {
        GlobalPosition = new Vector3(pos.X, 0.02f, pos.Z);
        Visible = true;
        elapsed = 0f;

        waterShockwave.Visible = true;
        waterShockwave.Scale = Vector3.One * 0.1f;
        waterShockwave.Transparency = 0.1f;

        flash.Visible = true;
        flash.LightEnergy = heavy ? 4.0f : 2.5f;

        spout.Amount = heavy ? 65 : 40;
        spout.Restart();
        spout.Emitting = true;

        splashRing.Amount = heavy ? 45 : 28;
        splashRing.Restart();
        splashRing.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;

        if (elapsed <= 0.48f)
        {
            float t = elapsed / 0.48f;
            waterShockwave.Scale = Vector3.One * Mathf.Lerp(0.1f, 1.85f, t);
            waterShockwave.Transparency = t;
        }
        else
        {
            waterShockwave.Visible = false;
        }

        if (elapsed <= 0.28f)
            flash.LightEnergy = Mathf.Lerp(flash.LightEnergy, 0f, dt * 10f);
        else
            flash.Visible = false;

        if (elapsed >= Duration)
        {
            Visible = false;
            spout.Emitting = false;
            splashRing.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledImpactWave : Node3D
{
    readonly MeshInstance3D shockwave;
    readonly OmniLight3D flash;
    float elapsed;
    const float Duration = 0.25f;

    public PooledImpactWave()
    {
        Name = "PooledImpactWave";
        shockwave = new MeshInstance3D
        {
            Name = "ShockwaveRing",
            Mesh = new PlaneMesh { Size = new Vector2(2.0f, 2.0f) },
            MaterialOverride = CombatProjectile.CreateShockwaveMaterial(new Color(0.92f, 0.88f, 0.80f, 0.45f))
        };
        AddChild(shockwave);

        flash = new OmniLight3D
        {
            Name = "ImpactFlashLight",
            LightEnergy = 1.8f,
            OmniRange = 5.0f
        };
        AddChild(flash);
    }

    public void Activate(Vector3 pos, Color color, float radius)
    {
        GlobalPosition = pos + Vector3.Up * 0.05f;
        Visible = true;
        elapsed = 0f;

        shockwave.Scale = Vector3.One * 0.15f;
        shockwave.Transparency = 0.15f;

        flash.Visible = true;
        flash.LightColor = color;
        flash.LightEnergy = 1.8f;
        flash.OmniRange = Mathf.Max(3.5f, radius * 3.0f);
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;
        float t = elapsed / Duration;

        shockwave.Scale = Vector3.One * Mathf.Lerp(0.15f, 1.55f, t);
        shockwave.Transparency = t;
        flash.LightEnergy = Mathf.Lerp(1.8f, 0f, t);

        if (elapsed >= Duration)
        {
            Visible = false;
            flash.Visible = false;
            return true;
        }
        return false;
    }
}

public partial class PooledBulletImpact : Node3D
{
    readonly CpuParticles3D spall;
    readonly CpuParticles3D sparks;
    readonly CpuParticles3D dust;
    readonly OmniLight3D flash;
    float elapsed;
    const float Duration = 0.45f;

    public PooledBulletImpact()
    {
        Name = "PooledBulletImpact";
        var particleQuad = new QuadMesh { Size = new Vector2(0.5f, 0.5f) };

        // 1. 穿甲击穿/跳弹高温金属破片溅射流 (Molten Armor Spall Spray)
        spall = new CpuParticles3D
        {
            Name = "ArmorSpall",
            Amount = 18,
            Lifetime = 0.28f,
            OneShot = true,
            Explosiveness = 0.98f,
            Spread = 38f,
            Gravity = new Vector3(0f, -14.0f, 0f),
            InitialVelocityMin = 8.5f,
            InitialVelocityMax = 18.0f,
            ScaleAmountMin = 0.06f,
            ScaleAmountMax = 0.20f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        var spallCurve = new Curve();
        spallCurve.AddPoint(new Vector2(0f, 1f));
        spallCurve.AddPoint(new Vector2(1f, 0.08f));
        spall.ScaleAmountCurve = spallCurve;
        spall.ColorRamp = CombatProjectile.CreateRamp4(
            new Color(2.8f, 2.5f, 1.4f, 1f), 0.25f,
            new Color(2.2f, 1.1f, 0.18f, 0.95f), 0.65f,
            new Color(1.4f, 0.25f, 0.04f, 0.70f),
            new Color(0.4f, 0.05f, 0.01f, 0f));
        AddChild(spall);

        // 2. 动能擦碰四散高亮火星 (Ricochet Sparks)
        sparks = new CpuParticles3D
        {
            Name = "RicochetSparks",
            Amount = 16,
            Lifetime = 0.25f,
            OneShot = true,
            Explosiveness = 0.98f,
            Spread = 62f,
            Gravity = new Vector3(0f, -10.5f, 0f),
            InitialVelocityMin = 6.0f,
            InitialVelocityMax = 12.5f,
            ScaleAmountMin = 0.07f,
            ScaleAmountMax = 0.22f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 1f));
        sCurve.AddPoint(new Vector2(1f, 0.1f));
        sparks.ScaleAmountCurve = sCurve;
        sparks.ColorRamp = CombatProjectile.CreateParticleMaterialRamp(new Color(2.6f, 2.3f, 1.3f, 1f), 0.4f, new Color(2.0f, 0.85f, 0.15f, 0.9f), new Color(0.9f, 0.15f, 0.02f, 0f));
        AddChild(sparks);

        // 3. 水泥地面/泥土微细碎屑与粉尘烟雾 (Concrete/Earth Debris & Dust)
        dust = new CpuParticles3D
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
            ScaleAmountMax = 0.65f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.80f, 0.78f, 0.74f, 0.65f), true)
        };
        var dCurve = new Curve();
        dCurve.AddPoint(new Vector2(0f, 0.35f));
        dCurve.AddPoint(new Vector2(0.4f, 1.2f));
        dCurve.AddPoint(new Vector2(1f, 1.8f));
        dust.ScaleAmountCurve = dCurve;
        dust.ColorRamp = CombatProjectile.CreateRamp(new Color(0.82f, 0.80f, 0.76f, 0.65f), 0.45f, new Color(0.68f, 0.66f, 0.62f, 0.35f), new Color(0.55f, 0.54f, 0.50f, 0f));
        AddChild(dust);

        // 4. 撞击瞬间超高速发光闪烁 (Impact Micro-Flash)
        flash = new OmniLight3D
        {
            Name = "ImpactFlash",
            LightEnergy = 2.4f,
            OmniRange = 4.2f
        };
        AddChild(flash);
    }

    public void Activate(Vector3 pos, Color color, Vector3 flightDir)
    {
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;

        var ricochetDir = (-flightDir + Vector3.Up * 0.85f).Normalized();
        spall.Direction = ricochetDir;
        spall.Restart();
        spall.Emitting = true;

        sparks.Direction = ricochetDir;
        sparks.Restart();
        sparks.Emitting = true;

        dust.Restart();
        dust.Emitting = true;

        flash.Visible = true;
        flash.LightColor = color;
        flash.LightEnergy = 2.4f;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;
        if (elapsed <= 0.06f)
            flash.LightEnergy = Mathf.Lerp(2.4f, 0f, elapsed / 0.06f);
        else
            flash.Visible = false;

        if (elapsed >= Duration)
        {
            Visible = false;
            spall.Emitting = false;
            sparks.Emitting = false;
            dust.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledFootstepDust : Node3D
{
    readonly CpuParticles3D dust;
    float elapsed;
    const float Duration = 0.38f;

    public PooledFootstepDust()
    {
        Name = "PooledFootstepDust";
        dust = new CpuParticles3D
        {
            Name = "Puff",
            Amount = 4,
            Lifetime = 0.38f,
            OneShot = true,
            Explosiveness = 0.88f,
            Spread = 40f,
            Gravity = new Vector3(0f, 0.25f, 0f),
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 0.55f,
            ScaleAmountMin = 0.12f,
            ScaleAmountMax = 0.30f,
            Mesh = new QuadMesh { Size = new Vector2(0.35f, 0.35f) },
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.78f, 0.73f, 0.60f, 0.32f), true)
        };

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 0.35f));
        scaleCurve.AddPoint(new Vector2(1f, 1.8f));
        dust.ScaleAmountCurve = scaleCurve;
        dust.ColorRamp = CombatProjectile.CreateParticleMaterialRamp(new Color(0.80f, 0.75f, 0.62f, 0.28f), 0.5f, new Color(0.76f, 0.71f, 0.58f, 0.16f), new Color(0.72f, 0.67f, 0.55f, 0f));
        AddChild(dust);
    }

    public void Activate(Vector3 pos)
    {
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;

        dust.Direction = Vector3.Up + new Vector3((float)GD.RandRange(-0.35, 0.35), 0.25f, (float)GD.RandRange(-0.35, 0.35)).Normalized();
        dust.Restart();
        dust.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;
        if (elapsed >= Duration)
        {
            Visible = false;
            dust.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledBackblast : Node3D
{
    readonly CpuParticles3D flame;
    readonly CpuParticles3D smoke;
    float elapsed;
    const float Duration = 0.45f;

    public PooledBackblast()
    {
        Name = "PooledBackblast";
        var particleQuad = new QuadMesh { Size = new Vector2(0.5f, 0.5f) };

        flame = new CpuParticles3D
        {
            Name = "BackFlame",
            Amount = 14,
            Lifetime = 0.12f,
            OneShot = true,
            Explosiveness = 0.95f,
            Spread = 22f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 5.5f,
            InitialVelocityMax = 9.5f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.65f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        flame.ColorRamp = CombatProjectile.CreateParticleMaterialRamp(new Color(2.0f, 1.4f, 0.4f, 1f), 0.4f, new Color(1.8f, 0.45f, 0.05f, 0.85f), new Color(0.6f, 0.1f, 0.01f, 0f));
        AddChild(flame);

        smoke = new CpuParticles3D
        {
            Name = "BackSmoke",
            Amount = 18,
            Lifetime = 0.45f,
            OneShot = true,
            Explosiveness = 0.90f,
            Spread = 38f,
            Gravity = new Vector3(0f, 0.8f, 0f),
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.35f,
            ScaleAmountMax = 1.15f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.88f, 0.88f, 0.88f, 0.65f), true)
        };
        AddChild(smoke);
    }

    public void Activate(Vector3 pos, Vector3 forwardDir)
    {
        var backDir = -forwardDir.Normalized();
        GlobalPosition = pos + backDir * 0.65f + Vector3.Up * 0.35f;
        Visible = true;
        elapsed = 0f;

        flame.Direction = backDir;
        flame.Restart();
        flame.Emitting = true;

        smoke.Direction = backDir + Vector3.Up * 0.2f;
        smoke.Restart();
        smoke.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;
        if (elapsed >= Duration)
        {
            Visible = false;
            flame.Emitting = false;
            smoke.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledFlakAirburst : Node3D
{
    readonly CpuParticles3D fireball;
    readonly CpuParticles3D flakSmoke;
    readonly CpuParticles3D shrapnel;
    readonly MeshInstance3D shockwave;
    readonly OmniLight3D flashLight;
    float elapsed;
    const float Duration = 0.48f;

    public PooledFlakAirburst()
    {
        Name = "PooledFlakAirburst";
        var particleQuad = new QuadMesh { Size = new Vector2(0.6f, 0.6f) };

        fireball = new CpuParticles3D
        {
            Name = "FlakFireball",
            Amount = 14,
            Lifetime = 0.12f,
            OneShot = true,
            Explosiveness = 0.98f,
            Spread = 85f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 11.0f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.75f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, false)
        };
        var fCurve = new Curve();
        fCurve.AddPoint(new Vector2(0f, 0.4f));
        fCurve.AddPoint(new Vector2(0.2f, 1.2f));
        fCurve.AddPoint(new Vector2(1f, 0.1f));
        fireball.ScaleAmountCurve = fCurve;
        fireball.ColorRamp = CombatProjectile.CreateRamp4(
            new Color(1.0f, 0.72f, 0.16f, 1f), 0.2f,
            new Color(0.95f, 0.38f, 0.03f, 0.95f), 0.6f,
            new Color(0.70f, 0.12f, 0.01f, 0.6f),
            new Color(0.15f, 0.05f, 0.02f, 0f));
        AddChild(fireball);

        flakSmoke = new CpuParticles3D
        {
            Name = "FlakCarbonPuff",
            Amount = 22,
            Lifetime = 0.42f,
            OneShot = true,
            Explosiveness = 0.94f,
            Spread = 70f,
            Gravity = new Vector3(0f, 0.3f, 0f),
            InitialVelocityMin = 2.0f,
            InitialVelocityMax = 5.5f,
            ScaleAmountMin = 0.45f,
            ScaleAmountMax = 1.6f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.22f, 0.22f, 0.24f, 0.85f), true, false)
        };
        var sCurve = new Curve();
        sCurve.AddPoint(new Vector2(0f, 0.35f));
        sCurve.AddPoint(new Vector2(0.3f, 1.4f));
        sCurve.AddPoint(new Vector2(1f, 2.2f));
        flakSmoke.ScaleAmountCurve = sCurve;
        flakSmoke.ColorRamp = CombatProjectile.CreateRamp(
            new Color(0.24f, 0.24f, 0.25f, 0.85f), 0.45f,
            new Color(0.18f, 0.18f, 0.20f, 0.45f),
            new Color(0.12f, 0.12f, 0.14f, 0f));
        AddChild(flakSmoke);

        shrapnel = new CpuParticles3D
        {
            Name = "FlakShrapnel",
            Amount = 20,
            Lifetime = 0.18f,
            OneShot = true,
            Explosiveness = 0.98f,
            Spread = 88f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 12.0f,
            InitialVelocityMax = 22.0f,
            ScaleAmountMin = 0.08f,
            ScaleAmountMax = 0.22f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(Colors.White, true, true)
        };
        shrapnel.ColorRamp = CombatProjectile.CreateRamp(
            new Color(1.0f, 0.65f, 0.10f, 1f), 0.4f,
            new Color(0.95f, 0.30f, 0.02f, 0.85f),
            new Color(0.40f, 0.05f, 0f, 0f));
        AddChild(shrapnel);

        shockwave = new MeshInstance3D
        {
            Name = "FlakShockwave",
            Mesh = new PlaneMesh { Size = new Vector2(2.4f, 2.4f) },
            MaterialOverride = CombatProjectile.CreateShockwaveMaterial(new Color(1.0f, 0.85f, 0.45f, 0.80f))
        };
        AddChild(shockwave);

        flashLight = new OmniLight3D
        {
            Name = "FlakFlashLight",
            LightColor = new Color(1.0f, 0.88f, 0.55f),
            LightEnergy = 4.8f,
            OmniRange = 8.5f
        };
        AddChild(flashLight);
    }

    public void Activate(Vector3 pos, Color color)
    {
        GlobalPosition = pos;
        Visible = true;
        elapsed = 0f;

        shockwave.Visible = true;
        shockwave.Scale = Vector3.One * 0.2f;
        shockwave.Transparency = 0.1f;

        flashLight.Visible = true;
        flashLight.LightEnergy = 4.8f;

        fireball.Restart();
        fireball.Emitting = true;

        flakSmoke.Restart();
        flakSmoke.Emitting = true;

        shrapnel.Restart();
        shrapnel.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;

        if (elapsed <= 0.22f)
        {
            float t = elapsed / 0.22f;
            shockwave.Scale = Vector3.One * Mathf.Lerp(0.2f, 1.6f, t);
            shockwave.Transparency = t;
        }
        else
        {
            shockwave.Visible = false;
        }

        if (elapsed <= 0.15f)
        {
            flashLight.LightEnergy = Mathf.Lerp(4.8f, 0f, elapsed / 0.15f);
        }
        else
        {
            flashLight.Visible = false;
        }

        if (elapsed >= Duration)
        {
            Visible = false;
            fireball.Emitting = false;
            flakSmoke.Emitting = false;
            shrapnel.Emitting = false;
            return true;
        }
        return false;
    }
}

public partial class PooledCatapultSteam : Node3D
{
    readonly CpuParticles3D steam;
    float elapsed;
    const float Duration = 0.95f;

    public PooledCatapultSteam()
    {
        Name = "PooledCatapultSteam";
        var particleQuad = new QuadMesh { Size = new Vector2(0.9f, 0.9f) };

        steam = new CpuParticles3D
        {
            Name = "CatapultSteamParticles",
            Amount = 28,
            Lifetime = 0.80f,
            OneShot = true,
            Explosiveness = 0.88f,
            Spread = 22f,
            Gravity = new Vector3(0f, 0.45f, 0f),
            InitialVelocityMin = 6.0f,
            InitialVelocityMax = 11.5f,
            ScaleAmountMin = 0.45f,
            ScaleAmountMax = 2.1f,
            Mesh = particleQuad,
            MaterialOverride = CombatProjectile.CreateParticleMaterial(new Color(0.95f, 0.96f, 0.98f, 0.65f), true, false)
        };

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 0.4f));
        scaleCurve.AddPoint(new Vector2(0.4f, 1.6f));
        scaleCurve.AddPoint(new Vector2(1f, 2.5f));
        steam.ScaleAmountCurve = scaleCurve;

        steam.ColorRamp = CombatProjectile.CreateRamp(
            new Color(0.96f, 0.97f, 1.0f, 0.70f), 0.4f,
            new Color(0.90f, 0.93f, 0.96f, 0.35f),
            new Color(0.85f, 0.88f, 0.92f, 0f));
        AddChild(steam);
    }

    public void Activate(Vector3 bowPos, Vector3 forwardDir)
    {
        GlobalPosition = bowPos;
        Visible = true;
        elapsed = 0f;

        var dir = (forwardDir + new Vector3(0f, 0.30f, 0f)).Normalized();
        steam.Direction = dir;
        steam.Restart();
        steam.Emitting = true;
    }

    public bool UpdateTick(float dt)
    {
        elapsed += dt;
        if (elapsed >= Duration)
        {
            Visible = false;
            steam.Emitting = false;
            return true;
        }
        return false;
    }
}

#endregion
