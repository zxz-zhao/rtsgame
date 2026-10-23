using Godot;
using System.Threading.Tasks;

public partial class LoginScreen : Control
{
    const string LobbyScenePath = "res://scenes/lobby/LobbyScene.tscn";
    const string DefaultServer = "127.0.0.1:8080";

    LineEdit usernameInput = null!;
    LineEdit passwordInput = null!;
    Label statusLabel = null!;
    Button loginButton = null!;
    Button registerButton = null!;
    Button guestButton = null!;
    Panel loginPanel = null!;
    Panel loginHeader = null!;
    Panel briefingPanel = null!;
    TextureRect loginFrame = null!;
    Label titleLabel = null!;
    Label editionLabel = null!;
    Label briefingTag = null!;
    Label briefingTitle = null!;
    Label briefingBody = null!;
    Panel? briefingHintBox;
    Label? briefingHint;
    Label briefingFooter = null!;
    Panel serverBadge = null!;
    Label serverBadgeLabel = null!;
    Label footerLabel = null!;
    bool uiReady;
    bool isRegisterMode;
    string fixedServerAddress = DefaultServer;
    LineEdit idCardInput = null!;
    LineEdit phoneInput = null!;
    LineEdit smsInput = null!;
    Button sendSmsButton = null!;
    int smsCountdown = 0;
    Godot.Timer smsTimer = null!;

