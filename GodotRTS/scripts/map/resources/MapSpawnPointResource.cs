using Godot;

[GlobalClass]
public partial class MapSpawnPointResource : Resource
{
    [Export] public string Label { get; set; } = "PlayerBase";
    [Export] public Vector3 Position { get; set; } = Vector3.Zero;
    [Export] public float Yaw { get; set; } = 0f;
    [Export] public bool IsPlayer { get; set; } = true;
    [Export] public int TeamIndex { get; set; } = 0;

    public SpawnPointSpec ToSpec()
    {
        return new SpawnPointSpec(Label, Position, Yaw, IsPlayer, TeamIndex);
    }

    public static MapSpawnPointResource FromSpec(SpawnPointSpec spec)
    {
        return new MapSpawnPointResource
        {
            Label = spec.Name,
            Position = spec.Position,
            Yaw = spec.Yaw,
            IsPlayer = spec.IsPlayer,
            TeamIndex = spec.TeamIndex,
        };
    }
}
