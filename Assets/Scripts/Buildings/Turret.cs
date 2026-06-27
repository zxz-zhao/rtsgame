using UnityEngine;
public class Turret : UpgradeableBuilding
{
    // Turrets use a tight footprint so placement and selection align with their small defensive role.
    protected override float DesiredVisualHeight => 4f;
    protected override float DesiredVisualFootprint => 4f;
    private static readonly UpgradeLevelDefinition[] UpgradeLevels =
    {
        new UpgradeLevelDefinition(
            1,
            700,
            0,
            1f,
            0,
            0,
            0,
            55,
            30f,
            1.2f),
        new UpgradeLevelDefinition(
            2,
            860,
            220,
            1f,
            0,
            0,
            0,
            72,
            34f,
            1.02f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 2)),
        new UpgradeLevelDefinition(
            3,
            1020,
            420,
            1f,
            0,
            0,
            0,
            92,
            38f,
            0.88f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 3)),
    };

    /// <summary>
    /// Applies the default defensive stats and auto-attack values for a turret.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "炮塔"; MaxHP = 700; GoldCost = 350; PowerCost = 15;
        bAutoAttack = true; TurretRange = 30f; TurretDamage = 55; TurretInterval = 1.2f;
        PopCapBonus = 0; PowerProvide = 0;
        bIsMainBase = false; bIsPowerPlant = false; bIsGoldMine = false;
        GoldIncomeAmount = 0;
        ApplyConfiguredUpgradeLevel(false);
    }

    protected override UpgradeLevelDefinition[] GetUpgradeLevelDefinitions() => UpgradeLevels;

    /// <summary>
    /// Runs the shared building startup flow; no additional turret-specific initialization is required yet.
    /// </summary>
    protected override void Start()
    {
        base.Start();
    }
}
