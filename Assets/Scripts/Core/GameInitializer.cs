using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// 游戏场景初始化器：在运行时动态创建初始建筑，避免将 RTSBuilding 脚本引用存入场景文件
public class GameInitializer : MonoBehaviour
{
    IEnumerator Start()
    {
        string mapName = PlayerPrefs.GetString("current_map", "沙漠绿洲");
        BattleMapDefinition map = BattleMapCatalog.Get(mapName);
        Random.InitState(map.RandomSeed);

        RuntimeBattleMapBuilder.Rebuild(map);
        yield return null; // allow stale generated scene objects to be destroyed

        ApplyMapTheme(mapName);
        SpawnPlayerBase();
        SpawnEnemyBase();

        yield return null; // ensure generated building colliders are present
        BuildRuntimeNavMesh();

        // 启用战雾系统（视野外敌方单位/建筑不显示）
        FogOfWar.EnsureInstance();

        // 开局相机定位到玩家主基地（玩家进场就能看到自己的建筑）
        // 注意：联机 Guest 由 GameManager.OnNetworkGameStarted 重新设置到红方基地
        var rtsCam = FindObjectOfType<RTSCamera>();
        if (rtsCam != null)
        {
            // 玩家蓝方基地位于 (-120, 0, -120)。相机俯视 +z，需要 z 偏移 ~-30 才能让基地处于屏幕中心。
            float camY = 32f;
            rtsCam.MinY = Mathf.Min(rtsCam.MinY, 20f);
            rtsCam.transform.position = new Vector3(-120f, camY, -138f);
            rtsCam.transform.rotation = Quaternion.Euler(61f, 0f, 0f);

            var cam = rtsCam.GetComponent<Camera>();
            if (cam != null && !cam.orthographic)
                cam.fieldOfView = 36f;
        }
    }

    static void ApplyMapTheme(string mapName)
    {
        BattleMapDefinition map = BattleMapCatalog.Get(mapName);

        // 地面材质
        TintObject("Ground", map.GroundColor);
        TintByPrefix("Road_", map.RoadColor);
        TintByPrefix("RoadEdge_", map.RoadEdgeColor);
        TintByPrefix("Water_", map.WaterColor);
        TintByPrefix("TerrainPatch0_", map.PatchAColor);
        TintByPrefix("TerrainPatch1_", map.PatchBColor);
        TintByPrefix("TerrainPatch2_", map.PlayerBasePadColor);
        TintByPrefix("TerrainPatch3_", map.EnemyBasePadColor);
        TintByName("Rock", map.RockColor);
        TintByName("Leaves", map.FoliageColor);
        TintByName("Tree", map.TrunkColor);
        TintByName("RuinWall", map.RuinColor);
        TintByName("Debris", Color.Lerp(map.RuinColor, Color.black, 0.12f));
        TintByPrefix("Wall", map.WallColor);

        // 雾效 + 环境光：使用地图自己的雾色，避免远景和地块间隙变成纯黑。
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1.12f;
        RenderSettings.fogColor = Color.Lerp(map.FogColor, Color.white, 0.12f);
        RenderSettings.ambientSkyColor = Color.Lerp(map.AmbientSkyColor, Color.white, 0.10f);
        RenderSettings.ambientEquatorColor = Color.Lerp(map.AmbientEquatorColor, Color.white, 0.08f);
        RenderSettings.ambientGroundColor = Color.Lerp(map.AmbientGroundColor, Color.white, 0.06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = UnityEngine.FogMode.Linear;
        RenderSettings.fogStartDistance = map.FogStart * 1.08f;
        RenderSettings.fogEndDistance = map.FogEnd * 1.18f;

        // 主摄像机背景色跟随地图主题，避免战场底图漏出时一片黑。
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.Lerp(map.SkyColor, Color.white, 0.16f);
        }
    }

    static void TintObject(string objectName, Color color)
    {
        var go = GameObject.Find(objectName);
        if (go == null) return;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
            renderer.material.color = color;
    }