    public override void _Ready()
    {
        usernameInput = GetNode<LineEdit>("%UsernameInput");
        passwordInput = GetNode<LineEdit>("%PasswordInput");
        statusLabel = GetNode<Label>("%StatusLabel");
        loginButton = GetNode<Button>("%LoginButton");
        registerButton = GetNode<Button>("%RegisterButton");
        guestButton = GetNode<Button>("%GuestButton");
        loginPanel = GetNode<Panel>("LoginPanel");
        loginHeader = GetNode<Panel>("LoginPanel/LoginHeader");
        briefingPanel = GetNode<Panel>("BriefingPanel");
        loginFrame = GetNode<TextureRect>("LoginPanel/LoginFrame");
        titleLabel = GetNode<Label>("LoginPanel/LoginHeader/TitleLogin");
        editionLabel = GetNode<Label>("LoginPanel/LoginPanelEdition");
        briefingTag = GetNode<Label>("BriefingPanel/BriefingTag");
        briefingTitle = GetNode<Label>("BriefingPanel/BriefingTitle");
        briefingBody = GetNode<Label>("BriefingPanel/BriefingBody");
        briefingHintBox = GetNodeOrNull<Panel>("BriefingPanel/BriefingHintBox");
        if (briefingHintBox == null)
        {
            briefingHintBox = new Panel { Name = "BriefingHintBox" };
            briefingPanel.AddChild(briefingHintBox);
            briefingHintBox.Position = new Vector2(40f, 288f);
            briefingHintBox.Size = new Vector2(440f, 86f);
        }
        briefingHint = briefingHintBox.GetNodeOrNull<Label>("BriefingHint");
        if (briefingHint == null)
        {
            briefingHint = new Label { Name = "BriefingHint" };
            briefingHintBox.AddChild(briefingHint);
            briefingHint.Position = new Vector2(12f, 8f);
            briefingHint.Size = new Vector2(416f, 70f);
            briefingHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        briefingFooter = GetNodeOrNull<Label>("BriefingPanel/BriefingFooter")!;
        if (briefingFooter == null)
        {
            briefingFooter = new Label { Name = "BriefingFooter" };
            briefingPanel.AddChild(briefingFooter);
        }

        serverBadge = GetNodeOrNull<Panel>("LoginPanel/ServerBadge")!;
        if (serverBadge == null)
        {
            serverBadge = new Panel { Name = "ServerBadge" };
            loginPanel.AddChild(serverBadge);
        }
        serverBadgeLabel = serverBadge.GetNodeOrNull<Label>("ServerBadgeLabel")!;
        if (serverBadgeLabel == null)
        {
            serverBadgeLabel = new Label { Name = "ServerBadgeLabel" };
            serverBadge.AddChild(serverBadgeLabel);
        }

        footerLabel = GetNode<Label>("FooterLabel");

        usernameInput.Text = "test";
        passwordInput.Text = "123456";

        ResolveServerAddress();

        idCardInput = new LineEdit { Name = "IdCardInput", Visible = false };
        loginPanel.AddChild(idCardInput);
        idCardInput.PlaceholderText = "  身份证号 (选填实名认证，如 110101199003072391)";
        idCardInput.Size = new Vector2(432f, 46f);

        phoneInput = new LineEdit { Name = "PhoneInput", Visible = false };
        loginPanel.AddChild(phoneInput);
        phoneInput.PlaceholderText = "  手机号码 (选填，如 13800138000)";
        phoneInput.Size = new Vector2(432f, 46f);

        smsInput = new LineEdit { Name = "SmsInput", Visible = false };
        loginPanel.AddChild(smsInput);
        smsInput.PlaceholderText = "  验证码 (选填，默认 123456)";
        smsInput.Size = new Vector2(268f, 46f);

        sendSmsButton = new Button { Name = "SendSmsButton", Visible = false };
        loginPanel.AddChild(sendSmsButton);
        sendSmsButton.Text = "获取验证码";
        sendSmsButton.Size = new Vector2(154f, 46f);
        sendSmsButton.Pressed += () => StartSmsCountdown();

        ApplyLocalizedText();

        loginButton.Pressed += () => _ = OnBigButtonPressed();
        registerButton.Pressed += () => SetRegisterMode(!isRegisterMode);
        guestButton.Pressed += () => _ = GuestLogin();

        ApplyServer();
        StyleUi();
        uiReady = true;
        SetRegisterMode(false);

        var cmdArgs = CommandLineArgs.Get();
        if (System.Array.IndexOf(cmdArgs, "--login-flow-test") != -1)
        {
            _ = RunAutomatedLoginFlow();
            return;
        }

        if (System.Array.IndexOf(cmdArgs, "--capture-preview") != -1 || System.Array.IndexOf(cmdArgs, "--battle") != -1 || Godot.FileAccess.FileExists("user://auto_battle.flag"))
        {
            Callable.From(() => GetTree().ChangeSceneToFile("res://scenes/battle/BattlePrototype.tscn")).CallDeferred();
            return;
        }
    }

    void ResolveServerAddress()
    {
        fixedServerAddress = DefaultServer;

        var cmdArgs = CommandLineArgs.Get();
        for (int i = 0; i < cmdArgs.Length; i++)
        {
            if (cmdArgs[i] == "--server" && i + 1 < cmdArgs.Length)
            {
                fixedServerAddress = cmdArgs[i + 1].Trim();
                break;
            }
        }

        if (fixedServerAddress == DefaultServer)
        {
            try
            {
                if (Godot.FileAccess.FileExists("user://server_ip.txt"))
                {
                    var saved = Godot.FileAccess.GetFileAsString("user://server_ip.txt")?.Trim();
                    if (!string.IsNullOrWhiteSpace(saved))
                        fixedServerAddress = saved;
                }
            }
            catch { }
        }

        if (fixedServerAddress == DefaultServer && OS.HasFeature("android"))
        {
            fixedServerAddress = "192.168.1.176:8080";
        }

        if (!fixedServerAddress.Contains(':'))
            fixedServerAddress += ":8080";
    }

    async Task RunAutomatedLoginFlow()
    {
        GD.Print("[LOGIN_FLOW_TEST] Phase 1: Login Scene loaded. Initializing test credentials...");
        usernameInput.Text = "test";
        passwordInput.Text = "123456";
        SetStatus($"【自动化测试流程】正在连接指挥部身份验证中心 ({fixedServerAddress})...");

        // Display login terminal for 2.5s (approx 150 frames @ 60 FPS)
        await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);

        GD.Print("[LOGIN_FLOW_TEST] Phase 1.1: Triggering Login authorization...");
        SetBusy(true, "【自动化测试流程】正在验证指挥官作战凭证 (账号: test)...");

        bool success = false;
        try
        {
            ApplyServer();
            var result = await (NetClient.Instance?.Login("test", "123456") ?? Task.FromResult(Failure("NetClient offline")));
            if (result.GetBool("success"))
            {
                success = true;
                GD.Print("[LOGIN_FLOW_TEST] Live Backend Login SUCCESS! Session token and profile retrieved.");
                SetStatus("【自动化测试流程】指挥官身份验证成功！正在跳转作战大厅...");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[LOGIN_FLOW_TEST] Login exception: {ex.Message}");
        }

        if (!success)
        {
            GD.Print("[LOGIN_FLOW_TEST] Fallback: Engaging GuestLogin session...");
            await GuestLogin();
        }
        else
        {
            await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);
            EnterLobby();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && uiReady)
            LayoutFromUnityAnchors();
    }

