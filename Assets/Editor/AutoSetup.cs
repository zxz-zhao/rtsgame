using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.AI;

// 自动全量初始化：编译完成后自动生成Prefab、场景、配置Android
[InitializeOnLoad]
public class AutoSetup
{
    static AutoSetup()
    {
        // 延迟执行，等编辑器完全加载
        EditorApplication.delayCall += RunOnce;
    }

    static void RunOnce()
    {
        // 只在首次或强制刷新时运行
        if (EditorPrefs.GetBool("RTS_AutoSetup_Done", false)) return;
        EditorApplication.delayCall -= RunOnce;

        Debug.Log("=== RTS 自动初始化开始 ===");
        try
        {
            Step1_ConfigureAndroid();
            Step2_BuildPrefabs();
            Step3_BuildLoginScene();
            Step4_BuildGameScene();
            EditorPrefs.SetBool("RTS_AutoSetup_Done", true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("=== RTS 自动初始化完成！双击 Assets/Scenes/GameScene.unity 然后按 NavMesh Bake 即可 Play ===");
            EditorUtility.DisplayDialog("初始化完成！",
                "✅ 全部配置已自动完成：\n\n" +
                "• Android构建已配置\n" +
                "• 所有单位/建筑 Prefab 已生成\n" +
                "• LoginScene 登录场景已生成\n" +
                "• GameScene 游戏场景已生成\n\n" +
                "接下来：\n" +
                "1. 双击 Assets/Scenes/GameScene.unity\n" +
                "2. Window→AI→Navigation→Bake（烘焙寻路）\n" +
                "3. 按 ▶ Play 运行！",
                "好的");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"自动初始化失败: {e.Message}\n{e.StackTrace}");
        }
    }

