using Godot;
using System.Collections.Generic;

public partial class CombatTestRunner : Node3D
{
    [Export] public Camera3D? CombatCamera { get; set; }
    [Export] public RtsUnit? PlayerTank { get; set; }
    [Export] public RtsUnit? PlayerInfantry { get; set; }
    [Export] public RtsUnit? EnemyTank { get; set; }
    [Export] public RtsUnit? EnemyInfantry { get; set; }

    float timer = 0f;
    bool ordersIssued = false;
    bool unitsSpawned = false;
    readonly List<RtsUnit> playerUnits = new();
    readonly List<RtsUnit> enemyUnits = new();

    public override void _EnterTree()
    {
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableWaterCheck = true;
    }

    public override void _Ready()
    {
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableWaterCheck = true;

        if (PlayerTank is not null)
        {
            PlayerTank.GlobalPosition = new Vector3(-7.5f, 0f, 1.2f);
            PlayerTank.LookAt(new Vector3(7.5f, 0f, 1.2f), Vector3.Up);
            playerUnits.Add(PlayerTank);
        }
        if (EnemyTank is not null)
        {
            EnemyTank.GlobalPosition = new Vector3(7.5f, 0f, 1.2f);
            EnemyTank.LookAt(new Vector3(-7.5f, 0f, 1.2f), Vector3.Up);
            enemyUnits.Add(EnemyTank);
        }
        if (PlayerInfantry is not null)
        {
            PlayerInfantry.GlobalPosition = new Vector3(-5.5f, 0f, -2.2f);
            PlayerInfantry.LookAt(new Vector3(5.5f, 0f, -2.2f), Vector3.Up);
            playerUnits.Add(PlayerInfantry);
        }
        if (EnemyInfantry is not null)
        {
            EnemyInfantry.GlobalPosition = new Vector3(5.5f, 0f, -2.2f);
            EnemyInfantry.LookAt(new Vector3(-5.5f, 0f, -2.2f), Vector3.Up);
            enemyUnits.Add(EnemyInfantry);
        }

        if (CombatCamera is not null)
        {
            CombatCamera.Fov = 46f;
            CombatCamera.Near = 0.3f;
            CombatCamera.Far = 500f;
            CombatCamera.GlobalPosition = new Vector3(-8.5f, 2.2f, -4.5f);
            CombatCamera.LookAt(new Vector3(-2.0f, 1.2f, 0.5f), Vector3.Up);
        }
    }

    void SpawnAdditionalLegionUnits()
    {
        if (BattleGameManager.Instance is not { } mgr)
            return;

        // 联盟重装军团补充部队
        SpawnAndRegister(mgr, "heavy_tank", new Vector3(-9.2f, 0f, -2.0f), true, 0f);
        SpawnAndRegister(mgr, "anti_air_gun", new Vector3(-10.5f, 0f, 4.2f), true, 0f);
        SpawnAndRegister(mgr, "artillery", new Vector3(-12.0f, 0f, -0.5f), true, 0f);
        SpawnAndRegister(mgr, "infantry_flamethrower", new Vector3(-5.2f, 0f, 2.6f), true, 0f);

        // 敌方集群补充部队
        SpawnAndRegister(mgr, "heavy_tank", new Vector3(9.2f, 0f, -2.0f), false, Mathf.Pi);
        SpawnAndRegister(mgr, "anti_air_gun", new Vector3(10.5f, 0f, 4.2f), false, Mathf.Pi);
        SpawnAndRegister(mgr, "artillery", new Vector3(12.0f, 0f, -0.5f), false, Mathf.Pi);
        SpawnAndRegister(mgr, "infantry_flamethrower", new Vector3(5.2f, 0f, 2.6f), false, Mathf.Pi);
    }

