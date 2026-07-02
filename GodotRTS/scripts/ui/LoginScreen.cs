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
        ApplyLocalizedText();

        loginButton.Pressed += () => _ = Login();
        registerButton.Pressed += () => _ = Register();
        guestButton.Pressed += () => _ = GuestLogin();

        ApplyServer();
        StyleUi();
        uiReady = true;
        LayoutFromUnityAnchors();
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
        if (user.Length < 3)
        {
            SetStatus("\u8d26\u53f7\u81f3\u5c11 3 \u4e2a\u5b57\u7b26");
            return;
        }
        if (pass.Length < 6)
        {
            SetStatus("\u5bc6\u7801\u81f3\u5c11 6 \u4e2a\u5b57\u7b26");
            return;
        }

        SetBusy(true, "\u6ce8\u518c\u4e2d...");
        var result = await (NetClient.Instance?.Register(user, pass) ?? Task.FromResult(Failure("\u7f51\u7edc\u6a21\u5757\u672a\u521d\u59cb\u5316")));
        if (result.GetBool("success"))
        {
            SetStatus("\u6ce8\u518c\u6210\u529f\uff0c\u6b63\u5728\u8fdb\u5165\u5927\u5385...");
            EnterLobby();
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

        var loginBase = new Vector2(520f, 476f);
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
        StyleActionButton(loginButton, ButtonTone.Primary);
        StyleActionButton(guestButton, ButtonTone.Steel);
        StyleActionButton(registerButton, ButtonTone.Green);

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
}
