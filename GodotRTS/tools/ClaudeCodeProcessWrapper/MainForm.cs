using System.Diagnostics;
using System.Windows.Forms;

internal sealed class MainForm : Form
{
    private readonly ClaudeCodeSettingsState _initialState;
    private readonly TextBox _settingsPathText = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _baseUrlText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _apiKeyText = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly CheckBox _showApiKeyCheck = new() { Text = "Show Key", AutoSize = true };
    private readonly TextBox _topLevelModelText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _apiModelText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _reasoningModelText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _defaultHaikuModelText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _defaultSonnetModelText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _defaultOpusModelText = new() { Dock = DockStyle.Fill };
    private readonly TextBox _otherEnvironmentText = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 320 };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true };

    public MainForm(ClaudeCodeSettingsState state)
    {
        _initialState = CloneState(state);

        Text = "Claude Code Settings Editor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 760);
        Size = new Size(1040, 860);

        _showApiKeyCheck.CheckedChanged += (_, _) => _apiKeyText.UseSystemPasswordChar = !_showApiKeyCheck.Checked;

        Controls.Add(BuildLayout());
        LoadFromState(_initialState);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            Text = "This program directly edits the Claude Code settings file used by the Claude Code plugin and CLI.",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
        }, 0, 0);

        panel.Controls.Add(new Label
        {
            Text = "After saving, reopen the Claude Code panel or Reload Window in VS Code to let the plugin pick up the changes.",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
        }, 0, 1);

        panel.Controls.Add(new Label
        {
            Text = "A backup copy of the previous settings file will be created automatically before each save.",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            ForeColor = Color.DarkGoldenrod,
        }, 0, 2);

        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
        };

        tabs.TabPages.Add(BuildMainTab());
        tabs.TabPages.Add(BuildAdvancedTab());
        return tabs;
    }

    private TabPage BuildMainTab()
    {
        var page = new TabPage("Claude Settings");
        var layout = CreateFormGrid();

        AddReadOnlyPathRow(layout, 0, "Settings Path", _settingsPathText, "Open Folder", OpenSettingsFolder);
        AddFieldRow(layout, 1, "ANTHROPIC_BASE_URL", _baseUrlText);
        AddFieldRow(layout, 2, "ANTHROPIC_API_KEY", _apiKeyText);
        AddFieldRow(layout, 3, "Show Key", _showApiKeyCheck);
        AddFieldRow(layout, 4, "model", WrapInPanel(_topLevelModelText, "Top-level Claude Code mode selector. Your current file uses values like sonnet here."));
        AddFieldRow(layout, 5, "ANTHROPIC_MODEL", _apiModelText);
        AddFieldRow(layout, 6, "ANTHROPIC_REASONING_MODEL", _reasoningModelText);
        AddFieldRow(layout, 7, "ANTHROPIC_DEFAULT_HAIKU_MODEL", _defaultHaikuModelText);
        AddFieldRow(layout, 8, "ANTHROPIC_DEFAULT_SONNET_MODEL", _defaultSonnetModelText);
        AddFieldRow(layout, 9, "ANTHROPIC_DEFAULT_OPUS_MODEL", _defaultOpusModelText);

        page.Controls.Add(WrapScrollable(layout));
        return page;
    }

    private TabPage BuildAdvancedTab()
    {
        var page = new TabPage("Other env");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "Other env entries under settings.json -> env. One per line, format NAME=VALUE. Known Claude fields above stay in the main tab; everything else can be edited here.",
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0, 0, 0, 8),
        }, 0, 0);
        layout.Controls.Add(_otherEnvironmentText, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private Control BuildFooter()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 12, 0, 0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel.Text = "Save changes here, then reopen Claude Code in VS Code.";
        _statusLabel.MaximumSize = new Size(680, 0);
        _statusLabel.Margin = new Padding(0, 6, 12, 0);
        layout.Controls.Add(_statusLabel, 0, 0);

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
        };

        var saveAndCloseButton = new Button { Text = "Save and Close", AutoSize = true };
        saveAndCloseButton.Click += (_, _) =>
        {
            if (SaveSettings())
            {
                Close();
            }
        };

        var saveButton = new Button { Text = "Save", AutoSize = true };
        saveButton.Click += (_, _) => SaveSettings();

        var reloadButton = new Button { Text = "Reload from file", AutoSize = true };
        reloadButton.Click += (_, _) => ReloadFromDisk();

        var openFolderButton = new Button { Text = "Open .claude", AutoSize = true };
        openFolderButton.Click += (_, _) => OpenSettingsFolder();

        var closeButton = new Button { Text = "Close", AutoSize = true };
        closeButton.Click += (_, _) => Close();

        buttonBar.Controls.Add(saveAndCloseButton);
        buttonBar.Controls.Add(saveButton);
        buttonBar.Controls.Add(reloadButton);
        buttonBar.Controls.Add(openFolderButton);
        buttonBar.Controls.Add(closeButton);

        layout.Controls.Add(buttonBar, 1, 0);
        return layout;
    }

    private void LoadFromState(ClaudeCodeSettingsState state)
    {
        _settingsPathText.Text = state.SettingsPath;
        _baseUrlText.Text = state.BaseUrl ?? string.Empty;
        _apiKeyText.Text = state.ApiKey ?? string.Empty;
        _topLevelModelText.Text = state.TopLevelModel ?? string.Empty;
        _apiModelText.Text = state.ApiModel ?? string.Empty;
        _reasoningModelText.Text = state.ReasoningModel ?? string.Empty;
        _defaultHaikuModelText.Text = state.DefaultHaikuModel ?? string.Empty;
        _defaultSonnetModelText.Text = state.DefaultSonnetModel ?? string.Empty;
        _defaultOpusModelText.Text = state.DefaultOpusModel ?? string.Empty;
        _otherEnvironmentText.Text = string.Join(
            Environment.NewLine,
            state.OtherEnvironment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private void ReloadFromDisk()
    {
        try
        {
            ClaudeCodeSettingsState state = ClaudeCodeUserSettingsStore.LoadUserSettings();
            LoadFromState(state);
            _statusLabel.Text = $"Reloaded {_settingsPathText.Text}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Reload failed: {ex.Message}";
            MessageBox.Show(
                $"Failed to reload Claude settings.\n\n{ex.Message}",
                "Claude Code Settings Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private bool SaveSettings()
    {
        try
        {
            ClaudeCodeSettingsState state = CollectState();
            ClaudeCodeUserSettingsStore.SaveUserSettings(state);
            _statusLabel.Text = $"Saved Claude settings to {state.SettingsPath}";
            MessageBox.Show(
                "Claude Code settings saved.\n\nReopen the Claude Code panel in VS Code, or run Reload Window, then reconnect.",
                "Claude Code Settings Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Save failed: {ex.Message}";
            MessageBox.Show(
                $"Failed to save Claude settings.\n\n{ex.Message}",
                "Claude Code Settings Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    private ClaudeCodeSettingsState CollectState()
    {
        return new ClaudeCodeSettingsState
        {
            SettingsPath = _settingsPathText.Text,
            Root = _initialState.Root.DeepClone() as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject(),
            BaseUrl = NullIfEmpty(_baseUrlText.Text),
            ApiKey = NullIfEmpty(_apiKeyText.Text),
            TopLevelModel = NullIfEmpty(_topLevelModelText.Text),
            ApiModel = NullIfEmpty(_apiModelText.Text),
            ReasoningModel = NullIfEmpty(_reasoningModelText.Text),
            DefaultHaikuModel = NullIfEmpty(_defaultHaikuModelText.Text),
            DefaultSonnetModel = NullIfEmpty(_defaultSonnetModelText.Text),
            DefaultOpusModel = NullIfEmpty(_defaultOpusModelText.Text),
            OtherEnvironment = ParseEnvironment(_otherEnvironmentText.Text),
        };
    }

    private void OpenSettingsFolder()
    {
        string? folder = Path.GetDirectoryName(_settingsPathText.Text);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folder,
            UseShellExecute = true,
        });
    }

    private static ClaudeCodeSettingsState CloneState(ClaudeCodeSettingsState state)
    {
        return new ClaudeCodeSettingsState
        {
            SettingsPath = state.SettingsPath,
            Root = state.Root.DeepClone() as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject(),
            BaseUrl = state.BaseUrl,
            ApiKey = state.ApiKey,
            TopLevelModel = state.TopLevelModel,
            ApiModel = state.ApiModel,
            ReasoningModel = state.ReasoningModel,
            DefaultHaikuModel = state.DefaultHaikuModel,
            DefaultSonnetModel = state.DefaultSonnetModel,
            DefaultOpusModel = state.DefaultOpusModel,
            OtherEnvironment = new Dictionary<string, string>(state.OtherEnvironment, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static Dictionary<string, string> ParseEnvironment(string rawText)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = rawText.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidOperationException($"Line {index + 1} must be NAME=VALUE.");
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..];
            if (key.Length == 0)
            {
                throw new InvalidOperationException($"Line {index + 1} is missing the variable name.");
            }

            environment[key] = value;
        }

        return environment;
    }

    private static TableLayoutPanel CreateFormGrid()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 0,
            Padding = new Padding(12),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        return layout;
    }

    private static Control WrapScrollable(Control content)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
        };
        panel.Controls.Add(content);
        return panel;
    }

    private static void AddFieldRow(TableLayoutPanel layout, int rowIndex, string labelText, Control control)
    {
        AddRow(layout, rowIndex, labelText, control, new Panel { Dock = DockStyle.Fill });
    }

    private static void AddReadOnlyPathRow(TableLayoutPanel layout, int rowIndex, string labelText, Control control, string buttonText, Action onClick)
    {
        var button = new Button { Text = buttonText, Dock = DockStyle.Fill };
        button.Click += (_, _) => onClick();
        AddRow(layout, rowIndex, labelText, control, button);
    }

    private static void AddRow(TableLayoutPanel layout, int rowIndex, string labelText, Control control, Control trailingControl)
    {
        while (layout.RowCount <= rowIndex)
        {
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 8, 12, 0),
        };

        control.Margin = new Padding(0, 4, 8, 4);
        trailingControl.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(label, 0, rowIndex);
        layout.Controls.Add(control, 1, rowIndex);
        layout.Controls.Add(trailingControl, 2, rowIndex);
    }

    private static Control WrapInPanel(Control mainControl, string hintText)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(mainControl, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = hintText,
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Margin = new Padding(0, 4, 0, 0),
        }, 0, 1);
        return layout;
    }

    private static string? NullIfEmpty(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