    static void TintByName(string objectName, Color color)
    {
        var renderers = Object.FindObjectsOfType<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.gameObject.name == objectName && renderer.material != null)
                renderer.material.color = color;
        }
    }

    static void TintByPrefix(string prefix, Color color)
    {
        var renderers = Object.FindObjectsOfType<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.gameObject.name.StartsWith(prefix) && renderer.material != null)
                renderer.material.color = color;
        }
    }

    // ── 运行时 NavMesh 生成（地面 200x200，不依赖编辑器烘焙）────────────
    static void BuildRuntimeNavMesh()
    {
        // 必须使用默认 agentTypeID(index=0)，与 NavMeshAgent 组件的默认设置匹配
        var settings = NavMesh.GetSettingsByIndex(0);
        settings.agentRadius   = 0.5f;
        settings.agentHeight   = 2f;
        settings.agentSlope    = 45f;
        settings.agentClimb    = 0.4f;

        var sources = new List<NavMeshBuildSource>();
        var bounds = new Bounds(Vector3.zero, new Vector3(440f, 10f, 440f));
        var markups = BuildNavMeshMarkups();

        NavMesh.RemoveAllNavMeshData();
        NavMeshBuilder.CollectSources(
            bounds,
            LayerMask.GetMask("Default"),
            NavMeshCollectGeometry.PhysicsColliders,
            0,
            markups,
            sources);

        if (sources.Count == 0)
        {
            var src = new NavMeshBuildSource();
            src.shape     = NavMeshBuildSourceShape.Box;
            src.size      = new Vector3(420f, 0.2f, 420f);
            src.transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            src.area      = 0; // walkable
            sources.Add(src);
        }

        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds,
                                                   Vector3.zero, Quaternion.identity);
        if (data != null)
        {
            NavMesh.AddNavMeshData(data);
            Debug.Log($"[GameInitializer] Runtime NavMesh rebuilt from {sources.Count} physics sources");
        }
        else
        {
            Debug.LogWarning("[GameInitializer] NavMesh build failed — units may not pathfind correctly");
        }
    }

    static List<NavMeshBuildMarkup> BuildNavMeshMarkups()
    {
        int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkableArea < 0) notWalkableArea = 1;

        var markups = new List<NavMeshBuildMarkup>();
        var colliders = Object.FindObjectsOfType<Collider>();
        foreach (var collider in colliders)
        {
            if (collider == null || collider.gameObject.name == "Ground") continue;
            if (!ShouldBlockNavMesh(collider.gameObject)) continue;

            var markup = new NavMeshBuildMarkup
            {
                root = collider.transform,
                overrideArea = true,
                area = notWalkableArea,
                ignoreFromBuild = false
            };
            markups.Add(markup);
        }
        return markups;
    }

    static bool ShouldBlockNavMesh(GameObject go)
    {
        if (go.GetComponentInParent<RTSBuilding>() != null) return true;

        string name = go.name;
        return name == "Rock"
            || name == "Tree"
            || name == "RuinWall"
            || name == "Debris"
            || name == "Sandbag"
            || name == "BagMark"
            || name == "Post"
            || name.StartsWith("Wall");
    }

    // 编辑器预览入口：让 Editor 菜单可以在 EditMode 调用相同逻辑
    public void EditorPreviewSpawn()
    {
        string mapName = PlayerPrefs.GetString("current_map", "沙漠绿洲");
        BattleMapDefinition map = BattleMapCatalog.Get(mapName);
        Random.InitState(map.RandomSeed);
        RuntimeBattleMapBuilder.Rebuild(map);
        ApplyMapTheme(mapName);
        SpawnPlayerBase();
        SpawnEnemyBase();
    }

    // ── 玩家阵营 ─────────────────────────────────────────────
    void SpawnPlayerBase()
    {
        // 主基地
        {
            var mb = SpawnBuilding<MainBase>("MainBase_P", new Vector3(-120f,0f,-120f),
                new Vector3(12,5,12), new Color(0.2f,0.4f,0.8f), true, "主基地");
            AddBuildingLabel(mb.gameObject, "主基地", Color.white);
        }

        // 兵工厂
        {
            var b = SpawnBuilding<Barracks>("Barracks_P", new Vector3(-90f,0f,-120f),
                new Vector3(8,4,8), new Color(0.3f,0.6f,0.3f), true, "兵工厂");
            b.ProductionUnits = LoadPlayerUnitPrefabs("Infantry_P");
            b.ProductionTimes = new float[]{ 5f };
            b.ProductionCosts = new int[]  { 100 };
            AddBuildingLabel(b.gameObject, "兵工厂", Color.yellow);
        }

        // 飞机厂
        {
            var a = SpawnBuilding<AirFactory>("AirFactory_P", new Vector3(-90f,0f,-90f),
                new Vector3(9,4,9), new Color(0.2f,0.6f,0.8f), true, "飞机厂");
            a.ProductionUnits = LoadPlayerUnitPrefabs("Fighter_P", "Bomber_P", "ScoutPlane_P");
            a.ProductionTimes = new float[]{ 10f, 18f, 8f };
            a.ProductionCosts = new int[]  { 200, 400, 120 };
            AddBuildingLabel(a.gameObject, "飞机厂", new Color(0.4f,0.85f,1f));
        }

        // 特需厂
        {
            var tk = SpawnBuilding<TankFactory>("TankFactory_P", new Vector3(-60f,0f,-90f),
                new Vector3(9,4,9), new Color(0.5f,0.35f,0.1f), true, "特需厂");
            tk.ProductionUnits = LoadPlayerUnitPrefabs("Tank_P", "Artillery_P", "Flamethrower_P");
            tk.ProductionTimes = new float[]{ 15f, 10f, 8f };
            tk.ProductionCosts = new int[]  { 300, 160, 220 };
            AddBuildingLabel(tk.gameObject, "特需厂", new Color(1f,0.7f,0.2f));
        }

        // 电厂
        {
            var pw = SpawnBuilding<PowerPlant>("PowerPlant_P", new Vector3(-120f,0f,-90f),
                new Vector3(6,3,6), new Color(0.9f,0.8f,0.2f), true, "电厂");
            AddBuildingLabel(pw.gameObject, "电厂", new Color(1f,0.95f,0.3f));
        }

        // 金矿
        {
            var gm = SpawnBuilding<GoldMine>("GoldMine_P", new Vector3(-60f,0f,-120f),
                new Vector3(6,3,6), new Color(0.9f,0.7f,0.1f), true, "金矿");
            AddBuildingLabel(gm.gameObject, "金矿", new Color(1f,0.85f,0.1f));
        }
    }

    // ── 敌方阵营（与玩家方对称，联机时 Guest 将控制这些建筑）────
    void SpawnEnemyBase()
    {
        // 主基地
        {
            var mb = SpawnBuilding<MainBase>("MainBase_E", new Vector3(120f,0f,120f),
                new Vector3(12,5,12), new Color(0.7f,0.15f,0.15f), false, "敌方主基地");
            AddBuildingLabel(mb.gameObject, "敌方基地", Color.white);
        }

        // 兵营
        {
            var eb = SpawnBuilding<Barracks>("Barracks_E", new Vector3(90f,0f,120f),
                new Vector3(8,4,8), new Color(0.55f,0.12f,0.12f), false, "敌方兵营");
            eb.ProductionUnits = LoadPlayerUnitPrefabs("Infantry_E");
            eb.ProductionTimes = new float[]{ 5f };
            eb.ProductionCosts = new int[]  { 100 };
            AddBuildingLabel(eb.gameObject, "兵营", new Color(1f,0.5f,0.5f));
        }

        // 飞机厂
        {
            var ea = SpawnBuilding<AirFactory>("AirFactory_E", new Vector3(90f,0f,90f),
                new Vector3(9,4,9), new Color(0.2f,0.3f,0.6f), false, "飞机厂");
            ea.ProductionUnits = LoadPlayerUnitPrefabs("Fighter_E", "Bomber_E", "ScoutPlane_E");
            ea.ProductionTimes = new float[]{ 10f, 18f, 8f };
            ea.ProductionCosts = new int[]  { 200, 400, 120 };
            AddBuildingLabel(ea.gameObject, "飞机厂", new Color(0.5f,0.7f,1f));
        }

        // 特需厂
        {
            var etk = SpawnBuilding<TankFactory>("TankFactory_E", new Vector3(60f,0f,90f),
                new Vector3(9,4,9), new Color(0.4f,0.15f,0.05f), false, "特需厂");
            etk.ProductionUnits = LoadPlayerUnitPrefabs("Tank_E", "Artillery_E", "Flamethrower_E");
            etk.ProductionTimes = new float[]{ 15f, 10f, 8f };
            etk.ProductionCosts = new int[]  { 300, 160, 220 };
            AddBuildingLabel(etk.gameObject, "特需厂", new Color(1f,0.55f,0.2f));
        }

        // 电厂
        {
            var epw = SpawnBuilding<PowerPlant>("PowerPlant_E", new Vector3(120f,0f,90f),
                new Vector3(6,3,6), new Color(0.7f,0.5f,0.1f), false, "电厂");
            AddBuildingLabel(epw.gameObject, "电厂", new Color(1f,0.9f,0.3f));
        }

        // 金矿
        {
            var egm = SpawnBuilding<GoldMine>("GoldMine_E", new Vector3(60f,0f,120f),
                new Vector3(6,3,6), new Color(0.7f,0.5f,0.05f), false, "金矿");
            AddBuildingLabel(egm.gameObject, "金矿", new Color(1f,0.85f,0.1f));
        }

        // 前沿炮塔 ×3
        foreach (var tpos in new Vector3[]{ new Vector3(100f,0f,110f), new Vector3(130f,0f,100f), new Vector3(110f,0f,140f) })
        {
            var t = SpawnBuilding<Turret>("Turret_E", tpos,
                new Vector3(4,3,4), new Color(0.5f,0.1f,0.1f), false, "炮塔");
            AddBuildingLabel(t.gameObject, "炮塔", new Color(1f,0.8f,0f));
        }
    }

    // ── 辅助函数 ──────────────────────────────────────────────
    // 加载建筑 Prefab（Resources），找不到则回退色块
    static T SpawnBuilding<T>(string prefabName, Vector3 pos,
        Vector3 fallbackScale, Color fallbackColor, bool playerOwned, string displayName) where T : RTSBuilding
    {
        var prefab = Resources.Load<GameObject>($"Prefabs/{prefabName}");
        GameObject go;
        T comp;
        if (prefab != null)
        {
            go = GameObject.Instantiate(prefab, new Vector3(pos.x, 0f, pos.z), Quaternion.identity);
            go.name = prefabName;
            comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
        }
        else
        {
            Debug.LogWarning($"[GameInitializer] Prefab not found: {prefabName}; no procedural visual will be drawn");
            go = CreateBuildingVisual(prefabName, pos, fallbackScale, fallbackColor);
            comp = go.AddComponent<T>();
        }
        comp.bPlayerOwned = playerOwned;
        comp.DisplayName  = displayName;
        // 为联机模式注册 NetId
        int nid = NetIdTracker.NextLocalId();
        NetIdTracker.RegisterBuilding(nid, comp);
        return comp;
    }

    static GameObject[] LoadPlayerUnitPrefabs(params string[] names)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var n in names)
        {
            var prefab = Resources.Load<GameObject>($"Prefabs/{n}");
            if (prefab != null) list.Add(prefab);
            else Debug.LogWarning($"[GameInitializer] Prefab not found: Prefabs/{n}");
        }
        return list.ToArray();
    }

    static GameObject CreateBuildingVisual(string objName, Vector3 pos, Vector3 scale, Color color)
    {
        var go = new GameObject(objName);
        go.name = objName;
        go.transform.position = new Vector3(pos.x, 0f, pos.z);
        var collider = go.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, scale.y * 0.5f, 0f);
        collider.size = scale;
        return go;
    }

    static void AddBuildingLabel(GameObject building, string label, Color color)
    {
        var labelGO = new GameObject("Label_" + label);
        labelGO.transform.SetParent(building.transform, false);
        // Keep world labels readable without covering the actual model.
        float aboveTop = 0.5f + 3.2f / building.transform.localScale.y;
        labelGO.transform.localPosition = new Vector3(0f, aboveTop, 0f);

        // Target world height: worldH = fontSize * charSize * localScale.y * parentScale.y.
        // fontSize=48, charSize=1.0 → localCharH=48; localScale.y = worldH / (48 * parentScale.y)
        float worldH = 3.4f;
        Vector3 ps = building.transform.localScale;
        labelGO.transform.localScale = new Vector3(
            worldH / (48f * ps.x),
            worldH / (48f * ps.y),
            worldH / (48f * ps.z));

        var tm = labelGO.AddComponent<TextMesh>();
        tm.text          = label;
        tm.fontSize      = 48;
        tm.characterSize = 1.0f;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = color;
        tm.fontStyle     = FontStyle.Bold;
        labelGO.AddComponent<BillboardLabel>();
        // 使标签不被建筑几何体遮挡
        var mr = labelGO.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }
}
