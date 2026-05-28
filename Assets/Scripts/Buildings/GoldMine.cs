using UnityEngine;
public class GoldMine : RTSBuilding
{
    void Awake() { GoldCost = 250; }
    protected override float DesiredVisualHeight => 4f;
    protected override float DesiredVisualFootprint => 6f;
    protected override void Start()
    {
        DisplayName = "金矿"; MaxHP = 500; GoldCost = 250; PowerCost = 10;
        bIsGoldMine = true; GoldIncomeAmount = 50; GoldIncomeInterval = 5f;
        base.Start();
    }
}
