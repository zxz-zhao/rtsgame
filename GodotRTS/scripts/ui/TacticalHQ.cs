using Godot;

/// <summary>
/// 战术指挥中心主控台 Presenter
/// 极简高科技军事风格 · 全屏沉浸式 HUD
/// TC-03 生命周期安全 · TC-07 零GC帧循环
/// </summary>
public partial class TacticalHQ : Control
{
    // ── 强类型信号 ───────────────────────────────────────────────
    [Signal] public delegate void FullAssaultPressedEventHandler();
    [Signal] public delegate void TacticalRetreatPressedEventHandler();
    [Signal] public delegate void CallSupportPressedEventHandler();
    [Signal] public delegate void CoreDefensePressedEventHandler();

    // ── 节点引用 ─────────────────────────────────────────────────
    private Label       _lblClock        = null!;
    private Label       _lblThreatLevel  = null!;
    private Label       _lblThreatVal    = null!;
    private Label       _lblCoordX       = null!;
    private Label       _lblCoordY       = null!;
    private Label       _lblMissionTimer = null!;

    // 单位状态
    private Label       _lblUnit1Hp      = null!;
    private Label       _lblUnit2Hp      = null!;
    private Label       _lblUnit3Hp      = null!;
    private ProgressBar _barUnit1Hp      = null!;
    private ProgressBar _barUnit2Hp      = null!;
    private ProgressBar _barUnit3Hp      = null!;
    private Label       _lblUnit1Status  = null!;
    private Label       _lblUnit2Status  = null!;
    private Label       _lblUnit3Status  = null!;

    // 资源
    private ProgressBar _barEnergy       = null!;
    private ProgressBar _barAmmo         = null!;
    private ProgressBar _barSupply       = null!;
    private Label       _lblEnergyVal    = null!;
    private Label       _lblAmmoVal      = null!;
    private Label       _lblSupplyVal    = null!;

    // 雷达
    private Control     _radarRoot       = null!;
    private Control     _radarSweep      = null!;

    // 指令按钮
    private Button      _btnAssault      = null!;
    private Button      _btnRetreat      = null!;
    private Button      _btnSupport      = null!;
    private Button      _btnDefense      = null!;

    // ── 帧动效状态（零GC纯值类型）───────────────────────────────
    private float _radarAngle   = 0f;
    private float _clockTimer   = 0f;
    private float _blinkT       = 0f;
    private float _scanlineT    = 0f;
    private int   _missionSec   = 0;
    private float _missionAccum = 0f;

    // ────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        // 时钟与威胁
        _lblClock        = GetNode<Label>("%LblClock");
        _lblThreatLevel  = GetNode<Label>("%LblThreatLevel");
        _lblThreatVal    = GetNode<Label>("%LblThreatVal");
        _lblCoordX       = GetNode<Label>("%LblCoordX");
        _lblCoordY       = GetNode<Label>("%LblCoordY");
        _lblMissionTimer = GetNode<Label>("%LblMissionTimer");

        // 单位
        _lblUnit1Hp     = GetNode<Label>("%LblUnit1Hp");
        _lblUnit2Hp     = GetNode<Label>("%LblUnit2Hp");
        _lblUnit3Hp     = GetNode<Label>("%LblUnit3Hp");
        _barUnit1Hp     = GetNode<ProgressBar>("%BarUnit1Hp");
        _barUnit2Hp     = GetNode<ProgressBar>("%BarUnit2Hp");
        _barUnit3Hp     = GetNode<ProgressBar>("%BarUnit3Hp");
        _lblUnit1Status = GetNode<Label>("%LblUnit1Status");
        _lblUnit2Status = GetNode<Label>("%LblUnit2Status");
        _lblUnit3Status = GetNode<Label>("%LblUnit3Status");

        // 资源
        _barEnergy    = GetNode<ProgressBar>("%BarEnergy");
        _barAmmo      = GetNode<ProgressBar>("%BarAmmo");
        _barSupply    = GetNode<ProgressBar>("%BarSupply");
        _lblEnergyVal = GetNode<Label>("%LblEnergyVal");
        _lblAmmoVal   = GetNode<Label>("%LblAmmoVal");
        _lblSupplyVal = GetNode<Label>("%LblSupplyVal");

        // 雷达
        _radarRoot  = GetNode<Control>("%RadarRoot");
        _radarSweep = GetNode<Control>("%RadarSweep");