    void SpawnAndRegister(BattleGameManager mgr, string key, Vector3 pos, bool playerOwned, float yaw)
    {
        var unit = mgr.SpawnUnit(key, pos, playerOwned);
        if (unit is not null)
        {
            unit.Rotation = new Vector3(0f, yaw, 0f);
            unit.MaxHealth = 99999f;
            unit.Health = 99999f;
            unit.AttackCooldown = Mathf.Max(0.75f, unit.AttackCooldown * 0.65f);
            if (playerOwned) playerUnits.Add(unit);
            else enemyUnits.Add(unit);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        timer += dt;

        if (!unitsSpawned && timer > 0.05f)
        {
            unitsSpawned = true;
            SpawnAdditionalLegionUnits();
            foreach (var u in playerUnits) { u.MaxHealth = 99999f; u.Health = 99999f; }
            foreach (var u in enemyUnits) { u.MaxHealth = 99999f; u.Health = 99999f; }
        }

        // 下达全军交火指令
        if (!ordersIssued && timer > 0.18f)
        {
            ordersIssued = true;
            for (int i = 0; i < Mathf.Min(playerUnits.Count, enemyUnits.Count); i++)
            {
                var p = playerUnits[i];
                var e = enemyUnits[i];
                if (GodotObject.IsInstanceValid(p) && GodotObject.IsInstanceValid(e))
                {
                    p.Attack(e);
                    e.Attack(p);
                }
            }
        }

        // 电影级 4 段式动态连续摄影机调度 (12.0 秒全程实机实录)
        UpdateCinematicCamera(timer);

        // 12.0 秒录制完成，调用 Quit() 安全保存写出完整电影文件
        if (timer >= 12.0f)
        {
            GD.Print($"[LegionCinematic] 12.0s Movie Recording completed cleanly. Quitting...");
            GetTree().Quit();
        }
    }

    void UpdateCinematicCamera(float time)
    {
        if (CombatCamera is null)
            return;

        Vector3 camPos;
        Vector3 lookTarget;
        float fov;

        if (time < 3.2f)
        {
            // 阶段 1：低机位坦克炮口特写与穿甲主炮开火 Bloom 高光
            float progress = time / 3.2f;
            camPos = new Vector3(-8.8f + progress * 2.5f, 1.9f + progress * 0.4f, -4.5f + progress * 2.0f);
            lookTarget = new Vector3(0.5f, 1.1f, 1.2f);
            fov = Mathf.Lerp(42f, 48f, progress);
        }
        else if (time < 6.5f)
        {
            // 阶段 2：侧翼跟拍 防空车地空导弹发射与喷火兵扇形烈焰扫射
            float progress = (time - 3.2f) / 3.3f;
            camPos = new Vector3(-6.3f + progress * 7.5f, 2.6f + progress * 1.8f, 7.8f - progress * 1.5f);
            lookTarget = new Vector3(0.0f, 1.0f, 2.5f);
            fov = Mathf.Lerp(48f, 52f, progress);
        }
        else if (time < 9.5f)
        {
            // 阶段 3：自行火炮大仰角曲射弹道与弹着点爆炸火球、火花与冲击波
            float progress = (time - 6.5f) / 3.0f;
            camPos = new Vector3(3.0f - progress * 4.0f, 6.5f + progress * 4.5f, -9.5f + progress * 3.5f);
            lookTarget = new Vector3(progress * 4.0f, 0.5f, 0.0f);
            fov = Mathf.Lerp(52f, 55f, progress);
        }
        else
        {
            // 阶段 4：经典 RTS 宏大高空上帝俯视全军会战终极全景
            float progress = Mathf.Clamp((time - 9.5f) / 2.5f, 0f, 1f);
            camPos = new Vector3(Mathf.Sin(progress * 0.8f) * 4.0f, 15.5f + progress * 2.5f, -18.5f - progress * 2.0f);
            lookTarget = new Vector3(0.0f, 0.5f, 0.5f);
            fov = Mathf.Lerp(55f, 48f, progress);
        }

        CombatCamera.Fov = fov;
        CombatCamera.GlobalPosition = camPos;
        CombatCamera.LookAt(lookTarget, Vector3.Up);
    }
}

