using UnityEngine;
public class AirFactory : RTSBuilding
{
    void Awake() { GoldCost = 400; }
    protected override float DesiredVisualHeight => 6f;
    protected override float DesiredVisualFootprint => 10f;
    protected override void Start()
    {
        DisplayName = "飞机厂"; MaxHP = 800; GoldCost = 400; PowerCost = 30;
        if (ProductionUnits == null || ProductionUnits.Length == 0)
        {
            var f  = Resources.Load<GameObject>("Prefabs/Fighter_P");
            var b  = Resources.Load<GameObject>("Prefabs/Bomber_P");
            var sc = Resources.Load<GameObject>("Prefabs/ScoutPlane_P");
            var list  = new System.Collections.Generic.List<GameObject>();
            var times = new System.Collections.Generic.List<float>();
            var costs = new System.Collections.Generic.List<int>();
            if (f  != null) { list.Add(f);  times.Add(10f); costs.Add(200); }
            if (b  != null) { list.Add(b);  times.Add(18f); costs.Add(400); }
            if (sc != null) { list.Add(sc); times.Add(8f);  costs.Add(120); }
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
