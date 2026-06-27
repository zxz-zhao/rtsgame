using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TerrainStripSpec
{
    public string Name;
    public Vector3 Center;
    public Vector2 Size;
    public float Angle;

    /// <summary>
    /// Packs the rectangular strip data used by roads, rivers, and other linear terrain features.
    /// </summary>
    public TerrainStripSpec(string name, Vector3 center, Vector2 size, float angle)
    {
        Name = name;
        Center = center;
        Size = size;
        Angle = angle;
    }
}

[Serializable]
public struct TerrainPatchSpec
{
    public string Name;
    public Vector3 Center;
    public Vector2 Size;
    public float Angle;
    public int PaletteIndex;

    /// <summary>
    /// Packs one rectangular terrain patch description plus the palette slot used to color it.
    /// </summary>
    public TerrainPatchSpec(string name, Vector3 center, Vector2 size, float angle, int paletteIndex)
    {
        Name = name;
        Center = center;
        Size = size;
        Angle = angle;
        PaletteIndex = paletteIndex;
    }
}

[Serializable]
public struct RuinWallSpec
{
    public Vector3 Center;
    public float Angle;
    public int Segments;

    /// <summary>
    /// Describes one ruined-wall run by center point, facing angle, and segment count.
    /// </summary>
    public RuinWallSpec(Vector3 center, float angle, int segments)
    {
        Center = center;
        Angle = angle;
        Segments = segments;
    }
}

[Serializable]
public struct SandbagRingSpec
{
    public Vector3 Center;
    public float Radius;
    public int Count;
    public bool IsEnemy;

    /// <summary>
    /// Describes one circular sandbag emplacement around a base or objective.
    /// </summary>
    public SandbagRingSpec(Vector3 center, float radius, int count, bool isEnemy)
    {
        Center = center;
        Radius = radius;
        Count = count;
        IsEnemy = isEnemy;
    }
}

[Serializable]
public struct SpawnPointSpec
{
    public string Name;
    public Vector3 Position;
    public float Yaw;
    public bool IsPlayer;
    public int TeamIndex;
    public SpawnPointSpec(string name, Vector3 position, float yaw, bool isPlayer, int teamIndex)
    {
        Name = name;
        Position = position;
        Yaw = yaw;
        IsPlayer = isPlayer;
        TeamIndex = teamIndex;
    }
}

[Serializable]
public struct MapPropSpec
{
    public string Name;
    public string ResourcePath;
    public Vector3 Position;
    public float Yaw;
    public float Scale;
    public bool BlocksNavigation;

    public MapPropSpec(string name, string resourcePath, Vector3 position, float yaw, float scale, bool blocksNavigation)
    {
        Name = name;
        ResourcePath = resourcePath;
        Position = position;
        Yaw = yaw;
        Scale = scale;
        BlocksNavigation = blocksNavigation;
    }
}
[Serializable]
public class BattleMapDefinition
{
    public string Name;
    public int RandomSeed;

    public Color GroundColor;
    public Color RoadColor;
    public Color RoadEdgeColor;
    public Color WaterColor;
    public Color PatchAColor;
    public Color PatchBColor;
    public Color PlayerBasePadColor;
    public Color EnemyBasePadColor;

    public Color RockColor;
    public Color FoliageColor;
    public Color TrunkColor;
    public Color WallColor;
    public Color RuinColor;

    public Color SkyColor;
    public Color FogColor;
    public Color AmbientSkyColor;
    public Color AmbientEquatorColor;
    public Color AmbientGroundColor;
    public float FogStart;
    public float FogEnd;

    public Vector3[] RockClusters;
    public Vector3[] TreePositions;
    public SpawnPointSpec[] SpawnPoints;
    public MapPropSpec[] Props;
    public RuinWallSpec[] RuinWalls;
    public SandbagRingSpec[] SandbagRings;
    public TerrainStripSpec[] Roads;
    public TerrainStripSpec[] Waters;
    public TerrainPatchSpec[] Patches;

    /// <summary>
    /// Resolves the terrain tint associated with one patch palette index.
    /// </summary>
    public Color GetPatchColor(int paletteIndex)
    {
        switch (paletteIndex)
        {
            case 1: return PatchBColor;
            case 2: return PlayerBasePadColor;
            case 3: return EnemyBasePadColor;
            default: return PatchAColor;
        }
    }
}

