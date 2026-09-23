using Godot;

namespace GodotRTS.Scripts.UI
{
    public partial class RadarReconController : Control
    {
        [Signal]
        public delegate void RadarCalledEventHandler();

        [Signal]
        public delegate void ClosedEventHandler();

        private Button _callButton;
        private Button _closeButton;
        private Label _statusLabel;
        private Tween _entryTween;

        public override void _Ready()
        {
            _callButton = GetNodeOrNull<Button>("PanelContainer/MarginContainer/VBoxContainer/HBoxContainer/CallButton");
            _closeButton = GetNodeOrNull<Button>("PanelContainer/MarginContainer/VBoxContainer/HBoxContainer/CloseButton");
            _statusLabel = GetNodeOrNull<Label>("PanelContainer/MarginContainer/VBoxContainer/StatusLabel");

            if (_callButton != null)
            {
                _callButton.Pressed += OnCallButtonPressed;
            }

            if (_closeButton != null)
            {
                _closeButton.Pressed += OnCloseButtonPressed;
            }

            // 添加入场动效
            var panel = GetNodeOrNull<Control>("PanelContainer");
            if (panel != null)
            {
                panel.Modulate = new Color(1, 1, 1, 0);
                panel.Scale = new Vector2(0.9f, 0.9f);
                _entryTween = CreateTween().SetParallel(true);
                _entryTween.TweenProperty(panel, "modulate:a", 1.0f, 0.3f);
                _entryTween.TweenProperty(panel, "scale", Vector2.One, 0.3f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            }
        }

        private void OnCallButtonPressed()
        {
            GD.Print("[RadarReconController] Radar reconnaissance called by player!");
            if (_statusLabel != null)
            {
                _statusLabel.Text = "状态：侦察中... (剩余 30s)";
            }
            EmitSignal(SignalName.RadarCalled);
        }

        private void OnCloseButtonPressed()
        {
            GD.Print("[RadarReconController] Dialog closed.");
            EmitSignal(SignalName.Closed);
            QueueFree();
        }

        public override void _ExitTree()
        {
            if (_callButton != null)
            {
                _callButton.Pressed -= OnCallButtonPressed;
            }
            if (_closeButton != null)
            {
                _closeButton.Pressed -= OnCloseButtonPressed;
            }
            _entryTween?.Kill();
            base._ExitTree();
        }
    }
}
