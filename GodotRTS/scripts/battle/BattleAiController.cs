using Godot;
using System.Linq;

public partial class BattleAiController : Node
{
    [Export] public int InitialGold { get; set; } = 800;
    [Export] public float GoldInterval { get; set; } = 5f;
    [Export] public int GoldPerInterval { get; set; } = 55;
    [Export] public float InitialAttackDelay { get; set; } = 20f;
    [Export] public float AttackWaveInterval { get; set; } = 32f;
    [Export] public int WaveBaseSize { get; set; } = 3;
    [Export] public int MaxAliveAiUnits { get; set; } = 26;
    [Export] public float DefenseRadius { get; set; } = 42f;

    // ---- 重建参数 ----
    [Export] public int RebuildGoldCost { get; set; } = 600;

    // ---- 科技参数 ----
    [Export] public float TechCastInterval { get; set; } = 45f;

    // ---- 升级参数 ----
    [Export] public float UpgradeCheckInterval { get; set; } = 60f;
    [Export] public int UpgradeGoldThreshold { get; set; } = 1200;

    readonly RandomNumberGenerator rng = new();

    float goldTimer;
    float waveTimer;
    float defenseTimer;
    float rebuildCheckTimer;
    float techTimer;
    float upgradeCheckTimer;
    int waveCount;

    public override void _Ready()
    {
        rng.Seed = (ulong)(BattleMapCatalog.Get(GameState.Instance?.SelectedMapName).RandomSeed + 177);
        if (BattleGameManager.Instance is not null)
            BattleGameManager.Instance.AddGold(false, InitialGold - BattleGameManager.Instance.EnemyGold);

        // 错开科技和升级的初始计时器，避免开局卡顿
        techTimer = TechCastInterval * 0.5f;
        upgradeCheckTimer = UpgradeCheckInterval * 0.7f;
    }

    public override void _Process(double delta)
    {
        var manager = BattleGameManager.Instance;
        if (manager is null || manager.GameOver)
            return;

        var dt = (float)delta;
        UpdateIncome(manager, dt);
        UpdateMainBaseRebuild(manager, dt);
        UpdateDefense(manager, dt);
        UpdateWave(manager, dt);
        UpdateAiTech(manager, dt);
        UpdateAiUpgrade(manager, dt);
    }

    void UpdateIncome(BattleGameManager manager, float delta)
    {
        goldTimer += delta;
        if (goldTimer < GoldInterval)
            return;

        goldTimer = 0f;
        var timeBonus = Mathf.Min(manager.GameTime / 180f, 2f);
        manager.AddGold(false, Mathf.RoundToInt(GoldPerInterval * (1f + timeBonus)));
    }

    void UpdateMainBaseRebuild(BattleGameManager manager, float delta)
    {
        rebuildCheckTimer += delta;
        if (rebuildCheckTimer < 2f)
            return;

        rebuildCheckTimer = 0f;

        // 找到所有处于废墟状态（可重建）的 AI 主基地
        var ruinedBases = manager.GetMainBases(playerOwned: false, includeRuined: true)
            .Where(b => b.CanStartRebuild())
            .ToList();

        if (ruinedBases.Count == 0)
            return;

        // 金币充足时触发重建（优先于攻击波次）
        if (manager.EnemyGold < RebuildGoldCost)
            return;

        foreach (var ruinedBase in ruinedBases)
        {
            if (ruinedBase.StartRebuild())
            {
                // 扣除重建金币（视为消耗）
                manager.TrySpendEnemyGold(RebuildGoldCost);
                GD.Print($"[AI] 主基地开始重建：{ruinedBase.Name}");
            }
        }
    }

    void UpdateDefense(BattleGameManager manager, float delta)
    {
        defenseTimer -= delta;
        if (defenseTimer > 0f)
            return;

        var enemyBase = manager.FindOperationalMainBase(false);
        if (enemyBase is null)
            return;

        var threat = manager.GetUnits(true)
            .OrderBy(u => u.GlobalPosition.DistanceTo(enemyBase.GlobalPosition))
            .FirstOrDefault();
        if (threat is null || threat.GlobalPosition.DistanceTo(enemyBase.GlobalPosition) > DefenseRadius)
        {
            defenseTimer = 2.5f;
            return;
        }

        defenseTimer = 12f;
        var count = waveCount >= 4 ? 3 : 2;
        for (var i = 0; i < count; i++)
            TrySpawnAttacker(manager, threat.GlobalPosition, 0.85f);
    }