public static class BattleMapCatalog
{
    public const string DefaultMapName = "沙漠绿洲";
    public const string IceFortressName = "冰雪要塞";
    public const string JungleName = "丛林战场";
    public const string CityRuinsName = "城市废墟";
    public const string SeaChartName = "海图群岛";
    public const string GlobalConquestName = "全球争霸";
    public const float MapHalfSize = 200f;
    public const string CustomMapResourcesPath = "MapAssets";

    /// <summary>
    /// Returns the subset of maps intended for normal skirmish selection.
    /// </summary>
    public static string[] GetPlayableMapNames()
    {
        return AppendCustomMapNames(new[]
        {
            DefaultMapName,
            IceFortressName,
            JungleName,
            CityRuinsName,
            SeaChartName,
        });
    }

    /// <summary>
    /// Returns every map definition name, including special or alternate modes.
    /// </summary>
    public static string[] GetAllMapNames()
    {
        return AppendCustomMapNames(new[]
        {
            DefaultMapName,
            IceFortressName,
            JungleName,
            CityRuinsName,
            SeaChartName,
            GlobalConquestName,
        });
    }

    /// <summary>
    /// Builds a fresh battle-map definition for the requested map name.
    /// </summary>
    public static BattleMapDefinition Get(string mapName)
    {
        if (TryGetCustomMap(mapName, out BattleMapDefinition customMap))
            return customMap;

        switch (mapName)
        {
            case IceFortressName:
                return CreateIceFortress();
            case JungleName:
                return CreateJungle();
            case CityRuinsName:
                return CreateCityRuins();
            case SeaChartName:
                return CreateSeaChartIslands();
            case GlobalConquestName:
                return CreateGlobalConquest();
            default:
                return CreateSandOasis();
        }
    }

    public static bool TryGetCustomMap(string mapName, out BattleMapDefinition map)
    {
        map = null;
        if (string.IsNullOrWhiteSpace(mapName))
            return false;

        BattleMapAsset[] assets = Resources.LoadAll<BattleMapAsset>(CustomMapResourcesPath);
        for (int i = 0; i < assets.Length; i++)
        {
            BattleMapAsset asset = assets[i];
            if (asset == null)
                continue;

            BattleMapDefinition definition = asset.ToDefinition();
            if (definition == null)
                continue;

            if (string.Equals(definition.Name, mapName, StringComparison.Ordinal)
                || string.Equals(asset.name, mapName, StringComparison.Ordinal))
            {
                map = definition;
                return true;
            }
        }

        return false;
    }

    static string[] AppendCustomMapNames(string[] builtInNames)
    {
        List<string> names = new List<string>(builtInNames);
        BattleMapAsset[] assets = Resources.LoadAll<BattleMapAsset>(CustomMapResourcesPath);
        for (int i = 0; i < assets.Length; i++)
        {
            BattleMapAsset asset = assets[i];
            if (asset == null)
                continue;

            BattleMapDefinition definition = asset.ToDefinition();
            string name = definition != null && !string.IsNullOrWhiteSpace(definition.Name)
                ? definition.Name
                : asset.name;

            if (!ContainsName(names, name))
                names.Add(name);
        }

        return names.ToArray();
    }

