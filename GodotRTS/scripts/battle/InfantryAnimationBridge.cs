using Godot;
using System.Collections.Generic;

/// <summary>
/// 运行时把分离的 Mixamo FBX 动画注入到步兵模型的 AnimationPlayer，
/// 使步兵模型支持 idle / walk / fire 骨骼动画。
/// 当前模型为 character_medium.glb（官方自带骨骼，动画通过骨骼名映射兼容 Mixamo 动画源）。
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
    /// character_medium.glb 若没有内置 AnimationPlayer，会自动创建一个并绑定到 Skeleton3D。
    /// 在 AddInfantryVisual 实例化 model 后调用一次即可。
    /// </summary>
    public static void InjectAnimations(Node3D model)
    {
        EnsureCache();

        var skeleton = FindSkeleton(model);
        if (skeleton is null)
        {
            GD.PushWarning($"[InfantryAnimationBridge] No Skeleton3D found in model: {model.Name}");
            return;
        }

        // 先找现有的 AnimationPlayer（如果导入时带了）
        var player = FindAnimationPlayerDeep(model);

        // X Bot.fbx 没有内置 AnimationPlayer，手动创建并挂到模型根上
        if (player is null)
        {
            player = new AnimationPlayer { Name = "InfantryAnimPlayer" };
            model.AddChild(player);
        }

        // 强行将 RootNode 设为模型根节点（".."），在加入场景树之前该路径也始终有效
        player.RootNode = new NodePath("..");

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
            if (!animCache.TryGetValue(targetName, out var sourceAnim) || sourceAnim is null)
                continue;

            // 动态将 Mixamo 的骨骼动画轨道映射到当前实际模型的 Skeleton3D 路径及骨骼名
            var anim = RemapAnimation(sourceAnim, model, skeleton);
            if (targetName == IdleAnimName || targetName == WalkAnimName)
            {
                anim.LoopMode = Animation.LoopModeEnum.Linear;
            }

            if (lib.HasAnimation(targetName))
                lib.RemoveAnimation(targetName);
            lib.AddAnimation(targetName, anim);
        }

        // 启动 idle 动画
        if (player.HasAnimation(IdleAnimName))
            player.Play(IdleAnimName);
        else
            GD.PushWarning("[InfantryAnimationBridge] idle animation not found after injection.");
    }

    private static string GetRelativePath(Node from, Node to)
    {
        var pathList = new List<string>();
        var curr = to;
        while (curr is not null && curr != from)
        {
            pathList.Insert(0, curr.Name.ToString());
            curr = curr.GetParent();
        }
        return string.Join("/", pathList);
    }

    private static Animation RemapAnimation(Animation sourceAnim, Node3D model, Skeleton3D skeleton)
    {
        var targetAnim = (Animation)sourceAnim.Duplicate(true);
        var skeletonPath = GetRelativePath(model, skeleton); // 例如 "Root/Skeleton3D"

        // 倒序循环遍历轨道，方便安全删除轨道而不会打乱索引
        for (int i = targetAnim.GetTrackCount() - 1; i >= 0; i--)
        {
            var trackType = targetAnim.TrackGetType(i);

            // 1. 丢弃所有缩放轨道（极度关键！防止 Mixamo 厘米级缩放导致 Kenney 模型缩为 1/100 大小变成隐形点）
            if (trackType == Animation.TrackType.Scale3D)
            {
                targetAnim.RemoveTrack(i);
                continue;
            }

            var path = targetAnim.TrackGetPath(i);
            var pathStr = path.ToString();
            
            // Mixamo 原始轨道格式通常为 "Skeleton3D:mixamorig_Hips" 或 "Skeleton3D:mixamorig_Hips:rotation"
            var parts = pathStr.Split(':');
            if (parts.Length >= 2)
            {
                var boneName = parts[1];
                if (boneName.StartsWith("mixamorig_"))
                {
                    boneName = boneName.Substring("mixamorig_".Length);
                }

                // 针对 Kenney 模型的常用骨骼命名差异进行别名映射
                if (boneName == "LeftToeBase" || boneName == "LeftToe") boneName = "LeftToes";
                else if (boneName == "RightToeBase" || boneName == "RightToe") boneName = "RightToes";
                else if (boneName == "Spine1") boneName = "Chest";
                else if (boneName == "Spine2") boneName = "UpperChest";

                // 2. 丢弃除 Hips（盆骨根节点）之外的所有位移轨道（防止骨骼因为两套模型骨长不同发生错位拉伸）
                if (trackType == Animation.TrackType.Position3D && boneName != "Hips")
                {
                    targetAnim.RemoveTrack(i);
                    continue;
                }

                var targetBoneName = boneName;
                if (skeleton.FindBone(targetBoneName) == -1 && skeleton.FindBone("mixamorig_" + targetBoneName) != -1)
                {
                    targetBoneName = "mixamorig_" + targetBoneName;
                }

                if (skeleton.FindBone(targetBoneName) != -1)
                {
                    var newPathStr = skeletonPath + ":" + targetBoneName;
                    if (parts.Length > 2)
                    {
                        for (int j = 2; j < parts.Length; j++)
                        {
                            newPathStr += ":" + parts[j];
                        }
                    }
                    targetAnim.TrackSetPath(i, new NodePath(newPathStr));
                }
                else
                {
                    // 3. 模型中不存在的骨骼轨道（比如手指细分骨骼）直接删掉，彻底静默控制台警告
                    targetAnim.RemoveTrack(i);
                }
            }
            else
            {
                targetAnim.RemoveTrack(i);
            }
        }
        return targetAnim;
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
