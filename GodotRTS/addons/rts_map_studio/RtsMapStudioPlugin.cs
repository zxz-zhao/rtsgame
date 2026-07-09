using Godot;

[Tool]
public partial class RtsMapStudioPlugin : EditorPlugin
{
    Button? openStudioButton;
    EditorDock? mapStudioEditorDock;
    MapStudioDock? mapStudioDock;

    public override void _EnterTree()
    {
        openStudioButton = new Button
        {
            Text = "Map Studio",
            TooltipText = "Open the editable RTS map authoring scene."
        };
        openStudioButton.Pressed += OpenStudio;
        AddControlToContainer(CustomControlContainer.Toolbar, openStudioButton);

        mapStudioDock = new MapStudioDock();
        mapStudioDock.Initialize(this);
        mapStudioEditorDock = new EditorDock
        {
            Name = "RTSMapStudioDock",
            Title = "RTS Map Studio",
            LayoutKey = "rts_map_studio",
            DefaultSlot = EditorDock.DockSlot.RightBl,
            Closable = false,
            IconName = "Node3D"
        };
        mapStudioEditorDock.AddChild(mapStudioDock);
        AddDock(mapStudioEditorDock);
        mapStudioEditorDock.Open();
        mapStudioEditorDock.MakeVisible();

        SceneChanged += OnSceneChanged;
        SceneSaved += OnSceneSaved;
    }

    public override void _ExitTree()
    {
        SceneChanged -= OnSceneChanged;
        SceneSaved -= OnSceneSaved;

        if (mapStudioEditorDock is not null)
        {
            RemoveDock(mapStudioEditorDock);
            mapStudioEditorDock.QueueFree();
            mapStudioEditorDock = null;
            mapStudioDock = null;
        }

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
        mapStudioDock?.RefreshFromScene(true);
    }

    void OnSceneChanged(Node sceneRoot)
    {
        mapStudioDock?.RefreshFromScene(true);
    }

    void OnSceneSaved(string path)
    {
        mapStudioDock?.RefreshFromScene(true);
    }
}
