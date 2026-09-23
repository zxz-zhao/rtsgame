using Godot;
using System;

namespace GodotRTS.Scripts.UI
{
    /// <summary>
    /// RTS 战斗胜利结算弹窗控制器 (VictoryDialog Presenter)
    /// 架构原则：
    /// 1. 严格分离视图与逻辑：视图布局与样式全部归属于 .tscn 声明式场景。
    /// 2. 动效驱动：内置 Tween 弹窗入场缓动与战绩数值滚动计数器。
    /// 3. 强类型解耦：通过 [Signal] 向外部上层管理器分发用户决策，不持有任何外部硬引用。
    /// </summary>
    public partial class VictoryDialog : Control
    {
        [Signal]
        public delegate void ReplayPressedEventHandler();

        [Signal]
        public delegate void MainMenuPressedEventHandler();

        // 场景节点绑定
        [Export] private Control _dialogCard;
        [Export] private ColorRect _dimmer;
        [Export] private Label _titleLabel;
        [Export] private Label _statsKillLabel;
        [Export] private Label _statsGoldLabel;
        [Export] private Label _gradeLabel;
        [Export] private Button _replayButton;
        [Export] private Button _mainMenuButton;

        // 动画数值暂存
        private int _targetKills = 48;
        private int _targetGold = 3500;
        private float _displayKills = 0f;
        private float _displayGold = 0f;

        public override void _Ready()
        {
            BindNodes();
            ConnectEvents();
            PlayEntranceAnimation();
        }

        private void BindNodes()
        {
            _dialogCard ??= GetNodeOrNull<Control>("%DialogCard");
            _dimmer ??= GetNodeOrNull<ColorRect>("%Dimmer");
            _titleLabel ??= GetNodeOrNull<Label>("%TitleLabel");
            _statsKillLabel ??= GetNodeOrNull<Label>("%StatsKillLabel");
            _statsGoldLabel ??= GetNodeOrNull<Label>("%StatsGoldLabel");
            _gradeLabel ??= GetNodeOrNull<Label>("%GradeLabel");
            _replayButton ??= GetNodeOrNull<Button>("%ReplayButton");
            _mainMenuButton ??= GetNodeOrNull<Button>("%MainMenuButton");
        }

        private void ConnectEvents()
        {
            if (_replayButton != null)
                _replayButton.Pressed += OnReplayClicked;
            if (_mainMenuButton != null)
                _mainMenuButton.Pressed += OnMainMenuClicked;
        }

        /// <summary>
        /// 外部数据注入接口（供 BattleManager / GameOverHandler 调用）
        /// </summary>
        public void SetStats(int totalKills, int totalGold, string grade = "RANK - S 卓越")
        {
            _targetKills = totalKills;
            _targetGold = totalGold;
            if (_gradeLabel != null)
                _gradeLabel.Text = grade;

            AnimateNumbers();
        }

        /// <summary>
        /// 弹性缩放弹窗入场动效
        /// </summary>
        private void PlayEntranceAnimation()
        {
            if (_dimmer != null)
            {
                var dimmerColor = _dimmer.Color;
                _dimmer.Color = new Color(dimmerColor.R, dimmerColor.G, dimmerColor.B, 0f);
                var dimmerTween = CreateTween();
                dimmerTween.TweenProperty(_dimmer, "color:a", dimmerColor.A, 0.35f);
            }

            if (_dialogCard != null)
            {
                _dialogCard.Scale = new Vector2(0.8f, 0.8f);
                _dialogCard.Modulate = new Color(1f, 1f, 1f, 0f);

                var cardTween = CreateTween();
                cardTween.SetParallel(true);
                cardTween.TweenProperty(_dialogCard, "scale", Vector2.One, 0.45f)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.Out);
                cardTween.TweenProperty(_dialogCard, "modulate:a", 1.0f, 0.25f);
            }

            AnimateNumbers();
        }

        /// <summary>
        /// 数值平滑滚动展现，模拟街机/RTS战果结算爽感
        /// </summary>
        private void AnimateNumbers()
        {
            _displayKills = 0f;
            _displayGold = 0f;

            var numTween = CreateTween();
            numTween.SetParallel(true);
            numTween.TweenMethod(Callable.From<float>(v =>
            {
                _displayKills = v;
                if (_statsKillLabel != null)
                    _statsKillLabel.Text = $"{(int)v} 目标";
            }), 0f, (float)_targetKills, 0.6f).SetEase(Tween.EaseType.Out);

            numTween.TweenMethod(Callable.From<float>(v =>
            {
                _displayGold = v;
                if (_statsGoldLabel != null)
                    _statsGoldLabel.Text = $"+{(int)v:N0}";
            }), 0f, (float)_targetGold, 0.8f).SetEase(Tween.EaseType.Out);
        }

        private void OnReplayClicked()
        {
            GD.Print("[VictoryDialog] 用户选择重新挑战对局。");
            EmitSignal(SignalName.ReplayPressed);
        }

        private void OnMainMenuClicked()
        {
            GD.Print("[VictoryDialog] 用户选择返回指挥中心。");
            EmitSignal(SignalName.MainMenuPressed);
        }

        public override void _ExitTree()
        {
            if (_replayButton != null)
                _replayButton.Pressed -= OnReplayClicked;
            if (_mainMenuButton != null)
                _mainMenuButton.Pressed -= OnMainMenuClicked;
        }
    }
}
