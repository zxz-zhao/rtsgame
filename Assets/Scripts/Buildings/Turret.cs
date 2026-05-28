using UnityEngine;
public class Turret : RTSBuilding
{
    void Awake() { GoldCost = 350; }
    protected override float DesiredVisualHeight => 4f;
    protected override float DesiredVisualFootprint => 4f;
    protected override void Start()
    {
        DisplayName = "炮塔"; MaxHP = 700; GoldCost = 350; PowerCost = 15;
        bAutoAttack = true; TurretRange = 30f; TurretDamage = 55; TurretInterval = 1.2f;
        base.Start();
    }
}
