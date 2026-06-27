using UnityEngine;

public static class BattlefieldPropSpawner
{
    public static readonly string[] GeneratedPropNames =
    {
        "SupplyCrate",
        "AmmoCrate",
        "TargetMarker",
        "VehicleWreck",
        "BarrierStrong",
        "SandbagWall",
        "BrickRubble",
        "StreetLight",
        "RuinedTower",
        "DamagedRail",
        "PirateShip",
        "Dock",
        "Palm",
        "SandRocks",
        "PirateFlag",
        "UserWarScene",
    };

    /// <summary>
    /// Spawns lightweight decorative battlefield props after terrain generation.
    /// </summary>
    public static void SpawnForMap(BattleMapDefinition map)
    {
        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-115f, 0f, -115f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(115f, 0f, 115f));
        Vector3 center = FindPatchCenter(map, 1, Vector3.zero);

        SpawnImportedWarScene(center);

        if (IsModernBattleMap(map))
        {
            SpawnBaseProps(playerBase, false);
            SpawnBaseProps(enemyBase, true);
            SpawnBaseStagingProps(playerBase, false);
            SpawnBaseStagingProps(enemyBase, true);
            SpawnCentralConflictProps(center);
            SpawnModernFieldProps(map, center, playerBase, enemyBase);
            return;
        }

