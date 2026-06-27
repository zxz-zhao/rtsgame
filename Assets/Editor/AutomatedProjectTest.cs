using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AutomatedProjectTest
{
    // 冒烟测试依赖的核心场景与资源路径。
    const string GameScenePath = "Assets/Scenes/GameScene.unity";
    const string LoginScenePath = "Assets/Scenes/LoginScene.unity";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    const string NavMeshAssetPath = "Assets/Scenes/GameScene/NavMesh.asset";

    /// <summary>编辑器菜单入口，运行完整冒烟测试但不抛出异常。</summary>
    [MenuItem("RTS/测试/运行自动冒烟测试")]
    public static void RunFromMenu()
    {
        RunSmokeTest(false);
    }

    // Batchmode entry point. Unity returns a failing exit code when this throws.
    public static void RunSmokeTestBatch()
    {
        RunSmokeTest(true);
    }

    /// <summary>场景生成流程结束后复用同一套校验，批处理模式下保持失败即退出。</summary>
    public static void RunAfterSceneGeneration()
    {
        RunSmokeTest(Application.isBatchMode);
    }

    /// <summary>汇总场景、布局与资源校验结果，并在需要时抛出失败原因。</summary>
    public static bool RunSmokeTest(bool throwOnFailure)
    {
        var failures = new List<string>();

        ValidateMapCatalog(failures);
        ValidateGameScene(failures);
        ValidateLoginScene(failures);
        ValidateLobbyScene(failures);
        ValidateResources(failures);

        if (failures.Count == 0)
        {
            Debug.Log("✅ 自动冒烟测试通过：地图、GameScene、NavMesh、HUD、小地图、结算面板、Prefab 模型资源检查正常");
            return true;
        }

        var sb = new StringBuilder();
        sb.AppendLine("❌ 自动冒烟测试失败：");
        foreach (string failure in failures)
            sb.AppendLine("- " + failure);

        string message = sb.ToString();
        Debug.LogError(message);
        if (throwOnFailure)
            throw new System.Exception(message);
        return false;
    }

    static void ValidateMapCatalog(List<string> failures)
    {
        string[] playableMaps = BattleMapCatalog.GetPlayableMapNames();
        string[] allMaps = BattleMapCatalog.GetAllMapNames();

        if (playableMaps == null || playableMaps.Length == 0)
            failures.Add("BattleMapCatalog.GetPlayableMapNames() 没有返回可玩地图。");
        if (allMaps == null || allMaps.Length == 0)
            failures.Add("BattleMapCatalog.GetAllMapNames() 没有返回地图。");

        var seen = new HashSet<string>();
        foreach (string mapName in allMaps ?? new string[0])
        {
            if (string.IsNullOrEmpty(mapName))
            {
                failures.Add("地图名为空。");
                continue;
            }
            if (!seen.Add(mapName))
                failures.Add("地图名重复：" + mapName);

            BattleMapDefinition map = BattleMapCatalog.Get(mapName);
            if (map == null)
            {
                failures.Add("地图返回 null：" + mapName);
                continue;
            }
            if (map.Name != mapName)
                failures.Add("地图名不一致：" + mapName + " -> " + map.Name);
            if (map.Roads == null || map.Roads.Length == 0)
                failures.Add(mapName + " 没有 Roads。");
            if (map.Patches == null || map.Patches.Length == 0)
                failures.Add(mapName + " 没有 Patches。");
            if (map.RockClusters == null)
                failures.Add(mapName + " 的 RockClusters 为 null。");
            if (map.TreePositions == null)
                failures.Add(mapName + " 的 TreePositions 为 null。");
            if (map.RuinWalls == null)
                failures.Add(mapName + " 的 RuinWalls 为 null。");
            if (map.SandbagRings == null)
                failures.Add(mapName + " 的 SandbagRings 为 null。");

            ValidateBounds(mapName, "Roads", map.Roads, failures);
            ValidateBounds(mapName, "Waters", map.Waters, failures);
            ValidateBounds(mapName, "Patches", map.Patches, failures);
            ValidateBounds(mapName, "RockClusters", map.RockClusters, failures);
            ValidateBounds(mapName, "TreePositions", map.TreePositions, failures);
        }
    }

    static void ValidateGameScene(List<string> failures)
    {
        if (!File.Exists(GameScenePath))
        {
            failures.Add("找不到 GameScene：" + GameScenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            failures.Add("GameScene 打开失败。");
            return;
        }

        RequireObject("Ground", failures);
        RequireObject("GameManager", failures);
        RequireObject("RTSPlayerState", failures);
        RequireObject("RTSPlayerController", failures);
        RequireObject("AIController", failures);
        RequireObject("GameInitializer", failures);
        RequireObject("HUDCanvas", failures);
        RequireObject("RTSHUD", failures);
        RequireObject("MinimapCamera", failures);
        RequireObject("MinimapManager", failures);
        RequireObject("WallN", failures);
        RequireObject("WallS", failures);
        RequireObject("WallE", failures);
        RequireObject("WallW", failures);

        if (Camera.main == null)
            failures.Add("缺少 MainCamera tag 的主摄像机。");
        if (Object.FindObjectOfType<EventSystem>() == null)
            failures.Add("缺少 EventSystem。");
        if (Object.FindObjectOfType<GameInitializer>() == null)
            failures.Add("缺少 GameInitializer 组件。");
        if (Object.FindObjectOfType<RTSPlayerController>() == null)
            failures.Add("缺少 RTSPlayerController 组件。");
        if (Object.FindObjectOfType<AIController>() == null)
            failures.Add("缺少 AIController 组件。");

        ValidateHud(failures);
        ValidateBattleHudLayout(failures);
        ValidateGameOverPanelLayout(failures);
        ValidateMinimap(failures);
        ValidateGeneratedTerrain(failures);
        ValidateNoMissingScripts(failures);
        ValidateNavMesh(failures);
    }

    static void ValidateLobbyScene(List<string> failures)
    {
        if (!File.Exists(LobbyScenePath))
        {
            failures.Add("Missing LobbyScene: " + LobbyScenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            failures.Add("LobbyScene failed to open.");
            return;
        }

        RequireObject("LobbyCanvas", failures);
        LobbyManager lobby = Object.FindObjectOfType<LobbyManager>();
        if (lobby == null)
            failures.Add("LobbyScene missing LobbyManager component.");

        if (FindExact("LogoutButton") > 0)
            failures.Add("LobbyScene should not contain the unused LogoutButton.");
        if (FindExact("LogoutBtnTop") > 0)
            failures.Add("LobbyScene should not contain the unused LogoutBtnTop.");

        ValidateNoTextContains("退出登录", failures);
        ValidateNoTextContains("登出登录", failures);

        bool hasRuntimeTechButtons = ValidateOptionalUiRectsDoNotOverlap(
            "TechSpeedBtn",
            "TechStartBtn",
            failures,
            "Lobby tech speed/start buttons overlap.");
        bool hasDynamicTechButtons = ValidateOptionalUiRectsDoNotOverlap(
            "DynamicTechSpeedBtn",
            "DynamicTechStartBtn",
            failures,
            "Lobby dynamic tech speed/start buttons overlap.");
        if (!hasRuntimeTechButtons && !hasDynamicTechButtons)
            failures.Add("LobbyScene missing tech speed/start button pair.");

        ValidateReturnBattleRuntimeLayout(failures);
    }

    static void ValidateLoginScene(List<string> failures)
    {
        if (!File.Exists(LoginScenePath))
        {
            failures.Add("Missing LoginScene: " + LoginScenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            failures.Add("LoginScene failed to open.");
            return;
        }

        LoginManager login = Object.FindObjectOfType<LoginManager>();
        if (login == null)
            failures.Add("LoginScene missing LoginManager component.");
        if (LoginManager.GuestLoginUsesNicknamePopup)
            failures.Add("Guest login must not require a nickname confirmation popup.");
        if (FindExact("GuestLoginButton") == 0)
            failures.Add("LoginScene missing GuestLoginButton.");
        if (FindTextContains("前线指挥终端") == 0 || FindTextContains("新版战区 UI") == 0)
            failures.Add("LoginScene is still using the old visual layout. Regenerate SceneBuilder.BuildLoginScene before packaging.");
    }

    static void ValidateHud(List<string> failures)
    {
        RTSHUD hud = Object.FindObjectOfType<RTSHUD>();
        if (hud == null) return;

        if (hud.GoldText == null) failures.Add("RTSHUD.GoldText 未绑定。");
        if (hud.PopText == null) failures.Add("RTSHUD.PopText 未绑定。");
        if (hud.PowerText == null) failures.Add("RTSHUD.PowerText 未绑定。");
        if (hud.GameOverPanel == null) failures.Add("RTSHUD.GameOverPanel 未绑定。");
        if (hud.GameOverText == null) failures.Add("RTSHUD.GameOverText 未绑定。");
        if (hud.BuildingPanel == null) failures.Add("RTSHUD.BuildingPanel 未绑定。");
        if (hud.UnitInfoPanel == null) failures.Add("RTSHUD.UnitInfoPanel 未绑定。");
        if (hud.ProductionButtons == null || hud.ProductionButtons.Length == 0)
            failures.Add("RTSHUD.ProductionButtons 为空。");
        if (hud.BuildButtons == null || hud.BuildButtons.Length == 0)
            failures.Add("RTSHUD.BuildButtons 为空。");
        if (hud.MinimapImage == null) failures.Add("RTSHUD.MinimapImage 未绑定。");
        if (hud.MinimapCameraRect == null) failures.Add("RTSHUD.MinimapCameraRect 未绑定。");
    }

    /// <summary>执行战斗 HUD 的运行时重排，并检查左下角关键入口是否互相遮挡。</summary>
    static void ValidateBattleHudLayout(List<string> failures)
    {
        RTSHUD hud = Object.FindObjectOfType<RTSHUD>();
        if (hud == null) return;

        MethodInfo rebuildLayoutMethod = typeof(RTSHUD).GetMethod(
            "RebuildBattleHudLayout",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (rebuildLayoutMethod == null)
        {
            failures.Add("RTSHUD missing RebuildBattleHudLayout.");
            return;
        }

        try
        {
            rebuildLayoutMethod.Invoke(hud, null);
        }
        catch (System.Exception e)
        {
            failures.Add("RTSHUD.RebuildBattleHudLayout failed: " + e.Message);
            return;
        }

        ValidateOptionalUiRectsDoNotOverlap(
            "MinimapImage",
            "BuildMenuToggle",
            failures,
            "Battle HUD minimap overlaps build button.");
        ValidateOptionalUiRectsDoNotOverlap(
            "MinimapImage",
            "_TechButton",
            failures,
            "Battle HUD minimap overlaps tech button.");
        ValidateOptionalUiRectsDoNotOverlap(
            "BuildMenuToggle",
            "_TechButton",
            failures,
            "Battle HUD build/tech buttons overlap.");
        ValidateBattleHudActionColumnLayout(failures);
        ValidateBattleHudCommandBarLayout(failures);
        ValidateBattleBuildMenuPopupCentering(failures);
        ValidateBattleChatLayout(hud, failures);
    }

    static void ValidateBattleHudActionColumnLayout(List<string> failures)
    {
        RectTransform minimap = FindRectTransform("MinimapImage");
        RectTransform build = FindRectTransform("BuildMenuToggle");
        RectTransform tech = FindRectTransform("_TechButton");
        RectTransform chatDock = FindRectTransform("_ChatDockButton");
        if (minimap == null || build == null || tech == null) return;

        Rect minimapRect = GetWorldRect(minimap);
        Rect buildRect = GetWorldRect(build);
        Rect techRect = GetWorldRect(tech);

        if (Mathf.Abs(buildRect.xMin - techRect.xMin) > 4f)
            failures.Add("Battle HUD build/tech buttons are not left-aligned in one column.");

        if (buildRect.yMin < techRect.yMax - 0.5f)
            failures.Add("Battle HUD build button is not stacked above the tech button.");

        if (techRect.yMin < minimapRect.yMax + 4f)
            failures.Add("Battle HUD tech button is not placed above the minimap.");

        if (Mathf.Abs(buildRect.xMin - minimapRect.xMin) > 4f || Mathf.Abs(techRect.xMin - minimapRect.xMin) > 4f)
            failures.Add("Battle HUD build/tech buttons are not aligned to the minimap's left edge.");

        if (chatDock != null)
        {
            Rect chatDockRect = GetWorldRect(chatDock);
            if (buildRect.yMax > chatDockRect.yMin - 8f)
                failures.Add("Battle HUD chat dock is too close to the build button.");
        }
    }

    static void ValidateBattleHudCommandBarLayout(List<string> failures)
    {
        RectTransform canvas = FindRectTransform("HUDCanvas");
        RectTransform commandBar = FindRectTransform("_CommandBar");
        RectTransform selectAll = FindRectTransform("_Cmd_SelectAll");
        RectTransform boxSelect = FindRectTransform("_Cmd_BoxSelect");
        if (commandBar == null)
        {
            failures.Add("Battle HUD command bar is missing.");
            return;
        }

        if (!commandBar.gameObject.activeInHierarchy)
            failures.Add("Battle HUD command bar is inactive.");
        if (selectAll == null)
            failures.Add("Battle HUD command bar missing Select All button.");
        if (boxSelect == null)
            failures.Add("Battle HUD command bar missing Box Select button.");
        if (canvas == null || selectAll == null || boxSelect == null)
            return;

        Rect canvasRect = GetWorldRect(canvas);
        Rect commandRect = GetWorldRect(commandBar);
        Rect selectAllRect = GetWorldRect(selectAll);
        Rect boxSelectRect = GetWorldRect(boxSelect);

        if (!RectContainsRect(canvasRect, commandRect))
            failures.Add("Battle HUD command bar is outside the HUD canvas.");
        if (!RectContainsRect(canvasRect, selectAllRect))
            failures.Add("Battle HUD Select All button is outside the HUD canvas.");
        if (!RectContainsRect(canvasRect, boxSelectRect))
            failures.Add("Battle HUD Box Select button is outside the HUD canvas.");
        if (selectAllRect.Overlaps(boxSelectRect))
            failures.Add("Battle HUD Select All and Box Select buttons overlap.");
    }

    /// <summary>注入一条长聊天消息，验证自动换行后的文本仍位于聊天面板正文区域内。</summary>
    static void ValidateBattleBuildMenuPopupCentering(List<string> failures)
    {
        RectTransform panel = FindRectTransform("BuildMenuPanel");
        if (panel == null) return;

        if (Vector2.Distance(panel.anchorMin, new Vector2(0.5f, 0.5f)) > 0.01f
            || Vector2.Distance(panel.anchorMax, new Vector2(0.5f, 0.5f)) > 0.01f)
            failures.Add("Battle HUD build menu panel is not anchored to the screen center.");

        if (Vector2.Distance(panel.pivot, new Vector2(0.5f, 0.5f)) > 0.01f)
            failures.Add("Battle HUD build menu panel pivot is not centered.");

        if (panel.anchoredPosition.sqrMagnitude > 1f)
            failures.Add("Battle HUD build menu panel is not positioned at the screen center.");

        RectTransform close = FindRectTransform("_BuildPopupClose");
        if (close == null)
        {
            failures.Add("Battle HUD build menu popup is missing the top-right close button.");
        }
        else
        {
            Rect panelRect = GetWorldRect(panel);
            Rect closeRect = GetWorldRect(close);
            if (closeRect.xMax < panelRect.xMax - 56f || closeRect.yMax < panelRect.yMax - 30f)
                failures.Add("Battle HUD build menu close button is not anchored near the popup's top-right corner.");
        }

        RectTransform tabs = FindRectTransform("_BuildCategoryTabs");
        if (tabs == null)
            failures.Add("Battle HUD build menu popup is missing category tabs.");

        RectTransform contentFrame = FindRectTransform("_BuildContentFrame");
        if (contentFrame == null)
            failures.Add("Battle HUD build menu popup is missing its content frame.");
        else if (tabs != null)
        {
            Rect tabsRect = GetWorldRect(tabs);
            Rect contentRect = GetWorldRect(contentFrame);
            if (tabsRect.yMin < contentRect.yMax + 4f)
                failures.Add("Battle HUD build menu tabs overlap the content frame.");
        }
    }

    static void ValidateBattleChatLayout(RTSHUD hud, List<string> failures)
    {
        if (hud == null) return;

        hud.AppendChatMessage(
            "测试",
            "这是一条需要自动换行的战场通讯消息，用来验证聊天面板里的多行文字不会互相覆盖或跑出语音框区域。",
            Color.white);
        Canvas.ForceUpdateCanvases();

        RectTransform chatPanel = FindRectTransform("_ChatPanel");
        if (chatPanel == null)
        {
            failures.Add("Battle HUD missing chat panel.");
            return;
        }

        RectTransform body = chatPanel.Find("Body") as RectTransform;
        if (body == null)
        {
            failures.Add("Battle HUD chat panel missing Body.");
            return;
        }

        var lines = new List<Text>();
        foreach (Text text in body.GetComponentsInChildren<Text>(true))
        {
            if (text != null && text.transform.parent == body)
                lines.Add(text);
        }

        if (lines.Count < 2)
        {
            failures.Add("Battle HUD chat panel did not create enough lines for wrap validation.");
            return;
        }

        lines.Sort((a, b) => b.rectTransform.anchoredPosition.y.CompareTo(a.rectTransform.anchoredPosition.y));
        Rect bodyRect = GetWorldRect(body);
        Rect previousRect = new Rect();
        bool hasPrevious = false;
        for (int i = 0; i < lines.Count; i++)
        {
            Rect currentRect = GetWorldRect(lines[i].rectTransform);
            if (!RectContainsRect(bodyRect, currentRect))
            {
                failures.Add("Battle HUD chat text overflows the chat body.");
                return;
            }

            if (hasPrevious && previousRect.Overlaps(currentRect))
            {
                failures.Add("Battle HUD chat messages overlap when wrapping.");
                return;
            }

            previousRect = currentRect;
            hasPrevious = true;
        }
    }

    static void ValidateMinimap(List<string> failures)
    {
        MinimapManager minimap = Object.FindObjectOfType<MinimapManager>();
        if (minimap == null) return;

        if (minimap.MinimapCamera == null) failures.Add("MinimapManager.MinimapCamera 未绑定。");
        if (minimap.MinimapImage == null) failures.Add("MinimapManager.MinimapImage 未绑定。");

        var image = GameObject.Find("MinimapImage")?.GetComponent<RawImage>();
        if (image == null) failures.Add("缺少 MinimapImage RawImage。");
        else if (image.texture == null) failures.Add("MinimapImage 没有 RenderTexture。");
    }

    static void ValidateGameOverPanelLayout(List<string> failures)
    {
        RTSHUD hud = Object.FindObjectOfType<RTSHUD>();
        if (hud == null) return;
        if (hud.GameOverPanel == null) return;

        MethodInfo repairMethod = typeof(RTSHUD).GetMethod(
            "EnsureGameOverPanelLayout",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (repairMethod == null)
        {
            failures.Add("RTSHUD 缺少 EnsureGameOverPanelLayout 修复方法。");
            return;
        }

        try
        {
            repairMethod.Invoke(hud, null);
        }
        catch (System.Exception e)
        {
            System.Exception root = e is TargetInvocationException && e.InnerException != null
                ? e.InnerException
                : e;
            failures.Add("RTSHUD.EnsureGameOverPanelLayout failed: "
                + root.GetType().Name + ": " + root.Message + "\n" + root.StackTrace);
            return;
        }

        Transform card = hud.GameOverPanel.transform.Find("GoCard");
        if (card == null)
        {
            failures.Add("GameOverPanel 缺少 GoCard。");
            return;
        }

        Transform report = card.Find("GoReportPanel");
        if (report == null)
            failures.Add("GoCard 缺少 GoReportPanel。");

        Transform badge = card.Find("GoResultBadge");
        if (badge == null)
        {
            failures.Add("GoCard 缺少 GoResultBadge。");
        }
        else if (badge.Find("Mark")?.GetComponent<Text>() == null)
        {
            failures.Add("GoResultBadge 缺少 Mark 文本。");
        }

        if (hud.GameOverText != null && hud.GameOverText.transform.parent != card)
            failures.Add("GameOverText 应挂在 GoCard 下。");

        if (hud.GameOverStatsText == null)
        {
            failures.Add("RTSHUD.GameOverStatsText 未绑定。");
        }
        else if (report == null)
        {
            failures.Add("GameOverStatsText 无法定位到 GoReportPanel。");
        }
        else if (hud.GameOverStatsText.transform.parent != report)
        {
            failures.Add("GameOverStatsText 应挂在 GoReportPanel 下。");
        }
    }

    static void ValidateGeneratedTerrain(List<string> failures)
    {
        if (FindByPrefix("Road_") == 0)
            failures.Add("场景没有生成 Road_ 道路对象。");
        if (FindByPrefix("TerrainPatch") == 0)
            failures.Add("场景没有生成 TerrainPatch 地面色块。");
        if (FindExact("Rock") == 0)
            failures.Add("场景没有生成 Rock 障碍。");
        if (FindExact("Tree") == 0)
            failures.Add("场景没有生成 Tree 装饰/障碍。");
        bool requireLegacyDecorProps = false;
        if (!requireLegacyDecorProps)
        {
            if (FindByPrefix("Water_") == 0)
                failures.Add("Scene did not generate clear Water_ ocean or river objects.");
            return;
        }

        if (FindExact("Sandbag") == 0)
            failures.Add("场景没有生成 Sandbag 防御圈。");
        if (FindExact("SupplyCrate") == 0)
            failures.Add("Scene did not generate SupplyCrate battlefield props.");
        if (FindExact("VehicleWreck") == 0)
            failures.Add("Scene did not generate VehicleWreck battlefield props.");
    }

    static void ValidateNavMesh(List<string> failures)
    {
        if (!File.Exists(NavMeshAssetPath))
        {
            failures.Add("找不到 NavMesh 资源：" + NavMeshAssetPath);
            return;
        }

        NavMeshData data = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
        if (data == null)
            failures.Add("NavMesh.asset 无法加载为 NavMeshData。");
    }

    static void ValidateResources(List<string> failures)
    {
        string[] prefabNames =
        {
            "MainBase_P", "MainBase_E",
            "Barracks_P", "Barracks_E",
            "AirFactory_P", "AirFactory_E",
            "TankFactory_P", "TankFactory_E",
            "ArmorFactory_P", "ArmorFactory_E",
            "PowerPlant_P", "PowerPlant_E",
            "GoldMine_P", "GoldMine_E",
            "Turret_P", "Turret_E",
            "NavalYard_P", "NavalYard_E",
            "Infantry_P", "Infantry_E",
            "Fighter_P", "Fighter_E",
            "Bomber_P", "Bomber_E",
            "ScoutPlane_P", "ScoutPlane_E",
            "Tank_P", "Tank_E",
            "Artillery_P", "Artillery_E",
            "AntiAirGun_P", "AntiAirGun_E",
            "Flamethrower_P", "Flamethrower_E",
            "PatrolBoat_P", "PatrolBoat_E",
            "DestroyerShip_P", "DestroyerShip_E",
            "TransportShip_P", "TransportShip_E",
        };

        foreach (string prefabName in prefabNames)
        {
            if (Resources.Load<GameObject>("Prefabs/" + prefabName) == null)
                failures.Add("Resources/Prefabs 缺少：" + prefabName);
        }

        ValidateExternalModelAssets(failures);
        ValidatePropPrefab("BattlefieldProp_SupplyCrate", failures, "CrateWide", "CrateSmall");
        ValidatePropPrefab("BattlefieldProp_AmmoCrate", failures, "AmmoCrateBody", "AmmoClip", "Grenade");
        ValidatePropPrefab("BattlefieldProp_TargetMarker", failures, "TargetLarge");
        ValidatePropPrefab("BattlefieldProp_VehicleWreck", failures, "Drivetrain", "Bumper", "WheelA");
        ValidateProjectilePrefab("BattleProjectile_Bullet", failures, "BulletModel");
        ValidateProjectilePrefab("BattleProjectile_Shell", failures, "ShellModel");
        ValidateProjectilePrefab("BattleProjectile_Bomb", failures, "BombModel");
        ValidateProjectilePrefab("BattleProjectile_Flame", failures, "FlameModel");

        ValidatePrefabModel("Infantry_P", failures, "Model", "MixamoBasicShooter", "KenneyWeapon", "WW2RifleBarrel", "Muzzle", "FactionPlate");
        ValidatePrefabModel("Infantry_E", failures, "Model", "MixamoBasicShooter", "KenneyWeapon", "WW2RifleBarrel", "Muzzle", "FactionPlate");
        ValidatePrefabModel("Tank_P", failures, "Model", "KenneyVehicleBase", "KenneyTankCannon", "Muzzle", "FactionPlate");
        ValidatePrefabModel("Tank_E", failures, "Model", "KenneyVehicleBase", "KenneyTankCannon", "Muzzle", "FactionPlate");
        ValidatePrefabModel("AntiAirGun_P", failures, "Model", "KenneyVehicleBase", "KenneyTankCannon", "Muzzle", "FactionPlate");
        ValidatePrefabModel("AntiAirGun_E", failures, "Model", "KenneyVehicleBase", "KenneyTankCannon", "Muzzle", "FactionPlate");
        ValidatePrefabModel("Artillery_P", failures, "Model", "MixamoBasicShooter", "KenneyWeapon", "WW2ShoulderTube", "WW2ShoulderMuzzle", "Muzzle", "FactionPlate");
        ValidatePrefabModel("Artillery_E", failures, "Model", "MixamoBasicShooter", "KenneyWeapon", "WW2ShoulderTube", "WW2ShoulderMuzzle", "Muzzle", "FactionPlate");
        ValidatePrefabDoesNotContain("Infantry_P", failures, "WW2ShoulderTube", "WW2ShoulderMuzzle");
        ValidatePrefabDoesNotContain("Infantry_E", failures, "WW2ShoulderTube", "WW2ShoulderMuzzle");
        ValidatePrefabDoesNotContain("Artillery_P", failures, "WW2RifleBarrel", "WW2RifleStock", "RifleBarrel", "RifleStock", "WW2ShellBandolier", "KenneyAmmoBall", "ArtilleryWeaponSocket");
        ValidatePrefabDoesNotContain("Artillery_E", failures, "WW2RifleBarrel", "WW2RifleStock", "RifleBarrel", "RifleStock", "WW2ShellBandolier", "KenneyAmmoBall", "ArtilleryWeaponSocket");
        ValidatePrefabModel("Flamethrower_P", failures, "Model", "KenneyVehicleBase", "KenneyWeapon", "KenneyFuelTank", "KenneyFuelTankB", "Muzzle", "WW2FlameArmor", "FactionPlate");
        ValidatePrefabModel("Flamethrower_E", failures, "Model", "KenneyVehicleBase", "KenneyWeapon", "KenneyFuelTank", "KenneyFuelTankB", "Muzzle", "WW2FlameArmor", "FactionPlate");
        ValidatePrefabModel("Fighter_P", failures, "Model", "KenneyAircraft", "KenneyNosePod", "Muzzle", "FactionPlate");
        ValidatePrefabModel("Bomber_P", failures, "Model", "KenneyAircraft", "KenneyBombA", "KenneyBombB", "Muzzle", "FactionPlate");
        ValidatePrefabModel("ScoutPlane_P", failures, "Model", "KenneyAircraft", "KenneySensor", "Muzzle", "FactionPlate");
        ValidatePrefabModel("PatrolBoat_P", failures, "Model", "KenneyWatercraft", "PatrolBowGun", "Muzzle", "NavalFactionStripeL", "NavalFactionStripeR");
        ValidatePrefabModel("DestroyerShip_P", failures, "Model", "KenneyWatercraft", "DestroyerMainGun", "Muzzle", "DestroyerSignalMast", "NavalFactionStripeL");
        ValidatePrefabModel("TransportShip_P", failures, "Model", "KenneyWatercraft", "BattleshipForwardGun", "BattleshipAftGun", "BattleshipSignalMast", "Muzzle", "NavalFactionStripeL");
        ValidateAircraftSlotRequirement("Fighter_P", true, failures);
        ValidateAircraftSlotRequirement("Fighter_E", true, failures);
        ValidateAircraftSlotRequirement("Bomber_P", true, failures);
        ValidateAircraftSlotRequirement("Bomber_E", true, failures);
        ValidateAircraftSlotRequirement("ScoutPlane_P", false, failures);
        ValidateAircraftSlotRequirement("ScoutPlane_E", false, failures);
        ValidatePrefabParts("Turret_P", failures, "Model", "WW2TurretCannon", "Muzzle", "FactionPlate");
        ValidatePrefabParts("Turret_E", failures, "Model", "WW2TurretCannon", "Muzzle", "FactionPlate");
        ValidatePrefabParts("NavalYard_P", failures, "Model", "WW2NavalYardRamp", "WW2NavalYardCraneGate", "WW2NavalYardBoatPreview");
        ValidatePrefabParts("NavalYard_E", failures, "Model", "WW2NavalYardRamp", "WW2NavalYardCraneGate", "WW2NavalYardBoatPreview");
        ValidatePrefabParts("PowerPlant_P", failures, "Model", "WW2PowerPlantShed", "WW2PowerStackA", "WW2PowerFuelTankL", "WW2PowerGeneratorHouse", "WW2PowerSandbagFence", "FactionPlate");
        ValidatePrefabParts("PowerPlant_E", failures, "Model", "WW2PowerPlantShed", "WW2PowerStackA", "WW2PowerFuelTankL", "WW2PowerGeneratorHouse", "WW2PowerSandbagFence", "FactionPlate");
        ValidatePrefabAnimator("Infantry_P", failures);
        ValidatePrefabAnimator("Infantry_E", failures);
        ValidatePrefabAnimatorParameters("Infantry_P", failures, "Speed", "Fire", "Die");
        ValidatePrefabAnimatorParameters("Infantry_E", failures, "Speed", "Fire", "Die");
        ValidatePrefabAttachmentBinder("Infantry_P", failures, 2);
        ValidatePrefabAttachmentBinder("Infantry_E", failures, 2);
        ValidatePrefabAttachmentParent("Infantry_P", failures, "KenneyWeapon", "RightHand", "RightForeArm", "RightArm", "mixamorig:RightHand");
        ValidatePrefabAttachmentParent("Infantry_P", failures, "FactionPlate", "UpperChest", "Chest", "Spine", "mixamorig:Spine2", "mixamorig:Spine1");
        ValidatePrefabAttachmentParent("Infantry_E", failures, "KenneyWeapon", "RightHand", "RightForeArm", "RightArm", "mixamorig:RightHand");
        ValidatePrefabAttachmentParent("Infantry_E", failures, "FactionPlate", "UpperChest", "Chest", "Spine", "mixamorig:Spine2", "mixamorig:Spine1");
        ValidateUnitScaleTarget<Infantry>("Infantry_P", failures, 3.6f, 2.0f);
        ValidateUnitScaleTarget<Infantry>("Infantry_E", failures, 3.6f, 2.0f);
        ValidateUnitScaleTarget<Artillery>("Artillery_P", failures, 3.6f, 2.0f);
        ValidateUnitScaleTarget<Artillery>("Artillery_E", failures, 3.6f, 2.0f);
        ValidateUnitScaleTarget<AntiAirGun>("AntiAirGun_P", failures, 3.0f, 3.4f);
        ValidateUnitScaleTarget<AntiAirGun>("AntiAirGun_E", failures, 3.0f, 3.4f);
        ValidateUnitScaleTarget<Fighter>("Fighter_P", failures, 1.2f, 2.6f);
        ValidateUnitScaleTarget<Fighter>("Fighter_E", failures, 1.2f, 2.6f);
        ValidateUnitScaleTarget<PatrolBoat>("PatrolBoat_P", failures, 1.2f, 3.6f);
        ValidateUnitScaleTarget<DestroyerShip>("DestroyerShip_P", failures, 1.8f, 5.7f);
        ValidateUnitScaleTarget<TransportShip>("TransportShip_P", failures, 2.2f, 7.2f);
        ValidatePrefabProjectile("Infantry_P", failures, "Prefabs/Projectiles/BattleProjectile_Bullet");
        ValidatePrefabProjectile("Flamethrower_P", failures, "Prefabs/Projectiles/BattleProjectile_Flame");
        ValidatePrefabProjectile("Tank_P", failures, "Prefabs/Projectiles/BattleProjectile_Shell");
        ValidatePrefabProjectile("Artillery_P", failures, "Prefabs/Projectiles/BattleProjectile_Shell");
        ValidatePrefabProjectile("AntiAirGun_P", failures, "Prefabs/Projectiles/BattleProjectile_Bullet");
        ValidatePrefabProjectile("Fighter_P", failures, "Prefabs/Projectiles/BattleProjectile_Bullet");
        ValidatePrefabProjectile("Bomber_P", failures, "Prefabs/Projectiles/BattleProjectile_Bomb");
        ValidatePrefabProjectile("ScoutPlane_P", failures, "Prefabs/Projectiles/BattleProjectile_Bullet");
        ValidatePrefabProjectile("PatrolBoat_P", failures, "Prefabs/Projectiles/BattleProjectile_Bullet");
        ValidatePrefabProjectile("DestroyerShip_P", failures, "Prefabs/Projectiles/BattleProjectile_Shell");
        ValidatePrefabProjectile("TransportShip_P", failures, "Prefabs/Projectiles/BattleProjectile_Shell");
    }

    static void ValidateExternalModelAssets(List<string> failures)
    {
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlockyCharacters/Models/FBX format/character-a.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlockyCharacters/Models/FBX format/character-b.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/CarKit/Models/FBX format/tractor-shovel.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/CarKit/Models/FBX format/truck-flat.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/crate-wide.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/crate-medium.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/target-large.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/CarKit/Models/FBX format/debris-drivetrain.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/SpaceKit/Models/FBX format/rover.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/SpaceKit/Models/FBX format/craft_speederA.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/SpaceKit/Models/FBX format/craft_cargoB.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/SpaceKit/Models/FBX format/craft_speederD.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/bullet-foam-tip.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/grenade-a.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/grenade-b.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/BlasterKit/Models/FBX format/smoke.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/AnimatedCharactersSurvivors/Model/characterMedium.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/AnimatedCharactersSurvivors/Animations/idle.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/AnimatedCharactersSurvivors/Animations/run.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Kenney/AnimatedCharactersSurvivors/Animations/jump.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Downloads/city-industrial/Models/FBX format/building-c.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Downloads/city-industrial/Models/FBX format/chimney-large.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Downloads/city-industrial/Models/FBX format/detail-tank.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Downloads/factory-kit/Models/FBX format/machine-fortified.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Downloads/factory-kit/Models/FBX format/pipe-large-long.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/Downloads/factory-kit/Models/FBX format/catwalk-straight.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/boat-speed-e.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/ship-large.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/ramp-wide.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/gate.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/cargo-container-a.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/cargo-container-b.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/cargo-pile-a.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/buoy-flag.fbx");
        ValidateExternalModelAsset(failures, "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/boat-speed-a.fbx");
    }

    static void ValidateExternalModelAsset(List<string> failures, string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            failures.Add("External model file missing: " + assetPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
            failures.Add("External model did not import as GameObject: " + assetPath);
    }

    static void ValidatePrefabParts(string prefabName, List<string> failures, params string[] requiredParts)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        foreach (string partName in requiredParts)
        {
            if (!HasChildNamed(prefab.transform, partName))
                failures.Add(prefabName + " missing prefab part: " + partName);
        }
    }

    static void ValidatePrefabModel(string prefabName, List<string> failures, params string[] requiredParts)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        foreach (string partName in requiredParts)
        {
            if (!HasChildNamed(prefab.transform, partName))
                failures.Add(prefabName + " 缺少模型部件：" + partName);
        }

        if (prefab.GetComponent<UnitVisualAnimator>() == null)
            failures.Add(prefabName + " 缺少 UnitVisualAnimator。");
    }

    static void ValidatePrefabDoesNotContain(string prefabName, List<string> failures, params string[] forbiddenParts)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        foreach (string partName in forbiddenParts)
        {
            if (HasChildNamed(prefab.transform, partName))
                failures.Add(prefabName + " still contains forbidden artillery model part: " + partName);
        }
    }

    static void ValidatePrefabModelMatches(string prefabName, string referencePrefabName, List<string> failures)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        GameObject referencePrefab = Resources.Load<GameObject>("Prefabs/" + referencePrefabName);
        if (prefab == null || referencePrefab == null) return;

        Transform model = prefab.transform.Find("Model");
        Transform referenceModel = referencePrefab.transform.Find("Model");
        if (model == null || referenceModel == null)
        {
            failures.Add(prefabName + " or " + referencePrefabName + " is missing Model for visual comparison.");
            return;
        }

        string signature = BuildModelHierarchySignature(model);
        string referenceSignature = BuildModelHierarchySignature(referenceModel);
        if (signature != referenceSignature)
            failures.Add(prefabName + " Model hierarchy must match " + referencePrefabName + "; artillery should reuse the infantry visual model.");
    }

    static string BuildModelHierarchySignature(Transform modelRoot)
    {
        var paths = new List<string>();
        AppendModelHierarchy(modelRoot, modelRoot.name, paths);
        paths.Sort();
        return string.Join("\n", paths.ToArray());
    }

    static void AppendModelHierarchy(Transform node, string path, List<string> paths)
    {
        if (node == null)
            return;

        paths.Add(path);
        foreach (Transform child in node)
            AppendModelHierarchy(child, path + "/" + child.name, paths);
    }

    static void ValidatePrefabAnimator(string prefabName, List<string> failures)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            failures.Add(prefabName + " 缺少 Animator。");
            return;
        }

        if (animator.runtimeAnimatorController == null)
            failures.Add(prefabName + " Animator 未绑定 Controller。");
    }

    static void ValidatePrefabAnimatorParameters(string prefabName, List<string> failures, params string[] parameterNames)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            failures.Add(prefabName + " animator controller is not an AnimatorController asset.");
            return;
        }

        for (int i = 0; i < parameterNames.Length; i++)
        {
            bool found = false;
            for (int p = 0; p < controller.parameters.Length; p++)
            {
                if (controller.parameters[p].name == parameterNames[i])
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                failures.Add(prefabName + " animator missing parameter: " + parameterNames[i]);
        }
    }

    static void ValidatePrefabAttachmentBinder(string prefabName, List<string> failures, int minimumBindings)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        AnimatedUnitAttachmentBinder binder = prefab.GetComponentInChildren<AnimatedUnitAttachmentBinder>(true);
        if (binder == null)
        {
            failures.Add(prefabName + " missing AnimatedUnitAttachmentBinder.");
            return;
        }

        if (binder.Bindings == null || binder.Bindings.Length < minimumBindings)
            failures.Add(prefabName + " attachment bindings are incomplete.");
    }

    static void ValidateUnitScaleTarget<TUnit>(string prefabName, List<string> failures, float expectedHeight, float expectedFootprint)
        where TUnit : RTSUnit
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        TUnit unit = prefab.GetComponent<TUnit>();
        if (unit == null)
        {
            failures.Add(prefabName + " missing " + typeof(TUnit).Name + " component.");
            return;
        }

        float height = ReadProtectedFloat(unit, "DesiredVisualHeight");
        float footprint = ReadProtectedFloat(unit, "DesiredVisualFootprint");
        if (!Approximately(height, expectedHeight))
            failures.Add(prefabName + " visual height mismatch: " + height + " != " + expectedHeight);
        if (!Approximately(footprint, expectedFootprint))
            failures.Add(prefabName + " visual footprint mismatch: " + footprint + " != " + expectedFootprint);
    }

    static float ReadProtectedFloat(object instance, string propertyName)
    {
        System.Type type = instance.GetType();
        while (type != null)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property != null)
            {
                object value = property.GetValue(instance, null);
                if (value is float f)
                    return f;
            }

            type = type.BaseType;
        }

        return float.NaN;
    }

    static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.001f;
    }

    static void ValidatePrefabAttachmentParent(string prefabName, List<string> failures, string attachmentName, params string[] expectedParents)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        Transform attachment = FindChildNamed(prefab.transform, attachmentName);
        if (attachment == null)
        {
            failures.Add(prefabName + " missing attachment: " + attachmentName);
            return;
        }

        Transform parent = attachment.parent;
        if (parent == null)
        {
            failures.Add(prefabName + " attachment has no parent: " + attachmentName);
            return;
        }

        for (int i = 0; i < expectedParents.Length; i++)
        {
            if (parent.name == expectedParents[i])
                return;
        }

        failures.Add(prefabName + " attachment parent mismatch: " + attachmentName + " -> " + parent.name);
    }

    static void ValidatePropPrefab(string prefabName, List<string> failures, params string[] requiredParts)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Props/" + prefabName);
        if (prefab == null)
        {
            failures.Add("Resources/Prefabs/Props missing: " + prefabName);
            return;
        }

        foreach (string partName in requiredParts)
        {
            if (!HasChildNamed(prefab.transform, partName))
                failures.Add(prefabName + " missing prop model part: " + partName);
        }
    }

    static void ValidatePrefabProjectile(string prefabName, List<string> failures, string expectedPath)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        RTSUnit unit = prefab.GetComponent<RTSUnit>();
        if (unit == null)
        {
            failures.Add(prefabName + " missing RTSUnit component for projectile validation.");
            return;
        }

        if (unit.ProjectilePrefabPath != expectedPath)
            failures.Add(prefabName + " projectile path mismatch: " + unit.ProjectilePrefabPath + " expected " + expectedPath);
        if (Resources.Load<GameObject>(unit.ProjectilePrefabPath) == null)
            failures.Add(prefabName + " projectile prefab cannot be loaded: " + unit.ProjectilePrefabPath);
    }

    static void ValidateAircraftSlotRequirement(string prefabName, bool expected, List<string> failures)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (prefab == null) return;

        AirUnit air = prefab.GetComponent<AirUnit>();
        if (air == null)
        {
            failures.Add(prefabName + " missing AirUnit component for airfield slot validation.");
            return;
        }

        if (air.RequiresAirfieldSlot != expected)
            failures.Add(prefabName + " airfield slot requirement mismatch: " + air.RequiresAirfieldSlot + " expected " + expected);
    }

    static void ValidateProjectilePrefab(string prefabName, List<string> failures, params string[] requiredParts)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Projectiles/" + prefabName);
        if (prefab == null)
        {
            failures.Add("Resources/Prefabs/Projectiles missing: " + prefabName);
            return;
        }

        if (prefab.GetComponent<CombatProjectile>() == null)
            failures.Add(prefabName + " missing CombatProjectile component.");

        foreach (string partName in requiredParts)
        {
            if (!HasChildNamed(prefab.transform, partName))
                failures.Add(prefabName + " missing projectile model part: " + partName);
        }
    }

    static bool HasChildNamed(Transform root, string name)
    {
        if (root.name == name) return true;
        foreach (Transform child in root)
        {
            if (HasChildNamed(child, name))
                return true;
        }
        return false;
    }

    static Transform FindChildNamed(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildNamed(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    static void ValidateNoMissingScripts(List<string> failures)
    {
        foreach (GameObject go in GetSceneObjects())
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null)
                {
                    failures.Add("对象存在 Missing Script：" + GetHierarchyPath(go));
                    break;
                }
            }
        }
    }

    static void ValidateNoTextContains(string forbiddenText, List<string> failures)
    {
        foreach (Text text in Object.FindObjectsOfType<Text>(true))
        {
            if (text != null && !string.IsNullOrEmpty(text.text) && text.text.Contains(forbiddenText))
                failures.Add("LobbyScene still contains text: " + forbiddenText + " at " + GetHierarchyPath(text.gameObject));
        }
    }

    static int FindTextContains(string expectedText)
    {
        int count = 0;
        foreach (Text text in Object.FindObjectsOfType<Text>(true))
        {
            if (text != null && !string.IsNullOrEmpty(text.text) && text.text.Contains(expectedText))
                count++;
        }
        return count;
    }

    static bool ValidateOptionalUiRectsDoNotOverlap(string firstName, string secondName, List<string> failures, string message)
    {
        RectTransform first = FindRectTransform(firstName);
        RectTransform second = FindRectTransform(secondName);
        if (first == null && second == null)
            return false;
        if (first == null || second == null)
        {
            failures.Add("Incomplete UI rect pair: " + firstName + " / " + secondName);
            return false;
        }

        if (GetWorldRect(first).Overlaps(GetWorldRect(second)))
            failures.Add(message);
        return true;
    }

    static void ValidateReturnBattleRuntimeLayout(List<string> failures)
    {
        Vector2 anchor = LobbyManager.ReturnBattleButtonAnchor;
        Vector2 size = LobbyManager.ReturnBattleButtonSize;
        if (anchor.y > 0.86f)
            failures.Add("ReturnBattleButton is too high and may overlap the top resource bar.");
        if (anchor.y < 0.72f)
            failures.Add("ReturnBattleButton is too low and may overlap the center match card.");
        if (size.x > 240f || size.y > 56f)
            failures.Add("ReturnBattleButton is too large for the top-center lobby gap.");
    }

    static RectTransform FindRectTransform(string name)
    {
        foreach (GameObject go in GetSceneObjects())
        {
            if (go.name == name)
                return go.GetComponent<RectTransform>();
        }
        return null;
    }

    static Rect GetWorldRect(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float minX = corners[0].x;
        float minY = corners[0].y;
        float maxX = corners[0].x;
        float maxY = corners[0].y;
        for (int i = 1; i < corners.Length; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxX = Mathf.Max(maxX, corners[i].x);
            maxY = Mathf.Max(maxY, corners[i].y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>允许少量浮点误差，判断内矩形是否完整落在外矩形范围内。</summary>
    static bool RectContainsRect(Rect outer, Rect inner)
    {
        return inner.xMin >= outer.xMin - 0.5f
            && inner.xMax <= outer.xMax + 0.5f
            && inner.yMin >= outer.yMin - 0.5f
            && inner.yMax <= outer.yMax + 0.5f;
    }

    static void ValidateBounds(string mapName, string group, TerrainStripSpec[] values, List<string> failures)
    {
        if (values == null) return;
        foreach (TerrainStripSpec value in values)
            ValidateBounds(mapName, group + "/" + value.Name, value.Center, failures, BattleMapCatalog.MapHalfSize + 15f);
    }

    static void ValidateBounds(string mapName, string group, TerrainPatchSpec[] values, List<string> failures)
    {
        if (values == null) return;
        foreach (TerrainPatchSpec value in values)
            ValidateBounds(mapName, group + "/" + value.Name, value.Center, failures, BattleMapCatalog.MapHalfSize + 15f);
    }

    static void ValidateBounds(string mapName, string group, Vector3[] values, List<string> failures)
    {
        if (values == null) return;
        foreach (Vector3 value in values)
            ValidateBounds(mapName, group, value, failures, BattleMapCatalog.MapHalfSize + 15f);
    }

    static void ValidateBounds(string mapName, string label, Vector3 pos, List<string> failures, float limit)
    {
        if (Mathf.Abs(pos.x) > limit || Mathf.Abs(pos.z) > limit)
            failures.Add(mapName + " 的 " + label + " 坐标超出地图边界：" + pos);
    }

    static void RequireObject(string name, List<string> failures)
    {
        if (GameObject.Find(name) == null)
            failures.Add("缺少对象：" + name);
    }

    static int FindByPrefix(string prefix)
    {
        int count = 0;
        foreach (GameObject go in GetSceneObjects())
        {
            if (go.name.StartsWith(prefix)) count++;
        }
        return count;
    }

    static int FindExact(string name)
    {
        int count = 0;
        foreach (GameObject go in GetSceneObjects())
        {
            if (go.name == name) count++;
        }
        return count;
    }

    static IEnumerable<GameObject> GetSceneObjects()
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid()) continue;
            if (go.hideFlags != HideFlags.None) continue;
            yield return go;
        }
    }

    static string GetHierarchyPath(GameObject go)
    {
        var names = new Stack<string>();
        Transform t = go.transform;
        while (t != null)
        {
            names.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", names.ToArray());
    }
}
