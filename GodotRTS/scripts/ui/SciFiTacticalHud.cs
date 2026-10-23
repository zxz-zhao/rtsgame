// ═══════════════════════════════════════════════════════════════════════════
// SciFiTacticalHud.cs — 现代科幻战术 HUD Presenter
// 职责：ViewModel 数据注入 / 强类型信号声明 / Tween 入场动效 / 生命周期安全
// 设计系统：MetalPalette (CyanAccent / Gold / Red / Green / DarkBg)
// 工程红线：TC-03 生命周期安全 / TC-07 零 GC 帧循环
// ═══════════════════════════════════════════════════════════════════════════
using Godot;
using GodotRTS.UI;

namespace GodotRTS.UI
{
    public partial class SciFiTacticalHud : Control
    {
        // ── 强类型自定义信号 ────────────────────────────────────────────────
        [Signal] public delegate void MoveCommandPressedEventHandler();
        [Signal] public delegate void PatrolCommandPressedEventHandler();
        [Signal] public delegate void HoldCommandPressedEventHandler();
        [Signal] public delegate void AttackCommandPressedEventHandler();
        [Signal] public delegate void AggressiveCommandPressedEventHandler();
        [Signal] public delegate void DefendCommandPressedEventHandler();
        [Signal] public delegate void RetreatCommandPressedEventHandler();
        [Signal] public delegate void Ability1PressedEventHandler();
        [Signal] public delegate void Ability2PressedEventHandler();
        [Signal] public delegate void Ability3PressedEventHandler();
        [Signal] public delegate void MinimapClickedEventHandler(Vector2 normalizedPos);

        // ── unique_name_in_owner 节点引用 ───────────────────────────────────
        private Label _energyValue;
        private Label _creditsValue;
        private Label _supplyValue;
        private Label _alertLabel;
        private Label _timerLabel;
        private Label _unitCountLabel;
        private Label _alertCenterLabel;
        private Label _uc1Name;
        private Label _uc2Name;
        private Label _uc3Name;
        private ProgressBar _uc1HpBar;
        private ProgressBar _uc2HpBar;
        private ProgressBar _uc3HpBar;
        private Button _btnMove;
        private Button _btnPatrol;
        private Button _btnHold;
        private Button _btnAttack;
        private Button _btnAggressive;
        private Button _btnDefend;
        private Button _btnRetreat;
        private Button _btnAbility1;
        private Button _btnAbility2;
        private Button _btnAbility3;
        private Control _alertOverlay;
        private Control _leftUnitPanel;
        private Control _topBar;
        private Control _bottomCommandBar;
        private Control _minimapPanel;

        // ── 内部状态（零 GC：不用 string 拼接，不用 LINQ）─────────────────
        private float _elapsedSeconds = 0f;
        private float _alertBlinkTimer = 0f;
        private bool _alertVisible = false;
        private bool _alertBlinkState = false;
        private int _battleMinutes = 12;
        private int _battleSeconds = 47;

        // ═══════════════════════════════════════════════════════════════════
        // _Ready — 节点绑定 + 样式注入 + 信号连接 + 入场 Tween
        // ═══════════════════════════════════════════════════════════════════
        public override void _Ready()
        {
            // 1. 绑定所有 unique 节点
            _energyValue      = GetNode<Label>("%EnergyValue");
            _creditsValue     = GetNode<Label>("%CreditsValue");
            _supplyValue      = GetNode<Label>("%SupplyValue");
            _alertLabel       = GetNode<Label>("%AlertLabel");
            _timerLabel       = GetNode<Label>("%TimerLabel");
            _unitCountLabel   = GetNode<Label>("%UnitCountLabel");
            _alertCenterLabel = GetNode<Label>("%AlertCenterLabel");
            _uc1Name          = GetNode<Label>("%UC1Name");
            _uc2Name          = GetNode<Label>("%UC2Name");
            _uc3Name          = GetNode<Label>("%UC3Name");
            _uc1HpBar         = GetNode<ProgressBar>("%UC1HpBar");
            _uc2HpBar         = GetNode<ProgressBar>("%UC2HpBar");
            _uc3HpBar         = GetNode<ProgressBar>("%UC3HpBar");
            _btnMove          = GetNode<Button>("%BtnMove");
            _btnPatrol        = GetNode<Button>("%BtnPatrol");
            _btnHold          = GetNode<Button>("%BtnHold");
            _btnAttack        = GetNode<Button>("%BtnAttack");
            _btnAggressive    = GetNode<Button>("%BtnAggressive");
            _btnDefend        = GetNode<Button>("%BtnDefend");
            _btnRetreat       = GetNode<Button>("%BtnRetreat");
            _btnAbility1      = GetNode<Button>("%BtnAbility1");
            _btnAbility2      = GetNode<Button>("%BtnAbility2");
            _btnAbility3      = GetNode<Button>("%BtnAbility3");
            _alertOverlay     = GetNode<Control>("AlertOverlay");
            _leftUnitPanel    = GetNode<Control>("LeftUnitPanel");
            _topBar           = GetNode<Control>("TopBar");
            _bottomCommandBar = GetNode<Control>("BottomCommandBar");
            _minimapPanel     = GetNode<Control>("MinimapPanel");

            // 2. 注入 MetalPalette 样式色彩
            ApplyMetalStyle();

            // 3. 连接按钮信号（TC-03：_ExitTree 中解绑）
            _btnMove.Pressed       += OnMovePressed;
            _btnPatrol.Pressed     += OnPatrolPressed;
            _btnHold.Pressed       += OnHoldPressed;
            _btnAttack.Pressed     += OnAttackPressed;
            _btnAggressive.Pressed += OnAggressivePressed;
            _btnDefend.Pressed     += OnDefendPressed;
            _btnRetreat.Pressed    += OnRetreatPressed;
            _btnAbility1.Pressed   += OnAbility1Pressed;
            _btnAbility2.Pressed   += OnAbility2Pressed;
            _btnAbility3.Pressed   += OnAbility3Pressed;

            // 4. 入场 Tween 动效（科幻滑入）
            PlayEntryTween();
        }

