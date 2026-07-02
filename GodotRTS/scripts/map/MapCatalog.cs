using Godot;
using System;
using System.Collections.Generic;

public readonly record struct TerrainStripSpec(string Name, Vector3 Center, Vector2 Size, float Angle);
public readonly record struct TerrainPatchSpec(string Name, Vector3 Center, Vector2 Size, float Angle, int PaletteIndex);
public readonly record struct RuinWallSpec(Vector3 Center, float Angle, int Segments);
public readonly record struct SandbagRingSpec(Vector3 Center, float Radius, int Count, bool IsEnemy);
public readonly record struct SpawnPointSpec(string Name, Vector3 Position, float Yaw, bool IsPlayer, int TeamIndex);

public sealed class BattleMapDefinition
{
    public string Name { get; init; } = BattleMapCatalog.DefaultMapName;
    public string Description { get; init; } = "";
    public int RandomSeed { get; init; }
    public float MapHalfSize { get; set; } = BattleMapCatalog.DefaultMapHalfSize;
    public float BaseSpawnOffset { get; set; } = BattleMapCatalog.DefaultBaseSpawnOffset;
    public float ForwardSpawnOffset { get; set; } = BattleMapCatalog.DefaultForwardSpawnOffset;
    public bool IsGlobalConquest { get; set; }
    public float BuildRadius { get; set; } = 60f;
    public float GroundUnitSpeedMultiplier { get; set; } = 1f;
    public float AirUnitSpeedMultiplier { get; set; } = 1f;
    public float NavalUnitSpeedMultiplier { get; set; } = 1f;
    public float ProductionTimeMultiplier { get; set; } = 1f;
    public float AircraftFuelMultiplier { get; set; } = 1f;
    public int AirfieldCapacityPerBuilding { get; set; } = 4;
    public float AerialRefuelRadius { get; set; } = 26f;
    public float AerialRefuelRate { get; set; }

    public Color GroundColor { get; set; }
    public Color RoadColor { get; set; }
    public Color RoadEdgeColor { get; set; }
    public Color WaterColor { get; set; }
    public Color PatchAColor { get; set; }
    public Color PatchBColor { get; set; }
    public Color PlayerBasePadColor { get; set; }
    public Color EnemyBasePadColor { get; set; }
    public Color RockColor { get; set; }
    public Color FoliageColor { get; set; }
    public Color TrunkColor { get; set; }
    public Color WallColor { get; set; }
    public Color RuinColor { get; set; }
    public Color SkyColor { get; set; }
    public Color FogColor { get; set; }
    public Color AmbientSkyColor { get; set; }
    public Color AmbientEquatorColor { get; set; }
    public Color AmbientGroundColor { get; set; }
    public float FogStart { get; set; }
    public float FogEnd { get; set; }

    public Vector3[] RockClusters { get; set; } = Array.Empty<Vector3>();
    public Vector3[] TreePositions { get; set; } = Array.Empty<Vector3>();
    public SpawnPointSpec[] SpawnPoints { get; set; } = Array.Empty<SpawnPointSpec>();
    public RuinWallSpec[] RuinWalls { get; set; } = Array.Empty<RuinWallSpec>();
    public SandbagRingSpec[] SandbagRings { get; set; } = Array.Empty<SandbagRingSpec>();
    public TerrainStripSpec[] Roads { get; set; } = Array.Empty<TerrainStripSpec>();
    public TerrainStripSpec[] Waters { get; set; } = Array.Empty<TerrainStripSpec>();
    public TerrainPatchSpec[] Patches { get; set; } = Array.Empty<TerrainPatchSpec>();

    public Color GetPatchColor(int paletteIndex) => paletteIndex switch
    {
        1 => PatchBColor,
        2 => PlayerBasePadColor,
        3 => EnemyBasePadColor,
        _ => PatchAColor
    };
}

