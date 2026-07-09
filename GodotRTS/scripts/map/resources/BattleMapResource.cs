using Godot;
using System;

[Tool]
[GlobalClass]
public partial class BattleMapResource : Resource
{
    [Export] public string MapName { get; set; } = "Simulation Sandbox";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "Editable 3D map resource for Terrain3D, GridMap, spawns, roads, and water.";
    [Export] public int RandomSeed { get; set; } = 7601;

    [ExportGroup("Surface Colors")]
    [Export] public Color GroundColor { get; set; } = new(0.25f, 0.36f, 0.22f);
    [Export] public Color RoadColor { get; set; } = new(0.19f, 0.20f, 0.20f);
    [Export] public Color RoadEdgeColor { get; set; } = new(0.40f, 0.38f, 0.31f);
    [Export] public Color WaterColor { get; set; } = new(0.02f, 0.24f, 0.48f);
    [Export] public Color PatchAColor { get; set; } = new(0.18f, 0.28f, 0.16f);
    [Export] public Color PatchBColor { get; set; } = new(0.32f, 0.35f, 0.24f);
    [Export] public Color PlayerBasePadColor { get; set; } = new(0.43f, 0.45f, 0.46f);
    [Export] public Color EnemyBasePadColor { get; set; } = new(0.42f, 0.41f, 0.39f);

    [ExportGroup("World Colors")]
    [Export] public Color RockColor { get; set; } = new(0.42f, 0.43f, 0.40f);
    [Export] public Color FoliageColor { get; set; } = new(0.10f, 0.34f, 0.13f);
    [Export] public Color TrunkColor { get; set; } = new(0.24f, 0.15f, 0.08f);
    [Export] public Color WallColor { get; set; } = new(0.30f, 0.36f, 0.30f);
    [Export] public Color RuinColor { get; set; } = new(0.46f, 0.42f, 0.35f);
    [Export] public Color SkyColor { get; set; } = new(0.56f, 0.71f, 0.78f);
    [Export] public Color FogColor { get; set; } = new(0.58f, 0.67f, 0.64f);
    [Export] public Color AmbientSkyColor { get; set; } = new(0.61f, 0.68f, 0.64f);
    [Export] public Color AmbientEquatorColor { get; set; } = new(0.38f, 0.43f, 0.34f);
    [Export] public Color AmbientGroundColor { get; set; } = new(0.19f, 0.21f, 0.17f);
    [Export] public float FogStart { get; set; } = 220f;
    [Export] public float FogEnd { get; set; } = 520f;

    [ExportGroup("Terrain")]
    [Export] public Vector3[] RockClusters { get; set; } = System.Array.Empty<Vector3>();
    [Export] public Vector3[] TreePositions { get; set; } = System.Array.Empty<Vector3>();
    [Export] public Godot.Collections.Array<MapSpawnPointResource> SpawnPoints { get; set; } = new();
    [Export] public Godot.Collections.Array<MapStripResource> Roads { get; set; } = new();
    [Export] public Godot.Collections.Array<MapStripResource> Waters { get; set; } = new();
    [Export] public Godot.Collections.Array<MapPatchResource> Patches { get; set; } = new();

