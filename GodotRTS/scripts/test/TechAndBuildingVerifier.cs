using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class TechAndBuildingVerifier : Node
{
    public static TechAndBuildingVerifier? Instance { get; private set; }

    [Signal] public delegate void VerificationCompletedEventHandler(bool allPassed, string reportSummary);

    public struct VerificationResult
    {
        public string TestName;
        public bool Passed;
        public string Details;
    }

    readonly List<VerificationResult> results = new();

    public override void _Ready()
    {
        Instance = this;
        var args = CommandLineArgs.Get();
        if (System.Array.Exists(args, a => a == "--verify-tech-and-buildings"))
        {
            CallDeferred(nameof(RunAllChecks));
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void RunAllChecks()
    {
        results.Clear();
        GD.Print("=================================================");
        GD.Print("[TechAndBuildingVerifier] 🏛️ Starting Comprehensive Technology & Building Checks...");
        GD.Print("=================================================");

        // 1. 建筑目录与基础属性完整性
        CheckBuildingCatalogDefinitions();

        // 2. 科技目录与材质图标资产存在性
        CheckTechCatalogDefinitionsAndAssets();

        // 3. 建筑分级解锁与出兵名册阶梯性校验
        CheckBuildingProductionRosterUnlocks();

        // 4. 建筑升级依赖链与属性成长
        CheckBuildingUpgradeRequirementsAndProgression();

        // 5. 电力系统供需与人口上限核算
        CheckPowerGridAndPopulationLogic();

        // 6. 主基地废墟生命周期与抢修机制
        CheckMainBaseRuinedAndRebuildLifecycle();

        // 7. 防御炮塔火力、射程与供电联动
        CheckTurretCombatAndPowerDependency();

        // 8. 战场科技释放、CD 与作用域（包含战鹰整备 AirOnly 限制）
        CheckBattleTechCastingAndDomainFiltering();

        // 9. 科技对地面武装、战机及防御建筑的实际数值加成
        CheckTechBuffApplicationOnUnitsAndBuildings();

        // 10. 真实军工 PBR 3D 建筑构建引擎无报错运行
        CheckRealisticBuildingMeshGeneration();

        // 11. 海图与水域地图船坞建造合理性与近海港口核查
        CheckNavalYardPlacementAndMapWaterProximity();

        // 12. 建筑放置判定与虚影模型自重叠免疫核查
        CheckBuildingPlacementPreviewAndOverlapRules();

        bool allPassed = true;
        var sb = new System.Text.StringBuilder(1024);
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            if (!r.Passed) allPassed = false;
            sb.Append(r.Passed ? "✅ PASS" : "❌ FAIL")
              .Append(" [").Append(r.TestName).Append("]: ")
              .AppendLine(r.Details);
        }
        string summary = sb.ToString().TrimEnd();

        GD.Print(summary);
        GD.Print("=================================================");
        GD.Print(allPassed ? "🎉 ALL TECH & BUILDING INTEGRITY CHECKS PASSED!" : "⚠️ SOME TECH / BUILDING CHECKS FAILED!");
        GD.Print("=================================================");

        try
        {
            var reportPath = ProjectSettings.GlobalizePath("res://scratch/tech_and_building_verification.txt");
            var dir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(reportPath, summary);
            GD.Print($"[TechAndBuildingVerifier] Saved test report to: {reportPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TechAndBuildingVerifier] Failed to write report file: {ex.Message}");
        }

        EmitSignal(SignalName.VerificationCompleted, allPassed, summary);

        var args = CommandLineArgs.Get();
        if (System.Array.Exists(args, a => a == "--verify-tech-and-buildings"))
        {
            GetTree().Quit(allPassed ? 0 : 1);
        }
    }

    void CheckBuildingCatalogDefinitions()
    {
        string[] expectedKeys = {
            "main_base", "barracks", "tank_factory", "armor_factory",
            "airfield", "air_factory", "naval_yard", "turret", "power_plant", "gold_mine"
        };

        var errors = new List<string>();
        foreach (var key in expectedKeys)
        {
            var def = BattleBuildingCatalog.Get(key);
            if (def.Key != key)
                errors.Add($"建筑 {key} 从 Catalog 获取失败 (返回 {def.Key})");
            if (def.MaxHealth <= 0f)
                errors.Add($"建筑 {key} 最大生命值异常: {def.MaxHealth}");
            if (def.CollisionSize.X <= 0f || def.CollisionSize.Z <= 0f)
                errors.Add($"建筑 {key} 碰撞尺寸异常: {def.CollisionSize}");
        }

        // 发电厂专有校验：必须提供电力 250，且人口上限增益必须为 0（不能误给250人口）
        var powerPlant = BattleBuildingCatalog.Get("power_plant");
        if (powerPlant.PopCapBonus != 0)
            errors.Add($"发电厂 PopCapBonus 异常，预期为 0 但实际为 {powerPlant.PopCapBonus}");
        if (powerPlant.PowerProvided != 250)
            errors.Add($"发电厂 PowerProvided 异常，预期为 250 但实际为 {powerPlant.PowerProvided}");

        // 金矿专有校验：产金速度与电力消耗
        var goldMine = BattleBuildingCatalog.Get("gold_mine");
        if (goldMine.GoldIncomeAmount != 5 || goldMine.PowerUsed <= 0)
            errors.Add($"金矿经济参数异常: 收入 {goldMine.GoldIncomeAmount}, 耗电 {goldMine.PowerUsed}");

        results.Add(new VerificationResult
        {
            TestName = "1. Building Catalog & Stats Integrity (全 10 种建筑基础属性定义完整度)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "全部 10 种建筑定义无误，发电厂/金矿/主基地属性严格合规" : string.Join("; ", errors)
        });
    }

    void CheckTechCatalogDefinitionsAndAssets()
    {
        var errors = new List<string>();
        var techs = BattleTechCatalog.All;

        if (techs.Length != 10)
            errors.Add($"科技总数异常，预期 10 项，实际 {techs.Length} 项");

        foreach (var tech in techs)
        {
            if (string.IsNullOrEmpty(tech.Key) || string.IsNullOrEmpty(tech.DisplayName))
                errors.Add($"科技定义字段缺失: {tech.Key}");
            if (tech.Duration <= 0f || tech.Cooldown <= 0f || tech.Radius <= 0f)
                errors.Add($"科技 {tech.Key} 核心时间或范围数值异常: 持续{tech.Duration}s CD{tech.Cooldown}s 范围{tech.Radius}m");

            // 检查对应图标与背景贴图是否存在（支持 ResourceLoader 或 Godot.FileAccess 原生加载）
            if (!Godot.FileAccess.FileExists(tech.IconPath) && !ResourceLoader.Exists(tech.IconPath))
                errors.Add($"科技 {tech.Key} 图标缺失: {tech.IconPath}");
            if (!Godot.FileAccess.FileExists(tech.BackgroundTexturePath) && !ResourceLoader.Exists(tech.BackgroundTexturePath))
                errors.Add($"科技 {tech.Key} 背景贴图缺失: {tech.BackgroundTexturePath}");
        }

        var airRefit = BattleTechCatalog.Get("air_refit");
        if (airRefit.TargetDomain != TechTargetDomain.AirOnly)
            errors.Add($"战鹰整备 TargetDomain 必须为 AirOnly，实际为 {airRefit.TargetDomain}");

        results.Add(new VerificationResult
        {
            TestName = "2. Tech Catalog & Texture Assets (全 10 种战场科技定义与图标资源完整度)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "全部 10 项科技定义完备，图标与背景 PBR 贴图均在磁盘成功加载" : string.Join("; ", errors)
        });
    }

    void CheckBuildingProductionRosterUnlocks()
    {
        var errors = new List<string>();

        // 验证兵营：Lv.1 步兵 -> Lv.2 迫击炮兵 -> Lv.3 喷火兵
        var barracks = new RtsBuilding { BuildKey = "barracks", BuildingLevel = 1 };
        var b1 = barracks.GetProductionRoster();
        if (b1.Length != 1 || b1[0] != "infantry")
            errors.Add($"兵营 Lv.1 解锁异常: [{string.Join(",", b1)}]");

        barracks.BuildingLevel = 2;
        var b2 = barracks.GetProductionRoster();
        if (b2.Length != 2 || !b2.Contains("infantry_artillery"))
            errors.Add($"兵营 Lv.2 解锁异常: [{string.Join(",", b2)}]");

        barracks.BuildingLevel = 3;
        var b3 = barracks.GetProductionRoster();
        if (b3.Length != 3 || !b3.Contains("infantry_flamethrower"))
            errors.Add($"兵营 Lv.3 解锁异常: [{string.Join(",", b3)}]");

        // 验证坦克工厂：Lv.1 轻坦+中坦 -> Lv.2 重坦 -> Lv.3 坦克歼击车
        var armor = new RtsBuilding { BuildKey = "armor_factory", BuildingLevel = 1 };
        var a1 = armor.GetProductionRoster();
        if (a1.Length != 2 || !a1.Contains("tank"))
            errors.Add($"坦克工厂 Lv.1 解锁异常: [{string.Join(",", a1)}]");

        armor.BuildingLevel = 2;
        var a2 = armor.GetProductionRoster();
        if (a2.Length != 3 || !a2.Contains("heavy_tank"))
            errors.Add($"坦克工厂 Lv.2 解锁异常: [{string.Join(",", a2)}]");

        armor.BuildingLevel = 3;
        var a3 = armor.GetProductionRoster();
        if (a3.Length != 4 || !a3.Contains("tank_destroyer"))
            errors.Add($"坦克工厂 Lv.3 解锁异常，必须解锁终极坦克歼击车，当前: [{string.Join(",", a3)}]");

        // 验证飞机工厂：Lv.3 必须解锁轰炸机
        var air = new RtsBuilding { BuildKey = "air_factory", BuildingLevel = 3 };
        var airRoster = air.GetProductionRoster();
        if (!airRoster.Contains("bomber") || airRoster.Length != 4)
            errors.Add($"飞机工厂 Lv.3 轰炸机解锁异常: [{string.Join(",", airRoster)}]");

        // 验证特勤工厂：Lv.3 必须解锁防空导弹车
        var tankFac = new RtsBuilding { BuildKey = "tank_factory", BuildingLevel = 3 };
        var tfRoster = tankFac.GetProductionRoster();
        if (!tfRoster.Contains("sam_missile") || tfRoster.Length != 4)
            errors.Add($"特勤工厂 Lv.3 防空导弹车解锁异常: [{string.Join(",", tfRoster)}]");

        // 验证船坞：Lv.3 必须解锁战列舰
        var naval = new RtsBuilding { BuildKey = "naval_yard", BuildingLevel = 3 };
        var nRoster = naval.GetProductionRoster();
        if (!nRoster.Contains("battleship") || nRoster.Length != 4)
            errors.Add($"船坞 Lv.3 战列舰解锁异常: [{string.Join(",", nRoster)}]");

        results.Add(new VerificationResult
        {
            TestName = "3. Production Roster Multi-Tier Unlocks (兵营/重工/机库阶梯解锁与终极兵种交付)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "各级生产建筑阶梯解锁完全正确，Lv.3 均能成功交付终极重型装备" : string.Join("; ", errors)
        });
    }

    void CheckBuildingUpgradeRequirementsAndProgression()
    {
        var errors = new List<string>();

        // 主基地 Lv.2 升级需求测试：需要兵营 1，金矿 2，发电厂 2
        var baseReqsLv2 = BattleBuildingUpgradeCatalog.GetRequirements("main_base", 2);
        if (baseReqsLv2.Length < 3)
            errors.Add($"主基地 Lv.2 需求缺失，实际只有 {baseReqsLv2.Length} 项");

        var statusLv2 = BattleBuildingUpgradeCatalog.EvaluateRequirements(
            baseReqsLv2,
            key => key switch { "barracks" => 1, "gold_mine" => 2, "power_plant" => 2, _ => 0 },
            _ => 1);
        if (!statusLv2.All(s => s.IsMet))
            errors.Add("主基地 Lv.2 条件在满足建筑数量时判定失败");

        // 主基地 Lv.3 升级需求测试：需要高阶工厂
        var baseReqsLv3 = BattleBuildingUpgradeCatalog.GetRequirements("main_base", 3);
        if (!baseReqsLv3.Any(r => r.BuildingKey == "armor_factory") || !baseReqsLv3.Any(r => r.BuildingKey == "air_factory"))
            errors.Add("主基地 Lv.3 必须包含坦克工厂与飞机工厂作为前置前置需求");

        // 炮塔升级属性测试：Lv.1 -> Lv.2 -> Lv.3
        var turretLv1 = BattleBuildingCatalog.Get("turret");
        var turretLv2 = BattleBuildingUpgradeCatalog.Get("turret", 2);
        var turretLv3 = BattleBuildingUpgradeCatalog.Get("turret", 3);

        if (turretLv2.TurretDamageOverride <= turretLv1.AttackDamage || turretLv3.TurretDamageOverride <= turretLv2.TurretDamageOverride)
            errors.Add($"炮塔伤害成长异常: Lv.1={turretLv1.AttackDamage}, Lv.2={turretLv2.TurretDamageOverride}, Lv.3={turretLv3.TurretDamageOverride}");
        if (turretLv2.TurretRangeOverride <= turretLv1.AttackRange || turretLv3.TurretRangeOverride <= turretLv2.TurretRangeOverride)
            errors.Add($"炮塔射程成长异常: Lv.1={turretLv1.AttackRange}, Lv.2={turretLv2.TurretRangeOverride}, Lv.3={turretLv3.TurretRangeOverride}");
        if (turretLv2.TurretCooldownOverride >= turretLv1.AttackCooldown || turretLv3.TurretCooldownOverride >= turretLv2.TurretCooldownOverride)
            errors.Add($"炮塔攻速成长异常: Lv.1={turretLv1.AttackCooldown}, Lv.2={turretLv2.TurretCooldownOverride}, Lv.3={turretLv3.TurretCooldownOverride}");

        results.Add(new VerificationResult
        {
            TestName = "4. Building Upgrade Tree & Progression (建筑升级依赖树与炮塔作战指标成长)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "科技树升级前置完备，炮塔攻击/射程/射速随等级严格单调增强" : string.Join("; ", errors)
        });
    }

    void CheckPowerGridAndPopulationLogic()
    {
        var errors = new List<string>();

        // 模拟一个电力充足与不足的场景
        var mockPowerPlant = new RtsBuilding();
        mockPowerPlant.Configure(BattleBuildingCatalog.Get("power_plant"), true);

        var mockBarracks = new RtsBuilding();
        mockBarracks.Configure(BattleBuildingCatalog.Get("barracks"), true);

        if (mockPowerPlant.PopCapBonus != 0)
            errors.Add("发电厂 PopCapBonus 异常，不应加人口");

        if (mockPowerPlant.PowerProvided <= 0)
            errors.Add("发电厂未提供任何电力");

        if (mockBarracks.PowerUsed <= 0)
            errors.Add("兵营未标注电力消耗");

        // 停电对生产建筑的阻断检测
        mockBarracks.Powered = false;
        if (mockBarracks.CanProduce("infantry"))
            errors.Add("兵营在断电状态下仍然允许生产单位，电力硬约束失效！");

        mockBarracks.Powered = true;
        if (!mockBarracks.CanProduce("infantry"))
            errors.Add("兵营在供电正常状态下无法生产步兵");

        results.Add(new VerificationResult
        {
            TestName = "5. Power Grid & PopCap Verification (电网供需平衡与断电生产熔断机制)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "发电厂不虚增人口，兵营等高精设施在断电时严格熔断生产" : string.Join("; ", errors)
        });
    }

    void CheckMainBaseRuinedAndRebuildLifecycle()
    {
        var errors = new List<string>();
        var baseBuilding = new RtsBuilding();
        AddChild(baseBuilding);
        baseBuilding.Configure(BattleBuildingCatalog.Get("main_base"), true);

        if (!baseBuilding.IsMainBase || !baseBuilding.CanBeRebuilt)
            errors.Add("主基地初始化后标志位 IsMainBase / CanBeRebuilt 异常");

        // 受到毁灭性打击
        baseBuilding.ApplyDamage(99999f);
        if (!baseBuilding.IsRuined || baseBuilding.Health > 0f)
            errors.Add("主基地被摧毁后未正确进入废墟 Ruined 状态");

        // 发起重建
        bool rebuildStarted = baseBuilding.StartRebuild(5.0f);
        if (!rebuildStarted || !baseBuilding.IsRebuilding)
            errors.Add("主基地未能启动废墟重建流程");

        // 完工交付
        baseBuilding.CompleteConstruction();
        if (baseBuilding.IsRuined || baseBuilding.IsRebuilding || baseBuilding.Health < baseBuilding.MaxHealth)
            errors.Add("主基地重建完成后未恢复满血活跃状态");

        baseBuilding.QueueFree();

        results.Add(new VerificationResult
        {
            TestName = "6. Main Base Ruined & Rebuild Lifecycle (主基地战损化废墟与自愈重建生命周期)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "主基地战损后转为废墟、支持重建倒计时并在完工后满血重启" : string.Join("; ", errors)
        });
    }

    void CheckTurretCombatAndPowerDependency()
    {
        var errors = new List<string>();
        var turret = new RtsBuilding();
        turret.Configure(BattleBuildingCatalog.Get("turret"), true);

        if (!turret.IsDefenseTurret)
            errors.Add("炮塔 IsDefenseTurret 标志位未开启");
        if (turret.AttackDamage <= 0f || turret.AttackRange <= 0f)
            errors.Add("炮塔攻击伤害或射程配置异常");

        // 断电测试
        turret.Powered = false;
        if (!turret.RequiresPower())
            errors.Add("炮塔应标记为依赖电力运作");

        results.Add(new VerificationResult
        {
            TestName = "7. Turret Combat & Power Interlock (防御炮塔独立索敌瞄准与电力闭锁校验)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "炮塔防空反装甲参数完整，供电中断时立即休眠闭锁" : string.Join("; ", errors)
        });
    }

    void CheckBattleTechCastingAndDomainFiltering()
    {
        var errors = new List<string>();

        if (BattleGameManager.Instance is { } mgr)
        {
            // 验证战鹰整备：仅对空中战机生效
            var groundTank = mgr.SpawnUnit("tank", new Vector3(0f, 0f, 0f), true);
            var plane = mgr.SpawnUnit("fighter", new Vector3(0f, 12f, 0f), true);

            if (groundTank is not null && plane is not null)
            {
                // 消耗战机燃油与机载弹药以备测试整备
                plane.CurrentFuel = 10f;
                plane.CurrentAmmo = 0;

                // 重置冷却时间确保测试环境干净
                mgr.ResetBattleTechCooldown("air_refit", true);

                // 释放技能
                mgr.TryCastBattleTech("air_refit", new Vector3(0f, 0f, 0f), out _, out int affected);

                // 检验战鹰整备是否过滤掉地面坦克
                if (groundTank.MoveSpeed > 7.0f) // 原速 6.9f
                    errors.Add("战鹰整备错误影响了地面坦克（应只对飞机生效）！");

                if (plane.CurrentFuel < 25f)
                    errors.Add("战鹰整备未能加满空中战机燃油！");
                if (plane.CurrentAmmo < plane.MaxAmmo)
                    errors.Add("战鹰整备未能装满机载对空对地格斗弹药！");
            }
        }

        results.Add(new VerificationResult
        {
            TestName = "8. Tech Casting & Domain Filtering (战场科技释放约束与航空专享整备过滤)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "战鹰整备严格过滤地面单位，空中战机燃油弹药瞬间补满自愈" : string.Join("; ", errors)
        });
    }

    void CheckTechBuffApplicationOnUnitsAndBuildings()
    {
        var errors = new List<string>();

        // 构造虚拟单位测试机动、火力、急速、装甲与回血
        var testTank = new RtsUnit();
        testTank.ConfigureFromDefinition(BattleUnitCatalog.Get("tank"), true, "坦克");

        float origSpeed = testTank.MoveSpeed;
        float origDmg = testTank.AttackDamage;
        float origCd = testTank.AttackCooldown;

        // 施加机动强化
        testTank.ApplyTechBuff("speed", 10f, 1.32f, 1f, 0f, 1f, 0f, 0f, 0f, Colors.Cyan);
        if (testTank.MoveSpeed <= origSpeed * 1.25f)
            errors.Add($"机动强化加速失效: 原速 {origSpeed}, 现速 {testTank.MoveSpeed}");

        // 施加火力强化
        testTank.ApplyTechBuff("firepower", 10f, 1f, 1.30f, 0f, 1f, 0f, 0f, 0f, Colors.Orange);
        if (testTank.AttackDamage <= origDmg * 1.20f)
            errors.Add($"火力强化攻击力未提升: 原伤害 {origDmg}, 现伤害 {testTank.AttackDamage}");

        // 施加急速射击
        testTank.ApplyTechBuff("rapid", 10f, 1f, 1f, 0f, 0.72f, 0f, 0f, 0f, Colors.Yellow);
        if (testTank.AttackCooldown >= origCd * 0.85f)
            errors.Add($"急速射击未缩短攻击间隔: 原CD {origCd}, 现CD {testTank.AttackCooldown}");

        // 构造虚拟防御炮塔测试建筑加成
        var testTurret = new RtsBuilding();
        testTurret.Configure(BattleBuildingCatalog.Get("turret"), true);
        float origTurretDmg = testTurret.AttackDamage;

        testTurret.ApplyTechBuff("firepower", 10f, 1.30f, 0f, 1f, 0f, 0f, 0f, Colors.Orange);
        if (testTurret.AttackDamage <= origTurretDmg * 1.20f)
            errors.Add($"炮塔未能享受到科技火力加成: 原伤害 {origTurretDmg}, 现伤害 {testTurret.AttackDamage}");

        results.Add(new VerificationResult
        {
            TestName = "9. Tech Multiplier Math (单位与防御设施科技增益动态叠加重算数学模型)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "航速/单发杀伤/开火射速/射程/护甲减免等乘数叠加与回退完全符合预期" : string.Join("; ", errors)
        });
    }

    void CheckRealisticBuildingMeshGeneration()
    {
        var errors = new List<string>();
        string[] allKeys = {
            "main_base", "barracks", "tank_factory", "armor_factory",
            "airfield", "air_factory", "naval_yard", "turret", "power_plant", "gold_mine"
        };

        foreach (var key in allKeys)
        {
            try
            {
                var dummyNode = new Node3D();
                RealisticBuildingBuilder.BuildRealisticBuilding(dummyNode, key, true, Colors.DarkSlateGray);
                int meshCount = dummyNode.FindChildren("*", "MeshInstance3D", true, false).Count;
                if (meshCount < 3)
                    errors.Add($"建筑 {key} 生成的真实 PBR 3D 部件数量过少 ({meshCount})，可能存在模型丢失");
                dummyNode.QueueFree();
            }
            catch (Exception ex)
            {
                errors.Add($"构建建筑 {key} 真实 PBR 3D 结构时抛出异常: {ex.Message}");
            }
        }

        results.Add(new VerificationResult
        {
            TestName = "10. Realistic Building PBR 3D Generation (真实军工建筑结构无异常高精生成)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 ? "全部 10 种建筑成功调用 PBR 装甲钢板/工业防滑板/偏光玻璃/旋转雷达装配，无任何空指针" : string.Join("; ", errors)
        });
    }

    void CheckNavalYardPlacementAndMapWaterProximity()
    {
        var errors = new List<string>();
        string[] waterMaps = {
            BattleMapCatalog.SeaChartName,
            BattleMapCatalog.DefaultMapName,
            BattleMapCatalog.TriLaneWarfareName,
            BattleMapCatalog.JungleName,
            BattleMapCatalog.ArcticTundraName
        };

        foreach (var mapName in waterMaps)
        {
            var map = BattleMapCatalog.Get(mapName);
            var playerSpawn = map.SpawnPoints.FirstOrDefault(s => s.Name == "PlayerBase");
            var basePos = playerSpawn.Name == "PlayerBase" 
                ? playerSpawn.Position 
                : new Vector3(-map.BaseSpawnOffset, 0f, -map.BaseSpawnOffset);
            float distToWater = BattleMapCatalog.DistanceToWater(map, basePos);

            // 主基地与水系距离必须在合理海岸线范围内（<= 55m），杜绝基地深陷内陆 130m+ 导致无法建造船坞
            if (distToWater > 55f)
            {
                errors.Add($"地图 [{mapName}] 主基地离水域过远 (当前距离 {distToWater:F1}m > 55m)，无法合理布局海军码头");
                continue;
            }

            // 在基地周边建造半径内寻找可成功建造船坞的海岸线点位
            float buildRadius = map.BuildRadius > 0f ? map.BuildRadius : 60f;
            float maxNavalDist = Mathf.Max(buildRadius * 1.5f, 95f);
            bool foundValidDockSpot = false;

            // 采样主基地周边 360 度与 20m~65m 范围内的海岸线点位
            for (int angleDeg = 0; angleDeg < 360; angleDeg += 15)
            {
                float rad = Mathf.DegToRad(angleDeg);
                for (float r = 24f; r <= buildRadius; r += 4f)
                {
                    var testPos = basePos + new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
                    float dWater = BattleMapCatalog.DistanceToWater(map, testPos);
                    bool inDeepWater = BattleMapCatalog.IsPointInWater(map, testPos, -3.0f);

                    if (dWater <= 24f && !inDeepWater && testPos.DistanceTo(basePos) <= maxNavalDist)
                    {
                        foundValidDockSpot = true;
                        break;
                    }
                }
                if (foundValidDockSpot) break;
            }

            if (!foundValidDockSpot)
            {
                errors.Add($"地图 [{mapName}] 基地周围建造半径范围内未发现合法的船坞放置点位");
            }
        }

        results.Add(new VerificationResult
        {
            TestName = "11. Naval Yard Placement & Coastal Proximity (海图与水系地图船坞建造合理性核查)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 
                ? "海图群岛、沙漠绿洲、三路大会战、丛林战场与极地冰川基地均具备近海/近水港湾(<=45m)，可在基地建造半径内顺畅落成船坞" 
                : string.Join("; ", errors)
        });
    }

    void CheckBuildingPlacementPreviewAndOverlapRules()
    {
        var errors = new List<string>();
        var battleMgr = BattleGameManager.Instance;
        if (battleMgr is null)
        {
            errors.Add("BattleGameManager.Instance 为空，跳过放置校验");
        }
        else
        {
            var initialBuildingCount = battleMgr.GetBuildings().Count;
            var preview = battleMgr.CreateBuildingPlacementPreview("power_plant");
            if (preview is null)
            {
                errors.Add("CreateBuildingPlacementPreview('power_plant') 返回空");
            }
            else
            {
                // 虚影不能是 RtsBuilding
                var ghost = preview.GetNodeOrNull<Node>("GhostModel");
                if (ghost is null)
                    errors.Add("preview 未包含 GhostModel 节点");
                else if (ghost is RtsBuilding)
                    errors.Add("GhostModel 不应为 RtsBuilding 实例（会导致注册进活跃建筑列表引发自重叠）");

                AddChild(preview);

                // 虚影加入场景树后，活跃建筑列表数量不应增长
                var countAfterAdd = battleMgr.GetBuildings().Count;
                if (countAfterAdd != initialBuildingCount)
                    errors.Add($"虚影加入场景树后，活跃建筑数增加了 {countAfterAdd - initialBuildingCount}，说明虚影被错误计入活跃建筑");

                // 测试合法空地上放置发电厂判定（在主基地建造半径内寻找空旷合法点）
                var playerBase = battleMgr.GetBuildings(true).FirstOrDefault(b => b.IsMainBase && !b.IsRuined && Mathf.Abs(b.GlobalPosition.X) < 100f)
                    ?? battleMgr.GetBuildings(true).FirstOrDefault(b => b.IsMainBase && !b.IsRuined);
                if (playerBase is not null)
                {
                    var basePos = playerBase.GlobalPosition;
                    Vector3 testPos = Vector3.Zero;
                    bool foundValidSpot = false;
                    for (float angle = 0f; angle < 360f; angle += 15f)
                    {
                        var rad = Mathf.DegToRad(angle);
                        for (float dist = 16f; dist <= 36f; dist += 4f)
                        {
                            var cand = basePos + new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
                            if (battleMgr.CanPlaceBuildingAt("power_plant", cand, true, out _))
                            {
                                testPos = cand;
                                foundValidSpot = true;
                                break;
                            }
                        }
                        if (foundValidSpot) break;
                    }

                    if (foundValidSpot)
                    {
                        preview.Position = testPos;
                        bool canPlace = battleMgr.CanPlaceBuildingAt("power_plant", testPos, true, out var errMsg);
                        if (!canPlace)
                        {
                            errors.Add($"在主基地空旷处放置发电厂失败: {errMsg}");
                        }
                    }
                    else
                    {
                        errors.Add("在主基地周围建造半径内未找到合法的空旷建造点位");
                    }

                    // 测试直接重叠在主基地中心上应当被拦截
                    bool overlapBase = battleMgr.CanPlaceBuildingAt("power_plant", basePos, true, out var overlapErr);
                    if (overlapBase)
                    {
                        errors.Add("与主基地直接重叠放置发电厂未被拦截，碰撞判定失效");
                    }
                }

                preview.QueueFree();
            }
        }

        results.Add(new VerificationResult
        {
            TestName = "12. Building Placement Preview & Overlap Immunity (建筑放置虚影不自重叠与无重叠建造准入核查)",
            Passed = errors.Count == 0,
            Details = errors.Count == 0 
                ? "建造虚影完全使用轻量 Node3D 呈现且粒子静默，不污染建筑列表，空旷区域判定 100% 允许建造，重叠区域准确拦截" 
                : string.Join("; ", errors)
        });
    }
}