    // ── Android 配置 ──────────────────────────────────────────
    static void Step1_ConfigureAndroid()
    {
        PlayerSettings.applicationIdentifier = "com.mystudio.unityRTS";
        PlayerSettings.productName = "星火RTS";
        PlayerSettings.companyName = "MyStudio";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/LoginScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/LobbyScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true),
        };
        Debug.Log("✅ Step1: Android配置完成");
    }

    // ── Prefab 生成 ───────────────────────────────────────────
    static void Step2_BuildPrefabs()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        // 单位 Prefab
        MakeUnit<Infantry>   ("Infantry_Player",   Color.blue,                  new Vector3(0.5f,1f,0.5f),    true);
        MakeUnit<Infantry>   ("Infantry_Enemy",    Color.red,                   new Vector3(0.5f,1f,0.5f),    false);
        MakeUnit<Artillery>  ("Artillery_Player",  Color.cyan,                  new Vector3(0.6f,0.8f,0.6f),  true);
        MakeUnit<Artillery>  ("Artillery_Enemy",   new Color(1f,0.5f,0f),       new Vector3(0.6f,0.8f,0.6f),  false);
        MakeUnit<Flamethrower>("Flame_Player",     Color.yellow,                new Vector3(0.5f,1.1f,0.5f),  true);
        MakeUnit<Flamethrower>("Flame_Enemy",      new Color(1f,0.3f,0.3f),     new Vector3(0.5f,1.1f,0.5f),  false);
        MakeUnit<Tank>       ("Tank_Player",       Color.blue,                  new Vector3(1.6f,1.0f,2.4f),  true);
        MakeUnit<Tank>       ("Tank_Enemy",        Color.red,                   new Vector3(1.6f,1.0f,2.4f),  false);
        MakeUnit<Fighter>    ("Fighter_Player",    new Color(0.2f,0.4f,1f),     new Vector3(1.8f,0.4f,1.2f),  true);
        MakeUnit<Fighter>    ("Fighter_Enemy",     new Color(1f,0.2f,0.2f),     new Vector3(1.8f,0.4f,1.2f),  false);
        MakeUnit<Bomber>     ("Bomber_Player",     new Color(0f,0.6f,1f),       new Vector3(2.5f,0.5f,1.5f),  true);
        MakeUnit<Bomber>     ("Bomber_Enemy",      new Color(0.8f,0.1f,0.1f),   new Vector3(2.5f,0.5f,1.5f),  false);

        // 建筑 Prefab
        MakeBld<MainBase>    ("MainBase_Player",   Color.blue,                  new Vector3(4f,2f,4f),    true);
        MakeBld<MainBase>    ("MainBase_Enemy",    Color.red,                   new Vector3(4f,2f,4f),    false);
        MakeBld<Barracks>    ("Barracks_Player",   new Color(0.2f,0.4f,1f),     new Vector3(3f,1.5f,2.5f),true);
        MakeBld<Barracks>    ("Barracks_Enemy",    new Color(1f,0.3f,0.3f),     new Vector3(3f,1.5f,2.5f),false);
        MakeBld<AirFactory>  ("AirFactory_Player", Color.cyan,                  new Vector3(4f,1.2f,3f),  true);
        MakeBld<AirFactory>  ("AirFactory_Enemy",  new Color(0.9f,0.3f,0.3f),   new Vector3(4f,1.2f,3f),  false);
        MakeBld<TankFactory> ("TankFactory_Player",new Color(0.1f,0.5f,0.9f),   new Vector3(3.5f,1.4f,3f),true);
        MakeBld<TankFactory> ("TankFactory_Enemy", new Color(0.9f,0.2f,0.2f),   new Vector3(3.5f,1.4f,3f),false);
        MakeBld<PowerPlant>  ("PowerPlant_Player", Color.yellow,                new Vector3(2.5f,2f,2.5f),true);
        MakeBld<PowerPlant>  ("PowerPlant_Enemy",  new Color(0.9f,0.7f,0f),     new Vector3(2.5f,2f,2.5f),false);
        MakeBld<GoldMine>    ("GoldMine_Player",   new Color(1f,0.85f,0f),      new Vector3(2f,1.2f,2f),  true);
        MakeBld<GoldMine>    ("GoldMine_Enemy",    new Color(1f,0.85f,0f),      new Vector3(2f,1.2f,2f),  false);
        MakeBld<Turret>      ("Turret_Player",     Color.green,                 new Vector3(1.5f,2.5f,1.5f),true);
        MakeBld<Turret>      ("Turret_Enemy",      new Color(0.8f,0.2f,0.2f),   new Vector3(1.5f,2.5f,1.5f),false);

        AssetDatabase.SaveAssets();
        Debug.Log("✅ Step2: 所有Prefab生成完毕");
    }

    static void MakeUnit<T>(string name, Color color, Vector3 scale, bool player) where T : RTSUnit
    {
        string path = $"Assets/Prefabs/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return; // 已存在跳过

        GameObject root = new GameObject(name);
        // 身体
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body"; body.transform.SetParent(root.transform);
        body.transform.localScale = scale;
        SetMat(body, color);
        // 朝向标记
        GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
        front.name = "Front"; front.transform.SetParent(root.transform);
        front.transform.localPosition = new Vector3(0, 0.1f, scale.z * 0.55f);
        front.transform.localScale = Vector3.one * 0.3f;
        SetMat(front, Color.white);
        // NavMeshAgent
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.5f; agent.height = scale.y; agent.speed = 5f;
        // Collider（用于射线选择）
        CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
        cc.height = scale.y; cc.radius = 0.5f;
        // 脚本
        T unit = root.AddComponent<T>();
        unit.bPlayerOwned = player;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void MakeBld<T>(string name, Color color, Vector3 scale, bool player) where T : RTSBuilding
    {
        string path = $"Assets/Prefabs/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        GameObject root = new GameObject(name);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0, scale.y * 0.5f, 0);
        body.transform.localScale = scale;
        SetMat(body, color);
        // 顶部旗帜
        GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flag.name = "Flag"; flag.transform.SetParent(root.transform);
        flag.transform.localPosition = new Vector3(0, scale.y + 0.4f, 0);
        flag.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
        SetMat(flag, player ? new Color(0.1f,0.4f,1f) : new Color(1f,0.2f,0.2f));
        // Collider
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, scale.y * 0.5f, 0); bc.size = scale;
        // 脚本
        T bld = root.AddComponent<T>();
        bld.bPlayerOwned = player;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void SetMat(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(Shader.Find("Standard")) { color = c };
        r.sharedMaterial = mat;
    }

    // ── 登录场景 ──────────────────────────────────────────────
    static void Step3_BuildLoginScene()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        if (System.IO.File.Exists("Assets/Scenes/LoginScene.unity")) { Debug.Log("LoginScene 已存在，跳过"); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Camera
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.1f, 0.18f);
        camGO.AddComponent<AudioListener>();

        // EventSystem
        MakeEventSystem();

        // Canvas
        GameObject canvasGO = MakeCanvas("Canvas");

        // 背景：优先使用 Assets/Textures/LoginBG 图片，否则退回纯色
        var bgGO = MakeImg(canvasGO.transform, "BG", new Color(0.06f,0.1f,0.18f), Vector2.zero, Vector2.one);
        Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/LoginBG.jpg");
        if (bgTex == null) bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/LoginBG.png");
        if (bgTex != null)
        {
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.sprite = Sprite.Create(bgTex, new Rect(0,0,bgTex.width,bgTex.height), new Vector2(0.5f,0.5f));
            bgImg.color  = Color.white;
            bgImg.type   = Image.Type.Simple;
            bgImg.preserveAspect = false;
        }

        // 标题
        MakeTxt(canvasGO.transform, "Title", "星 火 RTS", new Vector2(0.5f,0.88f), new Vector2(500,70), 46, Color.white, font);

        // 登录面板
        GameObject lp = MakePanel(canvasGO.transform, "LoginPanel", new Vector2(0.5f,0.5f), new Vector2(420,440));
        MakeTxt(lp.transform,"H","账号登录",new Vector2(0.5f,0.88f),new Vector2(340,44),28,Color.white,font);
        MakeInputField(lp.transform,"LoginUsernameInput","用户名",new Vector2(0.5f,0.7f),new Vector2(340,48),false,font);
        MakeInputField(lp.transform,"LoginPasswordInput","密码",new Vector2(0.5f,0.55f),new Vector2(340,48),true,font);
        MakeBtn(lp.transform,"LoginButton","登 录",new Vector2(0.5f,0.38f),new Vector2(200,48),new Color(0.2f,0.55f,1f),font);
        MakeBtn(lp.transform,"GuestLoginButton","游客登录",new Vector2(0.27f,0.2f),new Vector2(150,40),new Color(0.35f,0.35f,0.35f),font);
        MakeBtn(lp.transform,"SwitchToRegisterButton","注册账号",new Vector2(0.73f,0.2f),new Vector2(150,40),new Color(0.25f,0.25f,0.25f),font);
        MakeTxt(lp.transform,"LoginStatusText","",new Vector2(0.5f,0.1f),new Vector2(340,28),16,Color.red,font);
        // 服务器 IP 输入框（局域网真机联机配置）
        MakeInputField(lp.transform,"ServerIpInput","服务器IP（默认自动）",new Vector2(0.5f,0.02f),new Vector2(340,36),false,font);

        // 注册面板
        GameObject rp = MakePanel(canvasGO.transform, "RegisterPanel", new Vector2(0.5f,0.5f), new Vector2(420,480));
        rp.SetActive(false);
        MakeTxt(rp.transform,"H","注册账号",new Vector2(0.5f,0.88f),new Vector2(340,44),28,Color.white,font);
        MakeInputField(rp.transform,"RegUsernameInput","用户名（≥3字符）",new Vector2(0.5f,0.72f),new Vector2(340,48),false,font);
        MakeInputField(rp.transform,"RegPasswordInput","密码（≥6字符）",new Vector2(0.5f,0.57f),new Vector2(340,48),true,font);
        MakeInputField(rp.transform,"RegConfirmInput","确认密码",new Vector2(0.5f,0.42f),new Vector2(340,48),true,font);
        MakeBtn(rp.transform,"RegisterButton","注 册",new Vector2(0.5f,0.27f),new Vector2(200,48),new Color(0.2f,0.65f,0.3f),font);
        MakeBtn(rp.transform,"SwitchToLoginButton","返回登录",new Vector2(0.5f,0.12f),new Vector2(160,40),new Color(0.3f,0.3f,0.3f),font);
        MakeTxt(rp.transform,"RegisterStatusText","",new Vector2(0.5f,0.03f),new Vector2(340,28),16,Color.red,font);

        // 游客弹窗
        GameObject gp = MakePanel(canvasGO.transform, "GuestNamePopup", new Vector2(0.5f,0.5f), new Vector2(380,280));
        gp.SetActive(false);
        MakeTxt(gp.transform,"H","输入游客昵称",new Vector2(0.5f,0.82f),new Vector2(320,44),26,Color.white,font);
        MakeInputField(gp.transform,"GuestNameInput","昵称",new Vector2(0.5f,0.58f),new Vector2(320,48),false,font);
        MakeTxt(gp.transform,"GuestStatusText","",new Vector2(0.5f,0.42f),new Vector2(320,28),15,Color.red,font);
        MakeBtn(gp.transform,"GuestConfirmButton","确认",new Vector2(0.28f,0.18f),new Vector2(120,44),new Color(0.2f,0.55f,1f),font);
        MakeBtn(gp.transform,"GuestCancelButton","取消",new Vector2(0.72f,0.18f),new Vector2(120,44),new Color(0.55f,0.2f,0.2f),font);

        // LoginManager 并绑定引用
        GameObject mgrGO = new GameObject("LoginManager");
        LoginManager mgr = mgrGO.AddComponent<LoginManager>();
        mgr.LoginPanel = lp; mgr.RegisterPanel = rp; mgr.GuestNamePopup = gp;
        mgr.LoginUsernameInput = lp.transform.Find("LoginUsernameInput")?.GetComponent<InputField>();
        mgr.LoginPasswordInput = lp.transform.Find("LoginPasswordInput")?.GetComponent<InputField>();
        mgr.LoginButton         = lp.transform.Find("LoginButton")?.GetComponent<Button>();
        mgr.SwitchToRegisterButton = lp.transform.Find("SwitchToRegisterButton")?.GetComponent<Button>();
        mgr.GuestLoginButton    = lp.transform.Find("GuestLoginButton")?.GetComponent<Button>();
        mgr.LoginStatusText     = lp.transform.Find("LoginStatusText")?.GetComponent<Text>();
        mgr.RegUsernameInput    = rp.transform.Find("RegUsernameInput")?.GetComponent<InputField>();
        mgr.RegPasswordInput    = rp.transform.Find("RegPasswordInput")?.GetComponent<InputField>();
        mgr.RegConfirmInput     = rp.transform.Find("RegConfirmInput")?.GetComponent<InputField>();
        mgr.RegisterButton      = rp.transform.Find("RegisterButton")?.GetComponent<Button>();
        mgr.SwitchToLoginButton = rp.transform.Find("SwitchToLoginButton")?.GetComponent<Button>();
        mgr.RegisterStatusText  = rp.transform.Find("RegisterStatusText")?.GetComponent<Text>();
        mgr.GuestNameInput      = gp.transform.Find("GuestNameInput")?.GetComponent<InputField>();
        mgr.GuestConfirmButton  = gp.transform.Find("GuestConfirmButton")?.GetComponent<Button>();
        mgr.GuestCancelButton   = gp.transform.Find("GuestCancelButton")?.GetComponent<Button>();
        mgr.GuestStatusText     = gp.transform.Find("GuestStatusText")?.GetComponent<Text>();
        mgr.ServerIpInput       = lp.transform.Find("ServerIpInput")?.GetComponent<InputField>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LoginScene.unity");
        Debug.Log("✅ Step3: LoginScene 生成完毕");
    }

    // ── 游戏场景 ──────────────────────────────────────────────
    static void Step4_BuildGameScene()
    {
        if (System.IO.File.Exists("Assets/Scenes/GameScene.unity")) { Debug.Log("GameScene 已存在，跳过"); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 定向光
        GameObject lightGO = new GameObject("Directional Light");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50,-30,0);

        // 地面
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(20,1,20);
        var gmat = new Material(Shader.Find("Standard"));
        gmat.color = new Color(0.28f,0.45f,0.28f);
        ground.GetComponent<Renderer>().sharedMaterial = gmat;
        // NavMeshSurface 由用户手动Bake

        // 相机
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        cam.backgroundColor = new Color(0.5f,0.7f,1f);
        camGO.transform.position = new Vector3(0,38,-22);
        camGO.transform.rotation = Quaternion.Euler(58,0,0);
        RTSCamera rtsCam = camGO.AddComponent<RTSCamera>();
        rtsCam.MinX=-90f; rtsCam.MaxX=90f; rtsCam.MinZ=-90f; rtsCam.MaxZ=90f;

        // EventSystem
        MakeEventSystem();

        // Managers
        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("RTSPlayerState").AddComponent<RTSPlayerState>();
        RTSPlayerController pc = new GameObject("RTSPlayerController").AddComponent<RTSPlayerController>();
        pc.GroundLayer = LayerMask.GetMask("Default");
        pc.UnitLayer = LayerMask.GetMask("Default");
        pc.BuildingLayer = LayerMask.GetMask("Default");
        AIController ai = new GameObject("AIController").AddComponent<AIController>();

        // HUD Canvas
        GameObject hudGO = MakeCanvas("HUDCanvas");

        // 顶栏
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(hudGO.transform, false);
        RectTransform tbrt = topBar.AddComponent<RectTransform>();
        tbrt.anchorMin = new Vector2(0,1); tbrt.anchorMax = Vector2.one;
        tbrt.pivot = new Vector2(0.5f,1f);
        tbrt.offsetMin = new Vector2(0,-58); tbrt.offsetMax = Vector2.zero;
        Image tbImg = topBar.AddComponent<Image>();
        tbImg.color = new Color(0,0,0,0.7f);
        Text goldTxt  = MakeTxt(topBar.transform,"GoldText","金币: 800",new Vector2(0.10f,0.5f),new Vector2(180,38),20,Color.yellow,font).GetComponent<Text>();
        Text popTxt   = MakeTxt(topBar.transform,"PopText","人口: 0/10",new Vector2(0.38f,0.5f),new Vector2(180,38),20,Color.white,font).GetComponent<Text>();
        Text powerTxt = MakeTxt(topBar.transform,"PowerText","电力: 0",new Vector2(0.62f,0.5f),new Vector2(160,38),20,new Color(1f,0.9f,0.2f),font).GetComponent<Text>();
        Text killTxt  = MakeTxt(topBar.transform,"KillText","击杀: 0",new Vector2(0.88f,0.5f),new Vector2(160,38),20,Color.red,font).GetComponent<Text>();
        // 警报文本（顶部居中）
        Text alertTxt = MakeTxt(hudGO.transform,"AlertText","",new Vector2(0.5f,0.91f),new Vector2(600,38),18,new Color(1f,0.4f,0.4f),font).GetComponent<Text>();

        // 右下角：单位面板
        GameObject up = MakePanel(hudGO.transform,"UnitInfoPanel",Vector2.zero,new Vector2(280,175));
        SetAnchorBottomRight(up, new Vector2(-8,8), new Vector2(280,175));
        up.SetActive(false);
        Text unTxt  = MakeTxt(up.transform,"UnitNameText","单位名",new Vector2(0.5f,0.88f),new Vector2(250,34),20,Color.white,font).GetComponent<Text>();
        Slider uhBar = MakeSlider(up.transform,"UnitHPBar",new Vector2(0.5f,0.68f),new Vector2(250,18),Color.green);
        Text uhTxt  = MakeTxt(up.transform,"UnitHPText","HP",new Vector2(0.5f,0.5f),new Vector2(200,24),15,Color.white,font).GetComponent<Text>();
        Button skillBtn = MakeBtn(up.transform,"SkillButton","穿甲弹",new Vector2(0.5f,0.22f),new Vector2(150,38),new Color(0.8f,0.4f,0f),font).GetComponent<Button>();

        // 右下角：建筑面板
        GameObject bp = MakePanel(hudGO.transform,"BuildingPanel",Vector2.zero,new Vector2(414,320));
        SetAnchorBottomRight(bp, new Vector2(-12,34), new Vector2(414,320));
        bp.SetActive(false);
        Text bnTxt  = MakeTxt(bp.transform,"BuildingNameText","建筑",new Vector2(0.5f,0.94f),new Vector2(378,34),23,Color.white,font).GetComponent<Text>();
        Slider bhBar = MakeSlider(bp.transform,"BuildingHPBar",new Vector2(0.5f,0.75f),new Vector2(372,20),Color.green);
        Text bhTxt = MakeTxt(bp.transform,"BuildingHPText","---/---",new Vector2(0.82f,0.80f),new Vector2(150,24),15,new Color(0.82f,1f,0.78f),font).GetComponent<Text>();
        Slider pBar  = MakeSlider(bp.transform,"ProductionBar",new Vector2(0.5f,0.60f),new Vector2(372,16),Color.cyan);
        Text pTxt    = MakeTxt(bp.transform,"ProductionText","空闲",new Vector2(0.5f,0.5f),new Vector2(360,34),16,Color.white,font).GetComponent<Text>();
        Button[] pBtns = new Button[4];
        for(int i=0;i<4;i++) pBtns[i]=MakeBtn(bp.transform,$"ProductionButton{i}",$"单位{i+1}",
            new Vector2(0.20f+(i%3)*0.30f,0.29f-(i/3)*0.25f),new Vector2(122,68),new Color(0.2f,0.4f,0.6f),font).GetComponent<Button>();

        // 游戏结束面板（全屏遮罩 + 结算卡）
        GameObject gop = MakeImg(hudGO.transform, "GameOverPanel", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one);
        var gopRt = gop.GetComponent<RectTransform>();
        gopRt.offsetMin = Vector2.zero;
        gopRt.offsetMax = Vector2.zero;

        GameObject goCard = MakePanel(gop.transform, "GoCard", new Vector2(0.5f, 0.5f), new Vector2(720f, 560f));
        var goCardRt = goCard.GetComponent<RectTransform>();
        goCardRt.anchoredPosition = Vector2.zero;
        goCard.GetComponent<Image>().color = new Color(0.045f, 0.058f, 0.060f, 0.98f);
        var goCardOutline = goCard.AddComponent<Outline>();
        goCardOutline.effectColor = new Color(0.94f, 0.72f, 0.22f, 0.70f);
        goCardOutline.effectDistance = new Vector2(2f, -2f);
        var goCardShadow = goCard.AddComponent<Shadow>();
        goCardShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        goCardShadow.effectDistance = new Vector2(5f, -5f);

        Text gotTxt = MakeTxt(goCard.transform, "GameOverText", "胜利！", new Vector2(0.5f, 0.855f), new Vector2(620f, 76f), 54, Color.white, font).GetComponent<Text>();
        gotTxt.fontStyle = FontStyle.Bold;

        GameObject badge = MakePanel(goCard.transform, "GoResultBadge", new Vector2(0.5f, 0.855f), new Vector2(56f, 56f));
        var badgeRt = badge.GetComponent<RectTransform>();
        badgeRt.anchoredPosition = new Vector2(-260f, 0f);
        badge.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.05f, 0.94f);
        var badgeOutline = badge.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0.95f, 0.76f, 0.20f, 0.96f);
        badgeOutline.effectDistance = new Vector2(1.5f, -1.5f);
        var badgeShadow = badge.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
        badgeShadow.effectDistance = new Vector2(2f, -2f);
        Text markTxt = MakeTxt(badge.transform, "Mark", "V", new Vector2(0.5f, 0.5f), new Vector2(56f, 56f), 30, new Color(0.95f, 0.76f, 0.20f, 0.96f), font).GetComponent<Text>();
        markTxt.fontStyle = FontStyle.Bold;

        GameObject report = MakePanel(goCard.transform, "GoReportPanel", new Vector2(0.5f, 0.410f), new Vector2(640f, 225f));
        report.GetComponent<Image>().color = new Color(0.018f, 0.027f, 0.030f, 0.88f);
        var reportOutline = report.AddComponent<Outline>();
        reportOutline.effectColor = new Color(0.34f, 0.58f, 0.58f, 0.48f);
        reportOutline.effectDistance = new Vector2(1f, -1f);
        Text reportTitle = MakeTxt(report.transform, "GoReportTitle", "战斗报告", new Vector2(0f, 1f), new Vector2(180f, 26f), 15, new Color(1f, 0.86f, 0.42f), font).GetComponent<Text>();
        reportTitle.alignment = TextAnchor.MiddleLeft;
        reportTitle.fontStyle = FontStyle.Bold;
        var reportTitleRt = reportTitle.rectTransform;
        reportTitleRt.pivot = new Vector2(0f, 1f);
        reportTitleRt.anchoredPosition = new Vector2(22f, -10f);
        GameObject reportRule = new GameObject("GoReportRule");
        reportRule.transform.SetParent(report.transform, false);
        var reportRuleRt = reportRule.AddComponent<RectTransform>();
        reportRuleRt.anchorMin = new Vector2(0f, 1f);
        reportRuleRt.anchorMax = new Vector2(1f, 1f);
        reportRuleRt.pivot = new Vector2(0.5f, 0.5f);
        reportRuleRt.anchoredPosition = new Vector2(0f, -42f);
        reportRuleRt.sizeDelta = new Vector2(-44f, 2f);
        reportRule.AddComponent<Image>().color = new Color(0.94f, 0.72f, 0.22f, 0.35f);
        Text statsTxt = MakeTxt(report.transform, "GameOverStatsText", "", new Vector2(0.5f, 0.5f), new Vector2(602f, 163f), 13, new Color(0.84f, 0.93f, 1f), font).GetComponent<Text>();
        var statsRt = statsTxt.rectTransform;
        statsRt.anchorMin = Vector2.zero;
        statsRt.anchorMax = Vector2.one;
        statsRt.pivot = new Vector2(0.5f, 0.5f);
        statsRt.offsetMin = new Vector2(20f, 14f);
        statsRt.offsetMax = new Vector2(-18f, -48f);
        statsTxt.fontSize = 13;
        statsTxt.alignment = TextAnchor.UpperLeft;
        statsTxt.lineSpacing = 1.04f;
        statsTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        statsTxt.verticalOverflow = VerticalWrapMode.Overflow;
        statsTxt.color = new Color(0.84f, 0.93f, 1f);
        var statsShadow = statsTxt.gameObject.AddComponent<Shadow>();
        statsShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        statsShadow.effectDistance = new Vector2(1f, -1f);

        Text countdownTxt = MakeTxt(goCard.transform, "GoCountdownText", "30秒后自动返回大厅", new Vector2(0.5f, 0.195f), new Vector2(580f, 28f), 16, new Color(0.72f, 0.86f, 0.92f, 0.95f), font).GetComponent<Text>();
        countdownTxt.fontStyle = FontStyle.Bold;
        countdownTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

        Button restBtn = MakeBtn(goCard.transform, "RestartButton", "再来一局", new Vector2(0.5f, 0.105f), new Vector2(180f, 50f), new Color(0.12f, 0.48f, 0.22f, 0.98f), font).GetComponent<Button>();
        restBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110f, 0f);
        Button lobBtn = MakeBtn(goCard.transform, "LobbyButton", "返回大厅", new Vector2(0.5f, 0.105f), new Vector2(180f, 50f), new Color(0.16f, 0.33f, 0.66f, 0.98f), font).GetComponent<Button>();
        lobBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(110f, 0f);
        Button menuBtn = MakeBtn(goCard.transform, "MenuButton", "主菜单", new Vector2(0.8f, 0.105f), new Vector2(120f, 46f), new Color(0.5f, 0.2f, 0.2f, 0.98f), font).GetComponent<Button>();

        gop.SetActive(false);

        // 战局菜单
        GameObject pp = MakePanel(hudGO.transform,"PausePanel",new Vector2(0.5f,0.5f),new Vector2(280,190));
        pp.SetActive(false);
        MakeTxt(pp.transform,"PT","战局菜单",new Vector2(0.5f,0.78f),new Vector2(220,48),34,Color.white,font);
        MakeBtn(pp.transform,"ResumeButton","返回战场",new Vector2(0.5f,0.5f),new Vector2(180,48),new Color(0.2f,0.5f,0.2f),font);
        MakeBtn(pp.transform,"PauseMenuButton","返回主菜单",new Vector2(0.5f,0.2f),new Vector2(180,44),new Color(0.5f,0.2f,0.2f),font);

        // 绑定 RTSHUD 引用
        GameObject hudMgr = new GameObject("RTSHUD");
        RTSHUD hud = hudMgr.AddComponent<RTSHUD>();
        hud.GoldText=goldTxt; hud.PopText=popTxt; hud.KillText=killTxt;
        hud.PowerText=powerTxt; hud.AlertText=alertTxt;
        hud.UnitInfoPanel=up; hud.UnitNameText=unTxt; hud.UnitHPBar=uhBar;
        hud.UnitHPText=uhTxt; hud.SkillButton=skillBtn;
        hud.BuildingPanel=bp; hud.BuildingNameText=bnTxt; hud.BuildingHPBar=bhBar;
        hud.BuildingHPText=bhTxt; hud.ProductionBar=pBar; hud.ProductionText=pTxt; hud.ProductionButtons=pBtns;
        hud.GameOverPanel=gop; hud.GameOverText=gotTxt; hud.GameOverStatsText=statsTxt;
        hud.RestartButton=restBtn; hud.LobbyButton=lobBtn; hud.MenuButton=menuBtn;
        hud.PausePanel=pp;

        // 放置玩家基地和敌方基地
        SpawnBld("MainBase_Player",   new Vector3(-40,0,-40));
        SpawnBld("GoldMine_Player",   new Vector3(-32,0,-40));
        SpawnBld("PowerPlant_Player", new Vector3(-40,0,-32));
        SpawnUnit("Infantry_Player",  new Vector3(-35,0,-36));
        SpawnUnit("Infantry_Player",  new Vector3(-33,0,-36));
        SpawnUnit("Tank_Player",      new Vector3(-36,0,-39));

        SpawnBld("MainBase_Enemy",    new Vector3(40,0,40));
        SpawnBld("Barracks_Enemy",    new Vector3(33,0,40));
        SpawnBld("GoldMine_Enemy",    new Vector3(40,0,33));
        SpawnUnit("Infantry_Enemy",   new Vector3(35,0,36));
        SpawnUnit("Infantry_Enemy",   new Vector3(37,0,36));
        SpawnUnit("Tank_Enemy",       new Vector3(36,0,39));

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameScene.unity");
        Debug.Log("✅ Step4: GameScene 生成完毕");
    }

    static void SpawnBld(string name, Vector3 pos)
    {
        var pfb = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{name}.prefab");
        if (pfb == null) return;
        var go = PrefabUtility.InstantiatePrefab(pfb) as GameObject;
        if (go != null) go.transform.position = pos;
    }
    static void SpawnUnit(string name, Vector3 pos)
    {
        var pfb = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{name}.prefab");
        if (pfb == null) return;
        var go = PrefabUtility.InstantiatePrefab(pfb) as GameObject;
        if (go != null) go.transform.position = pos;
    }

    // ── 通用UI工具 ────────────────────────────────────────────
    static void MakeEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static GameObject MakeCanvas(string name)
    {
        var go = new GameObject(name);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static GameObject MakePanel(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.08f,0.08f,0.12f,0.93f);
        return go;
    }

    static void SetAnchorBottomRight(GameObject go, Vector2 anchoredPos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1,0);
        rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
    }

    static GameObject MakeImg(Transform parent, string name, Color color, Vector2 amin, Vector2 amax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeTxt(Transform parent, string name, string txt, Vector2 anchor, Vector2 size, int fs, Color color, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = txt; t.fontSize = fs; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.font = font;
        return go;
    }

    static GameObject MakeBtn(Transform parent, string name, string label, Vector2 anchor, Vector2 size, Color bg, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>(); img.color = bg;
        go.AddComponent<Button>().targetGraphic = img;
        var tgo = new GameObject("Text"); tgo.transform.SetParent(go.transform, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = tgo.AddComponent<Text>();
        t.text = label; t.fontSize = 17; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.font = font;
        return go;
    }

    static void MakeInputField(Transform parent, string name, string placeholder, Vector2 anchor, Vector2 size, bool password, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.18f,0.18f,0.24f);
        // 占位符
        var phGO = new GameObject("Placeholder"); phGO.transform.SetParent(go.transform, false);
        var phrt = phGO.AddComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero; phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(8,0); phrt.offsetMax = new Vector2(-8,0);
        var phT = phGO.AddComponent<Text>();
        phT.text = placeholder; phT.fontSize = 16;
        phT.color = new Color(0.5f,0.5f,0.5f); phT.font = font;
        phT.alignment = TextAnchor.MiddleLeft;
        // 文字
        var tGO = new GameObject("Text"); tGO.transform.SetParent(go.transform, false);
        var trt = tGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8,0); trt.offsetMax = new Vector2(-8,0);
        var tT = tGO.AddComponent<Text>();
        tT.fontSize = 18; tT.color = Color.white; tT.font = font;
        tT.alignment = TextAnchor.MiddleLeft;
        var input = go.AddComponent<InputField>();
        input.textComponent = tT; input.placeholder = phT;
        if (password) input.contentType = InputField.ContentType.Password;
    }

    static Slider MakeSlider(Transform parent, string name, Vector2 anchor, Vector2 size, Color fillColor)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        // 背景
        var bg = new GameObject("BG"); bg.transform.SetParent(go.transform, false);
        var bgrt = bg.AddComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.2f,0.2f,0.2f);
        // 填充区
        var fa = new GameObject("FillArea"); fa.transform.SetParent(go.transform, false);
        var fart = fa.AddComponent<RectTransform>();
        fart.anchorMin = Vector2.zero; fart.anchorMax = Vector2.one;
        fart.offsetMin = fart.offsetMax = Vector2.zero;
        var fill = new GameObject("Fill"); fill.transform.SetParent(fa.transform, false);
        var frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>(); fillImg.color = fillColor;
        var slider = go.AddComponent<Slider>();
        slider.fillRect = frt; slider.targetGraphic = fillImg; slider.value = 1f;
        return slider;
    }

    // 强制重新运行初始化（菜单项）
    [MenuItem("RTS/强制重新初始化（删除旧场景和Prefab）")]
    static void ForceReinit()
    {
        EditorPrefs.DeleteKey("RTS_AutoSetup_Done");
        // 删除旧场景和Prefab（可选）
        if (System.IO.File.Exists("Assets/Scenes/LoginScene.unity"))
            AssetDatabase.DeleteAsset("Assets/Scenes/LoginScene.unity");
        if (System.IO.File.Exists("Assets/Scenes/GameScene.unity"))
            AssetDatabase.DeleteAsset("Assets/Scenes/GameScene.unity");
        AssetDatabase.Refresh();
        EditorApplication.delayCall += RunOnce;
        Debug.Log("将在下一次编译后重新初始化");
    }
}
