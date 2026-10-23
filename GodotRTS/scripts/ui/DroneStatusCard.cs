using Godot;

/// <summary>
/// 无人侦察机状态卡片 Presenter
/// 职责：数据注入、信号声明、Tween 入场动效、生命周期安全解绑
/// </summary>
public partial class DroneStatusCard : Control
{
    // ── 强类型信号 ──────────────────────────────────────────────
    [Signal] public delegate void EmergencyReturnPressedEventHandler();

    // ── 节点引用（unique_name_in_owner 绑定）────────────────────
    private Label      _lblDroneId;
    private Label      _lblStatusBadge;
    private Label      _lblBatteryVal;
    private Label      _lblSignalVal;
    private Label      _lblAltitudeVal;
    private Label      _lblSpeedVal;
    private Label      _lblLatLon;
    private Label      _lblMissionTime;
    private ProgressBar _barBattery;
    private ProgressBar _barSignal;
    private Button     _btnEmergency;
    private Panel      _scanlinePanel;

    // ── 动效状态 ────────────────────────────────────────────────
    private Tween  _pulseTween;
    private float  _blinkPhase = 0f;
    private bool   _isWarning  = false;

    // ── ViewModel（可由外部注入）────────────────────────────────
    public struct DroneViewModel
    {
        public string DroneId;
        public string Status;       // "ONLINE" | "WARNING" | "LOST"
        public float  Battery;      // 0~100
        public float  Signal;       // 0~100
        public float  AltitudeM;
        public float  SpeedKmh;
        public string LatLon;
        public string MissionTime;
    }

    private DroneViewModel _vm = new DroneViewModel
    {
        DroneId     = "UAV-7 · SHADOW",
        Status      = "ONLINE",
        Battery     = 67f,
        Signal      = 88f,
        AltitudeM   = 1240f,
        SpeedKmh    = 312f,
        LatLon      = "N 34°12'41\"  E 108°55'03\"",
        MissionTime = "01:24:37"
    };

    // ────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        // 绑定节点
        _lblDroneId     = GetNode<Label>("%LblDroneId");
        _lblStatusBadge = GetNode<Label>("%LblStatusBadge");
        _lblBatteryVal  = GetNode<Label>("%LblBatteryVal");
        _lblSignalVal   = GetNode<Label>("%LblSignalVal");
        _lblAltitudeVal = GetNode<Label>("%LblAltitudeVal");
        _lblSpeedVal    = GetNode<Label>("%LblSpeedVal");
        _lblLatLon      = GetNode<Label>("%LblLatLon");
        _lblMissionTime = GetNode<Label>("%LblMissionTime");
        _barBattery     = GetNode<ProgressBar>("%BarBattery");
        _barSignal      = GetNode<ProgressBar>("%BarSignal");
        _btnEmergency   = GetNode<Button>("%BtnEmergency");

        // 绑定按钮事件
        _btnEmergency.Pressed += OnEmergencyPressed;

        // 注入数据
        Refresh(_vm);

        // 入场动效
        PlayEntranceTween();
    }

    // ── 数据注入 ─────────────────────────────────────────────────
    public void Refresh(DroneViewModel vm)
    {
        _vm = vm;

        _lblDroneId.Text     = vm.DroneId;
        _lblMissionTime.Text = vm.MissionTime;
        _lblLatLon.Text      = vm.LatLon;
        _lblAltitudeVal.Text = $"{vm.AltitudeM:F0} m";
        _lblSpeedVal.Text    = $"{vm.SpeedKmh:F0} km/h";
        _lblBatteryVal.Text  = $"{vm.Battery:F0}%";
        _lblSignalVal.Text   = $"{vm.Signal:F0}%";

        _barBattery.Value = vm.Battery;
        _barSignal.Value  = vm.Signal;

        // 电量颜色
        Color battColor = vm.Battery > 40f ? MetalUiStyle.Green.Highlight
                        : vm.Battery > 20f ? MetalUiStyle.Gold.Highlight
                                           : MetalUiStyle.Red.Highlight;
        _barBattery.GetThemeStylebox("fill"); // 触发主题缓存
        _lblBatteryVal.AddThemeColorOverride("font_color", battColor);

        // 状态徽章
        _isWarning = vm.Status != "ONLINE";
        switch (vm.Status)
        {
            case "ONLINE":
                _lblStatusBadge.Text = "● ONLINE";
                _lblStatusBadge.AddThemeColorOverride("font_color", MetalUiStyle.Green.Highlight);
                break;
            case "WARNING":
                _lblStatusBadge.Text = "▲ WARNING";
                _lblStatusBadge.AddThemeColorOverride("font_color", MetalUiStyle.Gold.Highlight);
                break;
            default:
                _lblStatusBadge.Text = "✕ SIGNAL LOST";
                _lblStatusBadge.AddThemeColorOverride("font_color", MetalUiStyle.Red.Highlight);
                break;
        }
    }

    // ── 入场 Tween ───────────────────────────────────────────────
    private void PlayEntranceTween()
    {
        Modulate = new Color(1, 1, 1, 0f);
        Position = new Vector2(Position.X, Position.Y + 24f);

        var tw = CreateTween();
        tw.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(this, "modulate:a", 1.0f, 0.45f);
        tw.Parallel().TweenProperty(this, "position:y", Position.Y - 24f, 0.45f);
    }

    // ── 紧急返航按钮闪烁脉冲（_Process 零GC版）────────────────
    private float _pulseT = 0f;
    public override void _Process(double delta)
    {
        _pulseT += (float)delta * 2.4f;
        float alpha = 0.72f + 0.28f * Mathf.Sin(_pulseT * Mathf.Pi);
        _btnEmergency.Modulate = new Color(1f, alpha * 0.55f + 0.45f, alpha * 0.45f, 1f);
    }

    // ── 信号处理 ─────────────────────────────────────────────────
    private void OnEmergencyPressed()
    {
        GD.Print("[DroneStatusCard] 紧急返航指令已发出！");
        EmitSignal(SignalName.EmergencyReturnPressed);

        // 按下反馈：短暂全红闪
        var tw = CreateTween();
        tw.TweenProperty(_btnEmergency, "modulate",
            new Color(1f, 0.2f, 0.2f, 1f), 0.08f);
        tw.TweenProperty(_btnEmergency, "modulate",
            new Color(1f, 1f, 1f, 1f), 0.25f);
    }

    // ── 生命周期安全解绑（TC-03）────────────────────────────────
    public override void _ExitTree()
    {
        if (_btnEmergency != null)
            _btnEmergency.Pressed -= OnEmergencyPressed;

        _pulseTween?.Kill();
    }
}