public static class BattleMapCatalog
{
    public const string DefaultMapName = "沙漠绿洲";
    public const string IceFortressName = "冰雪要塞";
    public const string JungleName = "丛林战场";
    public const string CityRuinsName = "城市废墟";
    public const string SeaChartName = "海图群岛";
    public const string GlobalConquestName = "全球争霸";
    public const float DefaultMapHalfSize = 200f;
    public const float DefaultBaseSpawnOffset = 140f;
    public const float DefaultForwardSpawnOffset = 98f;
    public const float MapHalfSize = DefaultMapHalfSize;
    public const float BaseSpawnOffset = DefaultBaseSpawnOffset;
    public const float ForwardSpawnOffset = DefaultForwardSpawnOffset;

    public static string[] GetPlayableMapNames() => new[]
    {
        DefaultMapName,
        IceFortressName,
        JungleName,
        CityRuinsName,
        SeaChartName
    };

    public static string[] GetAllMapNames() => new[]
    {
        DefaultMapName,
        IceFortressName,
        JungleName,
        CityRuinsName,
        SeaChartName,
        GlobalConquestName
    };

    public static BattleMapDefinition Get(string? mapName) => mapName switch
    {
        IceFortressName => CreateIceFortress(),
        JungleName => CreateJungle(),
        CityRuinsName => CreateCityRuins(),
        SeaChartName => CreateSeaChartIslands(),
        GlobalConquestName => CreateGlobalConquest(),
        _ => CreateSandOasis()
    };

