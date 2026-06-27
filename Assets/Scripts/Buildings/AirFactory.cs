using UnityEngine;
using System.Collections.Generic;

public class AirFactory : UpgradeableBuilding
{
    // Air factories use a taller silhouette so runtime scaling keeps the hangar readable from the RTS camera.
    protected override float DesiredVisualHeight => 6f;
    protected override float DesiredVisualFootprint => 10f;
    const float LaunchForwardDistance = 7f;
    const float FallbackLaunchOffset = 3.2f;
    const float LaunchGroundHeight = 0.55f;
    private static readonly UpgradeLevelDefinition[] UpgradeLevels =
    {
        new UpgradeLevelDefinition(
            1,
            800,
            0,
            1f),
        new UpgradeLevelDefinition(
            2,
            980,
            360,
            1.12f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 2),
            new UpgradeRequirement(typeof(Airfield), 1, "停机场")),
        new UpgradeLevelDefinition(
            3,
            1180,
            560,
            1.25f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 3),
            new UpgradeRequirement(typeof(Airfield), 2, "停机场")),
    };

    /// <summary>
    /// Counts how many queued production entries currently reserve an airfield slot.
    /// </summary>
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

    /// <summary>
    /// Applies the baseline economy and durability values for the aircraft production building.
    /// </summary>
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
        ApplyConfiguredUpgradeLevel(false);
    }

    protected override UpgradeLevelDefinition[] GetUpgradeLevelDefinitions() => UpgradeLevels;

    /// <summary>
    /// Lazily builds the aircraft production roster when the prefab was created without configured arrays.
    /// </summary>
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

    /// <summary>
    /// Blocks queueing aircraft when the faction has no spare airfield capacity left to reserve.
    /// </summary>
    protected override bool CanEnqueueProductionUnit(int idx)
    {
        if (!base.CanEnqueueProductionUnit(idx)) return false;
        if (!RequiresAirfieldSlotForProductionIndex(idx)) return true;
        return FreeAircraftSlots > 0;
    }

    /// <summary>
    /// Prevents production completion if no usable airfield can accept the finished aircraft.
    /// </summary>
    protected override bool CanCompleteProductionUnit(int idx)
    {
        if (!base.CanCompleteProductionUnit(idx)) return false;
        if (!RequiresAirfieldSlotForProductionIndex(idx)) return true;
        return Airfield.FindAvailable(bPlayerOwned, transform.position) != null;
    }

    /// <summary>
    /// Hands freshly spawned aircraft to the nearest compatible airfield so they launch from the factory and then ferry to parking.
    /// </summary>
    protected override void OnUnitSpawnedFromProduction(RTSUnit unit, int idx)
    {
        base.OnUnitSpawnedFromProduction(unit, idx);
        var air = unit as AirUnit;
        if (air == null) return;
        if (!air.RequiresAirfieldSlot) return;

        var airfield = Airfield.FindAvailable(bPlayerOwned, transform.position, air);
        if (airfield == null) return;

        Vector3 launchPoint = ResolveAircraftLaunchPoint();
        Vector3 climbPoint = ResolveAircraftClimbPoint(launchPoint, air.FlyHeight);
        air.BeginFactoryTransferToAirfield(airfield, launchPoint, climbPoint);
    }

    /// <summary>
    /// Exposes whether a production entry can currently reserve the airfield slot it requires.
    /// </summary>
    public bool HasAircraftSlotForProductionIndex(int idx)
    {
        if (!RequiresAirfieldSlotForProductionIndex(idx)) return true;
        return FreeAircraftSlots > 0;
    }

    /// <summary>
    /// Checks whether the requested production prefab is an aircraft that consumes an airfield parking slot.
    /// </summary>
    bool RequiresAirfieldSlotForProductionIndex(int idx)
    {
        if (ProductionUnits == null || idx < 0 || idx >= ProductionUnits.Length || ProductionUnits[idx] == null)
            return false;
        var air = ProductionUnits[idx].GetComponent<AirUnit>();
        return air != null && air.RequiresAirfieldSlot;
    }

    /// <summary>
    /// Aggregates queued aircraft for one faction across every active air factory.
    /// </summary>
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

    Vector3 ResolveAircraftLaunchPoint()
    {
        Transform launchMarker = FindChildByNameToken(transform, "WW2ParkedPlane", "ParkedPlane", "Plane");
        Vector3 launchPoint = launchMarker != null
            ? launchMarker.position
            : transform.position + GetFlatForward() * FallbackLaunchOffset;
        launchPoint.y = transform.position.y + LaunchGroundHeight;
        return launchPoint;
    }

    Vector3 ResolveAircraftClimbPoint(Vector3 launchPoint, float flyHeight)
    {
        Vector3 climbPoint = launchPoint + GetFlatForward() * LaunchForwardDistance;
        climbPoint.y = flyHeight;
        return climbPoint;
    }

    Vector3 GetFlatForward()
    {
        Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        return forward.normalized;
    }

    static Transform FindChildByNameToken(Transform root, params string[] tokens)
    {
        if (root == null || tokens == null || tokens.Length == 0)
            return null;

        foreach (Transform child in root)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token)
                    && child.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;
            }

            Transform nested = FindChildByNameToken(child, tokens);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
