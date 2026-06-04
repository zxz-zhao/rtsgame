using UnityEngine;
using System.Collections.Generic;

public class AirFactory : RTSBuilding
{
    protected override float DesiredVisualHeight => 6f;
    protected override float DesiredVisualFootprint => 10f;

    public int QueuedAircraftCount
    {
        get
        {
            int count = 0;
            if (ProductionQueue == null || ProductionUnits == null) return 0;
            foreach (int idx in ProductionQueue)
            {
                if (RequiresAirfieldSlotForProductionIndex(idx))
                    count++;
            }
            return count;
        }
    }

    public int FactionAircraftCapacity => Airfield.GetTotalAircraftCapacityForFaction(bPlayerOwned);
    public int FactionAssignedAircraftCount => Airfield.GetAssignedAircraftCountForFaction(bPlayerOwned);
    public int FactionQueuedAircraftCount => GetQueuedAircraftCountForFaction(bPlayerOwned);
    public int FactionReservedAircraftCount => FactionAssignedAircraftCount + FactionQueuedAircraftCount;
    public int FreeAircraftSlots => Mathf.Max(0, FactionAircraftCapacity - FactionReservedAircraftCount);

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "飞机厂";
        MaxHP = 800;
        GoldCost = 400;
        PowerCost = 30;
        PopCapBonus = 0;
        PowerProvide = 0;
        bIsMainBase = false;
        bIsPowerPlant = false;
        bIsGoldMine = false;
        GoldIncomeAmount = 0;
        bAutoAttack = false;
    }

    protected override void Start()
    {
        if (ProductionUnits == null || ProductionUnits.Length == 0)
        {
            var f = Resources.Load<GameObject>("Prefabs/Fighter_P");
            var b = Resources.Load<GameObject>("Prefabs/Bomber_P");
            var sc = Resources.Load<GameObject>("Prefabs/ScoutPlane_P");
            var list = new List<GameObject>();
            var times = new List<float>();
            var costs = new List<int>();
            if (f != null) { list.Add(f); times.Add(10f); costs.Add(200); }
            if (b != null) { list.Add(b); times.Add(18f); costs.Add(400); }
            if (sc != null) { list.Add(sc); times.Add(8f); costs.Add(120); }
            if (list.Count > 0)
            {
                ProductionUnits = list.ToArray();
                ProductionTimes = times.ToArray();
                ProductionCosts = costs.ToArray();
            }
        }
        base.Start();
    }

    protected override bool CanEnqueueProductionUnit(int idx)
    {
        if (!base.CanEnqueueProductionUnit(idx)) return false;
        if (!RequiresAirfieldSlotForProductionIndex(idx)) return true;
        return FreeAircraftSlots > 0;
    }

    protected override bool CanCompleteProductionUnit(int idx)
    {
        if (!base.CanCompleteProductionUnit(idx)) return false;
        if (!RequiresAirfieldSlotForProductionIndex(idx)) return true;
        return Airfield.FindAvailable(bPlayerOwned, transform.position) != null;
    }

    protected override void OnUnitSpawnedFromProduction(RTSUnit unit, int idx)
    {
        base.OnUnitSpawnedFromProduction(unit, idx);
        var air = unit as AirUnit;
        if (air == null) return;
        if (!air.RequiresAirfieldSlot) return;

        var airfield = Airfield.FindAvailable(bPlayerOwned, transform.position, air);
        if (airfield == null) return;

        airfield.TryAcceptAircraft(air);
        Vector3 approach = airfield.GetAirApproachPoint(air, air.FlyHeight);
        air.transform.position = approach;
    }

    public bool HasAircraftSlotForProductionIndex(int idx)
    {
        if (!RequiresAirfieldSlotForProductionIndex(idx)) return true;
        return FreeAircraftSlots > 0;
    }

    bool RequiresAirfieldSlotForProductionIndex(int idx)
    {
        if (ProductionUnits == null || idx < 0 || idx >= ProductionUnits.Length || ProductionUnits[idx] == null)
            return false;
        var air = ProductionUnits[idx].GetComponent<AirUnit>();
        return air != null && air.RequiresAirfieldSlot;
    }

    static int GetQueuedAircraftCountForFaction(bool playerOwned)
    {
        int count = 0;
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null) return 0;

        foreach (var b in buildings)
        {
            var factory = b as AirFactory;
            if (factory == null || factory.bPlayerOwned != playerOwned) continue;
            if (factory.GetHP() <= 0 || factory.bUnderConstruction) continue;
            count += factory.QueuedAircraftCount;
        }
        return count;
    }
}