    public static bool IsKnownMap(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return false;
        foreach (var name in GetAllMapNames())
        {
            if (string.Equals(name, mapName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static string RequestedMapName(string? fallback = null)
    {
        var args = CommandLineArgs.Get();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--map" && IsKnownMap(args[i + 1]))
                return args[i + 1];
        }

        return IsKnownMap(fallback) ? fallback! : DefaultMapName;
    }

    public static float GetMapHalfSize(BattleMapDefinition? map)
        => map?.MapHalfSize ?? DefaultMapHalfSize;

    public static Vector3 ClampToMap(Vector3 position, float margin = 0f)
        => ClampToMap(null, position, margin);

    public static Vector3 ClampToMap(BattleMapDefinition? map, Vector3 position, float margin = 0f)
    {
        var limit = Mathf.Max(1f, GetMapHalfSize(map) - margin);
        return new Vector3(
            Mathf.Clamp(position.X, -limit, limit),
            position.Y,
            Mathf.Clamp(position.Z, -limit, limit));
    }

    public static bool IsPointInWater(BattleMapDefinition map, Vector3 position, float padding = 0f)
    {
        foreach (var water in map.Waters)
        {
            if (IsPointInStrip(position, water, padding))
                return true;
        }

        return false;
    }

    public static float DistanceToWater(BattleMapDefinition map, Vector3 position)
    {
        if (map.Waters.Length == 0)
            return float.PositiveInfinity;

        var closest = ClosestWaterPoint(map, position);
        var flat = new Vector3(position.X - closest.X, 0f, position.Z - closest.Z);
        return flat.Length();
    }

    public static Vector3 ClosestWaterPoint(BattleMapDefinition map, Vector3 position, float inset = 1.5f)
    {
        var clamped = ClampToMap(map, position, 4f);
        if (map.Waters.Length == 0)
            return new Vector3(clamped.X, 0f, clamped.Z);

        var best = Vector3.Zero;
        var bestDistance = float.PositiveInfinity;
        foreach (var water in map.Waters)
        {
            var candidate = ClosestPointInStrip(clamped, water, inset);
            var distance = candidate.DistanceSquaredTo(new Vector3(clamped.X, 0f, clamped.Z));
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        return ClampToMap(map, best, 4f);
    }

    static bool IsPointInStrip(Vector3 position, TerrainStripSpec strip, float padding)
    {
        var local = (new Vector3(position.X, 0f, position.Z) - new Vector3(strip.Center.X, 0f, strip.Center.Z))
            .Rotated(Vector3.Up, -Mathf.DegToRad(strip.Angle));
        return Mathf.Abs(local.X) <= strip.Size.X * 0.5f + padding
            && Mathf.Abs(local.Z) <= strip.Size.Y * 0.5f + padding;
    }

    static Vector3 ClosestPointInStrip(Vector3 position, TerrainStripSpec strip, float inset)
    {
        var angle = Mathf.DegToRad(strip.Angle);
        var center = new Vector3(strip.Center.X, 0f, strip.Center.Z);
        var local = (new Vector3(position.X, 0f, position.Z) - center).Rotated(Vector3.Up, -angle);
        var halfX = Mathf.Max(0.5f, strip.Size.X * 0.5f - inset);
        var halfZ = Mathf.Max(0.5f, strip.Size.Y * 0.5f - inset);
        var clamped = new Vector3(
            Mathf.Clamp(local.X, -halfX, halfX),
            0f,
            Mathf.Clamp(local.Z, -halfZ, halfZ));
        return center + clamped.Rotated(Vector3.Up, angle);
    }

    static BattleMapDefinition CreateBase(
        string name,
        int seed,
        string description,
        float mapHalfSize = DefaultMapHalfSize,
        float baseSpawnOffset = DefaultBaseSpawnOffset,
        float forwardSpawnOffset = DefaultForwardSpawnOffset) => new()
    {
        Name = name,
        RandomSeed = seed,
        Description = description,
        MapHalfSize = mapHalfSize,
        BaseSpawnOffset = baseSpawnOffset,
        ForwardSpawnOffset = forwardSpawnOffset,
        RockClusters = new[]
        {
            new Vector3(-60, 0, 40), new Vector3(60, 0, -40),
            new Vector3(-100, 0, -60), new Vector3(100, 0, 60),
            new Vector3(0, 0, 100), new Vector3(0, 0, -100),
            new Vector3(-40, 0, 0), new Vector3(40, 0, 0),
            new Vector3(-140, 0, 60), new Vector3(140, 0, -60),
        },
        TreePositions = new[]
        {
            new Vector3(-160, 0, 160), new Vector3(160, 0, 160),
            new Vector3(-160, 0, -160), new Vector3(160, 0, -160),
            new Vector3(-120, 0, 0), new Vector3(120, 0, 0),
            new Vector3(0, 0, 140), new Vector3(0, 0, -140),
        },
        SpawnPoints = new[]
        {
            new SpawnPointSpec("PlayerBase", new Vector3(-baseSpawnOffset, 0f, -baseSpawnOffset), 45f, true, 0),
            new SpawnPointSpec("PlayerForward", new Vector3(-forwardSpawnOffset, 0f, -forwardSpawnOffset), 45f, true, 0),
            new SpawnPointSpec("EnemyBase", new Vector3(baseSpawnOffset, 0f, baseSpawnOffset), -135f, false, 1),
            new SpawnPointSpec("EnemyForward", new Vector3(forwardSpawnOffset, 0f, forwardSpawnOffset), -135f, false, 1),
        },
        RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3(10f, 0, 10f), 45f, 5),
            new RuinWallSpec(new Vector3(-10f, 0, -10f), 45f, 5),
            new RuinWallSpec(new Vector3(-50f, 0, 80f), 20f, 4),
            new RuinWallSpec(new Vector3(50f, 0, -80f), 20f, 4),
        },
        SandbagRings = new[]
        {
            new SandbagRingSpec(new Vector3(-baseSpawnOffset, 0f, -baseSpawnOffset), 22f, 8, false),
            new SandbagRingSpec(new Vector3(baseSpawnOffset, 0f, baseSpawnOffset), 22f, 8, true),
        },
        Roads = new[]
        {
            new TerrainStripSpec("MainAxis", new Vector3(0f, 0f, 0f), new Vector2(20f, 315f), 45f),
            new TerrainStripSpec("NorthFlank", new Vector3(-85f, 0f, 92f), new Vector2(13f, 135f), -30f),
            new TerrainStripSpec("SouthFlank", new Vector3(85f, 0f, -92f), new Vector2(13f, 135f), -30f),
            new TerrainStripSpec("MidCross", new Vector3(0f, 0f, 0f), new Vector2(12f, 145f), -45f),
        },
        Waters = new[]
        {
            new TerrainStripSpec("OasisNorth", new Vector3(-85f, 0f, 42f), new Vector2(16f, 74f), 72f),
            new TerrainStripSpec("OasisSouth", new Vector3(85f, 0f, -42f), new Vector2(16f, 74f), 72f),
        },
        Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-baseSpawnOffset, 0f, -baseSpawnOffset), new Vector2(82f, 82f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(baseSpawnOffset, 0f, baseSpawnOffset), new Vector2(82f, 82f), 0f, 3),
            new TerrainPatchSpec("CentralHardpoint", new Vector3(0f, 0f, 0f), new Vector2(60f, 52f), 45f, 1),
            new TerrainPatchSpec("NorthResourcePatch", new Vector3(-70f, 0f, 112f), new Vector2(64f, 34f), -18f, 0),
            new TerrainPatchSpec("SouthResourcePatch", new Vector3(70f, 0f, -112f), new Vector2(64f, 34f), -18f, 0),
        }
    };

