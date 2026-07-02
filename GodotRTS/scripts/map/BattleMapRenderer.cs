using Godot;
using System;

[Tool]
public partial class BattleMapRenderer : Node3D
{
    const float MapSize = BattleMapCatalog.MapHalfSize * 2f;
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
    [Export] public BattleMapResource? AuthoredMapResource { get; set; }
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
    string lastPreviewSignature = string.Empty;
    double livePreviewCooldown;
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

    public void Build(BattleMapDefinition map)
    {
        generatedRoot?.QueueFree();
        generatedRoot = new Node3D { Name = "GeneratedMap" };
        AddChild(generatedRoot);

        ApplyWorldEnvironment(map);
        AddGround(map);
        AddWater(map);
        AddStrips(map.Roads, map.RoadColor, RoadHeight, "Road");
        AddWaterColliders(map);
        AddPatches(map);
        AddRocks(map);
        AddTrees(map);
        AddGroundCover(map);
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
        var body = new StaticBody3D { Name = "MapCollision" };
        generatedRoot!.AddChild(body);

        var shape = new CollisionShape3D
        {
            Position = new Vector3(0, -0.5f, 0),
            Shape = new BoxShape3D { Size = new Vector3(MapSize, 1f, MapSize) }
        };
        body.AddChild(shape);

        var mesh = new MeshInstance3D
        {
            Name = "Ground",
            Position = new Vector3(0f, GroundHeight, 0f),
            Mesh = new PlaneMesh { Size = new Vector2(MapSize, MapSize) },
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
        if (riverLike && TryInstanceScene(PickWaterScene(map), $"WaterTerrain_{strip.Name}") is { } river)
        {
            river.Position = new Vector3(strip.Center.X, WaterModelHeight, strip.Center.Z);
            river.Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f);
            river.Scale = new Vector3(strip.Size.X, 1f, strip.Size.Y);
            generatedRoot!.AddChild(river);
        }

        var surface = new MeshInstance3D
        {
            Name = $"WaterSurface_{strip.Name}",
            Position = new Vector3(strip.Center.X, WaterSurfaceHeight, strip.Center.Z),
            Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f),
            Mesh = new PlaneMesh { Size = visualSize },
            MaterialOverride = CreateWaterMaterial(map, riverLike)
        };
        generatedRoot!.AddChild(surface);
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
            if (TryAddImportedScenery(
                PickTreeScene(map, i),
                $"Tree_{i:00}",
                new Vector3(pos.X, 0f, pos.Z),
                new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                targetHeight,
                targetHeight * 0.75f,
                map.FoliageColor,
                preserveMaterials: true))
            {
                continue;
            }

            var tree = new Node3D
            {
                Name = $"Tree_{i:00}",
                Position = new Vector3(pos.X, 0f, pos.Z),
                Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                Scale = Vector3.One * rng.RandfRange(1.1f, 1.8f)
            };
            generatedRoot!.AddChild(tree);

            var trunk = new MeshInstance3D
            {
                Name = "Trunk",
                Position = new Vector3(0f, 1.1f, 0f),
                Mesh = new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.38f, Height = 2.2f, RadialSegments = 6 },
                MaterialOverride = Material(map.TrunkColor, 0.8f)
            };
            tree.AddChild(trunk);

            var crown = new MeshInstance3D
            {
                Name = "Crown",
                Position = new Vector3(0f, 2.9f, 0f),
                Mesh = new SphereMesh { Radius = 1.15f, Height = 2.0f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = Material(map.FoliageColor, 0.92f)
            };
            tree.AddChild(crown);
        }
    }

    void AddGroundCover(BattleMapDefinition map)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(3, map.RandomSeed + 53);
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
                rng.RandfRange(-BattleMapCatalog.MapHalfSize + 18f, BattleMapCatalog.MapHalfSize - 18f),
                0f,
                rng.RandfRange(-BattleMapCatalog.MapHalfSize + 18f, BattleMapCatalog.MapHalfSize - 18f));

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
        var playerBase = FindPatchCenter(map, 2, new Vector3(-BattleMapCatalog.BaseSpawnOffset, 0f, -BattleMapCatalog.BaseSpawnOffset));
        var enemyBase = FindPatchCenter(map, 3, new Vector3(BattleMapCatalog.BaseSpawnOffset, 0f, BattleMapCatalog.BaseSpawnOffset));
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

        var playerBase = FindSpawn(map, "PlayerBase", new Vector3(-BattleMapCatalog.BaseSpawnOffset, 0f, -BattleMapCatalog.BaseSpawnOffset));
        var playerForward = FindSpawn(map, "PlayerForward", new Vector3(-BattleMapCatalog.ForwardSpawnOffset, 0f, -BattleMapCatalog.ForwardSpawnOffset));
        var focus = (playerBase * 0.62f + playerForward * 0.38f) + new Vector3(8f, 1.2f, 8f);
        camera.GlobalPosition = focus + new Vector3(0f, 34f, -44f);
        camera.LookAt(focus, Vector3.Up);
        camera.Fov = 42f;
    }

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
        if (AuthoredMapResource is not null)
            return AuthoredMapResource.ToDefinition();

        var preferredMap = string.IsNullOrWhiteSpace(PreviewMapName)
            ? null
            : PreviewMapName;
        var fallback = Engine.IsEditorHint()
            ? preferredMap
            : GameState.Instance?.SelectedMapName;
        var mapName = BattleMapCatalog.RequestedMapName(fallback);
        return BattleMapCatalog.Get(mapName);
    }

    static string BuildPreviewSignature(BattleMapDefinition map)
    {
        var signature = $"{map.Name}|{map.RandomSeed}|{map.TreePositions.Length}|{map.RockClusters.Length}|{map.SpawnPoints.Length}|{map.Roads.Length}|{map.Waters.Length}|{map.Patches.Length}";
        if (map.SpawnPoints.Length > 0)
        {
            var spawn = map.SpawnPoints[0];
            signature += $"|{spawn.Name}|{spawn.Position.X:F1}|{spawn.Position.Z:F1}|{spawn.Yaw:F1}";
        }
        if (map.Waters.Length > 0)
        {
            var water = map.Waters[0];
            signature += $"|{water.Center.X:F1}|{water.Center.Z:F1}|{water.Size.X:F1}|{water.Size.Y:F1}|{water.Angle:F1}";
        }
        return signature;
    }
}
