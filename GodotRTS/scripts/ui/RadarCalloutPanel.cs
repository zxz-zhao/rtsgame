using Godot;

public partial class RadarCalloutPanel : Control
{
    [Signal] public delegate void RadarConfirmedEventHandler();
    [Signal] public delegate void RadarCancelledEventHandler();

    private Button _confirmButton;
    private Button _cancelButton;

    public override void _Ready()
    {
        _confirmButton = GetNode<Button>("MarginContainer/PanelContainer/VBoxContainer/HBoxContainer/ConfirmButton");
        _cancelButton = GetNode<Button>("MarginContainer/PanelContainer/VBoxContainer/HBoxContainer/CancelButton");

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }
        if (_cancelButton != null)
        {
            _cancelButton.Pressed += OnCancelPressed;
        }

        // 入场动效
        Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1.0f, 0.3f);
    }

    private void OnConfirmPressed()
    {
        EmitSignal(SignalName.RadarConfirmed);
        QueueFree();
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.RadarCancelled);
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (_confirmButton != null)
        { 
            _confirmButton.Pressed -= OnConfirmPressed;
        }
        if (_cancelButton != null)
        {
            _cancelButton.Pressed -= OnCancelPressed;
        }
    }
}
