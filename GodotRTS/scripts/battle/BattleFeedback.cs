using Godot;
using System;
using System.Linq;

public partial class BattleFeedback : Node
{
    const float FloatingTextPixelSize = 0.00058f;
    const float CommandMarkerPixelSize = 0.00082f;
    const float UiBusVolumeDb = -7f;
    const float WorldBusVolumeDb = -4f;

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
        Instance?.PlayDamageSfx(worldPosition, targetPlayerOwned, lethal);

        // Trigger HUD invasion warning for player-owned units or buildings
        if (targetPlayerOwned && amount > 0.5f && owner.IsInsideTree())
        {
            var hud = owner.GetTree().CurrentScene?.GetNodeOrNull<BattleHud>("HUD");
            if (hud is not null)
            {
                bool isMainBase = false;
                if (owner is RtsBuilding b && b.IsMainBase)
                    isMainBase = true;

                string warningMsg = isMainBase 
                    ? "【紧急警报】主基地正在遭受敌军直接打击！请火速守卫！" 
                    : "【战况警告】我方单位或前线建筑遭受敌军入侵攻击！";

                hud.TriggerInvasionAlert(warningMsg, isMainBase);
            }
        }
    }

    public static void Repair(Node owner, Vector3 worldPosition, float amount)
    {
        if (amount <= 0f)
            return;
        Ensure(owner);
        Instance?.SpawnFloatingText(worldPosition, $"+{Mathf.RoundToInt(amount)}", new Color(0.42f, 1f, 0.58f), 0.92f);
        Instance?.PlayRepairSfx(worldPosition);
    }

    public static void Loot(Node owner, Vector3 worldPosition, float amount)
    {
        if (amount <= 0f)
            return;
        Ensure(owner);
        Instance?.SpawnFloatingText(worldPosition, $"+{Mathf.RoundToInt(amount)} 金币", new Color(0.95f, 0.78f, 0.24f), 1.15f);
    }

    public static void WeaponFire(Node owner, Vector3 worldPosition, string profile, bool heavy)
    {
        Ensure(owner);
        Instance?.PlayWeaponFireSfx(worldPosition, profile, heavy);
    }

    public static void Command(Node owner, Vector3 worldPosition, string text, Color color)
    {
        Ensure(owner);
        Instance?.SpawnCommandMarker(worldPosition, text, color);
        Instance?.PlayCommandSfx(worldPosition, text);
    }

    public static void Impact(Node owner, Vector3 worldPosition, bool heavy)
    {
        Ensure(owner);
        Instance?.PlayImpactSfx(worldPosition, heavy);
    }

    public static void Destroyed(Node owner, Vector3 worldPosition, bool heavy)
    {
        Ensure(owner);
        Instance?.PlayDestroyedSfx(worldPosition, heavy);
    }

    public static void Selection(Node owner, int selectedCount, bool containsPlayerOwned, bool containsEnemy)
    {
        if (selectedCount <= 0)
            return;
        Ensure(owner);
        Instance?.PlaySelectionSfx(selectedCount, containsPlayerOwned, containsEnemy);
    }

    public static void UiCancel(Node owner)
    {
        Ensure(owner);
        Instance?.PlayUiSfx(
            CreateLayeredTone(
                new ToneLayer(240f, 0.08f, 0.15f, Wave.Square, 0f, 0.020f, 0.010f),
                new ToneLayer(180f, 0.11f, 0.11f, Wave.Sine, 0f, 0.018f, 0.026f)),
            0.06f);
    }

    public static void UiConfirm(Node owner)
    {
        Ensure(owner);
        Instance?.PlayUiSfx(
            CreateLayeredTone(
                new ToneLayer(520f, 0.05f, 0.10f, Wave.Sine, 0f, 0.010f, 0.012f),
                new ToneLayer(720f, 0.06f, 0.07f, Wave.Triangle, 0.004f, 0.008f, 0.018f)),
            0.02f);
    }

    void SpawnFloatingText(Vector3 worldPosition, string text, Color color, float scale)
    {
        var root = GetTree().CurrentScene ?? this;
        var lengthScale = text.Length switch
        {
            >= 5 => 0.70f,
            4 => 0.80f,
            _ => 1f
        };
        var label = new Label3D
        {
            Name = "FloatingCombatText",
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FixedSize = true,
            FontSize = 30,
            PixelSize = FloatingTextPixelSize * scale * lengthScale,
            Modulate = color,
            OutlineModulate = new Color(0f, 0f, 0f, 0.86f),
            OutlineSize = text.Length >= 4 ? 2 : 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        root.AddChild(label);
        label.GlobalPosition = worldPosition + new Vector3(rng.RandfRange(-0.30f, 0.30f), rng.RandfRange(1.10f, 1.72f), rng.RandfRange(-0.22f, 0.22f));

        var end = label.Position + new Vector3(rng.RandfRange(-0.14f, 0.14f), 0.92f, 0f);
        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", end, 0.62).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(label, "modulate:a", 0f, 0.62).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree)).SetDelay(0.62);
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
            FontSize = 28,
            PixelSize = CommandMarkerPixelSize,
            Modulate = color,
            OutlineModulate = new Color(0f, 0f, 0f, 0.88f),
            OutlineSize = 3,
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

    void PlayDamageSfx(Vector3 worldPosition, bool targetPlayerOwned, bool lethal)
    {
        var accent = targetPlayerOwned ? 154f : 178f;
        var stream = lethal
            ? CreateLayeredTone(
                new ToneLayer(86f, 0.34f, 0.40f, Wave.Noise, 0f, 0.030f, 0.080f),
                new ToneLayer(112f, 0.28f, 0.20f, Wave.Saw, 0.006f, 0.018f, 0.065f),
                new ToneLayer(accent, 0.22f, 0.13f, Wave.Square, 0.012f, 0.012f, 0.040f))
            : CreateLayeredTone(
                new ToneLayer(accent, 0.10f, 0.17f, Wave.Square, 0f, 0.010f, 0.018f),
                new ToneLayer(accent * 0.54f, 0.13f, 0.12f, Wave.Noise, 0.003f, 0.010f, 0.030f));
        PlayWorldSfx(worldPosition, stream, lethal ? 0.08f : 0.02f, lethal ? 82f : 56f);
    }

    void PlayRepairSfx(Vector3 worldPosition)
    {
        PlayWorldSfx(
            worldPosition,
            CreateLayeredTone(
                new ToneLayer(410f, 0.08f, 0.08f, Wave.Sine, 0f, 0.009f, 0.018f),
                new ToneLayer(620f, 0.09f, 0.05f, Wave.Triangle, 0.014f, 0.008f, 0.024f)),
            0.01f,
            42f);
    }

    void PlayWeaponFireSfx(Vector3 worldPosition, string profile, bool heavy)
    {
        var normalized = string.IsNullOrWhiteSpace(profile) ? "cannon" : profile.Trim().ToLowerInvariant();
        var stream = normalized switch
        {
            "rifle" => CreateLayeredTone(
                new ToneLayer(840f, 0.045f, 0.12f, Wave.Square, 0f, 0.0025f, 0.010f),
                new ToneLayer(160f, 0.060f, 0.10f, Wave.Noise, 0.002f, 0.004f, 0.020f)),
            "machinegun" => CreateLayeredTone(
                new ToneLayer(950f, 0.040f, 0.10f, Wave.Square, 0f, 0.0020f, 0.008f),
                new ToneLayer(240f, 0.055f, 0.08f, Wave.Noise, 0.0015f, 0.003f, 0.018f)),
            "flame" => CreateLayeredTone(
                new ToneLayer(120f, 0.16f, 0.18f, Wave.Noise, 0f, 0.010f, 0.040f),
                new ToneLayer(260f, 0.12f, 0.08f, Wave.Saw, 0.006f, 0.008f, 0.030f)),
            "aa" => CreateLayeredTone(
                new ToneLayer(1120f, 0.035f, 0.11f, Wave.Square, 0f, 0.0015f, 0.006f),
                new ToneLayer(210f, 0.045f, 0.07f, Wave.Noise, 0.002f, 0.003f, 0.014f)),
            "naval" => CreateLayeredTone(
                new ToneLayer(132f, 0.18f, 0.18f, Wave.Noise, 0f, 0.008f, 0.050f),
                new ToneLayer(78f, 0.24f, 0.16f, Wave.Saw, 0.005f, 0.012f, 0.070f),
                new ToneLayer(420f, 0.08f, 0.08f, Wave.Square, 0.012f, 0.003f, 0.024f)),
            "bomb" => CreateLayeredTone(
                new ToneLayer(140f, 0.16f, 0.16f, Wave.Saw, 0f, 0.008f, 0.045f),
                new ToneLayer(72f, 0.24f, 0.16f, Wave.Noise, 0.010f, 0.012f, 0.065f)),
            "artillery" => CreateLayeredTone(
                new ToneLayer(126f, 0.16f, 0.17f, Wave.Noise, 0f, 0.006f, 0.040f),
                new ToneLayer(92f, 0.22f, 0.14f, Wave.Saw, 0.004f, 0.010f, 0.065f),
                new ToneLayer(480f, 0.08f, 0.07f, Wave.Square, 0.010f, 0.003f, 0.018f)),
            _ => heavy
                ? CreateLayeredTone(
                    new ToneLayer(112f, 0.14f, 0.16f, Wave.Noise, 0f, 0.006f, 0.038f),
                    new ToneLayer(88f, 0.20f, 0.14f, Wave.Saw, 0.004f, 0.010f, 0.060f),
                    new ToneLayer(420f, 0.07f, 0.06f, Wave.Square, 0.010f, 0.003f, 0.018f))
                : CreateLayeredTone(
                    new ToneLayer(720f, 0.050f, 0.12f, Wave.Square, 0f, 0.0025f, 0.010f),
                    new ToneLayer(180f, 0.060f, 0.09f, Wave.Noise, 0.002f, 0.004f, 0.020f))
        };

        PlayWorldSfx(worldPosition, stream, 0.01f, heavy ? 84f : 60f);
    }

    void PlayCommandSfx(Vector3 worldPosition, string text)
    {
        var stream = text switch
        {
            "ATTACK" => CreateLayeredTone(
                new ToneLayer(420f, 0.05f, 0.10f, Wave.Square, 0f, 0.006f, 0.012f),
                new ToneLayer(560f, 0.06f, 0.07f, Wave.Saw, 0.010f, 0.006f, 0.016f)),
            "FIRE" or "BOMB" => CreateLayeredTone(
                new ToneLayer(360f, 0.06f, 0.10f, Wave.Square, 0f, 0.006f, 0.014f),
                new ToneLayer(220f, 0.08f, 0.08f, Wave.Saw, 0.005f, 0.008f, 0.020f)),
            "PATROL" => CreateLayeredTone(
                new ToneLayer(480f, 0.05f, 0.08f, Wave.Sine, 0f, 0.008f, 0.010f),
                new ToneLayer(660f, 0.08f, 0.06f, Wave.Triangle, 0.012f, 0.010f, 0.024f)),
            "RALLY" or "MOVE" or "A-MOVE" or "BUILD" => CreateLayeredTone(
                new ToneLayer(520f, 0.05f, 0.08f, Wave.Sine, 0f, 0.008f, 0.012f),
                new ToneLayer(760f, 0.06f, 0.05f, Wave.Triangle, 0.010f, 0.008f, 0.020f)),
            _ => CreateLayeredTone(new ToneLayer(520f, 0.05f, 0.07f, Wave.Sine, 0f, 0.008f, 0.012f))
        };

        PlayWorldSfx(worldPosition, stream, 0.01f, 62f);
    }

    void PlayImpactSfx(Vector3 worldPosition, bool heavy)
    {
        var stream = heavy
            ? CreateLayeredTone(
                new ToneLayer(90f, 0.28f, 0.28f, Wave.Noise, 0f, 0.010f, 0.070f),
                new ToneLayer(64f, 0.34f, 0.16f, Wave.Saw, 0.006f, 0.018f, 0.090f),
                new ToneLayer(190f, 0.14f, 0.09f, Wave.Square, 0.008f, 0.006f, 0.030f))
            : CreateLayeredTone(
                new ToneLayer(170f, 0.12f, 0.16f, Wave.Noise, 0f, 0.006f, 0.028f),
                new ToneLayer(240f, 0.08f, 0.06f, Wave.Square, 0.004f, 0.004f, 0.014f));

        PlayWorldSfx(worldPosition, stream, 0.03f, heavy ? 88f : 58f);
    }

    void PlayDestroyedSfx(Vector3 worldPosition, bool heavy)
    {
        var stream = CreateLayeredTone(
            new ToneLayer(72f, heavy ? 0.46f : 0.32f, heavy ? 0.36f : 0.24f, Wave.Noise, 0f, 0.014f, heavy ? 0.11f : 0.08f),
            new ToneLayer(48f, heavy ? 0.58f : 0.40f, heavy ? 0.20f : 0.14f, Wave.Saw, 0.010f, 0.020f, heavy ? 0.16f : 0.12f),
            new ToneLayer(138f, heavy ? 0.16f : 0.12f, 0.08f, Wave.Square, 0.018f, 0.006f, 0.040f));

        PlayWorldSfx(worldPosition, stream, 0.09f, heavy ? 96f : 76f);
    }

    void PlaySelectionSfx(int selectedCount, bool containsPlayerOwned, bool containsEnemy)
    {
        if (!containsPlayerOwned && !containsEnemy)
            return;

        var pitch = containsEnemy && !containsPlayerOwned
            ? 320f
            : selectedCount >= 4
                ? 540f
                : 620f;
        var accent = containsEnemy && !containsPlayerOwned
            ? new ToneLayer(pitch * 0.72f, 0.08f, 0.08f, Wave.Square, 0.006f, 0.010f, 0.020f)
            : new ToneLayer(pitch * 1.24f, 0.07f, 0.05f, Wave.Triangle, 0.012f, 0.008f, 0.018f);

        PlayUiSfx(
            CreateLayeredTone(
                new ToneLayer(pitch, 0.06f, 0.08f, Wave.Sine, 0f, 0.010f, 0.014f),
                accent),
            0.01f);
    }

    void PlayWorldSfx(Vector3 worldPosition, AudioStream stream, float pitchVariance, float maxDistance)
    {
        var player = new AudioStreamPlayer3D
        {
            Name = "ProceduralBattleSfx",
            Stream = stream,
            UnitSize = 18f,
            MaxDistance = maxDistance,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            Bus = "Master",
            VolumeDb = WorldBusVolumeDb,
            PitchScale = 1f + rng.RandfRange(-pitchVariance, pitchVariance)
        };
        AddChild(player);
        player.GlobalPosition = worldPosition;
        player.Finished += player.QueueFree;
        player.Play();
    }

    void PlayUiSfx(AudioStream stream, float pitchVariance)
    {
        var player = new AudioStreamPlayer
        {
            Name = "ProceduralUiSfx",
            Stream = stream,
            Bus = "Master",
            VolumeDb = UiBusVolumeDb,
            PitchScale = 1f + rng.RandfRange(-pitchVariance, pitchVariance)
        };
        AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    static AudioStreamWav CreateTone(float frequency, float duration, float volume, Wave wave)
        => CreateLayeredTone(new ToneLayer(frequency, duration, volume, wave, 0f, duration * 0.12f, duration * 0.26f));

    static AudioStreamWav CreateLayeredTone(params ToneLayer[] layers)
    {
        const int sampleRate = 22050;
        if (layers.Length == 0)
            return CreateSilentTone(sampleRate);

        var duration = layers.Max(layer => layer.StartTime + layer.Duration);
        var samples = Math.Max(1, (int)(duration * sampleRate));
        var data = new byte[samples * 2];
        var noises = layers.Select((layer, index) => new Random((int)(layer.Frequency * 97f + layer.Duration * 1000f + index * 17f))).ToArray();

        for (var i = 0; i < samples; i++)
        {
            var t = i / (float)sampleRate;
            var mixed = 0f;
            for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                var layer = layers[layerIndex];
                var localTime = t - layer.StartTime;
                if (localTime < 0f || localTime > layer.Duration)
                    continue;

                var env = LayerEnvelope(localTime, layer.Duration, layer.AttackTime, layer.ReleaseTime);
                if (env <= 0f)
                    continue;

                var sample = WaveSample(layer.Wave, layer.Frequency, localTime, noises[layerIndex]);
                mixed += sample * env * layer.Volume;
            }

            var value = Mathf.Clamp(mixed, -1f, 1f);
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

    static AudioStreamWav CreateSilentTone(int sampleRate)
    {
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            Data = new byte[2]
        };
    }

    static float LayerEnvelope(float time, float duration, float attackTime, float releaseTime)
    {
        if (duration <= 0f)
            return 0f;

        var attack = Mathf.Clamp(attackTime, 0.0005f, duration);
        var release = Mathf.Clamp(releaseTime, 0.0005f, duration);
        var sustainEnd = Mathf.Max(attack, duration - release);
        if (time < attack)
            return time / attack;
        if (time < sustainEnd)
            return 1f;
        return Mathf.Clamp(1f - (time - sustainEnd) / release, 0f, 1f);
    }

    static float WaveSample(Wave wave, float frequency, float time, Random noise)
    {
        return wave switch
        {
            Wave.Sine => Mathf.Sin(Mathf.Tau * frequency * time),
            Wave.Square => Mathf.Sign(Mathf.Sin(Mathf.Tau * frequency * time)),
            Wave.Saw => Mathf.PosMod(time * frequency, 1f) * 2f - 1f,
            Wave.Triangle => Mathf.Abs(Mathf.PosMod(time * frequency, 1f) * 2f - 1f) * 2f - 1f,
            _ => (float)(noise.NextDouble() * 2.0 - 1.0)
        };
    }

    enum Wave
    {
        Sine,
        Square,
        Saw,
        Triangle,
        Noise
    }

    readonly record struct ToneLayer(
        float Frequency,
        float Duration,
        float Volume,
        Wave Wave,
        float StartTime,
        float AttackTime,
        float ReleaseTime);
}
