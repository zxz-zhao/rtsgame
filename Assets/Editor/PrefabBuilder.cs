using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.AI;

// 自动生成单位和建筑的占位Prefab（分型多部件 + 金属材质）
public class PrefabBuilder
{
    static string PrefabPath    = "Assets/Prefabs";
    static string ResPrefabPath = "Assets/Resources/Prefabs";

    enum UnitType { Infantry, Artillery, Flamethrower, Tank, Fighter, Bomber, ScoutPlane }
    enum BldType  { MainBase, Barracks, AirFactory, TankFactory, PowerPlant, GoldMine, Turret }

    const string KenneyBlockyFbxPath  = "Assets/External/Kenney/BlockyCharacters/Models/FBX format/";
    const string KenneyBlasterFbxPath = "Assets/External/Kenney/BlasterKit/Models/FBX format/";
    const string KenneyCarFbxPath     = "Assets/External/Kenney/CarKit/Models/FBX format/";
    const string KenneyNatureFbxPath  = "Assets/External/Kenney/NatureKit/Models/FBX format/";
    const string KenneyModBldFbxPath  = "Assets/External/Kenney/ModularBuildings/Models/FBX format/";
    const string KenneyTDKitFbxPath   = "Assets/External/Kenney/TowerDefenseKit/Models/FBX format/";
    const string KenneySurvivalFbxPath = "Assets/External/Kenney/SurvivalKit/Models/FBX format/";
    const string KenneyRetroUrbanFbxPath = "Assets/External/Kenney/RetroUrbanKit/Models/FBX format/";
    const string KenneyAnimSurvivorPath  = "Assets/External/Kenney/AnimatedCharactersSurvivors/Model/";
    const string KenneyAnimSurvivorAnimPath = "Assets/External/Kenney/AnimatedCharactersSurvivors/Animations/";
    const string KenneyCastleFbxPath     = "Assets/External/Kenney/CastleKit/Models/FBX format/";
    const string KenneyTrainFbxPath      = "Assets/External/Kenney/TrainKit/Models/FBX format/";
    const string KenneyPirateFbxPath     = "Assets/External/MilitaryModels/Kenney/Extracted/Models/FBX format/";
    const string KenneySpaceFbxPath      = "Assets/External/Kenney/SpaceKit/Models/FBX format/";
    const string DownloadCityIndustrialFbxPath = "Assets/External/Downloads/city-industrial/Models/FBX format/";
    const string DownloadFactoryKitFbxPath = "Assets/External/Downloads/factory-kit/Models/FBX format/";
    const string SurvivorAnimatorControllerPath = "Assets/Resources/Animations/Generated/SurvivorLocomotion_v2.controller";

    // ── 阵营配色 ──────────────────────────────────────────────────────────────
    static readonly Color P_BLUE   = new Color(0.20f, 0.44f, 0.82f);   // 玩家主色
    static readonly Color P_CYAN   = new Color(0.15f, 0.68f, 0.82f);   // 玩家飞行器
    static readonly Color P_YELLOW = new Color(0.90f, 0.76f, 0.08f);   // 玩家特殊
    static readonly Color P_GREEN  = new Color(0.18f, 0.68f, 0.25f);   // 玩家炮塔/兵营
    static readonly Color P_WHITE  = new Color(0.88f, 0.92f, 0.96f);   // 玩家侦察
    static readonly Color E_RED    = new Color(0.72f, 0.12f, 0.12f);   // 敌方主色
    static readonly Color E_ORANGE = new Color(0.82f, 0.38f, 0.06f);   // 敌方特殊
    static readonly Color E_GOLD   = new Color(0.75f, 0.58f, 0.04f);   // 敌方黄色
    static readonly Color E_GRAY   = new Color(0.50f, 0.20f, 0.20f);   // 敌方侦察
    static readonly Color METAL    = new Color(0.48f, 0.50f, 0.55f);   // 金属配件

    [MenuItem("RTS/生成场景/③ 生成所有Prefab")]
    public static void BuildAllPrefabs()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // 场地单位 ─ 玩家
        MakeUnit<Infantry>   ("Infantry_Player",   P_BLUE,   new Vector3(0.5f,1f,0.5f),    true,  UnitType.Infantry);
        MakeUnit<Artillery>  ("Artillery_Player",  P_CYAN,   new Vector3(0.6f,0.8f,0.6f),  true,  UnitType.Artillery);
        MakeUnit<Flamethrower>("Flame_Player",     P_YELLOW, new Vector3(0.9f,0.9f,1.35f), true,  UnitType.Flamethrower);
        MakeUnit<Tank>       ("Tank_Player",       P_BLUE,   new Vector3(1.6f,1.0f,2.4f),  true,  UnitType.Tank);
        MakeUnit<Fighter>    ("Fighter_Player",    P_CYAN,   new Vector3(1.8f,0.4f,1.2f),  true,  UnitType.Fighter);
        MakeUnit<Bomber>     ("Bomber_Player",     P_BLUE,   new Vector3(2.5f,0.5f,1.5f),  true,  UnitType.Bomber);
        MakeUnit<ScoutPlane> ("Scout_Player",      P_WHITE,  new Vector3(1.5f,0.3f,1f),    true,  UnitType.ScoutPlane);
        // 场地单位 ─ 敌方
        MakeUnit<Infantry>   ("Infantry_Enemy",    E_RED,    new Vector3(0.5f,1f,0.5f),    false, UnitType.Infantry);
        MakeUnit<Artillery>  ("Artillery_Enemy",   E_ORANGE, new Vector3(0.6f,0.8f,0.6f),  false, UnitType.Artillery);
        MakeUnit<Flamethrower>("Flame_Enemy",      E_ORANGE, new Vector3(0.9f,0.9f,1.35f), false, UnitType.Flamethrower);
        MakeUnit<Tank>       ("Tank_Enemy",        E_RED,    new Vector3(1.6f,1.0f,2.4f),  false, UnitType.Tank);
        MakeUnit<Fighter>    ("Fighter_Enemy",     E_RED,    new Vector3(1.8f,0.4f,1.2f),  false, UnitType.Fighter);
        MakeUnit<Bomber>     ("Bomber_Enemy",      E_ORANGE, new Vector3(2.5f,0.5f,1.5f),  false, UnitType.Bomber);
        MakeUnit<ScoutPlane> ("Scout_Enemy",       E_GRAY,   new Vector3(1.5f,0.3f,1f),    false, UnitType.ScoutPlane);
        // 建筑 ─ 玩家
        MakeBld<MainBase>   ("MainBase_Player",    P_BLUE,   new Vector3(4f,2f,4f),        true,  BldType.MainBase);
        MakeBld<Barracks>   ("Barracks_Player",    P_BLUE,   new Vector3(3f,1.5f,2.5f),    true,  BldType.Barracks);
        MakeBld<AirFactory> ("AirFactory_Player",  P_CYAN,   new Vector3(4f,1.2f,3f),      true,  BldType.AirFactory);
        MakeBld<TankFactory>("TankFactory_Player", P_BLUE,   new Vector3(3.5f,1.4f,3f),    true,  BldType.TankFactory);
        MakeBld<PowerPlant> ("PowerPlant_Player",  P_YELLOW, new Vector3(2.5f,2f,2.5f),    true,  BldType.PowerPlant);
        MakeBld<GoldMine>   ("GoldMine_Player",    new Color(0.95f,0.78f,0f), new Vector3(2f,1.2f,2f), true, BldType.GoldMine);
        MakeBld<Turret>     ("Turret_Player",      P_GREEN,  new Vector3(1.5f,2.5f,1.5f),  true,  BldType.Turret);
        // 建筑 ─ 敌方
        MakeBld<MainBase>   ("MainBase_Enemy",     E_RED,    new Vector3(4f,2f,4f),        false, BldType.MainBase);
        MakeBld<Barracks>   ("Barracks_Enemy",     E_RED,    new Vector3(3f,1.5f,2.5f),    false, BldType.Barracks);
        MakeBld<AirFactory> ("AirFactory_Enemy",   E_ORANGE, new Vector3(4f,1.2f,3f),      false, BldType.AirFactory);
        MakeBld<TankFactory>("TankFactory_Enemy",  E_RED,    new Vector3(3.5f,1.4f,3f),    false, BldType.TankFactory);
        MakeBld<PowerPlant> ("PowerPlant_Enemy",   E_GOLD,   new Vector3(2.5f,2f,2.5f),    false, BldType.PowerPlant);
        MakeBld<GoldMine>   ("GoldMine_Enemy",     new Color(0.78f,0.60f,0f), new Vector3(2f,1.2f,2f), false, BldType.GoldMine);
        MakeBld<Turret>     ("Turret_Enemy",       E_ORANGE, new Vector3(1.5f,2.5f,1.5f),  false, BldType.Turret);

        // 历史场景/预览工具可能还会按 Prefabs/*_Player 或 *_Enemy 从 Resources 读取。
        // 这里把刚生成的下载模型版本同步过去，避免旧手工方块 prefab 再混进运行时。
        MirrorLegacyPlayerEnemyPrefabsToResources();

        // Resources 版 ─ AI 单位（红色）
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Resources", "Prefabs"));
        MakeUnitRes<Infantry>   ("Infantry",    E_RED,    new Vector3(0.5f,1f,0.5f),    false, UnitType.Infantry);
        MakeUnitRes<Artillery>  ("Artillery",   E_ORANGE, new Vector3(0.6f,0.8f,0.6f),  false, UnitType.Artillery);
        MakeUnitRes<Flamethrower>("Flamethrower",E_ORANGE,new Vector3(0.9f,0.9f,1.35f), false, UnitType.Flamethrower);
        MakeUnitRes<Tank>       ("Tank",        E_RED,    new Vector3(1.6f,1.0f,2.4f),  false, UnitType.Tank);
        MakeUnitRes<Fighter>    ("Fighter",     E_RED,    new Vector3(1.8f,0.4f,1.2f),  false, UnitType.Fighter);
        MakeUnitRes<Bomber>     ("Bomber",      E_ORANGE, new Vector3(2.5f,0.5f,1.5f),  false, UnitType.Bomber);
        MakeUnitRes<ScoutPlane> ("ScoutPlane",  E_GRAY,   new Vector3(1.5f,0.3f,1f),    false, UnitType.ScoutPlane);
        // Resources 版 ─ 玩家单位
        MakeUnitRes<Infantry>   ("Infantry_P",    P_BLUE,   new Vector3(0.5f,1f,0.5f),    true, UnitType.Infantry);
        MakeUnitRes<Artillery>  ("Artillery_P",   P_CYAN,   new Vector3(0.6f,0.8f,0.6f),  true, UnitType.Artillery);
        MakeUnitRes<Tank>       ("Tank_P",         P_BLUE,   new Vector3(1.6f,1.0f,2.4f),  true, UnitType.Tank);
        MakeUnitRes<Flamethrower>("Flamethrower_P",P_YELLOW, new Vector3(0.9f,0.9f,1.35f), true, UnitType.Flamethrower);
        MakeUnitRes<Fighter>    ("Fighter_P",     P_CYAN,   new Vector3(1.8f,0.4f,1.2f),  true, UnitType.Fighter);
        MakeUnitRes<Bomber>     ("Bomber_P",      P_BLUE,   new Vector3(2.5f,0.5f,1.5f),  true, UnitType.Bomber);
        MakeUnitRes<ScoutPlane> ("ScoutPlane_P",  P_WHITE,  new Vector3(1.5f,0.3f,1f),    true, UnitType.ScoutPlane);
        // Resources 版 ─ 玩家建筑
        MakeBldRes<MainBase>   ("MainBase_P",    P_BLUE,   new Vector3(4f,2f,4f),        BldType.MainBase);
        MakeBldRes<Barracks>   ("Barracks_P",    P_GREEN,  new Vector3(3f,1.5f,2.5f),    BldType.Barracks);
        MakeBldRes<AirFactory> ("AirFactory_P",  P_CYAN,   new Vector3(4f,1.2f,3f),      BldType.AirFactory);
        MakeBldRes<TankFactory>("TankFactory_P", new Color(0.38f,0.28f,0.10f), new Vector3(3.5f,1.4f,3f), BldType.TankFactory);
        MakeBldRes<Turret>     ("Turret_P",      P_GREEN,  new Vector3(1.5f,2.5f,1.5f),  BldType.Turret);
        MakeBldRes<GoldMine>   ("GoldMine_P",    new Color(0.92f,0.72f,0.05f), new Vector3(2f,1.2f,2f), BldType.GoldMine);
        MakeBldRes<PowerPlant> ("PowerPlant_P",  P_YELLOW, new Vector3(2.5f,2f,2.5f),    BldType.PowerPlant);
        // Resources 版 ─ 敌方单位（_E 后缀，联机 RemoteSpawnUnit 加载）
        MakeUnitRes<Infantry>   ("Infantry_E",    E_RED,    new Vector3(0.5f,1f,0.5f),    false, UnitType.Infantry);
        MakeUnitRes<Artillery>  ("Artillery_E",   E_ORANGE, new Vector3(0.6f,0.8f,0.6f),  false, UnitType.Artillery);
        MakeUnitRes<Flamethrower>("Flamethrower_E",E_ORANGE,new Vector3(0.9f,0.9f,1.35f), false, UnitType.Flamethrower);
        MakeUnitRes<Tank>       ("Tank_E",        E_RED,    new Vector3(1.6f,1.0f,2.4f),  false, UnitType.Tank);
        MakeUnitRes<Fighter>    ("Fighter_E",     E_RED,    new Vector3(1.8f,0.4f,1.2f),  false, UnitType.Fighter);
        MakeUnitRes<Bomber>     ("Bomber_E",      E_ORANGE, new Vector3(2.5f,0.5f,1.5f),  false, UnitType.Bomber);
        MakeUnitRes<ScoutPlane> ("ScoutPlane_E",  E_GRAY,   new Vector3(1.5f,0.3f,1f),    false, UnitType.ScoutPlane);
        // Resources 版 ─ 敌方建筑（_E 后缀）
        MakeBldRes<MainBase>   ("MainBase_E",    E_RED,    new Vector3(4f,2f,4f),        BldType.MainBase,    false);
        MakeBldRes<Barracks>   ("Barracks_E",    E_RED,    new Vector3(3f,1.5f,2.5f),    BldType.Barracks,    false);
        MakeBldRes<AirFactory> ("AirFactory_E",  E_ORANGE, new Vector3(4f,1.2f,3f),      BldType.AirFactory,  false);
        MakeBldRes<TankFactory>("TankFactory_E", E_RED,    new Vector3(3.5f,1.4f,3f),    BldType.TankFactory, false);
        MakeBldRes<PowerPlant> ("PowerPlant_E",  E_GOLD,   new Vector3(2.5f,2f,2.5f),    BldType.PowerPlant,  false);
        MakeBldRes<GoldMine>   ("GoldMine_E",    new Color(0.78f,0.60f,0f), new Vector3(2f,1.2f,2f), BldType.GoldMine, false);
        MakeBldRes<Turret>     ("Turret_E",      E_ORANGE, new Vector3(1.5f,2.5f,1.5f),  BldType.Turret,      false);

