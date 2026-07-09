using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class MapStudioDock : VBoxContainer
{
    enum EntryKind
    {
        Spawn,
        Road,
        Water,
        Patch,
    }

    readonly record struct EntryRef(EntryKind Kind, int Index);

    const string MapStudioScenePath = "res://scenes/map/MapStudio.tscn";

    EditorPlugin plugin = null!;
    Label statusLabel = null!;
    ItemList entryList = null!;
    OptionButton kindPicker = null!;
    LineEdit mapNameEdit = null!;
    LineEdit descriptionEdit = null!;
    LineEdit labelEdit = null!;
    CheckBox isPlayerCheck = null!;
    SpinBox teamSpin = null!;
    SpinBox xSpin = null!;
    SpinBox zSpin = null!;
    SpinBox yawSpin = null!;
    SpinBox widthSpin = null!;
    SpinBox lengthSpin = null!;
    SpinBox paletteSpin = null!;
    Button addButton = null!;
    Button duplicateButton = null!;
    Button removeButton = null!;
    Button focusButton = null!;
    Button saveButton = null!;
    Button reloadButton = null!;

    readonly List<EntryRef> entries = new();
    bool suppressUi;
    string lastSnapshot = string.Empty;

    public void Initialize(EditorPlugin owner)
    {
        plugin = owner;
        Name = "RTS Map Studio";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(320f, 0f);
        BuildUi();
        RefreshFromScene();
    }

    public void RefreshFromScene(bool force = false)
    {
        var selected = TryGetSelectedEntry(out var currentSelection)
            ? currentSelection
            : (EntryRef?)null;
        var snapshot = BuildSnapshot();
        if (!force && snapshot == lastSnapshot)
            return;

        lastSnapshot = snapshot;
        RebuildEntryList(selected);
        UpdateHeaderFields();
        UpdateDetailFields();
    }

    void BuildUi()
    {
        var title = new Label
        {
            Text = "RTS Map Studio",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        AddChild(title);

        statusLabel = new Label
        {
            Text = "Open MapStudio to edit the authored map resource.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(0.78f, 0.82f, 0.88f)
        };
        AddChild(statusLabel);

        AddChild(BuildHeaderSection());
        AddChild(BuildToolbarSection());
        AddChild(BuildListSection());
        AddChild(BuildDetailsSection());
    }

    Control BuildHeaderSection()
    {
        var panel = CreateSectionPanel("Map");
        var body = panel.GetNode<VBoxContainer>("Margin/Body");

        mapNameEdit = new LineEdit
        {
            PlaceholderText = "Map name"
        };
        mapNameEdit.TextChanged += OnMapNameChanged;
        body.AddChild(CreateLabeledRow("Name", mapNameEdit));

        descriptionEdit = new LineEdit
        {
            PlaceholderText = "Short map description"
        };
        descriptionEdit.TextChanged += OnDescriptionChanged;
        body.AddChild(CreateLabeledRow("Desc", descriptionEdit));
        return panel;
    }

    Control BuildToolbarSection()
    {
        var panel = CreateSectionPanel("Actions");
        var body = panel.GetNode<VBoxContainer>("Margin/Body");

        var row = new HBoxContainer();
        body.AddChild(row);

        kindPicker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kindPicker.AddItem("Spawn");
        kindPicker.AddItem("Road");
        kindPicker.AddItem("Water");
        kindPicker.AddItem("Patch");
        row.AddChild(kindPicker);

        addButton = new Button { Text = "Add", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        addButton.Pressed += AddEntry;
        row.AddChild(addButton);

        var actionRow = new HBoxContainer();
        body.AddChild(actionRow);

        duplicateButton = new Button { Text = "Duplicate", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        duplicateButton.Pressed += DuplicateSelected;
        actionRow.AddChild(duplicateButton);

        removeButton = new Button { Text = "Remove", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        removeButton.Pressed += RemoveSelected;
        actionRow.AddChild(removeButton);

        focusButton = new Button { Text = "Focus", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        focusButton.Pressed += FocusSelected;
        actionRow.AddChild(focusButton);

        var saveRow = new HBoxContainer();
        body.AddChild(saveRow);

        saveButton = new Button { Text = "Save Map", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        saveButton.Pressed += SaveMap;
        saveRow.AddChild(saveButton);

        reloadButton = new Button { Text = "Reload", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reloadButton.Pressed += ReloadMap;
        saveRow.AddChild(reloadButton);

        return panel;
    }

    Control BuildListSection()
    {
        var panel = CreateSectionPanel("Entries");
        var body = panel.GetNode<VBoxContainer>("Margin/Body");

        entryList = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        entryList.ItemSelected += OnEntrySelected;
        entryList.CustomMinimumSize = new Vector2(0f, 260f);
        body.AddChild(entryList);
        return panel;
    }

    Control BuildDetailsSection()
    {
        var panel = CreateSectionPanel("Details");
        var body = panel.GetNode<VBoxContainer>("Margin/Body");

        labelEdit = new LineEdit { PlaceholderText = "Label" };
        labelEdit.TextChanged += OnLabelChanged;
        body.AddChild(CreateLabeledRow("Label", labelEdit));

        isPlayerCheck = new CheckBox { Text = "Player-owned spawn" };
        isPlayerCheck.Toggled += OnIsPlayerToggled;
        body.AddChild(isPlayerCheck);

        teamSpin = CreateSpin(0, 7, 1);
        teamSpin.ValueChanged += OnTeamChanged;
        body.AddChild(CreateLabeledRow("Team", teamSpin));

        xSpin = CreateSpin(-4096, 4096, 1);
        xSpin.ValueChanged += _ => OnPositionChanged();
        body.AddChild(CreateLabeledRow("X", xSpin));

        zSpin = CreateSpin(-4096, 4096, 1);
        zSpin.ValueChanged += _ => OnPositionChanged();
        body.AddChild(CreateLabeledRow("Z", zSpin));

        yawSpin = CreateSpin(-360, 360, 1);
        yawSpin.ValueChanged += OnYawChanged;
        body.AddChild(CreateLabeledRow("Yaw", yawSpin));

        widthSpin = CreateSpin(1, 4096, 1);
        widthSpin.ValueChanged += _ => OnSizeChanged();
        body.AddChild(CreateLabeledRow("Width", widthSpin));

        lengthSpin = CreateSpin(1, 4096, 1);
        lengthSpin.ValueChanged += _ => OnSizeChanged();
        body.AddChild(CreateLabeledRow("Length", lengthSpin));

        paletteSpin = CreateSpin(0, 3, 1);
        paletteSpin.ValueChanged += OnPaletteChanged;
        body.AddChild(CreateLabeledRow("Palette", paletteSpin));

        return panel;
    }

    PanelContainer CreateSectionPanel(string titleText)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var margin = new MarginContainer
        {
            Name = "Margin"
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var body = new VBoxContainer
        {
            Name = "Body",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        margin.AddChild(body);

        var title = new Label
        {
            Text = titleText
        };
        title.AddThemeFontSizeOverride("font_size", 15);
        body.AddChild(title);

        return panel;
    }

    Control CreateLabeledRow(string label, Control editor)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var caption = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(56f, 0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(caption);

        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(editor);
        return row;
    }

    static SpinBox CreateSpin(double min, double max, double step)
    {
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Rounded = false
        };
        spin.GetLineEdit().SelectAllOnFocus = true;
        return spin;
    }

    void RebuildEntryList(EntryRef? preferredSelection = null)
    {
        suppressUi = true;
        entryList.Clear();
        entries.Clear();

        if (TryGetMap(out var map, out _))
        {
            for (var i = 0; i < map.SpawnPoints.Count; i++)
                AddEntryItem(EntryKind.Spawn, i, map.SpawnPoints[i]?.Label ?? $"Spawn {i + 1}");
            for (var i = 0; i < map.Roads.Count; i++)
                AddEntryItem(EntryKind.Road, i, map.Roads[i]?.Label ?? $"Road {i + 1}");
            for (var i = 0; i < map.Waters.Count; i++)
                AddEntryItem(EntryKind.Water, i, map.Waters[i]?.Label ?? $"Water {i + 1}");
            for (var i = 0; i < map.Patches.Count; i++)
                AddEntryItem(EntryKind.Patch, i, map.Patches[i]?.Label ?? $"Patch {i + 1}");
        }

        if (preferredSelection is { } preferred && TrySelectEntry(preferred))
        {
            suppressUi = false;
            return;
        }

        if (entryList.ItemCount > 0)
            entryList.Select(0);

        suppressUi = false;
    }

    void AddEntryItem(EntryKind kind, int index, string label)
    {
        entries.Add(new EntryRef(kind, index));
        entryList.AddItem($"{KindPrefix(kind)} {label}");
    }

    void UpdateHeaderFields()
    {
        suppressUi = true;
        if (TryGetMap(out var map, out _))
        {
            mapNameEdit.Text = map.MapName;
            descriptionEdit.Text = map.Description;
            statusLabel.Text = "Editing the authored BattleMapResource used by MapStudio.";
        }
        else
        {
            mapNameEdit.Text = string.Empty;
            descriptionEdit.Text = string.Empty;
            statusLabel.Text = "Open res://scenes/map/MapStudio.tscn to begin editing.";
        }
        suppressUi = false;
    }

    void UpdateDetailFields()
    {
        suppressUi = true;

        var hasSelection = TryGetSelectedEntry(out var entry);
        var hasMap = TryGetMap(out var map, out _);
        var canEdit = hasSelection && hasMap;
        labelEdit.Editable = hasSelection;
        isPlayerCheck.Disabled = !canEdit;
        teamSpin.Editable = canEdit;
        xSpin.Editable = canEdit;
        zSpin.Editable = canEdit;
        yawSpin.Editable = canEdit;
        widthSpin.Editable = canEdit;
        lengthSpin.Editable = canEdit;
        paletteSpin.Editable = canEdit;
        duplicateButton.Disabled = !hasSelection;
        removeButton.Disabled = !hasSelection;
        focusButton.Disabled = !hasSelection;

        if (!canEdit)
        {
            labelEdit.Text = string.Empty;
            isPlayerCheck.ButtonPressed = false;
            teamSpin.Value = 0;
            xSpin.Value = 0;
            zSpin.Value = 0;
            yawSpin.Value = 0;
            widthSpin.Value = 1;
            lengthSpin.Value = 1;
            paletteSpin.Value = 0;
            suppressUi = false;
            return;
        }

        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                var spawn = map.SpawnPoints[entry.Index];
                if (spawn is not null)
                {
                    labelEdit.Text = spawn.Label;
                    isPlayerCheck.ButtonPressed = spawn.IsPlayer;
                    teamSpin.Value = spawn.TeamIndex;
                    xSpin.Value = spawn.Position.X;
                    zSpin.Value = spawn.Position.Z;
                    yawSpin.Value = spawn.Yaw;
                    widthSpin.Value = 1;
                    lengthSpin.Value = 1;
                    paletteSpin.Value = 0;
                }
                break;
            case EntryKind.Road:
                ApplyStripFields(map.Roads[entry.Index]);
                break;
            case EntryKind.Water:
                ApplyStripFields(map.Waters[entry.Index]);
                break;
            case EntryKind.Patch:
                var patch = map.Patches[entry.Index];
                if (patch is not null)
                {
                    labelEdit.Text = patch.Label;
                    xSpin.Value = patch.Center.X;
                    zSpin.Value = patch.Center.Z;
                    yawSpin.Value = patch.Angle;
                    widthSpin.Value = patch.Size.X;
                    lengthSpin.Value = patch.Size.Y;
                    paletteSpin.Value = patch.PaletteIndex;
                    isPlayerCheck.ButtonPressed = false;
                    teamSpin.Value = 0;
                }
                break;
        }

        isPlayerCheck.Visible = entry.Kind == EntryKind.Spawn;
        teamSpin.GetParent<Control>().Visible = entry.Kind == EntryKind.Spawn;
        widthSpin.GetParent<Control>().Visible = entry.Kind != EntryKind.Spawn;
        lengthSpin.GetParent<Control>().Visible = entry.Kind != EntryKind.Spawn;
        paletteSpin.GetParent<Control>().Visible = entry.Kind == EntryKind.Patch;

        suppressUi = false;
    }

    void ApplyStripFields(MapStripResource? strip)
    {
        if (strip is null)
            return;

        labelEdit.Text = strip.Label;
        xSpin.Value = strip.Center.X;
        zSpin.Value = strip.Center.Z;
        yawSpin.Value = strip.Angle;
        widthSpin.Value = strip.Size.X;
        lengthSpin.Value = strip.Size.Y;
        isPlayerCheck.ButtonPressed = false;
        teamSpin.Value = 0;
        paletteSpin.Value = 0;
    }

    void OnEntrySelected(long index)
    {
        if (suppressUi)
            return;

        UpdateDetailFields();
    }

    void OnMapNameChanged(string text)
    {
        if (suppressUi || !TryGetMap(out var map, out var renderer))
            return;

        map.MapName = string.IsNullOrWhiteSpace(text) ? "New Map" : text;
        MarkDirty(renderer);
    }

    void OnDescriptionChanged(string text)
    {
        if (suppressUi || !TryGetMap(out var map, out var renderer))
            return;

        map.Description = text;
        MarkDirty(renderer);
    }

    void OnLabelChanged(string text)
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                if (map.SpawnPoints[entry.Index] is { } spawn)
                    spawn.Label = FallbackLabel(text, entry.Kind, entry.Index);
                break;
            case EntryKind.Road:
                if (map.Roads[entry.Index] is { } road)
                    road.Label = FallbackLabel(text, entry.Kind, entry.Index);
                break;
            case EntryKind.Water:
                if (map.Waters[entry.Index] is { } water)
                    water.Label = FallbackLabel(text, entry.Kind, entry.Index);
                break;
            case EntryKind.Patch:
                if (map.Patches[entry.Index] is { } patch)
                    patch.Label = FallbackLabel(text, entry.Kind, entry.Index);
                break;
        }

        RebuildEntryList(entry);
        Reselect(entry);
        MarkDirty(renderer);
    }

    void OnIsPlayerToggled(bool toggledOn)
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || entry.Kind != EntryKind.Spawn || !TryGetMap(out var map, out var renderer))
            return;

        if (map.SpawnPoints[entry.Index] is { } spawn)
        {
            spawn.IsPlayer = toggledOn;
            MarkDirty(renderer);
        }
    }

    void OnTeamChanged(double value)
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || entry.Kind != EntryKind.Spawn || !TryGetMap(out var map, out var renderer))
            return;

        if (map.SpawnPoints[entry.Index] is { } spawn)
        {
            spawn.TeamIndex = Mathf.Clamp((int)Math.Round(value), 0, 7);
            MarkDirty(renderer);
        }
    }

    void OnPositionChanged()
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                if (map.SpawnPoints[entry.Index] is { } spawn)
                    spawn.Position = new Vector3((float)xSpin.Value, 0f, (float)zSpin.Value);
                break;
            case EntryKind.Road:
                if (map.Roads[entry.Index] is { } road)
                    road.Center = new Vector3((float)xSpin.Value, 0f, (float)zSpin.Value);
                break;
            case EntryKind.Water:
                if (map.Waters[entry.Index] is { } water)
                    water.Center = new Vector3((float)xSpin.Value, 0f, (float)zSpin.Value);
                break;
            case EntryKind.Patch:
                if (map.Patches[entry.Index] is { } patch)
                    patch.Center = new Vector3((float)xSpin.Value, 0f, (float)zSpin.Value);
                break;
        }

        MarkDirty(renderer);
    }

    void OnYawChanged(double value)
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        var yaw = (float)value;
        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                if (map.SpawnPoints[entry.Index] is { } spawn)
                    spawn.Yaw = yaw;
                break;
            case EntryKind.Road:
                if (map.Roads[entry.Index] is { } road)
                    road.Angle = yaw;
                break;
            case EntryKind.Water:
                if (map.Waters[entry.Index] is { } water)
                    water.Angle = yaw;
                break;
            case EntryKind.Patch:
                if (map.Patches[entry.Index] is { } patch)
                    patch.Angle = yaw;
                break;
        }

        MarkDirty(renderer);
    }

    void OnSizeChanged()
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        var size = new Vector2(Mathf.Max(1f, (float)widthSpin.Value), Mathf.Max(1f, (float)lengthSpin.Value));
        switch (entry.Kind)
        {
            case EntryKind.Road:
                if (map.Roads[entry.Index] is { } road)
                    road.Size = size;
                break;
            case EntryKind.Water:
                if (map.Waters[entry.Index] is { } water)
                    water.Size = size;
                break;
            case EntryKind.Patch:
                if (map.Patches[entry.Index] is { } patch)
                    patch.Size = size;
                break;
        }

        MarkDirty(renderer);
    }

    void OnPaletteChanged(double value)
    {
        if (suppressUi || !TryGetSelectedEntry(out var entry) || entry.Kind != EntryKind.Patch || !TryGetMap(out var map, out var renderer))
            return;

        if (map.Patches[entry.Index] is { } patch)
        {
            patch.PaletteIndex = Mathf.Clamp((int)Math.Round(value), 0, 3);
            MarkDirty(renderer);
        }
    }

    void AddEntry()
    {
        if (!TryGetMap(out var map, out var renderer))
        {
            statusLabel.Text = "Open MapStudio before adding entries.";
            return;
        }

        var kind = (EntryKind)kindPicker.Selected;
        EntryRef selected;
        switch (kind)
        {
            case EntryKind.Spawn:
                map.SpawnPoints.Add(new MapSpawnPointResource
                {
                    Label = $"Spawn{map.SpawnPoints.Count + 1}",
                    Position = Vector3.Zero,
                    Yaw = 0f,
                    IsPlayer = true,
                    TeamIndex = 0
                });
                selected = new EntryRef(kind, map.SpawnPoints.Count - 1);
                break;
            case EntryKind.Road:
                map.Roads.Add(new MapStripResource
                {
                    Label = $"Road{map.Roads.Count + 1}",
                    Center = Vector3.Zero,
                    Size = new Vector2(16f, 96f),
                    Angle = 0f
                });
                selected = new EntryRef(kind, map.Roads.Count - 1);
                break;
            case EntryKind.Water:
                map.Waters.Add(new MapStripResource
                {
                    Label = $"Water{map.Waters.Count + 1}",
                    Center = Vector3.Zero,
                    Size = new Vector2(18f, 96f),
                    Angle = 0f
                });
                selected = new EntryRef(kind, map.Waters.Count - 1);
                break;
            default:
                map.Patches.Add(new MapPatchResource
                {
                    Label = $"Patch{map.Patches.Count + 1}",
                    Center = Vector3.Zero,
                    Size = new Vector2(48f, 48f),
                    Angle = 0f,
                    PaletteIndex = 0
                });
                selected = new EntryRef(kind, map.Patches.Count - 1);
                break;
        }

        RebuildEntryList(selected);
        Reselect(selected);
        MarkDirty(renderer);
        statusLabel.Text = $"Added {KindLabel(kind)} entry.";
    }

    void DuplicateSelected()
    {
        if (!TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        EntryRef selected;
        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                if (map.SpawnPoints[entry.Index] is not { } sourceSpawn)
                    return;
                map.SpawnPoints.Add(new MapSpawnPointResource
                {
                    Label = $"{sourceSpawn.Label}_Copy",
                    Position = sourceSpawn.Position + new Vector3(12f, 0f, 12f),
                    Yaw = sourceSpawn.Yaw,
                    IsPlayer = sourceSpawn.IsPlayer,
                    TeamIndex = sourceSpawn.TeamIndex
                });
                selected = new EntryRef(entry.Kind, map.SpawnPoints.Count - 1);
                break;
            case EntryKind.Road:
                if (map.Roads[entry.Index] is not { } sourceRoad)
                    return;
                map.Roads.Add(new MapStripResource
                {
                    Label = $"{sourceRoad.Label}_Copy",
                    Center = sourceRoad.Center + new Vector3(12f, 0f, 12f),
                    Size = sourceRoad.Size,
                    Angle = sourceRoad.Angle
                });
                selected = new EntryRef(entry.Kind, map.Roads.Count - 1);
                break;
            case EntryKind.Water:
                if (map.Waters[entry.Index] is not { } sourceWater)
                    return;
                map.Waters.Add(new MapStripResource
                {
                    Label = $"{sourceWater.Label}_Copy",
                    Center = sourceWater.Center + new Vector3(12f, 0f, 12f),
                    Size = sourceWater.Size,
                    Angle = sourceWater.Angle
                });
                selected = new EntryRef(entry.Kind, map.Waters.Count - 1);
                break;
            default:
                if (map.Patches[entry.Index] is not { } sourcePatch)
                    return;
                map.Patches.Add(new MapPatchResource
                {
                    Label = $"{sourcePatch.Label}_Copy",
                    Center = sourcePatch.Center + new Vector3(12f, 0f, 12f),
                    Size = sourcePatch.Size,
                    Angle = sourcePatch.Angle,
                    PaletteIndex = sourcePatch.PaletteIndex
                });
                selected = new EntryRef(entry.Kind, map.Patches.Count - 1);
                break;
        }

        RebuildEntryList(selected);
        Reselect(selected);
        MarkDirty(renderer);
        statusLabel.Text = $"Duplicated {KindLabel(entry.Kind)} entry.";
    }

    void RemoveSelected()
    {
        if (!TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                map.SpawnPoints.RemoveAt(entry.Index);
                break;
            case EntryKind.Road:
                map.Roads.RemoveAt(entry.Index);
                break;
            case EntryKind.Water:
                map.Waters.RemoveAt(entry.Index);
                break;
            case EntryKind.Patch:
                map.Patches.RemoveAt(entry.Index);
                break;
        }

        RebuildEntryList();
        UpdateDetailFields();
        MarkDirty(renderer);
        statusLabel.Text = $"Removed {KindLabel(entry.Kind)} entry.";
    }

    void FocusSelected()
    {
        if (!TryGetSelectedEntry(out var entry) || !TryGetMap(out var map, out var renderer))
            return;

        var target = Vector3.Zero;
        switch (entry.Kind)
        {
            case EntryKind.Spawn:
                target = map.SpawnPoints[entry.Index]?.Position ?? Vector3.Zero;
                break;
            case EntryKind.Road:
                target = map.Roads[entry.Index]?.Center ?? Vector3.Zero;
                break;
            case EntryKind.Water:
                target = map.Waters[entry.Index]?.Center ?? Vector3.Zero;
                break;
            case EntryKind.Patch:
                target = map.Patches[entry.Index]?.Center ?? Vector3.Zero;
                break;
        }

        if (!string.IsNullOrEmpty(renderer.CameraPath.ToString()) && renderer.GetNodeOrNull<Node3D>(renderer.CameraPath) is { } camera)
        {
            camera.GlobalPosition = new Vector3(target.X, Mathf.Max(camera.GlobalPosition.Y, 72f), target.Z + 64f);
        }

        statusLabel.Text = $"Focused {KindLabel(entry.Kind)} near ({target.X:0}, {target.Z:0}).";
    }

    void SaveMap()
    {
        if (!TryGetMap(out var map, out var renderer))
            return;

        var resourcePath = renderer.AuthoredMapResourcePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            statusLabel.Text = "This map resource has no path yet.";
            return;
        }

        map.TakeOverPath(resourcePath);
        var result = ResourceSaver.Save(map, resourcePath);
        if (result == Error.Ok)
        {
            EditorInterface.Singleton.MarkSceneAsUnsaved();
            renderer.RefreshMap();
            RefreshFromScene(true);
            statusLabel.Text = $"Saved {resourcePath}.";
        }
        else
        {
            statusLabel.Text = $"Save failed: {result}.";
        }
    }

    void ReloadMap()
    {
        if (!TryGetRenderer(out var renderer))
        {
            statusLabel.Text = "MapStudio scene is not open.";
            return;
        }

        renderer.ReloadAuthoredMapResource();
        renderer.RefreshMap();
        RefreshFromScene(true);
        statusLabel.Text = "Reloaded current map resource into preview.";
    }

    bool TryGetMap(out BattleMapResource map, out BattleMapRenderer renderer)
    {
        map = null!;
        renderer = null!;
        if (!TryGetRenderer(out renderer) || !renderer.TryResolveAuthoredMapResource(out map))
            return false;

        return true;
    }

    bool TryGetRenderer(out BattleMapRenderer renderer)
    {
        renderer = null!;
        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        if (sceneRoot is null || sceneRoot.SceneFilePath != MapStudioScenePath)
            return false;

        renderer = sceneRoot.GetNodeOrNull<BattleMapRenderer>("MapRenderer");
        return renderer is not null;
    }

    bool TryGetSelectedEntry(out EntryRef entry)
    {
        entry = default;
        var selected = entryList.GetSelectedItems();
        if (selected.Length == 0)
            return false;

        var index = selected[0];
        if (index < 0 || index >= entries.Count)
            return false;

        entry = entries[index];
        return true;
    }

    void Reselect(EntryRef target)
    {
        if (TrySelectEntry(target))
        {
            UpdateDetailFields();
            return;
        }

        UpdateDetailFields();
    }

    bool TrySelectEntry(EntryRef target)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Kind == target.Kind && entries[i].Index == target.Index)
            {
                entryList.Select(i);
                return true;
            }
        }

        return false;
    }

    void MarkDirty(BattleMapRenderer renderer)
    {
        lastSnapshot = string.Empty;
        renderer.RefreshMap();
        EditorInterface.Singleton.MarkSceneAsUnsaved();
        UpdateHeaderFields();
    }

    string BuildSnapshot()
    {
        var scenePath = EditorInterface.Singleton.GetEditedSceneRoot()?.SceneFilePath ?? "none";
        if (!TryGetMap(out var map, out var renderer))
            return $"scene:{scenePath}|nomap";

        var resourcePath = renderer.AuthoredMapResourcePath?.Trim() ?? map.ResourcePath;
        var snapshot = $"{scenePath}|{resourcePath}|{map.MapName}|{map.Description}|{map.SpawnPoints.Count}|{map.Roads.Count}|{map.Waters.Count}|{map.Patches.Count}";
        for (var i = 0; i < map.SpawnPoints.Count; i++)
        {
            var spawn = map.SpawnPoints[i];
            if (spawn is null)
                continue;
            snapshot += $"|S|{spawn.Label}|{spawn.Position.X:F1}|{spawn.Position.Z:F1}|{spawn.Yaw:F1}|{spawn.IsPlayer}|{spawn.TeamIndex}";
        }
        for (var i = 0; i < map.Roads.Count; i++)
        {
            var road = map.Roads[i];
            if (road is null)
                continue;
            snapshot += $"|R|{road.Label}|{road.Center.X:F1}|{road.Center.Z:F1}|{road.Size.X:F1}|{road.Size.Y:F1}|{road.Angle:F1}";
        }
        for (var i = 0; i < map.Waters.Count; i++)
        {
            var water = map.Waters[i];
            if (water is null)
                continue;
            snapshot += $"|W|{water.Label}|{water.Center.X:F1}|{water.Center.Z:F1}|{water.Size.X:F1}|{water.Size.Y:F1}|{water.Angle:F1}";
        }
        for (var i = 0; i < map.Patches.Count; i++)
        {
            var patch = map.Patches[i];
            if (patch is null)
                continue;
            snapshot += $"|P|{patch.Label}|{patch.Center.X:F1}|{patch.Center.Z:F1}|{patch.Size.X:F1}|{patch.Size.Y:F1}|{patch.Angle:F1}|{patch.PaletteIndex}";
        }

        return snapshot;
    }

    static string KindPrefix(EntryKind kind) => kind switch
    {
        EntryKind.Spawn => "[S]",
        EntryKind.Road => "[R]",
        EntryKind.Water => "[W]",
        EntryKind.Patch => "[P]",
        _ => "[?]"
    };

    static string KindLabel(EntryKind kind) => kind switch
    {
        EntryKind.Spawn => "spawn",
        EntryKind.Road => "road",
        EntryKind.Water => "water",
        EntryKind.Patch => "patch",
        _ => "entry"
    };

    static string FallbackLabel(string text, EntryKind kind, int index)
    {
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        return $"{KindLabel(kind)}_{index + 1}";
    }
}
