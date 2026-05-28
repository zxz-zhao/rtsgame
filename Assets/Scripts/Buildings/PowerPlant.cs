using UnityEngine;
public class PowerPlant : RTSBuilding
{
    void Awake() { GoldCost = 200; }
    protected override float DesiredVisualHeight => 6f;
    protected override float DesiredVisualFootprint => 8f;
    protected override void Start()
    {
        DisplayName = "电厂"; MaxHP = 600; GoldCost = 200;
        PopCapBonus = 50; PowerProvide = 50; bIsPowerPlant = true;
        base.Start();
    }
}
