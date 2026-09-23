using Godot;

namespace GodotRTS.Scripts.UI
{
    public partial class AntiAirTurretPanelController : Control
    {
        [Signal] public delegate void OverchargePressedEventHandler();
        [Signal] public delegate void EmergencyFirePressedEventHandler();
        [Signal] public delegate void PanelClosedEventHandler();

        private Button _closeButton;
        private Button _btnOvercharge;
        private Button _btnEmergencyFire;
        private Label _valIntegrity;
        private Label _valAmmo;
        private Label _valRadar;

        public override void _Ready()
        {
            _closeButton = GetNode<Button>("%CloseButton");
            _btnOvercharge = GetNode<Button>("%BtnOvercharge");
            _btnEmergencyFire = GetNode<Button>("%BtnEmergencyFire");
            _valIntegrity = GetNode<Label>("%ValIntegrity");
            _valAmmo = GetNode<Label>("%ValAmmo");
            _valRadar = GetNode<Label>("%ValRadar");

            if (_closeButton != null)
                _closeButton.Pressed += OnClosePressed;
            
            if (_btnOvercharge != null)
                _btnOvercharge.Pressed += OnOverchargePressed;

            if (_btnEmergencyFire != null)
                _btnEmergencyFire.Pressed += OnEmergencyFirePressed;

            // 入场缩放/透明度动效
            Scale = new Vector2(0.8f, 0.8f);
            Modulate = new Color(1, 1, 1, 0);
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "scale", Vector2.One, 0.3f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(this, "modulate:a", 1.0f, 0.25f);
        }

        private void OnClosePressed()
        {
            EmitSignal(SignalName.PanelClosed);
            QueueFree();
        }

        private void OnOverchargePressed()
        {
            EmitSignal(SignalName.OverchargePressed);
            _valRadar.Text = "24.8 GHz (OVERCLOCKED)";
            _valRadar.Modulate = new Color(1.0f, 0.8f, 0.2f); // Gold highlight
            GD.Print("[AntiAirTurret] Radar overcharged!");
        }

        private void OnEmergencyFirePressed()
        {
            EmitSignal(SignalName.EmergencyFirePressed);
            _valAmmo.Text = "0 / 50 (DEPLETED)";
            _valAmmo.Modulate = new Color(1.0f, 0.3f, 0.3f); // Red alert
            GD.Print("[AntiAirTurret] Emergency full-area salvo fired!");
        }

        public override void _ExitTree()
        {
            if (_closeButton != null)
                _closeButton.Pressed -= OnClosePressed;
            if (_btnOvercharge != null)
                _btnOvercharge.Pressed -= OnOverchargePressed;
            if (_btnEmergencyFire != null)
                _btnEmergencyFire.Pressed -= OnEmergencyFirePressed;
        }
    }
}