    static bool ContainsName(List<string> names, string name)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates the common baseline geometry layout that each themed map variant customizes.
    /// </summary>
    static BattleMapDefinition CreateBase(string name, int seed)
    {
        return new BattleMapDefinition
        {
            Name = name,
            RandomSeed = seed,

            RockClusters = new[]
            {
                new Vector3(-60, 0, 40),  new Vector3(60, 0, -40),
                new Vector3(-100, 0, -60), new Vector3(100, 0, 60),
                new Vector3(0, 0, 100),   new Vector3(0, 0, -100),
                new Vector3(-40, 0, 0),   new Vector3(40, 0, 0),
                new Vector3(0, 0, 40),    new Vector3(0, 0, -40),
                new Vector3(-140, 0, 60), new Vector3(140, 0, -60),
                new Vector3(-60, 0, 140), new Vector3(60, 0, -140),
            },

            TreePositions = new[]
            {
                new Vector3(-160, 0, 160),  new Vector3(160, 0, 160),
                new Vector3(-160, 0, -160), new Vector3(160, 0, -160),
                new Vector3(-120, 0, 0),    new Vector3(120, 0, 0),
                new Vector3(0, 0, 140),     new Vector3(0, 0, -140),
                new Vector3(-170, 0, 0),    new Vector3(170, 0, 0),
                new Vector3(0, 0, 170),     new Vector3(0, 0, -170),
            },

            SpawnPoints = BattleMapDefinitionUtility.CreateDefaultSpawnPoints(),
            Props = Array.Empty<MapPropSpec>(),

            RuinWalls = new[]
            {
                new RuinWallSpec(new Vector3( 10f, 0, 10f),  45f, 5),
                new RuinWallSpec(new Vector3(-10f, 0, -10f), 45f, 5),
                new RuinWallSpec(new Vector3(-50f, 0, 80f),  20f, 4),
                new RuinWallSpec(new Vector3( 50f, 0, -80f), 20f, 4),
                new RuinWallSpec(new Vector3( 90f, 0, 30f),  70f, 3),
                new RuinWallSpec(new Vector3(-90f, 0, -30f), 70f, 3),
            },

            SandbagRings = new[]
            {
                new SandbagRingSpec(new Vector3(-115f, 0f, -115f), 22f, 8, false),
                new SandbagRingSpec(new Vector3( 115f, 0f,  115f), 22f, 8, true),
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
                new TerrainPatchSpec("PlayerBasePad", new Vector3(-115f, 0f, -115f), new Vector2(82f, 82f), 0f, 2),
                new TerrainPatchSpec("EnemyBasePad", new Vector3(115f, 0f, 115f), new Vector2(82f, 82f), 0f, 3),
                new TerrainPatchSpec("CentralHardpoint", new Vector3(0f, 0f, 0f), new Vector2(60f, 52f), 45f, 1),
                new TerrainPatchSpec("NorthResourcePatch", new Vector3(-70f, 0f, 112f), new Vector2(64f, 34f), -18f, 0),
                new TerrainPatchSpec("SouthResourcePatch", new Vector3(70f, 0f, -112f), new Vector2(64f, 34f), -18f, 0),
            },
        };
    }

    /// <summary>
    /// Creates the default desert-oasis skirmish map.
    /// </summary>
    static BattleMapDefinition CreateSandOasis()
    {
        var map = CreateBase(DefaultMapName, 7601);
        ApplyCleanLandForestOceanStyle(map);
        return map;
    }

