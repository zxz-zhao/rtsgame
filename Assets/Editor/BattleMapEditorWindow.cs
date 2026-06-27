using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BattleMapEditorWindow : EditorWindow
{
    enum ToolMode
    {
        Select,
        Trees,
        Rocks,
        Patches,
        Roads,
        Waters,
        Props,
        Spawns,
    }

    enum SelectionKind
    {
        None,
        Tree,
        Rock,
        Patch,
        Road,
        Water,
        Prop,
        SpawnPoint,
    }

    struct PropPaletteItem
    {
        public readonly string Label;
        public readonly string ResourcePath;
        public readonly float DefaultScale;
        public readonly bool BlocksNavigation;
        public readonly bool Scatter;

        public PropPaletteItem(string label, string resourcePath, float defaultScale, bool blocksNavigation, bool scatter)
        {
            Label = label;
            ResourcePath = resourcePath;
            DefaultScale = defaultScale;
            BlocksNavigation = blocksNavigation;
            Scatter = scatter;
        }
    }

    sealed class EditorSelection
    {
        public SelectionKind Kind;
        public int Index = -1;

        public void Clear()
        {
            Kind = SelectionKind.None;
            Index = -1;
        }
    }

    const string DefaultAssetFolder = "Assets/Resources/MapAssets";
    const string PreviewScenePath = "Assets/Scenes/MapEditorPreview.unity";
    const string CurrentMapPrefKey = "current_map";

    static readonly Color TreeColor = new Color(0.22f, 0.78f, 0.28f, 1f);
    static readonly Color RockHandleColor = new Color(0.65f, 0.70f, 0.76f, 1f);
    static readonly Color RoadHandleColor = new Color(0.92f, 0.82f, 0.28f, 1f);
    static readonly Color WaterHandleColor = new Color(0.22f, 0.70f, 1f, 1f);
    static readonly Color PropHandleColor = new Color(0.88f, 0.58f, 0.22f, 1f);
    static readonly Color PlayerSpawnColor = new Color(0.22f, 0.82f, 1f, 1f);
    static readonly Color EnemySpawnColor = new Color(1f, 0.36f, 0.28f, 1f);
    static readonly Color SelectedColor = new Color(1f, 0.55f, 0.12f, 1f);

    static readonly PropPaletteItem[] PropPalette =
    {
        new PropPaletteItem("Grass", "Prefabs/MapTiles/nature_grass", 0.85f, false, true),
        new PropPaletteItem("Tall Grass", "Prefabs/MapTiles/nature_grass_leafsLarge", 0.85f, false, true),
        new PropPaletteItem("Bush", "Prefabs/MapTiles/nature_plant_bush", 0.95f, false, true),
        new PropPaletteItem("Large Bush", "Prefabs/MapTiles/nature_plant_bushLarge", 1.05f, false, true),
        new PropPaletteItem("Oak Tree", "Prefabs/MapTiles/nature_tree_oak", 1.1f, true, false),
        new PropPaletteItem("Pine Tree", "Prefabs/MapTiles/nature_tree_pineTallA", 1.0f, true, false),
        new PropPaletteItem("Palm Tree", "Prefabs/MapTiles/nature_tree_palmTall", 1.0f, true, false),
        new PropPaletteItem("House", "builtin:house", 1.0f, true, false),
        new PropPaletteItem("Supply Crate", "Prefabs/Props/BattlefieldProp_SupplyCrate", 1.0f, true, false),
        new PropPaletteItem("Ammo Crate", "Prefabs/Props/BattlefieldProp_AmmoCrate", 1.0f, true, false),
        new PropPaletteItem("Sandbag Wall", "Prefabs/Props/BattlefieldProp_SandbagWall", 1.0f, true, false),
        new PropPaletteItem("Ruined Tower", "Prefabs/Props/BattlefieldProp_RuinedTower", 1.0f, true, false),
        new PropPaletteItem("Street Light", "Prefabs/Props/BattlefieldProp_StreetLight", 1.0f, true, false),
        new PropPaletteItem("Brick Rubble", "Prefabs/Props/BattlefieldProp_BrickRubble", 1.0f, true, true),
        new PropPaletteItem("Pirate Platform", "Prefabs/MapTiles/pirate_structure_platform", 1.0f, true, false),
    };

    static readonly string[] PropPaletteLabels = BuildPropPaletteLabels();

    BattleMapAsset selectedAsset;
    ToolMode currentTool;
    readonly EditorSelection selection = new EditorSelection();
    Vector2 scroll;
    int defaultPatchPaletteIndex = 1;
    int propPaletteIndex;
    int cloneTemplateIndex;
    bool autoPreview;
    float brushRadius = 14f;
    float brushSpacing = 8f;
    int brushDensity = 3;
    float propScale = 1f;
    bool propRandomYaw = true;
    bool propBlocksNavigation;
    bool eraseMode;
    bool isPainting;
    bool brushStrokeChanged;
    bool hasLastPaintPoint;
    bool hasLastStripPoint;
    Vector3 lastPaintPoint;
    Vector3 lastStripPoint;

    [MenuItem("RTS/Map/Map Editor")]
    public static void Open()
    {
        BattleMapEditorWindow window = GetWindow<BattleMapEditorWindow>("Map Editor");
        window.minSize = new Vector2(470f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        SyncPropBrushFromPalette();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject is BattleMapAsset mapAsset)
        {
            selectedAsset = mapAsset;
            Repaint();
        }
    }

    void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            selectedAsset = (BattleMapAsset)EditorGUILayout.ObjectField("Map Asset", selectedAsset, typeof(BattleMapAsset), false);
            if (GUILayout.Button("Blank", GUILayout.Width(64f)))
                CreateBlankAsset();
        }

        DrawCreationPanel();

        if (selectedAsset == null)
        {
            EditorGUILayout.HelpBox("Create or select a BattleMapAsset first.", MessageType.Info);
            return;
        }

        BattleMapDefinition map = selectedAsset.Map;
        if (map == null)
        {
            selectedAsset.Map = BattleMapDefinitionUtility.CreateDefault(selectedAsset.name);
            map = selectedAsset.Map;
        }
        BattleMapDefinitionUtility.Sanitize(map, selectedAsset.name);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawGeneralSettings(map);
        DrawBrushSettings();
        DrawPaletteSettings(map);
        DrawCollectionSummary(map);
        DrawSelectionInspector(map);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8f);
        DrawRuntimePanel(map);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Preview Scene", GUILayout.Height(30f)))
                BuildPreviewScene();
            if (GUILayout.Button("Open Preview Scene", GUILayout.Height(30f)))
                OpenPreviewScene();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            autoPreview = EditorGUILayout.ToggleLeft("Auto preview rebuild", autoPreview, GUILayout.Width(150f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping Asset", GUILayout.Width(88f)))
            {
                Selection.activeObject = selectedAsset;
                EditorGUIUtility.PingObject(selectedAsset);
            }
        }
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        currentTool = (ToolMode)GUILayout.Toolbar((int)currentTool,
            new[] { "Select", "Trees", "Rocks", "Patches", "Roads", "Sea", "Props", "Spawns" },
            EditorStyles.toolbarButton);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Left Drag Paint | Shift Erase", EditorStyles.miniLabel, GUILayout.Width(190f));
        EditorGUILayout.EndHorizontal();
    }

    void DrawCreationPanel()
    {
        string[] templates = BattleMapCatalog.GetAllMapNames();
        cloneTemplateIndex = Mathf.Clamp(cloneTemplateIndex, 0, Mathf.Max(0, templates.Length - 1));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Create Map", EditorStyles.boldLabel);
            cloneTemplateIndex = EditorGUILayout.Popup("Template", cloneTemplateIndex, templates);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Blank Map"))
                    CreateBlankAsset();
                if (GUILayout.Button("Create From Template"))
                    CreateAssetFromTemplate();
                if (GUILayout.Button("Overwrite Selected Asset"))
                    OverwriteFromTemplate();
            }
        }
    }

    void DrawRuntimePanel(BattleMapDefinition map)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Runtime Map", EditorStyles.boldLabel);
            string assetPath = AssetDatabase.GetAssetPath(selectedAsset);
            EditorGUILayout.LabelField("Asset Path", string.IsNullOrEmpty(assetPath) ? "Unsaved asset" : assetPath);
            EditorGUILayout.LabelField("Current Map", PlayerPrefs.GetString(CurrentMapPrefKey, BattleMapCatalog.DefaultMapName));

            if (!IsRuntimeMapPath(assetPath))
                EditorGUILayout.HelpBox("Save this map under Assets/Resources/MapAssets before using it in Play Mode.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                    SaveSelectedAsset();
                if (GUILayout.Button("Save Runtime Copy"))
                    SaveRuntimeCopy();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set As Current Map", GUILayout.Height(28f)))
                    SetAsCurrentMap();
                if (GUILayout.Button("Rebuild Game Scene", GUILayout.Height(28f)))
                    RebuildGameScene();
            }
        }
    }

    void DrawGeneralSettings(BattleMapDefinition map)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditField("Name", () => map.Name, value => map.Name = value, (label, value) => EditorGUILayout.TextField(label, value));
            EditField("Random Seed", () => map.RandomSeed, value => map.RandomSeed = Mathf.Max(1, value), (label, value) => EditorGUILayout.IntField(label, value));
            EditField("Fog Start", () => map.FogStart, value => map.FogStart = Mathf.Max(0f, value), (label, value) => EditorGUILayout.FloatField(label, value));
            EditField("Fog End", () => map.FogEnd, value => map.FogEnd = Mathf.Max(map.FogStart + 1f, value), (label, value) => EditorGUILayout.FloatField(label, value));
            defaultPatchPaletteIndex = EditorGUILayout.Popup("New Patch Palette", defaultPatchPaletteIndex,
                new[] { "0 - Patch A", "1 - Patch B", "2 - Player Base", "3 - Enemy Base" });
        }
    }

    void DrawBrushSettings()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.Slider("Radius", brushRadius, 2f, 80f);
            brushSpacing = EditorGUILayout.Slider("Spacing", brushSpacing, 1f, 60f);
            brushDensity = EditorGUILayout.IntSlider("Tree/Rock/Prop Density", brushDensity, 1, 16);
            if (currentTool == ToolMode.Props)
                DrawPropBrushSettings();
            eraseMode = EditorGUILayout.ToggleLeft("Erase mode", eraseMode);
        }
    }

    void DrawPropBrushSettings()
    {
        propPaletteIndex = Mathf.Clamp(propPaletteIndex, 0, Mathf.Max(0, PropPalette.Length - 1));
        PropPaletteItem item = PropPalette[propPaletteIndex];
        EditorGUI.BeginChangeCheck();
        propPaletteIndex = EditorGUILayout.Popup("Prop", propPaletteIndex, PropPaletteLabels);
        if (EditorGUI.EndChangeCheck())
            SyncPropBrushFromPalette();

        item = PropPalette[propPaletteIndex];
        propScale = EditorGUILayout.Slider("Prop Scale", propScale <= 0f ? item.DefaultScale : propScale, 0.15f, 4f);
        propRandomYaw = EditorGUILayout.ToggleLeft("Random rotation", propRandomYaw);
        propBlocksNavigation = EditorGUILayout.ToggleLeft("Blocks unit movement", propBlocksNavigation);
    }

    void DrawPaletteSettings(BattleMapDefinition map)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
            EditColor("Ground", () => map.GroundColor, value => map.GroundColor = value);
            EditColor("Road", () => map.RoadColor, value => map.RoadColor = value);
            EditColor("Road Edge", () => map.RoadEdgeColor, value => map.RoadEdgeColor = value);
            EditColor("Sea / Water", () => map.WaterColor, value => map.WaterColor = value);
            EditColor("Patch A", () => map.PatchAColor, value => map.PatchAColor = value);
            EditColor("Patch B", () => map.PatchBColor, value => map.PatchBColor = value);
            EditColor("Player Base", () => map.PlayerBasePadColor, value => map.PlayerBasePadColor = value);
            EditColor("Enemy Base", () => map.EnemyBasePadColor, value => map.EnemyBasePadColor = value);
            EditColor("Rock", () => map.RockColor, value => map.RockColor = value);
            EditColor("Foliage", () => map.FoliageColor, value => map.FoliageColor = value);
            EditColor("Trunk", () => map.TrunkColor, value => map.TrunkColor = value);
            EditColor("Wall", () => map.WallColor, value => map.WallColor = value);
            EditColor("Ruin", () => map.RuinColor, value => map.RuinColor = value);
            EditColor("Sky", () => map.SkyColor, value => map.SkyColor = value);
            EditColor("Fog", () => map.FogColor, value => map.FogColor = value);
        }
    }

    void DrawCollectionSummary(BattleMapDefinition map)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Counts", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Trees", Count(map.TreePositions).ToString());
            EditorGUILayout.LabelField("Rocks", Count(map.RockClusters).ToString());
            EditorGUILayout.LabelField("Patches", Count(map.Patches).ToString());
            EditorGUILayout.LabelField("Roads", Count(map.Roads).ToString());
            EditorGUILayout.LabelField("Sea Areas", Count(map.Waters).ToString());
            EditorGUILayout.LabelField("Props", Count(map.Props).ToString());
            EditorGUILayout.LabelField("Spawn Points", Count(map.SpawnPoints).ToString());
            EditorGUILayout.LabelField("Ruin Walls", Count(map.RuinWalls).ToString());
            EditorGUILayout.LabelField("Sandbag Rings", Count(map.SandbagRings).ToString());
        }
    }

    void DrawSelectionInspector(BattleMapDefinition map)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);

            if (selection.Kind == SelectionKind.None || selection.Index < 0)
            {
                EditorGUILayout.HelpBox("Use Select to click an element in Scene view. Paint tools draw with left drag.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Type", GetSelectionLabel(selection.Kind));
            EditorGUILayout.LabelField("Index", selection.Index.ToString());

            if (selection.Kind == SelectionKind.Patch)
                DrawPatchInspector(map);
            else if (selection.Kind == SelectionKind.Road)
                DrawStripInspector(map, true);
            else if (selection.Kind == SelectionKind.Water)
                DrawStripInspector(map, false);
            else if (selection.Kind == SelectionKind.Prop)
                DrawPropInspector(map);
            else if (selection.Kind == SelectionKind.SpawnPoint)
                DrawSpawnInspector(map);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Delete Selected Element"))
                DeleteSelection();
        }
    }

    void DrawPatchInspector(BattleMapDefinition map)
    {
        TerrainPatchSpec[] values = map.Patches ?? Array.Empty<TerrainPatchSpec>();
        if (selection.Index >= values.Length)
        {
            selection.Clear();
            return;
        }

        TerrainPatchSpec patch = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        patch.Name = EditorGUILayout.TextField("Name", patch.Name);
        patch.PaletteIndex = EditorGUILayout.Popup("Palette", patch.PaletteIndex,
            new[] { "0 - Patch A", "1 - Patch B", "2 - Player Base", "3 - Enemy Base" });
        if (EditorGUI.EndChangeCheck())
            ApplyPatch(selection.Index, patch);
    }

    void DrawStripInspector(BattleMapDefinition map, bool road)
    {
        TerrainStripSpec[] values = road ? (map.Roads ?? Array.Empty<TerrainStripSpec>()) : (map.Waters ?? Array.Empty<TerrainStripSpec>());
        if (selection.Index >= values.Length)
        {
            selection.Clear();
            return;
        }

        TerrainStripSpec strip = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        strip.Name = EditorGUILayout.TextField("Name", strip.Name);
        if (EditorGUI.EndChangeCheck())
        {
            if (road)
                ApplyRoad(selection.Index, strip);
            else
                ApplyWater(selection.Index, strip);
        }
    }

    void DrawSpawnInspector(BattleMapDefinition map)
    {
        SpawnPointSpec[] values = map.SpawnPoints ?? Array.Empty<SpawnPointSpec>();
        if (selection.Index >= values.Length)
        {
            selection.Clear();
            return;
        }

        SpawnPointSpec spawnPoint = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        spawnPoint.Name = EditorGUILayout.TextField("Name", spawnPoint.Name);
        spawnPoint.IsPlayer = EditorGUILayout.Toggle("Player Spawn", spawnPoint.IsPlayer);
        spawnPoint.TeamIndex = EditorGUILayout.IntField("Team Index", spawnPoint.IsPlayer ? 0 : spawnPoint.TeamIndex);
        spawnPoint.Yaw = EditorGUILayout.FloatField("Yaw", spawnPoint.Yaw);
        if (EditorGUI.EndChangeCheck())
            ApplySpawnPoint(selection.Index, spawnPoint);
    }

    void DrawPropInspector(BattleMapDefinition map)
    {
        MapPropSpec[] values = map.Props ?? Array.Empty<MapPropSpec>();
        if (selection.Index >= values.Length)
        {
            selection.Clear();
            return;
        }

        MapPropSpec prop = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        prop.Name = EditorGUILayout.TextField("Name", prop.Name);
        int paletteIndex = FindPropPaletteIndex(prop.ResourcePath);
        int nextPaletteIndex = EditorGUILayout.Popup("Prop", paletteIndex, PropPaletteLabels);
        if (nextPaletteIndex != paletteIndex)
        {
            PropPaletteItem item = PropPalette[nextPaletteIndex];
            prop.Name = item.Label + (selection.Index + 1);
            prop.ResourcePath = item.ResourcePath;
            prop.Scale = item.DefaultScale;
            prop.BlocksNavigation = item.BlocksNavigation;
        }
        prop.Yaw = EditorGUILayout.FloatField("Yaw", prop.Yaw);
        prop.Scale = EditorGUILayout.Slider("Scale", prop.Scale <= 0f ? 1f : prop.Scale, 0.15f, 4f);
        prop.BlocksNavigation = EditorGUILayout.Toggle("Blocks Movement", prop.BlocksNavigation);
        if (EditorGUI.EndChangeCheck())
            ApplyProp(selection.Index, prop);
    }

    void DrawSpawnPoints(SpawnPointSpec[] points)
    {
        if (points == null)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            SpawnPointSpec spawn = points[i];
            Vector3 pos = ClampToBounds(spawn.Position);
            bool isSelected = selection.Kind == SelectionKind.SpawnPoint && selection.Index == i;
            Quaternion rotation = Quaternion.Euler(0f, spawn.Yaw, 0f);
            Color color = isSelected ? SelectedColor : (spawn.IsPlayer ? PlayerSpawnColor : EnemySpawnColor);
            Handles.color = color;

            float handleSize = HandleUtility.GetHandleSize(pos) * 0.14f;
            Handles.DrawWireDisc(pos + Vector3.up * 0.06f, Vector3.up, handleSize * 1.6f);
            Handles.DrawLine(pos + Vector3.up * 0.12f, pos + Vector3.up * 0.12f + rotation * Vector3.forward * handleSize * 2.1f);
            if (currentTool == ToolMode.Select && Handles.Button(pos + Vector3.up * 0.12f, rotation, handleSize, handleSize, Handles.ConeHandleCap))
            {
                selection.Kind = SelectionKind.SpawnPoint;
                selection.Index = i;
                Repaint();
            }

            Handles.Label(pos + Vector3.up * 2.2f, GetSpawnLabel(spawn));
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (selectedAsset == null || selectedAsset.Map == null)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        DrawBounds();
        DrawPoints(selectedAsset.Map.TreePositions, SelectionKind.Tree, TreeColor, 2.2f);
        DrawPoints(selectedAsset.Map.RockClusters, SelectionKind.Rock, RockHandleColor, 2.4f);
        DrawRects(selectedAsset.Map.Patches, SelectionKind.Patch);
        DrawRects(selectedAsset.Map.Roads, SelectionKind.Road);
        DrawRects(selectedAsset.Map.Waters, SelectionKind.Water);
        DrawProps(selectedAsset.Map.Props);
        DrawSpawnPoints(selectedAsset.Map.SpawnPoints);

        HandleBrushPainting(sceneView);
        if (currentTool == ToolMode.Select)
            DrawSelectionHandles();
    }

    void DrawBounds()
    {
        float half = BattleMapCatalog.MapHalfSize;
        Vector3 a = new Vector3(-half, 0f, -half);
        Vector3 b = new Vector3(-half, 0f, half);
        Vector3 c = new Vector3(half, 0f, half);
        Vector3 d = new Vector3(half, 0f, -half);

        Handles.color = new Color(1f, 1f, 1f, 0.4f);
        Handles.DrawLine(a, b);
        Handles.DrawLine(b, c);
        Handles.DrawLine(c, d);
        Handles.DrawLine(d, a);
    }

    void DrawPoints(Vector3[] points, SelectionKind kind, Color color, float size)
    {
        if (points == null)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 pos = points[i];
            bool isSelected = selection.Kind == kind && selection.Index == i;
            Handles.color = isSelected ? SelectedColor : color;

            float handleSize = HandleUtility.GetHandleSize(pos) * 0.1f * size;
            if (currentTool == ToolMode.Select && Handles.Button(pos, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
            {
                selection.Kind = kind;
                selection.Index = i;
                Repaint();
            }

            Handles.Label(pos + Vector3.up * 1.8f, kind == SelectionKind.Tree ? "Tree" : "Rock");
        }
    }

    void DrawRects(TerrainPatchSpec[] values, SelectionKind kind)
    {
        if (values == null)
            return;

        for (int i = 0; i < values.Length; i++)
            DrawRect(values[i].Center, values[i].Size, values[i].Angle, kind, i, GetPatchColor(values[i].PaletteIndex));
    }

    void DrawRects(TerrainStripSpec[] values, SelectionKind kind)
    {
        if (values == null)
            return;

        Color color = kind == SelectionKind.Road ? RoadHandleColor : WaterHandleColor;
        for (int i = 0; i < values.Length; i++)
            DrawRect(values[i].Center, values[i].Size, values[i].Angle, kind, i, color);
    }

    void DrawRect(Vector3 center, Vector2 size, float angle, SelectionKind kind, int index, Color color)
    {
        bool isSelected = selection.Kind == kind && selection.Index == index;
        Handles.color = isSelected ? SelectedColor : color;

        Vector3[] corners = BuildRectCorners(center, size, angle);
        Handles.DrawAAPolyLine(3f, corners[0], corners[1], corners[2], corners[3], corners[0]);
        Handles.DrawLine(center, Vector3.Lerp(corners[2], corners[3], 0.5f));

        float buttonSize = HandleUtility.GetHandleSize(center) * 0.14f;
        if (currentTool == ToolMode.Select && Handles.Button(center, Quaternion.Euler(90f, 0f, 0f), buttonSize, buttonSize, Handles.RectangleHandleCap))
        {
            selection.Kind = kind;
            selection.Index = index;
            Repaint();
        }
    }

    void DrawProps(MapPropSpec[] props)
    {
        if (props == null)
            return;

        for (int i = 0; i < props.Length; i++)
        {
            MapPropSpec prop = props[i];
            Vector3 pos = ClampToBounds(prop.Position);
            bool isSelected = selection.Kind == SelectionKind.Prop && selection.Index == i;
            Handles.color = isSelected ? SelectedColor : PropHandleColor;

            float scale = Mathf.Max(0.4f, prop.Scale <= 0f ? 1f : prop.Scale);
            float handleSize = HandleUtility.GetHandleSize(pos) * 0.13f * Mathf.Clamp(scale, 0.75f, 1.8f);
            Quaternion rotation = Quaternion.Euler(0f, prop.Yaw, 0f);
            if (currentTool == ToolMode.Select && Handles.Button(pos + Vector3.up * 0.35f, rotation, handleSize, handleSize, Handles.CubeHandleCap))
            {
                selection.Kind = SelectionKind.Prop;
                selection.Index = i;
                Repaint();
            }

            Handles.DrawWireDisc(pos, Vector3.up, GetPropHandleRadius(prop));
            Handles.DrawLine(pos, pos + rotation * Vector3.forward * GetPropHandleRadius(prop));
            Handles.Label(pos + Vector3.up * 1.8f, string.IsNullOrWhiteSpace(prop.Name) ? "Prop" : prop.Name);
        }
    }

    void DrawSelectionHandles()
    {
        if (selection.Kind == SelectionKind.None || selection.Index < 0 || selectedAsset == null)
            return;

        switch (selection.Kind)
        {
            case SelectionKind.Tree:
                EditPointArray(ref selectedAsset.Map.TreePositions, selection.Index, "Move Tree");
                break;
            case SelectionKind.Rock:
                EditPointArray(ref selectedAsset.Map.RockClusters, selection.Index, "Move Rock");
                break;
            case SelectionKind.Patch:
                EditPatchHandle();
                break;
            case SelectionKind.Road:
                EditStripHandle(true);
                break;
            case SelectionKind.Water:
                EditStripHandle(false);
                break;
            case SelectionKind.Prop:
                EditPropHandle();
                break;
            case SelectionKind.SpawnPoint:
                EditSpawnHandle();
                break;
        }
    }

    void EditPointArray(ref Vector3[] array, int index, string undoLabel)
    {
        if (array == null || index < 0 || index >= array.Length)
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(array[index], Quaternion.identity);
        newPosition.y = 0f;
        if (EditorGUI.EndChangeCheck())
        {
            RecordAssetChange(undoLabel);
            array[index] = ClampToBounds(newPosition);
            MarkAssetDirty();
        }
    }

    void EditPatchHandle()
    {
        TerrainPatchSpec[] values = selectedAsset.Map.Patches;
        if (values == null || selection.Index >= values.Length)
            return;

        TerrainPatchSpec patch = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        EditRectTransform(ref patch.Center, ref patch.Size, ref patch.Angle);
        if (EditorGUI.EndChangeCheck())
            ApplyPatch(selection.Index, patch, "Edit Patch");
    }

    void EditStripHandle(bool road)
    {
        TerrainStripSpec[] values = road ? selectedAsset.Map.Roads : selectedAsset.Map.Waters;
        if (values == null || selection.Index >= values.Length)
            return;

        TerrainStripSpec strip = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        EditRectTransform(ref strip.Center, ref strip.Size, ref strip.Angle);
        if (EditorGUI.EndChangeCheck())
        {
            if (road)
                ApplyRoad(selection.Index, strip, "Edit Road");
            else
                ApplyWater(selection.Index, strip, "Edit Water");
        }
    }

    void EditSpawnHandle()
    {
        SpawnPointSpec[] values = selectedAsset.Map.SpawnPoints;
        if (values == null || selection.Index >= values.Length)
            return;

        SpawnPointSpec spawnPoint = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(spawnPoint.Position, Quaternion.Euler(0f, spawnPoint.Yaw, 0f));
        position.y = 0f;
        Quaternion rotation = Handles.RotationHandle(Quaternion.Euler(0f, spawnPoint.Yaw, 0f), position);
        if (EditorGUI.EndChangeCheck())
        {
            spawnPoint.Position = ClampToBounds(position);
            spawnPoint.Yaw = NormalizeAngle(rotation.eulerAngles.y);
            ApplySpawnPoint(selection.Index, spawnPoint, "Edit Spawn Point");
        }
    }

    void EditPropHandle()
    {
        MapPropSpec[] values = selectedAsset.Map.Props;
        if (values == null || selection.Index >= values.Length)
            return;

        MapPropSpec prop = values[selection.Index];
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(prop.Position, Quaternion.Euler(0f, prop.Yaw, 0f));
        position.y = 0f;
        Quaternion rotation = Handles.RotationHandle(Quaternion.Euler(0f, prop.Yaw, 0f), position);
        float handleSize = HandleUtility.GetHandleSize(position) * 1.2f;
        float scale = Handles.ScaleSlider(prop.Scale <= 0f ? 1f : prop.Scale, position, Vector3.up, Quaternion.identity, handleSize, 0.05f);
        if (EditorGUI.EndChangeCheck())
        {
            prop.Position = ClampToBounds(position);
            prop.Yaw = NormalizeAngle(rotation.eulerAngles.y);
            prop.Scale = Mathf.Clamp(scale, 0.15f, 4f);
            ApplyProp(selection.Index, prop, "Edit Prop");
        }
    }

    void EditRectTransform(ref Vector3 center, ref Vector2 size, ref float angle)
    {
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 position = Handles.PositionHandle(center, rotation);
        position.y = 0f;
        rotation = Handles.RotationHandle(rotation, position);
        Vector3 scale = Handles.ScaleHandle(new Vector3(size.x, 1f, size.y), position, rotation, HandleUtility.GetHandleSize(position));

        center = ClampToBounds(position);
        angle = NormalizeAngle(rotation.eulerAngles.y);
        size = new Vector2(Mathf.Clamp(scale.x, 4f, 400f), Mathf.Clamp(scale.z, 4f, 400f));
    }

    void HandleBrushPainting(SceneView sceneView)
    {
        Event evt = Event.current;
        if (evt == null || currentTool == ToolMode.Select)
        {
            EndBrushStroke();
            return;
        }

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (evt.type == EventType.Layout && !evt.alt)
            HandleUtility.AddDefaultControl(controlId);

        if (!TryGetGroundPoint(evt.mousePosition, out Vector3 hitPoint))
        {
            if (evt.type == EventType.MouseUp || evt.type == EventType.Ignore || evt.type == EventType.MouseLeaveWindow)
                EndBrushStroke();
            return;
        }

        bool erasing = eraseMode || evt.shift;
        DrawBrushPreview(ClampToBounds(hitPoint), erasing);

        if (evt.alt)
        {
            if (isPainting)
                EndBrushStroke();
            return;
        }

        switch (evt.type)
        {
            case EventType.MouseDown:
                if (evt.button != 0)
                    break;

                BeginBrushStroke(hitPoint, erasing);
                evt.Use();
                break;

            case EventType.MouseDrag:
                if (evt.button != 0 || !isPainting)
                    break;

                ContinueBrushStroke(hitPoint, erasing);
                evt.Use();
                break;

            case EventType.MouseUp:
                if (evt.button == 0 && isPainting)
                {
                    EndBrushStroke();
                    evt.Use();
                }
                break;

            case EventType.MouseMove:
                sceneView.Repaint();
                break;

            case EventType.Ignore:
            case EventType.MouseLeaveWindow:
                EndBrushStroke();
                break;
        }
    }

    void DrawBrushPreview(Vector3 center, bool erasing)
    {
        Color color = erasing ? new Color(1f, 0.25f, 0.18f, 0.9f) : GetToolColor(currentTool);
        center.y = 0.08f;

        Handles.color = new Color(color.r, color.g, color.b, 0.08f);
        Handles.DrawSolidDisc(center, Vector3.up, brushRadius);
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, brushRadius);
    }

    void BeginBrushStroke(Vector3 point, bool erasing)
    {
        isPainting = true;
        brushStrokeChanged = false;
        hasLastPaintPoint = false;
        hasLastStripPoint = false;
        selection.Clear();

        RecordAssetChange((erasing ? "Erase " : "Paint ") + GetToolLabel(currentTool));
        StampBrush(point, erasing);
    }

    void ContinueBrushStroke(Vector3 point, bool erasing)
    {
        if (currentTool == ToolMode.Spawns && !erasing)
            return;

        point = ClampToBounds(point);
        if (!hasLastPaintPoint)
        {
            StampBrush(point, erasing);
            return;
        }

        float spacing = Mathf.Max(1f, brushSpacing);
        Vector3 delta = point - lastPaintPoint;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < spacing)
            return;

        Vector3 direction = delta / distance;
        int steps = Mathf.FloorToInt(distance / spacing);
        for (int i = 1; i <= steps; i++)
            StampBrush(lastPaintPoint + direction * spacing, erasing);
    }

    void EndBrushStroke()
    {
        if (!isPainting)
            return;

        isPainting = false;
        hasLastPaintPoint = false;
        hasLastStripPoint = false;
        if (brushStrokeChanged)
            MarkAssetDirty();

        brushStrokeChanged = false;
    }

    void StampBrush(Vector3 point, bool erasing)
    {
        point = ClampToBounds(point);
        bool changed = erasing ? EraseAt(point) : PaintAt(point);
        hasLastPaintPoint = true;
        lastPaintPoint = point;

        if (!changed)
            return;

        brushStrokeChanged = true;
        MarkAssetDirty(false);
    }

    bool PaintAt(Vector3 point)
    {
        switch (currentTool)
        {
            case ToolMode.Trees:
                return AddBrushPoints(ref selectedAsset.Map.TreePositions, point, brushDensity, SelectionKind.Tree);
            case ToolMode.Rocks:
                return AddBrushPoints(ref selectedAsset.Map.RockClusters, point, Mathf.Max(1, Mathf.CeilToInt(brushDensity * 0.5f)), SelectionKind.Rock);
            case ToolMode.Patches:
                return AddBrushPatch(point);
            case ToolMode.Roads:
                return AddBrushStrip(point, true);
            case ToolMode.Waters:
                return AddBrushStrip(point, false);
            case ToolMode.Props:
                return AddBrushProps(point);
            case ToolMode.Spawns:
                return AddBrushSpawn(point);
            default:
                return false;
        }
    }

    bool EraseAt(Vector3 point)
    {
        bool changed = false;
        switch (currentTool)
        {
            case ToolMode.Trees:
                changed = RemovePointsNear(ref selectedAsset.Map.TreePositions, point, brushRadius);
                break;
            case ToolMode.Rocks:
                changed = RemovePointsNear(ref selectedAsset.Map.RockClusters, point, brushRadius);
                break;
            case ToolMode.Patches:
                changed = RemovePatchesNear(ref selectedAsset.Map.Patches, point, brushRadius);
                break;
            case ToolMode.Roads:
                changed = RemoveStripsNear(ref selectedAsset.Map.Roads, point, brushRadius);
                break;
            case ToolMode.Waters:
                changed = RemoveStripsNear(ref selectedAsset.Map.Waters, point, brushRadius);
                break;
            case ToolMode.Props:
                changed = RemovePropsNear(ref selectedAsset.Map.Props, point, brushRadius);
                break;
            case ToolMode.Spawns:
                changed = RemoveSpawnsNear(ref selectedAsset.Map.SpawnPoints, point, brushRadius);
                break;
        }

        if (changed)
            selection.Clear();

        return changed;
    }

    bool AddBrushPoints(ref Vector3[] array, Vector3 center, int count, SelectionKind kind)
    {
        List<Vector3> values = new List<Vector3>(array ?? Array.Empty<Vector3>());
        int startCount = values.Count;
        float minDistance = Mathf.Max(1.5f, Mathf.Min(brushSpacing, brushRadius) * 0.45f);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * brushRadius;
            Vector3 point = ClampToBounds(center + new Vector3(offset.x, 0f, offset.y));
            if (!HasNearbyPoint(values, point, minDistance))
                values.Add(point);
        }

        if (values.Count == startCount)
            return false;

        array = values.ToArray();
        selection.Kind = kind;
        selection.Index = array.Length - 1;
        return true;
    }

    bool AddBrushPatch(Vector3 center)
    {
        List<TerrainPatchSpec> values = new List<TerrainPatchSpec>(selectedAsset.Map.Patches ?? Array.Empty<TerrainPatchSpec>());
        float size = Mathf.Max(4f, brushRadius * 2f);
        values.Add(new TerrainPatchSpec(
            "Patch" + (values.Count + 1),
            ClampToBounds(center),
            new Vector2(size, size),
            0f,
            defaultPatchPaletteIndex));

        selectedAsset.Map.Patches = values.ToArray();
        selection.Kind = SelectionKind.Patch;
        selection.Index = values.Count - 1;
        return true;
    }

    bool AddBrushProps(Vector3 center)
    {
        propPaletteIndex = Mathf.Clamp(propPaletteIndex, 0, Mathf.Max(0, PropPalette.Length - 1));
        PropPaletteItem item = PropPalette[propPaletteIndex];
        List<MapPropSpec> values = new List<MapPropSpec>(selectedAsset.Map.Props ?? Array.Empty<MapPropSpec>());
        int startCount = values.Count;
        int count = item.Scatter ? brushDensity : 1;
        float minDistance = Mathf.Max(1f, Mathf.Min(brushSpacing, brushRadius) * 0.55f);

        for (int i = 0; i < count; i++)
        {
            Vector3 point = center;
            if (item.Scatter || count > 1)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * brushRadius;
                point += new Vector3(offset.x, 0f, offset.y);
            }

            point = ClampToBounds(point);
            if (HasNearbyProp(values, point, minDistance))
                continue;

            float yaw = propRandomYaw ? UnityEngine.Random.Range(0f, 360f) : 0f;
            float scale = Mathf.Clamp(propScale <= 0f ? item.DefaultScale : propScale, 0.15f, 4f);
            if (item.Scatter)
                scale *= UnityEngine.Random.Range(0.82f, 1.18f);

            values.Add(new MapPropSpec(
                item.Label + (values.Count + 1),
                item.ResourcePath,
                point,
                yaw,
                scale,
                propBlocksNavigation));
        }

        if (values.Count == startCount)
            return false;

        selectedAsset.Map.Props = values.ToArray();
        selection.Kind = SelectionKind.Prop;
        selection.Index = values.Count - 1;
        return true;
    }

    bool AddBrushSpawn(Vector3 point)
    {
        List<SpawnPointSpec> values = new List<SpawnPointSpec>(selectedAsset.Map.SpawnPoints ?? Array.Empty<SpawnPointSpec>());
        bool makePlayer = values.Count == 0 || !HasPlayerSpawn(values);
        int teamIndex = makePlayer ? 0 : NextEnemyTeamIndex(values);
        string name = makePlayer ? "PlayerSpawn" : "EnemySpawn" + teamIndex;
        values.Add(new SpawnPointSpec(name, ClampToBounds(point), makePlayer ? 45f : 225f, makePlayer, teamIndex));
        selectedAsset.Map.SpawnPoints = values.ToArray();
        selection.Kind = SelectionKind.SpawnPoint;
        selection.Index = values.Count - 1;
        return true;
    }

    bool AddBrushStrip(Vector3 point, bool road)
    {
        Vector3 from = hasLastStripPoint ? lastStripPoint : point;
        Vector3 delta = point - from;
        delta.y = 0f;
        float width = Mathf.Max(4f, brushRadius * 2f);
        float length = Mathf.Max(width, delta.magnitude + Mathf.Max(1f, brushSpacing));
        float angle = delta.sqrMagnitude > 0.001f
            ? Vector3.SignedAngle(Vector3.forward, delta.normalized, Vector3.up)
            : 0f;
        Vector3 center = hasLastStripPoint ? ClampToBounds((from + point) * 0.5f) : ClampToBounds(point);

        TerrainStripSpec strip = new TerrainStripSpec(
            (road ? "Road" : "Water") + (Count(road ? selectedAsset.Map.Roads : selectedAsset.Map.Waters) + 1),
            center,
            new Vector2(width, length),
            angle);

        if (road)
        {
            List<TerrainStripSpec> values = new List<TerrainStripSpec>(selectedAsset.Map.Roads ?? Array.Empty<TerrainStripSpec>()) { strip };
            selectedAsset.Map.Roads = values.ToArray();
            selection.Kind = SelectionKind.Road;
            selection.Index = values.Count - 1;
        }
        else
        {
            List<TerrainStripSpec> values = new List<TerrainStripSpec>(selectedAsset.Map.Waters ?? Array.Empty<TerrainStripSpec>()) { strip };
            selectedAsset.Map.Waters = values.ToArray();
            selection.Kind = SelectionKind.Water;
            selection.Index = values.Count - 1;
        }

        hasLastStripPoint = true;
        lastStripPoint = point;
        return true;
    }

    bool RemovePointsNear(ref Vector3[] array, Vector3 center, float radius)
    {
        if (array == null || array.Length == 0)
            return false;

        float radiusSqr = radius * radius;
        List<Vector3> values = new List<Vector3>(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            if (DistanceSqrXZ(array[i], center) > radiusSqr)
                values.Add(array[i]);
        }

        if (values.Count == array.Length)
            return false;

        array = values.ToArray();
        return true;
    }

    bool RemoveSpawnsNear(ref SpawnPointSpec[] array, Vector3 center, float radius)
    {
        if (array == null || array.Length == 0)
            return false;

        float radiusSqr = radius * radius;
        List<SpawnPointSpec> values = new List<SpawnPointSpec>(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            if (DistanceSqrXZ(array[i].Position, center) > radiusSqr)
                values.Add(array[i]);
        }

        if (values.Count == array.Length)
            return false;

        array = values.ToArray();
        return true;
    }

    bool RemovePropsNear(ref MapPropSpec[] array, Vector3 center, float radius)
    {
        if (array == null || array.Length == 0)
            return false;

        float radiusSqr = radius * radius;
        List<MapPropSpec> values = new List<MapPropSpec>(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            if (DistanceSqrXZ(array[i].Position, center) > radiusSqr)
                values.Add(array[i]);
        }

        if (values.Count == array.Length)
            return false;

        array = values.ToArray();
        return true;
    }

    bool RemovePatchesNear(ref TerrainPatchSpec[] array, Vector3 center, float radius)
    {
        if (array == null || array.Length == 0)
            return false;

        List<TerrainPatchSpec> values = new List<TerrainPatchSpec>(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            TerrainPatchSpec patch = array[i];
            if (!RectIntersectsCircle(center, radius, patch.Center, patch.Size, patch.Angle))
                values.Add(patch);
        }

        if (values.Count == array.Length)
            return false;

        array = values.ToArray();
        return true;
    }

    bool RemoveStripsNear(ref TerrainStripSpec[] array, Vector3 center, float radius)
    {
        if (array == null || array.Length == 0)
            return false;

        List<TerrainStripSpec> values = new List<TerrainStripSpec>(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            TerrainStripSpec strip = array[i];
            if (!RectIntersectsCircle(center, radius, strip.Center, strip.Size, strip.Angle))
                values.Add(strip);
        }

        if (values.Count == array.Length)
            return false;

        array = values.ToArray();
        return true;
    }

    bool TryGetGroundPoint(Vector2 mousePosition, out Vector3 hitPoint)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            hitPoint = ray.GetPoint(distance);
            hitPoint.y = 0f;
            return true;
        }

        hitPoint = default;
        return false;
    }

    void AddPoint(ref Vector3[] array, Vector3 point, SelectionKind kind, string undoLabel)
    {
        RecordAssetChange(undoLabel);
        List<Vector3> values = new List<Vector3>(array ?? Array.Empty<Vector3>()) { point };
        array = values.ToArray();
        selection.Kind = kind;
        selection.Index = array.Length - 1;
        MarkAssetDirty();
    }

    void AddPatch(Vector3 center)
    {
        RecordAssetChange("Add Patch");
        List<TerrainPatchSpec> values = new List<TerrainPatchSpec>(selectedAsset.Map.Patches ?? Array.Empty<TerrainPatchSpec>());
        values.Add(new TerrainPatchSpec(
            "Patch" + (values.Count + 1),
            ClampToBounds(center),
            new Vector2(36f, 36f),
            0f,
            defaultPatchPaletteIndex));
        selectedAsset.Map.Patches = values.ToArray();
        selection.Kind = SelectionKind.Patch;
        selection.Index = values.Count - 1;
        MarkAssetDirty();
    }

    void AddStrip(Vector3 center, bool road)
    {
        RecordAssetChange(road ? "Add Road" : "Add Water");
        TerrainStripSpec strip = new TerrainStripSpec(
            (road ? "Road" : "Water") + (Count(road ? selectedAsset.Map.Roads : selectedAsset.Map.Waters) + 1),
            ClampToBounds(center),
            new Vector2(14f, 52f),
            0f);

        if (road)
        {
            List<TerrainStripSpec> values = new List<TerrainStripSpec>(selectedAsset.Map.Roads ?? Array.Empty<TerrainStripSpec>()) { strip };
            selectedAsset.Map.Roads = values.ToArray();
            selection.Kind = SelectionKind.Road;
            selection.Index = values.Count - 1;
        }
        else
        {
            List<TerrainStripSpec> values = new List<TerrainStripSpec>(selectedAsset.Map.Waters ?? Array.Empty<TerrainStripSpec>()) { strip };
            selectedAsset.Map.Waters = values.ToArray();
            selection.Kind = SelectionKind.Water;
            selection.Index = values.Count - 1;
        }

        MarkAssetDirty();
    }

    void ApplyPatch(int index, TerrainPatchSpec patch, string undoLabel = "Edit Patch")
    {
        TerrainPatchSpec[] values = selectedAsset.Map.Patches ?? Array.Empty<TerrainPatchSpec>();
        if (index < 0 || index >= values.Length)
            return;

        RecordAssetChange(undoLabel);
        patch.Center = ClampToBounds(patch.Center);
        patch.Size = ClampSize(patch.Size);
        values[index] = patch;
        selectedAsset.Map.Patches = values;
        MarkAssetDirty();
    }

    void ApplyRoad(int index, TerrainStripSpec strip, string undoLabel = "Edit Road")
    {
        TerrainStripSpec[] values = selectedAsset.Map.Roads ?? Array.Empty<TerrainStripSpec>();
        if (index < 0 || index >= values.Length)
            return;

        RecordAssetChange(undoLabel);
        strip.Center = ClampToBounds(strip.Center);
        strip.Size = ClampSize(strip.Size);
        values[index] = strip;
        selectedAsset.Map.Roads = values;
        MarkAssetDirty();
    }

    void ApplyWater(int index, TerrainStripSpec strip, string undoLabel = "Edit Water")
    {
        TerrainStripSpec[] values = selectedAsset.Map.Waters ?? Array.Empty<TerrainStripSpec>();
        if (index < 0 || index >= values.Length)
            return;

        RecordAssetChange(undoLabel);
        strip.Center = ClampToBounds(strip.Center);
        strip.Size = ClampSize(strip.Size);
        values[index] = strip;
        selectedAsset.Map.Waters = values;
        MarkAssetDirty();
    }

    void DeleteSelection()
    {
        if (selectedAsset == null)
            return;

        switch (selection.Kind)
        {
            case SelectionKind.Tree:
                DeletePoint(ref selectedAsset.Map.TreePositions, selection.Index, "Delete Tree");
                break;
            case SelectionKind.Rock:
                DeletePoint(ref selectedAsset.Map.RockClusters, selection.Index, "Delete Rock");
                break;
            case SelectionKind.Patch:
                DeleteRect(ref selectedAsset.Map.Patches, selection.Index, "Delete Patch");
                break;
            case SelectionKind.Road:
                DeleteRect(ref selectedAsset.Map.Roads, selection.Index, "Delete Road");
                break;
            case SelectionKind.Water:
                DeleteRect(ref selectedAsset.Map.Waters, selection.Index, "Delete Water");
                break;
            case SelectionKind.Prop:
                DeleteRect(ref selectedAsset.Map.Props, selection.Index, "Delete Prop");
                break;
            case SelectionKind.SpawnPoint:
                DeletePoint(ref selectedAsset.Map.SpawnPoints, selection.Index, "Delete Spawn Point");
                break;
        }

        selection.Clear();
        MarkAssetDirty();
    }

    void DeletePoint(ref Vector3[] array, int index, string undoLabel)
    {
        if (array == null || index < 0 || index >= array.Length)
            return;

        RecordAssetChange(undoLabel);
        List<Vector3> values = new List<Vector3>(array);
        values.RemoveAt(index);
        array = values.ToArray();
    }

    void DeleteRect<T>(ref T[] array, int index, string undoLabel)
    {
        if (array == null || index < 0 || index >= array.Length)
            return;

        RecordAssetChange(undoLabel);
        List<T> values = new List<T>(array);
        values.RemoveAt(index);
        array = values.ToArray();
    }

    void DeletePoint(ref SpawnPointSpec[] array, int index, string undoLabel)
    {
        if (array == null || index < 0 || index >= array.Length)
            return;

        RecordAssetChange(undoLabel);
        List<SpawnPointSpec> values = new List<SpawnPointSpec>(array);
        values.RemoveAt(index);
        array = values.ToArray();
    }

    void ApplySpawnPoint(int index, SpawnPointSpec spawnPoint, string undoLabel = "Edit Spawn Point")
    {
        SpawnPointSpec[] values = selectedAsset.Map.SpawnPoints ?? Array.Empty<SpawnPointSpec>();
        if (index < 0 || index >= values.Length)
            return;

        RecordAssetChange(undoLabel);
        spawnPoint.Position = ClampToBounds(spawnPoint.Position);
        spawnPoint.Yaw = NormalizeAngle(spawnPoint.Yaw);
        if (string.IsNullOrWhiteSpace(spawnPoint.Name))
            spawnPoint.Name = spawnPoint.IsPlayer ? "PlayerSpawn" : "EnemySpawn" + Mathf.Max(1, spawnPoint.TeamIndex);
        spawnPoint.TeamIndex = spawnPoint.IsPlayer ? 0 : Mathf.Max(1, spawnPoint.TeamIndex);

        if (spawnPoint.IsPlayer)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i == index || !values[i].IsPlayer)
                    continue;

                SpawnPointSpec demoted = values[i];
                demoted.IsPlayer = false;
                demoted.TeamIndex = Mathf.Max(1, NextEnemyTeamIndex(new List<SpawnPointSpec>(values)));
                if (string.IsNullOrWhiteSpace(demoted.Name) || demoted.Name == "PlayerSpawn")
                    demoted.Name = "EnemySpawn" + demoted.TeamIndex;
                values[i] = demoted;
            }
        }

        values[index] = spawnPoint;
        selectedAsset.Map.SpawnPoints = values;
        selection.Kind = SelectionKind.SpawnPoint;
        selection.Index = index;
        MarkAssetDirty();
    }

    void ApplyProp(int index, MapPropSpec prop, string undoLabel = "Edit Prop")
    {
        MapPropSpec[] values = selectedAsset.Map.Props ?? Array.Empty<MapPropSpec>();
        if (index < 0 || index >= values.Length)
            return;

        RecordAssetChange(undoLabel);
        prop.Position = ClampToBounds(prop.Position);
        prop.Yaw = NormalizeAngle(prop.Yaw);
        prop.Scale = Mathf.Clamp(prop.Scale <= 0f ? 1f : prop.Scale, 0.15f, 4f);
        if (string.IsNullOrWhiteSpace(prop.Name))
            prop.Name = "Prop" + (index + 1);
        values[index] = prop;
        selectedAsset.Map.Props = values;
        selection.Kind = SelectionKind.Prop;
        selection.Index = index;
        MarkAssetDirty();
    }

    void CreateAssetFromTemplate()
    {
        string[] templates = BattleMapCatalog.GetAllMapNames();
        string mapName = templates.Length == 0 ? "New Map" : templates[Mathf.Clamp(cloneTemplateIndex, 0, templates.Length - 1)];
        BattleMapDefinition template = templates.Length == 0
            ? BattleMapDefinitionUtility.CreateDefault("New Map")
            : BattleMapCatalog.Get(mapName);

        CreateAssetFromDefinition(template);
    }

    void CreateBlankAsset()
    {
        CreateAssetFromDefinition(BattleMapDefinitionUtility.CreateDefault("Custom Map"));
    }

    void CreateAssetFromDefinition(BattleMapDefinition definition)
    {
        Directory.CreateDirectory(DefaultAssetFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath(ToProjectPath(Path.Combine(DefaultAssetFolder, SanitizeFileName(definition.Name) + ".asset")));
        BattleMapAsset asset = CreateInstance<BattleMapAsset>();
        asset.CopyFrom(definition);
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        selectedAsset = asset;
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        selection.Clear();
        ShowNotification(new GUIContent("Map asset created"));
    }

    void OverwriteFromTemplate()
    {
        if (selectedAsset == null)
            return;

        string[] templates = BattleMapCatalog.GetAllMapNames();
        if (templates.Length == 0)
            return;

        string mapName = templates[Mathf.Clamp(cloneTemplateIndex, 0, templates.Length - 1)];
        RecordAssetChange("Overwrite Map From Template");
        selectedAsset.CopyFrom(BattleMapCatalog.Get(mapName));
        MarkAssetDirty();
        selection.Clear();
        Repaint();
    }

    void BuildPreviewScene()
    {
        if (selectedAsset == null)
            return;

        SceneBuilder.BuildPreviewScene(selectedAsset.ToDefinition(), PreviewScenePath);
        AssetDatabase.Refresh();
    }

    void OpenPreviewScene()
    {
        if (!File.Exists(PreviewScenePath))
        {
            BuildPreviewScene();
            return;
        }

        EditorSceneManager.OpenScene(PreviewScenePath);
    }

    void SaveSelectedAsset()
    {
        if (selectedAsset == null)
            return;

        BattleMapDefinitionUtility.Sanitize(selectedAsset.Map, selectedAsset.name);
        EditorUtility.SetDirty(selectedAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ShowNotification(new GUIContent("Map saved"));
    }

    void SaveRuntimeCopy()
    {
        if (selectedAsset == null)
            return;

        selectedAsset = SaveRuntimeCopy(selectedAsset);
        Selection.activeObject = selectedAsset;
        EditorGUIUtility.PingObject(selectedAsset);
        ShowNotification(new GUIContent("Runtime map copy saved"));
    }

    BattleMapAsset SaveRuntimeCopy(BattleMapAsset source)
    {
        Directory.CreateDirectory(DefaultAssetFolder);
        BattleMapDefinition definition = source.ToDefinition();
        string path = AssetDatabase.GenerateUniqueAssetPath(ToProjectPath(Path.Combine(DefaultAssetFolder, SanitizeFileName(definition.Name) + ".asset")));
        BattleMapAsset copy = CreateInstance<BattleMapAsset>();
        copy.CopyFrom(definition);
        copy.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return copy;
    }

    bool EnsureRuntimeAsset()
    {
        if (selectedAsset == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(selectedAsset);
        if (IsRuntimeMapPath(assetPath))
        {
            SaveSelectedAsset();
            return true;
        }

        bool copy = EditorUtility.DisplayDialog(
            "Runtime Map",
            "Play Mode can only load custom maps saved under Assets/Resources/MapAssets. Save a runtime copy now?",
            "Save Copy",
            "Cancel");

        if (!copy)
            return false;

        selectedAsset = SaveRuntimeCopy(selectedAsset);
        return true;
    }

    void SetAsCurrentMap()
    {
        if (!EnsureRuntimeAsset())
            return;

        BattleMapDefinition map = selectedAsset.ToDefinition();
        PlayerPrefs.SetString(CurrentMapPrefKey, map.Name);
        PlayerPrefs.Save();
        ShowNotification(new GUIContent("Current map set: " + map.Name));
    }

    void RebuildGameScene()
    {
        if (!EnsureRuntimeAsset())
            return;

        BattleMapDefinition map = selectedAsset.ToDefinition();
        PlayerPrefs.SetString(CurrentMapPrefKey, map.Name);
        PlayerPrefs.Save();
        SceneBuilder.BuildGameScene(map.Name);
        AssetDatabase.Refresh();
        ShowNotification(new GUIContent("Game scene rebuilt"));
    }

    void RecordAssetChange(string label)
    {
        if (selectedAsset != null)
            Undo.RecordObject(selectedAsset, label);
    }

    void SyncPropBrushFromPalette()
    {
        if (PropPalette.Length == 0)
            return;

        propPaletteIndex = Mathf.Clamp(propPaletteIndex, 0, PropPalette.Length - 1);
        PropPaletteItem item = PropPalette[propPaletteIndex];
        propScale = item.DefaultScale;
        propBlocksNavigation = item.BlocksNavigation;
    }

    void MarkAssetDirty(bool rebuildPreview = true)
    {
        if (selectedAsset == null)
            return;

        BattleMapDefinitionUtility.Sanitize(selectedAsset.Map, selectedAsset.name);
        EditorUtility.SetDirty(selectedAsset);
        if (rebuildPreview && autoPreview)
            BuildPreviewScene();
        Repaint();
        SceneView.RepaintAll();
    }

    void EditColor(string label, Func<Color> getter, Action<Color> setter)
    {
        Color current = getter();
        Color updated = EditorGUILayout.ColorField(label, current);
        if (updated != current)
        {
            RecordAssetChange("Change " + label);
            setter(updated);
            MarkAssetDirty();
        }
    }

    void EditField(string label, Func<string> getter, Action<string> setter, Func<string, string, string> drawer)
    {
        string current = getter();
        string updated = drawer(label, current);
        if (updated != current)
        {
            RecordAssetChange("Edit " + label);
            setter(updated);
            MarkAssetDirty();
        }
    }

    void EditField(string label, Func<int> getter, Action<int> setter, Func<string, int, int> drawer)
    {
        int current = getter();
        int updated = drawer(label, current);
        if (updated != current)
        {
            RecordAssetChange("Edit " + label);
            setter(updated);
            MarkAssetDirty();
        }
    }

    void EditField(string label, Func<float> getter, Action<float> setter, Func<string, float, float> drawer)
    {
        float current = getter();
        float updated = drawer(label, current);
        if (!Mathf.Approximately(updated, current))
        {
            RecordAssetChange("Edit " + label);
            setter(updated);
            MarkAssetDirty();
        }
    }

    static int Count<T>(T[] values)
    {
        return values == null ? 0 : values.Length;
    }

    static string GetSelectionLabel(SelectionKind kind)
    {
        switch (kind)
        {
            case SelectionKind.Tree: return "Tree";
            case SelectionKind.Rock: return "Rock";
            case SelectionKind.Patch: return "Patch";
            case SelectionKind.Road: return "Road";
            case SelectionKind.Water: return "Water";
            case SelectionKind.Prop: return "Prop";
            case SelectionKind.SpawnPoint: return "Spawn Point";
            default: return "None";
        }
    }

    static Color GetPatchColor(int paletteIndex)
    {
        switch (paletteIndex)
        {
            case 1: return new Color(0.48f, 0.82f, 0.24f, 1f);
            case 2: return new Color(0.22f, 0.82f, 0.96f, 1f);
            case 3: return new Color(0.96f, 0.42f, 0.32f, 1f);
            default: return new Color(0.42f, 0.72f, 0.36f, 1f);
        }
    }

    static string GetToolLabel(ToolMode tool)
    {
        switch (tool)
        {
            case ToolMode.Trees: return "Trees";
            case ToolMode.Rocks: return "Rocks";
            case ToolMode.Patches: return "Patches";
            case ToolMode.Roads: return "Roads";
            case ToolMode.Waters: return "Sea";
            case ToolMode.Props: return "Props";
            case ToolMode.Spawns: return "Spawns";
            default: return "Map";
        }
    }

    static string GetSpawnLabel(SpawnPointSpec spawn)
    {
        string name = string.IsNullOrWhiteSpace(spawn.Name)
            ? (spawn.IsPlayer ? "PlayerSpawn" : "EnemySpawn" + Mathf.Max(1, spawn.TeamIndex))
            : spawn.Name;
        return spawn.IsPlayer
            ? "P0 " + name
            : "E" + Mathf.Max(1, spawn.TeamIndex) + " " + name;
    }

    static Color GetToolColor(ToolMode tool)
    {
        switch (tool)
        {
            case ToolMode.Trees: return TreeColor;
            case ToolMode.Rocks: return RockHandleColor;
            case ToolMode.Roads: return RoadHandleColor;
            case ToolMode.Waters: return WaterHandleColor;
            case ToolMode.Props: return PropHandleColor;
            case ToolMode.Spawns: return PlayerSpawnColor;
            case ToolMode.Patches: return new Color(0.48f, 0.82f, 0.24f, 0.9f);
            default: return Color.white;
        }
    }

    static string[] BuildPropPaletteLabels()
    {
        string[] labels = new string[PropPalette.Length];
        for (int i = 0; i < PropPalette.Length; i++)
            labels[i] = PropPalette[i].Label;
        return labels;
    }

    static int FindPropPaletteIndex(string resourcePath)
    {
        for (int i = 0; i < PropPalette.Length; i++)
        {
            if (string.Equals(PropPalette[i].ResourcePath, resourcePath, StringComparison.Ordinal))
                return i;
        }

        return 0;
    }

    static float GetPropHandleRadius(MapPropSpec prop)
    {
        string path = prop.ResourcePath ?? string.Empty;
        float scale = Mathf.Clamp(prop.Scale <= 0f ? 1f : prop.Scale, 0.15f, 4f);
        if (path.Contains("house") || path.Contains("Tower") || path.Contains("platform"))
            return 4.5f * scale;
        if (path.Contains("tree") || path.Contains("Palm"))
            return 3.2f * scale;
        if (path.Contains("grass") || path.Contains("bush") || path.Contains("plant"))
            return 1.6f * scale;
        return 2.4f * scale;
    }

    static bool HasNearbyPoint(List<Vector3> points, Vector3 point, float radius)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < points.Count; i++)
        {
            if (DistanceSqrXZ(points[i], point) <= radiusSqr)
                return true;
        }

        return false;
    }

    static bool HasNearbyProp(List<MapPropSpec> props, Vector3 point, float radius)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < props.Count; i++)
        {
            if (DistanceSqrXZ(props[i].Position, point) <= radiusSqr)
                return true;
        }

        return false;
    }

    static bool HasPlayerSpawn(List<SpawnPointSpec> spawns)
    {
        for (int i = 0; i < spawns.Count; i++)
        {
            if (spawns[i].IsPlayer)
                return true;
        }

        return false;
    }

    static int NextEnemyTeamIndex(List<SpawnPointSpec> spawns)
    {
        int maxTeam = 0;
        for (int i = 0; i < spawns.Count; i++)
        {
            if (!spawns[i].IsPlayer)
                maxTeam = Mathf.Max(maxTeam, spawns[i].TeamIndex);
        }

        return Mathf.Max(1, maxTeam + 1);
    }

    static float DistanceSqrXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    static bool RectIntersectsCircle(Vector3 circleCenter, float radius, Vector3 rectCenter, Vector2 rectSize, float angle)
    {
        Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0f, angle, 0f));
        Vector3 local = inverse * (circleCenter - rectCenter);
        Vector2 half = rectSize * 0.5f;
        float closestX = Mathf.Clamp(local.x, -half.x, half.x);
        float closestZ = Mathf.Clamp(local.z, -half.y, half.y);
        float dx = local.x - closestX;
        float dz = local.z - closestZ;
        return dx * dx + dz * dz <= radius * radius;
    }

    static Vector3 ClampToBounds(Vector3 value)
    {
        float half = BattleMapCatalog.MapHalfSize;
        value.x = Mathf.Clamp(value.x, -half, half);
        value.z = Mathf.Clamp(value.z, -half, half);
        value.y = 0f;
        return value;
    }

    static Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(Mathf.Clamp(size.x, 4f, 400f), Mathf.Clamp(size.y, 4f, 400f));
    }

    static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    static Vector3[] BuildRectCorners(Vector3 center, Vector2 size, float angle)
    {
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 half = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);

        return new[]
        {
            center + rotation * new Vector3(-half.x, 0f, -half.z),
            center + rotation * new Vector3(-half.x, 0f, half.z),
            center + rotation * new Vector3(half.x, 0f, half.z),
            center + rotation * new Vector3(half.x, 0f, -half.z),
        };
    }

    static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "BattleMap";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(' ', '_');
    }

    static bool IsRuntimeMapPath(string assetPath)
    {
        return ToProjectPath(assetPath).StartsWith(DefaultAssetFolder + "/", StringComparison.Ordinal);
    }

    static string ToProjectPath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }
}
