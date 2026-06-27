using UnityEngine;
using System.Collections.Generic;

public class Airfield : RTSBuilding
{
    public const int AircraftCapacity = 4;
    public const float PadWidth = 13.6f;
    public const float PadDepth = 13.2f;
    public const float PadColliderHeight = 1f;
    public const float ParkingHeight = 0.55f;

    const float PadSurfaceY = 0.04f;
    const float PadSurfaceThickness = 0.08f;
    const float MarkY = 0.12f;
    const float MarkThickness = 0.04f;
    const float DividerThickness = 0.16f;
    const float BayPaddingX = 0.30f;
    const float BayPaddingZ = 0.30f;

    private readonly List<AirUnit> assignedAircraft = new List<AirUnit>(AircraftCapacity);

    protected override float DesiredVisualHeight => 2.2f;
    protected override float DesiredVisualFootprint => PadWidth;
    protected override float ReadableTopOffsetFallback => 1.5f;
    protected override float GetBuildingLabelAnchorYOffset(float topOffset) => 0.42f;

    public static Vector3 ColliderCenter => new Vector3(0f, 0.45f, 0f);
    public static Vector3 ColliderSize => new Vector3(PadWidth, PadColliderHeight, PadDepth);
    public static Vector2 ParkingBaySize => new Vector2(
        PadWidth * 0.5f - BayPaddingX * 2f,
        PadDepth * 0.5f - BayPaddingZ * 2f);

    public int AssignedAircraftCount
    {
        get
        {
            CleanupAssignedAircraft();
            return assignedAircraft.Count;
        }
    }

    public int FreeAircraftSlots => Mathf.Max(0, AircraftCapacity - AssignedAircraftCount);

    /// <summary>
    /// Applies the baseline stats and economy values for an airfield building.
    /// </summary>
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

    /// <summary>
    /// Keeps the assigned-aircraft list pruned as aircraft die, switch owners, or lose their parking requirement.
    /// </summary>
    protected override void Update()
    {
        base.Update();
        CleanupAssignedAircraft();
    }

    /// <summary>
    /// Releases all parked aircraft bindings before the building destruction flow continues.
    /// </summary>
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

    /// <summary>
    /// Returns whether this airfield can host the supplied aircraft right now.
    /// </summary>
    public bool CanAcceptAircraft(AirUnit air)
    {
        if (GetHP() <= 0 || bUnderConstruction) return false;
        CleanupAssignedAircraft();
        if (air != null && !air.RequiresAirfieldSlot) return false;
        if (air != null && assignedAircraft.Contains(air)) return true;
        return assignedAircraft.Count < AircraftCapacity;
    }

    /// <summary>
    /// Reserves a parking slot for an aircraft and records the airfield as its home base.
    /// </summary>
    public bool TryAcceptAircraft(AirUnit air)
    {
        if (air == null || air.IsDead()) return false;
        if (!CanAcceptAircraft(air)) return false;
        if (!assignedAircraft.Contains(air))
            assignedAircraft.Add(air);
        air.SetHomeAirfieldFromAirport(this);
        return true;
    }

    /// <summary>
    /// Removes an aircraft from this airfield's assigned parking list.
    /// </summary>
    public void ReleaseAircraft(AirUnit air)
    {
        if (air == null) return;
        assignedAircraft.Remove(air);
    }

    /// <summary>
    /// Applies the shared runtime collider dimensions used by all airfield prefabs.
    /// </summary>
    public static void ConfigureCollider(BoxCollider collider)
    {
        if (collider == null) return;
        collider.center = ColliderCenter;
        collider.size = ColliderSize;
    }

    /// <summary>
    /// Builds the simple runtime pad visuals used when no authored model is supplied.
    /// </summary>
    public static void BuildVisual(Transform parent, bool playerOwned, System.Action<Transform, string, Vector3, Vector3, Color> addPart)
    {
        if (parent == null || addPart == null) return;

        Color baseColor = playerOwned ? new Color(0.18f, 0.42f, 0.58f) : new Color(0.55f, 0.30f, 0.18f);
        Color stripeColor = playerOwned ? new Color(0.60f, 0.88f, 1f) : new Color(1f, 0.55f, 0.35f);
        Color crateColor = new Color(0.34f, 0.32f, 0.28f);

        addPart(parent, "AirfieldPad", new Vector3(0f, PadSurfaceY, 0f), new Vector3(PadWidth, PadSurfaceThickness, PadDepth), baseColor);
        addPart(parent, "DividerVertical", new Vector3(0f, MarkY, 0f), new Vector3(DividerThickness, MarkThickness, PadDepth - 0.8f), stripeColor);
        addPart(parent, "DividerHorizontal", new Vector3(0f, MarkY, 0f), new Vector3(PadWidth - 0.8f, MarkThickness, DividerThickness), stripeColor);

        for (int slot = 0; slot < AircraftCapacity; slot++)
            AddParkingBayFrame(parent, slot, stripeColor, addPart);

        addPart(parent, "FuelCrate", new Vector3(PadWidth * 0.38f, 0.32f, 0f), new Vector3(0.68f, 0.56f, 0.68f), crateColor);
    }

