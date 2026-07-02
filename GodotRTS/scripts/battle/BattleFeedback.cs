using Godot;
using System;

public partial class BattleFeedback : Node
{
    public static BattleFeedback? Instance { get; private set; }

    readonly RandomNumberGenerator rng = new();

    public override void _Ready()
    {
        Instance = this;
        rng.Randomize();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Ensure(Node owner)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        if (root.GetNodeOrNull<BattleFeedback>("BattleFeedback") is not null)
            return;
        root.AddChild(new BattleFeedback { Name = "BattleFeedback" });
    }

    public static void Damage(Node owner, Vector3 worldPosition, float amount, bool targetPlayerOwned, bool lethal = false)
    {
        Ensure(owner);
        Instance?.SpawnFloatingText(worldPosition, $"-{Mathf.RoundToInt(amount)}", targetPlayerOwned ? new Color(1f, 0.36f, 0.30f) : new Color(1f, 0.78f, 0.28f), lethal ? 1.35f : 1f);
        Instance?.PlayProceduralTone(worldPosition, lethal ? 92f : 138f, lethal ? 0.30f : 0.16f, lethal ? 0.36f : 0.22f, lethal ? Wave.Noise : Wave.Square);
    }

    public static void Repair(Node owner, Vector3 worldPosition, float amount)
    {
        if (amount <= 0f)
            return;
        Ensure(owner);
        Instance?.SpawnFloatingText(worldPosition, $"+{Mathf.RoundToInt(amount)}", new Color(0.42f, 1f, 0.58f), 0.92f);
        Instance?.PlayProceduralTone(worldPosition, 420f, 0.10f, 0.12f, Wave.Sine);
    }

    public static void WeaponFire(Node owner, Vector3 worldPosition, bool heavy)
    {
        Ensure(owner);
        Instance?.PlayProceduralTone(worldPosition, heavy ? 72f : 190f, heavy ? 0.18f : 0.08f, heavy ? 0.24f : 0.15f, heavy ? Wave.Noise : Wave.Square);
    }

    public static void Command(Node owner, Vector3 worldPosition, string text, Color color)
    {
        Ensure(owner);
        Instance?.SpawnCommandMarker(worldPosition, text, color);
        Instance?.PlayProceduralTone(worldPosition, 520f, 0.055f, 0.045f, Wave.Sine);
    }

    public static void Impact(Node owner, Vector3 worldPosition, bool heavy)
    {
        Ensure(owner);
        Instance?.PlayProceduralTone(worldPosition, heavy ? 96f : 150f, heavy ? 0.22f : 0.11f, heavy ? 0.30f : 0.18f, Wave.Noise);
    }

    void SpawnFloatingText(Vector3 worldPosition, string text, Color color, float scale)
    {
        var root = GetTree().CurrentScene ?? this;
        var label = new Label3D
        {
            Name = "FloatingCombatText",
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FixedSize = true,
            PixelSize = 0.0065f * scale,
            Modulate = color,
            OutlineModulate = new Color(0f, 0f, 0f, 0.86f),
            OutlineSize = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        root.AddChild(label);
        label.GlobalPosition = worldPosition + new Vector3(rng.RandfRange(-0.35f, 0.35f), rng.RandfRange(1.35f, 2.15f), rng.RandfRange(-0.25f, 0.25f));

        var end = label.Position + new Vector3(rng.RandfRange(-0.18f, 0.18f), 1.35f, 0f);
        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", end, 0.72).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(label, "modulate:a", 0f, 0.72).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree)).SetDelay(0.72);
    }

    void SpawnCommandMarker(Vector3 worldPosition, string text, Color color)
    {
        var root = GetTree().CurrentScene ?? this;
        var marker = new MeshInstance3D
        {
            Name = "CommandMarker",
            Mesh = new CylinderMesh
            {
                TopRadius = 1.15f,
                BottomRadius = 1.15f,
                Height = 0.035f,
                RadialSegments = 40
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(color.R, color.G, color.B, 0.24f),
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = 0.45f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            }
        };
        root.AddChild(marker);
        marker.GlobalPosition = new Vector3(worldPosition.X, 0.06f, worldPosition.Z);

        var label = new Label3D
        {
            Name = "CommandMarkerLabel",
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FixedSize = true,
            PixelSize = 0.0058f,
            Modulate = color,
            OutlineModulate = new Color(0f, 0f, 0f, 0.88f),
            OutlineSize = 7,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        root.AddChild(label);
        label.GlobalPosition = new Vector3(worldPosition.X, 1.2f, worldPosition.Z);

        var tween = marker.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(marker, "scale", new Vector3(1.45f, 1f, 1.45f), 0.34).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(label, "position:y", label.Position.Y + 0.65f, 0.50).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.50).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(marker.QueueFree)).SetDelay(0.52);
        tween.TweenCallback(Callable.From(label.QueueFree)).SetDelay(0.52);
    }

    void PlayProceduralTone(Vector3 worldPosition, float baseFrequency, float duration, float volume, Wave wave)
    {
        var player = new AudioStreamPlayer3D
        {
            Name = "ProceduralBattleSfx",
            Stream = CreateTone(baseFrequency * rng.RandfRange(0.92f, 1.08f), duration, volume, wave),
            UnitSize = 18f,
            MaxDistance = 90f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            Bus = "Master"
        };
        AddChild(player);
        player.GlobalPosition = worldPosition;
        player.Finished += player.QueueFree;
        player.Play();
    }

    static AudioStreamWav CreateTone(float frequency, float duration, float volume, Wave wave)
    {
        const int sampleRate = 22050;
        var samples = Math.Max(1, (int)(duration * sampleRate));
        var data = new byte[samples * 2];
        var noise = new Random((int)(frequency * 97f + duration * 1000f));

        for (var i = 0; i < samples; i++)
        {
            var t = i / (float)sampleRate;
            var env = Mathf.Clamp(1f - i / (float)samples, 0f, 1f);
            env *= env;
            var sample = wave switch
            {
                Wave.Sine => Mathf.Sin(Mathf.Tau * frequency * t),
                Wave.Square => Mathf.Sign(Mathf.Sin(Mathf.Tau * frequency * t)),
                _ => (float)(noise.NextDouble() * 2.0 - 1.0)
            };
            var value = Mathf.Clamp(sample * env * volume, -1f, 1f);
            var pcm = (short)(value * short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xff);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            Data = data
        };
    }

    enum Wave
    {
        Sine,
        Square,
        Noise
    }
}
