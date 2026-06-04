using UnityEngine;
using System.Collections.Generic;

public class Airfield : RTSBuilding
{
    public const int AircraftCapacity = 4;

    private readonly List<AirUnit> assignedAircraft = new List<AirUnit>(AircraftCapacity);

    protected override float DesiredVisualHeight => 2.2f;
    protected override float DesiredVisualFootprint => 12f;

    public int AssignedAircraftCount
    {
        get
        {
            CleanupAssignedAircraft();
            return assignedAircraft.Count;
        }
    }

    public int FreeAircraftSlots => Mathf.Max(0, AircraftCapacity - AssignedAircraftCount);

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "停机场";
        MaxHP = 650;
        GoldCost = 260;
        PowerCost = 15;
        PopCapBonus = 0;
        PowerProvide = 0;
        bIsMainBase = false;
        bIsPowerPlant = false;
        bIsGoldMine = false;
        GoldIncomeAmount = 0;
        bAutoAttack = false;
    }

    protected override void Update()
    {
        base.Update();
        CleanupAssignedAircraft();
    }

    protected override void OnDestroyed()
    {
        var copy = new List<AirUnit>(assignedAircraft);
        assignedAircraft.Clear();
        foreach (var air in copy)
        {
            if (air != null)
                air.NotifyHomeAirfieldDestroyed(this);
        }
        base.OnDestroyed();
    }

    public bool CanAcceptAircraft(AirUnit air)
    {
        if (GetHP() <= 0 || bUnderConstruction) return false;
        CleanupAssignedAircraft();
        if (air != null && !air.RequiresAirfieldSlot) return false;
        if (air != null && assignedAircraft.Contains(air)) return true;
        return assignedAircraft.Count < AircraftCapacity;
    }

    public bool TryAcceptAircraft(AirUnit air)
    {
        if (air == null || air.IsDead()) return false;
        if (!CanAcceptAircraft(air)) return false;
        if (!assignedAircraft.Contains(air))
            assignedAircraft.Add(air);
        air.SetHomeAirfieldFromAirport(this);
        return true;
    }

    public void ReleaseAircraft(AirUnit air)
    {
        if (air == null) return;
        assignedAircraft.Remove(air);
    }

    public Vector3 GetParkingSpot(AirUnit air)
    {
        CleanupAssignedAircraft();
        int slot = assignedAircraft.IndexOf(air);
        if (slot < 0) slot = Mathf.Clamp(assignedAircraft.Count, 0, AircraftCapacity - 1);

        Vector3 right = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

        float lateral = ((slot % 2) == 0 ? -1f : 1f) * 3.2f;
        float depth = slot < 2 ? 1.2f : -2.6f;
        return transform.position + right * lateral + fwd * depth + Vector3.up * 0.55f;
    }

    public Vector3 GetAirApproachPoint(AirUnit air, float flyHeight)
    {
        Vector3 p = GetParkingSpot(air);
        return new Vector3(p.x, flyHeight, p.z);
    }

    public string GetCapacityLabel()
    {
        return $"机位 {AssignedAircraftCount}/{AircraftCapacity}";
    }

    public static Airfield FindAvailable(bool playerOwned, Vector3 anchor, AirUnit air = null)
    {
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null) return null;

        Airfield best = null;
        float bestDist = float.MaxValue;
        foreach (var b in buildings)
        {
            var airfield = b as Airfield;
            if (airfield == null || airfield.bPlayerOwned != playerOwned) continue;
            if (!airfield.CanAcceptAircraft(air)) continue;

            float d = Vector3.Distance(anchor, airfield.transform.position);
            if (d < bestDist)
            {
                best = airfield;
                bestDist = d;
            }
        }
        return best;
    }

    public static int GetTotalAircraftCapacityForFaction(bool playerOwned)
    {
        int total = 0;
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null) return 0;

        foreach (var b in buildings)
        {
            var airfield = b as Airfield;
            if (airfield == null || airfield.bPlayerOwned != playerOwned) continue;
            if (airfield.GetHP() <= 0 || airfield.bUnderConstruction) continue;
            total += AircraftCapacity;
        }
        return total;
    }

    public static int GetAssignedAircraftCountForFaction(bool playerOwned)
    {
        int count = 0;
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null) return 0;

        foreach (var b in buildings)
        {
            var airfield = b as Airfield;
            if (airfield == null || airfield.bPlayerOwned != playerOwned) continue;
            if (airfield.GetHP() <= 0 || airfield.bUnderConstruction) continue;
            count += airfield.AssignedAircraftCount;
        }
        return count;
    }

    void CleanupAssignedAircraft()
    {
        assignedAircraft.RemoveAll(air => air == null || air.IsDead() || !air.RequiresAirfieldSlot || air.HomeAirfield != this);
    }
}