    async Task Login()
    {
        ApplyServer();
        var user = usernameInput.Text.Trim();
        var pass = passwordInput.Text;
        if (string.IsNullOrWhiteSpace(user))
        {
            SetStatus("请输入指挥官账号");
            return;
        }
        if (string.IsNullOrWhiteSpace(pass))
        {
            SetStatus("请输入通讯密钥密码");
            return;
        }

        SetBusy(true, "正在连接战区指挥中心验证凭证...");
        var result = await (NetClient.Instance?.Login(user, pass) ?? Task.FromResult(Failure("网络模块未初始化")));
        if (result.GetBool("success"))
        {
            SetStatus("凭证验证通过，正在进驻战区大厅...");
            EnterLobby();
            return;
        }

        SetBusy(false, result.GetString("error", "登录验证未通过"));
    }

    async Task Register()
    {
        ApplyServer();
        var user = usernameInput.Text.Trim();
        var pass = passwordInput.Text;
        int weight = GetNameWeight(user);
        if (weight < 4 || weight > 14)
        {
            SetStatus("指挥官代号长度须为4-14位（中文算2位，英文算1位）");
            return;
        }
        if (ContainsBlockedName(user))
        {
            SetStatus("账号代号包含敏感字符，请重新输入");
            return;
        }
        if (pass.Length < 6)
        {
            SetStatus("通讯密码至少需要 6 个字符");
            return;
        }

        var idCard = idCardInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(idCard) && idCard.Length != 18)
        {
            SetStatus("身份证号须为18位（选填，亦可留空）");
            return;
        }

        var phone = phoneInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (phone.Length != 11 || !phone.StartsWith("1"))
            {
                SetStatus("手机号码须为11位（选填，亦可留空）");
                return;
            }