        SpawnBaseProps(playerBase, false);
        SpawnBaseProps(enemyBase, true);
        SpawnCentralConflictProps(center);
        SpawnAmbientUrbanProps(map, center, playerBase, enemyBase);
    }

    static bool IsModernBattleMap(BattleMapDefinition map)
    {
        if (map == null || string.IsNullOrEmpty(map.Name))
            return false;

        return map.Name == BattleMapCatalog.DefaultMapName
            || map.Name == BattleMapCatalog.GlobalConquestName;
    }

    static void SpawnImportedWarScene(Vector3 center)
    {
        SpawnProp("UserWarScene", "Prefabs/Props/BattlefieldProp_UserWarScene",
            center + new Vector3(0f, 0f, 128f), 180f, 1f);
    }

    static void SpawnCentralConflictProps(Vector3 center)
    {
        SpawnProp("TargetMarker", "Prefabs/Props/BattlefieldProp_TargetMarker",
            center + new Vector3(-18f, 0f, 12f), 35f, 1.15f);
        SpawnProp("TargetMarker", "Prefabs/Props/BattlefieldProp_TargetMarker",
            center + new Vector3(18f, 0f, -12f), 215f, 1.15f);
        SpawnProp("VehicleWreck", "Prefabs/Props/BattlefieldProp_VehicleWreck",
            center + new Vector3(0f, 0f, 28f), -18f, 1.25f);
    }

    /// <summary>
    /// Scatters urban warfare props along the main frontline.
    /// </summary>
    static void SpawnAmbientUrbanProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
    {
        Vector3 axis = enemyBase - playerBase;
        Vector3 axisN = axis.normalized;
        Vector3 perp = new Vector3(-axisN.z, 0f, axisN.x);

        var rng = new System.Random(map != null ? (map.RandomSeed * 31 + 17) : 12345);
        float halfLen = axis.magnitude * 0.35f;
        for (int i = 0; i < 9; i++)
        {
            float t = (float)(rng.NextDouble() * 2.0 - 1.0);
            float side = (float)(rng.NextDouble() * 2.0 - 1.0) * 42f;
            Vector3 pos = center + axisN * (t * halfLen) + perp * side;
            float yaw = (float)(rng.NextDouble() * 360.0);
            int kind = rng.Next(6);
            switch (kind)
            {
                case 0:
                    SpawnProp("BarrierStrong", "Prefabs/Props/BattlefieldProp_BarrierStrong", pos, yaw, 1.15f);
                    break;
                case 1:
                    SpawnProp("SandbagWall", "Prefabs/Props/BattlefieldProp_SandbagWall", pos, yaw, 1.10f);
                    break;
                case 2:
                    SpawnProp("BrickRubble", "Prefabs/Props/BattlefieldProp_BrickRubble", pos, yaw, 1.05f);
                    break;
                case 3:
                    SpawnProp("StreetLight", "Prefabs/Props/BattlefieldProp_StreetLight", pos, yaw, 1.0f);
                    break;
                case 4:
                    SpawnProp("RuinedTower", "Prefabs/Props/BattlefieldProp_RuinedTower", pos, yaw, 1.10f);
                    break;
                case 5:
                    float railYaw = Mathf.Atan2(axisN.x, axisN.z) * Mathf.Rad2Deg;
                    SpawnProp("DamagedRail", "Prefabs/Props/BattlefieldProp_DamagedRail",
                        pos, railYaw + (float)(rng.NextDouble() * 30.0 - 15.0), 1.10f);
                    break;
            }
        }
    }

    static void SpawnModernFieldProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
    {
        Vector3 axis = enemyBase - playerBase;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.01f)
            axis = new Vector3(1f, 0f, 1f);

        Vector3 axisN = axis.normalized;
        Vector3 perp = new Vector3(-axisN.z, 0f, axisN.x);
        float roadYaw = Mathf.Atan2(axisN.x, axisN.z) * Mathf.Rad2Deg;
        float sideYaw = roadYaw + 90f;
        int seed = map != null ? (map.RandomSeed * 73 + 101) : 101;
        var rng = new System.Random(seed);

        for (int i = -2; i <= 2; i++)
        {
            Vector3 roadNode = center + axisN * (i * 30f);
            float nodeScale = i == 0 ? 1.15f : 1.0f;
            SpawnProp("BarrierStrong", "Prefabs/Props/BattlefieldProp_BarrierStrong",
                roadNode + perp * 9f, sideYaw, 1.12f * nodeScale);
            SpawnProp("BarrierStrong", "Prefabs/Props/BattlefieldProp_BarrierStrong",
                roadNode - perp * 9f, sideYaw + 180f, 1.12f * nodeScale);

            if ((i & 1) == 0)
            {
                SpawnProp("StreetLight", "Prefabs/Props/BattlefieldProp_StreetLight",
                    roadNode + perp * 15f, sideYaw, 1f);
                SpawnProp("StreetLight", "Prefabs/Props/BattlefieldProp_StreetLight",
                    roadNode - perp * 15f, sideYaw + 180f, 1f);
            }
        }

        for (int i = 0; i < 10; i++)
        {
            float along = Mathf.Lerp(-0.42f, 0.42f, i / 9f);
            float lateral = (i % 2 == 0 ? -1f : 1f) * Mathf.Lerp(18f, 30f, (i % 3) / 2f);
            Vector3 pos = center + axisN * (along * axis.magnitude) + perp * lateral;
            int kind = rng.Next(5);

            if (kind == 0)
            {
                SpawnProp("SandbagWall", "Prefabs/Props/BattlefieldProp_SandbagWall", pos, sideYaw, 1.08f);
                continue;
            }

            if (kind == 1)
            {
                SpawnProp("BrickRubble", "Prefabs/Props/BattlefieldProp_BrickRubble",
                    pos, (float)rng.NextDouble() * 360f, 1.08f);
                continue;
            }

            if (kind == 2)
            {
                SpawnProp("VehicleWreck", "Prefabs/Props/BattlefieldProp_VehicleWreck",
                    pos, roadYaw + (float)(rng.NextDouble() * 30.0 - 15.0), 1.08f);
                continue;
            }

            if (kind == 3)
            {
                SpawnProp("BarrierStrong", "Prefabs/Props/BattlefieldProp_BarrierStrong", pos, sideYaw, 1.05f);
                SpawnProp("AmmoCrate", "Prefabs/Props/BattlefieldProp_AmmoCrate",
                    pos + axisN * 5f, roadYaw, 0.98f);
                continue;
            }

            SpawnProp("RuinedTower", "Prefabs/Props/BattlefieldProp_RuinedTower",
                pos, (float)rng.NextDouble() * 360f, 1.02f);
        }

        SpawnAmbientUrbanProps(map, center, playerBase, enemyBase);
    }

    /// <summary>
    /// Places a small prop cluster near one faction base to suggest supply lines and recent combat.
    /// </summary>
    static void SpawnBaseProps(Vector3 baseCenter, bool enemy)
    {
        float side = enemy ? -1f : 1f;
        SpawnProp("SupplyCrate", "Prefabs/Props/BattlefieldProp_SupplyCrate",
            baseCenter + new Vector3(side * 24f, 0f, -side * 12f), enemy ? 45f : 225f, 1f);
        SpawnProp("AmmoCrate", "Prefabs/Props/BattlefieldProp_AmmoCrate",
            baseCenter + new Vector3(side * 15f, 0f, -side * 24f), enemy ? 18f : 198f, 1f);
        SpawnProp("VehicleWreck", "Prefabs/Props/BattlefieldProp_VehicleWreck",
            baseCenter + new Vector3(-side * 28f, 0f, side * 18f), enemy ? 140f : -40f, 1f);
    }

    static void SpawnBaseStagingProps(Vector3 baseCenter, bool enemy)
    {
        float side = enemy ? -1f : 1f;
        float facing = enemy ? 45f : 225f;
        float counterFacing = facing + 180f;

        SpawnProp("BarrierStrong", "Prefabs/Props/BattlefieldProp_BarrierStrong",
            baseCenter + new Vector3(side * 26f, 0f, side * 8f), facing, 1.18f);
        SpawnProp("BarrierStrong", "Prefabs/Props/BattlefieldProp_BarrierStrong",
            baseCenter + new Vector3(side * 26f, 0f, -side * 8f), counterFacing, 1.18f);
        SpawnProp("SandbagWall", "Prefabs/Props/BattlefieldProp_SandbagWall",
            baseCenter + new Vector3(side * 12f, 0f, side * 26f), facing - 90f, 1.08f);
        SpawnProp("SandbagWall", "Prefabs/Props/BattlefieldProp_SandbagWall",
            baseCenter + new Vector3(-side * 12f, 0f, -side * 26f), facing + 90f, 1.08f);
        SpawnProp("StreetLight", "Prefabs/Props/BattlefieldProp_StreetLight",
            baseCenter + new Vector3(-side * 24f, 0f, side * 22f), facing, 1f);
        SpawnProp("StreetLight", "Prefabs/Props/BattlefieldProp_StreetLight",
            baseCenter + new Vector3(side * 22f, 0f, -side * 24f), counterFacing, 1f);
    }

    /// <summary>
    /// Finds the center of a terrain patch by palette index, falling back to a supplied default position.
    /// </summary>
    static Vector3 FindPatchCenter(BattleMapDefinition map, int paletteIndex, Vector3 fallback)
    {
        if (map != null && map.Patches != null)
        {
            foreach (var patch in map.Patches)
            {
                if (patch.PaletteIndex == paletteIndex)
                    return patch.Center;
            }
        }
        return fallback;
    }

    /// <summary>
    /// Loads and instantiates one decorative prop prefab at the requested position, rotation, and scale.
    /// </summary>
    static void SpawnProp(string name, string resourcePath, Vector3 position, float yaw, float scale)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("[BattlefieldPropSpawner] Downloaded prop prefab missing, skipped: " + resourcePath);
            return;
        }

        GameObject prop = Object.Instantiate(prefab);
        prop.name = name;
        prop.transform.position = new Vector3(position.x, 0f, position.z);
        prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        prop.transform.localScale = Vector3.one * scale;
    }
}
