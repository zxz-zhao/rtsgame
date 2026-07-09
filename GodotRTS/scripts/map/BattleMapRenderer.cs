using Godot;
using System;

[Tool]
public partial class BattleMapRenderer : Node3D
{
    const float GroundHeight = -0.02f;
    const float PatchHeight = 0.03f;
    const float WaterModelHeight = 0.045f;
    const float WaterSurfaceHeight = 0.06f;
    const float RoadHeight = 0.08f;
    const string WaterFlowShaderPath = "res://assets/shaders/water_flow.gdshader";
    const string NatureKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/NatureKit/Models/FBX format/";
    const string MilitaryExtractedRoot = "res://assets/unity_migrated/Assets/External/MilitaryModels/Kenney/Extracted/Models/FBX format/";
    const string SurvivalKitRoot = "res://assets/unity_migrated/Assets/External/MilitaryModels/Kenney/Extracted/SurvivalKit/Models/FBX format/";
    const string CityIndustrialRoot = "res://assets/unity_migrated/Assets/External/Downloads/city-industrial/Models/FBX format/";
    const string RetroUrbanRoot = "res://assets/unity_migrated/Assets/External/Kenney/RetroUrbanKit/Models/FBX format/";
    const string BlasterKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/BlasterKit/Models/FBX format/";
    const string CarKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/CarKit/Models/FBX format/";
    const string CastleKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/CastleKit/Models/FBX format/";
    const string TrainKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/TrainKit/Models/FBX format/";
    const string RiverScenePath = "res://assets/unity_migrated/Assets/External/Kenney/NatureKit/Models/FBX format/ground_riverStraight.fbx";
    const string SnowRiverScenePath = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/snow-tile-river-straight.fbx";
    const string RuinWallScenePath = MilitaryExtractedRoot + "castle-wall.fbx";

    [Export] public string PreviewMapName { get; set; } = BattleMapCatalog.DefaultMapName;
    [Export(PropertyHint.File, "*.tres,*.res")] public string AuthoredMapResourcePath { get; set; } = string.Empty;
    [Export] public bool ShowSpawnMarkers { get; set; } = false;
    [Export] public bool TintImportedAssets { get; set; } = true;
    [Export] public bool LivePreviewInEditor { get; set; } = true;
    [Export] public NodePath PlayerBasePath { get; set; } = new("");
    [Export] public NodePath EnemyBasePath { get; set; } = new("");
    [Export] public NodePath PlayerUnitPath { get; set; } = new("");
    [Export] public NodePath EnemyUnitPath { get; set; } = new("");
    [Export] public NodePath SunPath { get; set; } = new("");
    [Export] public NodePath CameraPath { get; set; } = new("");

    Node3D? generatedRoot;
    BattleMapResource? authoredMapResourceCache;
    string authoredMapResourceCachePath = string.Empty;
    string lastPreviewSignature = string.Empty;
    double livePreviewCooldown;
    /// <summary>所有已放置的树木节点，用于战斗时的淡化效果。</summary>
    readonly System.Collections.Generic.List<Node3D> treeNodes = new();
    /// <summary>所有树木的地面位置（XZ），用于判断单位是否在树林内。</summary>
    readonly System.Collections.Generic.List<Vector3> treePositions = new();

    readonly string[] temperateTreeScenes =
    {
        NatureKitRoot + "tree_detailed.fbx",
        NatureKitRoot + "tree_oak.fbx",
        NatureKitRoot + "tree_fat.fbx",
        NatureKitRoot + "tree_tall.fbx",
        NatureKitRoot + "tree_default.fbx",
        NatureKitRoot + "tree_small.fbx"
    };
    readonly string[] jungleTreeScenes =
    {
        NatureKitRoot + "tree_detailed_dark.fbx",
        NatureKitRoot + "tree_tall_dark.fbx",
        NatureKitRoot + "tree_default_dark.fbx",
        NatureKitRoot + "tree_palmDetailedTall.fbx",
        NatureKitRoot + "tree_palmBend.fbx",
        NatureKitRoot + "tree_palmTall.fbx"
    };
    readonly string[] snowTreeScenes =
    {
        NatureKitRoot + "tree_pineTallA_detailed.fbx",
        NatureKitRoot + "tree_pineTallB_detailed.fbx",
        NatureKitRoot + "tree_pineTallC_detailed.fbx",
        NatureKitRoot + "tree_pineTallD_detailed.fbx",
        NatureKitRoot + "tree_pineRoundA.fbx",
        NatureKitRoot + "tree_pineRoundC.fbx",
        NatureKitRoot + "tree_pineSmallA.fbx",
        NatureKitRoot + "tree_pineSmallC.fbx"
    };
    readonly string[] islandTreeScenes =
    {
        NatureKitRoot + "tree_palmDetailedTall.fbx",
        NatureKitRoot + "tree_palmTall.fbx",
        NatureKitRoot + "tree_palmBend.fbx",
        NatureKitRoot + "tree_palmDetailedShort.fbx",
        NatureKitRoot + "tree_palmShort.fbx"
    };
    readonly string[] rockScenes =
    {
        NatureKitRoot + "rock_largeA.fbx",
        NatureKitRoot + "rock_largeB.fbx",
        NatureKitRoot + "rock_largeC.fbx",
        NatureKitRoot + "rock_tallA.fbx",
        NatureKitRoot + "rock_tallB.fbx",
        NatureKitRoot + "rock_smallFlatA.fbx",
        SurvivalKitRoot + "rock-a.fbx",
        SurvivalKitRoot + "rock-b.fbx",
        SurvivalKitRoot + "rock-flat-grass.fbx"
    };
    readonly string[] bushScenes =
    {
        NatureKitRoot + "plant_bush.fbx",
        NatureKitRoot + "plant_bushDetailed.fbx",
        NatureKitRoot + "plant_bushLarge.fbx",
        NatureKitRoot + "plant_bushSmall.fbx"
    };
    readonly string[] grassScenes =
    {
        NatureKitRoot + "grass_large.fbx",
        NatureKitRoot + "grass_leafs.fbx",
        NatureKitRoot + "grass_leafsLarge.fbx",
        MilitaryExtractedRoot + "grass-patch.fbx",
        MilitaryExtractedRoot + "grass-plant.fbx",
        SurvivalKitRoot + "patch-grass.fbx"
    };
    readonly (string Path, Vector3 Position, float Yaw, float Height, float Span)[] cityRuinPlacements =
    {
        (CityIndustrialRoot + "building-a.fbx", new Vector3(-104f, 0f, 26f), 8f, 22f, 20f),
        (CityIndustrialRoot + "building-d.fbx", new Vector3(-62f, 0f, 86f), -4f, 18f, 16f),
        (CityIndustrialRoot + "building-h.fbx", new Vector3(-18f, 0f, 122f), 12f, 14f, 14f),
        (CityIndustrialRoot + "building-j.fbx", new Vector3(52f, 0f, -82f), -18f, 18f, 18f),
        (CityIndustrialRoot + "building-m.fbx", new Vector3(98f, 0f, -22f), 14f, 21f, 18f),
        (CityIndustrialRoot + "building-q.fbx", new Vector3(18f, 0f, -118f), -6f, 15f, 14f),
        (CityIndustrialRoot + "chimney-medium.fbx", new Vector3(-86f, 0f, -18f), 0f, 17f, 10f),
        (CityIndustrialRoot + "chimney-large.fbx", new Vector3(86f, 0f, 18f), 0f, 20f, 12f)
    };

