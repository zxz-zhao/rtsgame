using Godot;

[Tool]
[GlobalClass]
public partial class MapPatchResource : Resource
{
    [Export] public string Label { get; set; } = "Patch";
    [Export] public Vector3 Center { get; set; } = Vector3.Zero;
    [Export] public Vector2 Size { get; set; } = new Vector2(48f, 48f);
    [Export] public float Angle { get; set; } = 0f;
    [Export(PropertyHint.Range, "0,3,1")] public int PaletteIndex { get; set; } = 0;

    public TerrainPatchSpec ToSpec()
    {
        return new TerrainPatchSpec(Label, Center, Size, Angle, PaletteIndex);
    }

    public static MapPatchResource FromSpec(TerrainPatchSpec spec)
    {
        return new MapPatchResource
        {
            Label = spec.Name,
            Center = spec.Center,
            Size = spec.Size,
            Angle = spec.Angle,
            PaletteIndex = spec.PaletteIndex,
        };
    }
}
