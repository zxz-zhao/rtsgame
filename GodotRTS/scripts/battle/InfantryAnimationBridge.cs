using Godot;
using System.Collections.Generic;

/// <summary>
/// 运行时把分离的 Mixamo FBX 动画注入到 X Bot AnimationPlayer，
/// 使步兵模型支持 idle / walk / fire 骨骼动画。
/// </summary>
public static class InfantryAnimationBridge
{
    const string MixamoRoot = "res://assets/unity_migrated/Assets/External/Mixamo/BasicShooter/";

    // 注入后的动画名称（供 ResolveVisualAnimationNames 识别）
    public const string IdleAnimName = "infantry_idle";
    public const string WalkAnimName = "infantry_walk";
    public const string FireAnimName = "infantry_fire";

    // 动画源 FBX 路径（Mixamo BasicShooter – X Bot 骨骼）
    static readonly (string path, string targetName)[] AnimSources =
    {
        (MixamoRoot + "rifle aiming idle.fbx", IdleAnimName),
        (MixamoRoot + "walking.fbx",           WalkAnimName),
        (MixamoRoot + "firing rifle.fbx",      FireAnimName),
    };

    // 已提取的动画缓存（避免反复实例化 FBX）
    static readonly Dictionary<string, Animation?> animCache = new();
    static bool cacheBuilt;

    /// <summary>
    /// 将 Mixamo 动画注入到给定步兵模型节点下的 AnimationPlayer。
    /// X Bot.fbx 本身没有内置 AnimationPlayer，会自动创建一个并绑定到 Skeleton3D。
    /// 在 AddInfantryVisual 实例化 model 后调用一次即可。
    /// </summary>
    public static void InjectAnimations(Node3D model)
    {
        EnsureCache();

        // 先找现有的 AnimationPlayer（如果导入时带了）
        var player = FindAnimationPlayerDeep(model);

        // X Bot.fbx 没有内置 AnimationPlayer，手动创建并挂到模型根上
        if (player is null)
        {
            player = new AnimationPlayer { Name = "InfantryAnimPlayer" };
            // root_node 设为模型自身（即 "."），骨骼轨道路径使用从模型根开始的相对路径
            model.AddChild(player);
            player.RootNode = new NodePath(".");
        }

        // 确保有默认动画库
        AnimationLibrary? lib = null;
        if (player.HasAnimationLibrary(""))
            lib = player.GetAnimationLibrary("");
        if (lib is null)
        {
            lib = new AnimationLibrary();
            player.AddAnimationLibrary("", lib);
        }

        foreach (var (_, targetName) in AnimSources)
        {
            if (!animCache.TryGetValue(targetName, out var anim) || anim is null)
                continue;
            if (!lib.HasAnimation(targetName))
                lib.AddAnimation(targetName, anim);
        }

        // 启动 idle 动画
        if (player.HasAnimation(IdleAnimName))
            player.Play(IdleAnimName);
        else
            GD.PushWarning("[InfantryAnimationBridge] idle animation not found after injection.");
    }

    static void EnsureCache()
    {
        if (cacheBuilt)
            return;
        cacheBuilt = true;

        foreach (var (path, targetName) in AnimSources)
        {
            var anim = ExtractFirstAnimation(path);
            if (anim is null)
            {
                GD.PushWarning($"[InfantryAnimationBridge] Failed to extract animation from: {path}");
                continue;
            }
            anim.LoopMode = Animation.LoopModeEnum.Linear;
            animCache[targetName] = anim;
            GD.Print($"[InfantryAnimationBridge] Cached '{targetName}': {anim.GetTrackCount()} tracks");
        }
    }

    static Animation? ExtractFirstAnimation(string fbxPath)
    {
        if (!ResourceLoader.Exists(fbxPath))
            return null;

        var scene = ResourceLoader.Load<PackedScene>(fbxPath);
        if (scene is null)
            return null;

        var tempRoot = scene.Instantiate<Node>();
        var player = FindAnimationPlayerDeep(tempRoot);
        if (player is null)
        {
            tempRoot.Free();
            GD.PushWarning($"[InfantryAnimationBridge] No AnimationPlayer in source FBX: {fbxPath}");
            return null;
        }

        Animation? result = null;
        foreach (var name in player.GetAnimationList())
        {
            var nameStr = name.ToString();
            if (nameStr == "RESET")
                continue;
            var sourceAnim = player.GetAnimation(nameStr);
            if (sourceAnim is null)
                continue;
            result = (Animation)sourceAnim.Duplicate(true);
            break;
        }

        tempRoot.Free();
        return result;
    }

    /// <summary>深度优先在整棵树中找 AnimationPlayer（使用 FindChildren 全树搜索）。</summary>
    static AnimationPlayer? FindAnimationPlayerDeep(Node root)
    {
        if (root is AnimationPlayer p)
            return p;

        var found = root.FindChildren("*", "AnimationPlayer", true, false);
        return found.Count > 0 ? found[0] as AnimationPlayer : null;
    }

    /// <summary>找第一个 Skeleton3D 节点。</summary>
    static Skeleton3D? FindSkeleton(Node root)
    {
        if (root is Skeleton3D sk)
            return sk;
        var found = root.FindChildren("*", "Skeleton3D", true, false);
        return found.Count > 0 ? found[0] as Skeleton3D : null;
    }

    /// <summary>场景切换时清空缓存</summary>
    public static void ClearCache()
    {
        animCache.Clear();
        cacheBuilt = false;
    }
}