        BuildBattleMapTilePrefabs();
        BuildBattlefieldPropPrefabs();
        BuildProjectilePrefabs();
        BuildAirModelPrefabs();
        InstallIntegratedInfantryModelIfAvailable();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ 所有Prefab生成完毕（分型多部件）！");
    }

    // ── 统一入口 ──────────────────────────────────────────────────────────────
    static void MakeUnit<T>(string n, Color c, Vector3 s, bool p, UnitType t) where T : RTSUnit
    { var r = UnitMesh(n,c,s,t,p); AddUnitComps<T>(r,s,p,t); Save(r,$"{PrefabPath}/{n}.prefab"); }

    static void MakeUnitRes<T>(string n, Color c, Vector3 s, bool p, UnitType t) where T : RTSUnit
    { var r = UnitMesh(n,c,s,t,p); AddUnitComps<T>(r,s,p,t); Save(r,$"{ResPrefabPath}/{n}.prefab"); }

    public static void BuildUnitPrefabsOnly()
    {
        BuildAllPrefabs();
    }

    static void MakeBld<T>(string n, Color c, Vector3 s, bool p, BldType t) where T : RTSBuilding
    { var r = BldMesh(n,c,s,t,p); AddBldComps<T>(r,s,p); Save(r,$"{PrefabPath}/{n}.prefab"); }

    static void MakeBldRes<T>(string n, Color c, Vector3 s, BldType t, bool p = true) where T : RTSBuilding
    { var r = BldMesh(n,c,s,t,p); AddBldComps<T>(r,s,p); Save(r,$"{ResPrefabPath}/{n}.prefab"); }

    static void Save(GameObject root, string path)
    { PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); }

    static void InstallIntegratedInfantryModelIfAvailable()
    {
        string modelPath = System.IO.Path.Combine(Application.dataPath, "External", "Mixamo", "BasicShooter", "X Bot.fbx");
        if (!System.IO.File.Exists(modelPath))
            return;

        try
        {
            MixamoBasicShooterInstaller.Install();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PrefabBuilder] Mixamo Basic Shooter infantry install failed: " + e.Message);
        }
    }

    static void MirrorLegacyPlayerEnemyPrefabsToResources()
    {
        System.IO.Directory.CreateDirectory(ResPrefabPath);

        string[] names =
        {
            "Infantry_Player", "Infantry_Enemy",
            "Artillery_Player", "Artillery_Enemy",
            "Flame_Player", "Flame_Enemy",
            "Tank_Player", "Tank_Enemy",
            "Fighter_Player", "Fighter_Enemy",
            "Bomber_Player", "Bomber_Enemy",
            "Scout_Player", "Scout_Enemy",
            "MainBase_Player", "MainBase_Enemy",
            "Barracks_Player", "Barracks_Enemy",
            "AirFactory_Player", "AirFactory_Enemy",
            "TankFactory_Player", "TankFactory_Enemy",
            "PowerPlant_Player", "PowerPlant_Enemy",
            "GoldMine_Player", "GoldMine_Enemy",
            "Turret_Player", "Turret_Enemy",
        };

        for (int i = 0; i < names.Length; i++)
        {
            string src = $"{PrefabPath}/{names[i]}.prefab";
            string dst = $"{ResPrefabPath}/{names[i]}.prefab";
            if (!System.IO.File.Exists(src)) continue;

            // 只覆盖 prefab 内容，不碰 .meta；这样既清掉旧手工模型，也不破坏已有引用。
            System.IO.File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
        }
    }

    const string GeneratedMaterialPath = "Assets/Resources/Materials/Generated";
    const string ImportedMaterialPath = "Assets/Resources/Materials/Imported";

    static Material BuildMat(Color color, float metallic = 0.2f, float glossiness = 0.4f)
    {
        System.IO.Directory.CreateDirectory(GeneratedMaterialPath);

        string path = $"{GeneratedMaterialPath}/{MaterialKey(color, metallic, glossiness)}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        RendererColorUtil.TrySetColor(mat, color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", glossiness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static string MaterialKey(Color c, float metallic, float glossiness)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        int a = Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
        int m = Mathf.Clamp(Mathf.RoundToInt(metallic * 100f), 0, 100);
        int s = Mathf.Clamp(Mathf.RoundToInt(glossiness * 100f), 0, 100);
        return $"Mat_{r:X2}{g:X2}{b:X2}{a:X2}_M{m:000}_G{s:000}";
    }

    static Material BuildImportedMat(string assetPath, Material sourceMat, Color tint, bool factionTint, int slotIndex)
    {
        System.IO.Directory.CreateDirectory(ImportedMaterialPath);

        string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string sourceName = sourceMat != null ? SanitizeAssetName(sourceMat.name) : $"Slot{slotIndex}";
        string tintKey = factionTint ? MaterialKey(tint, 0f, 0.35f) : "Native";
        string matPath = $"{ImportedMaterialPath}/{SanitizeAssetName(assetName)}_{sourceName}_{tintKey}.mat";

        Texture sourceTexture = FindSourceTexture(sourceMat);
        Texture2D fallbackTexture = LoadFallbackImportedTexture(assetPath);
        Texture texture = sourceTexture != null ? sourceTexture : fallbackTexture;
        Shader targetShader = texture != null ? ImportedModelShader() : ImportedColorShader();

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(targetShader != null ? targetShader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (sourceMat != null)
        {
            mat.CopyPropertiesFromMaterial(sourceMat);
        }

        if (targetShader != null && mat.shader != targetShader)
            mat.shader = targetShader;

        AssignMainTexture(mat, texture);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);

        if (RendererColorUtil.TryGetColor(mat, out _))
        {
            Color baseColor = sourceMat != null && RendererColorUtil.TryGetColor(sourceMat, out Color sourceColor)
                ? sourceColor
                : Color.white;
            baseColor = NormalizeImportedColor(baseColor, texture != null);
            RendererColorUtil.TrySetColor(mat, factionTint ? Color.Lerp(baseColor, tint, 0.22f) : baseColor);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Shader ImportedModelShader()
    {
        return Shader.Find("Unlit/Texture")
            ?? Shader.Find("Mobile/Unlit (Supports Lightmap)")
            ?? Shader.Find("Mobile/Diffuse")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Diffuse");
    }

    static Shader ImportedColorShader()
    {
        return Shader.Find("Unlit/Color")
            ?? Shader.Find("Mobile/Unlit (Supports Lightmap)")
            ?? Shader.Find("Mobile/Diffuse")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Diffuse");
    }

    static Texture FindSourceTexture(Material sourceMat)
    {
        if (sourceMat == null) return null;
        if (sourceMat.HasProperty("_MainTex"))
        {
            Texture texture = sourceMat.GetTexture("_MainTex");
            if (texture != null) return texture;
        }
        if (sourceMat.HasProperty("_BaseMap"))
        {
            Texture texture = sourceMat.GetTexture("_BaseMap");
            if (texture != null) return texture;
        }
        if (sourceMat.mainTexture != null)
            return sourceMat.mainTexture;
        return null;
    }

    static Color NormalizeImportedColor(Color color, bool hasTexture)
    {
        if (hasTexture)
            return Color.white;

        float brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (brightness < 0.42f)
        {
            Color lifted = Color.Lerp(color, new Color(0.62f, 0.60f, 0.54f, Mathf.Max(color.a, 1f)), 0.55f);
            return new Color(
                Mathf.Max(lifted.r, 0.34f),
                Mathf.Max(lifted.g, 0.34f),
                Mathf.Max(lifted.b, 0.32f),
                Mathf.Max(color.a, 1f));
        }
        return color;
    }

    static string ResolveImportedTexturePath(string assetPath)
    {
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        if (assetPath.StartsWith(KenneyBlockyFbxPath))
        {
            int dash = file.LastIndexOf('-');
            string suffix = dash >= 0 ? file.Substring(dash + 1) : "a";
            return KenneyBlockyFbxPath + "Textures/texture-" + suffix + ".png";
        }
        if (assetPath.StartsWith(KenneyBlasterFbxPath))
            return KenneyBlasterFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyCarFbxPath))
            return KenneyCarFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyModBldFbxPath))
            return KenneyModBldFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyTDKitFbxPath))
            return KenneyTDKitFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneySurvivalFbxPath))
            return KenneySurvivalFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyCastleFbxPath))
            return KenneyCastleFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyTrainFbxPath))
            return KenneyTrainFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyPirateFbxPath))
            return KenneyPirateFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneySpaceFbxPath))
            return KenneySpaceFbxPath + "Textures/colormap.png";
        if (assetPath.StartsWith(KenneyAnimSurvivorPath))
            return "Assets/External/Kenney/AnimatedCharactersSurvivors/Skins/survivorMaleB.png";
        return null;
    }

    static Texture2D LoadFallbackImportedTexture(string assetPath)
    {
        string texturePath = ResolveImportedTexturePath(assetPath);
        return string.IsNullOrEmpty(texturePath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    }

    static void AssignMainTexture(Material mat, Texture texture)
    {
        if (mat == null || texture == null) return;
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", texture);
    }

    static string SanitizeAssetName(string value)
    {
        return value.Replace(" ", "_").Replace("-", "_").Replace(".", "_");
    }

    static AnimationClip LoadAnimationClipAsset(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                return clip;
        }
        return null;
    }

    static RuntimeAnimatorController EnsureSurvivorAnimatorController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SurvivorAnimatorControllerPath);
        if (controller != null)
            return controller;

        System.IO.Directory.CreateDirectory("Assets/Resources/Animations/Generated");

        AnimationClip idleClip = LoadAnimationClipAsset(KenneyAnimSurvivorAnimPath + "idle.fbx");
        AnimationClip runClip = LoadAnimationClipAsset(KenneyAnimSurvivorAnimPath + "run.fbx");
        AnimationClip jumpClip = LoadAnimationClipAsset(KenneyAnimSurvivorAnimPath + "jump.fbx");
        if (idleClip == null || runClip == null || jumpClip == null)
            return null;

        controller = AnimatorController.CreateAnimatorControllerAtPath(SurvivorAnimatorControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = idleClip;
        sm.defaultState = idle;

        var run = sm.AddState("Run");
        run.motion = runClip;

        var fire = sm.AddState("Fire");
        fire.motion = jumpClip;
        fire.speed = 1.35f;

        var die = sm.AddState("Die");
        die.motion = jumpClip;
        die.speed = 0.55f;

        var idleToRun = idle.AddTransition(run);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.12f, "Speed");
        idleToRun.duration = 0.12f;

        var runToIdle = run.AddTransition(idle);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");
        runToIdle.duration = 0.12f;

        var anyToFire = sm.AddAnyStateTransition(fire);
        anyToFire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
        anyToFire.hasExitTime = false;
        anyToFire.duration = 0.04f;
        anyToFire.canTransitionToSelf = false;

        var fireToIdle = fire.AddTransition(idle);
        fireToIdle.hasExitTime = true;
        fireToIdle.exitTime = 0.92f;
        fireToIdle.duration = 0.05f;
        fireToIdle.AddCondition(AnimatorConditionMode.Less, 0.10f, "Speed");

        var fireToRun = fire.AddTransition(run);
        fireToRun.hasExitTime = true;
        fireToRun.exitTime = 0.78f;
        fireToRun.duration = 0.05f;
        fireToRun.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");

        var anyToDie = sm.AddAnyStateTransition(die);
        anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        anyToDie.hasExitTime = false;
        anyToDie.duration = 0.03f;
        anyToDie.canTransitionToSelf = false;

        AssetDatabase.SaveAssets();
        return controller;
    }

    // ── 单位几何体构建 ────────────────────────────────────────────────────────
    static GameObject UnitMesh(string name, Color col, Vector3 s, UnitType t, bool player)
    {
        var prefabRoot = new GameObject(name);
        var root = new GameObject("Model");
        root.transform.SetParent(prefabRoot.transform, false);
        Color dk = Dk(col); Color lt = Lt(col);
        switch (t)
        {
            case UnitType.Infantry:
                Color infantryCol = WW2Tint(col, player);
                Color cloth = Dk(infantryCol, 0.88f);
                P(PrimitiveType.Capsule,  root,"Body",       V(0,s.y*0.32f,0),                 V(s.x*0.58f,s.y*0.62f,s.z*0.50f), infantryCol, 0.10f,0.30f);
                P(PrimitiveType.Sphere,   root,"Head",       V(0,s.y*1.04f,0),                 V3(s.x*0.36f),                    new Color(0.72f,0.54f,0.40f),0.02f,0.18f);
                P(PrimitiveType.Sphere,   root,"HelmetDome", V(0,s.y*1.18f,0),                 V(s.x*0.42f,s.y*0.12f,s.z*0.42f), Dk(infantryCol,0.66f),0.28f,0.36f);
                P(PrimitiveType.Cylinder, root,"HelmetBrim", V(0,s.y*1.13f,s.z*0.04f),         V(s.x*0.52f,s.x*0.035f,s.x*0.52f),Dk(infantryCol,0.58f),0.30f,0.38f);
                P(PrimitiveType.Cube,     root,"FieldPack",  V(0,s.y*0.42f,-s.z*0.34f),        V(s.x*0.42f,s.y*0.42f,s.z*0.16f), WW2_WOOD,0.06f,0.24f);
                P(PrimitiveType.Cube,     root,"Webbing",    V(0,s.y*0.55f,s.z*0.28f),         V(s.x*0.42f,s.y*0.05f,s.z*0.05f), WW2_WOOD,0.04f,0.20f);
                InfantryLimbs(root, s, cloth, new Color(0.08f,0.075f,0.06f));
                AddWW2Rifle(root, "Rifle", V(s.x*0.34f,s.y*0.60f,s.z*0.26f), V(6f,88f,0f), V3(1f));
                break;
            case UnitType.Flamethrower:
                Color flameCol = WW2Tint(col, player);
                Color flameTrack = new Color(0.06f,0.055f,0.045f);
                P(PrimitiveType.Cube,     root,"LowerHull",   V(0,s.y*0.25f,0),              V(s.x*1.05f,s.y*0.34f,s.z*0.92f), Dk(flameCol,0.72f),0.34f,0.48f);
                P(PrimitiveType.Cube,     root,"UpperHull",   V(0,s.y*0.48f,s.z*0.02f),      V(s.x*0.78f,s.y*0.26f,s.z*0.58f), flameCol,0.34f,0.50f);
                P(PrimitiveType.Cube,     root,"TrackL",      V(-s.x*0.56f,s.y*0.14f,0),     V(s.x*0.18f,s.y*0.28f,s.z*1.04f), flameTrack,0.08f,0.12f);
                P(PrimitiveType.Cube,     root,"TrackR",      V( s.x*0.56f,s.y*0.14f,0),     V(s.x*0.18f,s.y*0.28f,s.z*1.04f), flameTrack,0.08f,0.12f);
                TrackWheels(root, s, WW2_METAL);
                AddWW2FuelTank(root, "FuelTankL", V(-s.x*0.27f,s.y*0.62f,-s.z*0.30f), V(0f,0f,0f), V(0.16f,0.36f,0.16f));
                AddWW2FuelTank(root, "FuelTankR", V( s.x*0.27f,s.y*0.62f,-s.z*0.30f), V(0f,0f,0f), V(0.16f,0.36f,0.16f));
                AddWW2FlameNozzle(root, "Nozzle", V(0,s.y*0.62f,s.z*0.52f), V(0f,0f,0f), V3(1f));
                P(PrimitiveType.Cylinder, root,"CommanderCupola",V(-s.x*0.18f,s.y*0.76f,-s.z*0.02f),V(s.x*0.14f,s.y*0.08f,s.x*0.14f),WW2_METAL,0.58f,0.72f);
                P(PrimitiveType.Cube,     root,"ArmorMark",   V(0,s.y*0.55f,s.z*0.32f),      V(s.x*0.22f,s.y*0.055f,s.z*0.035f), player ? P_BLUE : E_RED,0.08f,0.35f);
                break;
            case UnitType.Artillery:
                Color artilleryCol = WW2Tint(col, player);
                Color artilleryCloth = Dk(artilleryCol, 0.88f);
                P(PrimitiveType.Capsule,  root,"Body",       V(0,s.y*0.32f,0),                 V(s.x*0.58f,s.y*0.62f,s.z*0.50f), artilleryCol, 0.10f,0.30f);
                P(PrimitiveType.Sphere,   root,"Head",       V(0,s.y*1.04f,0),                 V3(s.x*0.36f),                    new Color(0.72f,0.54f,0.40f),0.02f,0.18f);
                P(PrimitiveType.Sphere,   root,"HelmetDome", V(0,s.y*1.18f,0),                 V(s.x*0.42f,s.y*0.12f,s.z*0.42f), Dk(artilleryCol,0.66f),0.28f,0.36f);
                P(PrimitiveType.Cylinder, root,"HelmetBrim", V(0,s.y*1.13f,s.z*0.04f),         V(s.x*0.52f,s.x*0.035f,s.x*0.52f),Dk(artilleryCol,0.58f),0.30f,0.38f);
                P(PrimitiveType.Cube,     root,"FieldPack",  V(0,s.y*0.42f,-s.z*0.34f),        V(s.x*0.42f,s.y*0.42f,s.z*0.16f), WW2_WOOD,0.06f,0.24f);
                P(PrimitiveType.Cube,     root,"Webbing",    V(0,s.y*0.55f,s.z*0.28f),         V(s.x*0.42f,s.y*0.05f,s.z*0.05f), WW2_WOOD,0.04f,0.20f);
                InfantryLimbs(root, s, artilleryCloth, new Color(0.08f,0.075f,0.06f));
                AddWW2ShoulderCannon(root, "ShoulderCannon", V(s.x*0.32f,s.y*0.78f,s.z*0.28f), V(2f,88f,-7f), V3(1.06f));
                break;
            case UnitType.Tank:
                Color tankCol = WW2Tint(col, player);
                Color tankDk = Dk(tankCol);
                Color track = new Color(0.075f,0.075f,0.065f);
                Color rubber = new Color(0.04f,0.04f,0.035f);
                Color marking = player ? new Color(0.20f,0.42f,0.82f) : new Color(0.72f,0.16f,0.12f);
                P(PrimitiveType.Cube,    root,"LowerHull",   V(0,s.y*0.28f,0),            V(s.x*0.98f,s.y*0.34f,s.z*0.92f), Dk(tankCol,0.76f),0.40f,0.56f);
                P(PrimitiveType.Cube,    root,"UpperHull",   V(0,s.y*0.50f,0.02f),        V(s.x*0.76f,s.y*0.24f,s.z*0.66f), tankCol,0.42f,0.60f);
                var glacis=P(PrimitiveType.Cube,root,"FrontGlacis",V(0,s.y*0.47f,s.z*0.40f),V(s.x*0.76f,s.y*0.18f,s.z*0.16f),Lt(tankCol,1.12f),0.44f,0.62f);
                glacis.transform.localRotation = Quaternion.Euler(-12f,0f,0f);
                P(PrimitiveType.Cube,    root,"RearDeck",    V(0,s.y*0.54f,-s.z*0.36f),   V(s.x*0.70f,s.y*0.13f,s.z*0.30f), Dk(tankCol,0.60f),0.38f,0.50f);
                P(PrimitiveType.Cube,    root,"TrackL",      V(-s.x*0.57f,s.y*0.14f,0),   V(s.x*0.18f,s.y*0.28f,s.z*1.08f), track,0.10f,0.14f);
                P(PrimitiveType.Cube,    root,"TrackR",      V( s.x*0.57f,s.y*0.14f,0),   V(s.x*0.18f,s.y*0.28f,s.z*1.08f), track,0.10f,0.14f);
                P(PrimitiveType.Cube,    root,"SideSkirtL",  V(-s.x*0.50f,s.y*0.33f,0),   V(s.x*0.09f,s.y*0.15f,s.z*0.88f), Dk(tankCol,0.50f),0.30f,0.45f);
                P(PrimitiveType.Cube,    root,"SideSkirtR",  V( s.x*0.50f,s.y*0.33f,0),   V(s.x*0.09f,s.y*0.15f,s.z*0.88f), Dk(tankCol,0.50f),0.30f,0.45f);
                TrackWheels(root, s, METAL);
                for (int i = 0; i < 7; i++)
                {
                    float z = Mathf.Lerp(-s.z * 0.50f, s.z * 0.50f, i / 6f);
                    P(PrimitiveType.Cube,root,$"TrackShoeL{i}",V(-s.x*0.59f,s.y*0.30f,z),V(s.x*0.12f,s.y*0.045f,s.z*0.08f),rubber,0.08f,0.12f);
                    P(PrimitiveType.Cube,root,$"TrackShoeR{i}",V( s.x*0.59f,s.y*0.30f,z),V(s.x*0.12f,s.y*0.045f,s.z*0.08f),rubber,0.08f,0.12f);
                }
                P(PrimitiveType.Cube,    root,"Turret",      V(0,s.y*0.76f,0.00f),        V(s.x*0.58f,s.y*0.30f,s.z*0.46f), tankDk,0.46f,0.62f);
                var mantlet=P(PrimitiveType.Cylinder,root,"GunMantlet",V(0,s.y*0.76f,s.z*0.30f),V(s.x*0.21f,s.z*0.08f,s.x*0.21f),WW2_METAL,0.68f,0.80f);
                mantlet.transform.localRotation = Quaternion.Euler(90f,0f,0f);
                var tb=P(PrimitiveType.Cylinder,root,"Barrel",V(0,s.y*0.78f,s.z*0.72f),V(0.10f,s.z*0.70f,0.10f),WW2_METAL,0.72f,0.82f);
                tb.transform.localRotation = Quaternion.Euler(90f,0f,0f);
                var muzzle=P(PrimitiveType.Cylinder,root,"Muzzle",V(0,s.y*0.78f,s.z*1.10f),V(0.16f,s.z*0.12f,0.16f),WW2_METAL,0.74f,0.84f);
                muzzle.transform.localRotation = Quaternion.Euler(90f,0f,0f);
                P(PrimitiveType.Cylinder,root,"CommanderCupola",V(-s.x*0.11f,s.y*0.98f,s.z*0.02f),V(s.x*0.16f,s.y*0.09f,s.x*0.16f),WW2_METAL,0.65f,0.78f);
                P(PrimitiveType.Cube,    root,"TurretMark",  V(0,s.y*0.82f,s.z*0.245f),   V(s.x*0.22f,s.y*0.06f,s.z*0.035f), marking,0.16f,0.50f);
                P(PrimitiveType.Cylinder,root,"Antenna",     V(s.x*0.28f,s.y*1.15f,-s.z*0.18f),V(0.025f,s.y*0.42f,0.025f),WW2_METAL,0.65f,0.80f);
                P(PrimitiveType.Cube,    root,"Exhaust",     V(s.x*0.30f,s.y*0.52f,-s.z*0.53f),V(s.x*0.12f,s.y*0.10f,s.z*0.10f),WW2_METAL,0.55f,0.66f);
                break;
            case UnitType.Fighter:
                var ff=P(PrimitiveType.Capsule,root,"Fuselage",V(0,0,0),s,col,0.35f,0.70f);
                ff.transform.localRotation = Quaternion.Euler(90f,0,0);
                P(PrimitiveType.Cube,root,"Wings",   V(0,0,0),              V(s.x*2.6f,s.y*0.22f,s.z*0.68f),dk, 0.35f,0.65f);
                P(PrimitiveType.Cube,root,"WingTipL",V(-s.x*1.40f,0f,0),    V(s.x*0.18f,s.y*0.20f,s.z*0.52f),lt,0.30f,0.66f);
                P(PrimitiveType.Cube,root,"WingTipR",V( s.x*1.40f,0f,0),    V(s.x*0.18f,s.y*0.20f,s.z*0.52f),lt,0.30f,0.66f);
                P(PrimitiveType.Sphere,root,"Cockpit",V(0,s.y*0.26f,s.z*0.28f),V3(s.x*0.38f), new Color(0.28f,0.72f,0.92f),0f,0.92f);
                P(PrimitiveType.Cube,root,"TailFin", V(0,s.y*0.28f,-s.z*0.48f),V(0.11f,s.y*0.58f,s.z*0.28f),dk,0.35f,0.65f);
                P(PrimitiveType.Cube,root,"MissileL",V(-s.x*0.58f,-s.y*0.26f,s.z*0.18f),V(s.x*0.10f,s.y*0.12f,s.z*0.44f),METAL,0.62f,0.78f);
                P(PrimitiveType.Cube,root,"MissileR",V( s.x*0.58f,-s.y*0.26f,s.z*0.18f),V(s.x*0.10f,s.y*0.12f,s.z*0.44f),METAL,0.62f,0.78f);
                break;
            case UnitType.Bomber:
                var bf=P(PrimitiveType.Capsule,root,"Fuselage",V(0,0,0),s,col,0.35f,0.62f);
                bf.transform.localRotation = Quaternion.Euler(90f,0,0);
                P(PrimitiveType.Cube,root,"Wings",V(0,-s.y*0.10f,0),V(s.x*3.2f,s.y*0.26f,s.z*0.88f),dk,0.35f,0.58f);
                var eL=P(PrimitiveType.Cylinder,root,"EngL",V(-s.x*0.88f,-s.y*0.14f,s.z*0.12f),V(s.x*0.26f,s.z*0.30f,s.x*0.26f),METAL,0.65f,0.75f);
                eL.transform.localRotation = Quaternion.Euler(90f,0,0);
                var eR=P(PrimitiveType.Cylinder,root,"EngR",V( s.x*0.88f,-s.y*0.14f,s.z*0.12f),V(s.x*0.26f,s.z*0.30f,s.x*0.26f),METAL,0.65f,0.75f);
                eR.transform.localRotation = Quaternion.Euler(90f,0,0);
                P(PrimitiveType.Cube,root,"BombBay",V(0,-s.y*0.30f,0),V(s.x*0.34f,s.y*0.18f,s.z*0.42f),new Color(0.10f,0.10f,0.12f),0.40f,0.50f);
                P(PrimitiveType.Cube,root,"TailWing",V(0,s.y*0.02f,-s.z*0.62f),V(s.x*1.10f,s.y*0.18f,s.z*0.28f),dk,0.32f,0.56f);
                P(PrimitiveType.Sphere,root,"NoseGlass",V(0,s.y*0.04f,s.z*0.70f),V3(s.x*0.22f),new Color(0.30f,0.68f,0.90f),0f,0.92f);
                break;
            case UnitType.ScoutPlane:
                var sf=P(PrimitiveType.Capsule,root,"Fuselage",V(0,0,0),s,col,0.30f,0.80f);
                sf.transform.localRotation = Quaternion.Euler(90f,0,0);
                P(PrimitiveType.Cube,root,"Wings",V(0,0,0),V(s.x*3.8f,s.y*0.20f,s.z*0.62f),Lt(col),0.28f,0.75f);
                P(PrimitiveType.Sphere,root,"Cockpit",V(0,s.y*0.22f,s.z*0.18f),V3(s.x*0.48f),new Color(0.28f,0.72f,0.95f),0f,0.95f);
                var prop=P(PrimitiveType.Cube,root,"Propeller",V(0,0f,s.z*0.72f),V(s.x*1.00f,s.y*0.10f,s.z*0.06f),METAL,0.55f,0.82f);
                prop.transform.localRotation = Quaternion.Euler(0f,0f,0f);
                P(PrimitiveType.Cylinder,root,"RadarPod",V(0,-s.y*0.28f,-s.z*0.16f),V(s.x*0.18f,s.z*0.20f,s.x*0.18f),METAL,0.60f,0.82f).transform.localRotation = Quaternion.Euler(90f,0f,0f);
                break;
        }
        if (!TryApplyExternalUnitVisual(root, col, t, player))
        {
            RemoveGeneratedChildren(root);
            Debug.LogWarning("External unit model missing; procedural visual removed for " + name);
        }
        return prefabRoot;
    }

    // ── 建筑几何体构建 ────────────────────────────────────────────────────────
    static bool TryApplyExternalUnitVisual(GameObject modelRoot, Color factionColor, UnitType type, bool player)
    {
        // 二战配色：地面单位强制橄榄绿/沙黄；飞机改用更清晰的蓝灰/红褐以便俯视识别。
        Color tint = IsAirUnit(type) ? AircraftTint(factionColor, player) : WW2Tint(factionColor, player);
        bool ok = false;
        switch (type)
        {
            case UnitType.Infantry:
                var infantryCharacter = AddImportedModel(modelRoot, KenneyBlockyFbxPath + "character-a.fbx", "KenneyCharacter", V(0f,-0.02f,0f), V(0f,180f,0f), V3(0.52f), tint, true);
                if (infantryCharacter != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    AddAttachmentSocket(infantryCharacter, "RightHand", V(0.28f,0.58f,0.34f), V(8f,90f,0f));
                    AddAttachmentSocket(infantryCharacter, "UpperChest", V(0f,1.02f,-0.12f), V(0f,180f,0f));
                    AddWW2InfantryKit(infantryCharacter, tint);
                    ConfigureInfantryAnimator(infantryCharacter, false);
                    AddWW2Rifle(modelRoot, "KenneyWeapon", V(0.28f,0.58f,0.34f), V(8f,90f,0f), V3(1f));
                    AddAttachmentHardpoint(modelRoot, "KenneyWeapon", "Muzzle", 0.04f);
                    AddFactionMarker(modelRoot, V(0f,1.02f,-0.12f), V(0f,180f,0f), V3(0.18f), factionColor);
                    ConfigureAnimatedUnitAttachments(modelRoot, infantryCharacter,
                        PreserveAnimatedAttachment("KenneyWeapon", "RightHand", "RightForeArm", "RightArm"),
                        PreserveAnimatedAttachment("FactionPlate", "UpperChest", "Chest", "Spine"));
                }
                break;
            case UnitType.Flamethrower:
                if (AddImportedModel(modelRoot, KenneySpaceFbxPath + "rover.fbx", "KenneyVehicleBase", V(0f,0.03f,0f), V(0f,180f,0f), V3(0.50f), tint, true) != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    AddWW2FlameVehicleKit(modelRoot, tint, factionColor, player);
                }
                break;
            case UnitType.Tank:
                if (AddImportedModel(modelRoot, KenneySpaceFbxPath + "rover.fbx", "KenneyVehicleBase", V(0f,0.03f,0f), V(0f,180f,0f), V3(0.58f), tint, true) != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    GameObject tankCannon = AddImportedModel(modelRoot, KenneyTDKitFbxPath + "weapon-cannon.fbx", "KenneyTankCannon", V(0f,0.58f,0.18f), V(0f,0f,0f), V3(0.44f), WW2_METAL, true);
                    if (tankCannon != null)
                        AddAttachmentHardpoint(modelRoot, "KenneyTankCannon", "Muzzle", 0.12f);
                    AddImportedModel(modelRoot, KenneySurvivalFbxPath + "metal-panel-screws-narrow.fbx", "KenneyTankArmorPlateL", V(-0.48f,0.44f,0.12f), V(0f,0f,92f), V3(0.20f), WW2_METAL, true);
                    AddImportedModel(modelRoot, KenneySurvivalFbxPath + "metal-panel-screws-narrow.fbx", "KenneyTankArmorPlateR", V(0.48f,0.44f,0.12f), V(0f,0f,-92f), V3(0.20f), WW2_METAL, true);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "scope-large-b.fbx", "KenneyTankOptics", V(-0.18f,0.82f,0.32f), V(0f,0f,0f), V3(0.14f), WW2_METAL, false);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "crate-small.fbx", "KenneyAmmoStowage", V(-0.36f,0.55f,-0.42f), V(0f,18f,0f), V3(0.22f), WW2_WOOD, true);
                    AddImportedModel(modelRoot, KenneySurvivalFbxPath + "barrel.fbx", "KenneyFuelDrum", V(0.36f,0.46f,-0.45f), V(88f,0f,0f), V3(0.24f), WW2_METAL, true);
                    AddFactionMarker(modelRoot, V(0f,0.97f,-0.18f), V(0f,180f,0f), V3(0.24f), factionColor);
                }
                break;
            case UnitType.Artillery:
                var artilleryCharacter = AddImportedModel(modelRoot, KenneyBlockyFbxPath + "character-a.fbx", "KenneyCharacter", V(0f,-0.02f,0f), V(0f,180f,0f), V3(0.52f), tint, true);
                if (artilleryCharacter != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    // 二战火炮（TDKit weapon-cannon 安在车斗上）
                    AddAttachmentSocket(artilleryCharacter, "RightHand", V(0.28f,0.58f,0.34f), V(8f,90f,0f));
                    AddAttachmentSocket(artilleryCharacter, "UpperChest", V(0f,1.02f,-0.12f), V(0f,180f,0f));
                    AddWW2InfantryKit(artilleryCharacter, tint);
                    ConfigureInfantryAnimator(artilleryCharacter, false);
                    AddWW2ShoulderCannon(modelRoot, "KenneyWeapon", V(0.25f,0.78f,0.34f), V(2f,90f,-7f), V3(1f));
                    AddAttachmentHardpoint(modelRoot, "KenneyWeapon", "Muzzle", 0.08f);
                    AddFactionMarker(modelRoot, V(0f,1.02f,-0.12f), V(0f,180f,0f), V3(0.18f), factionColor);
                    ConfigureAnimatedUnitAttachments(modelRoot, artilleryCharacter,
                        PreserveAnimatedAttachment("KenneyWeapon", "RightHand", "RightForeArm", "RightArm"),
                        PreserveAnimatedAttachment("FactionPlate", "UpperChest", "Chest", "Spine"));
                }
                break;
            case UnitType.Fighter:
                GameObject fighterModel = AddImportedModel(modelRoot, KenneySpaceFbxPath + "craft_speederA.fbx", "KenneyAircraft", V(0f,0.10f,0f), V(0f,0f,0f), V3(0.78f), tint, true);
                if (fighterModel != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    AddAttachmentHardpoint(modelRoot, fighterModel.name, "Muzzle", 0.10f);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "scope-small.fbx", "KenneyNosePod", V(0f,0.22f,0.54f), V(0f,0f,0f), V3(0.22f), METAL, false);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "bullet-foam-tip.fbx", "KenneyWingPodL", V(-0.58f,0.02f,0.08f), V(90f,0f,0f), V3(0.12f), WW2_METAL, true);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "bullet-foam-tip.fbx", "KenneyWingPodR", V(0.58f,0.02f,0.08f), V(90f,0f,0f), V3(0.12f), WW2_METAL, true);
                    AddAircraftMarkings(modelRoot, factionColor, UnitType.Fighter);
                    AddFactionMarker(modelRoot, V(0f,0.46f,-0.10f), V(0f,180f,0f), V3(0.20f), factionColor);
                }
                break;
            case UnitType.Bomber:
                GameObject bomberModel = AddImportedModel(modelRoot, KenneySpaceFbxPath + "craft_cargoB.fbx", "KenneyAircraft", V(0f,0.08f,0f), V(0f,0f,0f), V3(0.72f), tint, true);
                if (bomberModel != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    AddAttachmentHardpoint(modelRoot, bomberModel.name, "Muzzle", 0.08f);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "grenade-b.fbx", "KenneyBombA", V(-0.38f,-0.10f,0.20f), V(90f,0f,0f), V3(0.20f), METAL, false);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "grenade-b.fbx", "KenneyBombB", V(0.38f,-0.10f,0.20f), V(90f,0f,0f), V3(0.20f), METAL, false);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "scope-large-a.fbx", "KenneyDorsalTurret", V(0f,0.34f,0.08f), V(0f,0f,0f), V3(0.15f), WW2_METAL, false);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "crate-small.fbx", "KenneyBombBayDetail", V(0f,-0.16f,-0.18f), V(0f,0f,0f), V3(0.14f), WW2_METAL, true);
                    AddAircraftMarkings(modelRoot, factionColor, UnitType.Bomber);
                    AddFactionMarker(modelRoot, V(0f,0.52f,-0.10f), V(0f,180f,0f), V3(0.22f), factionColor);
                }
                break;
            case UnitType.ScoutPlane:
                GameObject scoutModel = AddImportedModel(modelRoot, KenneySpaceFbxPath + "craft_speederD.fbx", "KenneyAircraft", V(0f,0.08f,0f), V(0f,0f,0f), V3(0.62f), tint, true);
                if (scoutModel != null)
                {
                    ok = true;
                    RemoveGeneratedChildren(modelRoot);
                    AddAttachmentHardpoint(modelRoot, scoutModel.name, "Muzzle", 0.08f);
                    AddImportedModel(modelRoot, KenneyBlasterFbxPath + "scope-large-a.fbx", "KenneySensor", V(0f,0.34f,-0.20f), V(0f,0f,0f), V3(0.18f), METAL, false);
                    AddAircraftMarkings(modelRoot, factionColor, UnitType.ScoutPlane);
                    AddFactionMarker(modelRoot, V(0f,0.44f,-0.10f), V(0f,180f,0f), V3(0.18f), factionColor);
                }
                break;
        }
        return ok;
    }

    static bool IsAirUnit(UnitType type)
    {
        return type == UnitType.Fighter || type == UnitType.Bomber || type == UnitType.ScoutPlane;
    }

    static Color AircraftTint(Color factionColor, bool player)
    {
        return player
            ? new Color(0.24f, 0.50f, 0.68f)
            : new Color(0.72f, 0.24f, 0.18f);
    }

    static void AddAircraftMarkings(GameObject modelRoot, Color factionColor, UnitType type)
    {
        float wingX = type == UnitType.Bomber ? 0.70f : 0.56f;
        float topY = type == UnitType.Bomber ? 0.44f : 0.40f;
        float wingWidth = type == UnitType.Bomber ? 0.38f : 0.30f;
        Color noseColor = Color.Lerp(Color.white, factionColor, 0.28f);

        P(PrimitiveType.Cube, modelRoot, "AircraftForwardStripe", V(0f, topY + 0.04f, 0.52f), V(0.18f, 0.035f, 0.30f), noseColor, 0.03f, 0.42f);
        P(PrimitiveType.Cube, modelRoot, "FactionStripeL", V(-wingX, topY, 0.04f), V(wingWidth, 0.035f, 0.13f), factionColor, 0.03f, 0.42f);
        P(PrimitiveType.Cube, modelRoot, "FactionStripeR", V( wingX, topY, 0.04f), V(wingWidth, 0.035f, 0.13f), factionColor, 0.03f, 0.42f);
    }

    static GameObject PartGroup(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent.transform, false);
        group.transform.localPosition = localPosition;
        group.transform.localRotation = Quaternion.Euler(localRotation);
        group.transform.localScale = localScale;
        return group;
    }

    static GameObject AddWW2Rifle(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
    {
        GameObject rifle = PartGroup(parent, name, localPosition, localRotation, localScale);
        P(PrimitiveType.Cube, rifle, "WW2RifleStock", V(0f,0f,-0.18f), V(0.075f,0.085f,0.30f), WW2_WOOD, 0.08f,0.22f);
        var barrel = P(PrimitiveType.Cylinder, rifle, "WW2RifleBarrel", V(0f,0f,0.16f), V(0.026f,0.36f,0.026f), WW2_METAL, 0.55f,0.66f);
        barrel.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cube, rifle, "WW2RifleMagazine", V(0f,-0.06f,-0.02f), V(0.055f,0.10f,0.07f), WW2_METAL, 0.35f,0.48f);
        P(PrimitiveType.Cube, rifle, "WW2RifleBayonet", V(0f,0.01f,0.48f), V(0.018f,0.018f,0.10f), WW2_METAL, 0.65f,0.78f);

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(rifle.transform, false);
        muzzle.transform.localPosition = V(0f,0f,0.55f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one;
        return rifle;
    }

    static GameObject AddWW2ShoulderCannon(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
    {
        GameObject cannon = PartGroup(parent, name, localPosition, localRotation, localScale);
        var tube = P(PrimitiveType.Cylinder, cannon, "WW2ShoulderTube", V(0f,0f,0.22f), V(0.070f,0.50f,0.070f), WW2_METAL, 0.58f,0.70f);
        tube.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cylinder, cannon, "WW2ShoulderMuzzle", V(0f,0f,0.72f), V(0.105f,0.075f,0.105f), WW2_METAL, 0.66f,0.80f).transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cylinder, cannon, "WW2ShoulderBreech", V(0f,0f,-0.30f), V(0.090f,0.085f,0.090f), Dk(WW2_METAL,0.72f), 0.46f,0.60f).transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cube, cannon, "WW2ShoulderRest", V(0f,-0.07f,-0.20f), V(0.16f,0.055f,0.20f), WW2_WOOD, 0.08f,0.22f);
        P(PrimitiveType.Cube, cannon, "WW2ShoulderGrip", V(0f,-0.14f,0.10f), V(0.055f,0.18f,0.07f), WW2_WOOD, 0.08f,0.22f);
        P(PrimitiveType.Cube, cannon, "WW2ShoulderSight", V(0.045f,0.065f,0.26f), V(0.035f,0.055f,0.16f), Dk(WW2_METAL,0.58f), 0.45f,0.62f);

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(cannon.transform, false);
        muzzle.transform.localPosition = V(0f,0f,0.82f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one;
        return cannon;
    }

    static void AddWW2InfantryKit(GameObject character, Color tint)
    {
        GameObject helmet = PartGroup(character, "WW2Helmet", V(0f,1.17f,0f), V(0f,0f,0f), V3(1f));
        P(PrimitiveType.Sphere, helmet, "WW2HelmetDome", V(0f,0.025f,0f), V(0.24f,0.105f,0.24f), Dk(tint,0.60f), 0.28f,0.36f);
        P(PrimitiveType.Cylinder, helmet, "WW2HelmetBrim", V(0f,-0.025f,0.02f), V(0.30f,0.025f,0.30f), Dk(tint,0.55f), 0.30f,0.38f);
        P(PrimitiveType.Cube, helmet, "WW2HelmetFrontLip", V(0f,-0.02f,0.20f), V(0.28f,0.025f,0.055f), Dk(tint,0.50f), 0.28f,0.35f);

        GameObject fieldPack = PartGroup(character, "WW2FieldPack", V(0f,0.54f,-0.28f), V(0f,0f,0f), V3(1f));
        P(PrimitiveType.Cube, fieldPack, "WW2PackBody", V(0f,0f,0f), V(0.30f,0.32f,0.12f), WW2_WOOD, 0.06f,0.22f);
        var bedroll = P(PrimitiveType.Cylinder, fieldPack, "WW2Bedroll", V(0f,0.21f,-0.02f), V(0.15f,0.34f,0.15f), Dk(tint,0.82f), 0.08f,0.24f);
        bedroll.transform.localRotation = Quaternion.Euler(0f,0f,90f);
        P(PrimitiveType.Cube, fieldPack, "WW2PackStrapL", V(-0.12f,0f,0.07f), V(0.035f,0.38f,0.035f), Dk(WW2_WOOD,0.55f), 0.04f,0.16f);
        P(PrimitiveType.Cube, fieldPack, "WW2PackStrapR", V(0.12f,0f,0.07f), V(0.035f,0.38f,0.035f), Dk(WW2_WOOD,0.55f), 0.04f,0.16f);
    }

    static GameObject AddWW2FuelTank(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation, Vector3 tankScale)
    {
        GameObject tank = PartGroup(parent, name, localPosition, localRotation, V3(1f));
        var body = P(PrimitiveType.Cylinder, tank, "WW2FuelCylinder", V(0f,0f,0f), tankScale, WW2_METAL, 0.56f,0.68f);
        body.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cube, tank, "WW2FuelStrapA", V(0f,0f,-tankScale.y*0.45f), V(tankScale.x*1.75f,tankScale.x*0.18f,tankScale.x*0.18f), Dk(WW2_METAL,0.45f), 0.28f,0.36f);
        P(PrimitiveType.Cube, tank, "WW2FuelStrapB", V(0f,0f, tankScale.y*0.45f), V(tankScale.x*1.75f,tankScale.x*0.18f,tankScale.x*0.18f), Dk(WW2_METAL,0.45f), 0.28f,0.36f);
        return tank;
    }

    static GameObject AddWW2FlameNozzle(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
    {
        GameObject nozzle = PartGroup(parent, name, localPosition, localRotation, localScale);
        P(PrimitiveType.Cube, nozzle, "WW2NozzleMount", V(0f,0f,-0.12f), V(0.20f,0.13f,0.18f), WW2_METAL, 0.48f,0.58f);
        var pipe = P(PrimitiveType.Cylinder, nozzle, "WW2FlamePipe", V(0f,0f,0.18f), V(0.055f,0.36f,0.055f), WW2_METAL, 0.65f,0.76f);
        pipe.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        var cone = P(PrimitiveType.Cylinder, nozzle, "WW2FlameBell", V(0f,0f,0.50f), V(0.09f,0.10f,0.09f), WW2_METAL, 0.70f,0.82f);
        cone.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Sphere, nozzle, "WW2PilotLight", V(0f,0.065f,0.60f), V3(0.045f), new Color(1f,0.42f,0.08f), 0.02f,0.85f);

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(nozzle.transform, false);
        muzzle.transform.localPosition = V(0f,0f,0.66f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one;
        return nozzle;
    }

    static void AddWW2FlameVehicleKit(GameObject modelRoot, Color tint, Color factionColor, bool player)
    {
        AddWW2FlameNozzle(modelRoot, "KenneyWeapon", V(0f,0.64f,0.66f), V(0f,0f,0f), V3(1f));
        AddAttachmentHardpoint(modelRoot, "KenneyWeapon", "Muzzle", 0.08f);
        AddWW2FuelTank(modelRoot, "KenneyFuelTank", V(-0.34f,0.48f,-0.36f), V(0f,0f,0f), V(0.13f,0.46f,0.13f));
        AddWW2FuelTank(modelRoot, "KenneyFuelTankB", V(0.34f,0.48f,-0.36f), V(0f,0f,0f), V(0.13f,0.46f,0.13f));
        P(PrimitiveType.Cube, modelRoot, "WW2FlameArmor", V(0f,0.56f,0.12f), V(0.72f,0.18f,0.50f), tint, 0.35f,0.50f);
        P(PrimitiveType.Cube, modelRoot, "WW2FlameSkirtL", V(-0.47f,0.28f,0f), V(0.08f,0.20f,0.82f), Dk(tint,0.58f), 0.26f,0.36f);
        P(PrimitiveType.Cube, modelRoot, "WW2FlameSkirtR", V(0.47f,0.28f,0f), V(0.08f,0.20f,0.82f), Dk(tint,0.58f), 0.26f,0.36f);
        P(PrimitiveType.Cube, modelRoot, "WW2FuelHoseL", V(-0.16f,0.55f,0.10f), V(0.055f,0.055f,0.62f), Dk(WW2_METAL,0.45f), 0.20f,0.28f);
        P(PrimitiveType.Cube, modelRoot, "WW2FuelHoseR", V(0.16f,0.55f,0.10f), V(0.055f,0.055f,0.62f), Dk(WW2_METAL,0.45f), 0.20f,0.28f);
        P(PrimitiveType.Cube, modelRoot, "WW2FlameMark", V(0f,0.72f,0.36f), V(0.24f,0.055f,0.04f), player ? P_BLUE : E_RED, 0.08f,0.35f);
        AddFactionMarker(modelRoot, V(0f,0.88f,-0.18f), V(0f,180f,0f), V3(0.22f), factionColor);
    }

    static GameObject AddFactionMarker(GameObject parent, Vector3 localPosition, Vector3 localRotation, Vector3 localScale, Color factionColor)
    {
        GameObject marker = AddImportedModel(parent,
            KenneyCastleFbxPath + "flag-pennant.fbx",
            "FactionPlate",
            localPosition,
            localRotation,
            localScale,
            factionColor,
            true);

        if (marker != null)
            return marker;

        return AddImportedModel(parent,
            KenneyPirateFbxPath + "flag-pennant.fbx",
            "FactionPlate",
            localPosition,
            localRotation,
            localScale,
            factionColor,
            true);
    }

    static GameObject AddBuildingFactionMarker(GameObject parent, Color factionColor, bool player)
    {
        string asset = player ? "flag-wide.fbx" : "flag-banner-short.fbx";
        GameObject marker = AddImportedModel(parent,
            KenneyCastleFbxPath + asset,
            "FactionPlate",
            V(0f,0.18f,1.02f),
            V(0f,180f,0f),
            V3(0.38f),
            factionColor,
            true);

        if (marker != null)
            return marker;

        return AddImportedModel(parent,
            KenneyPirateFbxPath + "flag.fbx",
            "FactionPlate",
            V(0f,0.18f,1.02f),
            V(0f,180f,0f),
            V3(0.40f),
            factionColor,
            true);
    }

    static GameObject AddAnimatedSurvivorUnit(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
    {
        GameObject instance = AddImportedModel(parent,
            KenneyAnimSurvivorPath + "characterMedium.fbx",
            name,
            localPosition,
            localRotation,
            localScale,
            Color.white,
            false);
        if (instance == null)
            return null;

        ConfigureInfantryAnimator(instance, true);
        return instance;
    }

    static void ConfigureInfantryAnimator(GameObject instance, bool copySourceAvatar)
    {
        if (instance == null)
            return;

        var animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = instance.AddComponent<Animator>();

        var controller = EnsureSurvivorAnimatorController();
        if (controller != null)
            animator.runtimeAnimatorController = controller;

        if (copySourceAvatar)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyAnimSurvivorPath + "characterMedium.fbx");
            var sourceAnimator = sourceAsset != null ? sourceAsset.GetComponentInChildren<Animator>(true) : null;
            if (animator.avatar == null && sourceAnimator != null)
                animator.avatar = sourceAnimator.avatar;
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    static void ConfigureAnimatedUnitAttachments(GameObject modelRoot, GameObject animatedModel, params AnimatedUnitAttachmentBinder.AttachmentBinding[] bindings)
    {
        if (modelRoot == null || bindings == null || bindings.Length == 0)
            return;

        var binder = modelRoot.GetComponent<AnimatedUnitAttachmentBinder>();
        if (binder == null)
            binder = modelRoot.AddComponent<AnimatedUnitAttachmentBinder>();

        binder.VisualRoot = modelRoot.transform;
        binder.AnimatedRoot = animatedModel != null ? animatedModel.transform : null;
        binder.Bindings = bindings;
        binder.BindAttachments();
    }

    static AnimatedUnitAttachmentBinder.AttachmentBinding PreserveAnimatedAttachment(string attachmentName, params string[] boneCandidates)
    {
        return new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = attachmentName,
            BoneCandidates = boneCandidates ?? new string[0],
            PreserveWorldPose = !string.Equals(attachmentName, "KenneyWeapon", System.StringComparison.OrdinalIgnoreCase),
            LocalScale = Vector3.one,
        };
    }

    static Transform AddAttachmentSocket(GameObject parent, string name, Vector3 localPosition, Vector3 localRotation)
    {
        if (parent == null || string.IsNullOrEmpty(name))
            return null;

        var socket = new GameObject(name);
        socket.transform.SetParent(parent.transform, false);
        socket.transform.localPosition = localPosition;
        socket.transform.localRotation = Quaternion.Euler(localRotation);
        socket.transform.localScale = Vector3.one;
        return socket.transform;
    }

    static void AddAttachmentHardpoint(GameObject modelRoot, string attachmentName, string hardpointName, float forwardPadding)
    {
        if (modelRoot == null || string.IsNullOrEmpty(attachmentName) || string.IsNullOrEmpty(hardpointName))
            return;

        Transform attachment = FindChildRecursive(modelRoot.transform, attachmentName);
        if (attachment == null)
            return;

        Bounds bounds;
        if (!TryGetLocalRendererBounds(attachment, out bounds))
            return;

        Transform hardpoint = FindChildRecursive(attachment, hardpointName);
        if (hardpoint == null)
        {
            var go = new GameObject(hardpointName);
            hardpoint = go.transform;
            hardpoint.SetParent(attachment, false);
        }

        hardpoint.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + forwardPadding);
        hardpoint.localRotation = Quaternion.identity;
        hardpoint.localScale = Vector3.one;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 localCorner = root.InverseTransformPoint(corners[c]);
                if (!hasBounds)
                {
                    bounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(localCorner);
                }
            }
        }

        return hasBounds;
    }

    static GameObject AddImportedModel(GameObject parent, string assetPath, string name, Vector3 localPosition, Vector3 localRotation, Vector3 localScale, Color tint, bool tintRenderers)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning("Imported model missing or not imported yet: " + assetPath);
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(asset);

        instance.name = name;
        instance.transform.SetParent(parent.transform, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localRotation);
        instance.transform.localScale = localScale;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Rigidbody rigidbody in instance.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rigidbody);

        ApplyImportedMaterials(instance, assetPath, tint, tintRenderers);

        return instance;
    }

    static void ApplyImportedMaterials(GameObject root, string assetPath, Color tint, bool factionTint)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = BuildImportedMat(assetPath, materials[i], tint, factionTint, i);
            renderer.sharedMaterials = materials;
        }
    }

    static void RemoveGeneratedChildren(GameObject modelRoot)
    {
        var generated = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in modelRoot.transform)
        {
            // 保留所有外部 FBX 模型节点（Kenney/WW2 前缀均使用 Kenney 资源），以及阵营牌。
            if (child.name.StartsWith("Kenney")
                || child.name.StartsWith("WW2")
                || child.name == "FactionPlate") continue;
            generated.Add(child.gameObject);
        }

        for (int i = 0; i < generated.Count; i++)
            Object.DestroyImmediate(generated[i]);
    }

    static void SetNamedChildrenVisible(GameObject modelRoot, bool visible, params string[] names)
    {
        foreach (Transform child in modelRoot.transform)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (child.name == names[i])
                {
                    child.gameObject.SetActive(visible);
                    break;
                }
            }
        }
    }

    static void BuildBattlefieldPropPrefabs()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Resources", "Prefabs", "Props"));

        MakeExternalProp("BattlefieldProp_SupplyCrate",
            new[]
            {
                new PropPart(KenneyBlasterFbxPath + "crate-wide.fbx", "CrateWide", V(0f,0f,0f), V(0f,0f,0f), V3(0.58f), new Color(0.46f, 0.36f, 0.22f), true),
                new PropPart(KenneyBlasterFbxPath + "crate-small.fbx", "CrateSmall", V(0.55f,0.55f,-0.18f), V(0f,18f,0f), V3(0.42f), new Color(0.40f, 0.30f, 0.18f), true),
            });

        MakeExternalProp("BattlefieldProp_AmmoCrate",
            new[]
            {
                new PropPart(KenneyBlasterFbxPath + "crate-medium.fbx", "AmmoCrateBody", V(0f,0f,0f), V(0f,0f,0f), V3(0.46f), new Color(0.28f, 0.34f, 0.24f), true),
                new PropPart(KenneyBlasterFbxPath + "clip-large.fbx", "AmmoClip", V(-0.45f,0.42f,0.18f), V(0f,35f,0f), V3(0.24f), METAL, true),
                new PropPart(KenneyBlasterFbxPath + "grenade-a.fbx", "Grenade", V(0.42f,0.42f,-0.16f), V(0f,-20f,0f), V3(0.22f), new Color(0.34f,0.38f,0.28f), true),
            });

        MakeExternalProp("BattlefieldProp_TargetMarker",
            new[]
            {
                new PropPart(KenneyBlasterFbxPath + "target-large.fbx", "TargetLarge", V(0f,0f,0f), V(0f,0f,0f), V3(0.8f), new Color(0.86f, 0.62f, 0.12f), true),
            });

        MakeExternalProp("BattlefieldProp_VehicleWreck",
            new[]
            {
                new PropPart(KenneyCarFbxPath + "debris-drivetrain.fbx", "Drivetrain", V(0f,0f,0f), V(0f,20f,0f), V3(0.62f), new Color(0.22f,0.22f,0.22f), true),
                new PropPart(KenneyCarFbxPath + "debris-bumper.fbx", "Bumper", V(0.9f,0f,0.26f), V(0f,-28f,0f), V3(0.48f), METAL, true),
                new PropPart(KenneyCarFbxPath + "wheel-truck.fbx", "WheelA", V(-0.65f,0.08f,-0.52f), V(0f,50f,82f), V3(0.42f), new Color(0.05f,0.05f,0.05f), true),
                new PropPart(KenneyCarFbxPath + "wheel-dark.fbx", "WheelB", V(0.52f,0.08f,-0.62f), V(0f,-20f,88f), V3(0.34f), new Color(0.05f,0.05f,0.05f), true),
            });

        // RetroUrbanKit 战场氛围 props
        MakeExternalProp("BattlefieldProp_BarrierStrong",
            new[]
            {
                new PropPart(KenneyRetroUrbanFbxPath + "detail-barrier-strong-type-a.fbx", "BarrierA", V(0f,0f,0f), V(0f,0f,0f), V3(1.20f), WW2_METAL, true),
                new PropPart(KenneyRetroUrbanFbxPath + "detail-barrier-strong-damaged.fbx", "BarrierDmg", V(2.50f,0f,0.20f), V(0f,180f,0f), V3(1.10f), WW2_METAL, true),
            });

        MakeExternalProp("BattlefieldProp_SandbagWall",
            new[]
            {
                new PropPart(KenneyRetroUrbanFbxPath + "detail-barrier-type-a.fbx", "SbgA", V(0f,0f,0f), V(0f,0f,0f), V3(1.10f), new Color(0.55f, 0.46f, 0.28f), true),
                new PropPart(KenneyRetroUrbanFbxPath + "detail-barrier-type-b.fbx", "SbgB", V(2.20f,0f,0f), V(0f,0f,0f), V3(1.10f), new Color(0.50f, 0.42f, 0.25f), true),
            });

        MakeExternalProp("BattlefieldProp_BrickRubble",
            new[]
            {
                new PropPart(KenneyRetroUrbanFbxPath + "detail-bricks-type-a.fbx", "RubbleA", V(0f,0f,0f), V(0f,15f,0f), V3(1.30f), new Color(0.45f, 0.30f, 0.22f), true),
                new PropPart(KenneyRetroUrbanFbxPath + "detail-bricks-type-b.fbx", "RubbleB", V(1.40f,0f,0.50f), V(0f,-25f,0f), V3(1.10f), new Color(0.42f, 0.28f, 0.20f), true),
                new PropPart(KenneyRetroUrbanFbxPath + "detail-block.fbx", "BlockA", V(-0.80f,0f,0.90f), V(0f,40f,0f), V3(0.75f), new Color(0.38f, 0.33f, 0.27f), true),
            });

        MakeExternalProp("BattlefieldProp_StreetLight",
            new[]
            {
                new PropPart(KenneyRetroUrbanFbxPath + "detail-light-double.fbx", "Light", V(0f,0f,0f), V(0f,90f,0f), V3(1.20f), WW2_METAL, true),
            });

        // CastleKit 攻城塔残骸（被毁）
        MakeExternalProp("BattlefieldProp_RuinedTower",
            new[]
            {
                new PropPart(KenneyCastleFbxPath + "siege-tower-demolished.fbx", "RuinedTower", V(0f,0f,0f), V(0f,15f,0f), V3(1.30f), new Color(0.42f, 0.32f, 0.24f), true),
                new PropPart(KenneyRetroUrbanFbxPath + "detail-bricks-type-a.fbx", "TowerRubble", V(1.40f,0f,0.40f), V(0f,40f,0f), V3(1.05f), new Color(0.45f, 0.30f, 0.22f), true),
            });

        // TrainKit 损坏铁路 + 木桶（战场遗弃感）
        MakeExternalProp("BattlefieldProp_DamagedRail",
            new[]
            {
                new PropPart(KenneyTrainFbxPath + "railroad-damaged-straight.fbx", "RailA", V(-2.0f,0f,0f), V(0f,0f,0f), V3(1.20f), WW2_METAL, true),
                new PropPart(KenneyTrainFbxPath + "railroad-damaged-straight-skew-left.fbx", "RailB", V(2.0f,0f,0f), V(0f,0f,0f), V3(1.20f), WW2_METAL, true),
                new PropPart(KenneySurvivalFbxPath + "barrel.fbx", "RailBarrel", V(0.40f,0.10f,1.20f), V(0f,90f,75f), V3(0.55f), WW2_METAL, true),
            });

        // Kenney Pirate Kit（CC0）：海图群岛地图专用装饰
        MakeExternalProp("BattlefieldProp_PirateShip",
            new[]
            {
                new PropPart(KenneyPirateFbxPath + "ship-pirate-medium.fbx", "PirateShip", V(0f,0f,0f), V(0f,180f,0f), V3(1.0f), new Color(0.72f, 0.56f, 0.34f), true),
            });

        MakeExternalProp("BattlefieldProp_Dock",
            new[]
            {
                new PropPart(KenneyPirateFbxPath + "structure-platform-dock.fbx", "Dock", V(0f,0f,0f), V(0f,0f,0f), V3(1.0f), new Color(0.55f, 0.38f, 0.20f), true),
                new PropPart(KenneyPirateFbxPath + "barrel.fbx", "DockBarrel", V(1.4f,0.15f,-0.8f), V(0f,20f,0f), V3(0.42f), WW2_WOOD, true),
                new PropPart(KenneyPirateFbxPath + "chest.fbx", "DockChest", V(-1.1f,0.15f,0.7f), V(0f,-25f,0f), V3(0.42f), new Color(0.70f, 0.48f, 0.22f), true),
            });

        MakeExternalProp("BattlefieldProp_Palm",
            new[]
            {
                new PropPart(KenneyPirateFbxPath + "palm-detailed-bend.fbx", "Palm", V(0f,0f,0f), V(0f,0f,0f), V3(1.0f), new Color(0.18f, 0.58f, 0.26f), true),
            });

        MakeExternalProp("BattlefieldProp_SandRocks",
            new[]
            {
                new PropPart(KenneyPirateFbxPath + "rocks-sand-a.fbx", "SandRockA", V(-0.6f,0f,0.1f), V(0f,20f,0f), V3(1.1f), new Color(0.66f, 0.56f, 0.38f), true),
                new PropPart(KenneyPirateFbxPath + "rocks-sand-b.fbx", "SandRockB", V(0.8f,0f,-0.4f), V(0f,-35f,0f), V3(0.9f), new Color(0.62f, 0.52f, 0.35f), true),
            });

        MakeExternalProp("BattlefieldProp_PirateFlag",
            new[]
            {
                new PropPart(KenneyPirateFbxPath + "flag-pirate-high.fbx", "PirateFlag", V(0f,0f,0f), V(0f,0f,0f), V3(1.0f), new Color(0.90f, 0.82f, 0.50f), true),
            });
    }

    static void BuildBattleMapTilePrefabs()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Resources", "Prefabs", "MapTiles"));

        // Android 包里不能直接读取 Assets/External 下的裸模型文件；这里提前把下载模型做成 Resources 预制体。
        // 清单尽量覆盖战场会用到的地表、石头、树木、草丛和海岸装饰，避免运行时退回占位模型。
        string[] models =
        {
            "nature:ground_grass", "nature:platform_grass", "nature:grass_large",
            "nature:grass", "nature:grass_leafs", "nature:grass_leafsLarge",
            "nature:plant_bush", "nature:plant_bushDetailed", "nature:plant_bushLarge", "nature:plant_bushSmall", "nature:plant_bushTriangle",
            "nature:flower_redA", "nature:flower_redB", "nature:flower_yellowA", "nature:flower_yellowB", "nature:flower_purpleA", "nature:flower_purpleB",
            "nature:lily_small", "nature:lily_large", "nature:log", "nature:log_large", "nature:log_stack", "nature:log_stackLarge",
            "nature:mushroom_red", "nature:mushroom_redGroup", "nature:mushroom_tan", "nature:mushroom_tanGroup",
            "nature:ground_pathStraight", "nature:ground_pathTile", "nature:ground_pathRocks", "nature:ground_pathSide",
            "nature:ground_pathBend", "nature:ground_pathCorner", "nature:ground_pathCross", "nature:ground_pathOpen", "nature:ground_pathSideOpen",
            "nature:ground_riverTile", "nature:ground_riverStraight", "nature:ground_riverRocks",
            "nature:ground_riverBend", "nature:ground_riverCorner", "nature:ground_riverCross", "nature:ground_riverOpen", "nature:ground_riverSide",
            "nature:platform_stone", "nature:platform_beach", "nature:bridge_stone", "nature:bridge_wood",
            "nature:rock_largeA", "nature:rock_largeB", "nature:rock_largeC",
            "nature:rock_largeD", "nature:rock_largeE", "nature:rock_largeF",
            "nature:rock_smallA", "nature:rock_smallB", "nature:rock_smallC", "nature:rock_smallD", "nature:rock_smallE", "nature:rock_smallF",
            "nature:rock_smallFlatA", "nature:rock_smallFlatB", "nature:rock_smallFlatC",
            "nature:rock_tallA", "nature:rock_tallB", "nature:rock_tallC", "nature:rock_tallD", "nature:rock_tallE", "nature:rock_tallF",
            "nature:tree_pineTallA", "nature:tree_pineTallB_detailed", "nature:tree_pineTallC", "nature:tree_pineTallD_detailed",
            "nature:tree_pineRoundA", "nature:tree_pineRoundB", "nature:tree_pineRoundC", "nature:tree_pineSmallA", "nature:tree_pineSmallB",
            "nature:tree_oak", "nature:tree_detailed", "nature:tree_default", "nature:tree_tall", "nature:tree_fat",
            "nature:tree_palm", "nature:tree_palmBend", "nature:tree_palmDetailedTall", "nature:tree_palmTall",
            "survival:floor", "survival:metal-panel", "survival:rock-sand-a", "survival:fence-fortified",
            "castle:gate", "castle:wall", "castle:flag-pennant", "castle:wall-doorway", "castle:wall-half", "castle:rocks-large", "castle:rocks-small",
            "pirate:patch-sand", "pirate:patch-sand-foliage", "pirate:patch-grass", "pirate:patch-grass-foliage", "pirate:platform-planks", "pirate:grass-patch",
            "pirate:ship-pirate-medium", "pirate:structure-platform-dock", "pirate:barrel", "pirate:chest",
            "pirate:palm-detailed-bend", "pirate:palm-detailed-straight", "pirate:palm-straight",
            "pirate:rocks-a", "pirate:rocks-b", "pirate:rocks-c", "pirate:rocks-sand-a", "pirate:rocks-sand-b", "pirate:rocks-sand-c",
            "pirate:ship-pirate-large", "pirate:ship-pirate-small", "pirate:ship-wreck", "pirate:boat-row-large", "pirate:boat-row-small",
            "pirate:structure-platform", "pirate:structure-platform-small", "pirate:structure-platform-dock-small",
            "pirate:flag-pirate-high", "pirate:flag-pennant", "pirate:flag-high-pennant"
        };

        for (int i = 0; i < models.Length; i++)
            MakeBattleMapTilePrefab(models[i]);
    }

    static void MakeBattleMapTilePrefab(string modelKey)
    {
        string assetPath = ResolveBattleMapTileFbxPath(modelKey);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("[PrefabBuilder] Missing downloaded map model: " + modelKey);
            return;
        }

        var root = new GameObject(BattleMapTileResourceName(modelKey));
        AddImportedModel(root, assetPath, "DownloadedModel", Vector3.zero, Vector3.zero, Vector3.one, Color.white, false);
        Save(root, $"{ResPrefabPath}/MapTiles/{BattleMapTileResourceName(modelKey)}.prefab");
    }

    static string ResolveBattleMapTileFbxPath(string modelKey)
    {
        int colon = modelKey.IndexOf(':');
        if (colon <= 0 || colon >= modelKey.Length - 1) return null;

        string kit = modelKey.Substring(0, colon);
        string model = modelKey.Substring(colon + 1);
        string basePath = null;
        if (kit == "nature") basePath = KenneyNatureFbxPath;
        else if (kit == "survival") basePath = KenneySurvivalFbxPath;
        else if (kit == "castle") basePath = KenneyCastleFbxPath;
        else if (kit == "pirate") basePath = KenneyPirateFbxPath;

        if (string.IsNullOrEmpty(basePath)) return null;
        string assetPath = basePath + model + ".fbx";
        return System.IO.File.Exists(assetPath) ? assetPath : null;
    }

    static string BattleMapTileResourceName(string modelKey)
    {
        return SanitizeAssetName(modelKey.Replace(':', '_'));
    }

    struct PropPart
    {
        public string AssetPath;
        public string Name;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        public Color Tint;
        public bool TintRenderers;

        public PropPart(string assetPath, string name, Vector3 position, Vector3 rotation, Vector3 scale, Color tint, bool tintRenderers)
        {
            AssetPath = assetPath;
            Name = name;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Tint = tint;
            TintRenderers = tintRenderers;
        }
    }

    static void MakeExternalProp(string name, PropPart[] parts)
    {
        var root = new GameObject(name);
        for (int i = 0; i < parts.Length; i++)
        {
            AddImportedModel(root, parts[i].AssetPath, parts[i].Name,
                parts[i].Position, parts[i].Rotation, parts[i].Scale,
                parts[i].Tint, parts[i].TintRenderers);
        }
        Save(root, $"{ResPrefabPath}/Props/{name}.prefab");
    }

    static void BuildProjectilePrefabs()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Resources", "Prefabs", "Projectiles"));

        MakeProjectile("BattleProjectile_Bullet",
            KenneyBlasterFbxPath + "bullet-foam-tip.fbx", "BulletModel",
            V(0f,0f,0f), V(90f,0f,0f), V3(0.22f),
            new Color(1f,0.84f,0.24f), 82f, 0f, 0.4f, true);

        MakeProjectile("BattleProjectile_Shell",
            KenneyBlasterFbxPath + "grenade-b.fbx", "ShellModel",
            V(0f,0f,0f), V(90f,0f,0f), V3(0.32f),
            new Color(0.92f,0.48f,0.12f), 45f, 2.4f, 1.1f, true);

        MakeProjectile("BattleProjectile_Bomb",
            KenneyBlasterFbxPath + "grenade-a.fbx", "BombModel",
            V(0f,0f,0f), V(90f,0f,0f), V3(1.05f),
            new Color(0.95f,0.35f,0.08f), 32f, 4f, 1.8f, true);

        MakeProjectile("BattleProjectile_Flame",
            KenneyBlasterFbxPath + "smoke.fbx", "FlameModel",
            V(0f,0f,0f), V(0f,0f,0f), V3(0.34f),
            new Color(1f,0.24f,0.04f), 30f, 0f, 1.0f, false);
    }

    static void MakeProjectile(string name, string assetPath, string modelName, Vector3 position,
        Vector3 rotation, Vector3 scale, Color tint, float speed, float arcHeight, float impactRadius, bool spin)
    {
        var root = new GameObject(name);
        GameObject model = AddImportedModel(root, assetPath, modelName, position, rotation, scale, tint, true);
        if (model == null)
            Debug.LogWarning("Projectile model missing; procedural projectile visual skipped for " + name);

        var projectile = root.AddComponent<CombatProjectile>();
        projectile.DefaultSpeed = speed;
        projectile.DefaultArcHeight = arcHeight;
        projectile.DefaultImpactRadius = impactRadius;
        projectile.DefaultTint = tint;
        projectile.Spin = spin;

        Save(root, $"{ResPrefabPath}/Projectiles/{name}.prefab");
    }

    [MenuItem("RTS/生成场景/生成空中模型Prefab")]
    public static void BuildAirModelPrefabs()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Resources", "Prefabs", "AirModels"));

        Save(MakeAirUnitModel("Aircraft_Model",   P_CYAN,  new Vector3(1.8f,0.4f,1.2f), UnitType.Fighter),    $"{ResPrefabPath}/AirModels/Aircraft_Model.prefab");
        Save(MakeAirUnitModel("Bomber_Model",     P_BLUE,  new Vector3(2.5f,0.5f,1.5f), UnitType.Bomber),     $"{ResPrefabPath}/AirModels/Bomber_Model.prefab");
        Save(MakeAirUnitModel("SmallPlane_Model", P_WHITE, new Vector3(1.5f,0.3f,1f),   UnitType.ScoutPlane), $"{ResPrefabPath}/AirModels/SmallPlane_Model.prefab");
        Save(MakeDroneModel("Drone_Model", P_CYAN),     $"{ResPrefabPath}/AirModels/Drone_Model.prefab");
        Save(MakeAirshipModel("Airship_Model", P_BLUE), $"{ResPrefabPath}/AirModels/Airship_Model.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static GameObject MakeAirUnitModel(string name, Color col, Vector3 scale, UnitType type)
    {
        var prefabRoot = UnitMesh(name, col, scale, type, true);
        var modelRoot = prefabRoot.transform.Find("Model");
        var anim = prefabRoot.AddComponent<UnitVisualAnimator>();
        anim.VisualRoot = modelRoot;
        anim.Style = UnitVisualAnimator.VisualStyle.Aircraft;
        anim.BobAmplitude = 0.035f;
        return prefabRoot;
    }

    static GameObject MakeAirVisualRoot(string name)
    {
        var prefabRoot = new GameObject(name);
        var modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(prefabRoot.transform, false);

        var anim = prefabRoot.AddComponent<UnitVisualAnimator>();
        anim.VisualRoot = modelRoot.transform;
        anim.Style = UnitVisualAnimator.VisualStyle.Aircraft;
        anim.BobAmplitude = 0.035f;
        return prefabRoot;
    }

    static GameObject MakeDroneModel(string name, Color col)
    {
        var prefabRoot = MakeAirVisualRoot(name);
        var root = prefabRoot.transform.Find("Model").gameObject;
        Color body = WW2Tint(col, true);
        Color accent = P_CYAN;
        Color dark = Dk(WW2_METAL, 0.62f);

        var fuselage = P(PrimitiveType.Capsule, root, "DroneBody", V(0f,0.16f,0f), V(0.52f,0.78f,0.52f), body, 0.35f,0.58f);
        fuselage.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Sphere, root, "SensorDome", V(0f,0.32f,0.34f), V3(0.18f), new Color(0.25f,0.78f,0.95f), 0f,0.88f);
        P(PrimitiveType.Cube, root, "CenterPlate", V(0f,0.13f,0f), V(0.62f,0.10f,0.48f), Dk(body,0.75f), 0.38f,0.52f);
        P(PrimitiveType.Cube, root, "ArmFront", V(0f,0.16f,0.46f), V(1.52f,0.07f,0.08f), dark, 0.50f,0.64f);
        P(PrimitiveType.Cube, root, "ArmRear", V(0f,0.16f,-0.46f), V(1.52f,0.07f,0.08f), dark, 0.50f,0.64f);

        AddDroneRotor(root, "RotorFL", V(-0.82f,0.22f, 0.50f), accent);
        AddDroneRotor(root, "RotorFR", V( 0.82f,0.22f, 0.50f), accent);
        AddDroneRotor(root, "RotorBL", V(-0.82f,0.22f,-0.50f), accent);
        AddDroneRotor(root, "RotorBR", V( 0.82f,0.22f,-0.50f), accent);

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = V(0f,0.12f,0.62f);
        return prefabRoot;
    }

    static void AddDroneRotor(GameObject root, string name, Vector3 pos, Color accent)
    {
        var group = PartGroup(root, name, pos, V(0f,0f,0f), V3(1f));
        P(PrimitiveType.Cylinder, group, "RotorHub", V(0f,0f,0f), V(0.16f,0.035f,0.16f), WW2_METAL, 0.55f,0.72f);
        P(PrimitiveType.Cube, group, "BladeA", V(0f,0.045f,0f), V(0.62f,0.018f,0.055f), accent, 0.15f,0.70f);
        P(PrimitiveType.Cube, group, "BladeB", V(0f,0.050f,0f), V(0.055f,0.018f,0.62f), accent, 0.15f,0.70f);
        P(PrimitiveType.Cylinder, group, "GuardRing", V(0f,-0.010f,0f), V(0.34f,0.018f,0.34f), Dk(WW2_METAL,0.72f), 0.45f,0.62f);
    }

    static GameObject MakeAirshipModel(string name, Color col)
    {
        var prefabRoot = MakeAirVisualRoot(name);
        var root = prefabRoot.transform.Find("Model").gameObject;
        Color body = WW2Tint(col, true);
        Color fabric = Lt(body, 1.22f);
        Color dark = Dk(body, 0.62f);

        var hull = P(PrimitiveType.Capsule, root, "Envelope", V(0f,0.82f,0f), V(1.14f,2.90f,1.14f), fabric, 0.12f,0.50f);
        hull.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cube, root, "Keel", V(0f,0.32f,0f), V(0.16f,0.10f,2.20f), dark, 0.38f,0.54f);
        P(PrimitiveType.Cube, root, "Gondola", V(0f,0.04f,0.22f), V(0.62f,0.28f,0.86f), WW2_METAL, 0.48f,0.62f);
        P(PrimitiveType.Cube, root, "GondolaGlass", V(0f,0.12f,0.69f), V(0.48f,0.12f,0.04f), new Color(0.25f,0.74f,0.96f), 0f,0.86f);
        P(PrimitiveType.Cube, root, "TailFinV", V(0f,0.88f,-1.62f), V(0.12f,0.66f,0.40f), dark, 0.28f,0.52f);
        P(PrimitiveType.Cube, root, "TailFinH", V(0f,0.80f,-1.62f), V(0.92f,0.11f,0.34f), dark, 0.28f,0.52f);
        P(PrimitiveType.Cube, root, "BandFront", V(0f,0.82f,0.72f), V(1.02f,0.06f,0.08f), Dk(fabric,0.70f), 0.20f,0.40f);
        P(PrimitiveType.Cube, root, "BandRear", V(0f,0.82f,-0.72f), V(1.02f,0.06f,0.08f), Dk(fabric,0.70f), 0.20f,0.40f);
        AddAirshipProp(root, "PropL", V(-0.48f,0.08f,-0.30f));
        AddAirshipProp(root, "PropR", V( 0.48f,0.08f,-0.30f));

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = V(0f,0.08f,0.86f);
        return prefabRoot;
    }

    static void AddAirshipProp(GameObject root, string name, Vector3 pos)
    {
        var group = PartGroup(root, name, pos, V(0f,0f,0f), V3(1f));
        var shaft = P(PrimitiveType.Cylinder, group, "Shaft", V(0f,0f,0f), V(0.045f,0.24f,0.045f), WW2_METAL, 0.55f,0.70f);
        shaft.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        P(PrimitiveType.Cube, group, "BladeA", V(0f,0f,-0.16f), V(0.48f,0.045f,0.035f), WW2_METAL, 0.50f,0.72f);
        P(PrimitiveType.Cube, group, "BladeB", V(0f,0f,-0.16f), V(0.045f,0.48f,0.035f), WW2_METAL, 0.50f,0.72f);
    }

    static GameObject BldMesh(string name, Color col, Vector3 s, BldType t, bool player)
    {
        var root = new GameObject(name);
        var visualRoot = new GameObject("Model");
        visualRoot.transform.SetParent(root.transform, false);
        Color dk   = Dk(col);
        Color lt   = Lt(col);
        Color flag = player ? new Color(0.22f,0.50f,0.88f) : new Color(0.80f,0.12f,0.12f);
        Color win  = new Color(0.52f,0.78f,0.92f,0.9f);
        switch (t)
        {
            case BldType.MainBase:
                P(PrimitiveType.Cube,visualRoot,"Platform",V(0,s.y*0.08f,0),    V(s.x*1.12f,s.y*0.14f,s.z*1.12f),Dk(col,0.42f),0.30f,0.50f);
                P(PrimitiveType.Cube,visualRoot,"Body",    V(0,s.y*0.5f,0),     s,                                 col,          0.35f,0.55f);
                P(PrimitiveType.Cube,visualRoot,"Tower",   V(0,s.y*1.30f,0),    V(s.x*0.40f,s.y*0.65f,s.z*0.40f), dk,           0.40f,0.62f);
                for(int i=-1;i<=1;i++) P(PrimitiveType.Cube,visualRoot,$"Win{i+1}",V(i*s.x*0.28f,s.y*0.52f,s.z*0.52f),V(s.x*0.14f,s.y*0.20f,0.08f),win,0f,0.92f);
                P(PrimitiveType.Cylinder,visualRoot,"Pole",V(s.x*0.36f,s.y*1.88f,s.z*0.36f),V(0.09f,s.y*0.45f,0.09f),METAL,0.75f,0.88f);
                P(PrimitiveType.Cube,    visualRoot,"Flag",V(s.x*0.36f+s.x*0.22f,s.y*2.55f,s.z*0.36f),V(s.x*0.44f,s.y*0.20f,0.12f),flag,0.10f,0.30f);
                break;
            case BldType.Barracks:
                P(PrimitiveType.Cube,    visualRoot,"Body",    V(0,s.y*0.5f,0),  s,                                  col, 0.30f,0.50f);
                P(PrimitiveType.Cube,    visualRoot,"Roof",    V(0,s.y*1.04f,0), V(s.x*1.06f,s.y*0.11f,s.z*1.06f),  dk,  0.35f,0.45f);
                P(PrimitiveType.Cube,    visualRoot,"WinL",    V(-s.x*0.28f,s.y*0.60f,s.z*0.52f),V(s.x*0.20f,s.y*0.26f,0.08f),win,0f,0.92f);
                P(PrimitiveType.Cube,    visualRoot,"WinR",    V( s.x*0.28f,s.y*0.60f,s.z*0.52f),V(s.x*0.20f,s.y*0.26f,0.08f),win,0f,0.92f);
                P(PrimitiveType.Cube,    visualRoot,"Door",    V(0,s.y*0.28f,s.z*0.52f),V(s.x*0.22f,s.y*0.44f,0.10f),dk,0.10f,0.20f);
                P(PrimitiveType.Cylinder,visualRoot,"Chimney", V(s.x*0.38f,s.y*1.32f,-s.z*0.28f),V(0.17f,s.y*0.30f,0.17f),dk,0.30f,0.40f);
                break;
            case BldType.AirFactory:
                P(PrimitiveType.Cube,    visualRoot,"Hangar",  V(0,s.y*0.5f,0),  s,                                  col, 0.35f,0.55f);
                P(PrimitiveType.Cylinder,visualRoot,"Dome",    V(0,s.y*1.04f,0), V(s.x*0.82f,s.y*0.17f,s.z*0.52f),  dk,  0.40f,0.60f);
                P(PrimitiveType.Cube,    visualRoot,"Tower",   V(-s.x*0.42f,s.y*1.04f,0),V(s.x*0.22f,s.y*0.88f,s.z*0.24f),lt,0.30f,0.50f);
                P(PrimitiveType.Cylinder,visualRoot,"Antenna", V(-s.x*0.42f,s.y*1.95f,0),V(0.06f,s.y*0.25f,0.06f),METAL,0.80f,0.90f);
                P(PrimitiveType.Cube,    visualRoot,"Stripe",  V(0,0.07f,0),     V(s.x*0.11f,0.06f,s.z*0.95f),       new Color(0.90f,0.90f,0.25f),0.10f,0.40f);
                break;
            case BldType.TankFactory:
                P(PrimitiveType.Cube,    visualRoot,"Body",    V(0,s.y*0.5f,0),  s,                                  col, 0.35f,0.50f);
                P(PrimitiveType.Cube,    visualRoot,"Roof",    V(0,s.y*1.02f,0), V(s.x*1.06f,s.y*0.10f,s.z*1.06f),  dk,  0.40f,0.50f);
                P(PrimitiveType.Cylinder,visualRoot,"Chimney1",V(-s.x*0.28f,s.y*1.45f,s.z*0.20f),V(0.19f,s.y*0.45f,0.19f),new Color(0.26f,0.26f,0.28f),0.50f,0.60f);
                P(PrimitiveType.Cylinder,visualRoot,"Chimney2",V( s.x*0.28f,s.y*1.30f,s.z*0.20f),V(0.16f,s.y*0.35f,0.16f),new Color(0.26f,0.26f,0.28f),0.50f,0.60f);
                P(PrimitiveType.Cube,    visualRoot,"Bay",     V(0,s.y*0.30f,-s.z*0.52f),V(s.x*0.55f,s.y*0.58f,0.10f),new Color(0.10f,0.10f,0.10f),0.20f,0.30f);
                break;
            case BldType.PowerPlant:
                P(PrimitiveType.Cube,    visualRoot,"Body",    V(0,s.y*0.5f,0),  s,                                  col, 0.40f,0.60f);
                P(PrimitiveType.Sphere,  visualRoot,"Dome",    V(0,s.y*1.10f,0), V(s.x*0.68f,s.y*0.34f,s.z*0.68f),  lt,  0.30f,0.65f);
                P(PrimitiveType.Cylinder,visualRoot,"CoolerL", V(-s.x*0.70f,s.y*0.58f,0),V(s.x*0.26f,s.y*0.65f,s.x*0.26f),new Color(0.58f,0.58f,0.60f),0.40f,0.55f);
                P(PrimitiveType.Cylinder,visualRoot,"CoolerR", V( s.x*0.70f,s.y*0.58f,0),V(s.x*0.26f,s.y*0.65f,s.x*0.26f),new Color(0.58f,0.58f,0.60f),0.40f,0.55f);
                P(PrimitiveType.Sphere,  visualRoot,"SteamL",  V(-s.x*0.70f,s.y*1.28f,0),V(s.x*0.28f,s.x*0.13f,s.x*0.28f),new Color(0.84f,0.84f,0.87f),0.10f,0.80f);
                P(PrimitiveType.Sphere,  visualRoot,"SteamR",  V( s.x*0.70f,s.y*1.28f,0),V(s.x*0.28f,s.x*0.13f,s.x*0.28f),new Color(0.84f,0.84f,0.87f),0.10f,0.80f);
                break;
            case BldType.GoldMine:
                P(PrimitiveType.Cube,    visualRoot,"Body",    V(0,s.y*0.5f,0),  s,                                  col, 0.35f,0.55f);
                P(PrimitiveType.Cylinder,visualRoot,"Drill",   V(0,s.y*1.22f,0), V(0.24f,s.y*0.28f,0.24f),          METAL,0.70f,0.85f);
                P(PrimitiveType.Sphere,  visualRoot,"OreA",    V(-s.x*0.30f,s.y*0.07f,s.z*0.30f),V(0.34f,0.24f,0.34f),new Color(0.96f,0.82f,0.07f),0.50f,0.88f);
                P(PrimitiveType.Sphere,  visualRoot,"OreB",    V( s.x*0.28f,s.y*0.05f,-s.z*0.28f),V(0.27f,0.19f,0.27f),new Color(0.90f,0.70f,0.04f),0.52f,0.86f);
                P(PrimitiveType.Sphere,  visualRoot,"OreC",    V(-s.x*0.08f,s.y*0.03f,-s.z*0.34f),V(0.21f,0.17f,0.21f),new Color(0.98f,0.88f,0.14f),0.55f,0.90f);
                break;
            case BldType.Turret:
                P(PrimitiveType.Cylinder,visualRoot,"Base",    V(0,s.y*0.12f,0), V(s.x*0.85f,s.y*0.22f,s.x*0.85f),  dk,  0.50f,0.65f);
                P(PrimitiveType.Cube,    visualRoot,"Body",    V(0,s.y*0.46f,0), V(s.x*0.62f,s.y*0.52f,s.z*0.62f),  col, 0.52f,0.72f);
                var bL=P(PrimitiveType.Cylinder,visualRoot,"BarrelL",V(-s.x*0.18f,s.y*0.72f,s.z*0.65f),V(0.13f,s.z*0.55f,0.13f),METAL,0.75f,0.88f);
                bL.transform.localRotation=Quaternion.Euler(90f,0,0);
                var bR=P(PrimitiveType.Cylinder,visualRoot,"BarrelR",V( s.x*0.18f,s.y*0.72f,s.z*0.65f),V(0.13f,s.z*0.55f,0.13f),METAL,0.75f,0.88f);
                bR.transform.localRotation=Quaternion.Euler(90f,0,0);
                P(PrimitiveType.Sphere,  visualRoot,"Radar",   V(0,s.y*1.05f,-s.z*0.18f),V(0.33f,0.19f,0.33f),      METAL,0.80f,0.92f);
                break;
        }
        if (!TryApplyExternalBuildingVisual(visualRoot, col, t, player))
        {
            RemoveGeneratedChildren(visualRoot);
            Debug.LogWarning("External building model missing; procedural visual removed for " + name);
        }
        return root;
    }

    /// <summary>把任意阵营色"做旧"成二战风：玩家=橄榄军绿，敌方=沙漠土黄。</summary>
    static Color WW2Tint(Color factionColor, bool player)
    {
        // 玩家：盟军橄榄绿；敌方：沙漠土黄。完全无视输入颜色（强制二战感）。
        return player
            ? new Color(0.32f, 0.40f, 0.20f)   // Olive Drab
            : new Color(0.55f, 0.46f, 0.25f);  // Desert Tan
    }

    /// <summary>金属做旧色（暗灰带轻微锈红）。</summary>
    static readonly Color WW2_METAL = new Color(0.36f, 0.34f, 0.30f);
    /// <summary>木头做旧色。</summary>
    static readonly Color WW2_WOOD  = new Color(0.42f, 0.30f, 0.18f);

    static bool TryApplyExternalBuildingVisual(GameObject root, Color factionColor, BldType type, bool player)
    {
        bool ok = false;
        Color tint = WW2Tint(factionColor, player);
        switch (type)
        {
            case BldType.MainBase:
                // 司令部塔楼（最大的 sample tower-d，约 4 层楼）
                ok |= AddImportedModel(root, KenneyModBldFbxPath + "building-sample-tower-d.fbx", "WW2HQTower", V(0f,0f,0f), V(0f,180f,0f), V3(0.85f), tint, true) != null;
                // 屋顶观察塔（TowerDefenseKit 圆塔顶段，做主基地高耸瞭望塔）
                ok |= AddImportedModel(root, KenneyTDKitFbxPath + "tower-square-top-c.fbx", "WW2HQObservation", V(-0.55f,2.05f,-0.20f), V(0f,180f,0f), V3(0.40f), WW2_METAL, true) != null;
                // 屋顶天线（用 BlasterKit 的 scope）
                ok |= AddImportedModel(root, KenneyBlasterFbxPath + "scope-large-a.fbx", "WW2CommandDish", V(0.5f,2.1f,0.3f), V(0f,30f,0f), V3(0.40f), WW2_METAL, true) != null;
                // 屋顶岗亭门口 sandbag（fence-fortified 当沙袋掩体）
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "fence-fortified.fbx", "WW2BaseSandbagFront", V(0f,0f,1.1f), V(0f,0f,0f), V3(0.95f), WW2_WOOD, true) != null;
                // 司令部入口两侧路障（RetroUrbanKit 加重型路障）
                ok |= AddImportedModel(root, KenneyRetroUrbanFbxPath + "detail-barrier-strong-type-a.fbx", "WW2HQBarrierL", V(-1.30f,0f,1.05f), V(0f,90f,0f), V3(0.85f), WW2_METAL, true) != null;
                ok |= AddImportedModel(root, KenneyRetroUrbanFbxPath + "detail-barrier-strong-type-b.fbx", "WW2HQBarrierR", V(1.30f,0f,1.05f), V(0f,90f,0f), V3(0.85f), WW2_METAL, true) != null;
                // 司令部顶端阵营旗帜（CastleKit flag-banner-long，长条旗）
                ok |= AddImportedModel(root, KenneyCastleFbxPath + "flag-banner-long.fbx", "WW2HQFlag", V(0.95f,2.55f,0.10f), V(0f,90f,0f), V3(0.55f),
                    player ? new Color(0.20f, 0.40f, 0.80f) : new Color(0.78f, 0.18f, 0.16f), true) != null;
                break;
            case BldType.Barracks:
                // 兵营平房（sample-house-a 经典坡屋顶）
                ok |= AddImportedModel(root, KenneyModBldFbxPath + "building-sample-house-a.fbx", "WW2BarracksHouse", V(0f,0f,0f), V(0f,180f,0f), V3(0.90f), tint, true) != null;
                // 旁边沙袋
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "fence-fortified.fbx", "WW2BarracksSandbag", V(1.0f,0f,0.4f), V(0f,30f,0f), V3(0.65f), WW2_WOOD, true) != null;
                // 油桶
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "barrel.fbx", "WW2BarracksBarrel", V(-0.85f,0f,0.55f), V(0f,0f,0f), V3(0.55f), WW2_METAL, true) != null;
                // 卫兵
                ok |= AddImportedModel(root, KenneyBlockyFbxPath + "character-b.fbx", "WW2Guard", V(0.85f,0.05f,0.85f), V(0f,180f,0f), V3(0.32f), tint, true) != null;
                break;
            case BldType.AirFactory:
                // 机库平房（sample-house-c 较大屋型）
                ok |= AddImportedModel(root, KenneyModBldFbxPath + "building-sample-house-c.fbx", "WW2HangarBldg", V(0f,0f,0f), V(0f,180f,0f), V3(0.95f), tint, true) != null;
                // 跑道侧的飞机（用 CarKit race）
                ok |= AddImportedModel(root, KenneyCarFbxPath + "race.fbx", "WW2ParkedPlane", V(1.10f,0.05f,-0.25f), V(0f,140f,0f), V3(0.38f), tint, true) != null;
                // 雷达
                ok |= AddImportedModel(root, KenneyBlasterFbxPath + "scope-large-b.fbx", "WW2AirRadar", V(-1.15f,1.10f,0.20f), V(0f,20f,0f), V3(0.35f), WW2_METAL, true) != null;
                // 油桶
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "barrel.fbx", "WW2HangarBarrel", V(0.65f,0f,1.00f), V(0f,0f,0f), V3(0.60f), WW2_METAL, true) != null;
                break;
            case BldType.TankFactory:
                // 工厂平房（sample-house-b 工业感）
                ok |= AddImportedModel(root, KenneyModBldFbxPath + "building-sample-house-b.fbx", "WW2FactoryBldg", V(0f,0f,0f), V(0f,180f,0f), V3(0.95f), tint, true) != null;
                // 旁边一辆未完工坦克底盘
                ok |= AddImportedModel(root, KenneySpaceFbxPath + "rover.fbx", "WW2FactoryTank", V(1.05f,0.05f,-0.30f), V(0f,140f,0f), V3(0.42f), tint, true) != null;
                // 物资箱堆
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "box-large.fbx", "WW2FactoryCrate", V(-0.95f,0f,0.85f), V(0f,15f,0f), V3(0.85f), WW2_WOOD, true) != null;
                // 油桶
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "barrel.fbx", "WW2FactoryBarrel", V(0.50f,0f,1.05f), V(0f,0f,0f), V3(0.55f), WW2_METAL, true) != null;
                break;
            case BldType.PowerPlant:
                // Industrial power plant: shed, stacks, tanks, pipes, and sandbags.
                ok |= AddImportedModel(root, DownloadCityIndustrialFbxPath + "building-c.fbx", "WW2PowerPlantShed", V(-0.10f,0f,-0.05f), V(0f,180f,0f), V3(0.72f), tint, true) != null;
                ok |= AddImportedModel(root, DownloadCityIndustrialFbxPath + "chimney-large.fbx", "WW2PowerStackA", V(-1.15f,0f,-0.45f), V(0f,0f,0f), V3(0.78f), WW2_METAL, true) != null;
                ok |= AddImportedModel(root, DownloadCityIndustrialFbxPath + "chimney-medium.fbx", "WW2PowerStackB", V(-0.72f,0f,-0.62f), V(0f,0f,0f), V3(0.70f), Dk(WW2_METAL,0.86f), true) != null;
                ok |= AddImportedModel(root, DownloadCityIndustrialFbxPath + "detail-tank.fbx", "WW2PowerFuelTankL", V(1.05f,0f,-0.55f), V(0f,25f,0f), V3(0.62f), WW2_METAL, true) != null;
                ok |= AddImportedModel(root, DownloadCityIndustrialFbxPath + "detail-tank.fbx", "WW2PowerFuelTankR", V(1.10f,0f,0.40f), V(0f,-18f,0f), V3(0.54f), Dk(WW2_METAL,0.90f), true) != null;
                ok |= AddImportedModel(root, DownloadFactoryKitFbxPath + "machine-fortified.fbx", "WW2PowerGeneratorHouse", V(0.10f,0f,0.78f), V(0f,180f,0f), V3(0.48f), tint, true) != null;
                ok |= AddImportedModel(root, DownloadFactoryKitFbxPath + "pipe-large-long.fbx", "WW2PowerPipeMain", V(0.70f,0.40f,0.02f), V(0f,90f,0f), V3(0.42f), WW2_METAL, true) != null;
                ok |= AddImportedModel(root, DownloadFactoryKitFbxPath + "pipe-large-bend.fbx", "WW2PowerPipeBend", V(0.25f,0.42f,0.48f), V(0f,0f,0f), V3(0.38f), WW2_METAL, true) != null;
                ok |= AddImportedModel(root, DownloadFactoryKitFbxPath + "catwalk-straight.fbx", "WW2PowerServiceWalk", V(-0.18f,0.80f,0.92f), V(0f,90f,0f), V3(0.42f), WW2_WOOD, true) != null;
                // Roof/service armor plate.
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "metal-panel-screws.fbx", "WW2PowerRoofPlate", V(0f,1.25f,0f), V(0f,0f,0f), V3(1.10f), WW2_METAL, true) != null;
                // Forward sandbag fence.
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "fence-fortified.fbx", "WW2PowerSandbagFence", V(0f,0f,1.30f), V(0f,0f,0f), V3(0.85f), WW2_WOOD, true) != null;
                break;
            case BldType.GoldMine:
                // 木箱主体（chest 是宝箱）
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "chest.fbx", "WW2MineChest", V(0f,0f,0f), V(0f,15f,0f), V3(1.10f), WW2_WOOD, true) != null;
                // 侧面木板堆
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "resource-planks.fbx", "WW2MinePlanks", V(-0.85f,0f,0.45f), V(0f,30f,0f), V3(0.85f), WW2_WOOD, true) != null;
                // 矿石（黄金桶）
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "barrel-open.fbx", "WW2MineOreBarrel", V(0.85f,0f,0.45f), V(0f,0f,0f), V3(0.65f), new Color(0.92f, 0.72f, 0.06f), true) != null;
                ok |= AddImportedModel(root, KenneyBlasterFbxPath + "crate-small.fbx", "WW2MineOreCrate", V(0f,0f,1.05f), V(0f,0f,0f), V3(0.45f), new Color(0.92f, 0.72f, 0.06f), true) != null;
                break;
            case BldType.Turret:
                // 全新设计：方塔基座（TowerDefenseKit 二层堆叠）+ 顶部加农炮 + 周围沙袋
                ok |= AddImportedModel(root, KenneyTDKitFbxPath + "tower-square-bottom-a.fbx", "WW2TurretBase", V(0f,0f,0f), V(0f,0f,0f), V3(0.95f), tint, true) != null;
                ok |= AddImportedModel(root, KenneyTDKitFbxPath + "tower-square-middle-a.fbx", "WW2TurretMid", V(0f,1.10f,0f), V(0f,0f,0f), V3(0.95f), tint, true) != null;
                ok |= AddImportedModel(root, KenneyTDKitFbxPath + "tower-square-top-a.fbx", "WW2TurretTop", V(0f,2.20f,0f), V(0f,0f,0f), V3(0.95f), WW2_METAL, true) != null;
                // 顶部加农炮（TowerDefenseKit 的 weapon-cannon）
                var turretCannon = AddImportedModel(root, KenneyTDKitFbxPath + "weapon-cannon.fbx", "WW2TurretCannon", V(0f,2.55f,0.10f), V(0f,0f,0f), V3(0.55f), WW2_METAL, true);
                ok |= turretCannon != null;
                if (turretCannon != null)
                    AddAttachmentHardpoint(root, "WW2TurretCannon", "Muzzle", 0.10f);
                // 基座周围沙袋
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "fence-fortified.fbx", "WW2TurretSandbagN", V(0f,0f,0.95f), V(0f,0f,0f), V3(0.75f), WW2_WOOD, true) != null;
                ok |= AddImportedModel(root, KenneySurvivalFbxPath + "fence-fortified.fbx", "WW2TurretSandbagS", V(0f,0f,-0.95f), V(0f,180f,0f), V3(0.75f), WW2_WOOD, true) != null;
                break;
        }

        if (!ok) return false;
        RemoveGeneratedChildren(root);
        // 阵营牌：盟军=蓝白星，敌方=红黑十字风格
        AddBuildingFactionMarker(root,
            player ? new Color(0.25f,0.55f,0.95f) : new Color(0.85f,0.20f,0.20f),
            player);
        return true;
    }

    // ── 组件挂载 ──────────────────────────────────────────────────────────────
    static void AddUnitComps<T>(GameObject root, Vector3 s, bool player, UnitType type) where T : RTSUnit
    {
        var ag = root.AddComponent<NavMeshAgent>();
        ag.radius = Mathf.Max(s.x,s.z)*0.5f; ag.height = s.y; ag.speed = 5f; ag.stoppingDistance = 0.5f;
        var cc = root.AddComponent<CapsuleCollider>();
        cc.height = s.y; cc.radius = Mathf.Max(s.x,s.z)*0.5f;
        var unit = root.AddComponent<T>();
        unit.bPlayerOwned = player;
        ApplyProjectileProfile(unit, type);
        var anim = root.AddComponent<UnitVisualAnimator>();
        anim.VisualRoot = root.transform.Find("Model");
        anim.Style = UnitVisualStyle(type);
        anim.BobAmplitude = (type == UnitType.Infantry || type == UnitType.Artillery) ? 0.06f : 0.035f;
        root.layer = LayerMask.NameToLayer("Default");
    }

    static void ApplyProjectileProfile(RTSUnit unit, UnitType type)
    {
        switch (type)
        {
            case UnitType.Flamethrower:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Flame";
                unit.ProjectileSpeed = 30f; unit.ProjectileArcHeight = 0f; unit.ProjectileImpactRadius = 1.05f;
                unit.ProjectileTint = new Color(1f, 0.34f, 0.08f, 1f);
                unit.TracerDuration = 0.08f;
                break;
            case UnitType.Tank:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
                unit.ProjectileSpeed = 46f; unit.ProjectileArcHeight = 1.8f; unit.ProjectileImpactRadius = 1.25f;
                unit.ProjectileTint = new Color(1f, 0.56f, 0.18f, 1f);
                unit.TracerDuration = 0.06f;
                break;
            case UnitType.Artillery:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
                unit.ProjectileSpeed = 34f; unit.ProjectileArcHeight = 5.5f; unit.ProjectileImpactRadius = 1.55f;
                unit.ProjectileTint = new Color(1f, 0.48f, 0.14f, 1f);
                unit.TracerDuration = 0.07f;
                break;
            case UnitType.Bomber:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bomb";
                unit.ProjectileSpeed = 30f; unit.ProjectileArcHeight = 4f; unit.ProjectileImpactRadius = 2.35f;
                unit.ProjectileTint = new Color(1f, 0.36f, 0.08f, 1f);
                unit.TracerDuration = 0.10f;
                break;
            case UnitType.Fighter:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
                unit.ProjectileSpeed = 105f; unit.ProjectileArcHeight = 0f; unit.ProjectileImpactRadius = 0.52f;
                unit.ProjectileTint = new Color(0.70f, 0.96f, 1f, 1f);
                unit.TracerDuration = 0.055f;
                break;
            case UnitType.ScoutPlane:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
                unit.ProjectileSpeed = 96f; unit.ProjectileArcHeight = 0f; unit.ProjectileImpactRadius = 0.42f;
                unit.ProjectileTint = new Color(0.9f, 0.96f, 1f, 1f);
                unit.TracerDuration = 0.05f;
                break;
            default:
                unit.ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
                unit.ProjectileSpeed = 82f; unit.ProjectileArcHeight = 0f; unit.ProjectileImpactRadius = 0.34f;
                unit.ProjectileTint = new Color(1f, 0.86f, 0.28f, 1f);
                unit.TracerDuration = 0.04f;
                break;
        }
    }

    static void AddBldComps<T>(GameObject root, Vector3 s, bool player) where T : RTSBuilding
    {
        var bc = root.AddComponent<BoxCollider>();
        bc.center = new Vector3(0,s.y*0.5f,0); bc.size = s;
        var building = root.AddComponent<T>();
        building.ApplyDefinitionDefaults();
        building.bPlayerOwned = player;
        root.layer = LayerMask.NameToLayer("Default");
    }

    // ── 几何体帮助方法 ────────────────────────────────────────────────────────
    static GameObject P(PrimitiveType type, GameObject parent, string partName,
                        Vector3 lp, Vector3 ls, Color c, float m=0.2f, float g=0.4f)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = partName; go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = lp; go.transform.localScale = ls;
        go.GetComponent<Renderer>().sharedMaterial = BuildMat(c, m, g);
        var col = go.GetComponent<Collider>(); if (col) Object.DestroyImmediate(col);
        return go;
    }

    static void WheelPair(GameObject root, Vector3 s, Color dk)
    {
        var wL = P(PrimitiveType.Cylinder,root,"WheelL",V(-s.x*0.55f,s.y*0.12f,0),V(s.y*0.44f,s.x*0.11f,s.y*0.44f),dk,0.15f,0.20f);
        wL.transform.localRotation = Quaternion.Euler(0,0,90f);
        var wR = P(PrimitiveType.Cylinder,root,"WheelR",V( s.x*0.55f,s.y*0.12f,0),V(s.y*0.44f,s.x*0.11f,s.y*0.44f),dk,0.15f,0.20f);
        wR.transform.localRotation = Quaternion.Euler(0,0,90f);
    }

    static void InfantryLimbs(GameObject root, Vector3 s, Color armor, Color boot)
    {
        var armL = P(PrimitiveType.Cylinder,root,"ArmL",V(-s.x*0.36f,s.y*0.55f,s.z*0.02f),V(s.x*0.09f,s.y*0.30f,s.x*0.09f),armor,0.20f,0.38f);
        armL.transform.localRotation = Quaternion.Euler(18f,0f,-18f);
        var armR = P(PrimitiveType.Cylinder,root,"ArmR",V( s.x*0.36f,s.y*0.55f,s.z*0.02f),V(s.x*0.09f,s.y*0.30f,s.x*0.09f),armor,0.20f,0.38f);
        armR.transform.localRotation = Quaternion.Euler(18f,0f,18f);
        var legL = P(PrimitiveType.Cylinder,root,"LegL",V(-s.x*0.16f,-s.y*0.22f,0),V(s.x*0.10f,s.y*0.34f,s.x*0.10f),armor,0.15f,0.30f);
        legL.transform.localRotation = Quaternion.Euler(0f,0f,-5f);
        var legR = P(PrimitiveType.Cylinder,root,"LegR",V( s.x*0.16f,-s.y*0.22f,0),V(s.x*0.10f,s.y*0.34f,s.x*0.10f),armor,0.15f,0.30f);
        legR.transform.localRotation = Quaternion.Euler(0f,0f,5f);
        P(PrimitiveType.Cube,root,"BootL",V(-s.x*0.16f,-s.y*0.58f,s.z*0.05f),V(s.x*0.20f,s.y*0.08f,s.z*0.26f),boot,0.12f,0.20f);
        P(PrimitiveType.Cube,root,"BootR",V( s.x*0.16f,-s.y*0.58f,s.z*0.05f),V(s.x*0.20f,s.y*0.08f,s.z*0.26f),boot,0.12f,0.20f);
        P(PrimitiveType.Cube,root,"ShoulderL",V(-s.x*0.30f,s.y*0.76f,0),V(s.x*0.18f,s.y*0.08f,s.z*0.22f),armor,0.22f,0.40f);
        P(PrimitiveType.Cube,root,"ShoulderR",V( s.x*0.30f,s.y*0.76f,0),V(s.x*0.18f,s.y*0.08f,s.z*0.22f),armor,0.22f,0.40f);
    }

    static void TrackWheels(GameObject root, Vector3 s, Color metal)
    {
        for (int i = 0; i < 5; i++)
        {
            float z = Mathf.Lerp(-s.z * 0.40f, s.z * 0.40f, i / 4f);
            var left = P(PrimitiveType.Cylinder,root,$"TrackWheelL{i}",V(-s.x*0.54f,s.y*0.12f,z),V(s.y*0.17f,s.x*0.055f,s.y*0.17f),metal,0.36f,0.46f);
            left.transform.localRotation = Quaternion.Euler(0f,0f,90f);
            var right = P(PrimitiveType.Cylinder,root,$"TrackWheelR{i}",V(s.x*0.54f,s.y*0.12f,z),V(s.y*0.17f,s.x*0.055f,s.y*0.17f),metal,0.36f,0.46f);
            right.transform.localRotation = Quaternion.Euler(0f,0f,90f);
        }
    }

    static UnitVisualAnimator.VisualStyle UnitVisualStyle(UnitType type)
    {
        switch (type)
        {
            case UnitType.Infantry:
            case UnitType.Artillery:
                return UnitVisualAnimator.VisualStyle.Infantry;
            case UnitType.Fighter:
            case UnitType.Bomber:
            case UnitType.ScoutPlane:
                return UnitVisualAnimator.VisualStyle.Aircraft;
            default:
                return UnitVisualAnimator.VisualStyle.Vehicle;
        }
    }

    static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
    static Vector3 V3(float v) => new Vector3(v,v,v);
    static Color   Dk(Color c, float f=0.52f) => new Color(c.r*f, c.g*f, c.b*f, c.a);
    static Color   Lt(Color c, float f=1.45f) => new Color(Mathf.Clamp01(c.r*f), Mathf.Clamp01(c.g*f), Mathf.Clamp01(c.b*f), c.a);
}