        // 按钮
        _btnAssault = GetNode<Button>("%BtnAssault");
        _btnRetreat = GetNode<Button>("%BtnRetreat");
        _btnSupport = GetNode<Button>("%BtnSupport");
        _btnDefense = GetNode<Button>("%BtnDefense");

        _btnAssault.Pressed += OnAssaultPressed;
        _btnRetreat.Pressed += OnRetreatPressed;
        _btnSupport.Pressed += OnSupportPressed;
        _btnDefense.Pressed += OnDefensePressed;

        // 初始数据
        _barUnit1Hp.Value = 78;
        _barUnit2Hp.Value = 45;
        _barUnit3Hp.Value = 91;
        _barEnergy.Value  = 72;
        _barAmmo.Value    = 58;
        _barSupply.Value  = 84;

        UpdateResourceLabels();
        PlayEntranceTween();
    }

    // ── 帧循环（TC-07 零GC）──────────────────────────────────────
    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 雷达扫描旋转
        _radarAngle += dt * 60f;
        if (_radarAngle >= 360f) _radarAngle -= 360f;
        _radarSweep.RotationDegrees = _radarAngle;

        // 实时时钟
        _clockTimer += dt;
        if (_clockTimer >= 1f)
        {
            _clockTimer -= 1f;
            _missionSec++;
            var t = System.TimeSpan.FromSeconds(_missionSec);
            _lblMissionTimer.Text = $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            UpdateClock();
        }

        // 威胁等级脉冲
        _blinkT += dt * 3.2f;
        float pulse = 0.6f + 0.4f * Mathf.Sin(_blinkT * Mathf.Pi);
        _lblThreatVal.Modulate = new Color(1f, pulse * 0.3f, pulse * 0.3f, 1f);

        // 坐标漂移模拟
        _scanlineT += dt * 0.4f;
        float cx = 108.923f + Mathf.Sin(_scanlineT * 1.3f) * 0.002f;
        float cy = 34.217f  + Mathf.Cos(_scanlineT * 0.9f) * 0.001f;
        _lblCoordX.Text = $"E {cx:F4}\u00b0";
        _lblCoordY.Text = $"N {cy:F4}\u00b0";
    }

    // ── 时钟更新 ─────────────────────────────────────────────────
    private void UpdateClock()
    {
        var now = System.DateTime.Now;
        _lblClock.Text = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}";
    }

    // ── 资源标签更新 ─────────────────────────────────────────────
    private void UpdateResourceLabels()
    {
        _lblEnergyVal.Text = $"{_barEnergy.Value:F0}%";
        _lblAmmoVal.Text   = $"{_barAmmo.Value:F0}%";
        _lblSupplyVal.Text = $"{_barSupply.Value:F0}%";
    }

    // ── 入场 Tween ───────────────────────────────────────────────
    private void PlayEntranceTween()
    {
        Modulate = new Color(1f, 1f, 1f, 0f);
        Tween tw = CreateTween();
        tw.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(this, "modulate:a", 1.0f, 0.6f);
        UpdateClock();
    }

    // ── 按钮信号 ─────────────────────────────────────────────────
    private void OnAssaultPressed()
    {
        EmitSignal(SignalName.FullAssaultPressed);
        FlashButton(_btnAssault, MetalPalette.Red);
    }

    private void OnRetreatPressed()
    {
        EmitSignal(SignalName.TacticalRetreatPressed);
        FlashButton(_btnRetreat, MetalPalette.Gold);
    }

    private void OnSupportPressed()
    {
        EmitSignal(SignalName.CallSupportPressed);
        FlashButton(_btnSupport, MetalPalette.Cyan);
    }

    private void OnDefensePressed()
    {
        EmitSignal(SignalName.CoreDefensePressed);
        FlashButton(_btnDefense, MetalPalette.Steel);
    }

    private void FlashButton(Button btn, Color flashColor)
    {
        Tween tw = CreateTween();
        tw.TweenProperty(btn, "modulate", flashColor, 0.06f);
        tw.TweenProperty(btn, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f);
    }

    // ── TC-03 生命周期安全解绑 ───────────────────────────────────
    public override void _ExitTree()
    {
        _btnAssault.Pressed -= OnAssaultPressed;
        _btnRetreat.Pressed -= OnRetreatPressed;
        _btnSupport.Pressed -= OnSupportPressed;
        _btnDefense.Pressed -= OnDefensePressed;
    }
}