    /// <summary>
    /// Applies the shared clean-land/ocean presentation used by the default land-focused map style.
    /// </summary>
    static void ApplyCleanLandForestOceanStyle(BattleMapDefinition map)
    {
        map.GroundColor = new Color(0.25f, 0.36f, 0.22f);
        map.RoadColor = new Color(0.19f, 0.20f, 0.20f);
        map.RoadEdgeColor = new Color(0.40f, 0.38f, 0.31f);
        map.WaterColor = new Color(0.02f, 0.24f, 0.48f);
        map.PatchAColor = new Color(0.18f, 0.28f, 0.16f);
        map.PatchBColor = new Color(0.32f, 0.35f, 0.24f);
        map.PlayerBasePadColor = new Color(0.43f, 0.45f, 0.46f);
        map.EnemyBasePadColor = new Color(0.42f, 0.41f, 0.39f);
        map.RockColor = new Color(0.42f, 0.43f, 0.40f);
        map.FoliageColor = new Color(0.10f, 0.34f, 0.13f);
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
            new TerrainStripSpec("CentralCreek", new Vector3(0f, 0f, 0f), new Vector2(14f, 52f), -42f),
        };

        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-115f, 0f, -115f), new Vector2(82f, 82f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(115f, 0f, 115f), new Vector2(82f, 82f), 0f, 3),
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
            new Vector3(-172,0,128), new Vector3(172,0,-128), new Vector3(-172,0,-128), new Vector3(172,0,128),
            new Vector3(-154,0,102), new Vector3(-118,0,96), new Vector3(-138,0,-92), new Vector3(-96,0,-106),
            new Vector3(154,0,-102), new Vector3(118,0,-96), new Vector3(138,0,92), new Vector3(96,0,106),
            new Vector3(-34,0,170), new Vector3(34,0,-170), new Vector3(-168,0,42), new Vector3(168,0,-42),
        };

        map.RockClusters = new[]
        {
            new Vector3(-84,0,-42), new Vector3(84,0,42),
            new Vector3(-42,0,92), new Vector3(42,0,-92),
            new Vector3(-150,0,-82), new Vector3(150,0,82),
        };

        map.RuinWalls = new RuinWallSpec[0];
        map.SandbagRings = new SandbagRingSpec[0];
    }

    /// <summary>
    /// Creates the snow-and-fortification themed map variant.
    /// </summary>
    static BattleMapDefinition CreateIceFortress()
    {
        var map = CreateBase(IceFortressName, 8612);
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
        map.RockClusters = new[]
        {
            new Vector3(-76,0,48), new Vector3(76,0,-48), new Vector3(-122,0,-44), new Vector3(122,0,44),
            new Vector3(-30,0,12), new Vector3(30,0,-12), new Vector3(-18,0,88), new Vector3(18,0,-88),
            new Vector3(-150,0,105), new Vector3(150,0,-105), new Vector3(-104,0,142), new Vector3(104,0,-142),
        };
        map.TreePositions = new[]
        {
            new Vector3(-170,0,170), new Vector3(170,0,170), new Vector3(-170,0,-170), new Vector3(170,0,-170),
            new Vector3(-150,0,30), new Vector3(150,0,-30), new Vector3(-32,0,160), new Vector3(32,0,-160),
        };
        map.RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3(-15f,0f,20f), 55f, 5),
            new RuinWallSpec(new Vector3(15f,0f,-20f), 55f, 5),
            new RuinWallSpec(new Vector3(-72f,0f,92f), 6f, 5),
            new RuinWallSpec(new Vector3(72f,0f,-92f), 6f, 5),
            new RuinWallSpec(new Vector3(112f,0f,12f), 88f, 4),
            new RuinWallSpec(new Vector3(-112f,0f,-12f), 88f, 4),
        };
        return map;
    }

    /// <summary>
    /// Creates the dense jungle map variant with rivers and short-visibility flavor.
    /// </summary>
    static BattleMapDefinition CreateJungle()
    {
        var map = CreateBase(JungleName, 9144);
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
        map.RockClusters = new[]
        {
            new Vector3(-58,0,36), new Vector3(58,0,-36), new Vector3(-112,0,-76), new Vector3(112,0,76),
            new Vector3(-24,0,-18), new Vector3(24,0,18), new Vector3(-136,0,42), new Vector3(136,0,-42),
        };
        map.TreePositions = new[]
        {
            new Vector3(-170,0,170), new Vector3(-145,0,138), new Vector3(-170,0,-170), new Vector3(-140,0,-130),
            new Vector3(170,0,170), new Vector3(142,0,128), new Vector3(170,0,-170), new Vector3(136,0,-138),
            new Vector3(-154,0,12), new Vector3(-132,0,62), new Vector3(154,0,-12), new Vector3(132,0,-62),
            new Vector3(-24,0,150), new Vector3(24,0,-150), new Vector3(-92,0,-18), new Vector3(92,0,18),
        };
        map.RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3( 8f,0f, 8f), 35f, 4),
            new RuinWallSpec(new Vector3(-8f,0f,-8f), 35f, 4),
            new RuinWallSpec(new Vector3(-62f,0f,86f), 68f, 3),
            new RuinWallSpec(new Vector3(62f,0f,-86f), 68f, 3),
        };
        return map;
    }

    /// <summary>
    /// Creates the urban-ruins map variant with wider roads and heavier central cover.
    /// </summary>
    static BattleMapDefinition CreateCityRuins()
    {
        var map = CreateBase(CityRuinsName, 10220);
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
        map.Patches = new[]
        {
            new TerrainPatchSpec("PlayerBasePad", new Vector3(-115f, 0f, -115f), new Vector2(86f, 86f), 0f, 2),
            new TerrainPatchSpec("EnemyBasePad", new Vector3(115f, 0f, 115f), new Vector2(86f, 86f), 0f, 3),
            new TerrainPatchSpec("CentralPlaza", new Vector3(0f, 0f, 0f), new Vector2(76f, 76f), 45f, 1),
            new TerrainPatchSpec("DepotNorth", new Vector3(-82f, 0f, 110f), new Vector2(68f, 38f), 0f, 0),
            new TerrainPatchSpec("DepotSouth", new Vector3(82f, 0f, -110f), new Vector2(68f, 38f), 0f, 0),
        };
        map.RockClusters = new[]
        {
            new Vector3(-72,0,40), new Vector3(72,0,-40), new Vector3(-118,0,-70), new Vector3(118,0,70),
            new Vector3(-28,0,92), new Vector3(28,0,-92), new Vector3(-138,0,118), new Vector3(138,0,-118),
        };
        map.TreePositions = new[]
        {
            new Vector3(-176,0,176), new Vector3(176,0,176), new Vector3(-176,0,-176), new Vector3(176,0,-176),
            new Vector3(-158,0,16), new Vector3(158,0,-16),
        };
        map.RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3( 18f,0f, 18f), 45f, 6),
            new RuinWallSpec(new Vector3(-18f,0f,-18f), 45f, 6),
            new RuinWallSpec(new Vector3(-54f,0f,78f), 0f, 6),
            new RuinWallSpec(new Vector3(54f,0f,-78f), 0f, 6),
            new RuinWallSpec(new Vector3(88f,0f,34f), 90f, 5),
            new RuinWallSpec(new Vector3(-88f,0f,-34f), 90f, 5),
            new RuinWallSpec(new Vector3(0f,0f,126f), 0f, 4),
            new RuinWallSpec(new Vector3(0f,0f,-126f), 0f, 4),
        };
        return map;
    }

    /// <summary>
    /// Creates the global-conquest variant by tweaking the default land map's presentation distances.
    /// </summary>
    static BattleMapDefinition CreateGlobalConquest()
    {
        var map = CreateSandOasis();
        map.Name = GlobalConquestName;
        map.RandomSeed = 11550;
        map.FogStart = 175f;
        map.FogEnd = 450f;
        return map;
    }

    /// <summary>
    /// Creates the island-heavy naval map variant used by sea-chart battles.
    /// </summary>
    static BattleMapDefinition CreateSeaChartIslands()
    {
        var map = CreateBase(SeaChartName, 12077);
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
            new TerrainStripSpec("SmugglerCut", new Vector3(0f, 0f, 0f), new Vector2(5f, 150f), 5f),
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
            new TerrainPatchSpec("PlayerIsland", new Vector3(-120f, 0f, -118f), new Vector2(94f, 82f), -16f, 2),
            new TerrainPatchSpec("EnemyIsland", new Vector3(120f, 0f, 118f), new Vector2(94f, 82f), -16f, 3),
            new TerrainPatchSpec("CompassAtoll", new Vector3(0f, 0f, 0f), new Vector2(78f, 68f), 45f, 1),
            new TerrainPatchSpec("NorthHarbor", new Vector3(-78f, 0f, 120f), new Vector2(76f, 40f), 12f, 0),
            new TerrainPatchSpec("SouthHarbor", new Vector3(78f, 0f, -120f), new Vector2(76f, 40f), 12f, 0),
            new TerrainPatchSpec("WestSandbar", new Vector3(-148f, 0f, 34f), new Vector2(54f, 28f), -22f, 0),
            new TerrainPatchSpec("EastSandbar", new Vector3(148f, 0f, -34f), new Vector2(54f, 28f), -22f, 0),
        };

        map.RockClusters = new[]
        {
            new Vector3(-136,0,-90), new Vector3(-98,0,-142), new Vector3(136,0,90), new Vector3(98,0,142),
            new Vector3(-22,0,28), new Vector3(22,0,-28), new Vector3(-92,0,118), new Vector3(92,0,-118),
            new Vector3(-154,0,36), new Vector3(154,0,-36),
        };

        map.TreePositions = new[]
        {
            new Vector3(-154,0,-132), new Vector3(-128,0,-86), new Vector3(-92,0,-128), new Vector3(-148,0,-72),
            new Vector3(154,0,132), new Vector3(128,0,86), new Vector3(92,0,128), new Vector3(148,0,72),
            new Vector3(-92,0,132), new Vector3(92,0,-132), new Vector3(-154,0,48), new Vector3(154,0,-48),
        };

        map.RuinWalls = new[]
        {
            new RuinWallSpec(new Vector3(-120f,0f,-102f), 28f, 3),
            new RuinWallSpec(new Vector3(120f,0f,102f), 28f, 3),
            new RuinWallSpec(new Vector3(-76f,0f,122f), 96f, 3),
            new RuinWallSpec(new Vector3(76f,0f,-122f), 96f, 3),
        };

        map.SandbagRings = new[]
        {
            new SandbagRingSpec(new Vector3(-120f, 0f, -118f), 24f, 7, false),
            new SandbagRingSpec(new Vector3(120f, 0f, 118f), 24f, 7, true),
        };

        return map;
    }
}

