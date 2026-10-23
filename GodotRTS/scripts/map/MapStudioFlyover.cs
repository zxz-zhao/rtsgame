using Godot;

public partial class MapStudioFlyover : Node3D
{
    [Export] public NodePath CameraRigPath { get; set; } = new("CameraRig");
    [Export] public float TotalDuration { get; set; } = 24.0f;

    struct FlyKeyframe
    {
        public float Time;
        public Vector3 CamPos;
        public Vector3 Focus;

        public FlyKeyframe(float time, Vector3 camPos, Vector3 focus)
        {
            Time = time;
            CamPos = camPos;
            Focus = focus;
        }
    }

    readonly FlyKeyframe[] keyframes = new[]
    {
        // 0. 盟军蓝方大本营与指挥台 (0.0s)
        new FlyKeyframe(0.00f, new Vector3(-260f, 105f, -260f), new Vector3(-190f, 10f, -190f)),
        // 1. 盟军中左/上路装甲与空军前沿集结点 (4.3s)
        new FlyKeyframe(0.18f, new Vector3(-210f, 85f, -115f), new Vector3(-235f, 6f, -145f)),
        // 2. 上路高峡险隘：俯瞰高台绝壁、高峡钢桥与装甲关隘残骸 (9.1s)
        new FlyKeyframe(0.38f, new Vector3(-160f, 72f, 45f), new Vector3(-115f, 10f, 115f)),
        // 3. 中路核心战场：低空掠过双侧路灯干道，特写中央要塞岛雷达塔与钢铁双主桥 (14.0s)
        new FlyKeyframe(0.58f, new Vector3(-45f, 65f, -45f), new Vector3(0f, 6f, 0f)),
        // 4. 下路深水三角洲海战场：掠过滨海战舰残骸、军港码头栈桥与棕榈群岛 (18.5s)
        new FlyKeyframe(0.77f, new Vector3(105f, 58f, -95f), new Vector3(155f, 0f, -155f)),
        // 5. 快速拉升爬升阶段 (21.6s)
        new FlyKeyframe(0.90f, new Vector3(60f, 180f, -180f), new Vector3(0f, 0f, 0f)),
        // 6. 宏伟全局大鸟瞰上帝视角 (24.0s: 700m x 700m 全域三路战线一览无余)
        new FlyKeyframe(1.00f, new Vector3(0f, 330f, -270f), new Vector3(0f, 0f, 0f)),
    };

    double timeElapsed = 0.0;
    Node3D? cameraRig;
    Camera3D? camera;

    int targetFrames = -1;
    int currentFrames = 0;

    public override void _Ready()
    {
        cameraRig = GetNodeOrNull<Node3D>(CameraRigPath);
        camera = cameraRig?.GetNodeOrNull<Camera3D>("Camera3D");
        if (camera != null)
        {
            camera.Far = 2200f;
            camera.Near = 1f;
            camera.Fov = 48f;
        }

        var args = CommandLineArgs.Get();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--capture-frames" && i + 1 < args.Length && int.TryParse(args[i + 1], out var frames))
            {
                targetFrames = frames;
            }
        }
    }

    public override void _Process(double delta)
    {
        currentFrames++;
        timeElapsed += delta;

        float progress = targetFrames > 0
            ? Mathf.Clamp((float)currentFrames / targetFrames, 0f, 1f)
            : Mathf.Clamp((float)(timeElapsed / TotalDuration), 0f, 1f);

        if (camera != null)
        {
            var (targetCamPos, targetFocus) = EvaluateKeyframes(progress);
            if (currentFrames <= 1)
            {
                camera.GlobalPosition = targetCamPos;
            }
            else
            {
                camera.GlobalPosition = camera.GlobalPosition.Lerp(targetCamPos, (float)Mathf.Clamp(delta * 5.0f, 0.08f, 0.6f));
            }
            camera.LookAt(targetFocus, Vector3.Up);
        }

        if (targetFrames > 0 && currentFrames >= targetFrames)
        {
            GetTree().Quit();
            return;
        }

        if (targetFrames <= 0 && timeElapsed >= TotalDuration + 0.5f)
        {
            var args = CommandLineArgs.Get();
            if (System.Array.IndexOf(args, "--capture-preview") != -1)
            {
                GetTree().Quit();
            }
        }
    }

    (Vector3, Vector3) EvaluateKeyframes(float progress)
    {
        if (progress <= keyframes[0].Time)
            return (keyframes[0].CamPos, keyframes[0].Focus);
        if (progress >= keyframes[^1].Time)
            return (keyframes[^1].CamPos, keyframes[^1].Focus);

        for (int i = 0; i < keyframes.Length - 1; i++)
        {
            var k0 = keyframes[i];
            var k1 = keyframes[i + 1];
            if (progress >= k0.Time && progress <= k1.Time)
            {
                float span = k1.Time - k0.Time;
                float localT = span > 0.0001f ? (progress - k0.Time) / span : 0f;
                float smoothT = localT * localT * (3f - 2f * localT);
                Vector3 pos = k0.CamPos.Lerp(k1.CamPos, smoothT);
                Vector3 focus = k0.Focus.Lerp(k1.Focus, smoothT);
                return (pos, focus);
            }
        }

        return (keyframes[^1].CamPos, keyframes[^1].Focus);
    }
}