    public BattleMapDefinition ToDefinition()
    {
        return new BattleMapDefinition
        {
            Name = string.IsNullOrWhiteSpace(MapName) ? "Simulation Sandbox" : MapName,
            Description = Description ?? string.Empty,
            RandomSeed = RandomSeed,
            GroundColor = GroundColor,
            RoadColor = RoadColor,
            RoadEdgeColor = RoadEdgeColor,
            WaterColor = WaterColor,
            PatchAColor = PatchAColor,
            PatchBColor = PatchBColor,
            PlayerBasePadColor = PlayerBasePadColor,
            EnemyBasePadColor = EnemyBasePadColor,
            RockColor = RockColor,
            FoliageColor = FoliageColor,
            TrunkColor = TrunkColor,
            WallColor = WallColor,
            RuinColor = RuinColor,
            SkyColor = SkyColor,
            FogColor = FogColor,
            AmbientSkyColor = AmbientSkyColor,
            AmbientEquatorColor = AmbientEquatorColor,
            AmbientGroundColor = AmbientGroundColor,
            FogStart = FogStart,
            FogEnd = FogEnd,
            RockClusters = RockClusters ?? System.Array.Empty<Vector3>(),
            TreePositions = TreePositions ?? System.Array.Empty<Vector3>(),
            SpawnPoints = ConvertSpawns(SpawnPoints),
            Roads = ConvertStrips(Roads),
            Waters = ConvertStrips(Waters),
            Patches = ConvertPatches(Patches),
            RuinWalls = System.Array.Empty<RuinWallSpec>(),
            SandbagRings = System.Array.Empty<SandbagRingSpec>(),
        };
    }

    public static BattleMapResource FromDefinition(BattleMapDefinition map)
    {
        var resource = new BattleMapResource
        {
            MapName = map.Name,
            Description = map.Description,
            RandomSeed = map.RandomSeed,
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
            FogStart = map.FogStart,
            FogEnd = map.FogEnd,
            RockClusters = map.RockClusters ?? System.Array.Empty<Vector3>(),
            TreePositions = map.TreePositions ?? System.Array.Empty<Vector3>(),
        };

        if (map.SpawnPoints is not null)
        {
            foreach (var spawn in map.SpawnPoints)
                resource.SpawnPoints.Add(MapSpawnPointResource.FromSpec(spawn));
        }

        if (map.Roads is not null)
        {
            foreach (var road in map.Roads)
                resource.Roads.Add(MapStripResource.FromSpec(road));
        }

        if (map.Waters is not null)
        {
            foreach (var water in map.Waters)
                resource.Waters.Add(MapStripResource.FromSpec(water));
        }

        if (map.Patches is not null)
        {
            foreach (var patch in map.Patches)
                resource.Patches.Add(MapPatchResource.FromSpec(patch));
        }

        return resource;
    }

    static SpawnPointSpec[] ConvertSpawns(Godot.Collections.Array<MapSpawnPointResource> resources)
    {
        if (resources is null || resources.Count == 0)
            return System.Array.Empty<SpawnPointSpec>();

        var buffer = new SpawnPointSpec[resources.Count];
        var count = 0;
        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            if (resource is null)
                continue;

            buffer[count++] = resource.ToSpec();
        }

        if (count == buffer.Length)
            return buffer;

        var trimmed = new SpawnPointSpec[count];
        System.Array.Copy(buffer, trimmed, count);
        return trimmed;
    }

    static TerrainStripSpec[] ConvertStrips(Godot.Collections.Array<MapStripResource> resources)
    {
        if (resources is null || resources.Count == 0)
            return System.Array.Empty<TerrainStripSpec>();

        var buffer = new TerrainStripSpec[resources.Count];
        var count = 0;
        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            if (resource is null)
                continue;

            buffer[count++] = resource.ToSpec();
        }

        if (count == buffer.Length)
            return buffer;

        var trimmed = new TerrainStripSpec[count];
        System.Array.Copy(buffer, trimmed, count);
        return trimmed;
    }

    static TerrainPatchSpec[] ConvertPatches(Godot.Collections.Array<MapPatchResource> resources)
    {
        if (resources is null || resources.Count == 0)
            return System.Array.Empty<TerrainPatchSpec>();

        var buffer = new TerrainPatchSpec[resources.Count];
        var count = 0;
        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            if (resource is null)
                continue;

            buffer[count++] = resource.ToSpec();
        }

        if (count == buffer.Length)
            return buffer;

        var trimmed = new TerrainPatchSpec[count];
        System.Array.Copy(buffer, trimmed, count);
        return trimmed;
    }
}
