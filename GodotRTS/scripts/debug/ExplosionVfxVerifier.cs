using Godot;
using System;
using System.Collections.Generic;

namespace GodotRTS.Debug;

public partial class ExplosionVfxVerifier : Node3D
{
    Camera3D? camera;
    Label? infoLabel;
    Label? statusLabel;
    int maxCaptureFrames = 1860; // 默认 31 秒 @ 60 FPS
    int currentFrame = 0;
    float elapsedTime = 0f;
    float nextExplosionTimer = 0.5f;
    int cycleStep = 0;
    readonly Random rng = new();

    public override void _EnterTree()
    {
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableWaterCheck = true;
    }

    public override void _Ready()
    {
        EffectPool.Ensure(this);
        BattleFeedback.Ensure(this);

        // 命令行参数解析
        var args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--capture-frames" && i + 1 < args.Length && int.TryParse(args[i + 1], out var targetFrames))
            {
                maxCaptureFrames = targetFrames;
            }
        }

        SetupEnvironment();
        SetupCamera();
        SetupUiOverlay();

        GD.Print($"[ExplosionVfxVerifier] Ready. Target frames: {maxCaptureFrames}");
    }

    void SetupEnvironment()
    {
        // 1. 阳光 (DirectionalLight3D)
        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightColor = new Color(1.0f, 0.95f, 0.88f),
            LightEnergy = 1.15f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-42f, 35f, 0f)
        };
        AddChild(sun);

        // 2. 环境光 (WorldEnvironment)
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.12f, 0.14f, 0.18f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.32f, 0.35f, 0.40f),
            AmbientLightEnergy = 0.85f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.0f
        };
        var worldEnv = new WorldEnvironment { Environment = env };
        AddChild(worldEnv);

        // 3. 真实地表战区面片 (Ground Plane)
        var groundMesh = new PlaneMesh { Size = new Vector2(160f, 160f) };
        var groundMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.24f, 0.22f, 0.19f),
            Roughness = 0.92f,
            Metallic = 0.05f
        };
        var ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = groundMesh,
            MaterialOverride = groundMat,
            Position = Vector3.Zero
        };
        AddChild(ground);

        // 4. 地表参考物 (坦克装甲残骸、障碍沙袋与路标，用于对比爆炸尺度与光照反射)
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            var marker = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.2f, 0.8f, 2.2f) },
                Position = new Vector3(i * 5.5f, 0.4f, -4.5f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.32f, 0.36f, 0.28f),
                    Roughness = 0.45f,
                    Metallic = 0.75f
                }
            };
            AddChild(marker);
        }
    }

    void SetupCamera()
    {
        camera = new Camera3D
        {
            Name = "MainCamera",
            Fov = 48f,
            Near = 0.3f,
            Far = 300f,
            Position = new Vector3(0f, 5.8f, 14.5f)
        };
        camera.LookAt(new Vector3(0f, 1.2f, 0f), Vector3.Up);
        AddChild(camera);
    }

    void SetupUiOverlay()
    {
        var canvas = new CanvasLayer { Name = "HUD" };
        AddChild(canvas);

        var bgPanel = new Panel
        {
            Position = new Vector2(24, 20),
            Size = new Vector2(580, 140)
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.12f, 0.82f),
            BorderColor = new Color(1.0f, 0.65f, 0.15f, 0.9f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        bgPanel.AddThemeStyleboxOverride("panel", style);
        canvas.AddChild(bgPanel);

        infoLabel = new Label
        {
            Position = new Vector2(36, 28),
            Size = new Vector2(550, 40),
            Text = "🔥 RTS 爆炸粒子特效拟真度验证 (Realistic Explosion VFX)"
        };
        infoLabel.AddThemeFontSizeOverride("font_size", 18);
        infoLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.32f));
        canvas.AddChild(infoLabel);

        statusLabel = new Label
        {
            Position = new Vector2(36, 68),
            Size = new Vector2(550, 80),
            Text = "色彩梯度: 白炽金黄 ➔ 烈焰真橙 ➔ 暗红余烬 ➔ 冷却炭烟\n贴图机制: 64x64 Hermite 径向羽化 (无白边/白框)\n混合通道: Alpha Mix 混合 (彻底消除白光过曝饱和)"
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 14);
        statusLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.95f));
        canvas.AddChild(statusLabel);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        elapsedTime += dt;
        currentFrame++;

        // 动态微调镜头以多角度展示爆炸体积
        if (camera is not null)
        {
            float camYaw = Mathf.Sin(elapsedTime * 0.22f) * 4.5f;
            float camY = 5.5f + Mathf.Cos(elapsedTime * 0.15f) * 1.2f;
            camera.Position = new Vector3(camYaw, camY, 14.2f);
            camera.LookAt(new Vector3(0f, 1.2f, 0f), Vector3.Up);
        }

        nextExplosionTimer -= dt;
        if (nextExplosionTimer <= 0f)
        {
            TriggerNextExplosionSequence();
        }

        // 截取高质量验证图
        if (currentFrame == 120 || currentFrame == 260 || currentFrame == 450 || currentFrame == 720)
        {
            CaptureVerificationScreenshot(currentFrame);
        }

        // 录制帧数达到后退出
        if (maxCaptureFrames > 0 && currentFrame >= maxCaptureFrames)
        {
            GD.Print($"[ExplosionVfxVerifier] Completed {currentFrame} frames. Exiting.");
            GetTree().Quit(0);
        }
    }

    void TriggerNextExplosionSequence()
    {
        cycleStep++;
        int mode = cycleStep % 6;

        switch (mode)
        {
            case 0:
                // 155mm 重型高爆炮弹爆炸 (地面激波 + 金黄/真橙烈焰 + 抛洒碎石泥土 + 滚滚黑烟)
                EffectPool.Instance?.SpawnExplosion(new Vector3(-2.8f, 0.1f, 0.5f), new Color(1.0f, 0.52f, 0.12f), true);
                if (statusLabel != null)
                    statusLabel.Text = "当前引爆: 【155mm 重装高爆弹】\n表现: 浓郁金橙色膨胀火球 + 抛洒碎土 + 滚滚深黑硝烟\n混合: Alpha Mix (零纯白过曝，边缘柔和)";
                nextExplosionTimer = 1.4f;
                break;

            case 1:
                // 500磅战术重磅航弹轰炸 (巨型烈焰火柱 + 宽幅橙光冲击波环 + 飞溅铜色破片火星)
                EffectPool.Instance?.SpawnAerialBombExplosion(new Vector3(2.5f, 0.1f, -0.8f), new Color(1.0f, 0.48f, 0.10f));
                if (statusLabel != null)
                    statusLabel.Text = "当前引爆: 【500磅航空高爆航弹】\n表现: 巨型橙红烈焰火球 + 宽幅激波环 + 飞溅铜红火星破片\n光照: 温暖写实火光动态照亮战车与地面";
                nextExplosionTimer = 1.8f;
                break;

            case 2:
                // 战术防空高炮破片空爆 (Flak Airburst)
                EffectPool.Instance?.SpawnFlakAirburst(new Vector3(-1.5f, 4.2f, 0.2f), new Color(1.0f, 0.65f, 0.15f));
                EffectPool.Instance?.SpawnFlakAirburst(new Vector3(1.2f, 4.8f, -0.5f), new Color(1.0f, 0.55f, 0.12f));
                if (statusLabel != null)
                    statusLabel.Text = "当前引爆: 【35mm 高射炮空中截击破片群】\n表现: 高空骤爆橙红火球 + 高速向外迸射破片 + 黑色膨胀烟团\n效果: 空中截击无死白方块";
                nextExplosionTimer = 1.2f;
                break;

            case 3:
                // 3联装连续炮击轰炸 (Artillery Salvo)
                EffectPool.Instance?.SpawnExplosion(new Vector3(-4.0f, 0.1f, -1.2f), new Color(1.0f, 0.55f, 0.14f), false);
                EffectPool.Instance?.SpawnExplosion(new Vector3(0.0f, 0.1f, 0.8f), new Color(1.0f, 0.50f, 0.12f), true);
                EffectPool.Instance?.SpawnExplosion(new Vector3(4.2f, 0.1f, -1.0f), new Color(1.0f, 0.52f, 0.12f), false);
                if (statusLabel != null)
                    statusLabel.Text = "当前引爆: 【多管火炮急速齐射连击】\n表现: 三处落点连续爆轰，火球叠合依然保持浓郁橙红\n验证: 密集粒子交叠时绝不产生白色色块";
                nextExplosionTimer = 1.6f;
                break;

            case 4:
                // 航弹连环地毯式轰炸 (Carpet Bombing)
                EffectPool.Instance?.SpawnAerialBombExplosion(new Vector3(-3.2f, 0.1f, 0.2f), new Color(1.0f, 0.45f, 0.08f));
                EffectPool.Instance?.SpawnAerialBombExplosion(new Vector3(3.2f, 0.1f, 0.5f), new Color(1.0f, 0.45f, 0.08f));
                if (statusLabel != null)
                    statusLabel.Text = "当前引爆: 【战术轰炸机双弹连击】\n表现: 双重爆心火浪扩散，地面留下柔和黑灰灼烧焦痕\n验证: 激波与焦痕均采用羽化无白边贴图";
                nextExplosionTimer = 1.9f;
                break;

            case 5:
                // 战车殉爆与残骸火焰燃耗 (Vehicle Cook-off Explosion)
                EffectPool.Instance?.SpawnExplosion(new Vector3(0.5f, 0.1f, -0.2f), new Color(1.0f, 0.58f, 0.15f), true);
                if (statusLabel != null)
                    statusLabel.Text = "当前引爆: 【装甲战车被击毁殉爆】\n表现: 弹药架引爆喷出烈火与浓烟，火星四射\n验证: 彻底告别原版刺眼纯白方块";
                nextExplosionTimer = 1.5f;
                break;
        }
    }

    void CaptureVerificationScreenshot(int frame)
    {
        try
        {
            var image = GetViewport().GetTexture().GetImage();
            if (image is not null)
            {
                string path1 = $"screenshots/explosion_vfx_frame_{frame}.png";
                string path2 = $"scratch/explosion_vfx_frame_{frame}.png";
                image.SavePng(path1);
                image.SavePng(path2);
                GD.Print($"[ExplosionVfxVerifier] Saved screenshot at frame {frame} -> {path1}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ExplosionVfxVerifier] Screenshot capture error: {ex.Message}");
        }
    }
}
