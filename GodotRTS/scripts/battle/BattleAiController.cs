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

    readonly RandomNumberGenerator rng = new();

    float goldTimer;
    float waveTimer;
    float defenseTimer;
    float rebuildCheckTimer;
    int waveCount;

    public override void _Ready()
    {
        rng.Seed = (ulong)(BattleMapCatalog.Get(GameState.Instance?.SelectedMapName).RandomSeed + 177);
        if (BattleGameManager.Instance is not null)
            BattleGameManager.Instance.AddGold(false, InitialGold - BattleGameManager.Instance.EnemyGold);
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
        if (rebuildCheckTimer < 1f)
            return;

        rebuildCheckTimer = 0f;
        if (manager.FindOperationalMainBase(false) is not null)
            return;

        manager.TryStartFactionMainBaseRebuild(false, out _);
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
