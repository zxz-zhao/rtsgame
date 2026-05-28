using System.IO;
using System.Collections.Generic;
using UnityEngine;

public static class RuntimeBattleMapBuilder
{
    const string KenneyPirateObjPath = "Assets/External/MilitaryModels/Kenney/Extracted/Models/OBJ format/";
    const string KenneyPirateTexturePath = KenneyPirateObjPath + "Textures/colormap.png";
    const string KenneyNatureObjPath = "Assets/External/Kenney/NatureKit/Models/OBJ format/";
    const string KenneySurvivalObjPath = "Assets/External/Kenney/SurvivalKit/Models/OBJ format/";
    const string KenneySurvivalTexturePath = KenneySurvivalObjPath + "Textures/colormap.png";
    const string KenneyCastleObjPath = "Assets/External/Kenney/CastleKit/Models/OBJ format/";
    const string KenneyCastleTexturePath = KenneyCastleObjPath + "Textures/colormap.png";
    const float DefaultMapTileStep = 18f;
    const int MaxExternalTilesPerFeature = 96;

    static readonly Dictionary<string, GameObject> ObjPrototypeCache = new Dictionary<string, GameObject>();

    static readonly string[] RuntimePrefixes =
    {
        "TerrainPatch", "Road_", "RoadEdge_", "Water_", "MapLayer_", "Wall", "SeaRoute_", "SeaChart_", "SeaCompass_"
    };

    static readonly string[] RuntimeNames =
    {
        "Rock", "Tree", "MapScenery", "Leaves", "RuinWall", "Debris", "Sandbag", "BagMark", "Post",
        "SupplyCrate", "AmmoCrate", "TargetMarker", "VehicleWreck",
        "BarrierStrong", "SandbagWall", "BrickRubble", "StreetLight",
        "RuinedTower", "DamagedRail",
        "SeaShip", "SeaDock", "SeaPalm", "SeaRocks", "SeaFlag", "SeaBuoy", "SeaWreck", "SeaBoat", "SeaPlatform"
    };

    public static void Rebuild(BattleMapDefinition map)
    {
        ClearGeneratedObjects();

        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            ground.transform.localScale = Vector3.one * (BattleMapCatalog.MapHalfSize / 5f);
            var renderer = ground.GetComponent<Renderer>();
            // Ground 只保留点击/导航碰撞面；可见地表由下载的 Kenney 地图块铺出来。
            if (renderer != null) renderer.enabled = false;
        }

        SpawnFlatFeature("TerrainPatch_Background",
            Vector3.zero,
            new Vector2(BattleMapCatalog.MapHalfSize * 2f, BattleMapCatalog.MapHalfSize * 2f),
            0f,
            map.GroundColor,
            0.012f,
            0.03f,
            0.12f);
        SpawnMapDetails(map);
        if (UseCleanReadableMap(map))
            SpawnCleanReadableMapLayers(map);

        float mapHalf = BattleMapCatalog.MapHalfSize;
        SpawnWall("WallN", new Vector3(0f, 1.5f, mapHalf + 5f), new Vector3(mapHalf * 2f + 10f, 3f, 5f), map.WallColor);
        SpawnWall("WallS", new Vector3(0f, 1.5f, -mapHalf - 5f), new Vector3(mapHalf * 2f + 10f, 3f, 5f), map.WallColor);
        SpawnWall("WallE", new Vector3(mapHalf + 5f, 1.5f, 0f), new Vector3(5f, 3f, mapHalf * 2f + 10f), map.WallColor);
        SpawnWall("WallW", new Vector3(-mapHalf - 5f, 1.5f, 0f), new Vector3(5f, 3f, mapHalf * 2f + 10f), map.WallColor);

