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
    };

    public static void SpawnForMap(BattleMapDefinition map)
    {
        if (map != null && map.Name == BattleMapCatalog.DefaultMapName)
            return;

        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-115f, 0f, -115f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(115f, 0f, 115f));
        Vector3 center = FindPatchCenter(map, 1, Vector3.zero);

        SpawnBaseProps(playerBase, false);
        SpawnBaseProps(enemyBase, true);

        SpawnProp("TargetMarker", "Prefabs/Props/BattlefieldProp_TargetMarker",
            center + new Vector3(-18f, 0f, 12f), 35f, 1.15f);
        SpawnProp("TargetMarker", "Prefabs/Props/BattlefieldProp_TargetMarker",
            center + new Vector3(18f, 0f, -12f), 215f, 1.15f);
        SpawnProp("VehicleWreck", "Prefabs/Props/BattlefieldProp_VehicleWreck",
            center + new Vector3(0f, 0f, 28f), -18f, 1.25f);

        // 战场氛围装饰：地图中部的"战线"区域散布路障/沙袋/砖瓦/路灯
        SpawnAmbientUrbanProps(map, center, playerBase, enemyBase);
    }

    /// <summary>战场中部沿战线散布 RetroUrbanKit 路障、沙袋墙、砖瓦废墟、路灯，营造城战氛围。</summary>
    static void SpawnAmbientUrbanProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
    {
        // 战线方向：从 playerBase 到 enemyBase 的中线
        Vector3 axis = (enemyBase - playerBase);
        Vector3 axisN = axis.normalized;
        Vector3 perp = new Vector3(-axisN.z, 0f, axisN.x); // 垂直战线方向

        var rng = new System.Random(map != null ? (map.RandomSeed * 31 + 17) : 12345);
        float halfLen = axis.magnitude * 0.35f;
        // 沿战线 ±35% 范围内布置 9 处装饰簇
        for (int i = 0; i < 9; i++)
        {
            float t = (float)(rng.NextDouble() * 2.0 - 1.0); // [-1,1]
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
                    // 损坏铁路沿战线方向（与 axis 平行更自然），yaw 取战线角度
                    float railYaw = Mathf.Atan2(axisN.x, axisN.z) * Mathf.Rad2Deg;
                    SpawnProp("DamagedRail", "Prefabs/Props/BattlefieldProp_DamagedRail", pos, railYaw + (float)(rng.NextDouble() * 30.0 - 15.0), 1.10f);
                    break;
            }
        }
    }

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