        // ═══════════════════════════════════════════════════════════════════
        // ApplyMetalStyle — 运行时注入 MetalPalette 颜色到关键控件
        // ═══════════════════════════════════════════════════════════════════
        private void ApplyMetalStyle()
        {
            // 顶栏标题色：CyanAccent
            _alertLabel.AddThemeColorOverride("font_color", MetalPalette.CyanAccent);
            _timerLabel.AddThemeColorOverride("font_color", MetalPalette.Gold);

            // 单位名称色
            _uc1Name.AddThemeColorOverride("font_color", MetalPalette.CyanAccent);
            _uc2Name.AddThemeColorOverride("font_color", MetalPalette.Gold);
            _uc3Name.AddThemeColorOverride("font_color", MetalPalette.Red);

            // 单位数量标签
            _unitCountLabel.AddThemeColorOverride("font_color", MetalPalette.Steel);

            // 告警中心文字
            _alertCenterLabel.AddThemeColorOverride("font_color", MetalPalette.Red);

            // HP 条进度条颜色覆盖（StyleBoxFlat）
            ApplyProgressBarColor(_uc1HpBar, MetalPalette.Green);
            ApplyProgressBarColor(_uc2HpBar, MetalPalette.Gold);
            ApplyProgressBarColor(_uc3HpBar, MetalPalette.Red);

            // 指令按钮样式
            ApplyCmdButtonStyle(_btnMove,       MetalPalette.CyanAccent);
            ApplyCmdButtonStyle(_btnPatrol,     MetalPalette.CyanAccent);
            ApplyCmdButtonStyle(_btnHold,       MetalPalette.Steel);
            ApplyCmdButtonStyle(_btnAttack,     MetalPalette.Red);
            ApplyCmdButtonStyle(_btnAggressive, MetalPalette.Red);
            ApplyCmdButtonStyle(_btnDefend,     MetalPalette.Green);
            ApplyCmdButtonStyle(_btnRetreat,    MetalPalette.Gold);
            ApplyCmdButtonStyle(_btnAbility1,   MetalPalette.Gold);
            ApplyCmdButtonStyle(_btnAbility2,   MetalPalette.CyanAccent);
            ApplyCmdButtonStyle(_btnAbility3,   MetalPalette.Steel);
        }

        private static void ApplyProgressBarColor(ProgressBar bar, Color fillColor)
        {
            var fill = new StyleBoxFlat();
            fill.BgColor = fillColor;
            bar.AddThemeStyleboxOverride("fill", fill);

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(fillColor.R, fillColor.G, fillColor.B, 0.15f);
            bar.AddThemeStyleboxOverride("background", bg);
        }

        private static void ApplyCmdButtonStyle(Button btn, Color accentColor)
        {
            // Normal
            var normal = new StyleBoxFlat();
            normal.BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.08f);
            normal.BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.55f);
            normal.BorderWidthBottom = 1;
            normal.BorderWidthTop    = 1;
            normal.BorderWidthLeft   = 1;
            normal.BorderWidthRight  = 1;
            normal.CornerRadiusTopLeft     = 3;
            normal.CornerRadiusTopRight    = 3;
            normal.CornerRadiusBottomLeft  = 3;
            normal.CornerRadiusBottomRight = 3;
            btn.AddThemeStyleboxOverride("normal", normal);

