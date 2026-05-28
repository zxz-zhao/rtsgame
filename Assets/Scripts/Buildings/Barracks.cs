using UnityEngine;
public class Barracks : RTSBuilding
{
    void Awake() { GoldCost = 300; }
    protected override float DesiredVisualHeight => 5f;
    protected override float DesiredVisualFootprint => 8f;
    protected override void Start()
    {
        DisplayName = "兵工厂"; MaxHP = 800; GoldCost = 300; PowerCost = 20;
        if (ProductionUnits == null || ProductionUnits.Length == 0)
        {
            var p = Resources.Load<GameObject>("Prefabs/Infantry_P");
            if (p != null)
            {
                ProductionUnits = new GameObject[] { p };
                ProductionTimes = new float[] { 5f };
                ProductionCosts = new int[]   { 100 };
            }
        }
        base.Start();
    }
}
