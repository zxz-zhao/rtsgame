using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Mixamo 动画接入工具。
/// 用法：菜单 RTS → 资源接入 → Mixamo 动画绑定。
///   1) 从 https://www.mixamo.com 下载需要的 fbx (with skin)；
///   2) 推荐动画：Idle, Walking, Running, Firing Rifle, Death From Front；
///   3) 把每个 fbx 拖到对应槽位，选择目标 RTSUnit prefab；
///   4) 点 "生成 AnimatorController" → 自动建立 AnimatorController + 状态机
///      并把 prefab 的 Animator 控制器绑定到该 controller。
/// </summary>
public class MixamoAnimationIntegrator : EditorWindow
{
    GameObject targetUnitPrefab;
    AnimationClip idleClip, walkClip, runClip, fireClip, deathClip;
    string outputFolder = "Assets/Resources/Animations";

    [MenuItem("RTS/资源接入/Mixamo 动画绑定")]
    static void Open() => GetWindow<MixamoAnimationIntegrator>("动画绑定");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Mixamo 动画接入", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "从 Mixamo 下载 fbx（含骨骼），导入项目后从 fbx 内拖出 AnimationClip 到下面槽位。\n"
            + "工具会自动创建 AnimatorController 并绑定到指定的单位 prefab。\n"
            + "Mixamo fbx 导入时记得改 Rig 为 Humanoid（Inspector → Rig → Animation Type）。",
            MessageType.Info);

        EditorGUILayout.Space();
        targetUnitPrefab = (GameObject)EditorGUILayout.ObjectField("目标单位 Prefab", targetUnitPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("动画片段", EditorStyles.miniBoldLabel);
        idleClip  = (AnimationClip)EditorGUILayout.ObjectField("Idle",     idleClip,  typeof(AnimationClip), false);
        walkClip  = (AnimationClip)EditorGUILayout.ObjectField("Walking",  walkClip,  typeof(AnimationClip), false);
        runClip   = (AnimationClip)EditorGUILayout.ObjectField("Running",  runClip,   typeof(AnimationClip), false);
        fireClip  = (AnimationClip)EditorGUILayout.ObjectField("Firing",   fireClip,  typeof(AnimationClip), false);
        deathClip = (AnimationClip)EditorGUILayout.ObjectField("Death",    deathClip, typeof(AnimationClip), false);

        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("输出目录", outputFolder);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("生成 AnimatorController 并绑定", GUILayout.Height(36)))
            GenerateAndBind();

        EditorGUILayout.Space(2);
        EditorGUILayout.HelpBox(
            "AnimatorController 参数：Speed (Float)、Fire (Trigger)、Die (Trigger)。\n"
            + "RTSUnit 不会自动驱动这些参数；如需在战斗中触发，请告诉我接入到 OnDeath/Fire 调用点。",
            MessageType.None);
    }

    void GenerateAndBind()
    {
        if (idleClip == null) { EditorUtility.DisplayDialog("缺失 Idle", "至少需要 Idle 动画。", "确定"); return; }
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        string controllerName = targetUnitPrefab != null ? targetUnitPrefab.name + "_Anim" : "Unit_Anim";
        string controllerPath = $"{outputFolder}/{controllerName}.controller";
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // 参数
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Fire",  AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die",   AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = idleClip;
        sm.defaultState = idle;

        AnimatorState walk = null, run = null, fire = null, death = null;
        if (walkClip != null) { walk = sm.AddState("Walk"); walk.motion = walkClip; }
        if (runClip  != null) { run  = sm.AddState("Run");  run.motion = runClip; }
        if (fireClip != null) { fire = sm.AddState("Fire"); fire.motion = fireClip; }
        if (deathClip != null){ death = sm.AddState("Death"); death.motion = deathClip; }

        // Idle ↔ Walk
        if (walk != null)
        {
            var t = idle.AddTransition(walk);
            t.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
            t.duration = 0.1f;
            var t2 = walk.AddTransition(idle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            t2.duration = 0.1f;
        }
        // Walk → Run
        if (walk != null && run != null)
        {
            var t = walk.AddTransition(run);
            t.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
            t.duration = 0.15f;
            var t2 = run.AddTransition(walk);
            t2.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");
            t2.duration = 0.15f;
        }
        // Any → Fire (trigger)
        if (fire != null)
        {
            var t = sm.AddAnyStateTransition(fire);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            var back = fire.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.1f;
        }
        // Any → Death (trigger)
        if (death != null)
        {
            var t = sm.AddAnyStateTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 绑定到 prefab
        if (targetUnitPrefab != null)
        {
            string prefabPath = AssetDatabase.GetAssetPath(targetUnitPrefab);
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var animator = contents.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    animator = contents.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        EditorUtility.DisplayDialog("动画绑定", $"已生成 AnimatorController 到 {controllerPath}\n并绑定到 {(targetUnitPrefab != null ? targetUnitPrefab.name : "（无 prefab）")}。", "确定");
    }
}