            // Hover
            var hover = new StyleBoxFlat();
            hover.BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.22f);
            hover.BorderColor = accentColor;
            hover.BorderWidthBottom = 2;
            hover.BorderWidthTop    = 2;
            hover.BorderWidthLeft   = 2;
            hover.BorderWidthRight  = 2;
            hover.CornerRadiusTopLeft     = 3;
            hover.CornerRadiusTopRight    = 3;
            hover.CornerRadiusBottomLeft  = 3;
            hover.CornerRadiusBottomRight = 3;
            btn.AddThemeStyleboxOverride("hover", hover);

            // Pressed
            var pressed = new StyleBoxFlat();
            pressed.BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.40f);
            pressed.BorderColor = accentColor;
            pressed.BorderWidthBottom = 2;
            pressed.BorderWidthTop    = 2;
            pressed.BorderWidthLeft   = 2;
            pressed.BorderWidthRight  = 2;
            pressed.CornerRadiusTopLeft     = 3;
            pressed.CornerRadiusTopRight    = 3;
            pressed.CornerRadiusBottomLeft  = 3;
            pressed.CornerRadiusBottomRight = 3;
            btn.AddThemeStyleboxOverride("pressed", pressed);

            btn.AddThemeColorOverride("font_color", accentColor);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
        }

        // ═══════════════════════════════════════════════════════════════════
        // PlayEntryTween — 科幻滑入动效（TopBar / Left / Bottom / Minimap）
        // ═══════════════════════════════════════════════════════════════════
        private void PlayEntryTween()
        {
            var topTarget = _topBar.Position;
            var leftTarget = _leftUnitPanel.Position;
            var bottomTarget = _bottomCommandBar.Position;
            var minimapTarget = _minimapPanel.Position;

            // TopBar 从上滑入
            _topBar.Position = topTarget + new Vector2(0f, -60f);
            _topBar.Modulate = new Color(1f, 1f, 1f, 0f);

            // LeftPanel 从左滑入
            _leftUnitPanel.Position = leftTarget + new Vector2(-260f, 0f);
            _leftUnitPanel.Modulate = new Color(1f, 1f, 1f, 0f);

            // BottomBar 从下滑入
            _bottomCommandBar.Position = bottomTarget + new Vector2(0f, 160f);
            _bottomCommandBar.Modulate = new Color(1f, 1f, 1f, 0f);

            // Minimap 从右滑入
            _minimapPanel.Position = minimapTarget + new Vector2(280f, 0f);
            _minimapPanel.Modulate = new Color(1f, 1f, 1f, 0f);

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.SetEase(Tween.EaseType.Out);
            tween.SetTrans(Tween.TransitionType.Cubic);

            // TopBar
            tween.TweenProperty(_topBar, "position", topTarget, 0.45f).SetDelay(0.05f);
            tween.TweenProperty(_topBar, "modulate", Colors.White, 0.35f).SetDelay(0.05f);

            // LeftPanel
            tween.TweenProperty(_leftUnitPanel, "position", leftTarget, 0.50f).SetDelay(0.15f);
            tween.TweenProperty(_leftUnitPanel, "modulate", Colors.White, 0.40f).SetDelay(0.15f);

            // BottomBar
            tween.TweenProperty(_bottomCommandBar, "position", bottomTarget, 0.50f).SetDelay(0.20f);
            tween.TweenProperty(_bottomCommandBar, "modulate", Colors.White, 0.40f).SetDelay(0.20f);

            // Minimap
            tween.TweenProperty(_minimapPanel, "position", minimapTarget, 0.50f).SetDelay(0.25f);
            tween.TweenProperty(_minimapPanel, "modulate", Colors.White, 0.40f).SetDelay(0.25f);
        }

        // ═══════════════════════════════════════════════════════════════════
        // _Process — 战斗计时器 + 告警闪烁（TC-07：零 GC，无 LINQ，无 new）
        // ═══════════════════════════════════════════════════════════════════
        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // 战斗计时器（每秒递增）
            _elapsedSeconds += dt;
            if (_elapsedSeconds >= 1.0f)
            {
                _elapsedSeconds -= 1.0f;
                _battleSeconds++;
                if (_battleSeconds >= 60)
                {
                    _battleSeconds = 0;
                    _battleMinutes++;
                }
                UpdateTimerLabel();
            }

            // 告警闪烁（0.6s 周期，仅在告警激活时运行）
            if (_alertVisible)
            {
                _alertBlinkTimer += dt;
                if (_alertBlinkTimer >= 0.6f)
                {
                    _alertBlinkTimer -= 0.6f;
                    _alertBlinkState = !_alertBlinkState;
                    _alertOverlay.Modulate = _alertBlinkState
                        ? new Color(1f, 1f, 1f, 1.0f)
                        : new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        // ── 零 GC 计时器文本更新（复用 GodotRTS 格式化约定）─────────────
        private void UpdateTimerLabel()
        {
            // 手动格式化避免 string.Format / interpolation GC
            int m = _battleMinutes;
            int s = _battleSeconds;
            char m0 = (char)('0' + m / 10);
            char m1 = (char)('0' + m % 10);
            char s0 = (char)('0' + s / 10);
            char s1 = (char)('0' + s % 10);
            _timerLabel.Text = $"  {m0}{m1}:{s0}{s1}";
        }

        // ═══════════════════════════════════════════════════════════════════
        // 公开 ViewModel 注入 API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>注入资源数据到顶栏显示</summary>
        public void SetResources(int energy, int credits, int supplyUsed, int supplyMax)
        {
            _energyValue.Text  = energy.ToString("N0");
            _creditsValue.Text = credits.ToString("N0");
            _supplyValue.Text  = $"{supplyUsed} / {supplyMax}";
        }

        /// <summary>注入单位卡片数据</summary>
        public void SetUnitCard(int slot, string unitName, float hpPercent)
        {
            switch (slot)
            {
                case 0:
                    _uc1Name.Text    = unitName;
                    _uc1HpBar.Value  = hpPercent * 100f;
                    break;
                case 1:
                    _uc2Name.Text    = unitName;
                    _uc2HpBar.Value  = hpPercent * 100f;
                    break;
                case 2:
                    _uc3Name.Text    = unitName;
                    _uc3HpBar.Value  = hpPercent * 100f;
                    break;
            }
        }

        /// <summary>设置额外选中单位数量</summary>
        public void SetExtraUnitCount(int count)
        {
            _unitCountLabel.Text = count > 0 ? $"  + {count} more units in selection" : "";
        }

        /// <summary>触发全屏告警叠加层（基地被攻击等）</summary>
        public void TriggerAlert(string message, bool show)
        {
            _alertVisible = show;
            _alertOverlay.Visible = show;
            if (show)
            {
                _alertCenterLabel.Text = message;
                _alertBlinkTimer = 0f;
                _alertBlinkState = true;
            }
        }

        /// <summary>设置技能冷却（按钮禁用）</summary>
        public void SetAbilityCooldown(int abilityIndex, bool onCooldown)
        {
            switch (abilityIndex)
            {
                case 0: _btnAbility1.Disabled = onCooldown; break;
                case 1: _btnAbility2.Disabled = onCooldown; break;
                case 2: _btnAbility3.Disabled = onCooldown; break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // 按钮信号处理器 → 转发强类型信号
        // ═══════════════════════════════════════════════════════════════════
        private void OnMovePressed()       => EmitSignal(SignalName.MoveCommandPressed);
        private void OnPatrolPressed()     => EmitSignal(SignalName.PatrolCommandPressed);
        private void OnHoldPressed()       => EmitSignal(SignalName.HoldCommandPressed);
        private void OnAttackPressed()     => EmitSignal(SignalName.AttackCommandPressed);
        private void OnAggressivePressed() => EmitSignal(SignalName.AggressiveCommandPressed);
        private void OnDefendPressed()     => EmitSignal(SignalName.DefendCommandPressed);
        private void OnRetreatPressed()    => EmitSignal(SignalName.RetreatCommandPressed);
        private void OnAbility1Pressed()   => EmitSignal(SignalName.Ability1Pressed);
        private void OnAbility2Pressed()   => EmitSignal(SignalName.Ability2Pressed);
        private void OnAbility3Pressed()   => EmitSignal(SignalName.Ability3Pressed);

        // ═══════════════════════════════════════════════════════════════════
        // TC-03：_ExitTree 完整解绑所有信号，杜绝内存泄漏
        // ═══════════════════════════════════════════════════════════════════
        public override void _ExitTree()
        {
            if (_btnMove       != null) _btnMove.Pressed       -= OnMovePressed;
            if (_btnPatrol     != null) _btnPatrol.Pressed     -= OnPatrolPressed;
            if (_btnHold       != null) _btnHold.Pressed       -= OnHoldPressed;
            if (_btnAttack     != null) _btnAttack.Pressed     -= OnAttackPressed;
            if (_btnAggressive != null) _btnAggressive.Pressed -= OnAggressivePressed;
            if (_btnDefend     != null) _btnDefend.Pressed     -= OnDefendPressed;
            if (_btnRetreat    != null) _btnRetreat.Pressed    -= OnRetreatPressed;
            if (_btnAbility1   != null) _btnAbility1.Pressed   -= OnAbility1Pressed;
            if (_btnAbility2   != null) _btnAbility2.Pressed   -= OnAbility2Pressed;
            if (_btnAbility3   != null) _btnAbility3.Pressed   -= OnAbility3Pressed;
        }
    }
}
