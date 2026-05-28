using UnityEngine;
public class TankFactory : RTSBuilding
{
    void Awake() { GoldCost = 400; }
    protected override float DesiredVisualHeight => 5.5f;
    protected override float DesiredVisualFootprint => 10f;
    protected override void Start()
    {
        DisplayName = "特需厂"; MaxHP = 850; GoldCost = 400; PowerCost = 35;
        if (ProductionUnits == null || ProductionUnits.Length == 0)
        {
            var t  = Resources.Load<GameObject>("Prefabs/Tank_P");
            var a  = Resources.Load<GameObject>("Prefabs/Artillery_P");
            var fl = Resources.Load<GameObject>("Prefabs/Flamethrower_P");
            var list  = new System.Collections.Generic.List<GameObject>();
            var times = new System.Collections.Generic.List<float>();
            var costs = new System.Collections.Generic.List<int>();
            if (t  != null) { list.Add(t);  times.Add(15f); costs.Add(300); }
            if (a  != null) { list.Add(a);  times.Add(10f); costs.Add(160); }
            if (fl != null) { list.Add(fl); times.Add(8f);  costs.Add(220); }
            if (list.Count > 0)
            {
                ProductionUnits = list.ToArray();
                ProductionTimes = times.ToArray();
                ProductionCosts = costs.ToArray();
            }
        }
        base.Start();
    }
}