    public override async void _Ready()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RefreshMap();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() || !LivePreviewInEditor)
            return;

        livePreviewCooldown -= delta;
        if (livePreviewCooldown > 0d)
            return;

        livePreviewCooldown = 0.6d;
        var map = ResolveMap();
        var signature = BuildPreviewSignature(map);
        if (signature == lastPreviewSignature)
            return;

        Build(map);
    }

    public void RefreshMap()
    {
        Build(ResolveMap());
    }

    public void ReloadAuthoredMapResource()
    {
        authoredMapResourceCache = null;
        authoredMapResourceCachePath = string.Empty;
    }

    public void Build(BattleMapDefinition map)
    {
        generatedRoot?.QueueFree();
        generatedRoot = new Node3D { Name = "GeneratedMap" };
        AddChild(generatedRoot);
        treeNodes.Clear();
        treePositions.Clear();

        ApplyWorldEnvironment(map);
        AddGround(map);
        AddWater(map);
        AddStrips(map.Roads, map.RoadColor, RoadHeight, "Road");
        AddWaterColliders(map);
        AddPatches(map);
        AddRocks(map);
        AddTrees(map);
        AddGroundCover(map);
        AddRiverDecorations(map);
        AddPatchDecorations(map);
        AddCityStructures(map);
        AddRuinWalls(map);
        AddSandbags(map);
        AddBattlefieldProps(map);
        AddSpawnMarkers(map);
        ApplySpawnPoints(map);
        FrameOpeningCamera(map);
        lastPreviewSignature = BuildPreviewSignature(map);
    }


    void ApplyWorldEnvironment(BattleMapDefinition map)
    {
        var world = new WorldEnvironment { Name = "WorldEnvironment" };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = map.SkyColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = map.AmbientSkyColor,
            AmbientLightEnergy = 0.75f,
            FogEnabled = true,
            FogLightColor = map.FogColor,
            FogDensity = 0.0035f
        };
        world.Environment = env;
        generatedRoot!.AddChild(world);

        if (!string.IsNullOrEmpty(SunPath.ToString()) && GetNodeOrNull<DirectionalLight3D>(SunPath) is { } sun)
        {
            sun.LightColor = map.AmbientSkyColor.Lerp(Colors.White, 0.45f);
            sun.LightEnergy = map.Name == BattleMapCatalog.JungleName ? 1.75f : 2.35f;
        }
    }

    void AddGround(BattleMapDefinition map)
    {
        var mapSize = ResolveMapSize(map);
        var body = new StaticBody3D { Name = "MapCollision" };
        generatedRoot!.AddChild(body);

        var shape = new CollisionShape3D
        {
            Position = new Vector3(0, -0.5f, 0),
            Shape = new BoxShape3D { Size = new Vector3(mapSize, 1f, mapSize) }
        };
        body.AddChild(shape);

        var mesh = new MeshInstance3D
        {
            Name = "Ground",
            Position = new Vector3(0f, GroundHeight, 0f),
            Mesh = new PlaneMesh { Size = new Vector2(mapSize, mapSize) },
            MaterialOverride = Material(map.GroundColor, 0.9f)
        };
        generatedRoot.AddChild(mesh);
    }

    void AddWaterColliders(BattleMapDefinition map)
    {
        foreach (var strip in map.Waters)
        {
            var body = new StaticBody3D
            {
                Name = $"WaterCollision_{strip.Name}",
                Position = new Vector3(strip.Center.X, 0.9f, strip.Center.Z),
                Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f),
                CollisionLayer = 1u << 1,
                CollisionMask = 0u
            };
            generatedRoot!.AddChild(body);
            body.AddChild(new CollisionShape3D
            {
                Position = Vector3.Zero,
                Shape = new BoxShape3D
                {
                    Size = new Vector3(strip.Size.X, 2.4f, strip.Size.Y)
                }
            });
        }
    }

    void AddStrips(TerrainStripSpec[] strips, Color color, float height, string prefix)
    {
        for (var i = 0; i < strips.Length; i++)
        {
            var strip = strips[i];
            var mesh = new MeshInstance3D
            {
                Name = $"{prefix}_{strip.Name}",
                Position = new Vector3(strip.Center.X, height, strip.Center.Z),
                Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f),
                Mesh = new PlaneMesh { Size = strip.Size },
                MaterialOverride = Material(color, prefix == "Water" ? 0.28f : 0.82f)
            };
            generatedRoot!.AddChild(mesh);
        }
    }

    void AddWater(BattleMapDefinition map)
    {
        foreach (var strip in map.Waters)
            AddWaterStrip(map, strip);
    }

    void AddWaterStrip(BattleMapDefinition map, TerrainStripSpec strip)
    {
        var riverLike = IsRiverLike(strip);
        var visualSize = riverLike
            ? new Vector2(strip.Size.X * 0.42f, strip.Size.Y * 0.96f)
            : strip.Size;
        bool riverInstanced = false;

        if (riverLike)
        {
            var scenePath = PickWaterScene(map);
            if (TryInstanceScene(scenePath, $"WaterTerrain_{strip.Name}") is { } river)
            {
                river.Position = new Vector3(strip.Center.X, WaterModelHeight, strip.Center.Z);
                river.Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f);
                river.Scale = new Vector3(strip.Size.X, 1f, strip.Size.Y);
                
                ApplyRiverMaterial(river, CreateWaterMaterial(map, riverLike));
                
                generatedRoot!.AddChild(river);
                riverInstanced = true;
            }
        }

        if (!riverInstanced)
        {
            var surface = new MeshInstance3D
            {
                Name = $"WaterSurface_{strip.Name}",
                Position = new Vector3(strip.Center.X, WaterSurfaceHeight, strip.Center.Z),
                Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f),
                Mesh = new PlaneMesh { Size = visualSize, SubdivideWidth = 32, SubdivideDepth = 32 },
                MaterialOverride = CreateWaterMaterial(map, riverLike)
            };
            generatedRoot!.AddChild(surface);
        }
    }

    private static void ApplyRiverMaterial(Node node, Material material)
    {
        if (node is MeshInstance3D mesh)
        {
            var name = mesh.Name.ToString().ToLowerInvariant();
            if (name.Contains("water") || name.Contains("river") || name.Contains("liquid"))
            {
                mesh.MaterialOverride = material;
                GD.Print($"[BattleMapRenderer] Applied water material to mesh: {mesh.Name}");
            }
            else
            {
                GD.Print($"[BattleMapRenderer] Non-water mesh in river model: {mesh.Name}");
            }
        }
        foreach (var child in node.GetChildren())
        {
            ApplyRiverMaterial(child, material);
        }
    }

    void AddPatches(BattleMapDefinition map)
    {
        foreach (var patch in map.Patches)
        {
            var mesh = new MeshInstance3D
            {
                Name = $"TerrainPatch_{patch.Name}",
                Position = new Vector3(patch.Center.X, PatchHeight, patch.Center.Z),
                Rotation = new Vector3(0f, Mathf.DegToRad(patch.Angle), 0f),
                Mesh = new PlaneMesh { Size = patch.Size },
                MaterialOverride = Material(map.GetPatchColor(patch.PaletteIndex), 0.88f)
            };
            generatedRoot!.AddChild(mesh);
        }
    }

    void AddRocks(BattleMapDefinition map)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(1, map.RandomSeed);
        for (var i = 0; i < map.RockClusters.Length; i++)
        {
            var pos = map.RockClusters[i];
            if (TryAddImportedScenery(
                PickRockScene(map, i),
                $"Rock_{i:00}",
                new Vector3(pos.X, 0.03f, pos.Z),
                new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                rng.RandfRange(2.6f, 4.8f),
                rng.RandfRange(3.8f, 7.0f),
                map.RockColor,
                preserveMaterials: true))
            {
                continue;
            }

            var rock = new MeshInstance3D
            {
                Name = $"Rock_{i:00}",
                Position = new Vector3(pos.X, 0.35f, pos.Z),
                Rotation = new Vector3(rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.1f, 0.1f)),
                Scale = Vector3.One * rng.RandfRange(2.5f, 5.8f),
                Mesh = new SphereMesh { Radius = 1f, Height = 1.1f, RadialSegments = 7, Rings = 4 },
                MaterialOverride = Material(map.RockColor, 0.94f)
            };
            generatedRoot!.AddChild(rock);
        }
    }

    void AddTrees(BattleMapDefinition map)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(2, map.RandomSeed + 31);
        for (var i = 0; i < map.TreePositions.Length; i++)
        {
            var pos = map.TreePositions[i];
            var targetHeight = map.Name switch
            {
                BattleMapCatalog.IceFortressName => rng.RandfRange(6.8f, 9.8f),
                BattleMapCatalog.SeaChartName => rng.RandfRange(6.0f, 8.4f),
                BattleMapCatalog.JungleName => rng.RandfRange(7.2f, 10.6f),
                _ => rng.RandfRange(5.8f, 8.6f)
            };
            var treePos3D = new Vector3(pos.X, 0f, pos.Z);

            // ─── 容器节点（视觉 + 碰撞 + 元数据） ───────────────────────
            var container = new Node3D
            {
                Name = $"TreeGroup_{i:00}",
                Position = treePos3D
            };
            generatedRoot!.AddChild(container);
            treeNodes.Add(container);
            treePositions.Add(treePos3D);

            // ─── 视觉部分：优先 FBX，失败则用程序化几何 ────────────────
            var visualYaw = rng.RandfRange(0f, Mathf.Tau);
            var imported = TryInstanceScene(PickTreeScene(map, i), $"TreeVisual_{i:00}");
            if (imported is not null)
            {
                FitImportedNode(imported, targetHeight, targetHeight * 0.75f);
                imported.Rotation = new Vector3(0f, visualYaw, 0f);
                // FBX 保留原材质（不覆盖），使树木呈现真实纹理
                container.AddChild(imported);
            }
            else
            {
                // ── fallback：程序化几何 ─────────────────────────────────
                var treeVisual = new Node3D
                {
                    Name = $"TreeVisual_{i:00}",
                    Rotation = new Vector3(0f, visualYaw, 0f),
                    Scale = Vector3.One * rng.RandfRange(0.9f, 1.3f)
                };
                container.AddChild(treeVisual);

                var trunkHeight = rng.RandfRange(2.4f, 3.6f);
                var trunkBottomR = rng.RandfRange(0.22f, 0.32f);
                var trunkTopR = trunkBottomR * 0.55f;
                var trunkMat = Material(map.TrunkColor, 0.72f);

                treeVisual.AddChild(new MeshInstance3D
                {
                    Name = "TrunkLow",
                    Position = new Vector3(rng.RandfRange(-0.04f, 0.04f), trunkHeight * 0.30f, rng.RandfRange(-0.04f, 0.04f)),
                    Rotation = new Vector3(rng.RandfRange(-0.04f, 0.04f), 0f, rng.RandfRange(-0.04f, 0.04f)),
                    Mesh = new CylinderMesh { TopRadius = trunkBottomR * 0.78f, BottomRadius = trunkBottomR, Height = trunkHeight * 0.60f, RadialSegments = 7 },
                    MaterialOverride = trunkMat
                });
                treeVisual.AddChild(new MeshInstance3D
                {
                    Name = "TrunkHigh",
                    Position = new Vector3(rng.RandfRange(-0.05f, 0.05f), trunkHeight * 0.80f, rng.RandfRange(-0.05f, 0.05f)),
                    Rotation = new Vector3(rng.RandfRange(-0.06f, 0.06f), 0f, rng.RandfRange(-0.06f, 0.06f)),
                    Mesh = new CylinderMesh { TopRadius = trunkTopR * 0.5f, BottomRadius = trunkBottomR * 0.78f, Height = trunkHeight * 0.44f, RadialSegments = 6 },
                    MaterialOverride = trunkMat
                });
                treeVisual.AddChild(new MeshInstance3D
                {
                    Name = "RootBulge",
                    Position = new Vector3(0f, 0.10f, 0f),
                    Mesh = new SphereMesh { Radius = trunkBottomR * 1.55f, Height = 0.30f, RadialSegments = 8, Rings = 3 },
                    MaterialOverride = trunkMat
                });

                var crownBaseY = trunkHeight * 0.88f;
                var crownBaseR = rng.RandfRange(1.05f, 1.45f);
                var foliageMat  = Material(map.FoliageColor, 0.90f);
                var foliageDark = Material(map.FoliageColor.Darkened(0.18f), 0.88f);
                var foliageMid  = Material(map.FoliageColor.Darkened(0.08f), 0.89f);

                treeVisual.AddChild(new MeshInstance3D
                {
                    Name = "CrownBottom",
                    Position = new Vector3(rng.RandfRange(-0.1f, 0.1f), crownBaseY + crownBaseR * 0.30f, rng.RandfRange(-0.1f, 0.1f)),
                    Rotation = new Vector3(rng.RandfRange(-0.05f, 0.05f), rng.RandfRange(0f, Mathf.Pi), rng.RandfRange(-0.05f, 0.05f)),
                    Mesh = new SphereMesh { Radius = crownBaseR, Height = crownBaseR * 1.15f, RadialSegments = 10, Rings = 5 },
                    MaterialOverride = foliageDark
                });
                treeVisual.AddChild(new MeshInstance3D
                {
                    Name = "CrownMid",
                    Position = new Vector3(rng.RandfRange(-0.08f, 0.08f), crownBaseY + crownBaseR * 0.95f, rng.RandfRange(-0.08f, 0.08f)),
                    Rotation = new Vector3(rng.RandfRange(-0.05f, 0.05f), rng.RandfRange(0f, Mathf.Pi), rng.RandfRange(-0.05f, 0.05f)),
                    Mesh = new SphereMesh { Radius = crownBaseR * 0.82f, Height = crownBaseR * 1.05f, RadialSegments = 9, Rings = 5 },
                    MaterialOverride = foliageMid
                });
                treeVisual.AddChild(new MeshInstance3D
                {
                    Name = "CrownTop",
                    Position = new Vector3(rng.RandfRange(-0.06f, 0.06f), crownBaseY + crownBaseR * 1.52f, rng.RandfRange(-0.06f, 0.06f)),
                    Rotation = new Vector3(rng.RandfRange(-0.04f, 0.04f), rng.RandfRange(0f, Mathf.Pi), rng.RandfRange(-0.04f, 0.04f)),
                    Mesh = new SphereMesh { Radius = crownBaseR * 0.55f, Height = crownBaseR * 0.80f, RadialSegments = 8, Rings = 4 },
                    MaterialOverride = foliageMat
                });
            }

            // ─── 碰撞体：Layer 3 = forest_zone ─────────────────────────
            // 仅阻挡联盟军地面单位（含此 mask 的单位），反抗军和空军不受影响。
            // CollisionMask = 0 表示此 StaticBody3D 不主动检测任何物体，
            // 只被其他单位的 CollisionMask 包含 Layer 3 时才产生阻挡。
            var forestBody = new StaticBody3D
            {
                Name = "ForestCollider",
                CollisionLayer = RtsUnit.ForestCollisionLayer,
                CollisionMask  = 0u
            };
            forestBody.AddChild(new CollisionShape3D
            {
                Name = "ForestShape",
                Position = new Vector3(0f, 1.5f, 0f),
                Shape = new CylinderShape3D { Radius = 2.5f, Height = 3.0f }
            });
            container.AddChild(forestBody);
        }
    }


    void AddGroundCover(BattleMapDefinition map)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(3, map.RandomSeed + 53);
        var mapHalfSize = BattleMapCatalog.GetMapHalfSize(map);
        var coverCount = map.Name switch
        {
            BattleMapCatalog.JungleName => 54,
            BattleMapCatalog.CityRuinsName => 24,
            BattleMapCatalog.SeaChartName => 42,
            _ => 40
        };
        for (var i = 0; i < coverCount; i++)
        {
            var pos = new Vector3(
                rng.RandfRange(-mapHalfSize + 18f, mapHalfSize - 18f),
                0f,
                rng.RandfRange(-mapHalfSize + 18f, mapHalfSize - 18f));

            if (BattleMapCatalog.IsPointInWater(map, pos, 4f))
                continue;
            if (BattleMapCatalog.DistanceToWater(map, pos) < 7f && rng.Randf() < 0.45f)
                continue;

            var useGrass = map.Name != BattleMapCatalog.CityRuinsName && (map.Name == BattleMapCatalog.JungleName || rng.Randf() > 0.35f);
            var scenePath = useGrass ? PickGrassScene(map, i) : PickBushScene(map, i);
            var targetHeight = useGrass
                ? rng.RandfRange(0.55f, map.Name == BattleMapCatalog.JungleName ? 1.55f : 1.15f)
                : rng.RandfRange(0.90f, 1.80f);
            var targetSpan = useGrass
                ? rng.RandfRange(0.90f, 2.10f)
                : rng.RandfRange(1.20f, 2.60f);

            if (TryAddImportedScenery(
                scenePath,
                $"{(useGrass ? "Grass" : "Bush")}_{i:00}",
                new Vector3(pos.X, 0.02f, pos.Z),
                new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                targetHeight,
                targetSpan,
                map.FoliageColor,
                preserveMaterials: true))
            {
                continue;
            }

            generatedRoot!.AddChild(new MeshInstance3D
            {
                Name = $"Bush_{i:00}",
                Position = new Vector3(pos.X, 0.05f, pos.Z),
                Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                Scale = Vector3.One * rng.RandfRange(0.9f, 1.4f),
                Mesh = new SphereMesh { Radius = 0.45f, Height = 0.7f, RadialSegments = 7, Rings = 4 },
                MaterialOverride = Material(map.FoliageColor, 0.92f)
            });
        }
    }

    void AddRuinWalls(BattleMapDefinition map)
    {
        for (var i = 0; i < map.RuinWalls.Length; i++)
        {
            var wall = map.RuinWalls[i];
            for (var s = 0; s < wall.Segments; s++)
            {
                var offset = (s - (wall.Segments - 1) * 0.5f) * 5.5f;
                var local = new Vector3(offset, 1.1f, 0f).Rotated(Vector3.Up, Mathf.DegToRad(wall.Angle));
                if (TryAddImportedScenery(
                    RuinWallScenePath,
                    $"RuinWall_{i:00}_{s:00}",
                    new Vector3(wall.Center.X, 0f, wall.Center.Z) + local,
                    new Vector3(0f, Mathf.DegToRad(wall.Angle), 0f),
                    2.8f,
                    5.4f,
                    map.RuinColor,
                    preserveMaterials: true))
                {
                    continue;
                }

                var segment = new MeshInstance3D
                {
                    Name = $"RuinWall_{i:00}_{s:00}",
                    Position = new Vector3(wall.Center.X, 0f, wall.Center.Z) + local,
                    Rotation = new Vector3(0f, Mathf.DegToRad(wall.Angle), 0f),
                    Mesh = new BoxMesh { Size = new Vector3(4.8f, 2.2f, 0.8f) },
                    MaterialOverride = Material(map.RuinColor, 0.86f)
                };
                generatedRoot!.AddChild(segment);
            }
        }
    }

    void AddCityStructures(BattleMapDefinition map)
    {
        if (map.Name != BattleMapCatalog.CityRuinsName)
            return;

        foreach (var placement in cityRuinPlacements)
        {
            _ = TryAddImportedScenery(
                placement.Path,
                $"CityRuin_{placement.Position.X:0}_{placement.Position.Z:0}",
                placement.Position,
                new Vector3(0f, Mathf.DegToRad(placement.Yaw), 0f),
                placement.Height,
                placement.Span,
                map.RuinColor,
                preserveMaterials: true);
        }
    }

    void AddBattlefieldProps(BattleMapDefinition map)
    {
        var playerBase = FindPatchCenter(map, 2, new Vector3(-map.BaseSpawnOffset, 0f, -map.BaseSpawnOffset));
        var enemyBase = FindPatchCenter(map, 3, new Vector3(map.BaseSpawnOffset, 0f, map.BaseSpawnOffset));
        var center = FindPatchCenter(map, 1, Vector3.Zero);

        AddBaseProps(playerBase, false);
        AddBaseProps(enemyBase, true);
        AddBaseStagingProps(playerBase, false);
        AddBaseStagingProps(enemyBase, true);
        AddCentralConflictProps(center);

        if (map.Name == BattleMapCatalog.SeaChartName)
        {
            AddSeaBattlefieldProps(map, center, playerBase, enemyBase);
            return;
        }

        AddAmbientUrbanProps(map, center, playerBase, enemyBase);
    }

    void AddBaseProps(Vector3 baseCenter, bool enemy)
    {
        var side = enemy ? -1f : 1f;
        AddSupplyCrateProp(
            $"SupplyCrate_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(side * 24f, 0f, -side * 12f),
            enemy ? 45f : 225f);
        AddAmmoCrateProp(
            $"AmmoCrate_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(side * 15f, 0f, -side * 24f),
            enemy ? 18f : 198f);
        AddVehicleWreckProp(
            $"VehicleWreck_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(-side * 28f, 0f, side * 18f),
            enemy ? 140f : -40f);
    }

    void AddBaseStagingProps(Vector3 baseCenter, bool enemy)
    {
        var side = enemy ? -1f : 1f;
        var facing = enemy ? 45f : 225f;
        var counterFacing = facing + 180f;

        AddBarrierProp(
            $"BaseBarrierA_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(side * 26f, 0f, side * 8f),
            facing,
            1.18f);
        AddBarrierProp(
            $"BaseBarrierB_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(side * 26f, 0f, -side * 8f),
            counterFacing,
            1.18f);
        AddSandbagWallProp(
            $"BaseSandbagA_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(side * 12f, 0f, side * 26f),
            facing - 90f,
            1.08f);
        AddSandbagWallProp(
            $"BaseSandbagB_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(-side * 12f, 0f, -side * 26f),
            facing + 90f,
            1.08f);
        AddStreetLightProp(
            $"BaseLightA_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(-side * 24f, 0f, side * 22f),
            facing);
        AddStreetLightProp(
            $"BaseLightB_{(enemy ? "Enemy" : "Player")}",
            baseCenter + new Vector3(side * 22f, 0f, -side * 24f),
            counterFacing);
    }

    void AddCentralConflictProps(Vector3 center)
    {
        AddTargetMarkerProp("TargetMarker_West", center + new Vector3(-18f, 0f, 12f), 35f);
        AddTargetMarkerProp("TargetMarker_East", center + new Vector3(18f, 0f, -12f), 215f);
        AddVehicleWreckProp("VehicleWreck_Center", center + new Vector3(0f, 0f, 28f), -18f, 1.25f);
    }

    void AddAmbientUrbanProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
    {
        var axis = enemyBase - playerBase;
        axis.Y = 0f;
        if (axis.LengthSquared() < 0.01f)
            axis = new Vector3(1f, 0f, 1f);

        var axisN = axis.Normalized();
        var perp = new Vector3(-axisN.Z, 0f, axisN.X);
        var halfLen = axis.Length() * 0.35f;
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(1, map.RandomSeed * 31 + 17);

        for (var i = 0; i < 10; i++)
        {
            var t = rng.RandfRange(-1f, 1f);
            var side = rng.RandfRange(-42f, 42f);
            var pos = center + axisN * (t * halfLen) + perp * side;
            var yaw = rng.RandfRange(0f, 360f);
            switch (i % 6)
            {
                case 0:
                    AddBarrierProp($"BarrierStrong_{i:00}", pos, yaw, 1.12f);
                    break;
                case 1:
                    AddSandbagWallProp($"SandbagWall_{i:00}", pos, yaw, 1.08f);
                    break;
                case 2:
                    AddBrickRubbleProp($"BrickRubble_{i:00}", pos, yaw, 1.08f);
                    break;
                case 3:
                    AddStreetLightProp($"StreetLight_{i:00}", pos, yaw);
                    break;
                case 4:
                    AddRuinedTowerProp($"RuinedTower_{i:00}", pos, yaw, 1.02f);
                    break;
                default:
                    AddDamagedRailProp($"DamagedRail_{i:00}", pos, yaw, 1.06f);
                    break;
            }
        }

        if (map.Name != BattleMapCatalog.CityRuinsName)
            return;

        AddBrickRubbleProp("CityRubble_North", center + new Vector3(-42f, 0f, 86f), 28f, 1.22f);
        AddBrickRubbleProp("CityRubble_South", center + new Vector3(46f, 0f, -92f), -18f, 1.18f);
        AddVehicleWreckProp("CityTruckWreck", center + new Vector3(18f, 0f, -48f), 164f, 1.18f);
        AddStreetLightProp("CityLight_North", center + new Vector3(-58f, 0f, 58f), 92f);
        AddStreetLightProp("CityLight_South", center + new Vector3(64f, 0f, -60f), -88f);
    }

    void AddSeaBattlefieldProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
    {
        var westShip = BattleMapCatalog.ClosestWaterPoint(map, center + new Vector3(-72f, 0f, 18f));
        var eastShip = BattleMapCatalog.ClosestWaterPoint(map, center + new Vector3(82f, 0f, -24f));
        var playerDock = BattleMapCatalog.ClosestWaterPoint(map, playerBase + new Vector3(-48f, 0f, 62f), 6f);
        var enemyDock = BattleMapCatalog.ClosestWaterPoint(map, enemyBase + new Vector3(48f, 0f, -62f), 6f);

        AddSimpleProp(MilitaryExtractedRoot + "ship-pirate-medium.fbx", "PirateShip_West", westShip, 210f, 8.5f, 15f, new Color(0.72f, 0.56f, 0.34f));
        AddSimpleProp(MilitaryExtractedRoot + "ship-pirate-small.fbx", "PirateShip_East", eastShip, 18f, 6.2f, 11f, new Color(0.70f, 0.54f, 0.32f));
        AddSimpleProp(MilitaryExtractedRoot + "structure-platform-dock.fbx", "SeaDock_Player", playerDock + new Vector3(0f, 0f, -3f), 142f, 4.2f, 11f, new Color(0.55f, 0.38f, 0.20f));
        AddSimpleProp(MilitaryExtractedRoot + "structure-platform-dock-small.fbx", "SeaDock_Enemy", enemyDock + new Vector3(0f, 0f, 3f), -38f, 3.4f, 8f, new Color(0.55f, 0.38f, 0.20f));
        AddSimpleProp(MilitaryExtractedRoot + "flag-pirate-high.fbx", "SeaFlag_Player", playerDock + new Vector3(6f, 0f, 10f), 40f, 7.5f, 3f, new Color(0.88f, 0.82f, 0.54f));
        AddSimpleProp(MilitaryExtractedRoot + "flag-pirate-high.fbx", "SeaFlag_Enemy", enemyDock + new Vector3(-6f, 0f, -10f), 220f, 7.5f, 3f, new Color(0.88f, 0.82f, 0.54f));
        AddSimpleProp(MilitaryExtractedRoot + "palm-detailed-straight.fbx", "SeaPalm_West", center + new Vector3(-28f, 0f, 118f), 0f, 8f, 5f, new Color(0.20f, 0.60f, 0.28f));
        AddSimpleProp(MilitaryExtractedRoot + "palm-detailed-bend.fbx", "SeaPalm_East", center + new Vector3(34f, 0f, -122f), 180f, 7.4f, 5f, new Color(0.20f, 0.60f, 0.28f));
        AddSimpleProp(MilitaryExtractedRoot + "rocks-sand-a.fbx", "SeaRock_West", center + new Vector3(-52f, 0f, 132f), 32f, 2.4f, 6f, new Color(0.66f, 0.56f, 0.38f));
        AddSimpleProp(MilitaryExtractedRoot + "rocks-sand-b.fbx", "SeaRock_East", center + new Vector3(56f, 0f, -136f), -26f, 2.4f, 6f, new Color(0.62f, 0.52f, 0.35f));
    }

    void AddSupplyCrateProp(string name, Vector3 position, float yaw)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (BlasterKitRoot + "crate-wide.fbx", Vector3.Zero, 0f, 1.6f, 2.8f, new Color(0.46f, 0.36f, 0.22f), true),
            (BlasterKitRoot + "crate-small.fbx", new Vector3(0.9f, 0.55f, -0.2f), 18f, 0.75f, 1.1f, new Color(0.40f, 0.30f, 0.18f), true));
    }

    void AddAmmoCrateProp(string name, Vector3 position, float yaw)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (BlasterKitRoot + "crate-medium.fbx", Vector3.Zero, 0f, 1.3f, 2.1f, new Color(0.28f, 0.34f, 0.24f), true),
            (BlasterKitRoot + "clip-large.fbx", new Vector3(-0.55f, 0.42f, 0.18f), 35f, 0.45f, 0.55f, new Color(0.28f, 0.30f, 0.32f), true),
            (BlasterKitRoot + "grenade-a.fbx", new Vector3(0.45f, 0.40f, -0.16f), -20f, 0.40f, 0.40f, new Color(0.34f, 0.38f, 0.28f), true));
    }

    void AddVehicleWreckProp(string name, Vector3 position, float yaw, float scale = 1f)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (CarKitRoot + "debris-drivetrain.fbx", Vector3.Zero, 20f, 1.2f * scale, 2.7f * scale, new Color(0.22f, 0.22f, 0.22f), true),
            (CarKitRoot + "debris-bumper.fbx", new Vector3(0.95f * scale, 0f, 0.26f * scale), -28f, 0.55f * scale, 1.0f * scale, new Color(0.38f, 0.40f, 0.42f), true),
            (CarKitRoot + "wheel-truck.fbx", new Vector3(-0.70f * scale, 0.08f * scale, -0.52f * scale), 50f, 0.75f * scale, 0.75f * scale, new Color(0.05f, 0.05f, 0.05f), true),
            (CarKitRoot + "wheel-dark.fbx", new Vector3(0.60f * scale, 0.08f * scale, -0.66f * scale), -20f, 0.55f * scale, 0.55f * scale, new Color(0.05f, 0.05f, 0.05f), true));
    }

    void AddBarrierProp(string name, Vector3 position, float yaw, float scale = 1f)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (RetroUrbanRoot + "detail-barrier-strong-type-a.fbx", Vector3.Zero, 0f, 1.6f * scale, 3.6f * scale, new Color(0.34f, 0.34f, 0.34f), true),
            (RetroUrbanRoot + "detail-barrier-strong-damaged.fbx", new Vector3(2.8f * scale, 0f, 0.22f * scale), 180f, 1.45f * scale, 3.0f * scale, new Color(0.36f, 0.34f, 0.32f), true));
    }

    void AddSandbagWallProp(string name, Vector3 position, float yaw, float scale = 1f)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (RetroUrbanRoot + "detail-barrier-type-a.fbx", Vector3.Zero, 0f, 1.25f * scale, 3.0f * scale, new Color(0.55f, 0.46f, 0.28f), true),
            (RetroUrbanRoot + "detail-barrier-type-b.fbx", new Vector3(2.3f * scale, 0f, 0f), 0f, 1.2f * scale, 2.8f * scale, new Color(0.50f, 0.42f, 0.25f), true));
    }

    void AddBrickRubbleProp(string name, Vector3 position, float yaw, float scale = 1f)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (RetroUrbanRoot + "detail-bricks-type-a.fbx", Vector3.Zero, 15f, 1.15f * scale, 2.5f * scale, new Color(0.45f, 0.30f, 0.22f), true),
            (RetroUrbanRoot + "detail-bricks-type-b.fbx", new Vector3(1.5f * scale, 0f, 0.5f * scale), -25f, 0.95f * scale, 2.0f * scale, new Color(0.42f, 0.28f, 0.20f), true),
            (RetroUrbanRoot + "detail-block.fbx", new Vector3(-0.8f * scale, 0f, 0.9f * scale), 40f, 0.7f * scale, 1.0f * scale, new Color(0.38f, 0.33f, 0.27f), true));
    }

    void AddStreetLightProp(string name, Vector3 position, float yaw)
    {
        AddSimpleProp(RetroUrbanRoot + "detail-light-double.fbx", name, position, yaw, 6.2f, 2.0f, new Color(0.40f, 0.42f, 0.44f));
    }

    void AddRuinedTowerProp(string name, Vector3 position, float yaw, float scale = 1f)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (CastleKitRoot + "siege-tower-demolished.fbx", Vector3.Zero, 15f, 5.8f * scale, 4.2f * scale, new Color(0.42f, 0.32f, 0.24f), true),
            (RetroUrbanRoot + "detail-bricks-type-a.fbx", new Vector3(1.4f * scale, 0f, 0.4f * scale), 40f, 0.95f * scale, 1.9f * scale, new Color(0.45f, 0.30f, 0.22f), true));
    }

    void AddDamagedRailProp(string name, Vector3 position, float yaw, float scale = 1f)
    {
        AddCompoundProp(
            name,
            position,
            yaw,
            (TrainKitRoot + "railroad-damaged-straight.fbx", new Vector3(-2.0f * scale, 0f, 0f), 0f, 0.8f * scale, 4.6f * scale, new Color(0.30f, 0.28f, 0.26f), true),
            (TrainKitRoot + "railroad-damaged-straight-skew-left.fbx", new Vector3(2.0f * scale, 0f, 0f), 0f, 0.8f * scale, 4.6f * scale, new Color(0.30f, 0.28f, 0.26f), true),
            (MilitaryExtractedRoot + "barrel.fbx", new Vector3(0.4f * scale, 0.1f * scale, 1.2f * scale), 90f, 0.75f * scale, 0.9f * scale, new Color(0.34f, 0.30f, 0.28f), true));
    }

    void AddTargetMarkerProp(string name, Vector3 position, float yaw)
    {
        AddSimpleProp(BlasterKitRoot + "target-large.fbx", name, position, yaw, 2.1f, 1.4f, new Color(0.86f, 0.62f, 0.12f));
    }

    void AddSimpleProp(string path, string name, Vector3 position, float yaw, float height, float span, Color tint)
    {
        _ = TryAddImportedScenery(
            path,
            name,
            position,
            new Vector3(0f, Mathf.DegToRad(yaw), 0f),
            height,
            span,
            tint,
            preserveMaterials: true);
    }

    void AddCompoundProp(
        string name,
        Vector3 position,
        float yaw,
        params (string Path, Vector3 LocalPosition, float LocalYaw, float Height, float Span, Color Tint, bool PreserveMaterials)[] parts)
    {
        var root = new Node3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0f, Mathf.DegToRad(yaw), 0f)
        };

        var anyAdded = false;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var imported = TryInstanceScene(part.Path, $"Part_{i:00}");
            if (imported is null)
                continue;

            FitImportedNode(imported, part.Height, part.Span);
            imported.Position += part.LocalPosition;
            imported.Rotation = new Vector3(0f, Mathf.DegToRad(part.LocalYaw), 0f);
            if (TintImportedAssets && !part.PreserveMaterials)
                TintImported(imported, part.Tint);
            root.AddChild(imported);
            anyAdded = true;
        }

        if (anyAdded)
            generatedRoot!.AddChild(root);
        else
            root.QueueFree();
    }

    void AddSandbags(BattleMapDefinition map)
    {
        foreach (var ring in map.SandbagRings)
        {
            for (var i = 0; i < ring.Count; i++)
            {
                var angle = Mathf.Tau * i / Mathf.Max(1, ring.Count);
                var pos = ring.Center + new Vector3(Mathf.Cos(angle) * ring.Radius, 0.35f, Mathf.Sin(angle) * ring.Radius);
                var bag = new MeshInstance3D
                {
                    Name = $"Sandbag_{(ring.IsEnemy ? "Enemy" : "Player")}_{i:00}",
                    Position = pos,
                    Rotation = new Vector3(0f, -angle, 0f),
                    Mesh = new BoxMesh { Size = new Vector3(3.8f, 0.7f, 1.2f) },
                    MaterialOverride = Material(new Color(0.42f, 0.34f, 0.22f), 0.9f)
                };
                generatedRoot!.AddChild(bag);
            }
        }
    }

    void AddSpawnMarkers(BattleMapDefinition map)
    {
        if (!ShowSpawnMarkers)
            return;

        for (var i = 0; i < map.SpawnPoints.Length; i++)
        {
            var spawn = map.SpawnPoints[i];
            var color = spawn.IsPlayer ? new Color(0.22f, 0.72f, 0.95f) : new Color(0.95f, 0.34f, 0.18f);
            var marker = new Node3D
            {
                Name = $"SpawnMarker_{i:00}_{spawn.Name}",
                Position = spawn.Position,
                Rotation = new Vector3(0f, Mathf.DegToRad(spawn.Yaw), 0f),
            };
            generatedRoot!.AddChild(marker);

            marker.AddChild(new MeshInstance3D
            {
                Name = "BaseDisc",
                Position = new Vector3(0f, 0.08f, 0f),
                Mesh = new CylinderMesh { TopRadius = 1.35f, BottomRadius = 1.35f, Height = 0.16f, RadialSegments = 18 },
                MaterialOverride = Material(color, 0.32f)
            });

            marker.AddChild(new MeshInstance3D
            {
                Name = "ArrowPole",
                Position = new Vector3(0f, 0.80f, 0f),
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.45f, RadialSegments = 12 },
                MaterialOverride = Material(color.Lerp(Colors.White, 0.18f), 0.28f)
            });

            marker.AddChild(new MeshInstance3D
            {
                Name = "ArrowHead",
                Position = new Vector3(0f, 1.70f, 1.10f),
                Rotation = new Vector3(Mathf.DegToRad(90f), 0f, 0f),
                Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.40f, Height = 0.95f, RadialSegments = 14 },
                MaterialOverride = Material(color, 0.20f)
            });

            marker.AddChild(new Label3D
            {
                Name = "MarkerLabel",
                Text = spawn.IsPlayer ? $"P{spawn.TeamIndex} {spawn.Name}" : $"E{spawn.TeamIndex} {spawn.Name}",
                Position = new Vector3(0f, 2.40f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = color.Lerp(Colors.White, 0.10f),
                FontSize = 36,
                OutlineSize = 8,
            });
        }
    }

    void ApplySpawnPoints(BattleMapDefinition map)
    {
        foreach (var spawn in map.SpawnPoints)
        {
            var target = spawn.Name switch
            {
                "PlayerBase" => GetNodeOrNull<Node3D>(PlayerBasePath),
                "PlayerForward" => GetNodeOrNull<Node3D>(PlayerUnitPath),
                "EnemyBase" => GetNodeOrNull<Node3D>(EnemyBasePath),
                "EnemyForward" => GetNodeOrNull<Node3D>(EnemyUnitPath),
                _ => null
            };

            if (target is null)
                continue;

            target.GlobalPosition = spawn.Position;
            target.GlobalRotation = new Vector3(0f, Mathf.DegToRad(spawn.Yaw), 0f);
        }
    }

    void FrameOpeningCamera(BattleMapDefinition map)
    {
        if (string.IsNullOrEmpty(CameraPath.ToString()) || GetNodeOrNull<Camera3D>(CameraPath) is not { } camera)
            return;

        camera.Far = 500f; // Prevent ground culling/clipping on larger maps

        var playerBase = FindSpawn(map, "PlayerBase", new Vector3(-map.BaseSpawnOffset, 0f, -map.BaseSpawnOffset));
        var playerForward = FindSpawn(map, "PlayerForward", new Vector3(-map.ForwardSpawnOffset, 0f, -map.ForwardSpawnOffset));
        var cameraScale = Mathf.Max(1f, ResolveMapScale(map));
        var focus = (playerBase * 0.62f + playerForward * 0.38f) + new Vector3(8f, 1.2f, 8f);
        camera.GlobalPosition = focus + new Vector3(0f, 34f * cameraScale, -44f * cameraScale);
        camera.LookAt(focus, Vector3.Up);
        camera.Fov = Mathf.Lerp(42f, 46f, Mathf.Clamp(cameraScale - 1f, 0f, 1f));
    }

    static float ResolveMapSize(BattleMapDefinition map)
        => BattleMapCatalog.GetMapHalfSize(map) * 2f;

    static float ResolveMapScale(BattleMapDefinition map)
        => BattleMapCatalog.GetMapHalfSize(map) / BattleMapCatalog.DefaultMapHalfSize;

    static Vector3 FindSpawn(BattleMapDefinition map, string name, Vector3 fallback)
    {
        foreach (var spawn in map.SpawnPoints)
        {
            if (spawn.Name == name)
                return spawn.Position;
        }
        return fallback;
    }

    static Vector3 FindPatchCenter(BattleMapDefinition map, int paletteIndex, Vector3 fallback)
    {
        foreach (var patch in map.Patches)
        {
            if (patch.PaletteIndex == paletteIndex)
                return patch.Center;
        }
        return fallback;
    }

    string PickTreeScene(BattleMapDefinition map, int index)
    {
        if (map.Name == BattleMapCatalog.SeaChartName)
            return islandTreeScenes[index % islandTreeScenes.Length];
        if (map.Name == BattleMapCatalog.IceFortressName)
            return snowTreeScenes[index % snowTreeScenes.Length];
        if (map.Name == BattleMapCatalog.JungleName)
            return jungleTreeScenes[index % jungleTreeScenes.Length];
        return temperateTreeScenes[index % temperateTreeScenes.Length];
    }

    string PickRockScene(BattleMapDefinition map, int index)
    {
        if (map.Name == BattleMapCatalog.SeaChartName)
        {
            var seaRocks = new[]
            {
                NatureKitRoot + "rock_largeC.fbx",
                NatureKitRoot + "rock_smallFlatA.fbx",
                SurvivalKitRoot + "rock-sand-a.fbx",
                SurvivalKitRoot + "rock-sand-b.fbx",
                SurvivalKitRoot + "rock-sand-c.fbx"
            };
            return seaRocks[index % seaRocks.Length];
        }

        return rockScenes[index % rockScenes.Length];
    }

    string PickBushScene(BattleMapDefinition map, int index)
    {
        if (map.Name == BattleMapCatalog.SeaChartName)
        {
            var islandBushes = new[]
            {
                NatureKitRoot + "plant_bushSmall.fbx",
                NatureKitRoot + "plant_bushTriangle.fbx",
                NatureKitRoot + "plant_bushLargeTriangle.fbx"
            };
            return islandBushes[index % islandBushes.Length];
        }

        return bushScenes[index % bushScenes.Length];
    }

    string PickGrassScene(BattleMapDefinition map, int index)
    {
        if (map.Name == BattleMapCatalog.IceFortressName)
            return index % 2 == 0
                ? NatureKitRoot + "grass_leafs.fbx"
                : NatureKitRoot + "grass.fbx";
        if (map.Name == BattleMapCatalog.SeaChartName)
            return index % 2 == 0
                ? NatureKitRoot + "grass_leafsLarge.fbx"
                : NatureKitRoot + "grass_large.fbx";
        return grassScenes[index % grassScenes.Length];
    }

    bool TryAddImportedScenery(
        string path,
        string name,
        Vector3 position,
        Vector3 rotation,
        float targetHeight,
        float targetSpan,
        Color tint,
        bool preserveMaterials)
    {
        var imported = TryInstanceScene(path, name);
        if (imported is null)
            return false;

        FitImportedNode(imported, targetHeight, targetSpan);
        imported.Position = position;
        imported.Rotation = rotation;
        if (TintImportedAssets && !preserveMaterials)
            TintImported(imported, tint);
        generatedRoot!.AddChild(imported);
        return true;
    }

    static Node3D? TryInstanceScene(string path, string name)
    {
        var resource = GD.Load<Resource>(path);
        Node3D? node = resource switch
        {
            PackedScene packed => packed.Instantiate<Node3D>(),
            Mesh mesh => new MeshInstance3D { Mesh = mesh },
            _ => null
        };
        if (node is not null)
            node.Name = name;
        return node;
    }

    static void FitImportedNode(Node3D node, float targetHeight, float targetSpan)
    {
        if (!TryGetLocalBounds(node, Transform3D.Identity, out var bounds))
            return;

        var size = bounds.Size;
        var heightScale = targetHeight > 0.01f && size.Y > 0.001f
            ? targetHeight / size.Y
            : 1f;
        var span = Mathf.Max(size.X, size.Z);
        var spanScale = targetSpan > 0.01f && span > 0.001f
            ? targetSpan / span
            : heightScale;
        var scale = targetHeight > 0.01f && targetSpan > 0.01f
            ? Mathf.Min(heightScale, spanScale)
            : targetHeight > 0.01f
                ? heightScale
                : spanScale;

        var center = bounds.Position + bounds.Size * 0.5f;
        node.Position = new Vector3(-center.X * scale, -bounds.Position.Y * scale, -center.Z * scale);
        node.Scale = Vector3.One * scale;
    }

    static bool TryGetLocalBounds(Node node, Transform3D transform, out Aabb bounds)
    {
        bounds = default;
        var hasBounds = false;

        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
        {
            bounds = TransformAabb(meshInstance.Mesh.GetAabb(), transform);
            hasBounds = true;
        }

        foreach (var child in node.GetChildren())
        {
            var childTransform = transform;
            if (child is Node3D child3D)
                childTransform = transform * child3D.Transform;

            if (!TryGetLocalBounds(child, childTransform, out var childBounds))
                continue;

            bounds = hasBounds ? bounds.Merge(childBounds) : childBounds;
            hasBounds = true;
        }

        return hasBounds;
    }

    static Aabb TransformAabb(Aabb aabb, Transform3D transform)
    {
        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var pos = aabb.Position;
        var size = aabb.Size;
        for (var x = 0; x <= 1; x++)
        for (var y = 0; y <= 1; y++)
        for (var z = 0; z <= 1; z++)
        {
            var corner = pos + new Vector3(size.X * x, size.Y * y, size.Z * z);
            var p = transform * corner;
            min = new Vector3(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y), Mathf.Min(min.Z, p.Z));
            max = new Vector3(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y), Mathf.Max(max.Z, p.Z));
        }

        return new Aabb(min, max - min);
    }

    static void TintImported(Node node, Color color)
    {
        foreach (var child in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is MeshInstance3D mesh)
                mesh.MaterialOverride = Material(color, 0.88f);
        }
        if (node is MeshInstance3D self)
            self.MaterialOverride = Material(color, 0.88f);
    }

    static StandardMaterial3D Material(Color color, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = 0f
        };
    }

    static bool IsRiverLike(TerrainStripSpec strip)
    {
        return Mathf.Min(strip.Size.X, strip.Size.Y) <= 28f;
    }

    static string PickWaterScene(BattleMapDefinition map)
    {
        return map.Name == BattleMapCatalog.IceFortressName
            ? SnowRiverScenePath
            : RiverScenePath;
    }

    static Material CreateWaterMaterial(BattleMapDefinition map, bool riverLike)
    {
        var shader = GD.Load<Shader>(WaterFlowShaderPath);
        if (shader is null)
            return Material(map.WaterColor, riverLike ? 0.45f : 0.3f);

        var material = new ShaderMaterial
        {
            Shader = shader
        };

        var highlight = map.Name == BattleMapCatalog.IceFortressName
            ? new Color(0.72f, 0.90f, 0.98f)
            : new Color(0.46f, 0.84f, 0.98f);
        var deepColor = map.Name == BattleMapCatalog.IceFortressName
            ? map.WaterColor.Lerp(new Color(0.20f, 0.50f, 0.76f), 0.45f)
            : map.WaterColor.Lerp(new Color(0.02f, 0.28f, 0.58f), 0.42f);
        var waterColor = deepColor.Lerp(highlight, riverLike ? 0.58f : 0.24f);
        var foamColor = map.FogColor.Lerp(Colors.White, 0.62f);

        material.SetShaderParameter("water_color", new Color(waterColor.R, waterColor.G, waterColor.B, riverLike ? 0.98f : 0.84f));
        material.SetShaderParameter("foam_color", foamColor);
        material.SetShaderParameter("flow_speed", riverLike ? 0.82f : 0.30f);
        material.SetShaderParameter("ripple_scale", riverLike ? 12.5f : 6.5f);
        material.SetShaderParameter("ripple_strength", riverLike ? 0.15f : 0.07f);
        material.SetShaderParameter("edge_fade", riverLike ? 0.28f : 0.12f);
        material.SetShaderParameter("shine_strength", riverLike ? 0.20f : 0.10f);
        return material;
    }

    BattleMapDefinition ResolveMap()
    {
        if (TryResolveAuthoredMapResource(out var authoredMap))
            return authoredMap.ToDefinition();

        var preferredMap = string.IsNullOrWhiteSpace(PreviewMapName)
            ? null
            : PreviewMapName;
        var fallback = Engine.IsEditorHint()
            ? preferredMap
            : GameState.Instance?.SelectedMapName;
        var mapName = BattleMapCatalog.RequestedMapName(fallback);
        return BattleMapCatalog.Get(mapName);
    }

    public bool TryResolveAuthoredMapResource(out BattleMapResource resource)
    {
        resource = null!;
        var resourcePath = AuthoredMapResourcePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourcePath))
            return false;

        if (authoredMapResourceCache is not null
            && string.Equals(authoredMapResourceCachePath, resourcePath, StringComparison.Ordinal))
        {
            resource = authoredMapResourceCache;
            return true;
        }

        if (!ResourceLoader.Exists(resourcePath))
            return false;

        var loaded = GD.Load<Resource>(resourcePath);
        if (loaded is BattleMapResource typed)
        {
            authoredMapResourceCache = typed;
            authoredMapResourceCachePath = resourcePath;
            resource = typed;
            return true;
        }

        if (loaded is not null && TryRehydrateBattleMapResource(loaded, resourcePath, out var hydrated))
        {
            authoredMapResourceCache = hydrated;
            authoredMapResourceCachePath = resourcePath;
            resource = hydrated;
            return true;
        }

        return false;
    }

    static bool TryRehydrateBattleMapResource(Resource source, string resourcePath, out BattleMapResource resource)
    {
        resource = null!;
        if (!HasMapResourceData(source))
            return false;

        var hydrated = new BattleMapResource();
        hydrated.MapName = ReadString(source, nameof(BattleMapResource.MapName), hydrated.MapName);
        hydrated.Description = ReadString(source, nameof(BattleMapResource.Description), hydrated.Description);
        hydrated.RandomSeed = ReadInt(source, nameof(BattleMapResource.RandomSeed), hydrated.RandomSeed);

        hydrated.GroundColor = ReadColor(source, nameof(BattleMapResource.GroundColor), hydrated.GroundColor);
        hydrated.RoadColor = ReadColor(source, nameof(BattleMapResource.RoadColor), hydrated.RoadColor);
        hydrated.RoadEdgeColor = ReadColor(source, nameof(BattleMapResource.RoadEdgeColor), hydrated.RoadEdgeColor);
        hydrated.WaterColor = ReadColor(source, nameof(BattleMapResource.WaterColor), hydrated.WaterColor);
        hydrated.PatchAColor = ReadColor(source, nameof(BattleMapResource.PatchAColor), hydrated.PatchAColor);
        hydrated.PatchBColor = ReadColor(source, nameof(BattleMapResource.PatchBColor), hydrated.PatchBColor);
        hydrated.PlayerBasePadColor = ReadColor(source, nameof(BattleMapResource.PlayerBasePadColor), hydrated.PlayerBasePadColor);
        hydrated.EnemyBasePadColor = ReadColor(source, nameof(BattleMapResource.EnemyBasePadColor), hydrated.EnemyBasePadColor);
        hydrated.RockColor = ReadColor(source, nameof(BattleMapResource.RockColor), hydrated.RockColor);
        hydrated.FoliageColor = ReadColor(source, nameof(BattleMapResource.FoliageColor), hydrated.FoliageColor);
        hydrated.TrunkColor = ReadColor(source, nameof(BattleMapResource.TrunkColor), hydrated.TrunkColor);
        hydrated.WallColor = ReadColor(source, nameof(BattleMapResource.WallColor), hydrated.WallColor);
        hydrated.RuinColor = ReadColor(source, nameof(BattleMapResource.RuinColor), hydrated.RuinColor);
        hydrated.SkyColor = ReadColor(source, nameof(BattleMapResource.SkyColor), hydrated.SkyColor);
        hydrated.FogColor = ReadColor(source, nameof(BattleMapResource.FogColor), hydrated.FogColor);
        hydrated.AmbientSkyColor = ReadColor(source, nameof(BattleMapResource.AmbientSkyColor), hydrated.AmbientSkyColor);
        hydrated.AmbientEquatorColor = ReadColor(source, nameof(BattleMapResource.AmbientEquatorColor), hydrated.AmbientEquatorColor);
        hydrated.AmbientGroundColor = ReadColor(source, nameof(BattleMapResource.AmbientGroundColor), hydrated.AmbientGroundColor);
        hydrated.FogStart = ReadFloat(source, nameof(BattleMapResource.FogStart), hydrated.FogStart);
        hydrated.FogEnd = ReadFloat(source, nameof(BattleMapResource.FogEnd), hydrated.FogEnd);

        hydrated.RockClusters = ReadVector3Array(source, nameof(BattleMapResource.RockClusters), hydrated.RockClusters);
        hydrated.TreePositions = ReadVector3Array(source, nameof(BattleMapResource.TreePositions), hydrated.TreePositions);

        foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.SpawnPoints)))
            hydrated.SpawnPoints.Add(RehydrateSpawnPoint(child));
        foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.Roads)))
            hydrated.Roads.Add(RehydrateStrip(child));
        foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.Waters)))
            hydrated.Waters.Add(RehydrateStrip(child));
        foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.Patches)))
            hydrated.Patches.Add(RehydratePatch(child));

        hydrated.TakeOverPath(resourcePath);
        resource = hydrated;
        return true;
    }

    static bool HasMapResourceData(Resource source)
    {
        return source.Get(nameof(BattleMapResource.MapName)).VariantType != Variant.Type.Nil
            || source.Get(nameof(BattleMapResource.SpawnPoints)).VariantType != Variant.Type.Nil
            || source.Get(nameof(BattleMapResource.Patches)).VariantType != Variant.Type.Nil;
    }

    static MapSpawnPointResource RehydrateSpawnPoint(Resource source)
    {
        var hydrated = new MapSpawnPointResource();
        hydrated.Label = ReadString(source, nameof(MapSpawnPointResource.Label), hydrated.Label);
        hydrated.Position = ReadVector3(source, nameof(MapSpawnPointResource.Position), hydrated.Position);
        hydrated.Yaw = ReadFloat(source, nameof(MapSpawnPointResource.Yaw), hydrated.Yaw);
        hydrated.IsPlayer = ReadBool(source, nameof(MapSpawnPointResource.IsPlayer), hydrated.IsPlayer);
        hydrated.TeamIndex = ReadInt(source, nameof(MapSpawnPointResource.TeamIndex), hydrated.TeamIndex);
        return hydrated;
    }

    static MapStripResource RehydrateStrip(Resource source)
    {
        var hydrated = new MapStripResource();
        hydrated.Label = ReadString(source, nameof(MapStripResource.Label), hydrated.Label);
        hydrated.Center = ReadVector3(source, nameof(MapStripResource.Center), hydrated.Center);
        hydrated.Size = ReadVector2(source, nameof(MapStripResource.Size), hydrated.Size);
        hydrated.Angle = ReadFloat(source, nameof(MapStripResource.Angle), hydrated.Angle);
        return hydrated;
    }

    static MapPatchResource RehydratePatch(Resource source)
    {
        var hydrated = new MapPatchResource();
        hydrated.Label = ReadString(source, nameof(MapPatchResource.Label), hydrated.Label);
        hydrated.Center = ReadVector3(source, nameof(MapPatchResource.Center), hydrated.Center);
        hydrated.Size = ReadVector2(source, nameof(MapPatchResource.Size), hydrated.Size);
        hydrated.Angle = ReadFloat(source, nameof(MapPatchResource.Angle), hydrated.Angle);
        hydrated.PaletteIndex = ReadInt(source, nameof(MapPatchResource.PaletteIndex), hydrated.PaletteIndex);
        return hydrated;
    }

    static string ReadString(GodotObject source, string propertyName, string fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
    }

    static int ReadInt(GodotObject source, string propertyName, int fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    static bool ReadBool(GodotObject source, string propertyName, bool fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    static float ReadFloat(GodotObject source, string propertyName, float fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsSingle();
    }

    static Color ReadColor(GodotObject source, string propertyName, Color fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsColor();
    }

    static Vector2 ReadVector2(GodotObject source, string propertyName, Vector2 fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector2();
    }

    static Vector3 ReadVector3(GodotObject source, string propertyName, Vector3 fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector3();
    }

    static Vector3[] ReadVector3Array(GodotObject source, string propertyName, Vector3[] fallback)
    {
        var value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector3Array();
    }

    static Godot.Collections.Array<Resource> ReadResourceArray(GodotObject source, string propertyName)
    {
        var resources = new Godot.Collections.Array<Resource>();
        var value = source.Get(propertyName);
        if (value.VariantType != Variant.Type.Array)
            return resources;

        var items = value.AsGodotArray();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.VariantType != Variant.Type.Object)
                continue;

            if (item.AsGodotObject() is Resource resource)
                resources.Add(resource);
        }

        return resources;
    }

    static string BuildPreviewSignature(BattleMapDefinition map)
    {
        var signature = $"{map.Name}|{map.RandomSeed}|{map.TreePositions.Length}|{map.RockClusters.Length}|{map.SpawnPoints.Length}|{map.Roads.Length}|{map.Waters.Length}|{map.Patches.Length}";
        for (var i = 0; i < map.SpawnPoints.Length; i++)
        {
            var spawn = map.SpawnPoints[i];
            signature += $"|{spawn.Name}|{spawn.Position.X:F1}|{spawn.Position.Z:F1}|{spawn.Yaw:F1}";
        }
        for (var i = 0; i < map.Roads.Length; i++)
        {
            var road = map.Roads[i];
            signature += $"|{road.Name}|{road.Center.X:F1}|{road.Center.Z:F1}|{road.Size.X:F1}|{road.Size.Y:F1}|{road.Angle:F1}";
        }
        for (var i = 0; i < map.Waters.Length; i++)
        {
            var water = map.Waters[i];
            signature += $"|{water.Center.X:F1}|{water.Center.Z:F1}|{water.Size.X:F1}|{water.Size.Y:F1}|{water.Angle:F1}";
        }
        for (var i = 0; i < map.Patches.Length; i++)
        {
            var patch = map.Patches[i];
            signature += $"|{patch.Name}|{patch.Center.X:F1}|{patch.Center.Z:F1}|{patch.Size.X:F1}|{patch.Size.Y:F1}|{patch.Angle:F1}|{patch.PaletteIndex}";
        }
        return signature;
    }

    // ── 树林查询与视觉效果 ─────────────────────────────────────────────

    /// <summary>
    /// 检查给定世界坐标（XZ 平面）是否在某棵树的覆盖范围（2.5m 半径）内。
    /// </summary>
    public bool IsPositionInForest(Vector3 worldPos)
    {
        const float forestRadius = 2.5f;
        const float forestRadiusSq = forestRadius * forestRadius;
        foreach (var treePos in treePositions)
        {
            var dx = worldPos.X - treePos.X;
            var dz = worldPos.Z - treePos.Z;
            if (dx * dx + dz * dz <= forestRadiusSq)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 淡化/恢复 combatPositions 附近的树木视觉效果。
    /// 当反抗军在树林中作战时，将附近树木变透明，突出战斗单位。
    /// 调用者每帧提供当前处于战斗状态的反抗军位置列表。
    /// </summary>
    public void UpdateForestFade(System.Collections.Generic.IReadOnlyList<Vector3> combatPositions)
    {
        const float fadeRadius = 8f;
        const float fadeRadiusSq = fadeRadius * fadeRadius;

        for (var t = 0; t < treeNodes.Count; t++)
        {
            var treeNode = treeNodes[t];
            if (!GodotObject.IsInstanceValid(treeNode))
                continue;

            var treePos = treePositions[t];
            var shouldFade = false;
            foreach (var combatPos in combatPositions)
            {
                var dx = treePos.X - combatPos.X;
                var dz = treePos.Z - combatPos.Z;
                if (dx * dx + dz * dz <= fadeRadiusSq)
                {
                    shouldFade = true;
                    break;
                }
            }

            ApplyTreeFade(treeNode, shouldFade);
        }
    }

    static void ApplyTreeFade(Node3D container, bool fade)
    {
        // 遍历容器下所有视觉节点（跳过 ForestCollider StaticBody3D）
        foreach (var child in container.GetChildren())
        {
            if (child is StaticBody3D)
                continue;
            if (child is Node3D visual3D)
                SetForestFadeRecursive(visual3D, fade);
        }
    }

    static void SetForestFadeRecursive(Node3D node, bool fade)
    {
        if (node is GeometryInstance3D geom)
        {
            if (fade)
            {
                // 淡化：叠加半透明覆盖层，突出树林中的战斗单位
                if (geom.MaterialOverlay is not StandardMaterial3D overlay
                    || overlay.ResourceName != "_forest_fade_overlay")
                {
                    overlay = new StandardMaterial3D
                    {
                        ResourceName = "_forest_fade_overlay",
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                        NoDepthTest = false,
                        AlbedoColor = new Color(0.1f, 0.12f, 0.08f, 0.55f)
                    };
                    geom.MaterialOverlay = overlay;
                }
            }
            else
            {
                // 恢复：只移除树林淡化覆盖层，保留其他覆盖
                if (geom.MaterialOverlay is StandardMaterial3D mat
                    && mat.ResourceName == "_forest_fade_overlay")
                    geom.MaterialOverlay = null;
            }
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node3D child3D)
                SetForestFadeRecursive(child3D, fade);
        }
    }

    void AddRiverDecorations(BattleMapDefinition map)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(5, map.RandomSeed + 127);

        var index = 0;
        foreach (var strip in map.Waters)
        {
            var riverLike = IsRiverLike(strip);
            if (!riverLike)
                continue; // Only decorate river edges, not large lakes/seas

            // Transform to convert local coordinates to world coordinates
            var transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(strip.Angle)), new Vector3(strip.Center.X, 0f, strip.Center.Z));

            var length = strip.Size.Y;
            var width = strip.Size.X;
            
            // Step size along Z (lengthwise) - say, every 3.5 units
            var stepZ = 3.5f;
            for (float z = -length * 0.45f; z <= length * 0.45f; z += stepZ)
            {
                // Left bank and Right bank X positions
                // Kenney's straight river model has water width around 42%, so the banks start around X = 0.21 * width.
                // We'll place items just outside the water edge, around X = 0.23 * width to 0.35 * width.
                var baseLeftX = -width * rng.RandfRange(0.24f, 0.32f);
                var baseRightX = width * rng.RandfRange(0.24f, 0.32f);

                // Add random offsets to make it look organic
                var leftPos = transform * new Vector3(baseLeftX, 0.03f, z + rng.RandfRange(-1.2f, 1.2f));
                var rightPos = transform * new Vector3(baseRightX, 0.03f, z + rng.RandfRange(-1.2f, 1.2f));

                SpawnRiverEdgeAsset(map, leftPos, rng, ref index);
                SpawnRiverEdgeAsset(map, rightPos, rng, ref index);
            }
        }
    }

    void SpawnRiverEdgeAsset(BattleMapDefinition map, Vector3 worldPos, RandomNumberGenerator rng, ref int index)
    {
        index++;
        var roll = rng.Randf();
        
        // 75% chance for grass/foliage, 15% chance for a small rock/pebble, 10% chance empty
        if (roll < 0.75f)
        {
            var useGrass = rng.Randf() > 0.30f;
            var scenePath = useGrass ? PickGrassScene(map, index) : PickBushScene(map, index);
            
            var targetHeight = useGrass ? rng.RandfRange(0.40f, 0.85f) : rng.RandfRange(0.60f, 1.10f);
            var targetSpan = useGrass ? rng.RandfRange(0.60f, 1.20f) : rng.RandfRange(0.80f, 1.40f);
            
            TryAddImportedScenery(
                scenePath,
                $"RiverFoliage_{index:00}",
                worldPos,
                new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                targetHeight,
                targetSpan,
                map.FoliageColor,
                preserveMaterials: true
            );
        }
        else if (roll < 0.90f)
        {
            var rockPath = PickRockScene(map, index);
            
            var targetHeight = rng.RandfRange(0.25f, 0.65f);
            var targetSpan = rng.RandfRange(0.45f, 0.95f);
            
            TryAddImportedScenery(
                rockPath,
                $"RiverRock_{index:00}",
                new Vector3(worldPos.X, worldPos.Y - 0.05f, worldPos.Z), // slightly sink it
                new Vector3(rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.1f, 0.1f)),
                targetHeight,
                targetSpan,
                map.RockColor,
                preserveMaterials: true
            );
        }
    }

    void AddPatchDecorations(BattleMapDefinition map)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(7, map.RandomSeed + 233);

        var index = 0;
        foreach (var patch in map.Patches)
        {
            var transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(patch.Angle)), new Vector3(patch.Center.X, 0f, patch.Center.Z));

            var width = patch.Size.X;
            var length = patch.Size.Y;

            bool nearWater = BattleMapCatalog.IsPointInWater(map, patch.Center, 2f) || BattleMapCatalog.DistanceToWater(map, patch.Center) < 8f;

            // We step along the boundary of the patch to place decorations
            // For width (horizontal edges)
            var stepX = 4.0f;
            for (float x = -width * 0.5f; x <= width * 0.5f; x += stepX)
            {
                var topPos = transform * new Vector3(x + rng.RandfRange(-1f, 1f), PatchHeight, -length * 0.5f + rng.RandfRange(-0.5f, 0.5f));
                SpawnPatchEdgeAsset(map, topPos, nearWater, rng, ref index);

                var bottomPos = transform * new Vector3(x + rng.RandfRange(-1f, 1f), PatchHeight, length * 0.5f + rng.RandfRange(-0.5f, 0.5f));
                SpawnPatchEdgeAsset(map, bottomPos, nearWater, rng, ref index);
            }

            // For length (vertical edges)
            var stepZ = 4.0f;
            for (float z = -length * 0.5f; z <= length * 0.5f; z += stepZ)
            {
                var leftPos = transform * new Vector3(-width * 0.5f + rng.RandfRange(-0.5f, 0.5f), PatchHeight, z + rng.RandfRange(-1f, 1f));
                SpawnPatchEdgeAsset(map, leftPos, nearWater, rng, ref index);

                var rightPos = transform * new Vector3(width * 0.5f + rng.RandfRange(-0.5f, 0.5f), PatchHeight, z + rng.RandfRange(-1f, 1f));
                SpawnPatchEdgeAsset(map, rightPos, nearWater, rng, ref index);
            }
        }
    }

    void SpawnPatchEdgeAsset(BattleMapDefinition map, Vector3 worldPos, bool nearWater, RandomNumberGenerator rng, ref int index)
    {
        index++;
        var roll = rng.Randf();

        if (nearWater)
        {
            if (roll < 0.50f)
            {
                var scenePath = PickGrassScene(map, index);
                var targetHeight = rng.RandfRange(0.40f, 0.90f);
                var targetSpan = rng.RandfRange(0.60f, 1.20f);
                TryAddImportedScenery(scenePath, $"PatchWaterGrass_{index:00}", worldPos, new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f), targetHeight, targetSpan, map.FoliageColor, true);
            }
            else if (roll < 0.85f)
            {
                var rockPath = PickRockScene(map, index);
                var targetHeight = rng.RandfRange(0.20f, 0.70f);
                var targetSpan = rng.RandfRange(0.40f, 1.00f);
                TryAddImportedScenery(rockPath, $"PatchWaterRock_{index:00}", new Vector3(worldPos.X, worldPos.Y - 0.04f, worldPos.Z), new Vector3(rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.1f, 0.1f)), targetHeight, targetSpan, map.RockColor, true);
            }
        }
        else
        {
            if (roll < 0.25f)
            {
                var scenePath = PickGrassScene(map, index);
                var targetHeight = rng.RandfRange(0.50f, 1.00f);
                var targetSpan = rng.RandfRange(0.70f, 1.30f);
                TryAddImportedScenery(scenePath, $"PatchGrass_{index:00}", worldPos, new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f), targetHeight, targetSpan, map.FoliageColor, true);
            }
        }
    }
}

