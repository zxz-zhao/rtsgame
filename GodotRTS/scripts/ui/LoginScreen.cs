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
    Label footerLabel = null!;
    bool uiReady;
    bool isRegisterMode;
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
        footerLabel = GetNode<Label>("FooterLabel");

        usernameInput.Text = "test";
        passwordInput.Text = "123456";

        idCardInput = new LineEdit { Name = "IdCardInput", Visible = false };
        loginPanel.AddChild(idCardInput);
        idCardInput.PlaceholderText = "  \u8eab\u4efd\u8bc1\u53f7"; // "  身份证号"
        idCardInput.Size = new Vector2(420f, 56f);

        phoneInput = new LineEdit { Name = "PhoneInput", Visible = false };
        loginPanel.AddChild(phoneInput);
        phoneInput.PlaceholderText = "  \u624b\u673a\u53f7\u7801"; // "  手机号码"
        phoneInput.Size = new Vector2(420f, 56f);

        smsInput = new LineEdit { Name = "SmsInput", Visible = false };
        loginPanel.AddChild(smsInput);
        smsInput.PlaceholderText = "  \u9a8c\u8bc1\u7801"; // "  验证码"
        smsInput.Size = new Vector2(260f, 56f);

        sendSmsButton = new Button { Name = "SendSmsButton", Visible = false };
        loginPanel.AddChild(sendSmsButton);
        sendSmsButton.Text = "\u83b7\u53d6\u9a8c\u8bc1\u7801"; // "获取验证码"
        sendSmsButton.Size = new Vector2(150f, 46f);
        sendSmsButton.Pressed += () => StartSmsCountdown();

        ApplyLocalizedText();

        loginButton.Pressed += () => _ = OnBigButtonPressed();
        registerButton.Pressed += () => SetRegisterMode(!isRegisterMode);
        guestButton.Pressed += () => _ = GuestLogin();

        ApplyServer();
        StyleUi();
        uiReady = true;
        SetRegisterMode(false);
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
            SetStatus("\u8bf7\u8f93\u5165\u8d26\u53f7");
            return;
        }
        if (string.IsNullOrWhiteSpace(pass))
        {
            SetStatus("\u8bf7\u8f93\u5165\u5bc6\u7801");
            return;
        }

        SetBusy(true, "\u767b\u5f55\u4e2d...");
        var result = await (NetClient.Instance?.Login(user, pass) ?? Task.FromResult(Failure("\u7f51\u7edc\u6a21\u5757\u672a\u521d\u59cb\u5316")));
        if (result.GetBool("success"))
        {
            SetStatus("\u767b\u5f55\u6210\u529f\uff0c\u6b63\u5728\u8fdb\u5165\u5927\u5385...");
            EnterLobby();
            return;
        }

        SetBusy(false, result.GetString("error", "\u767b\u5f55\u5931\u8d25"));
    }

    async Task Register()
    {
        ApplyServer();
        var user = usernameInput.Text.Trim();
        var pass = passwordInput.Text;
        int weight = GetNameWeight(user);
        if (weight < 4 || weight > 14)
        {
            SetStatus("\u8d26\u53f7\u957f\u5ea6\u4e0d\u7b26\u5408\u8981\u6c42\uff08\u4e2d\u6587\u5b57\u7b26\u7b972\uff0c\u82f1\u6587\u7b971\uff0c\u8981\u6c424-14\uff09");
            return;
        }
        if (ContainsBlockedName(user))
        {
            SetStatus("\u8d26\u53f7\u5305\u542b\u654f\u611f\u8bcd\u6216\u4e0d\u5f53\u8a00\u8bba");
            return;
        }
        if (pass.Length < 6)
        {
            SetStatus("\u5bc6\u7801\u81f3\u5c11 6 \u4e2a\u5b57\u7b26");
            return;
        }

        var idCard = idCardInput.Text.Trim();
        if (idCard.Length != 18)
        {
            SetStatus("\u8bf7\u8f93\u5165\u6b63\u786e\u7684\u0031\u0038\u4f4d\u8eab\u4efd\u8bc1\u53f7"); // "请输入正确的18位身份证号"
            return;
        }

        var phone = phoneInput.Text.Trim();
        if (phone.Length != 11 || !phone.StartsWith("1"))
        {
            SetStatus("\u8bf7\u8f93\u5165\u6b63\u786e\u7684\u0031\u0031\u4f4d\u624b\u673a\u53f7\u7801"); // "请输入正确的11位手机号码"
            return;
        }

        var code = smsInput.Text.Trim();
        if (code != "123456")
        {
            SetStatus("\u77ed\u4fe1\u9a8c\u8bc1\u7801\u9519\u8bef\u6216\u5df2\u8fc7\u671f"); // "短信验证码错误或已过期"
            return;
        }

        SetBusy(true, "\u6ce8\u518c\u4e2d...");
        var result = await (NetClient.Instance?.Register(user, pass, idCard) ?? Task.FromResult(Failure("\u7f51\u7edc\u6a21\u5757\u672a\u521d\u59cb\u5316")));
        if (result.GetBool("success"))
        {
            SetBusy(false, "");
            SetRegisterMode(false);
            SetStatus("\u6ce8\u518c\u6210\u529f\uff0c\u8bf7\u767b\u5f55\uff01"); // "注册成功，请登录！"
            return;
        }

        SetBusy(false, result.GetString("error", "\u6ce8\u518c\u5931\u8d25"));
    }

    Task GuestLogin()
    {
        var guestName = $"\u6e38\u5ba2{GD.Randi() % 9000 + 1000}";
        GameState.Instance?.SetSession(new Godot.Collections.Dictionary
        {
            ["token"] = $"guest-local-{GD.Randi()}",
            ["userId"] = "guest-local",
            ["username"] = guestName,
            ["isGuest"] = true,
            ["gold"] = 1000,
            ["gems"] = 100
        });
        SetBusy(true, "\u6e38\u5ba2\u6a21\u5f0f\uff0c\u6b63\u5728\u8fdb\u5165\u5927\u5385...");
        EnterLobby();
        return Task.CompletedTask;
    }

    void ApplyServer()
    {
        var server = OS.HasFeature("android") ? "10.0.2.2:8080" : DefaultServer;
        if (!server.Contains(':'))
            server += ":8080";
        if (NetClient.Instance is not null)
            NetClient.Instance.ServerUrl = "http://" + server;
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
            status = "服务器未连接，请启动 C++ 服务端，或点击“游客登录”直接进入单机演练。";
        }
        statusLabel.Text = status;
        statusLabel.Visible = !string.IsNullOrWhiteSpace(status);
    }

    void EnterLobby()
    {
        GetTree().ChangeSceneToFile(LobbyScenePath);
    }

    static Godot.Collections.Dictionary Failure(string error)
        => new()
        {
            ["success"] = false,
            ["error"] = error
        };

    void ApplyLocalizedText()
    {
        titleLabel.Text = "\u25aa  \u4f5c\u6218\u8eab\u4efd\u9a8c\u8bc1";
        editionLabel.Text = "\u65b0\u7248\u6218\u533a UI \u00b7 BUILD 2026.05.26";
        briefingTag.Text = "\u65b0\u7248\u6307\u6325\u754c\u9762";
        briefingTitle.Text = "RTS \u6765\u6218";
        briefingBody.Text = "\u5feb\u901f\u5165\u573a \u00b7 \u8054\u673a\u5339\u914d \u00b7 \u6d77\u9646\u7a7a\u6218\u573a\n\n\u9009\u62e9\u6e38\u5ba2\u767b\u5f55\uff0c\u53ef\u4ee5\u76f4\u63a5\u8fdb\u5165\u5927\u5385\u9a8c\u8bc1\u5339\u914d\u6d41\u7a0b\u3002";
        footerLabel.Text = "\u7ebf\u4e0a\u4f5c\u6218\u5ba4 / \u6d77 \u00b7 \u7a7a \u00b7 \u88c5\u7532 / 2026 \u65b0\u7248\u754c\u9762";
        usernameInput.PlaceholderText = "  \u8d26\u53f7";
        passwordInput.PlaceholderText = "  \u5bc6\u7801";
        loginButton.Text = "\u767b  \u5f55";
        guestButton.Text = "\u6e38\u5ba2\u767b\u5f55";
        registerButton.Text = "\u6ce8\u518c\u8d26\u53f7";
    }

    void LayoutFromUnityAnchors()
    {
        var size = GetViewportRect().Size;
        var scale = Mathf.Clamp(Mathf.Min(size.X / 1280f, size.Y / 720f), 0.78f, 1f);

        var loginHeight = isRegisterMode ? 620f : 476f;
        var loginBase = new Vector2(520f, loginHeight);
        var briefingBase = new Vector2(520f, 360f);
        var gap = Mathf.Max(44f, 48f * scale);
        var loginWidth = loginBase.X * scale;
        var briefingWidth = briefingBase.X * scale;
        var totalWidth = loginWidth + briefingWidth + gap;
        var left = Mathf.Max(48f, (size.X - totalWidth) * 0.5f);
        var loginTop = Mathf.Max(72f, size.Y * 0.5f - loginBase.Y * 0.5f * scale);
        var briefingTop = Mathf.Max(120f, size.Y * 0.55f - briefingBase.Y * 0.45f * scale);

        PlacePanel(loginPanel, loginBase, new Vector2(left + briefingWidth + gap, loginTop), scale);
        PlacePanel(briefingPanel, briefingBase, new Vector2(left, briefingTop), scale);
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
        StyleLabel(editionLabel, new Color(0.72f, 0.88f, 0.76f), 15, 1, HorizontalAlignment.Center);
        StyleLabel(briefingTag, new Color(1f, 0.82f, 0.28f), 18, 1, HorizontalAlignment.Left);
        StyleLabel(briefingTitle, new Color(0.94f, 0.88f, 0.62f), 34, 2, HorizontalAlignment.Left);
        StyleLabel(briefingBody, new Color(0.90f, 0.95f, 0.88f), 22, 1, HorizontalAlignment.Left);
        StyleLabel(footerLabel, new Color(0.78f, 0.92f, 0.95f, 0.92f), 14, 1, HorizontalAlignment.Center);

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
        editionLabel.Position = new Vector2(50f, 50f);
        usernameInput.Position = new Vector2(50f, 146f);
        passwordInput.Position = new Vector2(50f, 204f);
        loginButton.Position = new Vector2(50f, 274f);
        guestButton.Position = new Vector2(57f, 364f);
        registerButton.Position = new Vector2(281f, 364f);
        statusLabel.Position = new Vector2(50f, 420f);
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
        input.CustomMinimumSize = new Vector2(420f, 56f);
        input.AddThemeFontSizeOverride("font_size", 18);
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
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12
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
        var fontSize = tone == ButtonTone.Primary ? 23 : 18;
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
            titleLabel.Text = "\u25aa  \u6ce8\u518c\u65b0\u8d26\u53f7"; // "▪  注册新账号"
            loginButton.Text = "\u6ce8  \u518c"; // "注  册"
            registerButton.Text = "\u8fd4\u56de\u767b\u5f55"; // "返回登录"
            guestButton.Visible = false;

            idCardInput.Visible = true;
            phoneInput.Visible = true;
            smsInput.Visible = true;
            sendSmsButton.Visible = true;

            usernameInput.Position = new Vector2(50f, 120f);
            passwordInput.Position = new Vector2(50f, 178f);
            idCardInput.Position = new Vector2(50f, 236f);
            phoneInput.Position = new Vector2(50f, 294f);
            smsInput.Position = new Vector2(50f, 352f);
            sendSmsButton.Position = new Vector2(320f, 357f);
            loginButton.Position = new Vector2(50f, 428f);
            registerButton.Position = new Vector2(281f, 512f);
            statusLabel.Position = new Vector2(50f, 568f);

            SetStatus("");
        }
        else
        {
            titleLabel.Text = "\u25aa  \u4f5c\u6218\u8eab\u4efd\u9a8c\u8bc1"; // "▪  作战身份验证"
            loginButton.Text = "\u767b  \u5f55"; // "登  录"
            registerButton.Text = "\u6ce8\u518c\u8d26\u53f7"; // "注册账号"
            guestButton.Visible = true;

            idCardInput.Visible = false;
            phoneInput.Visible = false;
            smsInput.Visible = false;
            sendSmsButton.Visible = false;

            usernameInput.Position = new Vector2(50f, 146f);
            passwordInput.Position = new Vector2(50f, 204f);
            loginButton.Position = new Vector2(50f, 274f);
            guestButton.Position = new Vector2(57f, 364f);
            registerButton.Position = new Vector2(281f, 364f);
            statusLabel.Position = new Vector2(50f, 420f);

            SetStatus("");
        }
        LayoutFromUnityAnchors();
    }

    void StartSmsCountdown()
    {
        var phone = phoneInput.Text.Trim();
        if (phone.Length != 11 || !phone.StartsWith("1"))
        {
            SetStatus("\u8bf7\u8f93\u5165\u6b63\u786e\u7684\u0031\u0031\u4f4d\u624b\u673a\u53f7\u7801"); // "请输入正确的11位手机号码"
            return;
        }

        smsCountdown = 60;
        sendSmsButton.Disabled = true;
        sendSmsButton.Text = $"{smsCountdown}s";
        SetStatus("\u9a8c\u8bc1\u7801\u5df2\u53d1\u9001\uff08\u6a21\u62df\u9a8c\u8bc1\u7801\u4e3a\u0031\u0032\u0033\u0034\u0035\u0036\uff09"); // "验证码已发送（模拟验证码为123456）"

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
            sendSmsButton.Text = "\u83b7\u53d6\u9a8c\u8bc1\u7801"; // "获取验证码"
        }
        else
        {
            sendSmsButton.Text = $"{smsCountdown}s";
        }
    }
}