    static BattleMapDefinition CreateSandOasis()
    {
        var map = CreateBase(DefaultMapName, 7601, "标准陆战战场，河道、林地与双基地推进线。");
        map.GroundColor = new Color(0.22f, 0.40f, 0.21f);
        map.RoadColor = new Color(0.24f, 0.29f, 0.23f);
        map.RoadEdgeColor = new Color(0.40f, 0.38f, 0.31f);
        map.WaterColor = new Color(0.02f, 0.24f, 0.48f);
        map.PatchAColor = new Color(0.20f, 0.38f, 0.20f);
        map.PatchBColor = new Color(0.25f, 0.42f, 0.24f);
        map.PlayerBasePadColor = new Color(0.31f, 0.39f, 0.33f);
        map.EnemyBasePadColor = new Color(0.33f, 0.37f, 0.31f);
        map.RockColor = new Color(0.42f, 0.43f, 0.40f);
        map.FoliageColor = new Color(0.12f, 0.46f, 0.16f);
        map.TrunkColor = new Color(0.24f, 0.15f, 0.08f);
        map.WallColor = new Color(0.30f, 0.36f, 0.30f);
        map.RuinColor = new Color(0.46f, 0.42f, 0.35f);
        map.SkyColor = new Color(0.56f, 0.71f, 0.78f);
        map.FogColor = new Color(0.58f, 0.67f, 0.64f);
        map.AmbientSkyColor = new Color(0.61f, 0.68f, 0.64f);
        map.AmbientEquatorColor = new Color(0.38f, 0.43f, 0.34f);
        map.AmbientGroundColor = new Color(0.19f, 0.21f, 0.17f);
        map.FogStart = 220f;
        map.FogEnd = 520f;
        map.Roads = new[]
        {
            new TerrainStripSpec("MainLane", new Vector3(0f, 0f, 0f), new Vector2(16f, 330f), 45f),
            new TerrainStripSpec("CrossLane", new Vector3(0f, 0f, 0f), new Vector2(12f, 210f), -45f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("WestOcean", new Vector3(-180f, 0f, 0f), new Vector2(64f, 400f), 0f),
            new TerrainStripSpec("EastOcean", new Vector3(180f, 0f, 0f), new Vector2(64f, 400f), 0f),
            new TerrainStripSpec("NorthBay", new Vector3(0f, 0f, 180f), new Vector2(238f, 44f), 0f),
            new TerrainStripSpec("SouthBay", new Vector3(0f, 0f, -180f), new Vector2(238f, 44f), 0f),
            new TerrainStripSpec("InlandRiverWest", new Vector3(-74f, 0f, 64f), new Vector2(18f, 132f), 66f),
            new TerrainStripSpec("InlandRiverEast", new Vector3(74f, 0f, -64f), new Vector2(18f, 132f), 66f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(82f, 82f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(82f, 82f), 0f, 3),
            new TerrainPatchSpec("CentralClearing", new Vector3(0f, 0f, 0f), new Vector2(70f, 60f), 45f, 1),
            new TerrainPatchSpec("WestForest", new Vector3(-118f, 0f, 34f), new Vector2(72f, 142f), -8f, 0),
            new TerrainPatchSpec("EastForest", new Vector3(118f, 0f, -34f), new Vector2(72f, 142f), -8f, 0),
            new TerrainPatchSpec("NorthForest", new Vector3(-18f, 0f, 132f), new Vector2(150f, 52f), 5f, 0),
            new TerrainPatchSpec("SouthForest", new Vector3(18f, 0f, -132f), new Vector2(150f, 52f), 5f, 0),
        };
        map.TreePositions = new[]
        {
            new Vector3(-146,0,78), new Vector3(-132,0,36), new Vector3(-122,0,-12), new Vector3(-104,0,64),
            new Vector3(146,0,-78), new Vector3(132,0,-36), new Vector3(122,0,12), new Vector3(104,0,-64),
            new Vector3(-70,0,142), new Vector3(-24,0,148), new Vector3(26,0,142), new Vector3(76,0,132),
            new Vector3(70,0,-142), new Vector3(24,0,-148), new Vector3(-26,0,-142), new Vector3(-76,0,-132),
        };
        map.RuinWalls = Array.Empty<RuinWallSpec>();
        map.SandbagRings = Array.Empty<SandbagRingSpec>();
        return map;
    }

    static BattleMapDefinition CreateIceFortress()
    {
        var map = CreateBase(IceFortressName, 8612, "雪地堡垒战场，视野收窄，中央通道更冷硬。");
        map.GroundColor = new Color(0.86f, 0.91f, 0.95f);
        map.RoadColor = new Color(0.62f, 0.70f, 0.76f);
        map.RoadEdgeColor = new Color(0.95f, 0.98f, 1.0f);
        map.WaterColor = new Color(0.30f, 0.62f, 0.82f);
        map.PatchAColor = new Color(0.76f, 0.86f, 0.90f);
        map.PatchBColor = new Color(0.58f, 0.68f, 0.75f);
        map.PlayerBasePadColor = new Color(0.58f, 0.67f, 0.75f);
        map.EnemyBasePadColor = new Color(0.62f, 0.58f, 0.62f);
        map.RockColor = new Color(0.58f, 0.64f, 0.70f);
        map.FoliageColor = new Color(0.72f, 0.86f, 0.76f);
        map.TrunkColor = new Color(0.30f, 0.28f, 0.25f);
        map.WallColor = new Color(0.64f, 0.70f, 0.73f);
        map.RuinColor = new Color(0.62f, 0.68f, 0.72f);
        map.SkyColor = new Color(0.55f, 0.70f, 0.85f);
        map.FogColor = new Color(0.80f, 0.88f, 0.95f);
        map.AmbientSkyColor = new Color(0.60f, 0.70f, 0.80f);
        map.AmbientEquatorColor = new Color(0.50f, 0.60f, 0.68f);
        map.AmbientGroundColor = new Color(0.40f, 0.45f, 0.50f);
        map.FogStart = 115f;
        map.FogEnd = 330f;
        map.Roads = new[]
        {
            new TerrainStripSpec("FrozenSpine", new Vector3(0f, 0f, 0f), new Vector2(18f, 330f), 45f),
            new TerrainStripSpec("WestRampart", new Vector3(-112f, 0f, 18f), new Vector2(12f, 150f), 8f),
            new TerrainStripSpec("EastRampart", new Vector3(112f, 0f, -18f), new Vector2(12f, 150f), 8f),
            new TerrainStripSpec("IceCrossing", new Vector3(0f, 0f, 0f), new Vector2(14f, 170f), -42f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("FrozenRiftNorth", new Vector3(-34f, 0f, 88f), new Vector2(10f, 118f), 78f),
            new TerrainStripSpec("FrozenRiftSouth", new Vector3(34f, 0f, -88f), new Vector2(10f, 118f), 78f),
        };
        return map;
    }

    static BattleMapDefinition CreateJungle()
    {
        var map = CreateBase(JungleName, 9144, "密林河道战场，遮蔽更多，颜色更深。");
        map.GroundColor = new Color(0.13f, 0.30f, 0.11f);
        map.RoadColor = new Color(0.20f, 0.18f, 0.12f);
        map.RoadEdgeColor = new Color(0.28f, 0.44f, 0.18f);
        map.WaterColor = new Color(0.06f, 0.35f, 0.30f);
        map.PatchAColor = new Color(0.10f, 0.40f, 0.13f);
        map.PatchBColor = new Color(0.14f, 0.24f, 0.10f);
        map.PlayerBasePadColor = new Color(0.18f, 0.28f, 0.17f);
        map.EnemyBasePadColor = new Color(0.26f, 0.17f, 0.14f);
        map.RockColor = new Color(0.30f, 0.36f, 0.28f);
        map.FoliageColor = new Color(0.08f, 0.46f, 0.13f);
        map.TrunkColor = new Color(0.22f, 0.13f, 0.08f);
        map.WallColor = new Color(0.28f, 0.34f, 0.25f);
        map.RuinColor = new Color(0.32f, 0.38f, 0.30f);
        map.SkyColor = new Color(0.10f, 0.20f, 0.10f);
        map.FogColor = new Color(0.18f, 0.30f, 0.18f);
        map.AmbientSkyColor = new Color(0.30f, 0.45f, 0.25f);
        map.AmbientEquatorColor = new Color(0.16f, 0.28f, 0.16f);
        map.AmbientGroundColor = new Color(0.10f, 0.15f, 0.08f);
        map.FogStart = 95f;
        map.FogEnd = 270f;
        map.Roads = new[]
        {
            new TerrainStripSpec("MuddyMain", new Vector3(0f, 0f, 0f), new Vector2(16f, 315f), 43f),
            new TerrainStripSpec("RiverBankWest", new Vector3(-88f, 0f, 76f), new Vector2(10f, 145f), -18f),
            new TerrainStripSpec("RiverBankEast", new Vector3(88f, 0f, -76f), new Vector2(10f, 145f), -18f),
            new TerrainStripSpec("CanopyCut", new Vector3(0f, 0f, 0f), new Vector2(11f, 130f), -58f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("JungleRiverA", new Vector3(-58f, 0f, 56f), new Vector2(18f, 150f), 64f),
            new TerrainStripSpec("JungleRiverB", new Vector3(58f, 0f, -56f), new Vector2(18f, 150f), 64f),
            new TerrainStripSpec("CentralMarsh", new Vector3(0f, 0f, 0f), new Vector2(24f, 58f), -28f),
        };
        map.TreePositions = new[]
        {
            new Vector3(-170,0,170), new Vector3(-145,0,138), new Vector3(-170,0,-170), new Vector3(-140,0,-130),
            new Vector3(170,0,170), new Vector3(142,0,128), new Vector3(170,0,-170), new Vector3(136,0,-138),
            new Vector3(-154,0,12), new Vector3(-132,0,62), new Vector3(154,0,-12), new Vector3(132,0,-62),
            new Vector3(-24,0,150), new Vector3(24,0,-150), new Vector3(-92,0,-18), new Vector3(92,0,18),
        };
        return map;
    }

    static BattleMapDefinition CreateCityRuins()
    {
        var map = CreateBase(CityRuinsName, 10220, "城市废墟战场，道路宽、掩体密、中心冲突更直接。");
        map.GroundColor = new Color(0.36f, 0.35f, 0.32f);
        map.RoadColor = new Color(0.24f, 0.24f, 0.23f);
        map.RoadEdgeColor = new Color(0.52f, 0.49f, 0.43f);
        map.WaterColor = new Color(0.15f, 0.25f, 0.28f);
        map.PatchAColor = new Color(0.29f, 0.28f, 0.26f);
        map.PatchBColor = new Color(0.42f, 0.39f, 0.33f);
        map.PlayerBasePadColor = new Color(0.28f, 0.31f, 0.36f);
        map.EnemyBasePadColor = new Color(0.36f, 0.28f, 0.27f);
        map.RockColor = new Color(0.42f, 0.40f, 0.37f);
        map.FoliageColor = new Color(0.16f, 0.28f, 0.16f);
        map.TrunkColor = new Color(0.20f, 0.17f, 0.13f);
        map.WallColor = new Color(0.35f, 0.35f, 0.34f);
        map.RuinColor = new Color(0.50f, 0.46f, 0.39f);
        map.SkyColor = new Color(0.30f, 0.28f, 0.25f);
        map.FogColor = new Color(0.42f, 0.40f, 0.38f);
        map.AmbientSkyColor = new Color(0.45f, 0.42f, 0.38f);
        map.AmbientEquatorColor = new Color(0.33f, 0.31f, 0.28f);
        map.AmbientGroundColor = new Color(0.18f, 0.17f, 0.15f);
        map.FogStart = 135f;
        map.FogEnd = 310f;
        map.Roads = new[]
        {
            new TerrainStripSpec("AvenueNWSE", new Vector3(0f, 0f, 0f), new Vector2(24f, 335f), 45f),
            new TerrainStripSpec("AvenueNESW", new Vector3(0f, 0f, 0f), new Vector2(22f, 320f), -45f),
            new TerrainStripSpec("NorthStreet", new Vector3(-84f, 0f, 92f), new Vector2(15f, 150f), 0f),
            new TerrainStripSpec("SouthStreet", new Vector3(84f, 0f, -92f), new Vector2(15f, 150f), 0f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("FloodedUnderpassN", new Vector3(-82f, 0f, 32f), new Vector2(13f, 76f), 88f),
            new TerrainStripSpec("FloodedUnderpassS", new Vector3(82f, 0f, -32f), new Vector2(13f, 76f), 88f),
        };
        map.RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3(18f,0f,18f), 45f, 6),
            new RuinWallSpec(new Vector3(-18f,0f,-18f), 45f, 6),
            new RuinWallSpec(new Vector3(-54f,0f,78f), 0f, 6),
            new RuinWallSpec(new Vector3(54f,0f,-78f), 0f, 6),
            new RuinWallSpec(new Vector3(88f,0f,34f), 90f, 5),
            new RuinWallSpec(new Vector3(-88f,0f,-34f), 90f, 5),
        };
        return map;
    }

    static BattleMapDefinition CreateGlobalConquest()
    {
        var map = CreateSandOasis();
        return new BattleMapDefinition
        {
            Name = GlobalConquestName,
            RandomSeed = 11550,
            Description = "全球争霸模式入口，沿用标准陆战地形并扩大远景感。",
            GroundColor = map.GroundColor,
            RoadColor = map.RoadColor,
            RoadEdgeColor = map.RoadEdgeColor,
            WaterColor = map.WaterColor,
            PatchAColor = map.PatchAColor,
            PatchBColor = map.PatchBColor,
            PlayerBasePadColor = map.PlayerBasePadColor,
            EnemyBasePadColor = map.EnemyBasePadColor,
            RockColor = map.RockColor,
            FoliageColor = map.FoliageColor,
            TrunkColor = map.TrunkColor,
            WallColor = map.WallColor,
            RuinColor = map.RuinColor,
            SkyColor = map.SkyColor,
            FogColor = map.FogColor,
            AmbientSkyColor = map.AmbientSkyColor,
            AmbientEquatorColor = map.AmbientEquatorColor,
            AmbientGroundColor = map.AmbientGroundColor,
            FogStart = 175f,
            FogEnd = 450f,
            RockClusters = map.RockClusters,
            TreePositions = map.TreePositions,
            SpawnPoints = map.SpawnPoints,
            RuinWalls = map.RuinWalls,
            SandbagRings = map.SandbagRings,
            Roads = map.Roads,
            Waters = map.Waters,
            Patches = map.Patches
        };
    }

    static BattleMapDefinition CreateSeaChartIslands()
    {
        var map = CreateBase(SeaChartName, 12077, "海岛和航线构成的海图战场，水域面积更大。");
        map.GroundColor = new Color(0.035f, 0.15f, 0.20f);
        map.RoadColor = new Color(0.78f, 0.70f, 0.45f);
        map.RoadEdgeColor = new Color(0.12f, 0.34f, 0.40f);
        map.WaterColor = new Color(0.06f, 0.48f, 0.58f);
        map.PatchAColor = new Color(0.76f, 0.64f, 0.36f);
        map.PatchBColor = new Color(0.24f, 0.45f, 0.24f);
        map.PlayerBasePadColor = new Color(0.58f, 0.50f, 0.31f);
        map.EnemyBasePadColor = new Color(0.54f, 0.38f, 0.31f);
        map.RockColor = new Color(0.62f, 0.54f, 0.40f);
        map.FoliageColor = new Color(0.10f, 0.46f, 0.22f);
        map.TrunkColor = new Color(0.36f, 0.22f, 0.10f);
        map.WallColor = new Color(0.18f, 0.30f, 0.34f);
        map.RuinColor = new Color(0.55f, 0.44f, 0.30f);
        map.SkyColor = new Color(0.24f, 0.50f, 0.64f);
        map.FogColor = new Color(0.38f, 0.62f, 0.70f);
        map.AmbientSkyColor = new Color(0.42f, 0.70f, 0.76f);
        map.AmbientEquatorColor = new Color(0.28f, 0.46f, 0.42f);
        map.AmbientGroundColor = new Color(0.08f, 0.16f, 0.18f);
        map.FogStart = 130f;
        map.FogEnd = 390f;
        map.Roads = new[]
        {
            new TerrainStripSpec("TradeRouteMain", new Vector3(0f, 0f, 0f), new Vector2(9f, 330f), 45f),
            new TerrainStripSpec("TradeRouteReturn", new Vector3(0f, 0f, 0f), new Vector2(7f, 260f), -45f),
            new TerrainStripSpec("NorthHarborLine", new Vector3(-62f, 0f, 104f), new Vector2(6f, 132f), 78f),
            new TerrainStripSpec("SouthHarborLine", new Vector3(62f, 0f, -104f), new Vector2(6f, 132f), 78f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("LagoonWest", new Vector3(-78f, 0f, 34f), new Vector2(28f, 112f), 70f),
            new TerrainStripSpec("LagoonEast", new Vector3(78f, 0f, -34f), new Vector2(28f, 112f), 70f),
            new TerrainStripSpec("ReefNorth", new Vector3(-28f, 0f, 118f), new Vector2(18f, 100f), -18f),
            new TerrainStripSpec("ReefSouth", new Vector3(28f, 0f, -118f), new Vector2(18f, 100f), -18f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerIsland", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(104f, 92f), -16f, 2),
            new TerrainPatchSpec("EnemyIsland", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(104f, 92f), -16f, 3),
            new TerrainPatchSpec("CompassAtoll", new Vector3(0f, 0f, 0f), new Vector2(78f, 68f), 45f, 1),
            new TerrainPatchSpec("NorthHarbor", new Vector3(-78f, 0f, 120f), new Vector2(76f, 40f), 12f, 0),
            new TerrainPatchSpec("SouthHarbor", new Vector3(78f, 0f, -120f), new Vector2(76f, 40f), 12f, 0),
            new TerrainPatchSpec("WestSandbar", new Vector3(-148f, 0f, 34f), new Vector2(54f, 28f), -22f, 0),
            new TerrainPatchSpec("EastSandbar", new Vector3(148f, 0f, -34f), new Vector2(54f, 28f), -22f, 0),
        };
        map.TreePositions = new[]
        {
            new Vector3(-154,0,-132), new Vector3(-128,0,-86), new Vector3(-92,0,-128), new Vector3(-148,0,-72),
            new Vector3(154,0,132), new Vector3(128,0,86), new Vector3(92,0,128), new Vector3(148,0,72),
            new Vector3(-92,0,132), new Vector3(92,0,-132), new Vector3(-154,0,48), new Vector3(154,0,-48),
        };
        return map;
    }
}
