using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System.IO;

// 自动搭建登录场景和游戏场景的UI框架
public class SceneBuilder
{
    // ── 登录场景 ──────────────────────────────────────────────
    /// <summary>生成新版登录场景，包括登录面板、注册面板和游客入口。</summary>
    [MenuItem("RTS/生成场景/① 生成登录场景")]
    public static void BuildLoginScene()
    {
        // 新建或打开场景
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 相机设置
        Camera cam = Camera.main;
        if (cam != null) cam.backgroundColor = new Color(0.08f, 0.12f, 0.2f);

        // EventSystem
        CreateEventSystem();

        // Canvas
        GameObject canvasGO = CreateCanvas("MainCanvas");
        Canvas canvas = canvasGO.GetComponent<Canvas>();

        // ── 战场背景遮罩（可在编辑器里把 Background 的 Image Sprite 替换成战场图）──
        var bgImg = CreateImage(canvasGO.transform, "Background",
            new Color(0.04f, 0.05f, 0.09f), Vector2.zero, Vector2.one);
        // 中央由下至上的渐变暗角（用第二层半透明黑遮住背景下半部，让卡片更突出）
        CreateImage(canvasGO.transform, "Vignette",
            new Color(0f, 0f, 0f, 0.52f), Vector2.zero, Vector2.one);
        // 顶部金色细线
        {
            var tl = new GameObject("BgTopLine"); tl.transform.SetParent(canvasGO.transform, false);
            var tlrt = tl.AddComponent<RectTransform>();
            tlrt.anchorMin = new Vector2(0, 1); tlrt.anchorMax = Vector2.one;
            tlrt.offsetMin = new Vector2(0, -4); tlrt.offsetMax = Vector2.zero;
            tl.AddComponent<Image>().color = new Color(0.90f, 0.68f, 0.10f, 1f);
        }
        // 底部金色细线
        {
            var bl = new GameObject("BgBottomLine"); bl.transform.SetParent(canvasGO.transform, false);
            var blrt = bl.AddComponent<RectTransform>();
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = new Vector2(1, 0);
            blrt.offsetMin = Vector2.zero; blrt.offsetMax = new Vector2(0, 4);
            bl.AddComponent<Image>().color = new Color(0.90f, 0.68f, 0.10f, 0.7f);
        }

        // ── 新版标题区域：明显区别于旧的居中登录页 ─────────────────
        // 主标题左置，做成战区指挥大屏的感觉，避免打包后又像旧界面。
        {
            var titleGO = CreateText(canvasGO.transform, "Title", "前线指挥终端",
                new Vector2(0.24f, 0.86f), new Vector2(660, 92), 56);
            var tt = titleGO.GetComponent<Text>();
            tt.fontStyle = FontStyle.Bold;
            tt.color = new Color(1f, 0.85f, 0.18f);
            tt.alignment = TextAnchor.MiddleLeft;
            var ot = titleGO.AddComponent<Outline>();
            ot.effectColor = new Color(0.35f, 0.15f, 0f, 1f);
            ot.effectDistance = new Vector2(3, -3);
        }
        // 副标题
        {
            var sub = CreateText(canvasGO.transform, "SubTitle", "ONLINE WARROOM  /  SEA · AIR · ARMOR  /  2026 新版 UI",
                new Vector2(0.24f, 0.805f), new Vector2(660, 32), 17,
                new Color(0.86f, 0.78f, 0.54f));
            var st = sub.GetComponent<Text>();
            st.fontStyle = FontStyle.BoldAndItalic;
            st.alignment = TextAnchor.MiddleLeft;
        }

        // 左侧情报卡：让新版登录页一眼可见，不再只有一个老式账号框。
        GameObject briefingPanel = new GameObject("NewLoginBriefingPanel");
        briefingPanel.transform.SetParent(canvasGO.transform, false);
        {
            var brt = briefingPanel.AddComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.22f, 0.45f);
            brt.sizeDelta = new Vector2(520, 360);
            brt.anchoredPosition = Vector2.zero;
            briefingPanel.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.050f, 0.70f);
            var outline = briefingPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.90f, 0.68f, 0.10f, 0.44f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
        {
            var tag = CreateText(briefingPanel.transform, "BriefingTag", "NEW COMMAND UI",
                new Vector2(0.12f, 0.84f), new Vector2(210, 32), 18, new Color(1f, 0.82f, 0.28f));
            tag.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            var body = CreateText(briefingPanel.transform, "BriefingBody",
                "快速入场 · 联机匹配 · 海陆空战场\n\n新版界面已写入生成器，后续打包不会再被旧场景覆盖。\n\n选择游客快速入场，直接进入大厅验证匹配流程。",
                new Vector2(0.52f, 0.48f), new Vector2(420, 210), 24, new Color(0.90f, 0.95f, 0.88f));
            var bt = body.GetComponent<Text>();
            bt.alignment = TextAnchor.UpperLeft;
            bt.lineSpacing = 1.25f;
        }

        // ── 登录面板（右侧作战终端风格）─────────────────────────
        GameObject loginPanel = new GameObject("LoginPanel");
        loginPanel.transform.SetParent(canvasGO.transform, false);
        {
            var lrt = loginPanel.AddComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.74f, 0.50f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(520, 500);
            loginPanel.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.052f, 0.94f);
            var outline = loginPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.90f, 0.68f, 0.10f, 0.58f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
        // 面板左侧金色竖条
        {
            var ls = new GameObject("LeftStrip"); ls.transform.SetParent(loginPanel.transform, false);
            var lsrt = ls.AddComponent<RectTransform>();
            lsrt.anchorMin = Vector2.zero; lsrt.anchorMax = new Vector2(0, 1);
            lsrt.offsetMin = Vector2.zero; lsrt.offsetMax = new Vector2(4, 0);
            ls.AddComponent<Image>().color = new Color(0.90f, 0.68f, 0.10f, 1f);
        }
        // 面板顶部标题条（深橄榄色）
        {
            var lh = new GameObject("LoginHeader"); lh.transform.SetParent(loginPanel.transform, false);
            var lhrt = lh.AddComponent<RectTransform>();
            lhrt.anchorMin = new Vector2(0, 1); lhrt.anchorMax = Vector2.one;
            lhrt.offsetMin = new Vector2(0, -52); lhrt.offsetMax = Vector2.zero;
            lh.AddComponent<Image>().color = new Color(0.14f, 0.20f, 0.10f, 1f);
            // 标题条底部金色分割线
            var hdivGO = new GameObject("HeaderDiv"); hdivGO.transform.SetParent(lh.transform, false);
            var hdivRT = hdivGO.AddComponent<RectTransform>();
            hdivRT.anchorMin = new Vector2(0.04f, 0); hdivRT.anchorMax = new Vector2(0.96f, 0);
            hdivRT.offsetMin = Vector2.zero; hdivRT.offsetMax = new Vector2(0, 2);
            hdivGO.AddComponent<Image>().color = new Color(0.90f, 0.68f, 0.10f, 0.9f);
            // 标题文字（左对齐带金色小方块前缀）
            var lhTxt = CreateText(lh.transform, "TitleLogin", "▪  作战身份验证",
                new Vector2(0.5f, 0.52f), new Vector2(400, 40), 22);
            var lhT = lhTxt.GetComponent<Text>();
            lhT.fontStyle = FontStyle.Bold;
            lhT.color = new Color(0.92f, 0.84f, 0.60f);
            lhT.alignment = TextAnchor.MiddleLeft;
        }
        {
            var status = CreateText(loginPanel.transform, "LoginPanelEdition", "新版战区 UI · BUILD 2026.05.26",
                new Vector2(0.5f, 0.865f), new Vector2(420, 24), 15, new Color(0.72f, 0.88f, 0.76f));
            status.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        }
        // 用户名输入框
        {
            var uif = CreateInputField(loginPanel.transform, "LoginUsernameInput", "  用户名",
                new Vector2(0.5f, 0.62f), new Vector2(420, 56));
            uif.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            // 左侧金色竖条
            AddFieldAccent(uif.transform, new Color(0.90f, 0.68f, 0.10f, 0.9f));
            var ph = uif.transform.Find("Placeholder")?.GetComponent<Text>();
            if (ph != null) { ph.color = new Color(0.50f, 0.54f, 0.44f); ph.fontStyle = FontStyle.Italic; }
        }
        // 密码输入框
        {
            var pif = CreateInputField(loginPanel.transform, "LoginPasswordInput", "  密码",
                new Vector2(0.5f, 0.47f), new Vector2(420, 56), true);
            pif.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            AddFieldAccent(pif.transform, new Color(0.90f, 0.68f, 0.10f, 0.9f));
            var ph = pif.transform.Find("Placeholder")?.GetComponent<Text>();
            if (ph != null) { ph.color = new Color(0.50f, 0.54f, 0.44f); ph.fontStyle = FontStyle.Italic; }
        }
        // 主登录按钮（军事金）
        {
            var lb = CreateButton(loginPanel.transform, "LoginButton", "登  录",
                new Vector2(0.5f, 0.315f), new Vector2(420, 60), new Color(0.62f, 0.45f, 0.07f));
            var lbt = lb.GetComponentInChildren<Text>();
            lbt.fontSize = 23; lbt.fontStyle = FontStyle.Bold;
            lbt.color = new Color(1f, 0.96f, 0.82f);
            // 按钮顶部亮线
            AddButtonTopLine(lb.transform, new Color(1f, 0.88f, 0.35f, 0.85f));
        }
        // 下方双按钮
        var guestButton = CreateButton(loginPanel.transform, "GuestLoginButton", "游客快速入场",
            new Vector2(0.32f, 0.165f), new Vector2(210, 50), new Color(0.18f, 0.22f, 0.28f));
        guestButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        var registerButton = CreateButton(loginPanel.transform, "SwitchToRegisterButton", "创建指挥官",
            new Vector2(0.72f, 0.165f), new Vector2(176, 50), new Color(0.14f, 0.26f, 0.16f));
        registerButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        CreateText(loginPanel.transform, "LoginStatusText", "",
            new Vector2(0.5f, 0.055f), new Vector2(420, 26), 15, new Color(1f, 0.42f, 0.28f));

