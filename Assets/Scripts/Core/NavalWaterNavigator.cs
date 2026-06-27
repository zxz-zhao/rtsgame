using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches water and land rectangles so ships can cheaply validate movement against the active battle map.
/// </summary>
public static class NavalWaterNavigator
{
    struct RectZone
    {
        public Vector3 Center; // World-space center of the rectangular zone.
        public Vector2 Size; // Width and depth before rotation is applied.
        public float Yaw; // Rotation around the Y axis in degrees.

        public RectZone(Vector3 center, Vector2 size, float yaw)
        {
            Center = center;
            Size = size;
            Yaw = yaw;
        }
    }

    static readonly List<RectZone> WaterZones = new List<RectZone>(16); // Explicit water corridors for land-heavy maps.
    static readonly List<RectZone> LandZones = new List<RectZone>(16); // Island or shoreline blockers for sea-heavy maps.
    static string cachedMapName = string.Empty; // Last map name used to build the cached zones.

    /// <summary>
    /// Returns whether a world-space point is valid water, optionally shrinking the usable area by an inset.
    /// </summary>
    public static bool IsPointOnWater(Vector3 point, float inset = 0f)
    {
        EnsureCache();
        if (cachedMapName == BattleMapCatalog.SeaChartName)
            return IsSeaChartWater(point, inset);

        if (WaterZones.Count == 0)
            return false;

        for (int i = 0; i < WaterZones.Count; i++)
        {
            if (ContainsRect(WaterZones[i], point, -inset))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Projects an arbitrary point back onto the nearest legal water position.
    /// </summary>
    public static Vector3 ClampPointToWater(Vector3 point, float inset = 0f)
    {
        EnsureCache();
        if (cachedMapName == BattleMapCatalog.SeaChartName)
            return ClampSeaChartWater(point, inset);

        if (WaterZones.Count == 0)
            return point;

        Vector3 best = point;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < WaterZones.Count; i++)
        {
            Vector3 candidate = ClampInsideRect(WaterZones[i], point, inset);
            float sqr = PlanarSqrDistance(point, candidate);
            if (sqr < bestSqr)
            {
                best = candidate;
                bestSqr = sqr;
            }
        }

        best.y = point.y;
        return best;
    }

    /// <summary>
    /// Rebuilds the cached navigation masks when the selected map changes.
    /// </summary>
    static void EnsureCache()
    {
        string mapName = PlayerPrefs.GetString("current_map", BattleMapCatalog.DefaultMapName);
        if (mapName == cachedMapName)
            return;

        cachedMapName = mapName;
        WaterZones.Clear();
        LandZones.Clear();

        BattleMapDefinition map = BattleMapCatalog.Get(mapName);
        if (map == null)
            return;

        if (map.Waters != null)
        {
            for (int i = 0; i < map.Waters.Length; i++)
            {
                TerrainStripSpec water = map.Waters[i];
                WaterZones.Add(new RectZone(water.Center, water.Size, water.Angle));
            }
        }

        if (map.Patches != null)
        {
            for (int i = 0; i < map.Patches.Length; i++)
            {
                TerrainPatchSpec patch = map.Patches[i];
                LandZones.Add(new RectZone(patch.Center, patch.Size, patch.Angle));
            }
        }

        if (mapName == BattleMapCatalog.DefaultMapName || mapName == BattleMapCatalog.GlobalConquestName)
        {
            WaterZones.Add(new RectZone(new Vector3(-163f, 0f, 0f), new Vector2(10f, 396f), 0f));
            WaterZones.Add(new RectZone(new Vector3(163f, 0f, 0f), new Vector2(10f, 396f), 0f));
            WaterZones.Add(new RectZone(new Vector3(0f, 0f, 163f), new Vector2(252f, 10f), 0f));
            WaterZones.Add(new RectZone(new Vector3(0f, 0f, -163f), new Vector2(252f, 10f), 0f));
            WaterZones.Add(new RectZone(new Vector3(-190f, 0f, 0f), new Vector2(28f, 396f), 0f));
            WaterZones.Add(new RectZone(new Vector3(190f, 0f, 0f), new Vector2(28f, 396f), 0f));
            WaterZones.Add(new RectZone(new Vector3(0f, 0f, 190f), new Vector2(252f, 24f), 0f));
            WaterZones.Add(new RectZone(new Vector3(0f, 0f, -190f), new Vector2(252f, 24f), 0f));
        }
    }

    /// <summary>
    /// Treats the sea-chart map as open water with island patches carved out as forbidden land.
    /// </summary>
    static bool IsSeaChartWater(Vector3 point, float inset)
    {
        float bound = BattleMapCatalog.MapHalfSize - Mathf.Max(0.1f, inset);
        if (Mathf.Abs(point.x) > bound || Mathf.Abs(point.z) > bound)
            return false;

        for (int i = 0; i < LandZones.Count; i++)
        {
            if (ContainsRect(LandZones[i], point, inset))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Clamps a point to the sea-chart bounds and pushes it out of any island rectangles it overlaps.
    /// </summary>
    static Vector3 ClampSeaChartWater(Vector3 point, float inset)
    {
        Vector3 clamped = point;
        float bound = BattleMapCatalog.MapHalfSize - Mathf.Max(0.1f, inset);
        clamped.x = Mathf.Clamp(clamped.x, -bound, bound);
        clamped.z = Mathf.Clamp(clamped.z, -bound, bound);

        for (int pass = 0; pass < LandZones.Count; pass++)
        {
            bool moved = false;
            for (int i = 0; i < LandZones.Count; i++)
            {
                if (!ContainsRect(LandZones[i], clamped, inset))
                    continue;

                clamped = PushOutsideRect(LandZones[i], clamped, inset + 0.05f);
                clamped.x = Mathf.Clamp(clamped.x, -bound, bound);
                clamped.z = Mathf.Clamp(clamped.z, -bound, bound);
                moved = true;
            }

            if (!moved)
                break;
        }

        clamped.y = point.y;
        return clamped;
    }

    /// <summary>
    /// Tests whether a point lies inside a rotated rectangle after applying an optional margin.
    /// </summary>
    static bool ContainsRect(RectZone zone, Vector3 point, float margin)
    {
        Vector3 local = ToLocal(zone, point);
        Vector2 half = zone.Size * 0.5f + new Vector2(margin, margin);
        half.x = Mathf.Max(0.05f, half.x);
        half.y = Mathf.Max(0.05f, half.y);
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
    }

    /// <summary>
    /// Clamps a point to the interior of a rotated rectangle while preserving its current height.
    /// </summary>
    static Vector3 ClampInsideRect(RectZone zone, Vector3 point, float inset)
    {
        Vector3 local = ToLocal(zone, point);
        Vector2 half = zone.Size * 0.5f;
        float maxX = Mathf.Max(0.05f, half.x - inset);
        float maxZ = Mathf.Max(0.05f, half.y - inset);
        local.x = Mathf.Clamp(local.x, -maxX, maxX);
        local.z = Mathf.Clamp(local.z, -maxZ, maxZ);
        return ToWorld(zone, local, point.y);
    }

    /// <summary>
    /// Pushes a point to the nearest edge outside a rotated rectangle to prevent ships from resting on land.
    /// </summary>
    static Vector3 PushOutsideRect(RectZone zone, Vector3 point, float margin)
    {
        Vector3 local = ToLocal(zone, point);
        Vector2 half = zone.Size * 0.5f + new Vector2(margin, margin);
        half.x = Mathf.Max(0.05f, half.x);
        half.y = Mathf.Max(0.05f, half.y);

        float left = Mathf.Abs(local.x + half.x);
        float right = Mathf.Abs(half.x - local.x);
        float bottom = Mathf.Abs(local.z + half.y);
        float top = Mathf.Abs(half.y - local.z);
        const float epsilon = 0.05f;

        if (left <= right && left <= bottom && left <= top)
            local.x = -half.x - epsilon;
        else if (right <= bottom && right <= top)
            local.x = half.x + epsilon;
        else if (bottom <= top)
            local.z = -half.y - epsilon;
        else
            local.z = half.y + epsilon;

        return ToWorld(zone, local, point.y);
    }

    /// <summary>
    /// Converts a world-space point into the local space of a rotated navigation rectangle.
    /// </summary>
    static Vector3 ToLocal(RectZone zone, Vector3 point)
    {
        return Quaternion.Euler(0f, -zone.Yaw, 0f) * (point - zone.Center);
    }

    /// <summary>
    /// Converts a local rectangle-space point back to world space while restoring the caller's height.
    /// </summary>
    static Vector3 ToWorld(RectZone zone, Vector3 local, float y)
    {
        Vector3 world = zone.Center + Quaternion.Euler(0f, zone.Yaw, 0f) * new Vector3(local.x, 0f, local.z);
        world.y = y;
        return world;
    }

    /// <summary>
    /// Uses squared planar distance so callers can compare candidates without paying for a square root.
    /// </summary>
    static float PlanarSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