    /// <summary>
    /// Returns the world-space parking position assigned to the given aircraft.
    /// </summary>
    public Vector3 GetParkingSpot(AirUnit air)
    {
        CleanupAssignedAircraft();
        int slot = GetParkingSlotIndex(air);
        return transform.TransformPoint(GetParkingSlotLocalPosition(slot));
    }

    /// <summary>
    /// Returns the airborne approach point directly above the aircraft's parking slot.
    /// </summary>
    public Vector3 GetAirApproachPoint(AirUnit air, float flyHeight)
    {
        Vector3 p = GetParkingSpot(air);
        return new Vector3(p.x, flyHeight, p.z);
    }

    /// <summary>
    /// Formats the current occupancy label shown by UI surfaces that summarize airfield capacity.
    /// </summary>
    public string GetCapacityLabel()
    {
        return $"机位 {AssignedAircraftCount}/{AircraftCapacity}";
    }

    /// <summary>
    /// Finds the nearest usable airfield for the given faction that can still accept the supplied aircraft.
    /// </summary>
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

    /// <summary>
    /// Totals the effective aircraft capacity provided by all completed airfields for one faction.
    /// </summary>
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

    /// <summary>
    /// Totals the number of aircraft currently assigned to completed airfields for one faction.
    /// </summary>
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

    /// <summary>
    /// Purges invalid aircraft references from the assignment list so capacity math stays accurate.
    /// </summary>
    void CleanupAssignedAircraft()
    {
        assignedAircraft.RemoveAll(air => air == null || air.IsDead() || !air.RequiresAirfieldSlot || air.HomeAirfield != this);
    }

    /// <summary>
    /// Resolves the parking slot index for an aircraft, falling back to the next free slot when needed.
    /// </summary>
    int GetParkingSlotIndex(AirUnit air)
    {
        int slot = assignedAircraft.IndexOf(air);
        if (slot < 0) slot = Mathf.Clamp(assignedAircraft.Count, 0, AircraftCapacity - 1);
        return slot;
    }

    /// <summary>
    /// Converts a logical parking slot into its local-space position on the landing pad.
    /// </summary>
    static Vector3 GetParkingSlotLocalPosition(int slot)
    {
        slot = Mathf.Clamp(slot, 0, AircraftCapacity - 1);
        int column = slot % 2;
        int row = slot / 2;

        float localX = (column == 0 ? -1f : 1f) * PadWidth * 0.25f;
        float localZ = (row == 0 ? 1f : -1f) * PadDepth * 0.25f;
        return new Vector3(localX, ParkingHeight, localZ);
    }

    /// <summary>
    /// Draws the four border segments that frame one parking bay on the pad.
    /// </summary>
    static void AddParkingBayFrame(
        Transform parent,
        int slot,
        Color color,
        System.Action<Transform, string, Vector3, Vector3, Color> addPart)
    {
        Vector3 center = GetParkingSlotLocalPosition(slot);
        center.y = MarkY;

        Vector2 baySize = ParkingBaySize;
        float halfWidth = baySize.x * 0.5f;
        float halfDepth = baySize.y * 0.5f;

        addPart(parent, $"ParkingBay_{slot}_Top", center + new Vector3(0f, 0f, halfDepth), new Vector3(baySize.x, MarkThickness, DividerThickness), color);
        addPart(parent, $"ParkingBay_{slot}_Bottom", center + new Vector3(0f, 0f, -halfDepth), new Vector3(baySize.x, MarkThickness, DividerThickness), color);
        addPart(parent, $"ParkingBay_{slot}_Left", center + new Vector3(-halfWidth, 0f, 0f), new Vector3(DividerThickness, MarkThickness, baySize.y), color);
        addPart(parent, $"ParkingBay_{slot}_Right", center + new Vector3(halfWidth, 0f, 0f), new Vector3(DividerThickness, MarkThickness, baySize.y), color);
    }
}
