using System;
using UnityEngine;

public static class BattleMapDefinitionUtility
{
    public static readonly Vector3 DefaultPlayerBaseAnchor = new Vector3(-120f, 0f, -120f);
    public static readonly Vector3 DefaultEnemyBaseAnchor = new Vector3(120f, 0f, 120f);

    public static BattleMapDefinition CreateDefault(string mapName)
    {
        return new BattleMapDefinition
        {
            Name = string.IsNullOrEmpty(mapName) ? "New Map" : mapName,
            RandomSeed = 7601,
            GroundColor = new Color(0.25f, 0.36f, 0.22f),
            RoadColor = new Color(0.19f, 0.20f, 0.20f),
            RoadEdgeColor = new Color(0.40f, 0.38f, 0.31f),
            WaterColor = new Color(0.02f, 0.24f, 0.48f),
            PatchAColor = new Color(0.18f, 0.28f, 0.16f),
            PatchBColor = new Color(0.32f, 0.35f, 0.24f),
            PlayerBasePadColor = new Color(0.43f, 0.45f, 0.46f),
            EnemyBasePadColor = new Color(0.42f, 0.41f, 0.39f),
            RockColor = new Color(0.42f, 0.43f, 0.40f),
            FoliageColor = new Color(0.10f, 0.34f, 0.13f),
            TrunkColor = new Color(0.24f, 0.15f, 0.08f),
            WallColor = new Color(0.30f, 0.36f, 0.30f),
            RuinColor = new Color(0.46f, 0.42f, 0.35f),
            SkyColor = new Color(0.56f, 0.71f, 0.78f),
            FogColor = new Color(0.58f, 0.67f, 0.64f),
            AmbientSkyColor = new Color(0.61f, 0.68f, 0.64f),
            AmbientEquatorColor = new Color(0.38f, 0.43f, 0.34f),
            AmbientGroundColor = new Color(0.19f, 0.21f, 0.17f),
            FogStart = 220f,
            FogEnd = 520f,
            RockClusters = Array.Empty<Vector3>(),
            TreePositions = Array.Empty<Vector3>(),
            SpawnPoints = CreateDefaultSpawnPoints(),
            Props = Array.Empty<MapPropSpec>(),
            RuinWalls = Array.Empty<RuinWallSpec>(),
            SandbagRings = Array.Empty<SandbagRingSpec>(),
            Roads = Array.Empty<TerrainStripSpec>(),
            Waters = Array.Empty<TerrainStripSpec>(),
            Patches = Array.Empty<TerrainPatchSpec>(),
        };
    }

    public static SpawnPointSpec[] CreateDefaultSpawnPoints()
    {
        return new[]
        {
            new SpawnPointSpec("PlayerSpawn", DefaultPlayerBaseAnchor, 45f, true, 0),
            new SpawnPointSpec("EnemySpawn1", DefaultEnemyBaseAnchor, 225f, false, 1),
        };
    }

    public static BattleMapDefinition Clone(BattleMapDefinition source)
    {
        if (source == null)
            return CreateDefault("New Map");

        return new BattleMapDefinition
        {
            Name = source.Name,
            RandomSeed = source.RandomSeed,
            GroundColor = source.GroundColor,
            RoadColor = source.RoadColor,
            RoadEdgeColor = source.RoadEdgeColor,
            WaterColor = source.WaterColor,
            PatchAColor = source.PatchAColor,
            PatchBColor = source.PatchBColor,
            PlayerBasePadColor = source.PlayerBasePadColor,
            EnemyBasePadColor = source.EnemyBasePadColor,
            RockColor = source.RockColor,
            FoliageColor = source.FoliageColor,
            TrunkColor = source.TrunkColor,
            WallColor = source.WallColor,
            RuinColor = source.RuinColor,
            SkyColor = source.SkyColor,
            FogColor = source.FogColor,
            AmbientSkyColor = source.AmbientSkyColor,
            AmbientEquatorColor = source.AmbientEquatorColor,
            AmbientGroundColor = source.AmbientGroundColor,
            FogStart = source.FogStart,
            FogEnd = source.FogEnd,
            RockClusters = CloneArray(source.RockClusters),
            TreePositions = CloneArray(source.TreePositions),
            SpawnPoints = CloneArray(source.SpawnPoints),
            Props = CloneArray(source.Props),
            RuinWalls = CloneArray(source.RuinWalls),
            SandbagRings = CloneArray(source.SandbagRings),
            Roads = CloneArray(source.Roads),
            Waters = CloneArray(source.Waters),
            Patches = CloneArray(source.Patches),
        };
    }

