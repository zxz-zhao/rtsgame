using Godot;

public partial class WorldHealthBar3D : Node3D
{
    [Export] public float Width { get; set; } = 2.4f;
    [Export] public float HeightOffset { get; set; } = 2.4f;
    [Export] public float Depth { get; set; } = 0.22f;

    MeshInstance3D fill = null!;
    MeshInstance3D back = null!;

    public override void _Ready()
    {
        Position = new Vector3(0f, HeightOffset, 0f);

        back = new MeshInstance3D
        {
            Name = "Back",
            Mesh = new BoxMesh { Size = new Vector3(Width + 0.18f, 0.08f, Depth + 0.12f) },
            MaterialOverride = MakeMaterial(new Color(0.01f, 0.012f, 0.012f, 0.82f))
        };
        AddChild(back);

        fill = new MeshInstance3D
        {
            Name = "Fill",
            Position = new Vector3(0f, 0.07f, 0f),
            Mesh = new BoxMesh { Size = new Vector3(Width, 0.10f, Depth) },
            MaterialOverride = MakeMaterial(new Color(0.22f, 0.95f, 0.38f, 0.94f))
        };
        AddChild(fill);
    }

    public override void _Process(double delta)
    {
        var (current, max, selected, enemy) = ReadOwnerState();
        if (max <= 0f)
        {
            Visible = false;
            return;
        }

        var ratio = Mathf.Clamp(current / max, 0f, 1f);
        Visible = selected || ratio < 0.995f;
        fill.Scale = new Vector3(Mathf.Max(0.01f, ratio), 1f, 1f);
        fill.Position = new Vector3((ratio - 1f) * Width * 0.5f, 0.07f, 0f);
        fill.MaterialOverride = MakeMaterial(HealthColor(ratio, enemy));
    }

    (float current, float max, bool selected, bool enemy) ReadOwnerState()
    {
        return GetParent() switch
        {
            RtsUnit unit => (unit.Health, unit.MaxHealth, unit.Selected, !unit.PlayerOwned),
            RtsBuilding building => (building.Health, building.MaxHealth, building.Selected, !building.PlayerOwned),
            _ => (1f, 1f, false, false)
        };
    }

    static Color HealthColor(float ratio, bool enemy)
    {
        if (enemy)
            return ratio > 0.5f
                ? new Color(1f, 0.58f, 0.18f, 0.96f)
                : new Color(1f, 0.18f, 0.12f, 0.96f);

        return ratio > 0.55f
            ? new Color(0.18f, 0.92f, 0.38f, 0.96f)
            : new Color(1f, 0.76f, 0.14f, 0.96f);
    }

    static StandardMaterial3D MakeMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.85f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }
}
