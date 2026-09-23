using Godot;
using System;
using System.Collections.Generic;

public readonly record struct TerrainStripSpec(string Name, Vector3 Center, Vector2 Size, float Angle);
public readonly record struct TerrainPatchSpec(string Name, Vector3 Center, Vector2 Size, float Angle, int PaletteIndex);
public readonly record struct RuinWallSpec(Vector3 Center, float Angle, int Segments);
public readonly record struct SandbagRingSpec(Vector3 Center, float Radius, int Count, bool IsEnemy);
public readonly record struct SpawnPointSpec(string Name, Vector3 Position, float Yaw, bool IsPlayer, int TeamIndex);
public readonly record struct BridgeSpec(string Name, Vector3 Center, float Length, float Width, float Angle, string BridgeType);
public readonly record struct ElevatedPlateauSpec(string Name, Vector3 Center, Vector2 Size, float Height, float Angle, int StyleIndex);

public sealed class BattleMapDefinition
{
    public string Name { get; set; } = BattleMapCatalog.DefaultMapName;
    public string Description { get; set; } = "";
    public int RandomSeed { get; set; }
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
    public BridgeSpec[] Bridges { get; set; } = Array.Empty<BridgeSpec>();
    public ElevatedPlateauSpec[] ElevatedPlateaus { get; set; } = Array.Empty<ElevatedPlateauSpec>();

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
    public const string VolcanoFortressName = "火山要塞";
    public const string GobiWastelandName = "戈壁荒漠";
    public const string ArcticTundraName = "极地冰川";
    public const string TianshanMountainPassName = "天山高峡";
    public const string KunlunObsidianRidgeName = "昆仑黑山";
    public const string GreenValleyPeaksName = "绿谷雄峰";
    public const string GlobalConquestName = "全球争霸";
    public const float DefaultMapHalfSize = 200f;
    public const float DefaultBaseSpawnOffset = 140f;
    public const float DefaultForwardSpawnOffset = 98f;
    public const float GlobalConquestScale = 2.0f;
    public const float GlobalConquestBuildRadius = 160f;
    public const float MapHalfSize = DefaultMapHalfSize;
    public const float BaseSpawnOffset = DefaultBaseSpawnOffset;
    public const float ForwardSpawnOffset = DefaultForwardSpawnOffset;

    public static string[] GetPlayableMapNames() => new[]
    {
        DefaultMapName,
        IceFortressName,
        JungleName,
        CityRuinsName,
        SeaChartName,
        VolcanoFortressName,
        GobiWastelandName,
        ArcticTundraName,
        TianshanMountainPassName,
        KunlunObsidianRidgeName,
        GreenValleyPeaksName
    };

    public static string[] GetCustomRoomMapNames() => GetPlayableMapNames();

    public static string[] GetAllMapNames() => new[]
    {
        DefaultMapName,
        IceFortressName,
        JungleName,
        CityRuinsName,
        SeaChartName,
        VolcanoFortressName,
        GobiWastelandName,
        ArcticTundraName,
        TianshanMountainPassName,
        KunlunObsidianRidgeName,
        GreenValleyPeaksName,
        GlobalConquestName
    };

