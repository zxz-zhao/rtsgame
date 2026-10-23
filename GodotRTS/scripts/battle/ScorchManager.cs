using Godot;
using System;

/// <summary>
/// 次世代 RTS 战场地表焦痕与弹坑贴花管理系统 (ScorchManager)
/// 遵循高频物理热循环零 GC 铁律，预先烘焙并池化 64 个 Decal 贴花节点
/// 杜绝运行时 AddChild / QueueFree 导致的卡顿与内存碎片
/// </summary>
public partial class ScorchManager : Node3D
{
    public static ScorchManager? Instance { get; private set; }

    private const int PoolCapacity = 64;
    private readonly Decal[] decalPool = new Decal[PoolCapacity];
    private int poolIndex = 0;
    private static Texture2D? cachedScorchTexture;

    public static void Ensure(Node owner)
    {
        if (Instance is not null && GodotObject.IsInstanceValid(Instance))
            return;

        var root = owner.GetTree()?.CurrentScene ?? owner;
        if (root is null) return;
        if (root.GetNodeOrNull<ScorchManager>("ScorchManager") is { } existing)
        {
            Instance = existing;
            return;
        }

        var manager = new ScorchManager { Name = "ScorchManager" };
        if (root.IsNodeReady())
        {
            root.AddChild(manager);
        }
        else
        {
            root.CallDeferred(Node.MethodName.AddChild, manager);
        }
    }

    public override void _Ready()
    {
        Instance = this;
        InitializeDecalPool();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    private void InitializeDecalPool()
    {
        var texture = GetOrCreateScorchTexture();

        for (int i = 0; i < PoolCapacity; i++)
        {
            var decal = new Decal
            {
                Name = $"ScorchDecal_{i:D2}",
                TextureAlbedo = texture,
                Size = new Vector3(3.2f, 1.4f, 3.2f),
                DistanceFadeEnabled = true,
                DistanceFadeBegin = 120.0f,
                DistanceFadeLength = 30.0f,
                CullMask = 1, // 投影至地表层
                Visible = false
            };
            AddChild(decal);
            decalPool[i] = decal;
        }
    }

    private static Texture2D GetOrCreateScorchTexture()
    {
        if (cachedScorchTexture is not null)
            return cachedScorchTexture;

        // 动态构建高品质黑褐色爆炸焦痕径向渐变
        var gradient = new Gradient
        {
            Offsets = new float[] { 0.00f, 0.35f, 0.70f, 1.00f },
            Colors = new Color[]
            {
                new Color(0.04f, 0.04f, 0.05f, 0.88f), // 核心碳化黑
                new Color(0.12f, 0.10f, 0.08f, 0.65f), // 冲击波灼烧环
                new Color(0.20f, 0.16f, 0.12f, 0.25f), // 外圈飞溅泥土
                new Color(0.15f, 0.12f, 0.10f, 0.00f)  // 边缘自然羽化 (完全透明，彻底根除白边与白色方块)
            }
        };

        var gradTex = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.98f, 0.5f),
            Width = 128,
            Height = 128
        };

        cachedScorchTexture = gradTex;
        return cachedScorchTexture;
    }

    /// <summary>
    /// 在指定世界坐标生成爆炸焦痕贴花（零 GC 循环复用）
    /// </summary>
    public static void SpawnScorch(Node owner, Vector3 worldPos, float radius = 2.0f, Vector3? normal = null)
    {
        Ensure(owner);
        Instance?.SpawnScorchInternal(worldPos, radius, normal);
    }

    private void SpawnScorchInternal(Vector3 worldPos, float radius, Vector3? normal)
    {
        if (decalPool[0] is null)
            return;

        var decal = decalPool[poolIndex];
        poolIndex = (poolIndex + 1) % PoolCapacity;

        if (!GodotObject.IsInstanceValid(decal))
            return;

        float clampedRadius = Mathf.Clamp(radius, 0.8f, 6.0f);
        decal.Size = new Vector3(clampedRadius * 2.0f, 1.5f, clampedRadius * 2.0f);
        
        // 投影稍微高于地面中心以确保良好贴合
        decal.GlobalPosition = worldPos + Vector3.Up * 0.04f;

        // 微量随机偏航角，避免多枚弹坑视觉朝向完全一致
        float randomYaw = (poolIndex * 57.3f) % Mathf.Tau;
        decal.Rotation = new Vector3(0f, randomYaw, 0f);

        decal.Visible = true;
    }
}