        foreach (var pos in map.RockClusters) SpawnRockCluster(pos, map.RockColor);
        foreach (var pos in map.TreePositions) SpawnTree(pos, map.TrunkColor, map.FoliageColor);
        SpawnAmbientScenery(map);
        foreach (var ring in map.SandbagRings) SpawnSandbagRing(ring.Center, ring.Radius, ring.Count, ring.IsEnemy);
        foreach (var ruin in map.RuinWalls) SpawnRuinWall(ruin.Center, ruin.Angle, ruin.Segments, map.RuinColor);
        BattlefieldPropSpawner.SpawnForMap(map);
        if (map.Name == BattleMapCatalog.SeaChartName)
            SpawnSeaChartDetails(map);
    }

    public static void AddSeaChartDetails(BattleMapDefinition map)
    {
        SpawnSeaChartDetails(map);
    }

    static void ClearGeneratedObjects()
    {
        var all = Object.FindObjectsOfType<Transform>();
        foreach (var t in all)
        {
            if (t == null || t.parent != null) continue;
            if (ShouldClear(t.gameObject.name))
            {
                if (Application.isPlaying) Object.Destroy(t.gameObject);
                else Object.DestroyImmediate(t.gameObject);
            }
        }
    }

    static bool ShouldClear(string name)
    {
        foreach (var exact in RuntimeNames)
            if (name == exact) return true;

        foreach (var prefix in RuntimePrefixes)
            if (name.StartsWith(prefix)) return true;

        return false;
    }

    static Material MakeMat(Color color, float metallic = 0f, float glossiness = 0.2f)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", glossiness);
        return mat;
    }

    static readonly Dictionary<int, Texture2D> ReadableSurfaceTextureCache = new Dictionary<int, Texture2D>();

    static Material MakeReadableSurfaceMat(Color color)
    {
        color = MakeReadableSurfaceColor(color);
        Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        if (mat.HasProperty("_MainTex"))
            mat.mainTexture = GetReadableSurfaceTexture(color);
        if (mat.HasProperty("_Color"))
            mat.color = mat.HasProperty("_MainTex") ? Color.white : color;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f);
        return mat;
    }

    static Color MakeReadableSurfaceColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        float minReadableValue = v < 0.34f ? 0.44f : 0.50f;
        v = Mathf.Clamp01(Mathf.Max(v + 0.045f, minReadableValue));
        s = Mathf.Clamp01(s * 0.92f);
        Color readable = Color.HSVToRGB(h, s, v);
        readable = Color.Lerp(readable, Color.white, 0.035f);
        readable.a = color.a;
        return readable;
    }

    static Texture2D GetReadableSurfaceTexture(Color color)
    {
        Color32 c32 = color;
        int key = (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
        Texture2D cached;
        if (ReadableSurfaceTextureCache.TryGetValue(key, out cached) && cached != null)
            return cached;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.name = "ReadableSurfaceTexture";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float checker = (((x / 8) + (y / 8)) & 1) == 0 ? 0.025f : -0.025f;
                float grain = (Mathf.PerlinNoise((x + c32.r) * 0.19f, (y + c32.g) * 0.19f) - 0.5f) * 0.09f;
                texture.SetPixel(x, y, ShiftColor(color, checker + grain));
            }
        }

        texture.Apply(false, true);
        ReadableSurfaceTextureCache[key] = texture;
        return texture;
    }

    static Color ShiftColor(Color color, float amount)
    {
        Color target = amount >= 0f ? Color.white : Color.black;
        Color shifted = Color.Lerp(color, target, Mathf.Abs(amount));
        shifted.a = color.a;
        return shifted;
    }

    static void SpawnMapDetails(BattleMapDefinition map)
    {
        foreach (var patch in map.Patches)
        {
            SpawnFlatFeature("TerrainPatch" + patch.PaletteIndex + "_" + patch.Name,
                patch.Center, patch.Size, patch.Angle, map.GetPatchColor(patch.PaletteIndex), 0.018f, 0.03f, 0.12f);
        }

        foreach (var road in map.Roads)
        {
            SpawnFlatFeature("RoadEdge_" + road.Name,
                road.Center, road.Size + new Vector2(5f, 6f), road.Angle, map.RoadEdgeColor, 0.023f, 0.025f, 0.12f);
            SpawnFlatFeature("Road_" + road.Name,
                road.Center, road.Size, road.Angle, map.RoadColor, 0.032f, 0.025f, 0.08f);
        }

        foreach (var water in map.Waters)
        {
            SpawnFlatFeature("Water_" + water.Name,
                water.Center, water.Size, water.Angle, map.WaterColor, 0.045f, 0.03f, 0.75f);
        }
    }

    static void SpawnCleanReadableMapLayers(BattleMapDefinition map)
    {
        Color shore = new Color(0.56f, 0.52f, 0.34f, 1f);
        Color shallowWater = Color.Lerp(map.WaterColor, new Color(0.10f, 0.42f, 0.58f, 1f), 0.45f);
        Color deepWater = Color.Lerp(map.WaterColor, Color.black, 0.28f);
        Color forestCore = Color.Lerp(map.PatchAColor, Color.black, 0.18f);
        Color forestEdge = Color.Lerp(map.PatchAColor, map.GroundColor, 0.36f);
        Color openLand = Color.Lerp(map.GroundColor, map.PatchBColor, 0.12f);

        SpawnFlatFeature("MapLayer_OpenLandCenter", Vector3.zero,
            new Vector2(246f, 246f), 0f, openLand, 0.023f, 0.016f, 0.04f);

        SpawnFlatFeature("MapLayer_ShallowWaterWest", new Vector3(-163f, 0f, 0f),
            new Vector2(10f, 396f), 0f, shallowWater, 0.058f, 0.014f, 0.05f);
        SpawnFlatFeature("MapLayer_ShallowWaterEast", new Vector3(163f, 0f, 0f),
            new Vector2(10f, 396f), 0f, shallowWater, 0.058f, 0.014f, 0.05f);
        SpawnFlatFeature("MapLayer_ShallowWaterNorth", new Vector3(0f, 0f, 163f),
            new Vector2(252f, 10f), 0f, shallowWater, 0.058f, 0.014f, 0.05f);
        SpawnFlatFeature("MapLayer_ShallowWaterSouth", new Vector3(0f, 0f, -163f),
            new Vector2(252f, 10f), 0f, shallowWater, 0.058f, 0.014f, 0.05f);

        SpawnFlatFeature("MapLayer_DeepWaterWest", new Vector3(-190f, 0f, 0f),
            new Vector2(28f, 396f), 0f, deepWater, 0.060f, 0.012f, 0.06f);
        SpawnFlatFeature("MapLayer_DeepWaterEast", new Vector3(190f, 0f, 0f),
            new Vector2(28f, 396f), 0f, deepWater, 0.060f, 0.012f, 0.06f);
        SpawnFlatFeature("MapLayer_DeepWaterNorth", new Vector3(0f, 0f, 190f),
            new Vector2(252f, 24f), 0f, deepWater, 0.060f, 0.012f, 0.06f);
        SpawnFlatFeature("MapLayer_DeepWaterSouth", new Vector3(0f, 0f, -190f),
            new Vector2(252f, 24f), 0f, deepWater, 0.060f, 0.012f, 0.06f);

        SpawnFlatFeature("MapLayer_ShoreWest", new Vector3(-145f, 0f, 0f),
            new Vector2(7f, 396f), 0f, shore, 0.057f, 0.014f, 0.02f);
        SpawnFlatFeature("MapLayer_ShoreEast", new Vector3(145f, 0f, 0f),
            new Vector2(7f, 396f), 0f, shore, 0.057f, 0.014f, 0.02f);
        SpawnFlatFeature("MapLayer_ShoreNorth", new Vector3(0f, 0f, 145f),
            new Vector2(252f, 7f), 0f, shore, 0.057f, 0.014f, 0.02f);
        SpawnFlatFeature("MapLayer_ShoreSouth", new Vector3(0f, 0f, -145f),
            new Vector2(252f, 7f), 0f, shore, 0.057f, 0.014f, 0.02f);

        SpawnFlatFeature("MapLayer_ForestWestCore", new Vector3(-118f, 0f, 34f),
            new Vector2(54f, 118f), -8f, forestCore, 0.035f, 0.014f, 0.02f);
        SpawnFlatFeature("MapLayer_ForestEastCore", new Vector3(118f, 0f, -34f),
            new Vector2(54f, 118f), -8f, forestCore, 0.035f, 0.014f, 0.02f);
        SpawnFlatFeature("MapLayer_ForestNorthCore", new Vector3(-18f, 0f, 132f),
            new Vector2(126f, 36f), 5f, forestCore, 0.035f, 0.014f, 0.02f);
        SpawnFlatFeature("MapLayer_ForestSouthCore", new Vector3(18f, 0f, -132f),
            new Vector2(126f, 36f), 5f, forestCore, 0.035f, 0.014f, 0.02f);

        SpawnFlatFeature("MapLayer_ForestWestEdge", new Vector3(-118f, 0f, 34f),
            new Vector2(72f, 142f), -8f, forestEdge, 0.032f, 0.012f, 0.02f);
        SpawnFlatFeature("MapLayer_ForestEastEdge", new Vector3(118f, 0f, -34f),
            new Vector2(72f, 142f), -8f, forestEdge, 0.032f, 0.012f, 0.02f);
        SpawnFlatFeature("MapLayer_ForestNorthEdge", new Vector3(-18f, 0f, 132f),
            new Vector2(150f, 52f), 5f, forestEdge, 0.032f, 0.012f, 0.02f);
        SpawnFlatFeature("MapLayer_ForestSouthEdge", new Vector3(18f, 0f, -132f),
            new Vector2(150f, 52f), 5f, forestEdge, 0.032f, 0.012f, 0.02f);
    }

    static void SpawnSeaChartDetails(BattleMapDefinition map)
    {
        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-120f, 0f, -118f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(120f, 0f, 118f));
        Vector3 center = FindPatchCenter(map, 1, Vector3.zero);
        Vector3 northHarbor = new Vector3(-78f, 0f, 120f);
        Vector3 southHarbor = new Vector3(78f, 0f, -120f);

        Color ink = new Color(0.88f, 0.78f, 0.48f, 1f);
        Color dimInk = new Color(0.48f, 0.56f, 0.43f, 1f);

        SpawnRouteBetween("SeaRoute_PlayerToCompass", playerBase, center, 3.2f, ink, 0.058f);
        SpawnRouteBetween("SeaRoute_CompassToEnemy", center, enemyBase, 3.2f, ink, 0.058f);
        SpawnRouteBetween("SeaRoute_NorthHarbor", northHarbor, center, 2.1f, dimInk, 0.056f);
        SpawnRouteBetween("SeaRoute_SouthHarbor", southHarbor, center, 2.1f, dimInk, 0.056f);
        SpawnSeaGrid(dimInk);
        SpawnSeaCompass(center, ink);

        SpawnSeaProp("SeaShip", "Prefabs/Props/BattlefieldProp_PirateShip", center + new Vector3(42f, 0f, 30f), 38f, 1.25f);
        SpawnSeaProp("SeaShip", "Prefabs/Props/BattlefieldProp_PirateShip", center + new Vector3(-44f, 0f, -24f), 218f, 0.95f);
        SpawnSeaProp("SeaDock", "Prefabs/Props/BattlefieldProp_Dock", playerBase + new Vector3(30f, 0f, 20f), 45f, 1.20f);
        SpawnSeaProp("SeaDock", "Prefabs/Props/BattlefieldProp_Dock", enemyBase + new Vector3(-30f, 0f, -20f), 225f, 1.20f);
        SpawnSeaProp("SeaFlag", "Prefabs/Props/BattlefieldProp_PirateFlag", center + new Vector3(-18f, 0f, 16f), 18f, 0.9f);

        Vector3[] palms =
        {
            playerBase + new Vector3(-32f,0f,-18f), playerBase + new Vector3(20f,0f,-32f),
            enemyBase + new Vector3(32f,0f,18f), enemyBase + new Vector3(-20f,0f,32f),
            northHarbor + new Vector3(-18f,0f,16f), southHarbor + new Vector3(18f,0f,-16f),
        };
        for (int i = 0; i < palms.Length; i++)
            SpawnSeaProp("SeaPalm", "Prefabs/Props/BattlefieldProp_Palm", palms[i], 40f + i * 47f, 0.95f + (i % 2) * 0.18f);

        Vector3[] rocks =
        {
            new Vector3(-158f,0f,40f), new Vector3(158f,0f,-40f),
            new Vector3(-96f,0f,126f), new Vector3(96f,0f,-126f),
        };
        for (int i = 0; i < rocks.Length; i++)
            SpawnSeaProp("SeaRocks", "Prefabs/Props/BattlefieldProp_SandRocks", rocks[i], i * 63f, 1.05f);

        SpawnSeaProp("SeaWreck", null, center + new Vector3(-66f, 0f, 42f), 125f, 0.95f);
        SpawnSeaProp("SeaBoat", null, northHarbor + new Vector3(28f, 0f, -10f), 210f, 0.75f);
        SpawnSeaProp("SeaBoat", null, southHarbor + new Vector3(-28f, 0f, 10f), 30f, 0.75f);
        SpawnSeaProp("SeaPlatform", null, center + new Vector3(0f, 0f, -48f), 12f, 0.82f);

        SpawnBuoy(new Vector3(-30f, 0f, 74f), ink);
        SpawnBuoy(new Vector3(30f, 0f, -74f), ink);
        SpawnBuoy(new Vector3(-112f, 0f, 12f), dimInk);
        SpawnBuoy(new Vector3(112f, 0f, -12f), dimInk);
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

    static void SpawnRouteBetween(string name, Vector3 a, Vector3 b, float width, Color color, float y)
    {
        Vector3 delta = b - a;
        float length = new Vector2(delta.x, delta.z).magnitude;
        if (length < 0.1f) return;

        Vector3 center = (a + b) * 0.5f;
        float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        SpawnFlatFeature(name, center, new Vector2(width, length), yaw, color, y, 0.018f, 0.36f);

        int ticks = Mathf.Clamp(Mathf.RoundToInt(length / 38f), 2, 8);
        Vector3 dir = delta.normalized;
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
        for (int i = 1; i < ticks; i++)
        {
            float t = i / (float)ticks;
            Vector3 tickCenter = Vector3.Lerp(a, b, t) + perp * (i % 2 == 0 ? width * 1.1f : -width * 1.1f);
            SpawnFlatFeature("SeaChart_Tick_" + name + "_" + i, tickCenter, new Vector2(1.2f, 8f), yaw + 90f, color, y + 0.004f, 0.016f, 0.25f);
        }
    }

    static void SpawnSeaGrid(Color color)
    {
        for (int i = -2; i <= 2; i++)
        {
            float coord = i * 72f;
            SpawnFlatFeature("SeaChart_GridX_" + i, new Vector3(coord, 0f, 0f), new Vector2(0.8f, 330f), 0f, color, 0.052f, 0.012f, 0.18f);
            SpawnFlatFeature("SeaChart_GridZ_" + i, new Vector3(0f, 0f, coord), new Vector2(0.8f, 330f), 90f, color, 0.052f, 0.012f, 0.18f);
        }
    }

    static void SpawnSeaCompass(Vector3 center, Color color)
    {
        SpawnFlatFeature("SeaCompass_Disc", center, new Vector2(30f, 30f), 0f,
            new Color(0.18f, 0.34f, 0.34f, 1f), 0.07f, 0.018f, 0.25f);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            bool cardinal = i % 2 == 0;
            SpawnFlatFeature("SeaCompass_Ray_" + i, center, new Vector2(cardinal ? 2.4f : 1.2f, cardinal ? 74f : 54f),
                angle, color, 0.09f, 0.018f, 0.22f);
        }
    }

    static void SpawnSeaProp(string name, string resourcePath, Vector3 position, float yaw, float scale)
    {
        GameObject prefab = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<GameObject>(resourcePath);
        GameObject prop = prefab != null ? Object.Instantiate(prefab) : TryLoadDownloadedSeaModel(name);
        if (prop == null)
        {
            Debug.LogWarning("[RuntimeBattleMapBuilder] Missing downloaded sea model: " + name);
            return;
        }
        prop.name = name;
        prop.transform.position = new Vector3(position.x, 0.08f, position.z);
        prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        prop.transform.localScale = Vector3.one * scale;
    }

    static GameObject TryLoadDownloadedSeaModel(string name)
    {
        var root = new GameObject(name + "_Downloaded");
        bool loaded = false;

        if (name == "SeaShip")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:ship-pirate-medium", "PirateShip",
                Vector3.zero, new Vector3(0f, 180f, 0f), Vector3.one,
                new Color(0.72f, 0.56f, 0.34f), true);
        }
        else if (name == "SeaDock")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:structure-platform-dock", "Dock",
                Vector3.zero, Vector3.zero, Vector3.one,
                new Color(0.55f, 0.38f, 0.20f), true);
            loaded |= AddDownloadedObjPart(root, "pirate:barrel", "DockBarrel",
                new Vector3(1.4f, 0.15f, -0.8f), new Vector3(0f, 20f, 0f), Vector3.one * 0.42f,
                new Color(0.45f, 0.30f, 0.16f), true);
            loaded |= AddDownloadedObjPart(root, "pirate:chest", "DockChest",
                new Vector3(-1.1f, 0.15f, 0.7f), new Vector3(0f, -25f, 0f), Vector3.one * 0.42f,
                new Color(0.70f, 0.48f, 0.22f), true);
        }
        else if (name == "SeaPalm")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:palm-detailed-bend", "Palm",
                Vector3.zero, Vector3.zero, Vector3.one,
                new Color(0.18f, 0.58f, 0.26f), true);
        }
        else if (name == "SeaRocks")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:rocks-sand-a", "SandRockA",
                new Vector3(-0.6f, 0f, 0.1f), new Vector3(0f, 20f, 0f), Vector3.one * 1.1f,
                new Color(0.66f, 0.56f, 0.38f), true);
            loaded |= AddDownloadedObjPart(root, "pirate:rocks-sand-b", "SandRockB",
                new Vector3(0.8f, 0f, -0.4f), new Vector3(0f, -35f, 0f), Vector3.one * 0.9f,
                new Color(0.62f, 0.52f, 0.35f), true);
        }
        else if (name == "SeaFlag")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:flag-pirate-high", "PirateFlag",
                Vector3.zero, Vector3.zero, Vector3.one,
                new Color(0.90f, 0.82f, 0.50f), true);
        }
        else if (name == "SeaWreck")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:ship-wreck", "ShipWreck",
                Vector3.zero, new Vector3(0f, 180f, 0f), Vector3.one,
                new Color(0.58f, 0.42f, 0.26f), true);
            loaded |= AddDownloadedObjPart(root, "pirate:rocks-sand-c", "WreckRocks",
                new Vector3(1.8f, 0f, -0.8f), new Vector3(0f, 25f, 0f), Vector3.one * 0.75f,
                new Color(0.66f, 0.56f, 0.38f), true);
        }
        else if (name == "SeaBoat")
        {
            loaded |= AddDownloadedObjPart(root, Random.value > 0.5f ? "pirate:boat-row-large" : "pirate:boat-row-small", "RowBoat",
                Vector3.zero, Vector3.zero, Vector3.one,
                new Color(0.60f, 0.43f, 0.26f), true);
        }
        else if (name == "SeaPlatform")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:structure-platform-small", "SeaPlatform",
                Vector3.zero, Vector3.zero, Vector3.one,
                new Color(0.52f, 0.38f, 0.22f), true);
            loaded |= AddDownloadedObjPart(root, "pirate:barrel", "SeaPlatformBarrel",
                new Vector3(-0.75f, 0.16f, 0.45f), new Vector3(0f, 15f, 0f), Vector3.one * 0.34f,
                new Color(0.48f, 0.30f, 0.16f), true);
        }
        else if (name == "SeaBuoy")
        {
            loaded |= AddDownloadedObjPart(root, "pirate:barrel", "BuoyBarrel",
                Vector3.zero, new Vector3(0f, 0f, 90f), Vector3.one * 0.65f,
                new Color(0.92f, 0.72f, 0.18f), true);
            loaded |= AddDownloadedObjPart(root, "pirate:flag-pennant", "BuoyPennant",
                new Vector3(0f, 1.0f, 0f), new Vector3(0f, 25f, 0f), Vector3.one * 0.45f,
                new Color(0.94f, 0.82f, 0.28f), true);
        }

        if (loaded)
            return root;

        if (Application.isPlaying) Object.Destroy(root);
        else Object.DestroyImmediate(root);
        return null;
    }

    static bool AddDownloadedObjPart(GameObject parent, string objName, string childName,
        Vector3 localPosition, Vector3 localRotation, Vector3 localScale, Color tint, bool tintTexture)
    {
        return CreateDownloadedObjPart(parent, objName, childName, localPosition, localRotation, localScale, tint, tintTexture) != null;
    }

    static GameObject CreateDownloadedObjPart(GameObject parent, string objName, string childName,
        Vector3 localPosition, Vector3 localRotation, Vector3 localScale, Color tint, bool tintTexture)
    {
        GameObject part = InstantiateDownloadedResourceModel(objName, tint, tintTexture);
        if (part == null)
        {
            part = InstantiateLooseObjModel(objName, tint, tintTexture);
        }

        if (part == null)
            return null;

        part.name = childName;
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.Euler(localRotation);
        part.transform.localScale = localScale;
        return part;
    }

    static GameObject InstantiateDownloadedResourceModel(string objName, Color tint, bool tintRenderers)
    {
        string resourceName = BattleMapTileResourceName(objName);
        if (string.IsNullOrEmpty(resourceName))
            return null;

        // Android 运行时不能直接读取 APK 内 Assets/External 的裸 OBJ，所以优先使用编辑器预生成的下载模型预制体。
        GameObject prefab = Resources.Load<GameObject>("Prefabs/MapTiles/" + resourceName);
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(prefab);
        ApplyRuntimeTint(instance, tint, tintRenderers);
        return instance;
    }

    static GameObject InstantiateLooseObjModel(string objName, Color tint, bool tintTexture)
    {
        string assetPath = ResolveDownloadedObjAssetPath(objName);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        string path = ToProjectFilePath(assetPath);
        Material mat = MakeDownloadedModelMat(tint, tintTexture, assetPath);
        return InstantiateDownloadedObj(path, mat);
    }

    static void ApplyRuntimeTint(GameObject root, Color tint, bool tintRenderers)
    {
        if (!tintRenderers || root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] materials = renderers[r].sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;

                Material copy = new Material(source);
                if (copy.HasProperty("_Color"))
                    copy.color = Color.Lerp(copy.color, tint, 0.45f);
                else if (copy.HasProperty("_BaseColor"))
                    copy.SetColor("_BaseColor", Color.Lerp(copy.GetColor("_BaseColor"), tint, 0.45f));

                if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", 0f);
                if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", 0.35f);
                if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", 0.35f);
                materials[i] = copy;
            }
            renderers[r].sharedMaterials = materials;
        }
    }

    static string BattleMapTileResourceName(string objName)
    {
        if (string.IsNullOrEmpty(objName))
            return null;
        return objName.Replace(':', '_').Replace('-', '_').Replace('.', '_').Replace(' ', '_');
    }

    static string ResolveDownloadedObjAssetPath(string objName)
    {
        if (string.IsNullOrEmpty(objName))
            return null;

        int prefix = objName.IndexOf(':');
        if (prefix > 0)
        {
            string kit = objName.Substring(0, prefix).ToLowerInvariant();
            string model = objName.Substring(prefix + 1);
            string path = ResolveDownloadedObjAssetPathForKit(kit, model);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        string[] paths =
        {
            KenneyNatureObjPath + objName + ".obj",
            KenneySurvivalObjPath + objName + ".obj",
            KenneyCastleObjPath + objName + ".obj",
            KenneyPirateObjPath + objName + ".obj",
        };

        for (int i = 0; i < paths.Length; i++)
        {
            if (File.Exists(ToProjectFilePath(paths[i])))
                return paths[i];
        }
        return null;
    }

    static string ResolveDownloadedObjAssetPathForKit(string kit, string model)
    {
        string basePath = null;
        if (kit == "nature") basePath = KenneyNatureObjPath;
        else if (kit == "survival") basePath = KenneySurvivalObjPath;
        else if (kit == "castle") basePath = KenneyCastleObjPath;
        else if (kit == "pirate") basePath = KenneyPirateObjPath;

        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(model))
            return null;

        string path = basePath + model + ".obj";
        return File.Exists(ToProjectFilePath(path)) ? path : null;
    }

    static Material MakeDownloadedModelMat(Color tint, bool useTexture, string assetPath)
    {
        var mat = MakeMat(tint, 0f, 0.35f);
        if (!useTexture)
            return mat;

        string textureAssetPath = ResolveDownloadedTextureAssetPath(assetPath);
        if (string.IsNullOrEmpty(textureAssetPath))
            return mat;

        string texturePath = ToProjectFilePath(textureAssetPath);
        if (!File.Exists(texturePath))
            return mat;

        byte[] bytes = File.ReadAllBytes(texturePath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!texture.LoadImage(bytes))
            return mat;

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        if (mat.HasProperty("_MainTex"))
            mat.mainTexture = texture;
        return mat;
    }

    static string ResolveDownloadedTextureAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        if (assetPath.StartsWith(KenneyPirateObjPath, System.StringComparison.OrdinalIgnoreCase))
            return KenneyPirateTexturePath;
        if (assetPath.StartsWith(KenneySurvivalObjPath, System.StringComparison.OrdinalIgnoreCase))
            return KenneySurvivalTexturePath;
        if (assetPath.StartsWith(KenneyCastleObjPath, System.StringComparison.OrdinalIgnoreCase))
            return KenneyCastleTexturePath;
        return null;
    }

    static string ToProjectFilePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        if (!assetPath.StartsWith("Assets/"))
            return assetPath;

        string relative = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Application.dataPath, relative);
    }

    static void SpawnBuoy(Vector3 position, Color color)
    {
        var buoy = TryLoadDownloadedSeaModel("SeaBuoy");
        if (buoy == null)
        {
            Debug.LogWarning("[RuntimeBattleMapBuilder] Missing downloaded sea model: SeaBuoy");
            return;
        }
        buoy.name = "SeaBuoy";
        buoy.transform.position = position + Vector3.up * 0.8f;
        buoy.transform.localScale = Vector3.one * 1.15f;
    }

    static void DestroyCollider(GameObject go)
    {
        Collider collider = go != null ? go.GetComponent<Collider>() : null;
        if (collider == null) return;
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
    }

    static void SpawnFlatFeature(string name, Vector3 center, Vector2 size, float yaw,
        Color color, float y, float height, float glossiness)
    {
        var root = new GameObject(name);
        root.transform.position = new Vector3(center.x, y, center.z);
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (UseSimpleReadableSurface(name))
        {
            SpawnSimpleFlatBlock(root.transform, size, color, height, glossiness);
            return;
        }

        string[] variants = PickFlatFeatureModels(name);
        float step = PickTileStep(name, size);
        int countX = Mathf.Max(1, Mathf.CeilToInt(size.x / step));
        int countZ = Mathf.Max(1, Mathf.CeilToInt(size.y / step));
        float totalTiles = countX * countZ;
        if (totalTiles > MaxExternalTilesPerFeature)
        {
            float scale = Mathf.Sqrt(totalTiles / MaxExternalTilesPerFeature);
            countX = Mathf.Max(1, Mathf.CeilToInt(countX / scale));
            countZ = Mathf.Max(1, Mathf.CeilToInt(countZ / scale));
            step = Mathf.Max(size.x / countX, size.y / countZ);
        }

        for (int x = 0; x < countX; x++)
        {
            for (int z = 0; z < countZ; z++)
            {
                string model = variants[(x + z) % variants.Length];
                float lx = ((x + 0.5f) / countX - 0.5f) * size.x;
                float lz = ((z + 0.5f) / countZ - 0.5f) * size.y;
                float jitter = Mathf.Min(step * 0.08f, 1.1f);
                Vector3 localPos = new Vector3(
                    lx + Random.Range(-jitter, jitter),
                    0f,
                    lz + Random.Range(-jitter, jitter));
                float localYaw = Random.Range(-10f, 10f) + ((x + z) % 2 == 0 ? 0f : 180f);
                float scale = PickTileScale(name, step) * Random.Range(0.92f, 1.08f);
                GameObject tile = CreateDownloadedObjPart(root, model, model,
                    localPos,
                    new Vector3(0f, localYaw, 0f),
                    Vector3.one * scale,
                    color,
                    true);
                if (tile != null)
                {
                    float cellX = size.x / countX;
                    float cellZ = size.y / countZ;
                    ScaleDownloadedPartToFootprint(tile, cellX * 1.18f, cellZ * 1.18f, 0.18f);
                    DropDownloadedPartToSurface(tile, y);
                    DisableDownloadedTileColliders(tile);
                }
            }
        }
    }

    static bool UseSimpleReadableSurface(string name)
    {
        return name.Contains("Background")
            || name.StartsWith("MapLayer_")
            || name.StartsWith("TerrainPatch")
            || name.StartsWith("Road_")
            || name.StartsWith("RoadEdge_")
            || name.StartsWith("Water_");
    }

    static void SpawnSimpleFlatBlock(Transform parent, Vector2 size, Color color, float height, float glossiness)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "ReadableSurface";
        block.transform.SetParent(parent, false);
        block.transform.localPosition = Vector3.zero;
        block.transform.localScale = new Vector3(size.x, Mathf.Max(0.01f, height), size.y);
        var renderer = block.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MakeReadableSurfaceMat(color);
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
            {
                renderer.sharedMaterial.mainTextureScale = new Vector2(
                    Mathf.Max(1f, size.x / 32f),
                    Mathf.Max(1f, size.y / 32f));
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        DestroyCollider(block);
    }

    static void SpawnDownloadedBaseSurface(BattleMapDefinition map)
    {
        if (map == null)
            return;

        var root = new GameObject("TerrainPatch_DownloadedBase");
        root.transform.position = new Vector3(0f, 0.006f, 0f);

        string[] models = PickBaseSurfaceModels(map);
        const int count = 5;
        float mapSize = BattleMapCatalog.MapHalfSize * 2f;
        float cell = mapSize / count;
        float start = -mapSize * 0.5f + cell * 0.5f;

        for (int x = 0; x < count; x++)
        {
            for (int z = 0; z < count; z++)
            {
                string model = models[(x + z) % models.Length];
                Vector3 local = new Vector3(start + x * cell, 0f, start + z * cell);
                GameObject tile = CreateDownloadedObjPart(root, model, "DownloadedBaseTile",
                    local,
                    new Vector3(0f, (x + z) % 2 == 0 ? 0f : 180f, 0f),
                    Vector3.one,
                    map.GroundColor,
                    true);
                if (tile != null)
                {
                    ScaleDownloadedPartToFootprint(tile, cell * 1.08f, cell * 1.08f, 0.12f);
                    DropDownloadedPartToSurface(tile, 0.006f);
                    DisableDownloadedTileColliders(tile);
                }
            }
        }
    }

    static string[] PickBaseSurfaceModels(BattleMapDefinition map)
    {
        string name = map.Name ?? string.Empty;
        if (name == BattleMapCatalog.SeaChartName)
            return new[] { "nature:ground_riverTile", "nature:ground_riverOpen", "nature:ground_riverRocks", "pirate:patch-sand", "pirate:patch-sand-foliage" };
        if (name.Contains("城市"))
            return new[] { "survival:floor", "survival:metal-panel", "nature:ground_pathTile", "nature:ground_pathCross" };
        if (name.Contains("冰雪"))
            return new[] { "survival:floor", "nature:ground_riverTile", "nature:ground_riverRocks", "nature:ground_pathTile" };
        if (name.Contains("丛林"))
            return new[] { "nature:ground_grass", "nature:ground_pathTile", "pirate:patch-grass-foliage", "nature:grass_leafs" };
        return new[] { "pirate:patch-sand", "pirate:patch-sand-foliage", "nature:ground_grass", "nature:platform_beach" };
    }

    static void ScaleDownloadedPartToFootprint(GameObject part, float desiredX, float desiredZ, float maxHeight = 0f)
    {
        if (part == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(part, out bounds))
            return;

        float sizeX = Mathf.Max(0.01f, bounds.size.x);
        float sizeZ = Mathf.Max(0.01f, bounds.size.z);
        float factorX = desiredX / sizeX;
        float factorZ = desiredZ / sizeZ;
        if (float.IsNaN(factorX) || float.IsInfinity(factorX) || factorX <= 0.001f)
            return;
        if (float.IsNaN(factorZ) || float.IsInfinity(factorZ) || factorZ <= 0.001f)
            return;

        Vector3 scale = part.transform.localScale;
        scale.x *= Mathf.Clamp(factorX, 0.1f, 200f);
        // 地表模型只横向铺开，Y 轴不跟着放大，避免地块变成巨墙挡住镜头。
        scale.z *= Mathf.Clamp(factorZ, 0.1f, 200f);
        part.transform.localScale = scale;

        if (maxHeight > 0f && TryGetRendererBounds(part, out bounds))
        {
            float height = Mathf.Max(0.01f, bounds.size.y);
            if (height > maxHeight)
            {
                scale = part.transform.localScale;
                scale.y *= Mathf.Clamp(maxHeight / height, 0.005f, 1f);
                part.transform.localScale = scale;
            }
        }
    }

    static void DropDownloadedPartToSurface(GameObject part, float surfaceY)
    {
        if (part == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(part, out bounds))
            return;

        // 下载地表模型的原点不一定在底部，贴回地面可避免模型悬空或压进镜头。
        Vector3 pos = part.transform.position;
        pos.y += surfaceY - bounds.min.y;
        part.transform.position = pos;
    }

    static void DisableDownloadedTileColliders(GameObject part)
    {
        if (part == null)
            return;

        Collider[] colliders = part.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return hasBounds;
    }

    static void SpawnWall(string name, Vector3 pos, Vector3 scale, Color wallColor)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        bool horizontal = scale.x >= scale.z;
        float length = horizontal ? scale.x : scale.z;
        int count = Mathf.Clamp(Mathf.CeilToInt(length / 12f), 1, 36);
        float yaw = horizontal ? 90f : 0f;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (i + 0.5f) / count;
            float offset = (t - 0.5f) * length;
            Vector3 local = horizontal ? new Vector3(offset, -scale.y * 0.5f, 0f) : new Vector3(0f, -scale.y * 0.5f, offset);
            AddDownloadedObjPart(root, i % 3 == 0 ? "castle:gate" : "castle:wall", "BoundaryWall",
                local,
                new Vector3(0f, yaw, 0f),
                Vector3.one * 1.05f,
                wallColor,
                true);
        }

        var collider = root.AddComponent<BoxCollider>();
        collider.size = scale;
    }

    static void SpawnRockCluster(Vector3 center, Color rockBaseColor)
    {
        var root = new GameObject("Rock");
        root.transform.position = center;
        string[] models =
        {
            "nature:rock_largeA", "nature:rock_largeB", "nature:rock_largeC", "nature:rock_largeD", "nature:rock_largeE", "nature:rock_largeF",
            "nature:rock_smallA", "nature:rock_smallB", "nature:rock_smallC", "nature:rock_smallD", "nature:rock_smallE", "nature:rock_smallF",
            "nature:rock_smallFlatA", "nature:rock_smallFlatB", "nature:rock_smallFlatC",
            "nature:rock_tallA", "nature:rock_tallB", "nature:rock_tallC", "castle:rocks-large", "castle:rocks-small", "survival:rock-sand-a"
        };
        int count = Random.Range(4, 8);
        for (int i = 0; i < count; i++)
        {
            bool isBoulder = i == 0;
            string model = models[Random.Range(0, models.Length)];
            float offsetX = isBoulder ? 0f : Random.Range(-7f, 7f);
            float offsetZ = isBoulder ? 0f : Random.Range(-7f, 7f);
            float scale = isBoulder ? Random.Range(1.10f, 1.85f) : Random.Range(0.45f, 1.10f);
            if (model.Contains("tall")) scale *= 0.72f;
            AddDownloadedObjPart(root, model, "RockModel",
                new Vector3(offsetX, 0f, offsetZ),
                new Vector3(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)),
                Vector3.one * scale,
                rockBaseColor,
                true);
        }
    }

    static void SpawnTree(Vector3 pos, Color trunkColor, Color foliageColor)
    {
        var root = new GameObject("Tree");
        root.transform.position = pos;
        string[] models =
        {
            "nature:tree_pineTallA",
            "nature:tree_pineTallB_detailed",
            "nature:tree_pineTallC",
            "nature:tree_pineTallD_detailed",
            "nature:tree_pineRoundA",
            "nature:tree_pineRoundB",
            "nature:tree_pineRoundC",
            "nature:tree_pineSmallA",
            "nature:tree_pineSmallB",
            "nature:tree_oak",
            "nature:tree_detailed",
            "nature:tree_default",
            "nature:tree_tall",
            "nature:tree_fat",
            "nature:tree_palm",
            "nature:tree_palmBend",
            "nature:tree_palmDetailedTall",
            "nature:tree_palmTall"
        };
        string model = models[Random.Range(0, models.Length)];
        AddDownloadedObjPart(root, model, "DownloadedTree",
            Vector3.zero,
            new Vector3(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f)),
            Vector3.one * Random.Range(0.82f, 1.35f),
            Color.Lerp(trunkColor, foliageColor, 0.45f),
            true);
    }

    static void SpawnAmbientScenery(BattleMapDefinition map)
    {
        if (map == null)
            return;

        if (UseCleanReadableMap(map))
            return;

        var root = new GameObject("MapScenery");
        string[] models = PickAmbientSceneryModels(map);
        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-120f, 0f, -118f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(120f, 0f, 118f));
        int count = map.Name == BattleMapCatalog.SeaChartName ? 68 : (map.Name != null && map.Name.Contains("城市") ? 42 : 56);

        // 小景观只做视觉层，避开基地核心区域，避免影响建造、寻路和框选体验。
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = PickSceneryPosition(playerBase, enemyBase);
            string model = models[Random.Range(0, models.Length)];
            float scale = PickSceneryScale(model);
            AddDownloadedObjPart(root, model, "Scenery_" + BattleMapTileResourceName(model),
                pos,
                new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f)),
                Vector3.one * scale,
                PickSceneryTint(model, map),
                true);
        }
    }

    static bool UseCleanReadableMap(BattleMapDefinition map)
    {
        return map != null && map.Name == BattleMapCatalog.DefaultMapName;
    }

    static Vector3 PickSceneryPosition(Vector3 playerBase, Vector3 enemyBase)
    {
        float half = BattleMapCatalog.MapHalfSize - 18f;
        for (int tries = 0; tries < 16; tries++)
        {
            Vector3 pos = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
            if (Vector3.Distance(pos, playerBase) > 48f && Vector3.Distance(pos, enemyBase) > 48f && Mathf.Abs(pos.x - pos.z) > 20f)
                return pos;
        }
        return new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
    }

    static string[] PickAmbientSceneryModels(BattleMapDefinition map)
    {
        string name = map.Name ?? string.Empty;
        if (name == BattleMapCatalog.SeaChartName)
        {
            return new[]
            {
                "pirate:rocks-sand-a", "pirate:rocks-sand-b", "pirate:rocks-sand-c", "pirate:grass-patch",
                "pirate:palm-detailed-bend", "pirate:palm-detailed-straight", "pirate:barrel", "pirate:chest",
                "nature:grass", "nature:plant_bushSmall", "nature:rock_smallFlatA", "nature:rock_smallFlatB"
            };
        }
        if (name.Contains("城市"))
        {
            return new[]
            {
                "nature:rock_smallA", "nature:rock_smallB", "nature:rock_smallFlatA", "nature:log_stack",
                "survival:metal-panel", "survival:rock-sand-a", "castle:rocks-small", "nature:plant_bushSmall"
            };
        }
        if (name.Contains("冰雪"))
        {
            return new[]
            {
                "nature:rock_largeD", "nature:rock_largeE", "nature:rock_smallFlatB", "nature:rock_tallA",
                "castle:rocks-large", "castle:rocks-small", "nature:tree_pineSmallA", "nature:tree_pineSmallB"
            };
        }
        if (name.Contains("丛林"))
        {
            return new[]
            {
                "nature:plant_bush", "nature:plant_bushDetailed", "nature:plant_bushLarge", "nature:grass_leafs", "nature:grass_leafsLarge",
                "nature:flower_redA", "nature:flower_yellowA", "nature:log_large", "nature:log_stack", "nature:mushroom_redGroup",
                "nature:rock_smallC", "nature:rock_smallD"
            };
        }
        return new[]
        {
            "nature:rock_smallA", "nature:rock_smallB", "nature:rock_smallFlatA", "nature:grass", "nature:grass_leafs",
            "nature:plant_bushSmall", "nature:flower_yellowA", "nature:log", "nature:log_large", "pirate:rocks-sand-a"
        };
    }

    static float PickSceneryScale(string model)
    {
        if (model.Contains("barrel") || model.Contains("chest")) return Random.Range(0.35f, 0.62f);
        if (model.Contains("flower") || model.Contains("mushroom") || model.Contains("grass")) return Random.Range(0.45f, 0.86f);
        if (model.Contains("bush") || model.Contains("plant")) return Random.Range(0.62f, 1.05f);
        if (model.Contains("tree_pineSmall")) return Random.Range(0.55f, 0.90f);
        if (model.Contains("palm")) return Random.Range(0.75f, 1.05f);
        if (model.Contains("rock_tall")) return Random.Range(0.45f, 0.78f);
        if (model.Contains("rock")) return Random.Range(0.55f, 1.10f);
        return Random.Range(0.55f, 0.95f);
    }

    static Color PickSceneryTint(string model, BattleMapDefinition map)
    {
        if (model.Contains("rock") || model.Contains("metal"))
            return Color.Lerp(map.RockColor, Color.white, 0.12f);
        if (model.Contains("log") || model.Contains("barrel") || model.Contains("chest"))
            return Color.Lerp(map.TrunkColor, new Color(0.70f, 0.48f, 0.25f), 0.45f);
        if (model.Contains("flower"))
            return Color.Lerp(map.FoliageColor, Color.white, 0.25f);
        return Color.Lerp(map.FoliageColor, Color.white, 0.08f);
    }

    static void SpawnSandbagRing(Vector3 center, float radius, int count, bool isEnemy)
    {
        Color bagColor = isEnemy ? new Color(0.40f, 0.18f, 0.12f) : new Color(0.58f, 0.50f, 0.32f);
        Color accent = isEnemy ? new Color(0.65f, 0.10f, 0.10f) : new Color(0.22f, 0.45f, 0.78f);
        var root = new GameObject("Sandbag");
        root.transform.position = center;

        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            float yaw = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg + 90f;

            AddDownloadedObjPart(root, "survival:fence-fortified", "DownloadedFence",
                local,
                new Vector3(0f, yaw, 0f),
                Vector3.one * 1.05f,
                bagColor,
                true);

            if (i % 3 == 0)
            {
                AddDownloadedObjPart(root, isEnemy ? "castle:flag-pennant" : "pirate:flag-high-pennant", "BagMark",
                    local + Vector3.up * 0.15f,
                    new Vector3(0f, yaw, 0f),
                    Vector3.one * 0.45f,
                    accent,
                    true);
            }
        }
    }

    static void SpawnRuinWall(Vector3 center, float angle, int segments, Color wallColor)
    {
        float segLen = 6f;
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
        Vector3 start = center - dir * (segments * segLen * 0.5f);
        var root = new GameObject("RuinWall");
        root.transform.position = center;

        for (int i = 0; i < segments; i++)
        {
            if (Random.value < 0.18f) continue;

            Vector3 pos = start + dir * (i * segLen + segLen * 0.5f);
            Vector3 local = pos - center;
            AddDownloadedObjPart(root, i % 3 == 0 ? "castle:wall-doorway" : "castle:wall-half", "RuinWallPart",
                local,
                new Vector3(Random.Range(-8f, 8f), angle + Random.Range(-8f, 8f), Random.Range(-5f, 5f)),
                Vector3.one * Random.Range(0.85f, 1.15f),
                wallColor,
                true);

            for (int j = 0; j < 2; j++)
            {
                AddDownloadedObjPart(root, j == 0 ? "castle:rocks-large" : "castle:rocks-small", "Debris",
                    local + new Vector3(Random.Range(-segLen * 0.4f, segLen * 0.4f), 0f, Random.Range(-2f, 2f)),
                    new Vector3(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f)),
                    Vector3.one * Random.Range(0.35f, 0.75f),
                    Color.Lerp(wallColor, Color.black, 0.12f),
                    true);
            }
        }
    }

    static string[] PickFlatFeatureModels(string name)
    {
        if (name.StartsWith("Water_"))
            return new[] { "nature:ground_riverTile", "nature:ground_riverStraight", "nature:ground_riverRocks", "nature:ground_riverBend", "nature:ground_riverOpen" };
        if (name.StartsWith("SeaRoute_") || name.StartsWith("SeaChart_") || name.StartsWith("SeaCompass_"))
            return new[] { "pirate:patch-sand", "pirate:patch-sand-foliage", "pirate:platform-planks", "nature:ground_pathTile" };
        if (name.StartsWith("Road_") || name.StartsWith("SeaRoute_") || name.StartsWith("SeaChart_"))
            return new[] { "nature:ground_pathStraight", "nature:ground_pathTile", "nature:ground_pathRocks", "nature:ground_pathBend", "nature:ground_pathCross" };
        if (name.StartsWith("RoadEdge_"))
            return new[] { "nature:ground_pathSide", "nature:ground_pathSideOpen", "nature:ground_grass", "pirate:patch-sand-foliage" };
        if (name.Contains("Background"))
            return new[] { "nature:ground_grass", "nature:ground_pathTile", "pirate:patch-sand", "pirate:patch-grass" };
        if (name.StartsWith("TerrainPatch1_"))
            return new[] { "nature:ground_grass", "nature:ground_pathTile", "pirate:patch-sand-foliage", "pirate:patch-grass-foliage" };
        if (name.StartsWith("TerrainPatch2_") || name.StartsWith("TerrainPatch3_"))
            return new[] { "survival:floor", "survival:metal-panel", "nature:ground_pathTile", "nature:bridge_stone" };
        return new[] { "nature:ground_grass", "nature:ground_pathTile", "pirate:patch-sand", "nature:platform_beach" };
    }

    static float PickTileStep(string name, Vector2 size)
    {
        if (name.Contains("Background")) return 42f;
        if (name.StartsWith("Road_") || name.StartsWith("SeaRoute_")) return 13f;
        if (name.StartsWith("SeaChart_")) return 18f;
        if (name.StartsWith("Water_")) return 16f;
        return DefaultMapTileStep;
    }

    static float PickTileScale(string name, float step)
    {
        if (name.Contains("Background")) return Mathf.Max(4.8f, step * 0.22f);
        if (name.StartsWith("Road_") || name.StartsWith("SeaRoute_") || name.StartsWith("SeaChart_")) return Mathf.Max(1.7f, step * 0.18f);
        if (name.StartsWith("Water_")) return Mathf.Max(2.2f, step * 0.20f);
        return Mathf.Max(2.3f, step * 0.20f);
    }

    static GameObject InstantiateDownloadedObj(string path, Material material)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        GameObject prototype;
        if (!ObjPrototypeCache.TryGetValue(path, out prototype) || prototype == null)
        {
            prototype = SimpleObjModelLoader.LoadFromFile(path, null);
            if (prototype == null)
                return null;
            prototype.name = "__ExternalMapModelCache_" + Path.GetFileNameWithoutExtension(path);
            prototype.hideFlags = HideFlags.HideAndDontSave;
            prototype.SetActive(false);
            ObjPrototypeCache[path] = prototype;
        }

        GameObject instance = Object.Instantiate(prototype);
        instance.hideFlags = HideFlags.None;
        instance.SetActive(true);
        var renderer = instance.GetComponentInChildren<Renderer>(true);
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return instance;
    }
}
