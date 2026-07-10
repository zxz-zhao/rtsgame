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
        "TerrainPatch", "Road_", "RoadEdge_", "Water_", "WaterBlocker_", "MapLayer_", "MapProp_", "Wall", "SeaRoute_", "SeaChart_", "SeaCompass_"
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
            -0.010f,
            0.020f,
            0.12f);
        SpawnMapDetails(map);
        if (UseCleanReadableMap(map))
            SpawnCleanReadableMapLayers(map);
        SpawnBattleWearLayers(map);

        float mapHalf = BattleMapCatalog.MapHalfSize;
        SpawnWall("WallN", new Vector3(0f, 1.5f, mapHalf + 5f), new Vector3(mapHalf * 2f + 10f, 3f, 5f), map.WallColor);
        SpawnWall("WallS", new Vector3(0f, 1.5f, -mapHalf - 5f), new Vector3(mapHalf * 2f + 10f, 3f, 5f), map.WallColor);
        SpawnWall("WallE", new Vector3(mapHalf + 5f, 1.5f, 0f), new Vector3(5f, 3f, mapHalf * 2f + 10f), map.WallColor);
        SpawnWall("WallW", new Vector3(-mapHalf - 5f, 1.5f, 0f), new Vector3(5f, 3f, mapHalf * 2f + 10f), map.WallColor);

        foreach (var pos in map.RockClusters) SpawnRockCluster(pos, map.RockColor);
        foreach (var pos in map.TreePositions) SpawnTree(map, pos, map.TrunkColor, map.FoliageColor);
        SpawnManualProps(map);
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

    public static void ClearGeneratedObjects()
    {
        var all = Object.FindObjectsOfType<Transform>();
        var targets = new List<GameObject>();
        foreach (var t in all)
        {
            if (t == null) continue;
            if (!ShouldClear(t.gameObject.name)) continue;
            if (HasClearableAncestor(t)) continue;
            targets.Add(t.gameObject);
        }

        for (int i = 0; i < targets.Count; i++)
            DestroyGeneratedObject(targets[i]);
    }

    static bool ShouldClear(string name)
    {
        foreach (var exact in RuntimeNames)
            if (name == exact) return true;

        foreach (var prefix in RuntimePrefixes)
            if (name.StartsWith(prefix)) return true;

        return false;
    }

    static bool HasClearableAncestor(Transform transform)
    {
        var parent = transform.parent;
        while (parent != null)
        {
            if (ShouldClear(parent.gameObject.name))
                return true;
            parent = parent.parent;
        }
        return false;
    }

    static void DestroyGeneratedObject(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
        {
            go.SetActive(false);
            Object.Destroy(go);
        }
        else
        {
            Object.DestroyImmediate(go);
        }
    }

    static Material MakeMat(Color color, float metallic = 0f, float glossiness = 0.2f)
    {
        var mat = new Material(Shader.Find("Standard"));
        RendererColorUtil.TrySetColor(mat, color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", glossiness);
        return mat;
    }

    enum SurfaceMaterialKind
    {
        Grassland,
        ForestFloor,
        DirtRoad,
        Asphalt,
        Concrete,
        Gravel,
        Shore,
        Water,
        BasePad,
        BattleScar,
        RuinDust,
        Snow,
        Mud,
    }

    static readonly Dictionary<int, Texture2D> SurfaceAlbedoTextureCache = new Dictionary<int, Texture2D>();
    static readonly Dictionary<int, Texture2D> SurfaceNormalTextureCache = new Dictionary<int, Texture2D>();
    static readonly Dictionary<SurfaceMaterialKind, string> InstalledSurfaceMaterialPaths = new Dictionary<SurfaceMaterialKind, string>
    {
        { SurfaceMaterialKind.Grassland, "Materials/MapEnvironment/Env_Grassland" },
        { SurfaceMaterialKind.ForestFloor, "Materials/MapEnvironment/Env_ForestFloor" },
        { SurfaceMaterialKind.DirtRoad, "Materials/MapEnvironment/Env_DirtRoad" },
        { SurfaceMaterialKind.Asphalt, "Materials/MapEnvironment/Env_Asphalt" },
        { SurfaceMaterialKind.Concrete, "Materials/MapEnvironment/Env_Concrete" },
        { SurfaceMaterialKind.Gravel, "Materials/MapEnvironment/Env_Gravel" },
        { SurfaceMaterialKind.Shore, "Materials/MapEnvironment/Env_Shore" },
        { SurfaceMaterialKind.BasePad, "Materials/MapEnvironment/Env_BasePad" },
        { SurfaceMaterialKind.BattleScar, "Materials/MapEnvironment/Env_BattleScar" },
        { SurfaceMaterialKind.RuinDust, "Materials/MapEnvironment/Env_RuinDust" },
        { SurfaceMaterialKind.Snow, "Materials/MapEnvironment/Env_Snow" },
        { SurfaceMaterialKind.Mud, "Materials/MapEnvironment/Env_Mud" },
    };
    static readonly Dictionary<SurfaceMaterialKind, Material> InstalledSurfaceTemplateCache = new Dictionary<SurfaceMaterialKind, Material>();
    static readonly HashSet<SurfaceMaterialKind> MissingInstalledSurfaceTemplates = new HashSet<SurfaceMaterialKind>();

    static Material MakeReadableSurfaceMat(string name, Color color, float glossiness)
    {
        SurfaceMaterialKind kind = ClassifySurfaceMaterialKind(name, color);
        color = MakeReadableSurfaceColor(kind, color);

        Material installed;
        if (TryMakeInstalledSurfaceMat(kind, color, glossiness, out installed))
            return installed;

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);

        Texture2D albedo;
        Texture2D normal;
        GetReadableSurfaceTextures(kind, color, out albedo, out normal);

        if (mat.HasProperty("_MainTex"))
            mat.mainTexture = albedo;
        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        RendererColorUtil.TrySetColor(mat, albedo != null ? Color.white : color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", kind == SurfaceMaterialKind.Water ? 0.02f : 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", PickSurfaceSmoothness(kind, glossiness));
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", PickSurfaceSmoothness(kind, glossiness));
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", PickSurfaceNormalScale(kind));
        return mat;
    }

    static bool TryMakeInstalledSurfaceMat(SurfaceMaterialKind kind, Color color, float glossiness, out Material material)
    {
        material = null;
        Material template = LoadInstalledSurfaceTemplate(kind);
        if (template == null)
            return false;

        material = new Material(template);
        material.name = "InstalledSurface_" + kind;

        Color tint = Color.Lerp(Color.white, color, PickInstalledSurfaceTintStrength(kind));
        tint.a = color.a;
        RendererColorUtil.TrySetColor(material, tint);

        float smoothness = PickSurfaceSmoothness(kind, glossiness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", smoothness);
        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", PickSurfaceNormalScale(kind));
        if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", PickInstalledSurfaceOcclusion(kind));
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", kind == SurfaceMaterialKind.Water ? 0.02f : 0f);
        return true;
    }

    static Material LoadInstalledSurfaceTemplate(SurfaceMaterialKind kind)
    {
        Material material;
        if (InstalledSurfaceTemplateCache.TryGetValue(kind, out material) && material != null)
            return material;
        if (MissingInstalledSurfaceTemplates.Contains(kind))
            return null;

        string path;
        if (!InstalledSurfaceMaterialPaths.TryGetValue(kind, out path))
        {
            MissingInstalledSurfaceTemplates.Add(kind);
            return null;
        }

        material = Resources.Load<Material>(path);
        if (material == null)
        {
            MissingInstalledSurfaceTemplates.Add(kind);
            return null;
        }

        InstalledSurfaceTemplateCache[kind] = material;
        return material;
    }

    static float PickInstalledSurfaceTintStrength(SurfaceMaterialKind kind)
    {
        switch (kind)
        {
            case SurfaceMaterialKind.Grassland: return 0.18f;
            case SurfaceMaterialKind.ForestFloor: return 0.14f;
            case SurfaceMaterialKind.DirtRoad: return 0.10f;
            case SurfaceMaterialKind.Asphalt: return 0.04f;
            case SurfaceMaterialKind.Concrete: return 0.05f;
            case SurfaceMaterialKind.Gravel: return 0.08f;
            case SurfaceMaterialKind.Shore: return 0.10f;
            case SurfaceMaterialKind.BasePad: return 0.05f;
            case SurfaceMaterialKind.BattleScar: return 0.12f;
            case SurfaceMaterialKind.RuinDust: return 0.10f;
            case SurfaceMaterialKind.Snow: return 0.06f;
            case SurfaceMaterialKind.Mud: return 0.14f;
            default: return 0.10f;
        }
    }

    static float PickInstalledSurfaceOcclusion(SurfaceMaterialKind kind)
    {
        switch (kind)
        {
            case SurfaceMaterialKind.ForestFloor: return 0.92f;
            case SurfaceMaterialKind.DirtRoad: return 0.86f;
            case SurfaceMaterialKind.Gravel: return 0.82f;
            case SurfaceMaterialKind.Shore: return 0.72f;
            case SurfaceMaterialKind.Mud: return 0.76f;
            case SurfaceMaterialKind.Snow: return 0.68f;
            default: return 0.80f;
        }
    }

    static SurfaceMaterialKind ClassifySurfaceMaterialKind(string name, Color color)
    {
        string safeName = name ?? string.Empty;
        if (safeName.StartsWith("Water_") || safeName.Contains("ShallowWater") || safeName.Contains("DeepWater"))
            return SurfaceMaterialKind.Water;
        if (safeName.Contains("Shore"))
            return SurfaceMaterialKind.Shore;
        if (safeName.Contains("Hardstand") || safeName.Contains("Apron") || safeName.Contains("MotorPool"))
            return SurfaceMaterialKind.Concrete;
        if (safeName.StartsWith("RoadMark_"))
            return SurfaceMaterialKind.Concrete;
        if (safeName.StartsWith("RoadEdge_"))
            return SurfaceMaterialKind.Gravel;
        if (safeName.StartsWith("Road_"))
        {
            Color.RGBToHSV(color, out float roadH, out float roadS, out float roadV);
            if (roadS < 0.18f && roadV < 0.38f)
                return SurfaceMaterialKind.Asphalt;
            return SurfaceMaterialKind.DirtRoad;
        }
        if (safeName.Contains("Forest"))
            return SurfaceMaterialKind.ForestFloor;
        if (safeName.Contains("BattleScar"))
            return SurfaceMaterialKind.BattleScar;
        if (safeName.Contains("CityDust"))
            return SurfaceMaterialKind.RuinDust;
        if (safeName.Contains("JungleDamp"))
            return SurfaceMaterialKind.Mud;
        if (safeName.StartsWith("TerrainPatch2_") || safeName.StartsWith("TerrainPatch3_"))
            return SurfaceMaterialKind.Concrete;
        if (safeName.StartsWith("TerrainPatch1_") && (safeName.Contains("Clearing") || safeName.Contains("Plaza")))
            return SurfaceMaterialKind.Gravel;

        Color.RGBToHSV(color, out float h, out float s, out float v);
        if (v > 0.78f && s < 0.18f)
            return SurfaceMaterialKind.Snow;
        if (h > 0.50f && h < 0.64f && s > 0.25f)
            return SurfaceMaterialKind.Water;
        if (s < 0.16f && v < 0.38f)
            return SurfaceMaterialKind.Asphalt;
        if (s < 0.18f && v >= 0.38f && v < 0.62f)
            return SurfaceMaterialKind.Concrete;
        if (s < 0.30f && h > 0.08f && h < 0.16f)
            return SurfaceMaterialKind.Gravel;
        if (h > 0.08f && h < 0.18f && s > 0.28f)
            return SurfaceMaterialKind.DirtRoad;
        return SurfaceMaterialKind.Grassland;
    }

    static Color MakeReadableSurfaceColor(SurfaceMaterialKind kind, Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        switch (kind)
        {
            case SurfaceMaterialKind.ForestFloor:
                s = Mathf.Clamp01(s * 1.05f);
                v = Mathf.Clamp01(Mathf.Max(v, 0.32f) * 0.90f);
                break;
            case SurfaceMaterialKind.DirtRoad:
            case SurfaceMaterialKind.Gravel:
            case SurfaceMaterialKind.BattleScar:
            case SurfaceMaterialKind.RuinDust:
            case SurfaceMaterialKind.Mud:
                s = Mathf.Clamp01(s * 0.78f);
                v = Mathf.Clamp01(Mathf.Max(v, 0.34f) * 0.92f);
                break;
            case SurfaceMaterialKind.Asphalt:
                s = Mathf.Clamp01(s * 0.28f);
                v = Mathf.Clamp01(Mathf.Max(v, 0.22f) * 0.82f);
                break;
            case SurfaceMaterialKind.Concrete:
                s = Mathf.Clamp01(s * 0.34f);
                v = Mathf.Clamp01(Mathf.Max(v, 0.40f) * 0.90f);
                break;
            case SurfaceMaterialKind.Water:
                s = Mathf.Clamp01(Mathf.Max(s, 0.36f));
                v = Mathf.Clamp01(Mathf.Max(v, 0.36f));
                break;
            case SurfaceMaterialKind.Snow:
                s = Mathf.Clamp01(s * 0.45f);
                v = Mathf.Clamp01(Mathf.Max(v, 0.82f));
                break;
            default:
                s = Mathf.Clamp01(s * 0.92f);
                v = Mathf.Clamp01(Mathf.Max(v + 0.035f, 0.42f));
                break;
        }

        Color readable = Color.HSVToRGB(h, s, v);
        readable = Color.Lerp(readable, Color.white, kind == SurfaceMaterialKind.Snow ? 0.08f : 0.03f);
        readable.a = color.a;
        return readable;
    }

    static void GetReadableSurfaceTextures(SurfaceMaterialKind kind, Color color, out Texture2D albedo, out Texture2D normal)
    {
        int key = CreateSurfaceCacheKey(kind, color);
        if (!SurfaceAlbedoTextureCache.TryGetValue(key, out albedo) || albedo == null
            || !SurfaceNormalTextureCache.TryGetValue(key, out normal) || normal == null)
        {
            BuildReadableSurfaceTextures(kind, color, key, out albedo, out normal);
            SurfaceAlbedoTextureCache[key] = albedo;
            SurfaceNormalTextureCache[key] = normal;
        }
    }

    static int CreateSurfaceCacheKey(SurfaceMaterialKind kind, Color color)
    {
        Color32 c32 = color;
        return ((int)kind << 24) ^ (c32.r << 16) ^ (c32.g << 8) ^ c32.b;
    }

    static void BuildReadableSurfaceTextures(SurfaceMaterialKind kind, Color color, int key, out Texture2D albedo, out Texture2D normal)
    {
        const int size = 128;
        albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        albedo.name = "ReadableSurfaceTexture";
        normal.name = "ReadableSurfaceNormal";
        albedo.wrapMode = TextureWrapMode.Repeat;
        normal.wrapMode = TextureWrapMode.Repeat;
        albedo.filterMode = FilterMode.Trilinear;
        normal.filterMode = FilterMode.Trilinear;
        albedo.anisoLevel = 2;
        normal.anisoLevel = 2;

        float[] heights = new float[size * size];
        float ox = (key & 255) * 0.071f + 11.3f;
        float oy = ((key >> 8) & 255) * 0.053f + 3.7f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float macro = Mathf.PerlinNoise(ox + u * 2.8f, oy + v * 2.8f);
                float detail = Mathf.PerlinNoise(ox * 0.7f + u * 9.5f, oy * 0.7f + v * 9.5f);
                float micro = Mathf.PerlinNoise(ox * 1.7f + u * 26f, oy * 1.7f + v * 26f);
                float streak = Mathf.PerlinNoise(ox * 0.25f + u * 1.4f, oy * 2.1f + v * 18f);
                float spots = Mathf.PerlinNoise(ox * 2.4f + u * 18f, oy * 1.3f + v * 18f);

                Color pixel = BuildSurfacePixel(kind, color, macro, detail, micro, streak, spots);
                float height = BuildSurfaceHeight(kind, macro, detail, micro, streak, spots);

                albedo.SetPixel(x, y, pixel);
                heights[y * size + x] = height;
            }
        }

        float normalStrength = PickSurfaceNormalStrength(kind);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float hL = heights[y * size + ((x - 1 + size) % size)];
                float hR = heights[y * size + ((x + 1) % size)];
                float hD = heights[((y - 1 + size) % size) * size + x];
                float hU = heights[((y + 1) % size) * size + x];
                Vector3 n = new Vector3((hL - hR) * normalStrength, 1f, (hD - hU) * normalStrength).normalized;
                normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
        }

        albedo.Apply(true, true);
        normal.Apply(true, true);
    }

    static Color BuildSurfacePixel(SurfaceMaterialKind kind, Color baseColor, float macro, float detail, float micro, float streak, float spots)
    {
        Color c = baseColor;
        float broad = (macro - 0.5f) * 0.18f;
        float fine = (detail - 0.5f) * 0.10f + (micro - 0.5f) * 0.05f;

        switch (kind)
        {
            case SurfaceMaterialKind.Grassland:
                c = ShiftColor(c, broad * 0.36f + fine * 0.22f);
                if (spots > 0.84f) c = Color.Lerp(c, new Color(0.24f, 0.40f, 0.17f), 0.045f);
                if (spots < 0.12f) c = Color.Lerp(c, new Color(0.32f, 0.56f, 0.22f), 0.04f);
                break;
            case SurfaceMaterialKind.ForestFloor:
                c = ShiftColor(c, broad * 0.8f + fine * 0.75f);
                c = Color.Lerp(c, new Color(0.22f, 0.18f, 0.10f), 0.20f);
                if (spots > 0.66f) c = Color.Lerp(c, new Color(0.30f, 0.22f, 0.12f), 0.22f);
                if (spots < 0.18f) c = Color.Lerp(c, new Color(0.12f, 0.26f, 0.10f), 0.14f);
                break;
            case SurfaceMaterialKind.DirtRoad:
                c = Color.Lerp(c, new Color(0.44f, 0.38f, 0.28f), 0.30f);
                c = ShiftColor(c, broad * 0.75f + fine * 0.85f);
                c = Color.Lerp(c, Color.black, Mathf.Clamp01((streak - 0.62f) * 0.18f));
                break;
            case SurfaceMaterialKind.Asphalt:
                c = Color.Lerp(c, new Color(0.17f, 0.18f, 0.18f), 0.54f);
                c = ShiftColor(c, broad * 0.20f + fine * 0.42f);
                if (spots > 0.80f) c = Color.Lerp(c, new Color(0.34f, 0.34f, 0.32f), 0.10f);
                if (streak > 0.72f) c = Color.Lerp(c, new Color(0.58f, 0.58f, 0.52f), 0.06f);
                break;
            case SurfaceMaterialKind.Concrete:
                c = Color.Lerp(c, new Color(0.52f, 0.52f, 0.50f), 0.36f);
                c = ShiftColor(c, broad * 0.28f + fine * 0.34f);
                if (spots > 0.74f) c = Color.Lerp(c, new Color(0.34f, 0.35f, 0.34f), 0.12f);
                break;
            case SurfaceMaterialKind.Gravel:
                c = Color.Lerp(c, new Color(0.46f, 0.42f, 0.34f), 0.34f);
                c = ShiftColor(c, broad * 0.52f + fine * 0.58f);
                if (spots > 0.72f) c = Color.Lerp(c, new Color(0.28f, 0.28f, 0.26f), 0.10f);
                break;
            case SurfaceMaterialKind.Shore:
                c = Color.Lerp(c, new Color(0.68f, 0.62f, 0.44f), 0.28f);
                c = ShiftColor(c, broad * 0.55f + fine * 0.65f);
                if (spots > 0.76f) c = Color.Lerp(c, new Color(0.46f, 0.44f, 0.38f), 0.16f);
                break;
            case SurfaceMaterialKind.Water:
                c = Color.Lerp(c, new Color(0.06f, 0.30f, 0.42f), 0.18f);
                c = ShiftColor(c, broad * 0.42f + fine * 0.30f);
                c = Color.Lerp(c, Color.white, Mathf.Clamp01((streak - 0.72f) * 0.12f));
                break;
            case SurfaceMaterialKind.BasePad:
                c = Color.Lerp(c, new Color(0.46f, 0.44f, 0.36f), 0.24f);
                c = ShiftColor(c, broad * 0.38f + fine * 0.55f);
                if (spots > 0.70f) c = Color.Lerp(c, new Color(0.30f, 0.30f, 0.28f), 0.12f);
                break;
            case SurfaceMaterialKind.BattleScar:
                c = Color.Lerp(c, new Color(0.18f, 0.16f, 0.14f), 0.36f);
                c = ShiftColor(c, broad * 0.32f + fine * 0.45f);
                if (spots > 0.62f) c = Color.Lerp(c, new Color(0.32f, 0.24f, 0.20f), 0.18f);
                break;
            case SurfaceMaterialKind.RuinDust:
                c = Color.Lerp(c, new Color(0.42f, 0.38f, 0.34f), 0.30f);
                c = ShiftColor(c, broad * 0.40f + fine * 0.58f);
                break;
            case SurfaceMaterialKind.Snow:
                c = ShiftColor(c, broad * 0.24f + fine * 0.18f);
                c = Color.Lerp(c, new Color(0.74f, 0.80f, 0.84f), 0.10f);
                if (spots > 0.78f) c = Color.Lerp(c, new Color(0.62f, 0.64f, 0.66f), 0.10f);
                break;
            case SurfaceMaterialKind.Mud:
                c = Color.Lerp(c, new Color(0.30f, 0.25f, 0.18f), 0.28f);
                c = ShiftColor(c, broad * 0.58f + fine * 0.48f);
                c = Color.Lerp(c, Color.black, Mathf.Clamp01((spots - 0.68f) * 0.16f));
                break;
        }

        c.a = baseColor.a;
        return c;
    }

    static float BuildSurfaceHeight(SurfaceMaterialKind kind, float macro, float detail, float micro, float streak, float spots)
    {
        float h = macro * 0.55f + detail * 0.30f + micro * 0.15f;
        switch (kind)
        {
            case SurfaceMaterialKind.Grassland:
                h *= 0.34f;
                break;
            case SurfaceMaterialKind.ForestFloor:
                h += Mathf.Clamp01((spots - 0.74f) * 0.25f);
                break;
            case SurfaceMaterialKind.DirtRoad:
                h = h * 0.55f - Mathf.Abs(streak - 0.5f) * 0.08f;
                break;
            case SurfaceMaterialKind.Asphalt:
                h = h * 0.18f + Mathf.Abs(streak - 0.5f) * 0.02f;
                break;
            case SurfaceMaterialKind.Concrete:
                h = h * 0.20f + Mathf.Clamp01((spots - 0.78f) * 0.03f);
                break;
            case SurfaceMaterialKind.Gravel:
                h = h * 0.46f + Mathf.Clamp01((spots - 0.70f) * 0.08f);
                break;
            case SurfaceMaterialKind.Water:
                h = 0.45f + (detail - 0.5f) * 0.08f + (streak - 0.5f) * 0.05f;
                break;
            case SurfaceMaterialKind.BattleScar:
                h = h * 0.42f + Mathf.Clamp01((spots - 0.72f) * 0.06f);
                break;
            case SurfaceMaterialKind.Shore:
            case SurfaceMaterialKind.BasePad:
            case SurfaceMaterialKind.RuinDust:
                h *= 0.48f;
                break;
            case SurfaceMaterialKind.Snow:
                h = h * 0.34f + Mathf.Clamp01((spots - 0.76f) * 0.08f);
                break;
            case SurfaceMaterialKind.Mud:
                h = h * 0.38f - Mathf.Clamp01((spots - 0.66f) * 0.08f);
                break;
        }
        return h;
    }

    static float PickSurfaceSmoothness(SurfaceMaterialKind kind, float fallbackGlossiness)
    {
        switch (kind)
        {
            case SurfaceMaterialKind.Water: return 0.72f;
            case SurfaceMaterialKind.Mud: return 0.28f;
            case SurfaceMaterialKind.Shore: return 0.12f;
            case SurfaceMaterialKind.Asphalt: return 0.14f;
            case SurfaceMaterialKind.Concrete: return 0.18f;
            case SurfaceMaterialKind.Gravel: return 0.08f;
            case SurfaceMaterialKind.BasePad: return 0.16f;
            case SurfaceMaterialKind.Snow: return 0.18f;
            default: return Mathf.Clamp(fallbackGlossiness * 0.55f, 0.04f, 0.18f);
        }
    }

    static float PickSurfaceNormalScale(SurfaceMaterialKind kind)
    {
        switch (kind)
        {
            case SurfaceMaterialKind.Water: return 0.22f;
            case SurfaceMaterialKind.DirtRoad: return 0.58f;
            case SurfaceMaterialKind.Asphalt: return 0.26f;
            case SurfaceMaterialKind.Concrete: return 0.22f;
            case SurfaceMaterialKind.Gravel: return 0.42f;
            case SurfaceMaterialKind.ForestFloor: return 0.72f;
            case SurfaceMaterialKind.BattleScar: return 0.48f;
            case SurfaceMaterialKind.BasePad: return 0.36f;
            case SurfaceMaterialKind.Grassland: return 0.18f;
            default: return 0.52f;
        }
    }

    static float PickSurfaceNormalStrength(SurfaceMaterialKind kind)
    {
        switch (kind)
        {
            case SurfaceMaterialKind.Water: return 1.8f;
            case SurfaceMaterialKind.Shore: return 3.4f;
            case SurfaceMaterialKind.ForestFloor: return 5.2f;
            case SurfaceMaterialKind.DirtRoad: return 4.5f;
            case SurfaceMaterialKind.Asphalt: return 2.1f;
            case SurfaceMaterialKind.Concrete: return 1.8f;
            case SurfaceMaterialKind.Gravel: return 3.2f;
            case SurfaceMaterialKind.BattleScar: return 3.8f;
            case SurfaceMaterialKind.Mud: return 2.6f;
            case SurfaceMaterialKind.Grassland: return 1.35f;
            default: return 4.0f;
        }
    }

    static Vector2 GetReadableSurfaceTiling(string name, Vector2 size)
    {
        SurfaceMaterialKind kind = ClassifySurfaceMaterialKind(name, Color.white);
        float baseTile = 14f;
        switch (kind)
        {
            case SurfaceMaterialKind.Water: baseTile = 22f; break;
            case SurfaceMaterialKind.DirtRoad: baseTile = 11f; break;
            case SurfaceMaterialKind.Asphalt: baseTile = 13f; break;
            case SurfaceMaterialKind.Concrete: baseTile = 16f; break;
            case SurfaceMaterialKind.Gravel: baseTile = 11f; break;
            case SurfaceMaterialKind.ForestFloor: baseTile = 10f; break;
            case SurfaceMaterialKind.Shore: baseTile = 13f; break;
            case SurfaceMaterialKind.BasePad: baseTile = 8f; break;
            case SurfaceMaterialKind.BattleScar: baseTile = 12f; break;
            case SurfaceMaterialKind.Snow: baseTile = 16f; break;
            case SurfaceMaterialKind.Mud: baseTile = 12f; break;
        }
        return new Vector2(
            Mathf.Max(1f, size.x / baseTile),
            Mathf.Max(1f, size.y / baseTile));
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
            if (UseCleanReadableMap(map))
                continue;

            string patchName = "TerrainPatch" + patch.PaletteIndex + "_" + patch.Name;
            Color patchColor = map.GetPatchColor(patch.PaletteIndex);

            SpawnFlatFeature(patchName,
                patch.Center, patch.Size, patch.Angle, patchColor, 0.018f, 0.03f, 0.12f);
        }

        foreach (var road in map.Roads)
        {
            if (UseCleanReadableMap(map))
                continue;

            SpawnFlatFeature("RoadEdge_" + road.Name,
                road.Center, road.Size + new Vector2(5f, 6f), road.Angle, map.RoadEdgeColor, 0.023f, 0.025f, 0.12f);
            SpawnFlatFeature("Road_" + road.Name,
                road.Center, road.Size, road.Angle, map.RoadColor, 0.032f, 0.025f, 0.08f);
        }

        foreach (var water in map.Waters)
        {
            SpawnFlatFeature("Water_" + water.Name,
                water.Center, water.Size, water.Angle, map.WaterColor, 0.016f, 0.012f, 0.75f);
            SpawnWaterBlocker(map, water);
        }
    }

    static void SpawnManualProps(BattleMapDefinition map)
    {
        if (map == null || map.Props == null)
            return;

        for (int i = 0; i < map.Props.Length; i++)
            SpawnManualProp(map, map.Props[i], i);
    }

    static void SpawnManualProp(BattleMapDefinition map, MapPropSpec prop, int index)
    {
        string propName = string.IsNullOrWhiteSpace(prop.Name) ? "Prop" + (index + 1) : prop.Name;
        GameObject root = new GameObject("MapProp_" + propName);
        root.transform.position = new Vector3(prop.Position.x, 0f, prop.Position.z);
        root.transform.rotation = Quaternion.Euler(0f, prop.Yaw, 0f);

        float scale = Mathf.Clamp(prop.Scale <= 0f ? 1f : prop.Scale, 0.08f, 12f);
        string resourcePath = prop.ResourcePath ?? string.Empty;
        GameObject instance = null;

        if (resourcePath == "builtin:house")
        {
            CreateFallbackHouse(root, map, scale);
        }
        else if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, root.transform);
                instance.name = "Visual";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * scale;
                DisableDownloadedTileColliders(instance);
                TintManualProp(instance, map, resourcePath);
            }
        }

        if (instance == null && root.transform.childCount == 0)
            CreateFallbackCrate(root, map, scale);

        if (prop.BlocksNavigation)
            AddManualPropCollider(root);
    }

    static void CreateFallbackHouse(GameObject root, BattleMapDefinition map, float scale)
    {
        Color wall = map != null ? Color.Lerp(map.RuinColor, Color.white, 0.12f) : new Color(0.56f, 0.50f, 0.42f);
        Color roof = map != null ? Color.Lerp(map.TrunkColor, new Color(0.70f, 0.28f, 0.18f), 0.35f) : new Color(0.55f, 0.22f, 0.16f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "HouseBody";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.6f * scale, 0f);
        body.transform.localScale = new Vector3(5.4f, 3.2f, 4.5f) * scale;
        body.GetComponent<Renderer>().material = MakeMat(wall, 0f, 0.22f);
        DestroyCollider(body);

        GameObject roofBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofBlock.name = "HouseRoof";
        roofBlock.transform.SetParent(root.transform, false);
        roofBlock.transform.localPosition = new Vector3(0f, 3.55f * scale, 0f);
        roofBlock.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        roofBlock.transform.localScale = new Vector3(4.8f, 1.1f, 4.8f) * scale;
        roofBlock.GetComponent<Renderer>().material = MakeMat(roof, 0f, 0.18f);
        DestroyCollider(roofBlock);

        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "HouseDoor";
        door.transform.SetParent(root.transform, false);
        door.transform.localPosition = new Vector3(0f, 0.95f * scale, -2.28f * scale);
        door.transform.localScale = new Vector3(1.05f, 1.9f, 0.12f) * scale;
        door.GetComponent<Renderer>().material = MakeMat(map != null ? map.TrunkColor : new Color(0.28f, 0.16f, 0.08f), 0f, 0.16f);
        DestroyCollider(door);
    }

    static void CreateFallbackCrate(GameObject root, BattleMapDefinition map, float scale)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "FallbackProp";
        crate.transform.SetParent(root.transform, false);
        crate.transform.localPosition = new Vector3(0f, 0.65f * scale, 0f);
        crate.transform.localScale = new Vector3(2.4f, 1.3f, 2.4f) * scale;
        crate.GetComponent<Renderer>().material = MakeMat(map != null ? map.RuinColor : new Color(0.48f, 0.42f, 0.35f), 0f, 0.18f);
        DestroyCollider(crate);
    }

    static void AddManualPropCollider(GameObject root)
    {
        if (root == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(root, out bounds))
            return;

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
            collider = root.AddComponent<BoxCollider>();

        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
        collider.center = new Vector3(localCenter.x, Mathf.Max(0.45f, localCenter.y), localCenter.z);
        collider.size = new Vector3(Mathf.Max(0.8f, Mathf.Abs(localSize.x)), Mathf.Max(1f, Mathf.Abs(localSize.y)), Mathf.Max(0.8f, Mathf.Abs(localSize.z)));
    }

    static void TintManualProp(GameObject root, BattleMapDefinition map, string resourcePath)
    {
        if (root == null || map == null)
            return;

        Color tint = PickManualPropTint(map, resourcePath);
        bool vegetation = ContainsIgnoreCase(resourcePath, "grass")
            || ContainsIgnoreCase(resourcePath, "plant")
            || ContainsIgnoreCase(resourcePath, "tree")
            || ContainsIgnoreCase(resourcePath, "palm")
            || ContainsIgnoreCase(resourcePath, "flower");
        float strength = vegetation ? 0.10f : 0.22f;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                    continue;

                if (RendererColorUtil.TryGetColor(material, out Color baseColor))
                    RendererColorUtil.TrySetColor(material, Color.Lerp(baseColor, tint, strength));
            }
        }
    }

    static Color PickManualPropTint(BattleMapDefinition map, string resourcePath)
    {
        if (ContainsIgnoreCase(resourcePath, "rock") || ContainsIgnoreCase(resourcePath, "rubble"))
            return map.RockColor;
        if (ContainsIgnoreCase(resourcePath, "grass")
            || ContainsIgnoreCase(resourcePath, "plant")
            || ContainsIgnoreCase(resourcePath, "tree")
            || ContainsIgnoreCase(resourcePath, "palm")
            || ContainsIgnoreCase(resourcePath, "flower"))
            return map.FoliageColor;
        if (ContainsIgnoreCase(resourcePath, "sandbag"))
            return Color.Lerp(map.TrunkColor, map.RoadEdgeColor, 0.55f);
        if (ContainsIgnoreCase(resourcePath, "tower") || ContainsIgnoreCase(resourcePath, "wall"))
            return map.RuinColor;
        return Color.Lerp(map.RuinColor, Color.white, 0.10f);
    }

    static void SpawnCleanReadableMapLayers(BattleMapDefinition map)
    {
        if (map == null)
            return;

        foreach (var patch in map.Patches)
        {
            string patchName = "TerrainPatch" + patch.PaletteIndex + "_" + patch.Name;
            Color patchColor = map.GetPatchColor(patch.PaletteIndex);

            if (patch.PaletteIndex == 2 || patch.PaletteIndex == 3)
            {
                Color stagingColor = Color.Lerp(map.RoadEdgeColor, map.GroundColor, 0.32f);
                Color hardstandColor = Color.Lerp(patchColor, map.RoadEdgeColor, 0.22f);

                SpawnFlatFeature("TerrainPatch_Apron_" + patch.Name,
                    patch.Center, patch.Size + new Vector2(14f, 14f), patch.Angle, stagingColor, 0.017f, 0.018f, 0.08f);
                SpawnFlatFeature("TerrainPatch_Hardstand_" + patch.Name,
                    patch.Center, new Vector2(Mathf.Max(18f, patch.Size.x - 12f), Mathf.Max(18f, patch.Size.y - 12f)),
                    patch.Angle, hardstandColor, 0.028f, 0.024f, 0.17f);
                SpawnFlatFeature(patchName,
                    patch.Center, new Vector2(Mathf.Max(16f, patch.Size.x - 26f), Mathf.Max(16f, patch.Size.y - 26f)),
                    patch.Angle, patchColor, 0.036f, 0.026f, 0.19f);
                continue;
            }

            if (ContainsIgnoreCase(patch.Name, "forest"))
            {
                SpawnFlatFeature("TerrainPatch_ForestShadow_" + patch.Name,
                    patch.Center, patch.Size + new Vector2(10f, 12f), patch.Angle,
                    Color.Lerp(map.GroundColor, map.TrunkColor, 0.18f), 0.015f, 0.016f, 0.04f);
            }

            if (patch.PaletteIndex == 1)
            {
                SpawnFlatFeature("TerrainPatch_Gravel_" + patch.Name,
                    patch.Center, patch.Size + new Vector2(8f, 8f), patch.Angle,
                    Color.Lerp(map.RoadEdgeColor, map.GroundColor, 0.42f), 0.019f, 0.016f, 0.06f);
            }

            SpawnFlatFeature(patchName,
                patch.Center, patch.Size, patch.Angle, patchColor, 0.024f, 0.020f, 0.08f);
        }

        foreach (var road in map.Roads)
        {
            SpawnFlatFeature("RoadEdge_" + road.Name,
                road.Center, road.Size + new Vector2(8f, 10f), road.Angle,
                map.RoadEdgeColor, 0.020f, 0.015f, 0.07f);
            SpawnFlatFeature("Road_" + road.Name,
                road.Center, new Vector2(Mathf.Max(4f, road.Size.x - 2f), road.Size.y), road.Angle,
                map.RoadColor, 0.030f, 0.018f, 0.12f);

            float markWidth = Mathf.Clamp(road.Size.x * 0.11f, 0.55f, 1.15f);
            SpawnFlatFeature("RoadMark_" + road.Name,
                road.Center, new Vector2(markWidth, Mathf.Max(18f, road.Size.y - 20f)), road.Angle,
                new Color(0.74f, 0.72f, 0.62f, 1f), 0.038f, 0.006f, 0.22f);
        }

        foreach (var water in map.Waters)
        {
            SpawnFlatFeature("Water_Shore_" + water.Name,
                water.Center, water.Size + new Vector2(9f, 10f), water.Angle,
                Color.Lerp(map.RoadEdgeColor, map.GroundColor, 0.55f), 0.014f, 0.010f, 0.08f);
            SpawnFlatFeature("Water_" + water.Name,
                water.Center, water.Size, water.Angle, map.WaterColor, 0.016f, 0.012f, 0.75f);
            SpawnWaterBlocker(map, water);
        }

        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-120f, 0f, -118f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(120f, 0f, 118f));
        Vector3 center = FindPatchCenter(map, 1, Vector3.zero);
        Vector3 axis = enemyBase - playerBase;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.001f)
            axis = new Vector3(1f, 0f, 1f);

        Vector3 axisN = axis.normalized;
        Vector3 perp = new Vector3(-axisN.z, 0f, axisN.x);
        float yaw = Mathf.Atan2(axisN.x, axisN.z) * Mathf.Rad2Deg;

        SpawnFlatFeature("MapLayer_BattleScar_Main_Default", center,
            new Vector2(18f, 150f), yaw, Color.Lerp(map.RoadColor, Color.black, 0.18f), 0.032f, 0.008f, 0.05f);
        SpawnFlatFeature("MapLayer_BattleScar_Left_Default", center + perp * 14f,
            new Vector2(8f, 124f), yaw, Color.Lerp(map.RoadEdgeColor, Color.black, 0.16f), 0.031f, 0.007f, 0.05f);
        SpawnFlatFeature("MapLayer_BattleScar_Right_Default", center - perp * 14f,
            new Vector2(8f, 124f), yaw, Color.Lerp(map.RoadEdgeColor, Color.black, 0.16f), 0.031f, 0.007f, 0.05f);
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
        ApplyRuntimeTint(instance, objName, tint, tintRenderers);
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

    static void ApplyRuntimeTint(GameObject root, string objName, Color tint, bool tintRenderers)
    {
        if (!tintRenderers || root == null)
            return;

        bool vegetation = IsVegetationModel(objName);
        float tintStrength = vegetation ? 0.12f : 0.45f;
        float smoothness = vegetation ? 0.08f : 0.35f;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] materials = renderers[r].sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;

                Material copy = new Material(source);
                if (RendererColorUtil.TryGetColor(copy, out Color baseColor))
                    RendererColorUtil.TrySetColor(copy, Color.Lerp(baseColor, tint, tintStrength));

                if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", 0f);
                if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", smoothness);
                if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", smoothness);
                materials[i] = copy;
            }
            renderers[r].sharedMaterials = materials;
        }
    }

    static bool IsVegetationModel(string objName)
    {
        if (string.IsNullOrEmpty(objName))
            return false;

        return objName.Contains("tree")
            || objName.Contains("bush")
            || objName.Contains("plant")
            || objName.Contains("grass")
            || objName.Contains("flower")
            || objName.Contains("leaf")
            || objName.Contains("mushroom")
            || objName.Contains("palm");
    }

    static bool ContainsIgnoreCase(string value, string token)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
            return false;

        return value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
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
            SpawnSimpleFlatBlock(name, root.transform, size, color, height, glossiness);
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

    static void SpawnSimpleFlatBlock(string name, Transform parent, Vector2 size, Color color, float height, float glossiness)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "ReadableSurface";
        block.transform.SetParent(parent, false);
        block.transform.localPosition = Vector3.zero;
        block.transform.localScale = new Vector3(size.x, Mathf.Max(0.01f, height), size.y);
        var renderer = block.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MakeReadableSurfaceMat(name, color, glossiness);
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
            {
                Vector2 tiling = GetReadableSurfaceTiling(name, size);
                renderer.sharedMaterial.mainTextureScale = tiling;
                if (renderer.sharedMaterial.HasProperty("_BumpMap"))
                    renderer.sharedMaterial.SetTextureScale("_BumpMap", tiling);
                if (renderer.sharedMaterial.HasProperty("_MetallicGlossMap"))
                    renderer.sharedMaterial.SetTextureScale("_MetallicGlossMap", tiling);
                if (renderer.sharedMaterial.HasProperty("_OcclusionMap"))
                    renderer.sharedMaterial.SetTextureScale("_OcclusionMap", tiling);
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
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
        if (name == BattleMapCatalog.DefaultMapName || name == BattleMapCatalog.GlobalConquestName)
            return new[] { "nature:ground_grass" };
        if (name == BattleMapCatalog.SeaChartName)
            return new[] { "nature:ground_riverTile", "nature:ground_riverOpen", "nature:ground_riverRocks", "pirate:patch-sand", "pirate:patch-sand-foliage" };
        if (name == BattleMapCatalog.CityRuinsName)
            return new[] { "survival:floor", "survival:metal-panel", "nature:ground_pathTile", "nature:ground_pathCross" };
        if (name == BattleMapCatalog.IceFortressName)
            return new[] { "survival:floor", "nature:ground_riverTile", "nature:ground_riverRocks", "nature:ground_pathTile" };
        if (name == BattleMapCatalog.JungleName)
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

    static void SpawnTree(BattleMapDefinition map, Vector3 pos, Color trunkColor, Color foliageColor)
    {
        var root = new GameObject("Tree");
        root.transform.position = pos;
        string[] models = PickPrimaryTreeModels(map);
        string model = models[Random.Range(0, models.Length)];
        float scale = PickPrimaryTreeScale(map, model);
        Color tint = PickPrimaryTreeTint(map, model, trunkColor, foliageColor);
        if (AddDownloadedObjPart(root, model, "DownloadedTree",
            Vector3.zero,
            new Vector3(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f)),
            Vector3.one * scale,
            tint,
            true))
        {
            DisableDownloadedTileColliders(root);
            AddTreeCollider(root);
            AddSecondaryTreeCluster(root, map, trunkColor, foliageColor);
            AddTreeDetailScatter(root, trunkColor, foliageColor);
        }
    }

    static string[] PickPrimaryTreeModels(BattleMapDefinition map)
    {
        string name = map != null ? (map.Name ?? string.Empty) : string.Empty;
        if (name == BattleMapCatalog.SeaChartName)
        {
            return new[]
            {
                "nature:tree_palmDetailedTall",
                "nature:tree_palmTall",
                "nature:tree_palmBend",
                "nature:tree_palm",
            };
        }

        if (name == BattleMapCatalog.IceFortressName)
        {
            return new[]
            {
                "nature:tree_pineTallB_detailed",
                "nature:tree_pineTallD_detailed",
                "nature:tree_pineTallC",
                "nature:tree_pineTallA",
                "nature:tree_pineRoundA",
                "nature:tree_pineRoundB",
                "nature:tree_pineRoundC",
            };
        }

        if (name == BattleMapCatalog.JungleName)
        {
            return new[]
            {
                "nature:tree_detailed",
                "nature:tree_fat",
                "nature:tree_oak",
                "nature:tree_tall",
                "nature:tree_default",
                "nature:tree_palmDetailedTall",
                "nature:tree_palmBend",
            };
        }

        if (name == BattleMapCatalog.CityRuinsName)
        {
            return new[]
            {
                "nature:tree_tall",
                "nature:tree_detailed",
                "nature:tree_oak",
                "nature:tree_default",
                "nature:tree_pineRoundA",
                "nature:tree_pineRoundB",
            };
        }

        return new[]
        {
            "nature:tree_detailed",
            "nature:tree_oak",
            "nature:tree_fat",
            "nature:tree_default",
            "nature:tree_tall",
            "nature:tree_pineRoundA",
            "nature:tree_pineRoundB",
        };
    }

    static float PickPrimaryTreeScale(BattleMapDefinition map, string model)
    {
        string name = map != null ? (map.Name ?? string.Empty) : string.Empty;
        if (model.Contains("palmDetailedTall") || model.Contains("palmTall"))
            return Random.Range(1.08f, 1.42f);
        if (model.Contains("palmBend"))
            return Random.Range(0.98f, 1.24f);
        if (model.Contains("pineTall"))
            return Random.Range(1.02f, 1.32f);
        if (model.Contains("pineRound"))
            return Random.Range(0.98f, 1.24f);
        if (model.Contains("tree_fat") || model.Contains("tree_oak") || model.Contains("tree_detailed"))
            return name == BattleMapCatalog.JungleName
                ? Random.Range(1.12f, 1.48f)
                : Random.Range(0.98f, 1.28f);
        return Random.Range(0.92f, 1.18f);
    }

    static Color PickPrimaryTreeTint(BattleMapDefinition map, string model, Color trunkColor, Color foliageColor)
    {
        string name = map != null ? (map.Name ?? string.Empty) : string.Empty;
        if (name == BattleMapCatalog.IceFortressName)
            return Color.Lerp(foliageColor, new Color(0.70f, 0.80f, 0.74f), 0.18f);
        if (name == BattleMapCatalog.JungleName)
            return Color.Lerp(foliageColor, new Color(0.08f, 0.32f, 0.12f), 0.10f);
        if (model.Contains("palm"))
            return Color.Lerp(foliageColor, new Color(0.18f, 0.46f, 0.20f), 0.08f);
        if (model.Contains("pine"))
            return Color.Lerp(foliageColor, new Color(0.18f, 0.28f, 0.16f), 0.10f);
        return Color.Lerp(foliageColor, trunkColor, 0.04f);
    }

    static void AddSecondaryTreeCluster(GameObject root, BattleMapDefinition map, Color trunkColor, Color foliageColor)
    {
        if (root == null)
            return;

        string[] supportModels = PickSecondaryTreeModels(map);
        int supportCount = map != null && map.Name == BattleMapCatalog.JungleName
            ? Random.Range(2, 4)
            : Random.Range(1, 3);

        for (int i = 0; i < supportCount; i++)
        {
            string model = supportModels[Random.Range(0, supportModels.Length)];
            GameObject part = CreateDownloadedObjPart(
                root,
                model,
                "DownloadedUnderTree",
                PickTreeScatterLocalPosition(2.2f, 5.4f),
                new Vector3(0f, Random.Range(0f, 360f), 0f),
                Vector3.one * PickSecondaryTreeScale(model),
                PickSecondaryTreeTint(model, trunkColor, foliageColor),
                true);
            DisableDownloadedTileColliders(part);
        }
    }

    static string[] PickSecondaryTreeModels(BattleMapDefinition map)
    {
        string name = map != null ? (map.Name ?? string.Empty) : string.Empty;
        if (name == BattleMapCatalog.SeaChartName)
        {
            return new[]
            {
                "nature:tree_palm",
                "nature:tree_palmBend",
                "nature:plant_bushLarge",
                "nature:plant_bushDetailed",
            };
        }

        if (name == BattleMapCatalog.IceFortressName)
        {
            return new[]
            {
                "nature:tree_pineSmallA",
                "nature:tree_pineSmallB",
                "nature:tree_pineRoundA",
                "nature:tree_pineRoundC",
            };
        }

        return new[]
        {
            "nature:tree_default",
            "nature:tree_tall",
            "nature:tree_pineSmallA",
            "nature:tree_pineSmallB",
            "nature:plant_bushLarge",
            "nature:plant_bushDetailed",
        };
    }

    static float PickSecondaryTreeScale(string model)
    {
        if (model.Contains("bush") || model.Contains("plant"))
            return Random.Range(0.72f, 1.08f);
        if (model.Contains("pineSmall"))
            return Random.Range(0.66f, 0.92f);
        if (model.Contains("palm"))
            return Random.Range(0.62f, 0.88f);
        return Random.Range(0.58f, 0.88f);
    }

    static Color PickSecondaryTreeTint(string model, Color trunkColor, Color foliageColor)
    {
        if (model.Contains("pine"))
            return Color.Lerp(foliageColor, new Color(0.20f, 0.28f, 0.16f), 0.12f);
        if (model.Contains("palm"))
            return Color.Lerp(foliageColor, new Color(0.18f, 0.44f, 0.20f), 0.10f);
        return Color.Lerp(foliageColor, trunkColor, 0.06f);
    }

    static void SpawnAmbientScenery(BattleMapDefinition map)
    {
        if (map == null)
            return;


        var root = new GameObject("MapScenery");
        string[] models = PickAmbientSceneryModels(map);
        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-120f, 0f, -118f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(120f, 0f, 118f));
        int desiredCount = PickAmbientSceneryCount(map);

        // Ambient scenery stays visual-only and avoids key build / routing areas.
        for (int i = 0; i < desiredCount; i++)
        {
            Vector3 pos = PickSceneryPosition(map, playerBase, enemyBase);
            string model = models[Random.Range(0, models.Length)];
            float scale = PickSceneryScale(model);
            AddDownloadedObjPart(root, model, "Scenery_" + BattleMapTileResourceName(model),
                pos,
                new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f)),
                Vector3.one * scale,
                PickSceneryTint(model, map),
                true);
        }

        SpawnFrontlineScatter(root, map, playerBase, enemyBase);
    }

    static void SpawnBattleWearLayers(BattleMapDefinition map)
    {
        if (map == null
            || map.Name == BattleMapCatalog.SeaChartName
            || map.Name == BattleMapCatalog.DefaultMapName
            || map.Name == BattleMapCatalog.GlobalConquestName)
            return;

        Vector3 playerBase = FindPatchCenter(map, 2, new Vector3(-120f, 0f, -118f));
        Vector3 enemyBase = FindPatchCenter(map, 3, new Vector3(120f, 0f, 118f));
        Vector3 center = FindPatchCenter(map, 1, Vector3.zero);
        Vector3 axis = enemyBase - playerBase;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.001f)
            axis = new Vector3(1f, 0f, 1f);

        Vector3 axisN = axis.normalized;
        Vector3 perp = new Vector3(-axisN.z, 0f, axisN.x);
        float yaw = Mathf.Atan2(axisN.x, axisN.z) * Mathf.Rad2Deg;
        Color scar = Color.Lerp(map.RoadColor, Color.black, 0.16f);
        Color churn = Color.Lerp(map.RuinColor, map.RoadColor, 0.46f);
        Color meadow = Color.Lerp(map.GroundColor, map.FoliageColor, 0.22f);
        Color ash = Color.Lerp(map.RockColor, Color.black, 0.22f);

        SpawnFlatFeature("MapLayer_BattleScar_Main", center,
            new Vector2(26f, 166f), yaw, scar, 0.034f, 0.012f, 0.06f);
        SpawnFlatFeature("MapLayer_BattleScar_Cross", center,
            new Vector2(18f, 92f), yaw + 90f, churn, 0.035f, 0.011f, 0.05f);
        SpawnFlatFeature("MapLayer_BattleScar_Left", center + perp * 20f,
            new Vector2(12f, 118f), yaw, ash, 0.033f, 0.010f, 0.05f);
        SpawnFlatFeature("MapLayer_BattleScar_Right", center - perp * 20f,
            new Vector2(12f, 118f), yaw, ash, 0.033f, 0.010f, 0.05f);

        SpawnFlatFeature("MapLayer_Meadow_PlayerFlank", center - axisN * 44f + perp * 34f,
            new Vector2(42f, 54f), yaw + 18f, meadow, 0.028f, 0.010f, 0.04f);
        SpawnFlatFeature("MapLayer_Meadow_EnemyFlank", center + axisN * 44f - perp * 34f,
            new Vector2(42f, 54f), yaw + 18f, meadow, 0.028f, 0.010f, 0.04f);

        if (map.Name == BattleMapCatalog.CityRuinsName)
        {
            SpawnFlatFeature("MapLayer_CityDust_West", center + perp * 32f,
                new Vector2(34f, 78f), yaw - 6f, churn, 0.036f, 0.012f, 0.05f);
            SpawnFlatFeature("MapLayer_CityDust_East", center - perp * 32f,
                new Vector2(34f, 78f), yaw - 6f, churn, 0.036f, 0.012f, 0.05f);
        }
        else if (map.Name == BattleMapCatalog.JungleName)
        {
            Color damp = Color.Lerp(map.GroundColor, map.WaterColor, 0.18f);
            SpawnFlatFeature("MapLayer_JungleDamp_North", center + axisN * 36f,
                new Vector2(30f, 70f), yaw + 24f, damp, 0.031f, 0.010f, 0.04f);
            SpawnFlatFeature("MapLayer_JungleDamp_South", center - axisN * 36f,
                new Vector2(30f, 70f), yaw + 24f, damp, 0.031f, 0.010f, 0.04f);
        }
    }

    static bool UseCleanReadableMap(BattleMapDefinition map)
    {
        return map != null
            && (map.Name == BattleMapCatalog.DefaultMapName
                || map.Name == BattleMapCatalog.GlobalConquestName);
    }

    static void AddTreeDetailScatter(GameObject root, Color trunkColor, Color foliageColor)
    {
        if (root == null)
            return;

        string[] floraModels =
        {
            "nature:grass", "nature:grass_large", "nature:grass_leafs", "nature:grass_leafsLarge",
            "nature:plant_bushSmall", "nature:plant_bush", "nature:plant_bushDetailed",
            "nature:flower_yellowA", "nature:flower_redA", "nature:flower_purpleA",
            "pirate:patch-grass-foliage"
        };
        string[] groundModels =
        {
            "nature:rock_smallA", "nature:rock_smallB", "nature:rock_smallFlatA",
            "nature:log", "nature:log_large", "nature:mushroom_redGroup"
        };

        int floraCount = Random.Range(2, 5);
        int groundCount = Random.Range(1, 3);

        for (int i = 0; i < floraCount; i++)
        {
            string model = floraModels[Random.Range(0, floraModels.Length)];
            GameObject part = CreateDownloadedObjPart(
                root,
                model,
                "ForestPatch_Flora",
                PickTreeScatterLocalPosition(1.3f, 4.2f),
                new Vector3(0f, Random.Range(0f, 360f), 0f),
                Vector3.one * PickTreeScatterScale(model),
                PickTreeScatterTint(model, trunkColor, foliageColor),
                true);
            DisableDownloadedTileColliders(part);
        }

        for (int i = 0; i < groundCount; i++)
        {
            string model = groundModels[Random.Range(0, groundModels.Length)];
            GameObject part = CreateDownloadedObjPart(
                root,
                model,
                "ForestPatch_Ground",
                PickTreeScatterLocalPosition(1.6f, 4.8f),
                new Vector3(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f)),
                Vector3.one * PickTreeScatterScale(model),
                PickTreeScatterTint(model, trunkColor, foliageColor),
                true);
            DisableDownloadedTileColliders(part);
        }
    }

    static int PickAmbientSceneryCount(BattleMapDefinition map)
    {
        string name = map != null ? (map.Name ?? string.Empty) : string.Empty;
        if (name == BattleMapCatalog.SeaChartName)
            return 68;
        if (name == BattleMapCatalog.DefaultMapName || name == BattleMapCatalog.GlobalConquestName)
            return 92;
        if (name == BattleMapCatalog.JungleName)
            return 82;
        if (name == BattleMapCatalog.CityRuinsName)
            return 46;
        return 56;
    }

    static void SpawnFrontlineScatter(GameObject root, BattleMapDefinition map, Vector3 playerBase, Vector3 enemyBase)
    {
        if (root == null || map == null || map.Name == BattleMapCatalog.SeaChartName)
            return;

        string[] models =
        {
            "nature:rock_smallFlatA", "nature:rock_smallFlatB", "nature:rock_smallC",
            "nature:log", "nature:log_large", "nature:plant_bushSmall",
            "nature:plant_bushDetailed", "nature:grass_leafsLarge", "nature:ground_pathRocks",
            "pirate:patch-sand-foliage"
        };

        Vector3 center = FindPatchCenter(map, 1, Vector3.zero);
        Vector3 axis = enemyBase - playerBase;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.001f)
            axis = new Vector3(1f, 0f, 1f);

        Vector3 axisN = axis.normalized;
        Vector3 perp = new Vector3(-axisN.z, 0f, axisN.x);
        int scatterCount = map.Name == BattleMapCatalog.DefaultMapName ? 18 : 12;

        for (int i = 0; i < scatterCount; i++)
        {
            float along = Random.Range(-72f, 72f);
            float side = Random.Range(-34f, 34f);
            Vector3 pos = center + axisN * along + perp * side;
            if (Vector3.Distance(pos, playerBase) < 34f || Vector3.Distance(pos, enemyBase) < 34f)
                continue;
            if (!IsValidFrontlineScatterPosition(map, pos))
                continue;

            string model = models[Random.Range(0, models.Length)];
            GameObject part = CreateDownloadedObjPart(
                root,
                model,
                "Scenery_Frontline_" + BattleMapTileResourceName(model),
                pos,
                new Vector3(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f)),
                Vector3.one * Random.Range(0.55f, 1.05f),
                PickSceneryTint(model, map),
                true);
            if (part != null)
                DisableDownloadedTileColliders(part);
        }
    }

    static Vector3 PickSceneryPosition(BattleMapDefinition map, Vector3 playerBase, Vector3 enemyBase)
    {
        float half = BattleMapCatalog.MapHalfSize - 18f;
        for (int tries = 0; tries < 28; tries++)
        {
            Vector3 pos = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
            if (Vector3.Distance(pos, playerBase) > 48f
                && Vector3.Distance(pos, enemyBase) > 48f
                && Mathf.Abs(pos.x - pos.z) > 20f
                && IsValidSceneryPosition(map, pos))
                return pos;
        }

        for (int tries = 0; tries < 32; tries++)
        {
            Vector3 pos = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
            if (IsValidSceneryPosition(map, pos))
                return pos;
        }

        return new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
    }

    static bool IsValidSceneryPosition(BattleMapDefinition map, Vector3 pos)
    {
        if (map == null)
            return true;

        if (map.Waters != null)
        {
            for (int i = 0; i < map.Waters.Length; i++)
            {
                if (ContainsRotatedRect(pos, map.Waters[i].Center, map.Waters[i].Size, map.Waters[i].Angle, 4f))
                    return false;
            }
        }

        if (map.Roads != null)
        {
            for (int i = 0; i < map.Roads.Length; i++)
            {
                if (ContainsRotatedRect(pos, map.Roads[i].Center, map.Roads[i].Size, map.Roads[i].Angle, 3f))
                    return false;
            }
        }

        if (map.Patches != null)
        {
            for (int i = 0; i < map.Patches.Length; i++)
            {
                TerrainPatchSpec patch = map.Patches[i];
                if (patch.PaletteIndex == 0)
                    continue;
                if (ContainsRotatedRect(pos, patch.Center, patch.Size, patch.Angle, 6f))
                    return false;
            }
        }

        return true;
    }

    static bool IsValidFrontlineScatterPosition(BattleMapDefinition map, Vector3 pos)
    {
        if (map == null)
            return true;

        if (map.Waters != null)
        {
            for (int i = 0; i < map.Waters.Length; i++)
            {
                if (ContainsRotatedRect(pos, map.Waters[i].Center, map.Waters[i].Size, map.Waters[i].Angle, 5f))
                    return false;
            }
        }

        return Mathf.Abs(pos.x) <= BattleMapCatalog.MapHalfSize - 12f
            && Mathf.Abs(pos.z) <= BattleMapCatalog.MapHalfSize - 12f;
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
        if (name == BattleMapCatalog.CityRuinsName)
        {
            return new[]
            {
                "nature:rock_smallA", "nature:rock_smallB", "nature:rock_smallFlatA", "nature:log_stack",
                "survival:metal-panel", "survival:rock-sand-a", "castle:rocks-small", "nature:plant_bushSmall"
            };
        }
        if (name == BattleMapCatalog.IceFortressName)
        {
            return new[]
            {
                "nature:rock_largeD", "nature:rock_largeE", "nature:rock_smallFlatB", "nature:rock_tallA",
                "castle:rocks-large", "castle:rocks-small", "nature:tree_pineSmallA", "nature:tree_pineSmallB"
            };
        }
        if (name == BattleMapCatalog.JungleName)
        {
            return new[]
            {
                "nature:plant_bush", "nature:plant_bushDetailed", "nature:plant_bushLarge", "nature:grass_leafs", "nature:grass_leafsLarge",
                "nature:flower_redA", "nature:flower_yellowA", "nature:log_large", "nature:log_stack", "nature:mushroom_redGroup",
                "nature:rock_smallC", "nature:rock_smallD"
            };
        }
        if (name == BattleMapCatalog.DefaultMapName || name == BattleMapCatalog.GlobalConquestName)
        {
            return new[]
            {
                "nature:grass", "nature:grass_large", "nature:grass_leafs", "nature:grass_leafsLarge",
                "nature:plant_bushSmall", "nature:plant_bush", "nature:plant_bushDetailed",
                "nature:flower_yellowA", "nature:flower_redA", "nature:flower_purpleA",
                "nature:log", "nature:log_large", "pirate:patch-grass-foliage"
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

    static Vector3 PickTreeScatterLocalPosition(float minRadius, float maxRadius)
    {
        Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
        if (circle.sqrMagnitude < 0.0001f)
            circle = new Vector2(minRadius, 0f);
        return new Vector3(circle.x, 0f, circle.y);
    }

    static float PickTreeScatterScale(string model)
    {
        if (string.IsNullOrEmpty(model))
            return 1f;
        if (model.Contains("flower") || model.Contains("mushroom"))
            return Random.Range(0.45f, 0.72f);
        if (model.Contains("grass") || model.Contains("patch"))
            return Random.Range(0.55f, 0.95f);
        if (model.Contains("bush") || model.Contains("plant"))
            return Random.Range(0.62f, 1.02f);
        if (model.Contains("log"))
            return Random.Range(0.52f, 0.88f);
        if (model.Contains("rock"))
            return Random.Range(0.45f, 0.82f);
        return Random.Range(0.58f, 0.90f);
    }

    static Color PickTreeScatterTint(string model, Color trunkColor, Color foliageColor)
    {
        if (string.IsNullOrEmpty(model))
            return foliageColor;
        if (model.Contains("rock"))
            return Color.Lerp(trunkColor, Color.gray, 0.42f);
        if (model.Contains("log"))
            return Color.Lerp(trunkColor, new Color(0.42f, 0.28f, 0.16f), 0.38f);
        if (model.Contains("flower"))
            return Color.Lerp(foliageColor, Color.white, 0.18f);
        return Color.Lerp(foliageColor, Color.white, 0.08f);
    }

    static void SpawnWaterBlocker(BattleMapDefinition map, TerrainStripSpec water)
    {
        if (map == null || map.Name == BattleMapCatalog.SeaChartName)
            return;

        var blocker = new GameObject("WaterBlocker_" + water.Name);
        blocker.transform.position = new Vector3(water.Center.x, 1.2f, water.Center.z);
        blocker.transform.rotation = Quaternion.Euler(0f, water.Angle, 0f);

        var collider = blocker.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = new Vector3(
            Mathf.Max(4f, water.Size.x - 1.2f),
            2.4f,
            Mathf.Max(6f, water.Size.y - 1.2f));
    }

    static void AddTreeCollider(GameObject root)
    {
        if (root == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(root, out bounds))
            return;

        var collider = root.GetComponent<CapsuleCollider>();
        if (collider == null)
            collider = root.AddComponent<CapsuleCollider>();

        float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.18f, 0.45f, 1.6f);
        float height = Mathf.Clamp(bounds.size.y * 0.42f, 2.2f, 6.4f);
        Vector3 trunkCenterWorld = new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z);

        collider.direction = 1;
        collider.radius = radius;
        collider.height = Mathf.Max(height, radius * 2f + 0.1f);
        collider.center = root.transform.InverseTransformPoint(trunkCenterWorld);
    }

    static bool ContainsRotatedRect(Vector3 point, Vector3 center, Vector2 size, float yaw, float padding)
    {
        Quaternion inverse = Quaternion.Euler(0f, -yaw, 0f);
        Vector3 local = inverse * (point - center);
        Vector2 half = size * 0.5f + new Vector2(padding, padding);
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
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
