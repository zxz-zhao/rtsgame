using Godot;

public partial class WorldHealthBar3D : Node3D
{
    [Export] public float Width { get; set; } = 2.4f;
    [Export] public float HeightOffset { get; set; } = 2.4f;
    [Export] public float Depth { get; set; } = 0.22f;

    MeshInstance3D fill = null!;
    MeshInstance3D back = null!;
    MeshInstance3D progressFill = null!;
    MeshInstance3D progressBack = null!;

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

        progressBack = new MeshInstance3D
        {
            Name = "ProgressBack",
            Mesh = new BoxMesh { Size = new Vector3(Width + 0.18f, 0.08f, Depth + 0.12f) },
            MaterialOverride = MakeMaterial(new Color(0.01f, 0.012f, 0.012f, 0.82f)),
            Visible = false
        };
        AddChild(progressBack);

        progressFill = new MeshInstance3D
        {
            Name = "ProgressFill",
            Mesh = new BoxMesh { Size = new Vector3(Width, 0.10f, Depth) },
            MaterialOverride = MakeMaterial(new Color(0.18f, 0.62f, 0.92f, 0.96f)),
            Visible = false
        };
        AddChild(progressFill);
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
        
        // 检测是否为建造中/重建中的建筑，展示对应的 3D 进度条；或者单位有等离子护盾
        var parent = GetParent();
        var showProgress = false;
        var progressRatio = 0f;
        if (parent is RtsBuilding building && (building.UnderConstruction || building.IsRebuilding))
        {
            showProgress = true;
            progressRatio = building.UnderConstruction ? building.ConstructionProgress : building.RebuildProgress;
            progressFill.MaterialOverride = MakeMaterial(new Color(0.18f, 0.62f, 0.92f, 0.96f));
        }
        else if (parent is RtsUnit unit && unit.MaxShield > 0f)
        {
            showProgress = true;
            progressRatio = Mathf.Clamp(unit.Shield / unit.MaxShield, 0f, 1f);
            progressFill.MaterialOverride = MakeMaterial(new Color(0.0f, 0.72f, 1.0f, 0.98f)); // 纯科技炫蓝色
        }

        if (showProgress)
        {
            // 建造中/回盾中：将血条偏移，并在并排位置显示进度条
            var zOffset = Depth + 0.12f;
            back.Position = new Vector3(0f, 0f, -zOffset * 0.5f);
            fill.Position = new Vector3((ratio - 1f) * Width * 0.5f, 0.07f, -zOffset * 0.5f);
            
            progressBack.Visible = true;
            progressFill.Visible = true;
            progressBack.Position = new Vector3(0f, 0f, zOffset * 0.5f);
            progressFill.Position = new Vector3((progressRatio - 1f) * Width * 0.5f, 0.07f, zOffset * 0.5f);
            progressFill.Scale = new Vector3(Mathf.Max(0.01f, progressRatio), 1f, 1f);
            
            Visible = selected || ratio < 0.995f || progressRatio < 0.995f;
        }
        else
        {
            // 正常状态：血条居中，隐藏进度条
            back.Position = Vector3.Zero;
            fill.Position = new Vector3((ratio - 1f) * Width * 0.5f, 0.07f, 0f);
            
            progressBack.Visible = false;
            progressFill.Visible = false;
            
            Visible = selected || ratio < 0.995f;
        }

        fill.Scale = new Vector3(Mathf.Max(0.01f, ratio), 1f, 1f);
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

    static readonly System.Collections.Concurrent.ConcurrentDictionary<Color, StandardMaterial3D> materialCache = new();

    static StandardMaterial3D MakeMaterial(Color color)
    {
        return materialCache.GetOrAdd(color, col => new StandardMaterial3D
        {
            AlbedoColor = col,
            Roughness = 0.85f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            RenderPriority = 10
        });
    }
}