            var code = smsInput.Text.Trim();
            if (code != "123456")
            {
                SetStatus("短信验证码错误或已过期（模拟验证码为 123456）");
                return;
            }
        }

        SetBusy(true, "正在注册新指挥官档案...");
        var result = await (NetClient.Instance?.Register(user, pass, idCard) ?? Task.FromResult(Failure("网络模块未初始化")));
        if (result.GetBool("success"))
        {
            SetBusy(false, "");
            SetRegisterMode(false);
            SetStatus("指挥官档案创建成功，请登入！");
            return;
        }

        SetBusy(false, result.GetString("error", "注册失败"));
    }

    Task GuestLogin()
    {
        var guestName = $"指挥官{GD.Randi() % 9000 + 1000}";
        GameState.Instance?.SetSession(new Godot.Collections.Dictionary
        {
            ["token"] = $"guest-local-{GD.Randi()}",
            ["userId"] = "guest-local",
            ["username"] = guestName,
            ["isGuest"] = true,
            ["gold"] = 1000,
            ["gems"] = 100
        });
        SetBusy(true, "【快速出击】正在进驻战区大厅...");
        EnterLobby();
        return Task.CompletedTask;
    }

    void ApplyServer()
    {
        if (string.IsNullOrWhiteSpace(fixedServerAddress))
            fixedServerAddress = DefaultServer;

        if (NetClient.Instance is not null)
            NetClient.Instance.ServerUrl = fixedServerAddress.StartsWith("http") ? fixedServerAddress : "http://" + fixedServerAddress;

        if (serverBadgeLabel != null)
        {
            serverBadgeLabel.Text = $"🟢 战区官方主节点 · 专线已就绪 ({fixedServerAddress})";
        }
    }

    void SetBusy(bool busy, string status)
    {
        loginButton.Disabled = busy;
        registerButton.Disabled = busy;
        guestButton.Disabled = busy;
        SetStatus(status);
    }

    void SetStatus(string status)
    {
        if (status == "NETWORK_UNAVAILABLE")
        {
            status = "战区服务器未连接，请启动服务端，或点击【快速出击】直接开展战术演训。";
        }
        statusLabel.Text = status;
        statusLabel.Visible = !string.IsNullOrWhiteSpace(status);
    }

    void EnterLobby()
    {
        Callable.From(() => GetTree().ChangeSceneToFile(LobbyScenePath)).CallDeferred();
    }

    static Godot.Collections.Dictionary Failure(string error)
        => new()
        {
            ["success"] = false,
            ["error"] = error
        };

    void ApplyLocalizedText()
    {
        titleLabel.Text = "▪  最高统帅部 · 身份核验";
        editionLabel.Text = "现代化三栖联合作战终端 · 2026 战略版";
        briefingTag.Text = "★ 战区联合指挥部 · 战略动员令";
        briefingTitle.Text = "钢铁雄师 · 决胜全域";
        briefingBody.Text = "海陆空天 · 立体协同 · 实时战略巅峰对抗\n\n各战区重装突击集群已完成战前集结，防空拦截天网全面张开，装甲铁流静待最高指令！\n\n立即接入全域战区指挥链路，调动三栖尖端战力撕裂敌军防线，主宰现代战争全域疆场！";
        if (briefingHint != null)
            briefingHint.Text = "💡 指挥官提示：\n点击【快速出击】可免密直接进驻作战大厅，即刻展开战术推演与实兵匹配。";
        if (briefingFooter != null)
            briefingFooter.Text = "● 战备等级：一级战备 (DEFCON 1)   |   战略链路：全域同步";
        footerLabel.Text = "战区全域联合指挥中枢 · 现代化海陆空立体推演系统 · 2026";
        usernameInput.PlaceholderText = "  👤 指挥官代号 / 账号";
        passwordInput.PlaceholderText = "  🔑 通讯密钥 / 密码";
        loginButton.Text = "指  挥  官  登  入";
        guestButton.Text = "⚡ 快速出击";
        registerButton.Text = "📝 注册新账号";
    }

    void LayoutFromUnityAnchors()
    {
        var size = GetViewportRect().Size;
        var scale = Mathf.Clamp(Mathf.Min(size.X / 1280f, size.Y / 720f), 0.78f, 1f);

        var loginHeight = isRegisterMode ? 620f : 476f;
        var loginBase = new Vector2(520f, loginHeight);
        var briefingBase = new Vector2(520f, 476f);
        var gap = Mathf.Max(44f, 48f * scale);
        var loginWidth = loginBase.X * scale;
        var briefingWidth = briefingBase.X * scale;
        var totalWidth = loginWidth + briefingWidth + gap;
        var left = Mathf.Max(48f, (size.X - totalWidth) * 0.5f);
        var loginTop = Mathf.Max(64f, (size.Y - loginHeight * scale) * 0.5f);
        var briefingTop = Mathf.Max(64f, (size.Y - briefingBase.Y * scale) * 0.5f);

        PlacePanel(loginPanel, loginBase, new Vector2(left + briefingWidth + gap, loginTop), scale);
        PlacePanel(briefingPanel, briefingBase, new Vector2(left, briefingTop), scale);
        LayoutBriefingContent();

        if (footerLabel != null)
        {
            footerLabel.Size = new Vector2(600f, 26f);
            footerLabel.Position = new Vector2((size.X - 600f) * 0.5f, size.Y - 36f);
        }
    }

    void LayoutBriefingContent()
    {
        if (briefingTag != null)
        {
            briefingTag.Position = new Vector2(40f, 28f);
            briefingTag.Size = new Vector2(440f, 28f);
        }

        if (briefingTitle != null)
        {
            briefingTitle.Position = new Vector2(40f, 58f);
            briefingTitle.Size = new Vector2(440f, 46f);
        }

        var divA = briefingPanel.GetNodeOrNull<Control>("BriefingDividerA");
        if (divA != null)
        {
            divA.Position = new Vector2(40f, 112f);
            divA.Size = new Vector2(440f, 2f);
        }

        if (briefingBody != null)
        {
            briefingBody.Position = new Vector2(40f, 124f);
            briefingBody.Size = new Vector2(440f, 154f);
            briefingBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        if (briefingHintBox != null)
        {
            briefingHintBox.Position = new Vector2(40f, 288f);
            briefingHintBox.Size = new Vector2(440f, 86f);
        }

        if (briefingHint != null)
        {
            briefingHint.Position = new Vector2(12f, 8f);
            briefingHint.Size = new Vector2(416f, 70f);
            briefingHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        var divB = briefingPanel.GetNodeOrNull<Control>("BriefingDividerB");
        if (divB != null)
        {
            divB.Position = new Vector2(40f, 388f);
            divB.Size = new Vector2(440f, 2f);
        }

        if (briefingFooter != null)
        {
            briefingFooter.Position = new Vector2(40f, 404f);
            briefingFooter.Size = new Vector2(440f, 34f);
        }
    }

    static void PlacePanel(Control control, Vector2 baseSize, Vector2 topLeft, float scale)
    {
        control.Size = baseSize;
        control.PivotOffset = Vector2.Zero;
        control.Position = topLeft;
        control.Scale = Vector2.One * scale;
    }

    void StyleUi()
    {
        MetalUiStyle.ApplyMetalPanel(loginPanel, MakeGlassPanelPalette(0.42f), 2, 18, 2);
        MetalUiStyle.ApplyMetalPanel(briefingPanel, MakeGlassPanelPalette(0.38f), 2, 16, 2);
        loginHeader.AddThemeStyleboxOverride("panel", MakeHeaderStyle(new Color(0.12f, 0.19f, 0.08f, 0.70f)));
        loginFrame.Modulate = new Color(1f, 0.92f, 0.64f, 0.18f);

        StyleLabel(titleLabel, new Color(0.92f, 0.84f, 0.60f), 22, 2, HorizontalAlignment.Center);
        StyleLabel(editionLabel, new Color(0.72f, 0.88f, 0.76f), 14, 1, HorizontalAlignment.Center);
        StyleLabel(briefingTag, new Color(1f, 0.82f, 0.28f), 15, 1, HorizontalAlignment.Left);
        StyleLabel(briefingTitle, new Color(0.96f, 0.90f, 0.65f), 30, 2, HorizontalAlignment.Left);
        StyleLabel(briefingBody, new Color(0.88f, 0.94f, 0.90f), 14, 1, HorizontalAlignment.Left);
        briefingBody.AddThemeConstantOverride("line_spacing", 4);

        if (briefingHintBox != null)
        {
            var hintBoxStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.04f, 0.12f, 0.15f, 0.72f),
                BorderColor = new Color(0.24f, 0.72f, 0.78f, 0.45f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                ContentMarginLeft = 12,
                ContentMarginTop = 8,
                ContentMarginRight = 12,
                ContentMarginBottom = 8
            };
            briefingHintBox.AddThemeStyleboxOverride("panel", hintBoxStyle);
        }
        if (briefingHint != null)
        {
            StyleLabel(briefingHint, new Color(0.72f, 0.95f, 0.92f), 13, 1, HorizontalAlignment.Left);
            briefingHint.AddThemeConstantOverride("line_spacing", 3);
        }

        if (briefingFooter != null)
            StyleLabel(briefingFooter, new Color(0.55f, 0.88f, 0.78f), 13, 1, HorizontalAlignment.Center);
        StyleLabel(footerLabel, new Color(0.78f, 0.92f, 0.95f, 0.92f), 14, 1, HorizontalAlignment.Center);

        // Style the Server Badge
        var badgeStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.07f, 0.08f, 0.82f),
            BorderColor = new Color(0.24f, 0.65f, 0.48f, 0.60f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10,
            ContentMarginTop = 6,
            ContentMarginRight = 10,
            ContentMarginBottom = 6
        };
        serverBadge.AddThemeStyleboxOverride("panel", badgeStyle);
        serverBadgeLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        serverBadgeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        serverBadgeLabel.VerticalAlignment = VerticalAlignment.Center;
        serverBadgeLabel.AddThemeFontSizeOverride("font_size", 14);
        serverBadgeLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.95f, 0.78f));
        serverBadgeLabel.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.05f, 0.03f, 0.95f));
        serverBadgeLabel.AddThemeConstantOverride("outline_size", 1);

        StyleInput(usernameInput);
        StyleInput(passwordInput);
        StyleInput(idCardInput);
        StyleInput(phoneInput);
        StyleInput(smsInput);
        StyleActionButton(loginButton, ButtonTone.Primary);
        StyleActionButton(guestButton, ButtonTone.Steel);
        StyleActionButton(registerButton, ButtonTone.Green);
        StyleActionButton(sendSmsButton, ButtonTone.Steel);

        statusLabel.AddThemeFontSizeOverride("font_size", 15);
        statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.42f, 0.28f));
        statusLabel.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.02f, 0.01f, 0.95f));
        statusLabel.AddThemeConstantOverride("outline_size", 1);

        loginHeader.CustomMinimumSize = new Vector2(0f, 46f);
    }

    static MetalUiStyle.MetalPalette MakeGlassPanelPalette(float alpha)
        => new(
            new Color(0.010f, 0.028f, 0.030f, alpha),
            new Color(0.92f, 0.72f, 0.18f, 0.78f),
            new Color(1f, 0.94f, 0.72f, 0.58f),
            new Color(0.04f, 0.032f, 0.020f, 0.66f),
            new Color(0.98f, 0.74f, 0.20f, 0.18f));

    static StyleBoxFlat MakeHeaderStyle(Color bg)
        => new()
        {
            BgColor = bg,
            BorderColor = new Color(0.25f, 0.78f, 0.85f, 0.55f),
            BorderWidthBottom = 1
        };

    static void StyleLabel(Label label, Color color, int fontSize, int outline, HorizontalAlignment alignment)
    {
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0.03f, 0.035f, 0.04f, 0.95f));
        label.AddThemeConstantOverride("outline_size", outline);
        label.HorizontalAlignment = alignment;
    }

    static void StyleInput(LineEdit input)
    {
        input.CustomMinimumSize = new Vector2(432f, 46f);
        input.AddThemeFontSizeOverride("font_size", 17);
        input.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 1f));
        input.AddThemeColorOverride("caret_color", new Color(1f, 0.90f, 0.58f));
        input.AddThemeColorOverride("font_placeholder_color", new Color(0.58f, 0.66f, 0.70f, 0.92f));

        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.12f, 0.18f, 1f),
            BorderColor = new Color(0.50f, 0.64f, 0.70f, 0.28f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginTop = 10,
            ContentMarginRight = 14,
            ContentMarginBottom = 10
        };
        var focus = (StyleBoxFlat)normal.Duplicate();
        focus.BorderColor = new Color(0.92f, 0.76f, 0.30f, 0.92f);
        focus.ShadowColor = new Color(0.95f, 0.77f, 0.24f, 0.22f);
        focus.ShadowSize = 8;

        input.AddThemeStyleboxOverride("normal", normal);
        input.AddThemeStyleboxOverride("read_only", normal);
        input.AddThemeStyleboxOverride("focus", focus);
    }

    enum ButtonTone
    {
        Primary,
        Steel,
        Green
    }

    static void StyleActionButton(Button button, ButtonTone tone)
    {
        var fontSize = tone == ButtonTone.Primary ? 22 : 17;
        button.AddThemeColorOverride("font_color", tone == ButtonTone.Primary
            ? new Color(1f, 0.96f, 0.82f)
            : new Color(0.95f, 0.97f, 1f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
        button.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.97f, 0.86f));
        button.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.04f, 0.02f, 0.95f));
        button.AddThemeConstantOverride("outline_size", 2);

        Color normalBg = tone switch
        {
            ButtonTone.Primary => new Color(0.62f, 0.45f, 0.07f, 1f),
            ButtonTone.Green => new Color(0.14f, 0.26f, 0.16f, 1f),
            _ => new Color(0.18f, 0.22f, 0.28f, 1f)
        };
        Color border = tone switch
        {
            ButtonTone.Primary => new Color(1f, 0.88f, 0.35f, 0.85f),
            ButtonTone.Green => new Color(0.35f, 0.80f, 0.40f, 0.72f),
            _ => new Color(0.50f, 0.68f, 0.76f, 0.62f)
        };

        var palette = new MetalUiStyle.MetalPalette(
            normalBg,
            border,
            tone == ButtonTone.Primary ? new Color(1f, 0.96f, 0.76f, 0.95f) : new Color(0.92f, 0.97f, 1f, 0.90f),
            new Color(0.06f, 0.04f, 0.02f, 0.92f),
            tone == ButtonTone.Primary ? new Color(0.95f, 0.77f, 0.24f, 0.25f) : new Color(border.R, border.G, border.B, 0.12f));
        MetalUiStyle.ApplyMetalButton(button, palette, fontSize, tone == ButtonTone.Primary);
    }

    static readonly string[] BlockedNames = new[]
    {
        "傻逼", "煞笔", "沙比", "操你妈", "肏", "妈的", "特么的", "王八蛋", "滚蛋", "垃圾", "废柴", "混蛋", "二百五", "婊子", "贱人",
        "fuck", "bitch", "shit", "asshole", "bastard", "sb", "wocao", "caonima"
    };

    static bool ContainsBlockedName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant();
        foreach (var word in BlockedNames)
        {
            if (lower.Contains(word))
                return true;
        }
        return false;
    }

    static int GetNameWeight(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        int weight = 0;
        foreach (char c in name)
        {
            weight += c > 127 ? 2 : 1;
        }
        return weight;
    }

    async Task OnBigButtonPressed()
    {
        if (isRegisterMode)
        {
            await Register();
        }
        else
        {
            await Login();
        }
    }

    void SetRegisterMode(bool registerMode)
    {
        isRegisterMode = registerMode;
        if (isRegisterMode)
        {
            titleLabel.Text = "▪  新晋指挥官战备注册";
            loginButton.Text = "提  交  注  册";
            registerButton.Text = "◀  返回登录终端";
            guestButton.Visible = false;

            idCardInput.Visible = true;
            phoneInput.Visible = true;
            smsInput.Visible = true;
            sendSmsButton.Visible = true;

            serverBadge.Position = new Vector2(44f, 86f);
            serverBadge.Size = new Vector2(432f, 38f);

            usernameInput.Position = new Vector2(44f, 134f);
            usernameInput.Size = new Vector2(432f, 46f);

            passwordInput.Position = new Vector2(44f, 188f);
            passwordInput.Size = new Vector2(432f, 46f);

            idCardInput.Position = new Vector2(44f, 242f);
            idCardInput.Size = new Vector2(432f, 46f);

            phoneInput.Position = new Vector2(44f, 296f);
            phoneInput.Size = new Vector2(432f, 46f);

            smsInput.Position = new Vector2(44f, 350f);
            smsInput.Size = new Vector2(268f, 46f);

            sendSmsButton.Position = new Vector2(322f, 350f);
            sendSmsButton.Size = new Vector2(154f, 46f);

            loginButton.Position = new Vector2(44f, 416f);
            loginButton.Size = new Vector2(432f, 52f);

            registerButton.Position = new Vector2(44f, 480f);
            registerButton.Size = new Vector2(432f, 46f);

            statusLabel.Position = new Vector2(44f, 542f);
            statusLabel.Size = new Vector2(432f, 36f);

            SetStatus("");
        }
        else
        {
            titleLabel.Text = "▪  最高统帅部 · 身份核验";
            loginButton.Text = "指  挥  官  登  入";
            registerButton.Text = "📝 注册新账号";
            guestButton.Visible = true;

            idCardInput.Visible = false;
            phoneInput.Visible = false;
            smsInput.Visible = false;
            sendSmsButton.Visible = false;

            serverBadge.Position = new Vector2(44f, 88f);
            serverBadge.Size = new Vector2(432f, 38f);

            usernameInput.Position = new Vector2(44f, 142f);
            usernameInput.Size = new Vector2(432f, 50f);

            passwordInput.Position = new Vector2(44f, 206f);
            passwordInput.Size = new Vector2(432f, 50f);

            loginButton.Position = new Vector2(44f, 276f);
            loginButton.Size = new Vector2(432f, 56f);

            guestButton.Position = new Vector2(44f, 352f);
            guestButton.Size = new Vector2(210f, 48f);

            registerButton.Position = new Vector2(266f, 352f);
            registerButton.Size = new Vector2(210f, 48f);

            statusLabel.Position = new Vector2(44f, 418f);
            statusLabel.Size = new Vector2(432f, 36f);

            SetStatus("");
        }
        LayoutFromUnityAnchors();
    }

    void StartSmsCountdown()
    {
        var phone = phoneInput.Text.Trim();
        if (phone.Length != 11 || !phone.StartsWith("1"))
        {
            SetStatus("请输入正确的11位手机号码");
            return;
        }

        smsCountdown = 60;
        sendSmsButton.Disabled = true;
        sendSmsButton.Text = $"{smsCountdown}s";
        SetStatus("验证码已发送（模拟验证码为 123456）");

        if (smsTimer is null)
        {
            smsTimer = new Godot.Timer();
            AddChild(smsTimer);
            smsTimer.Timeout += OnSmsTimerTimeout;
        }
        smsTimer.WaitTime = 1.0f;
        smsTimer.OneShot = false;
        smsTimer.Start();
    }

    void OnSmsTimerTimeout()
    {
        smsCountdown--;
        if (smsCountdown <= 0)
        {
            smsTimer.Stop();
            sendSmsButton.Disabled = false;
            sendSmsButton.Text = "获取验证码";
        }
        else
        {
            sendSmsButton.Text = $"{smsCountdown}s";
        }
    }
}
