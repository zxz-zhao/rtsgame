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
    readonly System.Collections.Generic.Dictionary<string, AudioStream> cachedSamples = new();

    public override void _Ready()
    {
        Instance = this;
        rng.Randomize();
        PreloadCombatAudio();
    }

    void PreloadCombatAudio()
    {
        string[] sampleNames =
        {
            "bomb_explosion", "heavy_explosion", "medium_explosion", "water_spout_explosion",
            "bomb_whistle", "bomb_release", "cannon_fire", "heavy_artillery_fire",
            "naval_cannon_fire", "missile_launch", "rifle_fire", "machinegun_fire",
            "aa_cannon_fire", "armor_pierce_impact", "ricochet"
        };

        foreach (var name in sampleNames)
        {
            string path = $"res://assets/audio/combat/{name}.wav";
            if (ResourceLoader.Exists(path))
            {
                try
                {
                    var stream = ResourceLoader.Load<AudioStream>(path);
                    if (stream is not null)
                    {
                        cachedSamples[name] = stream;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[BattleFeedback] Error loading sample {path}: {ex.Message}");
                }
            }
        }
    }

    AudioStream? GetSample(string name)
    {
        return cachedSamples.TryGetValue(name, out var s) ? s : null;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Ensure(Node owner)
    {
        if (Instance is not null && GodotObject.IsInstanceValid(Instance))
            return;

        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;
        if (root.GetNodeOrNull<BattleFeedback>("BattleFeedback") is { } existing)
        {
            Instance = existing;
            return;
        }

        var feedback = new BattleFeedback { Name = "BattleFeedback" };
        if (root.IsNodeReady())
        {
            root.AddChild(feedback);
        }
        else
        {
            root.CallDeferred(Node.MethodName.AddChild, feedback);
        }
    }

    public static void Damage(
        Node owner,
        Vector3 worldPosition,
        float amount,
        bool targetPlayerOwned,
        bool lethal = false,
        bool isCrit = false,
        bool isRearHit = false,
        bool isRicochet = false)
    {
        Ensure(owner);
        string text;
        Color textColor;
        float textScale;

        if (isRicochet)
        {
            text = $"跳弹 -{Mathf.RoundToInt(amount)}";
            textColor = new Color(0.72f, 0.88f, 1.0f);
            textScale = 0.90f;
            Instance?.PlayRicochetSfx(worldPosition);
        }
        else if (isCrit)
        {
            text = $"暴击! -{Mathf.RoundToInt(amount)}";
            textColor = new Color(1.0f, 0.88f, 0.15f);
            textScale = lethal ? 1.55f : 1.35f;
            Instance?.PlayCriticalSfx(worldPosition);
            Instance?.PlayDamageSfx(worldPosition, targetPlayerOwned, lethal);
        }
        else if (isRearHit)
        {
            text = $"背击 -{Mathf.RoundToInt(amount)}";
            textColor = new Color(1.0f, 0.42f, 0.18f);
            textScale = lethal ? 1.45f : 1.25f;
            Instance?.PlayDamageSfx(worldPosition, targetPlayerOwned, lethal);
        }
        else
        {
            text = $"-{Mathf.RoundToInt(amount)}";
            textColor = targetPlayerOwned ? new Color(1f, 0.36f, 0.30f) : new Color(1f, 0.78f, 0.28f);
            textScale = lethal ? 1.35f : 1f;
            Instance?.PlayDamageSfx(worldPosition, targetPlayerOwned, lethal);
        }

        Instance?.SpawnFloatingText(worldPosition, text, textColor, textScale);

        // Trigger HUD invasion warning and minimap alert for player-owned units or buildings
        if (targetPlayerOwned && amount > 0.5f && owner.IsInsideTree())
        {
            bool isMainBase = owner is RtsBuilding { IsMainBase: true };
            BattleMinimap.Instance?.NotifyUnderAttack(worldPosition, isMainBase);

            var hud = owner.GetTree().CurrentScene?.GetNodeOrNull<BattleHud>("HUD")
                ?? owner.GetTree().Root.GetNodeOrNull<BattleHud>("HUD");
            if (hud is not null)
            {
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

    public static void BombRelease(Vector3 worldPosition)
    {
        Instance?.PlayBombReleaseSfx(worldPosition);
    }

    public static void BombWhistle(Vector3 worldPosition, float duration)
    {
        Instance?.PlayBombWhistleSfx(worldPosition, duration);
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
        ScorchManager.SpawnScorch(owner, worldPosition, heavy ? 2.6f : 1.5f);
    }

    public static void WaterSpout(Node owner, Vector3 worldPosition)
    {
        Ensure(owner);
        Instance?.PlayWaterSpoutSfx(worldPosition);
    }

    public static void FlakAirburst(Node owner, Vector3 worldPosition)
    {
        Ensure(owner);
        Instance?.PlayFlakAirburstSfx(worldPosition);
    }

    void PlayFlakAirburstSfx(Vector3 worldPosition)
    {
        if (GetSample("aa_cannon_fire") is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.02f, 75f);
            return;
        }
        PlayImpactSfx(worldPosition, false);
    }

    public static void TriggerCameraShake(Vector3 worldPosition, float intensity = 0.08f, float duration = 0.30f)
    {
        if (RtsCamera.Instance is { } rtsCam && GodotObject.IsInstanceValid(rtsCam))
        {
            float dist = rtsCam.GlobalPosition.DistanceTo(worldPosition);
            float distFactor = Mathf.Clamp(1.0f - (dist - 12f) / 45f, 0f, 1f);
            float finalIntensity = intensity * distFactor;
            if (finalIntensity > 0.005f)
            {
                rtsCam.TriggerShake(Mathf.Min(finalIntensity, 0.09f), duration);
            }
        }
        else if (AutoCombatArenaController.Instance is { } arena && GodotObject.IsInstanceValid(arena))
        {
            Vector3 camPos = arena.ArenaCamera?.GlobalPosition ?? Vector3.Zero;
            float dist = camPos.DistanceTo(worldPosition);
            float distFactor = Mathf.Clamp(1.0f - (dist - 12f) / 45f, 0f, 1f);
            float finalIntensity = intensity * distFactor;
            if (finalIntensity > 0.005f)
            {
                arena.TriggerCameraShake(Mathf.Min(finalIntensity, 0.09f), duration);
            }
        }
    }

    public static void Destroyed(Node owner, Vector3 worldPosition, bool heavy)
    {
        Ensure(owner);
        Instance?.PlayDestroyedSfx(worldPosition, heavy);
        CombatProjectile.SpawnExplosion(owner, worldPosition, heavy ? new Color(1.0f, 0.45f, 0.1f) : new Color(1.0f, 0.7f, 0.2f), heavy);
        TriggerCameraShake(worldPosition, heavy ? 0.08f : 0.04f, heavy ? 0.30f : 0.20f);
        ScorchManager.SpawnScorch(owner, worldPosition, heavy ? 3.6f : 2.2f);
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

    public static void PlayEnemySpottedAlert()
    {
        if (Instance is null || !GodotObject.IsInstanceValid(Instance)) return;
        var stream = CreateLayeredTone(
            new ToneLayer(820f, 0.09f, 0.22f, Wave.Sine, 0f, 0.008f, 0.025f),
            new ToneLayer(1240f, 0.14f, 0.26f, Wave.Sine, 0.08f, 0.008f, 0.040f),
            new ToneLayer(1860f, 0.10f, 0.12f, Wave.Triangle, 0.10f, 0.005f, 0.030f)
        );
        Instance.PlayUiSfx(stream, 0.01f);
    }

    public static void PlayUnderAttackAlert()
    {
        if (Instance is null || !GodotObject.IsInstanceValid(Instance)) return;
        var stream = CreateLayeredTone(
            new ToneLayer(640f, 0.09f, 0.24f, Wave.Square, 0f, 0.005f, 0.020f),
            new ToneLayer(480f, 0.09f, 0.24f, Wave.Square, 0.09f, 0.005f, 0.020f),
            new ToneLayer(760f, 0.12f, 0.20f, Wave.Saw, 0.18f, 0.006f, 0.030f)
        );
        Instance.PlayUiSfx(stream, 0.01f);
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

    int activeFloatingTextCount;

    void SpawnFloatingText(Vector3 worldPosition, string text, Color color, float scale)
    {
        if (activeFloatingTextCount >= 24)
            return;

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
        activeFloatingTextCount++;
        label.GlobalPosition = worldPosition + new Vector3(rng.RandfRange(-0.30f, 0.30f), rng.RandfRange(1.10f, 1.72f), rng.RandfRange(-0.22f, 0.22f));

        var end = label.Position + new Vector3(rng.RandfRange(-0.14f, 0.14f), 0.92f, 0f);
        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", end, 0.62).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(label, "modulate:a", 0f, 0.62).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            activeFloatingTextCount--;
            if (activeFloatingTextCount < 0) activeFloatingTextCount = 0;
            label.QueueFree();
        })).SetDelay(0.62);
    }

    void SpawnCommandMarker(Vector3 worldPosition, string text, Color color)
    {
        var root = GetTree().CurrentScene ?? this;
        var upperText = text.Trim().ToUpperInvariant();
        bool isAttackCommand = upperText is "ATTACK" or "FIRE" or "BOMB";
        bool isStandardUnitOrder = upperText is "MOVE" or "ATTACK" or "FIRE" or "BOMB" or "PATROL" or "RALLY" or "DEFEND" or "BUILD";

        var markerRoot = new Node3D
        {
            Name = "CommandMarkerRoot"
        };
        root.AddChild(markerRoot);
        markerRoot.GlobalPosition = new Vector3(worldPosition.X, 0.08f, worldPosition.Z);

        if (isAttackCommand)
        {
            // === 攻击/开火/轰炸：军用战术锁定准星 (Military Combat Snap-Lock Reticle) ===
            var reticleColor = new Color(1.0f, 0.22f, 0.20f, 0.95f);
            var reticleMat = new StandardMaterial3D
            {
                AlbedoColor = reticleColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always
            };

            var outerRing = new MeshInstance3D
            {
                Name = "TargetOuterRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.95f,
                    OuterRadius = 1.08f,
                    RingSegments = 40
                },
                MaterialOverride = reticleMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            markerRoot.AddChild(outerRing);

            var innerRing = new MeshInstance3D
            {
                Name = "TargetInnerRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.42f,
                    OuterRadius = 0.50f,
                    RingSegments = 32
                },
                MaterialOverride = reticleMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            markerRoot.AddChild(innerRing);

            // 十字刻度线
            var crossH = new MeshInstance3D
            {
                Name = "CrossH",
                Mesh = new BoxMesh { Size = new Vector3(2.4f, 0.02f, 0.08f) },
                MaterialOverride = reticleMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            markerRoot.AddChild(crossH);

            var crossV = new MeshInstance3D
            {
                Name = "CrossV",
                Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.02f, 2.4f) },
                MaterialOverride = reticleMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            markerRoot.AddChild(crossV);

            markerRoot.Scale = new Vector3(1.85f, 1f, 1.85f);
            var tween = markerRoot.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(markerRoot, "scale", new Vector3(0.92f, 1f, 0.92f), 0.16)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(markerRoot, "rotation:y", Mathf.DegToRad(45f), 0.16)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);

            tween.Chain().TweenProperty(markerRoot, "scale", new Vector3(1.05f, 1f, 1.05f), 0.22)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            tween.Parallel().TweenProperty(reticleMat, "albedo_color:a", 0f, 0.22)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(markerRoot.QueueFree)).SetDelay(0.40);
        }
        else
        {
            // === 移动/巡逻/集结点/防守：高科技双环脉冲扩散波 (High-Tech Double Ground Pulse Ripple) ===
            var pulseColor = upperText switch
            {
                "RALLY" => new Color(1.0f, 0.85f, 0.25f, 0.95f),
                "DEFEND" => new Color(0.25f, 0.75f, 1.0f, 0.95f),
                "PATROL" => new Color(0.85f, 1.0f, 0.30f, 0.95f),
                _ => new Color(0.20f, 0.95f, 0.70f, 0.95f) // 经典科技青绿
            };

            var outerMat = new StandardMaterial3D
            {
                AlbedoColor = pulseColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always
            };

            var outerRing = new MeshInstance3D
            {
                Name = "PulseOuterRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.72f,
                    OuterRadius = 0.84f,
                    RingSegments = 36
                },
                MaterialOverride = outerMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            markerRoot.AddChild(outerRing);

            var innerMat = new StandardMaterial3D
            {
                AlbedoColor = pulseColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always
            };

            var innerRing = new MeshInstance3D
            {
                Name = "PulseInnerRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.32f,
                    OuterRadius = 0.40f,
                    RingSegments = 28
                },
                MaterialOverride = innerMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            markerRoot.AddChild(innerRing);

            outerRing.Scale = new Vector3(0.35f, 1f, 0.35f);
            innerRing.Scale = new Vector3(0.20f, 1f, 0.20f);

            var tween = markerRoot.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(outerRing, "scale", new Vector3(1.55f, 1f, 1.55f), 0.35)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(outerMat, "albedo_color:a", 0f, 0.35)
                .SetEase(Tween.EaseType.In);

            tween.TweenProperty(innerRing, "scale", new Vector3(1.10f, 1f, 1.10f), 0.28)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(innerMat, "albedo_color:a", 0f, 0.28)
                .SetEase(Tween.EaseType.In);

            tween.TweenCallback(Callable.From(markerRoot.QueueFree)).SetDelay(0.38);
        }

        // 仅在非标准单位指令（即关卡剧情横幅、特殊系统通告）时保留信息标签
        if (!isStandardUnitOrder && !string.IsNullOrWhiteSpace(text))
        {
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

            var labelTween = label.CreateTween();
            labelTween.SetParallel(true);
            labelTween.TweenProperty(label, "position:y", label.Position.Y + 0.65f, 0.50).SetEase(Tween.EaseType.Out);
            labelTween.TweenProperty(label, "modulate:a", 0f, 0.50).SetEase(Tween.EaseType.In);
            labelTween.TweenCallback(Callable.From(label.QueueFree)).SetDelay(0.52);
        }
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

    public void PlayRicochetSfx(Vector3 worldPosition)
    {
        if (GetSample("ricochet") is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.05f, 65f);
            return;
        }

        // 钢板跳弹清脆金属鸣响 (1280Hz -> 820Hz)
        var stream = CreateLayeredTone(
            new ToneLayer(1280f, 0.08f, 0.18f, Wave.Sine, 0f, 0.001f, 0.040f),
            new ToneLayer(960f, 0.06f, 0.12f, Wave.Triangle, 0.005f, 0.002f, 0.035f),
            new ToneLayer(320f, 0.04f, 0.06f, Wave.Noise, 0f, 0.001f, 0.020f));
        PlayWorldSfx(worldPosition, stream, 0.08f, 55f);
    }

    public void PlayCriticalSfx(Vector3 worldPosition)
    {
        if (GetSample("armor_pierce_impact") is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.04f, 82f);
            return;
        }

        // 暴击重击破甲冲击 (低频下潜震荡)
        var stream = CreateLayeredTone(
            new ToneLayer(95f, 0.25f, 0.32f, Wave.Triangle, 0f, 0.004f, 0.080f),
            new ToneLayer(180f, 0.14f, 0.22f, Wave.Square, 0.002f, 0.004f, 0.045f),
            new ToneLayer(80f, 0.30f, 0.18f, Wave.Noise, 0f, 0.008f, 0.060f));
        PlayWorldSfx(worldPosition, stream, 0.04f, 75f);
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

        // 优先使用高品质军工实录采样 (Hollywood Military Foley)
        string? sampleKey = normalized switch
        {
            "rifle" => "rifle_fire",
            "machinegun" => "machinegun_fire",
            "aa" => "aa_cannon_fire",
            "artillery" => "heavy_artillery_fire",
            "mortar" => "heavy_artillery_fire",
            "naval" => "naval_cannon_fire",
            "bomb" => "bomb_release",
            "cannon" => heavy ? "heavy_artillery_fire" : "cannon_fire",
            _ => heavy ? "cannon_fire" : "rifle_fire"
        };

        if (sampleKey is not null && GetSample(sampleKey) is { } sample)
        {
            PlayWorldSfx(worldPosition, sample, 0.04f, heavy ? 92f : 68f);
            return;
        }

        var stream = normalized switch
        {
            "rifle" => CreateLayeredTone(
                new ToneLayer(840f, 0.045f, 0.12f, Wave.Square, 0f, 0.0025f, 0.010f),
                new ToneLayer(160f, 0.060f, 0.10f, Wave.Noise, 0.002f, 0.004f, 0.020f)),
            "machinegun" => CreateLayeredTone(
                new ToneLayer(950f, 0.040f, 0.10f, Wave.Square, 0f, 0.0020f, 0.008f),
                new ToneLayer(240f, 0.055f, 0.08f, Wave.Noise, 0.0015f, 0.003f, 0.018f)),
            "mortar" => CreateLayeredTone(
                new ToneLayer(320f, 0.08f, 0.22f, Wave.Noise, 0f, 0.005f, 0.040f),
                new ToneLayer(90f, 0.18f, 0.25f, Wave.Sine, 0.002f, 0.010f, 0.060f)),
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
            "catapult" => CreateLayeredTone(
                new ToneLayer(320f, 0.28f, 0.22f, Wave.Noise, 0f, 0.012f, 0.080f),
                new ToneLayer(640f, 0.32f, 0.14f, Wave.Saw, 0.015f, 0.008f, 0.055f),
                new ToneLayer(110f, 0.25f, 0.18f, Wave.Sine, 0.010f, 0.015f, 0.065f)),
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

    void PlayBombReleaseSfx(Vector3 worldPosition)
    {
        if (GetSample("bomb_release") is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.03f, 78f);
            return;
        }

        var stream = CreateLayeredTone(
            new ToneLayer(460f, 0.06f, 0.16f, Wave.Triangle, 0f, 0.003f, 0.018f),
            new ToneLayer(115f, 0.24f, 0.22f, Wave.Noise, 0.008f, 0.012f, 0.065f),
            new ToneLayer(68f, 0.20f, 0.28f, Wave.Saw, 0f, 0.006f, 0.045f));
        PlayWorldSfx(worldPosition, stream, 0.02f, 75f);
    }

    void PlayBombWhistleSfx(Vector3 worldPosition, float duration)
    {
        if (GetSample("bomb_whistle") is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.02f, 88f);
            return;
        }

        float playDur = Mathf.Clamp(duration, 0.45f, 1.25f);
        var stream = CreateLayeredTone(
            new ToneLayer(820f, playDur, 0.16f, Wave.Sine, 0f, playDur * 0.2f, playDur * 0.15f),
            new ToneLayer(440f, playDur, 0.12f, Wave.Triangle, 0.04f, playDur * 0.25f, playDur * 0.12f),
            new ToneLayer(180f, playDur * 0.8f, 0.10f, Wave.Noise, 0.08f, playDur * 0.15f, playDur * 0.15f));
        PlayWorldSfx(worldPosition, stream, 0.03f, 80f);
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
        var sampleKey = heavy ? "heavy_explosion" : "medium_explosion";
        if (GetSample(sampleKey) is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.04f, heavy ? 95f : 65f);
            return;
        }

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

    void PlayWaterSpoutSfx(Vector3 worldPosition)
    {
        if (GetSample("water_spout_explosion") is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.04f, 98f);
            return;
        }
        PlayImpactSfx(worldPosition, true);
    }

    void PlayDestroyedSfx(Vector3 worldPosition, bool heavy)
    {
        var sampleKey = heavy ? "bomb_explosion" : "heavy_explosion";
        if (GetSample(sampleKey) is { } s)
        {
            PlayWorldSfx(worldPosition, s, 0.04f, heavy ? 105f : 78f);
            return;
        }

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
