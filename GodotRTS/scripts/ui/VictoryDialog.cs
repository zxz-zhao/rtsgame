using Godot;
using System;

namespace GodotRTS.Scripts.UI
{
    public partial class VictoryDialog : Control
    {
        [Signal]
        public delegate void ReplayPressedEventHandler();

        [Signal]
        public delegate void MainMenuPressedEventHandler();

        private Label _titleLabel;
        private Label _statsKillLabel;
        private Label _statsGoldLabel;
        private Button _replayButton;
        private Button _menuButton;

        public override void _Ready()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;

            // 背景半透明遮罩
            var bgPanel = new Panel();
            bgPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            var bgStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.08f, 0.15f, 0.85f)
            };
            bgPanel.AddThemeStyleboxOverride("panel", bgStyle);
            AddChild(bgPanel);

            // 中心弹窗容器
            var centerContainer = new CenterContainer();
            centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(centerContainer);

            // 主面板卡片
            var mainPanel = new PanelContainer();
            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.12f, 0.22f, 0.95f),
                BorderColor = new Color(0f, 0.8f, 1f, 0.8f),
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12,
                ContentMarginLeft = 40,
                ContentMarginRight = 40,
                ContentMarginTop = 30,
                ContentMarginBottom = 30
            };
            mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
            centerContainer.AddChild(mainPanel);

            // 垂直布局
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 20);
            mainPanel.AddChild(vbox);

            // 标题
            _titleLabel = new Label();
            _titleLabel.Text = "⚡ 战斗胜利 (VICTORY) ⚡";
            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _titleLabel.AddThemeFontSizeOverride("font_size", 36);
            _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
            vbox.AddChild(_titleLabel);

            // 分割线
            var hSeparator = new HSeparator();
            vbox.AddChild(hSeparator);

            // 数据统计区域
            var statsVBox = new VBoxContainer();
            statsVBox.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(statsVBox);

            _statsKillLabel = new Label();
            _statsKillLabel.Text = "⚔ 敌军歼灭数: 48";
            _statsKillLabel.AddThemeFontSizeOverride("font_size", 18);
            _statsKillLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 1f));
            statsVBox.AddChild(_statsKillLabel);

            _statsGoldLabel = new Label();
            _statsGoldLabel.Text = "💰 战利品奖励: +3,500 金币";
            _statsGoldLabel.AddThemeFontSizeOverride("font_size", 18);
            _statsGoldLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.3f));
            statsVBox.AddChild(_statsGoldLabel);

            // 按钮水平布局
            var hbox = new HBoxContainer();
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            hbox.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(hbox);

            // 重玩按钮
            _replayButton = new Button();
            _replayButton.Text = "重新挑战";
            _replayButton.CustomMinimumSize = new Vector2(140, 45);
            _replayButton.Pressed += OnReplayPressed;
            StyleButtonStyle(_replayButton, new Color(0.1f, 0.5f, 0.9f), new Color(0.2f, 0.7f, 1f));
            hbox.AddChild(_replayButton);

            // 返回主菜单按钮
            _menuButton = new Button();
            _menuButton.Text = "返回大厅";
            _menuButton.CustomMinimumSize = new Vector2(140, 45);
            _menuButton.Pressed += OnMenuPressed;
            StyleButtonStyle(_menuButton, new Color(0.3f, 0.35f, 0.45f), new Color(0.45f, 0.52f, 0.65f));
            hbox.AddChild(_menuButton);
        }

        private void StyleButtonStyle(Button btn, Color normalColor, Color hoverColor)
        {
            var normalStyle = new StyleBoxFlat
            {
                BgColor = normalColor,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6
            };
            var hoverStyle = new StyleBoxFlat
            {
                BgColor = hoverColor,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6
            };
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
            btn.AddThemeColorOverride("font_color", Colors.White);
            btn.AddThemeFontSizeOverride("font_size", 16);
        }

        public void SetStats(int kills, int gold)
        {
            if (_statsKillLabel != null)
                _statsKillLabel.Text = $"⚔ 敌军歼灭数: {kills}";
            if (_statsGoldLabel != null)
                _statsGoldLabel.Text = $"💰 战利品奖励: +{gold:N0} 金币";
        }

        private void OnReplayPressed()
        {
            EmitSignal(SignalName.ReplayPressed);
        }

        private void OnMenuPressed()
        {
            EmitSignal(SignalName.MainMenuPressed);
        }
    }
}
