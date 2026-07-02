using Godot;

[Tool]
public partial class RtsMapStudioPlugin : EditorPlugin
{
    Button? openStudioButton;

    public override void _EnterTree()
    {
        openStudioButton = new Button
        {
            Text = "Map Studio",
            TooltipText = "Open the editable RTS map authoring scene."
        };
        openStudioButton.Pressed += OpenStudio;
        AddControlToContainer(CustomControlContainer.Toolbar, openStudioButton);
    }

    public override void _ExitTree()
    {
        if (openStudioButton is not null)
        {
            RemoveControlFromContainer(CustomControlContainer.Toolbar, openStudioButton);
            openStudioButton.QueueFree();
            openStudioButton = null;
        }
    }

    void OpenStudio()
    {
        EditorInterface.Singleton.OpenSceneFromPath("res://scenes/map/MapStudio.tscn");
    }
}