        // ── 注册面板 ──────────────────────────────────────────
        GameObject regPanel = new GameObject("RegisterPanel");
        regPanel.transform.SetParent(canvasGO.transform, false);
        {
            var rrt = regPanel.AddComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0, -10);
            rrt.sizeDelta = new Vector2(460, 470);
            regPanel.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        }
        regPanel.SetActive(false);
        // 左侧绿色竖条
        {
            var rs = new GameObject("LeftStrip"); rs.transform.SetParent(regPanel.transform, false);
            var rsrt = rs.AddComponent<RectTransform>();
            rsrt.anchorMin = Vector2.zero; rsrt.anchorMax = new Vector2(0, 1);
            rsrt.offsetMin = Vector2.zero; rsrt.offsetMax = new Vector2(4, 0);
            rs.AddComponent<Image>().color = new Color(0.28f, 0.72f, 0.32f, 1f);
        }
        // 顶部标题条（深绿）
        {
            var rh = new GameObject("RegHeader"); rh.transform.SetParent(regPanel.transform, false);
            var rhrt = rh.AddComponent<RectTransform>();
            rhrt.anchorMin = new Vector2(0, 1); rhrt.anchorMax = Vector2.one;
            rhrt.offsetMin = new Vector2(0, -52); rhrt.offsetMax = Vector2.zero;
            rh.AddComponent<Image>().color = new Color(0.10f, 0.20f, 0.12f, 1f);
            var hdiv = new GameObject("HeaderDiv"); hdiv.transform.SetParent(rh.transform, false);
            var hdivRT = hdiv.AddComponent<RectTransform>();
            hdivRT.anchorMin = new Vector2(0.04f, 0); hdivRT.anchorMax = new Vector2(0.96f, 0);
            hdivRT.offsetMin = Vector2.zero; hdivRT.offsetMax = new Vector2(0, 2);
            hdiv.AddComponent<Image>().color = new Color(0.28f, 0.72f, 0.32f, 0.9f);
            var rhTxt = CreateText(rh.transform, "TitleReg", "▪  注册账号",
                new Vector2(0.5f, 0.52f), new Vector2(400, 40), 22);
            var rhT = rhTxt.GetComponent<Text>();
            rhT.fontStyle = FontStyle.Bold;
            rhT.color = new Color(0.72f, 0.92f, 0.62f);
            rhT.alignment = TextAnchor.MiddleLeft;
        }
        // 三个输入框（统一深色+绿色左侧竖条）
        {
            var ri1 = CreateInputField(regPanel.transform, "RegUsernameInput", "  用户名（3字符以上）",
                new Vector2(0.5f, 0.74f), new Vector2(400, 52));
            ri1.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            AddFieldAccent(ri1.transform, new Color(0.28f, 0.72f, 0.32f, 0.9f));
            var ph1 = ri1.transform.Find("Placeholder")?.GetComponent<Text>();
            if (ph1 != null) { ph1.color = new Color(0.42f, 0.52f, 0.44f); ph1.fontStyle = FontStyle.Italic; }
        }
        {
            var ri2 = CreateInputField(regPanel.transform, "RegPasswordInput", "  密码（6字符以上）",
                new Vector2(0.5f, 0.59f), new Vector2(400, 52), true);
            ri2.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            AddFieldAccent(ri2.transform, new Color(0.28f, 0.72f, 0.32f, 0.9f));
            var ph2 = ri2.transform.Find("Placeholder")?.GetComponent<Text>();
            if (ph2 != null) { ph2.color = new Color(0.42f, 0.52f, 0.44f); ph2.fontStyle = FontStyle.Italic; }
        }
        {
            var ri3 = CreateInputField(regPanel.transform, "RegConfirmInput", "  确认密码",
                new Vector2(0.5f, 0.44f), new Vector2(400, 52), true);
            ri3.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            AddFieldAccent(ri3.transform, new Color(0.28f, 0.72f, 0.32f, 0.9f));
            var ph3 = ri3.transform.Find("Placeholder")?.GetComponent<Text>();
            if (ph3 != null) { ph3.color = new Color(0.42f, 0.52f, 0.44f); ph3.fontStyle = FontStyle.Italic; }
        }
        {
            var rb = CreateButton(regPanel.transform, "RegisterButton", "注  册",
                new Vector2(0.5f, 0.30f), new Vector2(400, 56), new Color(0.16f, 0.42f, 0.20f));
            var rbt = rb.GetComponentInChildren<Text>();
            rbt.fontSize = 23; rbt.fontStyle = FontStyle.Bold;
            rbt.color = new Color(0.82f, 1f, 0.82f);
            AddButtonTopLine(rb.transform, new Color(0.45f, 1f, 0.48f, 0.7f));
        }
        CreateButton(regPanel.transform, "SwitchToLoginButton", "返回登录",
            new Vector2(0.5f, 0.155f), new Vector2(200, 46), new Color(0.18f, 0.22f, 0.28f));
        CreateText(regPanel.transform, "RegisterStatusText", "",
            new Vector2(0.5f, 0.04f), new Vector2(400, 26), 15, new Color(1f, 0.42f, 0.28f));

        // ── 游客昵称弹窗（带全屏遮罩） ────────────────────────
        GameObject guestPopup = new GameObject("GuestNamePopup");
        guestPopup.transform.SetParent(canvasGO.transform, false);
        {
            var grt = guestPopup.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = grt.offsetMax = Vector2.zero;
            guestPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        }
        guestPopup.SetActive(false);
        // 居中内容卡片
        GameObject guestCard = new GameObject("GuestCard");
        guestCard.transform.SetParent(guestPopup.transform, false);
        {
            var gcrt = guestCard.AddComponent<RectTransform>();
            gcrt.anchorMin = gcrt.anchorMax = gcrt.pivot = new Vector2(0.5f, 0.5f);
            gcrt.sizeDelta = new Vector2(420, 290);
            guestCard.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        }
        // 左侧青色竖条
        {
            var gs = new GameObject("LeftStrip"); gs.transform.SetParent(guestCard.transform, false);
            var gsrt = gs.AddComponent<RectTransform>();
            gsrt.anchorMin = Vector2.zero; gsrt.anchorMax = new Vector2(0, 1);
            gsrt.offsetMin = Vector2.zero; gsrt.offsetMax = new Vector2(4, 0);
            gs.AddComponent<Image>().color = new Color(0.25f, 0.78f, 0.85f, 1f);
        }
        // 顶部标题条（深青）
        {
            var gh = new GameObject("GuestHeader"); gh.transform.SetParent(guestCard.transform, false);
            var ghrt = gh.AddComponent<RectTransform>();
            ghrt.anchorMin = new Vector2(0, 1); ghrt.anchorMax = Vector2.one;
            ghrt.offsetMin = new Vector2(0, -52); ghrt.offsetMax = Vector2.zero;
            gh.AddComponent<Image>().color = new Color(0.08f, 0.18f, 0.22f, 1f);
            var gdiv = new GameObject("HeaderDiv"); gdiv.transform.SetParent(gh.transform, false);
            var gdivRT = gdiv.AddComponent<RectTransform>();
            gdivRT.anchorMin = new Vector2(0.04f, 0); gdivRT.anchorMax = new Vector2(0.96f, 0);
            gdivRT.offsetMin = Vector2.zero; gdivRT.offsetMax = new Vector2(0, 2);
            gdiv.AddComponent<Image>().color = new Color(0.25f, 0.78f, 0.85f, 0.9f);
            var ghTxt = CreateText(gh.transform, "GuestTitle", "▪  输入游客昵称",
                new Vector2(0.5f, 0.52f), new Vector2(360, 40), 21);
            var ghT = ghTxt.GetComponent<Text>();
            ghT.fontStyle = FontStyle.Bold;
            ghT.color = new Color(0.68f, 0.92f, 0.96f);
            ghT.alignment = TextAnchor.MiddleLeft;
        }
        {
            var gni = CreateInputField(guestCard.transform, "GuestNameInput", "  昵称（2~12字符）",
                new Vector2(0.5f, 0.56f), new Vector2(360, 52));
            gni.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            AddFieldAccent(gni.transform, new Color(0.25f, 0.78f, 0.85f, 0.9f));
            var ph = gni.transform.Find("Placeholder")?.GetComponent<Text>();
            if (ph != null) { ph.color = new Color(0.40f, 0.50f, 0.55f); ph.fontStyle = FontStyle.Italic; }
        }
        CreateText(guestCard.transform, "GuestStatusText", "",
            new Vector2(0.5f, 0.38f), new Vector2(360, 26), 14, new Color(1f, 0.42f, 0.28f));
        {
            var gcb = CreateButton(guestCard.transform, "GuestConfirmButton", "确  认",
                new Vector2(0.29f, 0.165f), new Vector2(148, 48), new Color(0.62f, 0.45f, 0.07f));
            gcb.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            AddButtonTopLine(gcb.transform, new Color(1f, 0.88f, 0.35f, 0.7f));
        }
        CreateButton(guestCard.transform, "GuestCancelButton", "取  消",
            new Vector2(0.71f, 0.165f), new Vector2(148, 48), new Color(0.36f, 0.12f, 0.12f));

        // 添加LoginManager脚本并绑定引用
        GameObject mgr = new GameObject("LoginManager");
        // 注：绑定引用需在Editor中手动完成，或通过Find动态绑定

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LoginScene.unity");
        Debug.Log("✅ 登录场景生成完毕: Assets/Scenes/LoginScene.unity");
    }

    // ── 游戏场景 ──────────────────────────────────────────────
    /// <summary>使用默认地图重建 GameScene。</summary>
    [MenuItem("RTS/生成场景/② 生成游戏场景（地形+HUD）")]
    public static void BuildGameScene()
    {
        BuildGameScene(BattleMapCatalog.DefaultMapName);
    }

    [MenuItem("RTS/地图/生成预览/沙漠绿洲")]
    public static void BuildSandOasisScene()
    {
        BuildGameScene(BattleMapCatalog.DefaultMapName);
    }

    [MenuItem("RTS/地图/生成预览/冰雪要塞")]
    public static void BuildIceFortressScene()
    {
        BuildGameScene(BattleMapCatalog.IceFortressName);
    }

    [MenuItem("RTS/地图/生成预览/丛林战场")]
    public static void BuildJungleScene()
    {
        BuildGameScene(BattleMapCatalog.JungleName);
    }

    [MenuItem("RTS/地图/生成预览/城市废墟")]
    public static void BuildCityRuinsScene()
    {
        BuildGameScene(BattleMapCatalog.CityRuinsName);
    }

    [MenuItem("RTS/地图/生成预览/海图群岛")]
    public static void BuildSeaChartScene()
    {
        BuildGameScene(BattleMapCatalog.SeaChartName);
    }

    [MenuItem("RTS/地图/生成预览/全球争霸")]
    public static void BuildGlobalConquestScene()
    {
        BuildGameScene(BattleMapCatalog.GlobalConquestName);
    }

    /// <summary>根据地图定义重建游戏场景，同时生成地形、单位出生点和 HUD。</summary>
    public static void BuildGameScene(string mapName)
    {
        BattleMapDefinition map = BattleMapCatalog.Get(mapName);
        BuildBattleScene(map, "Assets/Scenes/GameScene.unity", true, false);
    }

    public static void BuildPreviewScene(BattleMapDefinition map, string scenePath = "Assets/Scenes/MapEditorPreview.unity")
    {
        BuildBattleScene(map, scenePath, false, true);
    }

    static void BuildBattleScene(BattleMapDefinition map, string scenePath, bool runAutomatedTest, bool includeSpawnMarkers)
    {
        if (map == null)
        {
            Debug.LogError("[SceneBuilder] Cannot build battle scene: map is null.");
            return;
        }

        UnityEngine.Random.InitState(map.RandomSeed);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 主定向光（暖黄日光 + 软阴影）
        GameObject light = new GameObject("DirectionalLight");
        Light l = light.AddComponent<Light>();
        l.type      = LightType.Directional;
        l.intensity = 1.08f;
        l.color     = new Color(1.0f, 0.93f, 0.76f);   // 暖黄阳光色
        l.shadows   = LightShadows.Soft;
        l.shadowStrength = 0.55f;
        l.shadowBias     = 0.04f;
        light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

        // 补光（冷蓝天空色，来自左后方，无阴影）
        GameObject fillLight = new GameObject("FillLight");
        Light fl = fillLight.AddComponent<Light>();
        fl.type      = LightType.Directional;
        fl.intensity = 0.32f;
        fl.color     = new Color(0.55f, 0.70f, 1.0f);  // 天空蓝
        fl.shadows   = LightShadows.None;
        fillLight.transform.rotation = Quaternion.Euler(30f, 155f, 0f);

        // ── 环境：地面 ──────────────────────────────────────
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        float mapHalf = BattleMapCatalog.MapHalfSize;
        ground.transform.localScale = new Vector3(mapHalf / 5f, 1, mapHalf / 5f);
        var groundMat = new Material(Shader.Find("Standard"));
        groundMat.color = Color.Lerp(map.GroundColor, Color.black, 0.18f);
        groundMat.SetFloat("_Metallic", 0f);
        groundMat.SetFloat("_Glossiness", 0.08f);
        ground.GetComponent<Renderer>().material = groundMat;
        // 标记为导航静态体，供 NavMesh 烘焙使用
        MarkNavigationStatic(ground);

        RuntimeBattleMapBuilder.Rebuild(map);
        if (includeSpawnMarkers)
            SpawnSpawnPointPreviewMarkers(map);

        // 光照强化（半球环境光）
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = map.AmbientSkyColor;
        RenderSettings.ambientEquatorColor = map.AmbientEquatorColor;
        RenderSettings.ambientGroundColor  = map.AmbientGroundColor;
        // 轻度雾效
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = map.FogStart;
        RenderSettings.fogEndDistance   = map.FogEnd;
        RenderSettings.fogColor = map.FogColor;

        // RTS相机
        Camera cam = Camera.main ?? new GameObject("Main Camera").AddComponent<Camera>();
        cam.gameObject.tag = "MainCamera";
        cam.transform.position = new Vector3(-120, 32, -138);  // 玩家基地近景，优先看清 FBX 单位和建筑
        cam.cullingMask = ~0;  // 渲染所有层（MinimapDot 由 Camera 回调控制可见性）
        cam.backgroundColor = map.SkyColor;
        cam.transform.rotation = Quaternion.Euler(61, 0, 0);
        cam.fieldOfView = 36f;
        cam.orthographic = false;
        var rtsCamera = cam.gameObject.AddComponent<RTSCamera>();
        rtsCamera.MinX = -180f; rtsCamera.MaxX = 180f;
        rtsCamera.MinZ = -180f; rtsCamera.MaxZ = 180f;
        rtsCamera.MinY = 20f;   rtsCamera.MaxY = 125f;
        rtsCamera.MoveSpeed = 36f;
        rtsCamera.DefaultPitch = 61f;
        rtsCamera.DefaultFieldOfView = 36f;

        // Managers
        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("RTSPlayerState").AddComponent<RTSPlayerState>();
        var pc = new GameObject("RTSPlayerController").AddComponent<RTSPlayerController>();
        pc.GroundLayer   = LayerMask.GetMask("Default");
        pc.UnitLayer     = LayerMask.GetMask("Default");
        pc.BuildingLayer = LayerMask.GetMask("Default");
        new GameObject("AIController").AddComponent<AIController>();

        // 建筑在运行时由 GameInitializer 动态创建，避免 batch mode 下 MonoBehaviour 引用序列化失败
        new GameObject("GameInitializer").AddComponent<GameInitializer>();

        // EventSystem
        CreateEventSystem();

        // HUD Canvas
        GameObject hudCanvas = CreateCanvas("HUDCanvas");

        // 顶部信息栏
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(hudCanvas.transform, false);
        {
            var rt2 = topBar.AddComponent<RectTransform>();
            rt2.anchorMin = new Vector2(0, 1); rt2.anchorMax = Vector2.one;
            rt2.offsetMin = new Vector2(0, -76); rt2.offsetMax = Vector2.zero;
            var topBarImage = topBar.AddComponent<Image>();
            topBarImage.color = new Color(0f, 0f, 0f, 0f);
            topBarImage.raycastTarget = false;
        }
        // 底部金色强调线
        {
            var accent = new GameObject("TopBarAccent");
            accent.transform.SetParent(topBar.transform, false);
            var art = accent.AddComponent<RectTransform>();
            art.anchorMin = Vector2.zero; art.anchorMax = new Vector2(1, 0);
            art.offsetMin = Vector2.zero; art.offsetMax = new Vector2(0, 2);
            accent.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        }
        // 竖向分割线辅助方法
        System.Action<float> addDiv = (float x) => {
            var d = new GameObject("Div"); d.transform.SetParent(topBar.transform, false);
            var drt = d.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(x, 0.15f); drt.anchorMax = new Vector2(x, 0.85f);
            drt.sizeDelta = new Vector2(1, 0);
            d.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        };
        addDiv(0.20f); addDiv(0.40f); addDiv(0.60f); addDiv(0.80f);
        // 各项数据文本
        var goldTxt = CreateText(topBar.transform, "GoldText",  "$ 800",
            new Vector2(0.17f, 0.56f), new Vector2(230, 42), 22, new Color(1f, 0.88f, 0.2f));
        goldTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
        var popTxt = CreateText(topBar.transform, "PopText",  "人口 0/20",
            new Vector2(0.32f, 0.56f), new Vector2(150, 42), 21, new Color(0.65f, 0.9f, 1f));
        popTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
        var killTxt = CreateText(topBar.transform, "KillText", "击杀 0",
            new Vector2(0.50f, 0.52f), new Vector2(160, 42), 21, new Color(1f, 0.42f, 0.42f));
        killTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
        killTxt.SetActive(false);
        var powerTxt = CreateText(topBar.transform, "PowerText", "电力 0/0",
            new Vector2(0.64f, 0.56f), new Vector2(160, 42), 21, new Color(0.4f, 1f, 0.55f));
        powerTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
        var timerTxt = CreateText(topBar.transform, "GameTimerText", "00:00",
            new Vector2(0.77f, 0.56f), new Vector2(110, 42), 24, new Color(0.88f, 0.93f, 1f));
        timerTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // 居中警报文本（屏幕顶部中央）
        GameObject alertGO = CreateText(hudCanvas.transform, "AlertText", "",
            new Vector2(0.5f, 0.88f), new Vector2(700, 60), 28, new Color(1f, 0.28f, 0.28f));
        {
            var at = alertGO.GetComponent<Text>();
            at.fontStyle = FontStyle.Bold;
            // 描边增加可读性
            var ao = alertGO.AddComponent<Outline>();
            ao.effectColor = new Color(0.3f, 0f, 0f, 0.9f);
            ao.effectDistance = new Vector2(1.5f, -1.5f);
        }
        alertGO.SetActive(false);

        // 单位信息面板（右下）
        GameObject unitPanel = CreatePanel(hudCanvas.transform, "UnitInfoPanel",
            new Vector2(1, 0), new Vector2(248, 150));
        {
            var urt = unitPanel.GetComponent<RectTransform>();
            urt.anchorMin = new Vector2(1, 0); urt.anchorMax = new Vector2(1, 0);
            urt.pivot = new Vector2(1, 0);
            urt.anchoredPosition = new Vector2(-12, 34);
            unitPanel.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.16f, 0.95f);
        }
        // 彩色顶部标题栏（青色）
        CreateImage(unitPanel.transform, "UnitPanelHeader",
            new Color(0.04f, 0.13f, 0.17f, 1f), new Vector2(0, 0.80f), Vector2.one);
        {
            var hn = CreateText(unitPanel.transform, "UnitNameText", "单位名",
                new Vector2(0.5f, 0.91f), new Vector2(270, 32), 21);
            hn.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }
        // HP 标签
        var unitHpLabel = CreateText(unitPanel.transform, "UnitHPLabel", "生命值",
            new Vector2(0.12f, 0.69f), new Vector2(80, 22), 14, new Color(0.7f, 0.9f, 0.7f));
        unitHpLabel.SetActive(false);
        CreateSlider(unitPanel.transform, "UnitHPBar",
            new Vector2(0.5f, 0.59f), new Vector2(220, 14), new Color(0.15f, 0.85f, 0.35f));
        CreateText(unitPanel.transform, "UnitHPText", "---/---",
            new Vector2(0.5f, 0.35f), new Vector2(220, 58), 12, new Color(0.85f, 1f, 0.85f));
        CreateButton(unitPanel.transform, "SkillButton", "穿甲弹",
            new Vector2(0.5f, 0.22f), new Vector2(190, 44), new Color(0.75f, 0.35f, 0.0f));
        unitPanel.SetActive(false);

        // 建筑面板（右下）
        GameObject bldPanel = CreatePanel(hudCanvas.transform, "BuildingPanel",
            new Vector2(1, 0), new Vector2(414, 300));
        {
            var brt = bldPanel.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1, 0); brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(1, 0);
            brt.anchoredPosition = new Vector2(-12, 34);
            bldPanel.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.16f, 0.95f);
        }
        // 彩色顶部标题栏（蓝紫色）
        CreateImage(bldPanel.transform, "BldPanelHeader",
            new Color(0.06f, 0.11f, 0.19f, 1f), new Vector2(0, 0.86f), Vector2.one);
        {
            var bn = CreateText(bldPanel.transform, "BuildingNameText", "建筑",
                new Vector2(0.5f, 0.94f), new Vector2(378, 34), 23);
            bn.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }
        CreateText(bldPanel.transform, "BldHPLabel", "耐久",
            new Vector2(0.05f, 0.84f), new Vector2(56, 18), 13, new Color(0.7f, 0.9f, 0.7f));
        CreateText(bldPanel.transform, "BuildingHPText", "---/---",
            new Vector2(0.86f, 0.84f), new Vector2(92, 18), 13, new Color(0.82f, 1f, 0.78f));
        CreateSlider(bldPanel.transform, "BuildingHPBar",
            new Vector2(0.48f, 0.84f), new Vector2(260, 10), new Color(0.15f, 0.85f, 0.35f));
        // 生产进度
        CreateText(bldPanel.transform, "ProdLabel", "进度",
            new Vector2(0.05f, 0.75f), new Vector2(56, 18), 13, new Color(0.5f, 0.85f, 1f));
        CreateSlider(bldPanel.transform, "ProductionBar",
            new Vector2(0.56f, 0.75f), new Vector2(334, 10), new Color(0.2f, 0.75f, 1f));
        var prodStateText = CreateText(bldPanel.transform, "ProductionText", "空闲",
            new Vector2(0.5f, 0.63f), new Vector2(360, 26), 13, new Color(0.75f, 0.9f, 1f));
        {
            var pst = prodStateText.GetComponent<Text>();
            pst.fontStyle = FontStyle.Bold;
            pst.lineSpacing = 0.9f;
            pst.horizontalOverflow = HorizontalWrapMode.Wrap;
            pst.verticalOverflow = VerticalWrapMode.Truncate;
        }
        // 生产按钮 × 4（每个按钮独立配色 + 顶部亮边）
        Color[] prodBtnColors = {
            new Color(0.12f,0.42f,0.18f),  // 绿：步兵/地面1
            new Color(0.10f,0.35f,0.62f),  // 蓝：坦克/地面2
            new Color(0.12f,0.38f,0.55f),  // 青：空军1
            new Color(0.42f,0.24f,0.08f),  // 橙：特殊
        };
        for (int i = 0; i < 4; i++)
        {
            int col = i % 3;
            int row = i / 3;
            var pb = CreateButton(bldPanel.transform, $"ProductionButton{i}", $"单位{i + 1}",
                new Vector2(0.19f + col * 0.31f, 0.30f - row * 0.22f), new Vector2(120, 58), prodBtnColors[i]);
            pb.GetComponentInChildren<Text>().fontSize = 13;
            // 顶部颜色亮边
            var topAccent = new GameObject("ProdBtnAccent");
            topAccent.transform.SetParent(pb.transform, false);
            var tar = topAccent.AddComponent<RectTransform>();
            tar.anchorMin = new Vector2(0, 1); tar.anchorMax = Vector2.one;
            tar.offsetMin = new Vector2(0, -3); tar.offsetMax = Vector2.zero;
            topAccent.AddComponent<Image>().color = Color.Lerp(prodBtnColors[i], Color.white, 0.55f);
        }
        bldPanel.SetActive(false);

        // 游戏结束面板 — 全屏暗化遮罩 + 居中结算卡片
        GameObject gameoverPanel = new GameObject("GameOverPanel");
        gameoverPanel.transform.SetParent(hudCanvas.transform, false);
        {
            var gort = gameoverPanel.AddComponent<RectTransform>();
            gort.anchorMin = Vector2.zero; gort.anchorMax = Vector2.one;
            gort.offsetMin = gort.offsetMax = Vector2.zero;
            // 全屏半透明遮罩
            gameoverPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        }
        // 中央卡片
        GameObject goCard = new GameObject("GoCard");
        goCard.transform.SetParent(gameoverPanel.transform, false);
        {
            var crt = goCard.AddComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(720, 560);
            goCard.AddComponent<Image>().color = new Color(0.045f, 0.058f, 0.060f, 0.98f);
            var goCardOutline = goCard.AddComponent<Outline>();
            goCardOutline.effectColor = new Color(0.94f, 0.72f, 0.22f, 0.70f);
            goCardOutline.effectDistance = new Vector2(2, -2);
            var shadow = goCard.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(5, -5);
        }
        // 卡片顶部强调条（金色）
        {
            var topStrip = new GameObject("GoTopStrip");
            topStrip.transform.SetParent(goCard.transform, false);
            var trt2 = topStrip.AddComponent<RectTransform>();
            trt2.anchorMin = new Vector2(0, 1f); trt2.anchorMax = Vector2.one;
            trt2.offsetMin = new Vector2(0, -4); trt2.offsetMax = Vector2.zero;
            topStrip.AddComponent<Image>().color = new Color(0.92f, 0.70f, 0.18f, 0.96f);
        }
        {
            var bottomStrip = new GameObject("GoBottomStrip");
            bottomStrip.transform.SetParent(goCard.transform, false);
            var brt2 = bottomStrip.AddComponent<RectTransform>();
            brt2.anchorMin = Vector2.zero; brt2.anchorMax = new Vector2(1f, 0f);
            brt2.offsetMin = Vector2.zero; brt2.offsetMax = new Vector2(0, 4);
            bottomStrip.AddComponent<Image>().color = new Color(0.92f, 0.70f, 0.18f, 0.96f);
        }
        {
            var gt = CreateText(goCard.transform, "GameOverText", "胜利",
                new Vector2(0.5f, 0.855f), new Vector2(620, 76), 54);
            gt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var go2 = gt.AddComponent<Outline>();
            go2.effectColor = new Color(0f, 0f, 0f, 0.8f);
            go2.effectDistance = new Vector2(2, -2);
            var sh = gt.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.65f);
            sh.effectDistance = new Vector2(3, -3);
        }
        CreateGameOverResultBadge(goCard.transform, true);
        CreateGameOverSummaryCard(goCard.transform, 0, "金币", "$ 0", new Color(0.98f, 0.78f, 0.22f));
        CreateGameOverSummaryCard(goCard.transform, 1, "电力", "0/0", new Color(0.42f, 0.94f, 0.76f));
        CreateGameOverSummaryCard(goCard.transform, 2, "主基地", "Lv.1", new Color(0.54f, 0.76f, 1f));
        CreateGameOverSummaryCard(goCard.transform, 3, "战果", "0 杀  00:00", new Color(1f, 0.50f, 0.42f));
        CreateGameOverReportPanel(goCard.transform);
        {
            var report = goCard.transform.Find("GoReportPanel") ?? goCard.transform;
            var gs = CreateText(report, "GameOverStatsText", "",
                new Vector2(0.5f, 0.5f), new Vector2(602, 163), 13, new Color(0.84f, 0.93f, 1f));
            var gsrt = gs.GetComponent<RectTransform>();
            gsrt.anchorMin = Vector2.zero;
            gsrt.anchorMax = Vector2.one;
            gsrt.offsetMin = new Vector2(20, 14);
            gsrt.offsetMax = new Vector2(-18, -48);
            var gst = gs.GetComponent<Text>();
            gst.alignment = TextAnchor.UpperLeft;
            gst.lineSpacing = 1.04f;
            gst.horizontalOverflow = HorizontalWrapMode.Wrap;
            gst.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = gs.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.72f);
            sh.effectDistance = new Vector2(1, -1);
        }
        {
            var cd = CreateText(goCard.transform, "GoCountdownText", "30秒后自动返回大厅",
                new Vector2(0.5f, 0.195f), new Vector2(580, 28), 16, new Color(0.72f, 0.86f, 0.92f, 0.95f));
            var cdt = cd.GetComponent<Text>();
            cdt.fontStyle = FontStyle.Bold;
            cdt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sh = cd.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.76f);
            sh.effectDistance = new Vector2(1.4f, -1.4f);
        }
        CreateGameOverCountdownBar(goCard.transform);
        CreateGameOverButtonDeck(goCard.transform);
        var restartButton = CreateButton(goCard.transform, "RestartButton", "再来一局",
            new Vector2(0.5f, 0.105f), new Vector2(180, 50), new Color(0.12f, 0.48f, 0.22f, 0.98f));
        restartButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110, 0);
        StyleGameOverButton(restartButton, new Color(0.48f, 1f, 0.58f, 0.92f));
        var lobbyButton = CreateButton(goCard.transform, "LobbyButton", "返回大厅",
            new Vector2(0.5f, 0.105f), new Vector2(180, 50), new Color(0.16f, 0.33f, 0.66f, 0.98f));
        lobbyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, 0);
        StyleGameOverButton(lobbyButton, new Color(0.58f, 0.78f, 1f, 0.92f));
        gameoverPanel.SetActive(false);

        // ESC 战局菜单 — 全屏暗化遮罩 + 居中卡片
        GameObject pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(hudCanvas.transform, false);
        {
            var prt = pausePanel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            pausePanel.AddComponent<Image>().color = new Color(0f, 0f, 0.05f, 0.65f);
        }
        GameObject pauseCard = new GameObject("PauseCard");
        pauseCard.transform.SetParent(pausePanel.transform, false);
        {
            var pcrt = pauseCard.AddComponent<RectTransform>();
            pcrt.anchorMin = pcrt.anchorMax = pcrt.pivot = new Vector2(0.5f, 0.5f);
            pcrt.sizeDelta = new Vector2(340, 295);
            pauseCard.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.15f, 0.98f);
        }
        // 顶部强调条（蓝色）
        {
            var ps = new GameObject("PauseStrip");
            ps.transform.SetParent(pauseCard.transform, false);
            var psrt = ps.AddComponent<RectTransform>();
            psrt.anchorMin = new Vector2(0, 1f); psrt.anchorMax = Vector2.one;
            psrt.offsetMin = new Vector2(0, -4); psrt.offsetMax = Vector2.zero;
            ps.AddComponent<Image>().color = new Color(0.25f, 0.55f, 1f, 1f);
        }
        {
            var pt = CreateText(pauseCard.transform, "PauseTitle", "战局菜单",
                new Vector2(0.5f, 0.83f), new Vector2(290, 50), 30);
            pt.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }
        CreateButton(pauseCard.transform, "ResumeButton", "返回战场",
            new Vector2(0.5f, 0.61f), new Vector2(240, 54), new Color(0.15f, 0.52f, 0.2f));
        CreateButton(pauseCard.transform, "SurrenderButton", "投  降",
            new Vector2(0.5f, 0.32f), new Vector2(240, 54), new Color(0.62f, 0.12f, 0.12f));
        pausePanel.SetActive(false);

        // 游戏内设置入口：音量与返回大厅
        GameObject settingsButton = CreateButton(hudCanvas.transform, "GameSettingsButton", "",
            new Vector2(0.86f, 1f), new Vector2(38, 38), new Color(0f, 0f, 0f, 0f));
        {
            var srt = settingsButton.GetComponent<RectTransform>();
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, -34f);
            AddSettingsGearIcon(settingsButton.transform, new Vector2(34f, 34f));
        }

        GameObject settingsPanel = new GameObject("GameSettingsPanel");
        settingsPanel.transform.SetParent(hudCanvas.transform, false);
        {
            var sprt = settingsPanel.AddComponent<RectTransform>();
            sprt.anchorMin = Vector2.zero; sprt.anchorMax = Vector2.one;
            sprt.offsetMin = sprt.offsetMax = Vector2.zero;
            settingsPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);
        }
        GameObject settingsCard = new GameObject("SettingsCard");
        settingsCard.transform.SetParent(settingsPanel.transform, false);
        {
            var scrt = settingsCard.AddComponent<RectTransform>();
            scrt.anchorMin = scrt.anchorMax = scrt.pivot = new Vector2(0.5f, 0.5f);
            scrt.sizeDelta = new Vector2(480, 360);
            settingsCard.AddComponent<Image>().color = new Color(0.045f, 0.060f, 0.075f, 0.98f);
        }
        {
            var topLine = new GameObject("SettingsTopLine");
            topLine.transform.SetParent(settingsCard.transform, false);
            var tlrt = topLine.AddComponent<RectTransform>();
            tlrt.anchorMin = new Vector2(0f, 1f); tlrt.anchorMax = Vector2.one;
            tlrt.offsetMin = new Vector2(0f, -3f); tlrt.offsetMax = Vector2.zero;
            topLine.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f, 0.95f);
        }
        {
            var title = CreateText(settingsCard.transform, "SettingsTitle", "设置",
                new Vector2(0.5f, 1f), new Vector2(360, 48), 28);
            title.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -38f);
            title.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }
        {
            var closeButton = CreateButton(settingsCard.transform, "GameSettingsCloseButton", "×",
                new Vector2(1f, 1f), new Vector2(36, 32), new Color(0.10f, 0.12f, 0.15f, 0.96f));
            closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-24f, -24f);
            var closeText = closeButton.GetComponentInChildren<Text>();
            if (closeText != null) closeText.fontSize = 24;
        }
        var masterLabel = CreateText(settingsCard.transform, "MasterVolumeLabel", "总音量",
            new Vector2(0f, 1f), new Vector2(110, 30), 18, new Color(0.90f, 0.96f, 1f));
        masterLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(96f, -118f);
        masterLabel.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
        var masterValue = CreateText(settingsCard.transform, "MasterVolumeValueText", "80%",
            new Vector2(1f, 1f), new Vector2(70, 30), 17, new Color(1f, 0.90f, 0.55f));
        masterValue.GetComponent<RectTransform>().anchoredPosition = new Vector2(-68f, -118f);
        masterValue.GetComponent<Text>().alignment = TextAnchor.MiddleRight;
        var masterSlider = CreateSlider(settingsCard.transform, "MasterVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(330, 24), new Color(0.42f, 0.72f, 0.95f));
        masterSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(20f, -155f);
        masterSlider.GetComponent<Slider>().value = 0.8f;

        var sfxLabel = CreateText(settingsCard.transform, "SfxVolumeLabel", "音效音量",
            new Vector2(0f, 1f), new Vector2(130, 30), 18, new Color(0.90f, 0.96f, 1f));
        sfxLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(96f, -204f);
        sfxLabel.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
        var sfxValue = CreateText(settingsCard.transform, "SfxVolumeValueText", "100%",
            new Vector2(1f, 1f), new Vector2(70, 30), 17, new Color(1f, 0.90f, 0.55f));
        sfxValue.GetComponent<RectTransform>().anchoredPosition = new Vector2(-68f, -204f);
        sfxValue.GetComponent<Text>().alignment = TextAnchor.MiddleRight;
        var sfxSlider = CreateSlider(settingsCard.transform, "SfxVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(330, 24), new Color(0.70f, 0.88f, 0.42f));
        sfxSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(20f, -241f);

        var returnLobby = CreateButton(settingsCard.transform, "GameSettingsReturnLobbyButton", "返回大厅",
            new Vector2(0.5f, 0f), new Vector2(180, 44), new Color(0.54f, 0.16f, 0.16f));
        returnLobby.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 24f);
        settingsPanel.SetActive(false);

        // ── 小地图 HUD 面板（左下角）──────────────────────────
        // 外框背景
        GameObject mmPanel = new GameObject("MinimapPanel");
        mmPanel.transform.SetParent(hudCanvas.transform, false);
        RectTransform mmPanelRT = mmPanel.AddComponent<RectTransform>();
        mmPanelRT.anchorMin = mmPanelRT.anchorMax = mmPanelRT.pivot = new Vector2(0, 0);
        mmPanelRT.anchoredPosition = new Vector2(10, 68);
        mmPanelRT.sizeDelta = new Vector2(264, 199);
        Image mmBg = mmPanel.AddComponent<Image>();
        mmBg.color = new Color(0.04f, 0.06f, 0.14f, 0.97f);
        // 金色顶边线
        { var tl=new GameObject("MMTopLine"); tl.transform.SetParent(mmPanel.transform,false);
          var r=tl.AddComponent<RectTransform>(); r.anchorMin=new Vector2(0,1); r.anchorMax=Vector2.one;
          r.offsetMin=new Vector2(0,-2); r.offsetMax=Vector2.zero;
          tl.AddComponent<Image>().color=new Color(0.10f,0.16f,0.22f,0.95f); }
        // 金色底边线
        { var bl=new GameObject("MMBotLine"); bl.transform.SetParent(mmPanel.transform,false);
          var r=bl.AddComponent<RectTransform>(); r.anchorMin=Vector2.zero; r.anchorMax=new Vector2(1,0);
          r.offsetMin=Vector2.zero; r.offsetMax=new Vector2(0,2);
          bl.AddComponent<Image>().color=new Color(0.10f,0.16f,0.22f,0.75f); }
        // 左边线
        { var ll=new GameObject("MMLtLine"); ll.transform.SetParent(mmPanel.transform,false);
          var r=ll.AddComponent<RectTransform>(); r.anchorMin=Vector2.zero; r.anchorMax=new Vector2(0,1);
          r.offsetMin=Vector2.zero; r.offsetMax=new Vector2(2,0);
          ll.AddComponent<Image>().color=new Color(0.10f,0.16f,0.22f,0.75f); }
        // 右边线
        { var rl=new GameObject("MMRtLine"); rl.transform.SetParent(mmPanel.transform,false);
          var r=rl.AddComponent<RectTransform>(); r.anchorMin=new Vector2(1,0); r.anchorMax=Vector2.one;
          r.offsetMin=new Vector2(-2,0); r.offsetMax=Vector2.zero;
          rl.AddComponent<Image>().color=new Color(0.85f,0.62f,0.08f,0.75f); }
        // 小地图 RawImage
        GameObject mmImg = new GameObject("MinimapImage");
        mmImg.transform.SetParent(mmPanel.transform, false);
        RectTransform mmRT = mmImg.AddComponent<RectTransform>();
        mmRT.anchorMin = mmRT.anchorMax = mmRT.pivot = new Vector2(0.5f, 0.5f);
        mmRT.anchoredPosition = Vector2.zero;
        mmRT.sizeDelta = new Vector2(260, 195);
        RawImage rawImg = mmImg.AddComponent<RawImage>();
        RenderTexture minimapTexture = EnsureMinimapRenderTexture();
        rawImg.texture = minimapTexture;

        // 相机视野框（白色）
        GameObject camRect = new GameObject("MinimapCamRect");
        camRect.transform.SetParent(mmImg.transform, false);
        RectTransform camRectRT = camRect.AddComponent<RectTransform>();
        camRectRT.anchorMin = camRectRT.anchorMax = camRectRT.pivot = new Vector2(0.5f, 0.5f);
        camRectRT.anchoredPosition = Vector2.zero;
        camRectRT.sizeDelta = new Vector2(60, 60);
        Image camRectImg = camRect.AddComponent<Image>();
        camRectImg.color = new Color(1f, 1f, 1f, 0.08f);  // 极淡白色填充（Outline需要非零alpha才可见）
        var outline = camRect.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2, 2);

        // 小地图摄像机
        GameObject mmCamGO = new GameObject("MinimapCamera");
        Camera mmCam = mmCamGO.AddComponent<Camera>();
        mmCam.orthographic = true;
        mmCam.orthographicSize = 220f;  // 覆盖 400x400 地图
        mmCam.transform.position = new Vector3(0, 400f, 0);
        mmCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mmCam.clearFlags = CameraClearFlags.SolidColor;
        mmCam.backgroundColor = Color.Lerp(map.GroundColor, Color.black, 0.35f);
        mmCam.depth = -2;
        mmCam.farClipPlane = 450f;
        mmCam.nearClipPlane = 1f;
        mmCam.targetTexture = minimapTexture;

        // MinimapManager
        GameObject mmMgr = new GameObject("MinimapManager");
        MinimapManager mmManager = mmMgr.AddComponent<MinimapManager>();
        mmManager.MinimapCamera = mmCam;
        mmManager.MinimapImage  = rawImg;
        mmManager.CameraRect    = camRectRT;
        mmManager.MapHalfX = 200f;
        mmManager.MapHalfZ = 200f;

        // ── 移动端建造面板 ─────────────────────────────────────
        // 建造/科技入口：固定在小地图上方，避免遮挡战场底部指挥栏。
        GameObject buildToggleGO = CreateButton(hudCanvas.transform, "BuildMenuToggle", "建造",
            new Vector2(0f, 0f), new Vector2(136, 42), new Color(0.12f, 0.42f, 0.18f));
        // 左下角主入口按钮，后续运行时会继续沿用这组初始尺寸与锚点。
        {
            var btr = buildToggleGO.GetComponent<RectTransform>();
            btr.anchorMin = new Vector2(0, 0); btr.anchorMax = new Vector2(0, 0);
            btr.pivot = new Vector2(0, 0);
            btr.anchoredPosition = new Vector2(10, 325);
            // 顶部强调线
            var tline = new GameObject("ToggleAccent");
            tline.transform.SetParent(buildToggleGO.transform, false);
            var tlrt = tline.AddComponent<RectTransform>();
            tlrt.anchorMin = new Vector2(0, 1); tlrt.anchorMax = Vector2.one;
            tlrt.offsetMin = new Vector2(0, -3); tlrt.offsetMax = Vector2.zero;
            tline.AddComponent<Image>().color = new Color(0.3f, 1f, 0.4f, 0.9f);
            buildToggleGO.GetComponentInChildren<Text>().fontSize = 15;
        }
        GameObject techButtonGO = CreateButton(hudCanvas.transform, "_TechButton", "科技",
            new Vector2(0f, 0f), new Vector2(136, 42), new Color(0.16f, 0.30f, 0.54f));
        // 科技入口与建造入口上下对齐，方便 RTSHUD 在运行时继续校正布局。
        {
            var tr = techButtonGO.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(0, 0);
            tr.pivot = new Vector2(0, 0);
            tr.anchoredPosition = new Vector2(10, 273);
            var txt = techButtonGO.GetComponentInChildren<Text>();
            if (txt) txt.fontSize = 15;
        }

        // 建造面板（中央弹窗，运行时会进一步整理内部布局）
        string[] bldNames = { "兵工厂", "飞机厂", "停机场", "特需厂", "坦克厂", "炮塔", "金矿", "电厂", "船坞" };
        Color[] bldColors = {
            new Color(0.18f,0.42f,0.18f), new Color(0.10f,0.38f,0.58f), new Color(0.18f,0.34f,0.58f),
            new Color(0.42f,0.28f,0.08f), new Color(0.28f,0.34f,0.12f), new Color(0.48f,0.12f,0.12f),
            new Color(0.50f,0.44f,0.04f), new Color(0.38f,0.20f,0.52f), new Color(0.05f,0.44f,0.56f)
        };
        GameObject buildMenuPanel = new GameObject("BuildMenuPanel");
        buildMenuPanel.transform.SetParent(hudCanvas.transform, false);
        RectTransform bmpRT = buildMenuPanel.AddComponent<RectTransform>();
        bmpRT.anchorMin = bmpRT.anchorMax = bmpRT.pivot = new Vector2(0.5f, 0.5f);
        bmpRT.anchoredPosition = Vector2.zero;
        bmpRT.sizeDelta = new Vector2(676, 426);
        buildMenuPanel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.13f, 0.96f);
        // 面板顶部绿色边线
        {
            var bml = new GameObject("BmpTopLine");
            bml.transform.SetParent(buildMenuPanel.transform, false);
            var blrt = bml.AddComponent<RectTransform>();
            blrt.anchorMin = new Vector2(0, 1); blrt.anchorMax = Vector2.one;
            blrt.offsetMin = new Vector2(0, -2); blrt.offsetMax = Vector2.zero;
            bml.AddComponent<Image>().color = new Color(0.12f, 0.22f, 0.16f, 0.85f);
        }

        for (int i = 0; i < bldNames.Length; i++)
        {
            GameObject btnGO = CreateButton(buildMenuPanel.transform, $"BuildButton{i}", bldNames[i],
                new Vector2(0.5f, 0.5f), new Vector2(144, 84), bldColors[i]);
            var br = btnGO.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.5f, 0.5f); br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(-231 + (i % 4) * 154, 90 - (i / 4) * 96);
            var txt = btnGO.GetComponentInChildren<Text>();
            if (txt) { txt.fontSize = 16; txt.lineSpacing = 0.9f; txt.horizontalOverflow = HorizontalWrapMode.Wrap; }
            // 底部颜色亮边（每种建筑独有色）
            var btmLine = new GameObject("BtnLine");
            btmLine.transform.SetParent(btnGO.transform, false);
            var blrt2 = btmLine.AddComponent<RectTransform>();
            blrt2.anchorMin = Vector2.zero; blrt2.anchorMax = new Vector2(1, 0);
            blrt2.offsetMin = Vector2.zero; blrt2.offsetMax = new Vector2(0, 3);
            btmLine.AddComponent<Image>().color = Color.Lerp(bldColors[i], Color.white, 0.45f);
        }
        buildMenuPanel.SetActive(false);

        // HUD脚本并自动绑定引用
        GameObject hudMgr = new GameObject("RTSHUD");
        RTSHUD hud = hudMgr.AddComponent<RTSHUD>();
        hud.MinimapImage     = rawImg;
        hud.MinimapCameraRect = camRectRT;
        BindRTSHUD(hud, hudCanvas);

        BuildNavMeshIfNeeded();

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        Debug.Log("✅ 游戏场景生成完毕: " + scenePath);
        if (runAutomatedTest)
            AutomatedProjectTest.RunAfterSceneGeneration();
    }

    // ── 环境辅助函数 ──────────────────────────────────────────
    static void BuildNavMeshIfNeeded()
    {
        const string navMeshPath = "Assets/Scenes/GameScene/NavMesh.asset";
        if (File.Exists(navMeshPath))
        {
            Debug.Log("[SceneBuilder] Reusing existing NavMesh asset: " + navMeshPath);
            return;
        }

        // The legacy synchronous bake can stall the editor; only run it for a missing first-time asset.
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        Debug.Log("✅ NavMesh 烘焙完毕");
    }

    static void MarkNavigationStatic(GameObject go)
    {
        if (go == null) return;

#pragma warning disable 0618
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);
#pragma warning restore 0618
    }

    static Material MakeMat(Color color, float metallic = 0f, float glossiness = 0.2f)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", glossiness);
        return mat;
    }

    static RenderTexture EnsureMinimapRenderTexture()
    {
        const string path = "Assets/Textures/MinimapRT.renderTexture";
        Directory.CreateDirectory("Assets/Textures");

        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (rt == null)
        {
            rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT";
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            AssetDatabase.CreateAsset(rt, path);
            AssetDatabase.SaveAssets();
        }

        rt.width = 256;
        rt.height = 256;
        rt.depth = 16;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        EditorUtility.SetDirty(rt);
        return rt;
    }

    static void SpawnMapDetails(BattleMapDefinition map)
    {
        foreach (var patch in map.Patches)
        {
            SpawnFlatFeature("TerrainPatch" + patch.PaletteIndex + "_" + patch.Name,
                patch.Center, patch.Size, patch.Angle, map.GetPatchColor(patch.PaletteIndex), 0.018f, 0.03f, 0.12f);
        }

        foreach (var road in map.Roads)
        {
            SpawnFlatFeature("RoadEdge_" + road.Name,
                road.Center, road.Size + new Vector2(5f, 6f), road.Angle, map.RoadEdgeColor, 0.023f, 0.025f, 0.12f);
            SpawnFlatFeature("Road_" + road.Name,
                road.Center, road.Size, road.Angle, map.RoadColor, 0.032f, 0.025f, 0.08f);
        }

        foreach (var water in map.Waters)
        {
            SpawnFlatFeature("Water_" + water.Name,
                water.Center, water.Size, water.Angle, map.WaterColor, 0.045f, 0.03f, 0.75f);
        }
    }

    static void SpawnSpawnPointPreviewMarkers(BattleMapDefinition map)
    {
        if (map == null || map.SpawnPoints == null)
            return;

        for (int i = 0; i < map.SpawnPoints.Length; i++)
            SpawnSpawnPointPreviewMarker(map.SpawnPoints[i], i);
    }

    static void SpawnSpawnPointPreviewMarker(SpawnPointSpec spawn, int index)
    {
        Vector3 position = spawn.Position;
        position.y = 0f;

        Color markerColor = spawn.IsPlayer
            ? new Color(0.22f, 0.82f, 1f, 1f)
            : new Color(1f, 0.36f, 0.28f, 1f);

        int teamIndex = spawn.IsPlayer ? 0 : Mathf.Max(1, spawn.TeamIndex);
        string fallbackName = spawn.IsPlayer ? "PlayerSpawn" : "EnemySpawn" + teamIndex;
        string label = spawn.IsPlayer
            ? "P0 " + (string.IsNullOrWhiteSpace(spawn.Name) ? fallbackName : spawn.Name)
            : "E" + teamIndex + " " + (string.IsNullOrWhiteSpace(spawn.Name) ? fallbackName : spawn.Name);

        GameObject root = new GameObject("SpawnMarker_" + index + "_" + fallbackName);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, spawn.Yaw, 0f);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SpawnMarkerRing";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localScale = new Vector3(3.6f, 0.04f, 3.6f);
        ring.GetComponent<Renderer>().material = MakeMat(markerColor, 0f, 0.45f);
        Object.DestroyImmediate(ring.GetComponent<Collider>());

        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "SpawnMarkerStem";
        stem.transform.SetParent(root.transform, false);
        stem.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        stem.transform.localScale = new Vector3(0.22f, 1.35f, 0.22f);
        stem.GetComponent<Renderer>().material = MakeMat(Color.Lerp(markerColor, Color.white, 0.18f), 0f, 0.28f);
        Object.DestroyImmediate(stem.GetComponent<Collider>());

        GameObject pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pointer.name = "SpawnMarkerPointer";
        pointer.transform.SetParent(root.transform, false);
        pointer.transform.localPosition = new Vector3(0f, 0.24f, 3.2f);
        pointer.transform.localScale = new Vector3(0.9f, 0.18f, 2.6f);
        pointer.GetComponent<Renderer>().material = MakeMat(markerColor, 0f, 0.40f);
        Object.DestroyImmediate(pointer.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "SpawnMarkerHead";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.24f, 4.9f);
        head.transform.localScale = new Vector3(1.6f, 0.24f, 1.1f);
        head.GetComponent<Renderer>().material = MakeMat(Color.Lerp(markerColor, Color.white, 0.12f), 0f, 0.46f);
        Object.DestroyImmediate(head.GetComponent<Collider>());

        GameObject labelGO = new GameObject("SpawnMarkerLabel");
        labelGO.transform.SetParent(root.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 3.9f, 0f);
        labelGO.transform.localScale = Vector3.one * 0.06f;
        TextMesh tm = labelGO.AddComponent<TextMesh>();
        tm.text = label;
        tm.fontSize = 48;
        tm.characterSize = 1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = markerColor;
        labelGO.AddComponent<BillboardLabel>();
        MeshRenderer labelRenderer = labelGO.GetComponent<MeshRenderer>();
        if (labelRenderer != null && labelRenderer.material != null)
            labelRenderer.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    static GameObject SpawnFlatFeature(string name, Vector3 center, Vector2 size,
        float yaw, Color color, float y, float height, float glossiness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = new Vector3(center.x, y, center.z);
        go.transform.localScale = new Vector3(size.x, height, size.y);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.GetComponent<Renderer>().material = MakeMat(color, 0f, glossiness);

        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return go;
    }

    // 用 CreatePrimitive 创建建筑可视体（避免 GetBuiltinResource<Mesh> 在 batchmode 返回 null）
    static GameObject SpawnBuildingCube(string objName, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = MakeMat(color);
        return go;
    }

    static void SpawnWall(string name, Vector3 pos, Vector3 scale, Color wallColor)
    {
        // 主墙体（混凝土灰色）
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.transform.position = pos;
        w.transform.localScale = scale;
        w.GetComponent<Renderer>().material = MakeMat(wallColor, 0.12f, 0.22f);
        MarkNavigationStatic(w);

        // 顶部铁丝/金属压条
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = name + "Cap";
        cap.transform.position = pos + new Vector3(0, scale.y * 0.5f + 0.3f, 0);
        cap.transform.localScale = new Vector3(scale.x + 0.2f, 0.5f, scale.z + 0.2f);
        cap.GetComponent<Renderer>().material = MakeMat(Color.Lerp(wallColor, Color.black, 0.22f), 0.65f, 0.55f);
        MarkNavigationStatic(cap);
    }

    static void SpawnRockCluster(Vector3 center, Color rockBaseColor)
    {
        // 主锚石（扁平大石块）
        Color baseCol = rockBaseColor + new Color(
            UnityEngine.Random.Range(-0.08f, 0.08f),
            UnityEngine.Random.Range(-0.04f, 0.04f),
            UnityEngine.Random.Range(-0.04f, 0.04f), 0f);

        int cnt = UnityEngine.Random.Range(3, 6);
        for (int i = 0; i < cnt; i++)
        {
            bool isBoulder = (i == 0);
            // 大石用Cube（NavMesh烘焙更准确），小石随机Cube或Sphere
            PrimitiveType shape = (isBoulder || UnityEngine.Random.value > 0.45f)
                ? PrimitiveType.Cube : PrimitiveType.Sphere;
            var r = GameObject.CreatePrimitive(shape);
            r.name = "Rock";

            float sz  = isBoulder ? UnityEngine.Random.Range(4.5f, 7f)
                                  : UnityEngine.Random.Range(1.5f, 3.8f);
            float ox  = isBoulder ? 0f : UnityEngine.Random.Range(-7f, 7f);
            float oz  = isBoulder ? 0f : UnityEngine.Random.Range(-7f, 7f);
            float scY = isBoulder ? UnityEngine.Random.Range(0.35f, 0.55f)
                                  : UnityEngine.Random.Range(0.5f,  1.1f);
            float scZ = sz * UnityEngine.Random.Range(0.7f, 1.3f);

            r.transform.position = center + new Vector3(ox, sz * scY * 0.5f, oz);
            r.transform.localScale = new Vector3(sz, sz * scY, scZ);
            r.transform.rotation = Quaternion.Euler(
                UnityEngine.Random.Range(-12f, 12f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-12f, 12f));

            Color c = baseCol + new Color(
                UnityEngine.Random.Range(-0.06f, 0.06f),
                UnityEngine.Random.Range(-0.04f, 0.04f),
                UnityEngine.Random.Range(-0.04f, 0.04f), 0);
            r.GetComponent<Renderer>().material = MakeMat(c, 0f, 0.18f);

            // 标记为导航静态，NavMesh烘焙时绕开岩石
            MarkNavigationStatic(r);
        }
    }

    static void SpawnTree(Vector3 pos, Color trunkColor, Color foliageColor)
    {
        float scale = UnityEngine.Random.Range(0.75f, 1.35f);
        float lean  = UnityEngine.Random.Range(-6f, 6f);
        float yaw   = UnityEngine.Random.Range(0f, 360f);
        // 树干
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Tree";
        trunk.transform.position = pos + new Vector3(0, 2.8f * scale, 0);
        trunk.transform.localScale = new Vector3(0.75f * scale, 2.8f * scale, 0.75f * scale);
        trunk.transform.rotation = Quaternion.Euler(lean, yaw, 0);
        trunk.GetComponent<Renderer>().material =
            MakeMat(trunkColor + new Color(UnityEngine.Random.Range(-0.04f, 0.04f), 0f, 0f, 0f), 0f, 0.12f);
        MarkNavigationStatic(trunk);

        // 三层叶冠（底大顶小）
        float[] foliageH = { 5.8f, 7.8f, 9.4f };
        float[] foliageR = { 4.8f, 3.6f, 2.4f };
        for (int i = 0; i < 3; i++)
        {
            var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            float r = foliageR[i] * scale;
            leaves.transform.position = pos + new Vector3(
                Mathf.Sin(lean * Mathf.Deg2Rad) * foliageH[i] * scale * 0.25f,
                foliageH[i] * scale, 0);
            leaves.transform.localScale = new Vector3(r, r * 0.75f, r);
            leaves.GetComponent<Renderer>().material =
                MakeMat(foliageColor + new Color(
                    UnityEngine.Random.Range(-0.03f, 0.03f),
                    UnityEngine.Random.Range(-0.05f, 0.05f) - i * 0.03f,
                    UnityEngine.Random.Range(-0.02f, 0.02f), 0f), 0f, 0.18f);
            MarkNavigationStatic(leaves);
        }
    }

    // 沙袋掩体环（围绕基地，isEnemy决定颜色）
    static void SpawnSandbagRing(Vector3 center, float radius, int count, bool isEnemy)
    {
        Color bagCol = isEnemy ? new Color(0.40f, 0.18f, 0.12f) : new Color(0.58f, 0.50f, 0.32f);
        Color accentC= isEnemy ? new Color(0.65f, 0.10f, 0.10f) : new Color(0.22f, 0.45f, 0.78f);

        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            float ox = Mathf.Cos(angle) * radius;
            float oz = Mathf.Sin(angle) * radius;
            Vector3 bpos = center + new Vector3(ox, 0, oz);
            float yaw = Mathf.Atan2(oz, ox) * Mathf.Rad2Deg + 90f;

            // 主沙袋墙（扁平方块）
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sandbag";
            wall.transform.position = bpos + Vector3.up * 0.65f;
            wall.transform.localScale = new Vector3(4.5f, 1.3f, 1.2f);
            wall.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var wm = new Material(Shader.Find("Standard")) { color = bagCol };
            wm.SetFloat("_Metallic", 0f); wm.SetFloat("_Glossiness", 0.08f);
            wall.GetComponent<Renderer>().material = wm;
            MarkNavigationStatic(wall);

            // 顶部阵营色小方块（表示阵营标记）
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "BagMark";
            mark.transform.position = bpos + Vector3.up * 1.45f;
            mark.transform.localScale = new Vector3(0.9f, 0.4f, 0.25f);
            mark.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var mm = new Material(Shader.Find("Standard")) { color = accentC };
            mm.SetFloat("_Metallic", 0.55f); mm.SetFloat("_Glossiness", 0.65f);
            mark.GetComponent<Renderer>().material = mm;
            MarkNavigationStatic(mark);

            // 侧面支撑桩
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.position = bpos + Vector3.up * 1.1f;
            post.transform.localScale = new Vector3(0.22f, 1.1f, 0.22f);
            post.transform.rotation = Quaternion.Euler(0, yaw, 0);
            post.GetComponent<Renderer>().material =
                new Material(Shader.Find("Standard")) { color = new Color(0.25f, 0.22f, 0.18f) };
            MarkNavigationStatic(post);
        }
    }

    // 废墟残墙：战术阻断用，NavMesh 烘焙绕行
    static void SpawnRuinWall(Vector3 center, float angle, int segments, Color wallCol)
    {
        float segLen = 6f;
        Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.right;
        Vector3 start = center - dir * (segments * segLen * 0.5f);

        for (int i = 0; i < segments; i++)
        {
            // 随机缺口（约20%概率）
            if (UnityEngine.Random.value < 0.18f) continue;

            Vector3 segPos = start + dir * (i * segLen + segLen * 0.5f);
            float h    = UnityEngine.Random.Range(2f, 5f);
            float tilt = UnityEngine.Random.Range(-14f, 14f);

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "RuinWall";
            seg.transform.position = segPos + Vector3.up * (h * 0.5f);
            seg.transform.localScale = new Vector3(segLen * 0.88f, h, 1.8f);
            seg.transform.rotation = Quaternion.Euler(
                tilt, angle, UnityEngine.Random.Range(-5f, 5f));
            seg.GetComponent<Renderer>().material =
                MakeMat(wallCol + new Color(
                    UnityEngine.Random.Range(-0.07f, 0.07f), 0, 0, 0), 0f, 0.18f);
            MarkNavigationStatic(seg);

            // 底部乱石堆（视觉增强）
            for (int j = 0; j < 2; j++)
            {
                var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris.name = "Debris";
                float ds = UnityEngine.Random.Range(0.6f, 1.8f);
                debris.transform.position = segPos + new Vector3(
                    UnityEngine.Random.Range(-segLen*0.4f, segLen*0.4f), ds*0.3f,
                    UnityEngine.Random.Range(-2f, 2f));
                debris.transform.localScale = new Vector3(ds * UnityEngine.Random.Range(0.6f, 1.8f), ds * 0.35f, ds);
                debris.transform.rotation = Quaternion.Euler(
                    UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(0, 360f), 0);
                debris.GetComponent<Renderer>().material =
                    MakeMat(Color.Lerp(wallCol, Color.black, 0.12f), 0f, 0.12f);
                MarkNavigationStatic(debris);
            }
        }
    }

    // ── UI工具函数 ────────────────────────────────────────────
    /// <summary>创建场景级 Canvas，并设置统一的移动端参考分辨率。</summary>
    static GameObject CreateCanvas(string name)
    {
        GameObject go = new GameObject(name);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 0;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    /// <summary>确保场景中存在一个可接收 UI 输入的 EventSystem。</summary>
    static void CreateEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    /// <summary>创建一个带默认底色的矩形面板。</summary>
    static GameObject CreatePanel(Transform parent, string name, Vector2 anchorPos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchorPos;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);
        return go;
    }

    /// <summary>创建铺满指定锚点区域的 Image 节点。</summary>
    static GameObject CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        return go;
    }

    /// <summary>创建基础文本节点，统一默认字体和居中排版。</summary>
    static GameObject CreateText(Transform parent, string name, string text,
        Vector2 anchor, Vector2 size, int fontSize, Color? color = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color ?? Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return go;
    }

    /// <summary>创建基础按钮，并自动补齐背景、文字和点击组件。</summary>
    static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 size, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = bgColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        // 悬停/按下颜色过渡
        var cb = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.22f);
        cb.pressedColor     = Color.Lerp(bgColor, Color.black, 0.25f);
        cb.selectedColor    = Color.Lerp(bgColor, Color.white, 0.12f);
        cb.disabledColor    = new Color(bgColor.r * 0.55f, bgColor.g * 0.55f, bgColor.b * 0.55f, 0.75f);
        cb.colorMultiplier  = 1f;
        cb.fadeDuration     = 0.1f;
        btn.colors = cb;
        // 文字
        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = txtGO.AddComponent<Text>();
        t.text = label; t.fontSize = 18;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.95f, 0.97f, 1f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontStyle = FontStyle.Bold;
        return go;
    }

    static void AddSettingsGearIcon(Transform parent, Vector2 size)
    {
        if (parent == null) return;
        var sprite = LoadSettingsGearSprite();
        if (sprite == null) return;

        var go = new GameObject("GearIcon");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    static Sprite LoadSettingsGearSprite()
    {
        const string assetPath = "Assets/Resources/icons3/gear.png";
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || ti.mipmapEnabled || !ti.alphaIsTransparency))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        return sprite;
    }

    static void CreateGameOverSummaryCard(Transform parent, int index, string label, string value, Color accentColor)
    {
        GameObject go = new GameObject($"GoSummary{index}");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.675f);
        rt.sizeDelta = new Vector2(150, 66);
        rt.anchoredPosition = new Vector2(-240 + index * 160, 0);
        go.AddComponent<Image>().color = new Color(0.030f, 0.048f, 0.050f, 0.98f);
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.42f);
        outline.effectDistance = new Vector2(1, -1);

        var accent = new GameObject("Accent");
        accent.transform.SetParent(go.transform, false);
        var art = accent.AddComponent<RectTransform>();
        art.anchorMin = new Vector2(0, 1);
        art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(0, -3);
        art.offsetMax = Vector2.zero;
        accent.AddComponent<Image>().color = accentColor;

        var labelGO = CreateText(go.transform, "Label", label,
            new Vector2(0.5f, 0.64f), new Vector2(116, 22), 14, new Color(0.64f, 0.72f, 0.76f));
        labelGO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var valueGO = CreateText(go.transform, "Value", value,
            new Vector2(0.5f, 0.30f), new Vector2(116, 24), 19, accentColor);
        var valueText = valueGO.GetComponent<Text>();
        valueText.alignment = TextAnchor.MiddleLeft;
        valueText.fontStyle = FontStyle.Bold;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.resizeTextForBestFit = true;
        valueText.resizeTextMinSize = 13;
        valueText.resizeTextMaxSize = 20;
    }

    static void CreateGameOverReportPanel(Transform parent)
    {
        GameObject report = new GameObject("GoReportPanel");
        report.transform.SetParent(parent, false);
        RectTransform rt = report.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.410f);
        rt.sizeDelta = new Vector2(640, 225);
        report.AddComponent<Image>().color = new Color(0.018f, 0.027f, 0.030f, 0.88f);
        var outline = report.AddComponent<Outline>();
        outline.effectColor = new Color(0.34f, 0.58f, 0.58f, 0.48f);
        outline.effectDistance = new Vector2(1, -1);

        var title = CreateText(report.transform, "GoReportTitle", "战斗报告",
            new Vector2(0f, 1f), new Vector2(180, 26), 15, new Color(1f, 0.86f, 0.42f));
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(22f, -10f);
        var titleText = title.GetComponent<Text>();
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.fontStyle = FontStyle.Bold;

        var rule = new GameObject("GoReportRule");
        rule.transform.SetParent(report.transform, false);
        var ruleRt = rule.AddComponent<RectTransform>();
        ruleRt.anchorMin = new Vector2(0f, 1f);
        ruleRt.anchorMax = new Vector2(1f, 1f);
        ruleRt.anchoredPosition = new Vector2(0f, -42f);
        ruleRt.sizeDelta = new Vector2(-44f, 2f);
        rule.AddComponent<Image>().color = new Color(0.94f, 0.72f, 0.22f, 0.35f);

        var accent = new GameObject("GoReportAccent");
        accent.transform.SetParent(report.transform, false);
        var accentRt = accent.AddComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 0f);
        accentRt.anchorMax = new Vector2(0f, 1f);
        accentRt.sizeDelta = new Vector2(4f, 0f);
        accent.AddComponent<Image>().color = new Color(0.42f, 0.94f, 0.76f, 0.55f);
    }

    static void CreateGameOverResultBadge(Transform parent, bool win)
    {
        GameObject badge = new GameObject("GoResultBadge");
        badge.transform.SetParent(parent, false);
        RectTransform rt = badge.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.855f);
        rt.anchoredPosition = new Vector2(-260f, 0f);
        rt.sizeDelta = new Vector2(56, 56);
        badge.AddComponent<Image>().color = win ? new Color(0.22f, 0.18f, 0.05f, 0.94f) : new Color(0.22f, 0.06f, 0.055f, 0.94f);
        Color accent = win ? new Color(0.95f, 0.76f, 0.20f, 0.96f) : new Color(0.94f, 0.30f, 0.24f, 0.96f);
        var outline = badge.AddComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var shadow = badge.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
        shadow.effectDistance = new Vector2(2, -2);

        var mark = CreateText(badge.transform, "Mark", win ? "V" : "X", new Vector2(0.5f, 0.5f), new Vector2(56, 56), 30, accent);
        var text = mark.GetComponent<Text>();
        text.fontStyle = FontStyle.Bold;
    }

    static void CreateGameOverCountdownBar(Transform parent)
    {
        GameObject bar = new GameObject("GoCountdownBar");
        bar.transform.SetParent(parent, false);
        RectTransform rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.165f);
        rt.sizeDelta = new Vector2(440, 6);
        bar.AddComponent<Image>().color = new Color(0.010f, 0.016f, 0.018f, 0.90f);

        GameObject fill = new GameObject("GoCountdownFill");
        fill.transform.SetParent(bar.transform, false);
        RectTransform frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.pivot = new Vector2(0f, 0.5f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = new Color(0.42f, 0.94f, 0.76f, 0.95f);
    }

    static void CreateGameOverButtonDeck(Transform parent)
    {
        GameObject deck = new GameObject("GoButtonDeck");
        deck.transform.SetParent(parent, false);
        RectTransform rt = deck.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.105f);
        rt.sizeDelta = new Vector2(430, 70);
        deck.AddComponent<Image>().color = new Color(0.012f, 0.018f, 0.020f, 0.48f);
        var outline = deck.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.60f, 0.60f, 0.22f);
        outline.effectDistance = new Vector2(1, -1);
    }

    static void StyleGameOverButton(GameObject button, Color accentColor)
    {
        var outline = button.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        var shadow = button.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(2.5f, -2.5f);
        var line = AddButtonTopLine(button.transform, Color.Lerp(accentColor, Color.white, 0.18f));
        line.name = "GoButtonTopLine";

        var text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontSize = 19;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.96f, 0.98f, 1f);
            var textShadow = text.gameObject.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            textShadow.effectDistance = new Vector2(1.2f, -1.2f);
        }
    }

    static GameObject CreateInputField(Transform parent, string name, string placeholder,
        Vector2 anchor, Vector2 size, bool password = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // 占位符
        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        RectTransform phrt = phGO.AddComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero; phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(5, 0); phrt.offsetMax = new Vector2(-5, 0);
        var phTxt = phGO.AddComponent<Text>();
        phTxt.text = placeholder; phTxt.fontSize = 16;
        phTxt.color = new Color(0.5f, 0.5f, 0.5f);
        phTxt.alignment = TextAnchor.MiddleLeft; phTxt.font = font;
        // 文字
        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(5, 0); trt.offsetMax = new Vector2(-5, 0);
        var txt = txtGO.AddComponent<Text>();
        txt.fontSize = 18; txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft; txt.font = font;
        var input = go.AddComponent<InputField>();
        if (password) input.contentType = InputField.ContentType.Password;
        input.textComponent = txt;
        input.placeholder = phTxt;
        return go;
    }

    static GameObject CreateSlider(Transform parent, string name,
        Vector2 anchor, Vector2 size, Color fillColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Slider slider = go.AddComponent<Slider>();
        // 背景
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        RectTransform bgrt = bg.AddComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f);
        // 填充区域
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fart = fillArea.AddComponent<RectTransform>();
        fart.anchorMin = Vector2.zero; fart.anchorMax = Vector2.one;
        fart.offsetMin = fart.offsetMax = Vector2.zero;
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
        slider.fillRect = frt;
        slider.targetGraphic = fillImg;
        slider.value = 1f;
        return go;
    }

    // 给输入框左侧添加彩色竖条装饰
    static void AddFieldAccent(Transform field, Color color)
    {
        var accent = new GameObject("FieldAccent");
        accent.transform.SetParent(field, false);
        var rt = accent.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0, 1);
        rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(3, 0);
        accent.AddComponent<Image>().color = color;
    }

    // 给按钮顶部添加高光细线
    static GameObject AddButtonTopLine(Transform btn, Color color)
    {
        var line = new GameObject("TopLine");
        line.transform.SetParent(btn, false);
        var rt = line.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 1); rt.anchorMax = new Vector2(0.98f, 1);
        rt.offsetMin = new Vector2(0, -2); rt.offsetMax = Vector2.zero;
        line.AddComponent<Image>().color = color;
        return line;
    }

    static T FindComp<T>(GameObject root, string name) where T : Component
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) { var c = t.GetComponent<T>(); if (c) return c; }
        return null;
    }

    static void BindRTSHUD(RTSHUD hud, GameObject canvas)
    {
        hud.GoldText         = FindComp<Text>(canvas, "GoldText");
        hud.PopText          = FindComp<Text>(canvas, "PopText");
        hud.KillText         = FindComp<Text>(canvas, "KillText");
        hud.PowerText        = FindComp<Text>(canvas, "PowerText");
        hud.GameTimerText    = FindComp<Text>(canvas, "GameTimerText");

        hud.UnitInfoPanel    = canvas.transform.Find("UnitInfoPanel")?.gameObject;
        hud.UnitNameText     = FindComp<Text>(canvas, "UnitNameText");
        hud.UnitHPBar        = FindComp<Slider>(canvas, "UnitHPBar");
        hud.UnitHPText       = FindComp<Text>(canvas, "UnitHPText");
        hud.SkillButton      = FindComp<Button>(canvas, "SkillButton");

        hud.BuildingPanel    = canvas.transform.Find("BuildingPanel")?.gameObject;
        hud.BuildingNameText = FindComp<Text>(canvas, "BuildingNameText");
        hud.BuildingHPBar    = FindComp<Slider>(canvas, "BuildingHPBar");
        hud.BuildingHPText   = FindComp<Text>(canvas, "BuildingHPText");
        hud.ProductionBar    = FindComp<Slider>(canvas, "ProductionBar");
        hud.ProductionText   = FindComp<Text>(canvas, "ProductionText");

        var btns = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 4; i++)
        {
            var b = FindComp<Button>(canvas, $"ProductionButton{i}");
            if (b) btns.Add(b);
        }
        hud.ProductionButtons = btns.ToArray();

        hud.GameOverPanel     = canvas.transform.Find("GameOverPanel")?.gameObject;
        hud.GameOverText      = FindComp<Text>(canvas, "GameOverText");
        hud.GameOverStatsText = FindComp<Text>(canvas, "GameOverStatsText");
        hud.RestartButton     = FindComp<Button>(canvas, "RestartButton");
        hud.LobbyButton       = FindComp<Button>(canvas, "LobbyButton");
        hud.MenuButton        = FindComp<Button>(canvas, "MenuButton");

        hud.PausePanel        = canvas.transform.Find("PausePanel")?.gameObject;
        hud.ResumeButton      = FindComp<Button>(canvas, "ResumeButton");
        hud.SurrenderButton   = FindComp<Button>(canvas, "SurrenderButton");
        hud.PauseLobbyButton  = FindComp<Button>(canvas, "PauseLobbyButton");
        hud.GameSettingsButton = FindComp<Button>(canvas, "GameSettingsButton");
        hud.GameSettingsPanel = canvas.transform.Find("GameSettingsPanel")?.gameObject;
        hud.MasterVolumeSlider = FindComp<Slider>(canvas, "MasterVolumeSlider");
        hud.MasterVolumeValueText = FindComp<Text>(canvas, "MasterVolumeValueText");
        hud.SfxVolumeSlider = FindComp<Slider>(canvas, "SfxVolumeSlider");
        hud.SfxVolumeValueText = FindComp<Text>(canvas, "SfxVolumeValueText");
        hud.GameSettingsCloseButton = FindComp<Button>(canvas, "GameSettingsCloseButton");
        hud.GameSettingsReturnLobbyButton = FindComp<Button>(canvas, "GameSettingsReturnLobbyButton");

        // 小地图（已在 BuildGameScene 直接赋值，这里查找兜底）
        if (hud.MinimapImage == null)
            hud.MinimapImage      = FindComp<RawImage>(canvas, "MinimapImage");
        if (hud.MinimapCameraRect == null)
            hud.MinimapCameraRect = FindComp<RectTransform>(canvas, "MinimapCamRect");

        // 警报文本
        hud.AlertText = FindComp<Text>(canvas, "AlertText");

        // 建造面板
        hud.BuildMenuPanel  = canvas.transform.Find("BuildMenuPanel")?.gameObject;
        hud.BuildMenuToggle = FindComp<Button>(canvas, "BuildMenuToggle");
        var buildBtns = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 9; i++)
        {
            var b = FindComp<Button>(canvas, $"BuildButton{i}");
            if (b) buildBtns.Add(b);
        }
        hud.BuildButtons = buildBtns.ToArray();

        EditorUtility.SetDirty(hud);
        Debug.Log("✅ RTSHUD 引用自动绑定完成");
    }
}