    void UpdateWave(BattleGameManager manager, float delta)
    {
        if (manager.GameTime < InitialAttackDelay)
            return;

        var dynamicInterval = Mathf.Max(12f, AttackWaveInterval - waveCount * 1.7f);
        waveTimer += delta;
        if (waveTimer < dynamicInterval)
            return;

        waveTimer = 0f;
        var playerBase = manager.FindOperationalMainBase(true);
        if (playerBase is null)
            return;

        var alive = manager.GetUnits(false).Count;
        if (alive >= MaxAliveAiUnits)
            return;

        waveCount++;
        var count = Mathf.Min(WaveBaseSize + waveCount, 10);
        var target = playerBase.GlobalPosition;
        var split = waveCount >= 4;
        for (var i = 0; i < count; i++)
        {
            var flank = Vector3.Zero;
            if (split && i >= count / 2)
                flank = new Vector3(rng.RandfRange(-45f, 45f), 0f, rng.RandfRange(-45f, 45f));
            TrySpawnAttacker(manager, target + flank, 1f);
        }
    }

    /// <summary>AI 定期向主基地附近释放战场科技，增强防御和攻击能力。</summary>
    void UpdateAiTech(BattleGameManager manager, float delta)
    {
        if (manager.GameTime < 60f)
            return; // 开局 60 秒后才开始使用科技

        techTimer += delta;
        if (techTimer < TechCastInterval)
            return;

        techTimer = 0f;

        var enemyBase = manager.FindOperationalMainBase(playerOwned: false);
        if (enemyBase is null)
            return;

        // 按照战场状态选择科技：攻击波次多时选火力，否则选防御/修复
        string techKey;
        if (waveCount >= 6)
            techKey = rng.RandiRange(0, 1) == 0 ? "firepower" : "rapid";
        else if (waveCount >= 3)
            techKey = rng.RandiRange(0, 1) == 0 ? "armor" : "repair";
        else
            techKey = "speed";

        if (manager.CanCastBattleTechForFaction(techKey, playerOwned: false, out _))
        {
            // 在主基地附近释放，覆盖防御单位
            var castPoint = enemyBase.GlobalPosition + new Vector3(
                rng.RandfRange(-6f, 6f), 0f, rng.RandfRange(-6f, 6f));
            manager.TryCastBattleTechForFaction(techKey, castPoint, playerOwned: false, out _, out _);
        }
    }

    /// <summary>AI 金币充裕时尝试升级主基地，提升战斗力。</summary>
    void UpdateAiUpgrade(BattleGameManager manager, float delta)
    {
        upgradeCheckTimer += delta;
        if (upgradeCheckTimer < UpgradeCheckInterval)
            return;

        upgradeCheckTimer = 0f;

        // 金币不足时跳过
        if (manager.EnemyGold < UpgradeGoldThreshold)
            return;

        var enemyBase = manager.FindOperationalMainBase(playerOwned: false);
        if (enemyBase is null || !enemyBase.IsMainBase)
            return;

        // 尝试升级主基地
        if (manager.TryUpgradeBuildingForFaction(enemyBase, playerOwned: false, out var msg))
            GD.Print($"[AI] 主基地升级成功：{msg}");
    }

    bool TrySpawnAttacker(BattleGameManager manager, Vector3 target, float budgetScale)
    {
        var enemyBase = manager.FindOperationalMainBase(false);
        if (enemyBase is null)
            return false;

        var def = BattleUnitCatalog.PickAi(manager.GameTime, rng);
        if (manager.EnemyGold < Mathf.RoundToInt(def.GoldCost * budgetScale))
            def = BattleUnitCatalog.Infantry;
        if (!manager.TrySpendEnemyGold(def.GoldCost))
            return false;

        var spawn = enemyBase.GlobalPosition + new Vector3(rng.RandfRange(-10f, 10f), 0f, rng.RandfRange(-10f, 10f));
        var playerBase = manager.FindOperationalMainBase(true);
        var unit = manager.SpawnUnit(def.Key, spawn, false, target);
        if (unit is not null && playerBase is not null)
            unit.Attack(playerBase);
        return unit is not null;
    }
}