    public static BattleMapDefinition Get(string? mapName) => mapName switch
    {
        IceFortressName => CreateIceFortress(),
        JungleName => CreateJungle(),
        CityRuinsName => CreateCityRuins(),
        SeaChartName => CreateSeaChartIslands(),
        VolcanoFortressName => CreateVolcanoFortress(),
        GobiWastelandName => CreateGobiWasteland(),
        ArcticTundraName => CreateArcticTundra(),
        TianshanMountainPassName => CreateTianshanMountainPass(),
        KunlunObsidianRidgeName => CreateKunlunObsidianRidge(),
        GreenValleyPeaksName => CreateGreenValleyPeaks(),
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

    public static bool IsCustomRoomMap(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return false;

        foreach (var name in GetCustomRoomMapNames())
        {
            if (string.Equals(name, mapName, StringComparison.Ordinal))
                return true;
        }

        if (string.Equals(GlobalConquestName, mapName, StringComparison.Ordinal))
            return true;

        return false;
    }

    public static string NormalizeCustomRoomMap(string? mapName)
        => IsCustomRoomMap(mapName) ? mapName! : DefaultMapName;

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

    public static Vector3 ClosestLandPoint(BattleMapDefinition? map, Vector3 position, float padding = 1.5f)
    {
        var clamped = ClampToMap(map, position, 4f);
        if (map == null || map.Waters.Length == 0 || !IsPointInWater(map, clamped, 0f))
            return new Vector3(clamped.X, 0f, clamped.Z);

        var bestLandPoint = clamped;
        var minPushDistance = float.PositiveInfinity;

        foreach (var water in map.Waters)
        {
            if (!IsPointInStrip(clamped, water, 0f))
                continue;

            var rad = Mathf.DegToRad(water.Angle);
            var center = new Vector3(water.Center.X, 0f, water.Center.Z);
            var local = (new Vector3(clamped.X, 0f, clamped.Z) - center).Rotated(Vector3.Up, -rad);

            float halfW = water.Size.X * 0.5f + padding;
            float halfH = water.Size.Y * 0.5f + padding;

            float distToLeft = local.X - (-halfW);
            float distToRight = halfW - local.X;
            float distToBottom = local.Z - (-halfH);
            float distToTop = halfH - local.Z;

            float minD = Mathf.Min(Mathf.Min(distToLeft, distToRight), Mathf.Min(distToBottom, distToTop));

            Vector3 localOut = local;
            if (Mathf.IsEqualApprox(minD, distToLeft)) localOut.X = -halfW;
            else if (Mathf.IsEqualApprox(minD, distToRight)) localOut.X = halfW;
            else if (Mathf.IsEqualApprox(minD, distToBottom)) localOut.Z = -halfH;
            else if (Mathf.IsEqualApprox(minD, distToTop)) localOut.Z = halfH;

            var worldOut = center + localOut.Rotated(Vector3.Up, rad);
            var pushDist = (worldOut - clamped).LengthSquared();
            if (pushDist < minPushDistance)
            {
                minPushDistance = pushDist;
                bestLandPoint = worldOut;
            }
        }

        return ClampToMap(map, bestLandPoint, 4f);
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
        SpawnPoints = new[]
        {
            new SpawnPointSpec("PlayerBase", new Vector3(-baseSpawnOffset, 0f, -baseSpawnOffset), 45f, true, 0),
            new SpawnPointSpec("PlayerForward", new Vector3(-forwardSpawnOffset, 0f, -forwardSpawnOffset), 45f, true, 0),
            new SpawnPointSpec("EnemyBase", new Vector3(baseSpawnOffset, 0f, baseSpawnOffset), -135f, false, 1),
            new SpawnPointSpec("EnemyForward", new Vector3(forwardSpawnOffset, 0f, forwardSpawnOffset), -135f, false, 1),
        },
        SandbagRings = new[]
        {
            new SandbagRingSpec(new Vector3(-baseSpawnOffset, 0f, -baseSpawnOffset), 22f, 8, false),
            new SandbagRingSpec(new Vector3(baseSpawnOffset, 0f, baseSpawnOffset), 22f, 8, true),
        },
        RockClusters = Array.Empty<Vector3>(),
        TreePositions = Array.Empty<Vector3>(),
        RuinWalls = Array.Empty<RuinWallSpec>(),
        Roads = Array.Empty<TerrainStripSpec>(),
        Waters = Array.Empty<TerrainStripSpec>(),
        ElevatedPlateaus = Array.Empty<ElevatedPlateauSpec>(),
        Bridges = Array.Empty<BridgeSpec>(),
        Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-baseSpawnOffset, 0f, -baseSpawnOffset), new Vector2(82f, 82f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(baseSpawnOffset, 0f, baseSpawnOffset), new Vector2(82f, 82f), 0f, 3),
        }
    };

    static BattleMapDefinition CreateSandOasis()
    {
        var map = CreateBase(DefaultMapName, 7601, "标准陆战绿洲，中央绿洲湖泊、高台绿洲与石拱桥过河路。");
        map.GroundColor = new Color(0.85f, 0.72f, 0.47f);
        map.RoadColor = new Color(0.55f, 0.47f, 0.38f);
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
            new TerrainStripSpec("PlayerRoadApproach", new Vector3(-110f, 0f, -110f), new Vector2(16f, 90f), 45f),
            new TerrainStripSpec("EnemyRoadApproach", new Vector3(110f, 0f, 110f), new Vector2(16f, 90f), 45f),
        };
        map.Waters = new[]
        {
            // 贯穿地图中心呈“十字”相交的 3D 宽阔水系与大洋海峡 (Center 3D Water Crossroads / Water Junction!)
            new TerrainStripSpec("MainRiverChannel", new Vector3(0f, 0f, 0f), new Vector2(68f, 440f), -45f),
            new TerrainStripSpec("CrossRiverChannel", new Vector3(0f, 0f, 0f), new Vector2(48f, 260f), 45f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(82f, 82f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(82f, 82f), 0f, 3),
            new TerrainPatchSpec("WestForest", new Vector3(-125f, 0f, 40f), new Vector2(72f, 142f), -8f, 0),
            new TerrainPatchSpec("EastForest", new Vector3(125f, 0f, -40f), new Vector2(72f, 142f), -8f, 0),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("WestMountainCliff", new Vector3(-125f, 0f, 40f), new Vector2(55f, 120f), 4.5f, -45f, 0),
            new ElevatedPlateauSpec("EastMountainCliff", new Vector3(125f, 0f, -40f), new Vector2(55f, 120f), 4.5f, -45f, 0),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("CenterBridge", new Vector3(-45f, 0f, -45f), 76f, 18f, 135f, "stone"),
            new BridgeSpec("CenterBridge2", new Vector3(45f, 0f, 45f), 76f, 18f, 135f, "stone"),
            new BridgeSpec("NorthBridge", new Vector3(-85f, 0f, 85f), 76f, 18f, 45f, "stone"),
            new BridgeSpec("SouthBridge", new Vector3(85f, 0f, -85f), 76f, 18f, 45f, "stone"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-60, 0, 40), new Vector3(60, 0, -40),
            new Vector3(-100, 0, -60), new Vector3(100, 0, 60),
            new Vector3(0, 0, 100), new Vector3(0, 0, -100)
        };
        map.TreePositions = new[]
        {
            new Vector3(-146,0,78), new Vector3(-132,0,36), new Vector3(-122,0,-12), new Vector3(-104,0,64),
            new Vector3(146,0,-78), new Vector3(132,0,-36), new Vector3(122,0,12), new Vector3(104,0,-64),
            new Vector3(-70,0,142), new Vector3(-24,0,148), new Vector3(26,0,142), new Vector3(76,0,132),
        };
        return map;
    }

    static BattleMapDefinition CreateIceFortress()
    {
        var map = CreateBase(IceFortressName, 8612, "三路山谷雪地要塞，防守城墙关隘、悬崖高台与冰雪石桥。");
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
            new TerrainStripSpec("CenterSpine", new Vector3(0f, 0f, 0f), new Vector2(16f, 320f), 45f),
            new TerrainStripSpec("WestPass", new Vector3(-90f, 0f, 40f), new Vector2(14f, 180f), 15f),
            new TerrainStripSpec("EastPass", new Vector3(90f, 0f, -40f), new Vector2(14f, 180f), 15f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("MoatNorth", new Vector3(-40f, 0f, 75f), new Vector2(22f, 140f), 75f),
            new TerrainStripSpec("MoatSouth", new Vector3(40f, 0f, -75f), new Vector2(22f, 140f), 75f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("IceCenterField", new Vector3(0f, 0f, 0f), new Vector2(65f, 65f), 45f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("WestMountainCliff", new Vector3(-95f, 0f, 0f), new Vector2(45f, 170f), 6.0f, 0f, 2),
            new ElevatedPlateauSpec("EastMountainCliff", new Vector3(95f, 0f, 0f), new Vector2(45f, 170f), 6.0f, 0f, 2),
            new ElevatedPlateauSpec("CenterFortressDeck", new Vector3(0f, 0f, 0f), new Vector2(54f, 54f), 4.0f, 45f, 2),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("NorthMoatBridge", new Vector3(-40f, 0f, 75f), 38f, 16f, 165f, "stone"),
            new BridgeSpec("SouthMoatBridge", new Vector3(40f, 0f, -75f), 38f, 16f, 165f, "stone"),
        };
        map.RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3(-90f, 0f, -90f), 45f, 7),
            new RuinWallSpec(new Vector3(90f, 0f, 90f), 45f, 7),
            new RuinWallSpec(new Vector3(15f, 0f, -15f), -45f, 5),
        };
        map.RockClusters = new[]
        {
            new Vector3(-50, 0, 0), new Vector3(50, 0, 0),
            new Vector3(-110, 0, -20), new Vector3(110, 0, 20)
        };
        return map;
    }

    static BattleMapDefinition CreateJungle()
    {
        var map = CreateBase(JungleName, 9144, "蛇形 S 弯深林大河、悬崖制高点与 3 座雨林木桥。");
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
            new TerrainStripSpec("MuddyMain", new Vector3(0f, 0f, 0f), new Vector2(16f, 315f), 35f),
            new TerrainStripSpec("CanopyCut", new Vector3(0f, 0f, 0f), new Vector2(12f, 180f), -55f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("RiverSegmentNW", new Vector3(-100f, 0f, 100f), new Vector2(32f, 160f), 20f),
            new TerrainStripSpec("RiverSegmentCenter", new Vector3(0f, 0f, 0f), new Vector2(38f, 140f), 70f),
            new TerrainStripSpec("RiverSegmentSE", new Vector3(100f, 0f, -100f), new Vector2(32f, 160f), 20f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("FordIsland", new Vector3(0f, 0f, 0f), new Vector2(50f, 50f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("JungleWestOverlook", new Vector3(-105f, 0f, -25f), new Vector2(65f, 55f), 4.5f, 0f, 0),
            new ElevatedPlateauSpec("JungleEastOverlook", new Vector3(105f, 0f, 25f), new Vector2(65f, 55f), 4.5f, 0f, 0),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("JungleBridgeNW", new Vector3(-70f, 0f, 70f), 44f, 14f, 110f, "wood"),
            new BridgeSpec("JungleBridgeCenter", new Vector3(0f, 0f, 0f), 48f, 16f, 160f, "wood"),
            new BridgeSpec("JungleBridgeSE", new Vector3(70f, 0f, -70f), 44f, 14f, 110f, "wood"),
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
        var map = CreateBase(CityRuinsName, 10220, "城市废墟高台网格，高架广场高台、立交桥与钢筋桥梁。");
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
            new TerrainStripSpec("BroadwayAvenue", new Vector3(0f, 0f, 0f), new Vector2(26f, 360f), 90f),
            new TerrainStripSpec("5thAvenue", new Vector3(0f, 0f, 0f), new Vector2(26f, 360f), 0f),
            new TerrainStripSpec("DiagonalPass", new Vector3(0f, 0f, 0f), new Vector2(18f, 300f), 45f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("FloodedTunnelNorth", new Vector3(-75f, 0f, 75f), new Vector2(22f, 110f), 45f),
            new TerrainStripSpec("FloodedTunnelSouth", new Vector3(75f, 0f, -75f), new Vector2(22f, 110f), 45f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("CityPlazaCenter", new Vector3(0f, 0f, 0f), new Vector2(75f, 75f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("CityPlazaDeck", new Vector3(0f, 0f, 0f), new Vector2(75f, 75f), 4.5f, 0f, 4),
            new ElevatedPlateauSpec("ElevatedHighwayNorth", new Vector3(-85f, 0f, 45f), new Vector2(45f, 95f), 5.5f, 0f, 4),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("UnderpassBridgeNorth", new Vector3(-75f, 0f, 75f), 40f, 18f, 135f, "steel"),
            new BridgeSpec("UnderpassBridgeSouth", new Vector3(75f, 0f, -75f), 40f, 18f, 135f, "steel"),
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
        map.RockClusters = new[]
        {
            new Vector3(-40, 0, 40), new Vector3(40, 0, -40),
            new Vector3(-90, 0, 0), new Vector3(90, 0, 0)
        };
        return map;
    }

    static Vector3 ScaleFlat(Vector3 value, float scale)
        => new(value.X * scale, value.Y, value.Z * scale);

    static Vector2 ScaleSize(Vector2 value, float scale)
        => new(value.X * scale, value.Y * scale);

    static BattleMapDefinition CreateGlobalConquest()
    {
        var map = CreateSandOasis();
        var scale = GlobalConquestScale;
        map.Name = GlobalConquestName;
        map.RandomSeed = 11550;
        map.Description = "全球争霸宏大战场，具备更宽广的正面战线与推进路线。";
        map.IsGlobalConquest = true;
        map.MapHalfSize = DefaultMapHalfSize * scale;
        map.BaseSpawnOffset = DefaultBaseSpawnOffset * scale;
        map.ForwardSpawnOffset = DefaultForwardSpawnOffset * scale;
        map.BuildRadius = GlobalConquestBuildRadius;
        map.FogStart = 260f;
        map.FogEnd = 620f;
        map.RockClusters = Array.ConvertAll(map.RockClusters, position => ScaleFlat(position, scale));
        map.TreePositions = Array.ConvertAll(map.TreePositions, position => ScaleFlat(position, scale));
        map.SpawnPoints = Array.ConvertAll(map.SpawnPoints, spawn => spawn with
        {
            Position = ScaleFlat(spawn.Position, scale)
        });
        map.RuinWalls = Array.ConvertAll(map.RuinWalls, wall => wall with
        {
            Center = ScaleFlat(wall.Center, scale)
        });
        map.SandbagRings = Array.ConvertAll(map.SandbagRings, ring => new SandbagRingSpec(
            ScaleFlat(ring.Center, scale),
            ring.Radius * scale,
            ring.Count,
            ring.IsEnemy));
        map.Roads = Array.ConvertAll(map.Roads, road => road with
        {
            Center = ScaleFlat(road.Center, scale),
            Size = ScaleSize(road.Size, scale)
        });
        map.Waters = Array.ConvertAll(map.Waters, water => water with
        {
            Center = ScaleFlat(water.Center, scale),
            Size = ScaleSize(water.Size, scale)
        });
        map.Patches = Array.ConvertAll(map.Patches, patch => patch with
        {
            Center = ScaleFlat(patch.Center, scale),
            Size = ScaleSize(patch.Size, scale)
        });
        map.ElevatedPlateaus = Array.ConvertAll(map.ElevatedPlateaus, plateau => plateau with
        {
            Center = ScaleFlat(plateau.Center, scale),
            Size = ScaleSize(plateau.Size, scale)
        });
        map.Bridges = Array.ConvertAll(map.Bridges, bridge => bridge with
        {
            Center = ScaleFlat(bridge.Center, scale),
            Length = bridge.Length * scale,
            Width = bridge.Width * scale
        });
        return map;
#if false
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
#endif
    }

    static BattleMapDefinition CreateSeaChartIslands()
    {
        var map = CreateBase(SeaChartName, 12077, "海岛群岛战场，两座绿洲大洲、中央海槽大洋与跨海悬索栈桥。");
        map.GroundColor = new Color(0.28f, 0.54f, 0.24f); // 鲜艳绿色群岛大陆草地！
        map.RoadColor = new Color(0.78f, 0.70f, 0.45f);
        map.RoadEdgeColor = new Color(0.12f, 0.34f, 0.40f);
        map.WaterColor = new Color(0.04f, 0.88f, 0.28f, 0.95f); // 100% 浓郁竹青/翠绿色水体 (Deep Bamboo Emerald Green)!
        map.PatchAColor = new Color(0.76f, 0.64f, 0.36f);
        map.PatchBColor = new Color(0.32f, 0.58f, 0.28f);
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
        };
        map.Waters = new[]
        {
            // 贯穿左右两块群岛大洲的大洋海峡 (Wide 120m ocean channel running through map center!)
            new TerrainStripSpec("CentralOceanChannel", new Vector3(0f, 0f, 0f), new Vector2(120f, 400f), -45f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerIsland", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(72f, 72f), 0f, 2),
            new TerrainPatchSpec("EnemyIsland", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(72f, 72f), 0f, 3),
            new TerrainPatchSpec("NorthIsland", new Vector3(-80f, 0f, 110f), new Vector2(70f, 45f), 0f, 0),
            new TerrainPatchSpec("SouthIsland", new Vector3(80f, 0f, -110f), new Vector2(70f, 45f), 0f, 0),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("PlayerIslandHill", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(65f, 65f), 4.0f, 0f, 0),
            new ElevatedPlateauSpec("EnemyIslandHill", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(65f, 65f), 4.0f, 0f, 0),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("MainSeaBridgeNW", new Vector3(-45f, 0f, -45f), 108f, 18f, 135f, "steel"),
            new BridgeSpec("MainSeaBridgeSE", new Vector3(45f, 0f, 45f), 108f, 18f, 135f, "steel"),
            new BridgeSpec("NorthHarborBridge", new Vector3(-60f, 0f, 95f), 36f, 14f, 0f, "wood"),
            new BridgeSpec("SouthHarborBridge", new Vector3(60f, 0f, -95f), 36f, 14f, 0f, "wood"),
        };
        map.TreePositions = new[]
        {
            new Vector3(-154,0,-132), new Vector3(-128,0,-86), new Vector3(-92,0,-128), new Vector3(-148,0,-72),
            new Vector3(154,0,132), new Vector3(128,0,86), new Vector3(92,0,128), new Vector3(148,0,72),
        };
        return map;
    }

    static BattleMapDefinition CreateVolcanoFortress()
    {
        var map = CreateBase(VolcanoFortressName, 13390, "火山口环形要塞，黑曜石环形悬崖高台与 3 座熔岩铁拱桥。");
        map.GroundColor = new Color(0.18f, 0.16f, 0.16f);
        map.RoadColor = new Color(0.28f, 0.22f, 0.20f);
        map.RoadEdgeColor = new Color(0.48f, 0.20f, 0.14f);
        map.WaterColor = new Color(0.85f, 0.22f, 0.05f);
        map.PatchAColor = new Color(0.24f, 0.20f, 0.18f);
        map.PatchBColor = new Color(0.35f, 0.18f, 0.14f);
        map.PlayerBasePadColor = new Color(0.32f, 0.28f, 0.26f);
        map.EnemyBasePadColor = new Color(0.38f, 0.22f, 0.20f);
        map.RockColor = new Color(0.24f, 0.20f, 0.20f);
        map.FoliageColor = new Color(0.38f, 0.18f, 0.12f);
        map.TrunkColor = new Color(0.18f, 0.12f, 0.10f);
        map.WallColor = new Color(0.26f, 0.22f, 0.22f);
        map.RuinColor = new Color(0.36f, 0.26f, 0.22f);
        map.SkyColor = new Color(0.35f, 0.12f, 0.10f);
        map.FogColor = new Color(0.48f, 0.18f, 0.12f);
        map.AmbientSkyColor = new Color(0.42f, 0.18f, 0.14f);
        map.AmbientEquatorColor = new Color(0.30f, 0.14f, 0.12f);
        map.AmbientGroundColor = new Color(0.16f, 0.08f, 0.06f);
        map.FogStart = 110f;
        map.FogEnd = 320f;
        map.Roads = new[]
        {
            new TerrainStripSpec("BasaltBridgeNWSE", new Vector3(0f, 0f, 0f), new Vector2(20f, 320f), 45f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("LavaRingNorth", new Vector3(0f, 0f, 70f), new Vector2(220f, 24f), 0f),
            new TerrainStripSpec("LavaRingSouth", new Vector3(0f, 0f, -70f), new Vector2(220f, 24f), 0f),
            new TerrainStripSpec("MagmaRiftCenter", new Vector3(0f, 0f, 0f), new Vector2(28f, 160f), 90f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("VolcanicCraterRim", new Vector3(0f, 0f, 0f), new Vector2(70f, 70f), 45f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("VolcanicRimPlateau", new Vector3(0f, 0f, 0f), new Vector2(75f, 75f), 5.5f, 45f, 3),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("LavaBridgeNorth", new Vector3(0f, 0f, 70f), 40f, 18f, 90f, "basalt"),
            new BridgeSpec("LavaBridgeSouth", new Vector3(0f, 0f, -70f), 40f, 18f, 90f, "basalt"),
            new BridgeSpec("CalderaBridgeCenter", new Vector3(0f, 0f, 0f), 42f, 18f, 0f, "basalt"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-40, 0, 30), new Vector3(40, 0, -30),
            new Vector3(-90, 0, -40), new Vector3(90, 0, 40)
        };
        return map;
    }

    static BattleMapDefinition CreateGobiWasteland()
    {
        var map = CreateBase(GobiWastelandName, 14205, "双峡谷通道战场，中央高耸红砂岩绝壁高台与峡谷悬索石桥。");
        map.GroundColor = new Color(0.68f, 0.44f, 0.30f);
        map.RoadColor = new Color(0.48f, 0.35f, 0.24f);
        map.RoadEdgeColor = new Color(0.60f, 0.40f, 0.28f);
        map.WaterColor = new Color(0.20f, 0.35f, 0.42f);
        map.PatchAColor = new Color(0.55f, 0.38f, 0.26f);
        map.PatchBColor = new Color(0.72f, 0.52f, 0.36f);
        map.PlayerBasePadColor = new Color(0.50f, 0.42f, 0.34f);
        map.EnemyBasePadColor = new Color(0.54f, 0.38f, 0.32f);
        map.RockColor = new Color(0.58f, 0.38f, 0.26f);
        map.FoliageColor = new Color(0.42f, 0.48f, 0.24f);
        map.TrunkColor = new Color(0.32f, 0.22f, 0.14f);
        map.WallColor = new Color(0.52f, 0.40f, 0.30f);
        map.RuinColor = new Color(0.60f, 0.45f, 0.34f);
        map.SkyColor = new Color(0.78f, 0.52f, 0.32f);
        map.FogColor = new Color(0.70f, 0.55f, 0.40f);
        map.AmbientSkyColor = new Color(0.72f, 0.54f, 0.38f);
        map.AmbientEquatorColor = new Color(0.50f, 0.38f, 0.28f);
        map.AmbientGroundColor = new Color(0.28f, 0.20f, 0.14f);
        map.FogStart = 140f;
        map.FogEnd = 360f;
        map.Roads = new[]
        {
            new TerrainStripSpec("WestGorgeRoad", new Vector3(-80f, 0f, 0f), new Vector2(22f, 340f), 0f),
            new TerrainStripSpec("EastGorgeRoad", new Vector3(80f, 0f, 0f), new Vector2(22f, 340f), 0f),
            new TerrainStripSpec("CrossRavine", new Vector3(0f, 0f, 0f), new Vector2(14f, 200f), 90f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("DryRiverWash", new Vector3(0f, 0f, 0f), new Vector2(20f, 280f), 90f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("CentralSandstoneMesa", new Vector3(0f, 0f, 0f), new Vector2(90f, 240f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("CentralSandstoneMesa", new Vector3(0f, 0f, 0f), new Vector2(90f, 240f), 7.0f, 0f, 1),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("WestGorgeBridge", new Vector3(-80f, 0f, 0f), 38f, 18f, 0f, "stone"),
            new BridgeSpec("EastGorgeBridge", new Vector3(80f, 0f, 0f), 38f, 18f, 0f, "stone"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-40, 0, -80), new Vector3(-40, 0, 80),
            new Vector3(40, 0, -80), new Vector3(40, 0, 80)
        };
        return map;
    }

    static BattleMapDefinition CreateArcticTundra()
    {
        var map = CreateBase(ArcticTundraName, 15880, "极地冰川巨裂谷战场，高耸冰川高台与 2 座跨峡谷冰川桁架大桥。");
        map.GroundColor = new Color(0.92f, 0.96f, 0.98f);
        map.RoadColor = new Color(0.75f, 0.85f, 0.92f);
        map.RoadEdgeColor = new Color(0.88f, 0.94f, 0.98f);
        map.WaterColor = new Color(0.08f, 0.48f, 0.68f);
        map.PatchAColor = new Color(0.82f, 0.90f, 0.95f);
        map.PatchBColor = new Color(0.68f, 0.82f, 0.90f);
        map.PlayerBasePadColor = new Color(0.72f, 0.82f, 0.88f);
        map.EnemyBasePadColor = new Color(0.78f, 0.76f, 0.84f);
        map.RockColor = new Color(0.65f, 0.75f, 0.85f);
        map.FoliageColor = new Color(0.80f, 0.92f, 0.88f);
        map.TrunkColor = new Color(0.28f, 0.26f, 0.25f);
        map.WallColor = new Color(0.70f, 0.80f, 0.86f);
        map.RuinColor = new Color(0.72f, 0.80f, 0.85f);
        map.SkyColor = new Color(0.55f, 0.78f, 0.92f);
        map.FogColor = new Color(0.82f, 0.92f, 0.98f);
        map.AmbientSkyColor = new Color(0.65f, 0.82f, 0.92f);
        map.AmbientEquatorColor = new Color(0.50f, 0.68f, 0.80f);
        map.AmbientGroundColor = new Color(0.38f, 0.48f, 0.58f);
        map.FogStart = 150f;
        map.FogEnd = 380f;
        map.Roads = new[]
        {
            new TerrainStripSpec("WestIceBridge", new Vector3(-75f, 0f, 75f), new Vector2(16f, 70f), 45f),
            new TerrainStripSpec("EastIceBridge", new Vector3(75f, 0f, -75f), new Vector2(16f, 70f), 45f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("GlacialFjordRift", new Vector3(0f, 0f, 0f), new Vector2(350f, 34f), -45f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("NorthGlacialSheet", new Vector3(-60f, 0f, -60f), new Vector2(120f, 120f), 0f, 1),
            new TerrainPatchSpec("SouthGlacialSheet", new Vector3(60f, 0f, 60f), new Vector2(120f, 120f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("NorthIceDeck", new Vector3(-65f, 0f, -65f), new Vector2(110f, 110f), 4.5f, 0f, 2),
            new ElevatedPlateauSpec("SouthIceDeck", new Vector3(65f, 0f, 65f), new Vector2(110f, 110f), 4.5f, 0f, 2),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("WestGlacierBridge", new Vector3(-75f, 0f, 75f), 52f, 18f, 45f, "ice"),
            new BridgeSpec("EastGlacierBridge", new Vector3(75f, 0f, -75f), 52f, 18f, 45f, "ice"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-40, 0, 40), new Vector3(40, 0, -40),
            new Vector3(-100, 0, -80), new Vector3(100, 0, 80)
        };
        map.TreePositions = new[]
        {
            new Vector3(-130, 0, -100), new Vector3(-100, 0, -130), new Vector3(-150, 0, -60),
            new Vector3(130, 0, 100), new Vector3(100, 0, 130), new Vector3(150, 0, 60)
        };
        return map;
    }

    static BattleMapDefinition CreateTianshanMountainPass()
    {
        var map = CreateBase(TianshanMountainPassName, 18920, "天山雄伟山脉高峡战场，高耸入云的连绵巨石山脊与峡谷关隘，设有 2 座高空跨峡石桥与高山要道。");
        map.GroundColor = new Color(0.68f, 0.58f, 0.42f);
        map.RoadColor = new Color(0.78f, 0.70f, 0.55f);
        map.RoadEdgeColor = new Color(0.62f, 0.52f, 0.38f);
        map.WaterColor = new Color(0.12f, 0.48f, 0.65f);
        map.PatchAColor = new Color(0.58f, 0.48f, 0.35f);
        map.PatchBColor = new Color(0.75f, 0.65f, 0.48f);
        map.PlayerBasePadColor = new Color(0.62f, 0.54f, 0.40f);
        map.EnemyBasePadColor = new Color(0.65f, 0.50f, 0.38f);
        map.RockColor = new Color(0.52f, 0.46f, 0.38f);
        map.FoliageColor = new Color(0.24f, 0.48f, 0.30f);
        map.TrunkColor = new Color(0.35f, 0.28f, 0.22f);
        map.WallColor = new Color(0.60f, 0.52f, 0.42f);
        map.RuinColor = new Color(0.58f, 0.50f, 0.40f);
        map.SkyColor = new Color(0.55f, 0.72f, 0.85f);
        map.FogColor = new Color(0.85f, 0.82f, 0.76f);
        map.AmbientSkyColor = new Color(0.88f, 0.82f, 0.72f);
        map.AmbientEquatorColor = new Color(0.70f, 0.62f, 0.52f);
        map.AmbientGroundColor = new Color(0.48f, 0.40f, 0.30f);
        map.FogStart = 160f;
        map.FogEnd = 400f;

        map.Roads = new[]
        {
            new TerrainStripSpec("NorthMountainRoad", new Vector3(-65f, 0f, 0f), new Vector2(16f, 180f), 0f),
            new TerrainStripSpec("SouthMountainRoad", new Vector3(65f, 0f, 0f), new Vector2(16f, 180f), 0f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("TianshanGorgeRiver", new Vector3(0f, 0f, 0f), new Vector2(24f, 320f), 90f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("WestMountainRidge", new Vector3(-90f, 0f, 0f), new Vector2(80f, 180f), 0f, 1),
            new TerrainPatchSpec("EastMountainRidge", new Vector3(90f, 0f, 0f), new Vector2(80f, 180f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("CentralTianshanRidge", new Vector3(0f, 0f, 0f), new Vector2(75f, 250f), 9.5f, 0f, 0),
            new ElevatedPlateauSpec("WestMountainPeak", new Vector3(-120f, 0f, 40f), new Vector2(90f, 120f), 13.0f, 15f, 0),
            new ElevatedPlateauSpec("EastMountainPeak", new Vector3(120f, 0f, -40f), new Vector2(90f, 120f), 13.0f, 15f, 0),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("NorthPassBridge", new Vector3(-65f, 0f, 0f), 42f, 18f, 90f, "stone"),
            new BridgeSpec("SouthPassBridge", new Vector3(65f, 0f, 0f), 42f, 18f, 90f, "stone"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-45, 0, -60), new Vector3(-45, 0, 60),
            new Vector3(45, 0, -60), new Vector3(45, 0, 60),
            new Vector3(-110, 0, -90), new Vector3(110, 0, 90)
        };
        map.TreePositions = new[]
        {
            new Vector3(-120, 0, -70), new Vector3(-100, 0, -110),
            new Vector3(120, 0, 70), new Vector3(100, 0, 110)
        };
        return map;
    }

    static BattleMapDefinition CreateKunlunObsidianRidge()
    {
        var map = CreateBase(KunlunObsidianRidgeName, 21450, "昆仑黑曜石黑山要塞战场，险峻黑山绝壁关隘与黑曜石高台要塞，险要的黑山峡谷通道。");
        map.GroundColor = new Color(0.38f, 0.32f, 0.26f);
        map.RoadColor = new Color(0.52f, 0.44f, 0.36f);
        map.RoadEdgeColor = new Color(0.35f, 0.28f, 0.22f);
        map.WaterColor = new Color(0.10f, 0.32f, 0.45f);
        map.PatchAColor = new Color(0.32f, 0.26f, 0.20f);
        map.PatchBColor = new Color(0.45f, 0.38f, 0.30f);
        map.PlayerBasePadColor = new Color(0.40f, 0.35f, 0.30f);
        map.EnemyBasePadColor = new Color(0.42f, 0.28f, 0.24f);
        map.RockColor = new Color(0.28f, 0.24f, 0.20f);
        map.FoliageColor = new Color(0.25f, 0.38f, 0.25f);
        map.TrunkColor = new Color(0.22f, 0.18f, 0.15f);
        map.WallColor = new Color(0.42f, 0.36f, 0.30f);
        map.RuinColor = new Color(0.38f, 0.32f, 0.26f);
        map.SkyColor = new Color(0.62f, 0.68f, 0.75f);
        map.FogColor = new Color(0.68f, 0.64f, 0.58f);
        map.AmbientSkyColor = new Color(0.75f, 0.70f, 0.62f);
        map.AmbientEquatorColor = new Color(0.58f, 0.52f, 0.44f);
        map.AmbientGroundColor = new Color(0.35f, 0.28f, 0.22f);
        map.FogStart = 140f;
        map.FogEnd = 360f;

        map.Roads = new[]
        {
            new TerrainStripSpec("KunlunPassRoad", new Vector3(0f, 0f, 0f), new Vector2(18f, 240f), 45f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("DarkMountainRiver", new Vector3(0f, 0f, 0f), new Vector2(22f, 300f), -45f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("WestKunlunObsidian", new Vector3(-70f, 0f, 70f), new Vector2(110f, 110f), 0f, 1),
            new TerrainPatchSpec("EastKunlunObsidian", new Vector3(70f, 0f, -70f), new Vector2(110f, 110f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("WestKunlunPeak", new Vector3(-75f, 0f, 75f), new Vector2(110f, 180f), 11.5f, -45f, 3),
            new ElevatedPlateauSpec("EastKunlunPeak", new Vector3(75f, 0f, -75f), new Vector2(110f, 180f), 11.5f, -45f, 3),
            new ElevatedPlateauSpec("CentralValleyMesa", new Vector3(0f, 0f, 0f), new Vector2(80f, 80f), 7.0f, 0f, 3),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("KunlunSteelBridge", new Vector3(0f, 0f, 0f), 48f, 18f, -45f, "steel"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-40, 0, -40), new Vector3(40, 0, 40),
            new Vector3(-120, 0, 30), new Vector3(120, 0, -30)
        };
        return map;
    }

    static BattleMapDefinition CreateGreenValleyPeaks()
    {
        var map = CreateBase(GreenValleyPeaksName, 26840, "高山绿谷与阿尔卑斯山脉盆地，四周高耸绿山连绵屏障，中央绿谷河流与山顶高台要塞。");
        map.GroundColor = new Color(0.28f, 0.55f, 0.22f);
        map.RoadColor = new Color(0.65f, 0.55f, 0.38f);
        map.RoadEdgeColor = new Color(0.38f, 0.48f, 0.25f);
        map.WaterColor = new Color(0.12f, 0.55f, 0.78f);
        map.PatchAColor = new Color(0.22f, 0.48f, 0.18f);
        map.PatchBColor = new Color(0.42f, 0.62f, 0.28f);
        map.PlayerBasePadColor = new Color(0.32f, 0.50f, 0.25f);
        map.EnemyBasePadColor = new Color(0.48f, 0.45f, 0.28f);
        map.RockColor = new Color(0.55f, 0.52f, 0.48f);
        map.FoliageColor = new Color(0.18f, 0.52f, 0.22f);
        map.TrunkColor = new Color(0.35f, 0.28f, 0.20f);
        map.WallColor = new Color(0.58f, 0.54f, 0.48f);
        map.RuinColor = new Color(0.55f, 0.50f, 0.45f);
        map.SkyColor = new Color(0.45f, 0.75f, 0.95f);
        map.FogColor = new Color(0.82f, 0.90f, 0.85f);
        map.AmbientSkyColor = new Color(0.82f, 0.88f, 0.78f);
        map.AmbientEquatorColor = new Color(0.55f, 0.68f, 0.48f);
        map.AmbientGroundColor = new Color(0.28f, 0.42f, 0.20f);
        map.FogStart = 180f;
        map.FogEnd = 420f;

        map.Roads = new[]
        {
            new TerrainStripSpec("NorthValleyPass", new Vector3(-50f, 0f, -50f), new Vector2(16f, 160f), -30f),
            new TerrainStripSpec("SouthValleyPass", new Vector3(50f, 0f, 50f), new Vector2(16f, 160f), -30f),
        };
        map.Waters = new[]
        {
            new TerrainStripSpec("GreenValleyRiver", new Vector3(0f, 0f, 0f), new Vector2(22f, 290f), -30f),
        };
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-BaseSpawnOffset, 0f, -BaseSpawnOffset), new Vector2(85f, 85f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(BaseSpawnOffset, 0f, BaseSpawnOffset), new Vector2(85f, 85f), 0f, 3),
            new TerrainPatchSpec("NorthForestPlateau", new Vector3(-60f, 0f, -80f), new Vector2(140f, 90f), 0f, 1),
            new TerrainPatchSpec("SouthForestPlateau", new Vector3(60f, 0f, 80f), new Vector2(140f, 90f), 0f, 1),
        };
        map.ElevatedPlateaus = new[]
        {
            new ElevatedPlateauSpec("NorthAlpineRange", new Vector3(-60f, 0f, -80f), new Vector2(200f, 75f), 10.5f, 10f, 0),
            new ElevatedPlateauSpec("SouthAlpineRange", new Vector3(60f, 0f, 80f), new Vector2(200f, 75f), 10.5f, 10f, 0),
            new ElevatedPlateauSpec("CentralFortressPeak", new Vector3(0f, 0f, 0f), new Vector2(70f, 70f), 6.0f, 45f, 0),
        };
        map.Bridges = new[]
        {
            new BridgeSpec("ValleyStoneBridge", new Vector3(0f, 0f, 0f), 44f, 18f, -30f, "stone"),
        };
        map.RockClusters = new[]
        {
            new Vector3(-35, 0, 50), new Vector3(35, 0, -50),
            new Vector3(-110, 0, -50), new Vector3(110, 0, 50)
        };
        map.TreePositions = new[]
        {
            new Vector3(-90, 0, -110), new Vector3(-70, 0, -90), new Vector3(-120, 0, -70),
            new Vector3(90, 0, 110), new Vector3(70, 0, 90), new Vector3(120, 0, 70)
        };
        return map;
    }
}