    public static void Sanitize(BattleMapDefinition map, string fallbackName)
    {
        if (map == null)
            return;

        if (string.IsNullOrWhiteSpace(map.Name))
            map.Name = string.IsNullOrWhiteSpace(fallbackName) ? "New Map" : fallbackName;

        if (map.RandomSeed == 0)
            map.RandomSeed = 7601;

        map.RockClusters ??= Array.Empty<Vector3>();
        map.TreePositions ??= Array.Empty<Vector3>();
        map.SpawnPoints ??= Array.Empty<SpawnPointSpec>();
        map.Props ??= Array.Empty<MapPropSpec>();
        map.RuinWalls ??= Array.Empty<RuinWallSpec>();
        map.SandbagRings ??= Array.Empty<SandbagRingSpec>();
        map.Roads ??= Array.Empty<TerrainStripSpec>();
        map.Waters ??= Array.Empty<TerrainStripSpec>();
        map.Patches ??= Array.Empty<TerrainPatchSpec>();
    }

    public static bool TryGetPlayerSpawn(BattleMapDefinition map, out SpawnPointSpec spawn)
    {
        SpawnPointSpec[] spawnPoints = map != null ? map.SpawnPoints : null;
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (!spawnPoints[i].IsPlayer)
                    continue;

                spawn = spawnPoints[i];
                spawn.Position = ClampSpawnPosition(spawn.Position);
                spawn.TeamIndex = 0;
                return true;
            }
        }

        spawn = default;
        return false;
    }

    public static bool TryGetPrimaryEnemySpawn(BattleMapDefinition map, out SpawnPointSpec spawn)
    {
        SpawnPointSpec[] spawnPoints = map != null ? map.SpawnPoints : null;
        bool found = false;
        int bestTeamIndex = int.MaxValue;
        spawn = default;

        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPointSpec candidate = spawnPoints[i];
                if (candidate.IsPlayer)
                    continue;

                int teamIndex = Mathf.Max(1, candidate.TeamIndex);
                if (found && teamIndex >= bestTeamIndex)
                    continue;

                candidate.Position = ClampSpawnPosition(candidate.Position);
                candidate.TeamIndex = teamIndex;
                spawn = candidate;
                bestTeamIndex = teamIndex;
                found = true;
            }
        }

        return found;
    }

    public static Vector3 GetPlayerBaseAnchor(BattleMapDefinition map)
    {
        return TryGetPlayerSpawn(map, out SpawnPointSpec spawn)
            ? spawn.Position
            : DefaultPlayerBaseAnchor;
    }

    public static Vector3 GetEnemyBaseAnchor(BattleMapDefinition map)
    {
        return TryGetPrimaryEnemySpawn(map, out SpawnPointSpec spawn)
            ? spawn.Position
            : DefaultEnemyBaseAnchor;
    }

    public static Vector3 TranslateFromLegacyAnchor(Vector3 legacyPosition, Vector3 legacyAnchor, Vector3 targetAnchor)
    {
        Vector3 offset = legacyPosition - legacyAnchor;
        Vector3 translated = targetAnchor + offset;
        translated.y = 0f;
        return translated;
    }

    static T[] CloneArray<T>(T[] values)
    {
        if (values == null || values.Length == 0)
            return Array.Empty<T>();

        T[] clone = new T[values.Length];
        Array.Copy(values, clone, values.Length);
        return clone;
    }

    static Vector3 ClampSpawnPosition(Vector3 position)
    {
        position.y = 0f;
        return position;
    }
}

