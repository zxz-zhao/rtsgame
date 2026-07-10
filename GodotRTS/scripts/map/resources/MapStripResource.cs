using Godot;

[Tool]
[GlobalClass]
public partial class MapStripResource : Resource
{
    [Export] public string Label { get; set; } = "Strip";
    [Export] public Vector3 Center { get; set; } = Vector3.Zero;
    [Export] public Vector2 Size { get; set; } = new Vector2(24f, 120f);
    [Export] public float Angle { get; set; } = 0f;

    public TerrainStripSpec ToSpec()
    {
        return new TerrainStripSpec(Label, Center, Size, Angle);
    }

    public static MapStripResource FromSpec(TerrainStripSpec spec)
    {
        return new MapStripResource
        {
            Label = spec.Name,
            Center = spec.Center,
            Size = spec.Size,
            Angle = spec.Angle,
        };
    }
}
