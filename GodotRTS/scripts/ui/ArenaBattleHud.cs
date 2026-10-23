using Godot;
using System;

namespace GodotRTS.Scripts.UI
{
    public partial class ArenaBattleHud : CanvasLayer
    {
        [Signal] public delegate void SpawnRequestedEventHandler(string unitType, bool isBlue);
        [Signal] public delegate void WaveRequestedEventHandler(bool isBlue);
        [Signal] public delegate void ClashRequestedEventHandler();
        [Signal] public delegate void AutoWaveToggledEventHandler();
        [Signal] public delegate void SpeedToggledEventHandler();
        [Signal] public delegate void PauseToggledEventHandler();
        [Signal] public delegate void ClearRequestedEventHandler();
        [Signal] public delegate void CameraModeRequestedEventHandler();
        [Signal] public delegate void ExitRequestedEventHandler();

        // Top Telemetry
        private Label _lblBlueUnits = null!;
        private Label _lblBlueKills = null!;
        private Label _lblRedUnits = null!;
        private Label _lblRedKills = null!;
        private Label _lblClock = null!;
        private ProgressBar _tugOfWarBar = null!;
        private Label _lblAlertTicker = null!;

        // Tab Switcher & Grids
        private Button _btnTabBlue = null!;
        private Button _btnTabRed = null!;
        private Control _blueGrid = null!;
        private Control _redGrid = null!;
        private bool _isBlueTabActive = true;

        // Blue Action Buttons
        private Button _btnBlueFighter = null!;
        private Button _btnBlueTank = null!;
        private Button _btnBlueArtillery = null!;
        private Button _btnBlueShip = null!;
        private Button _btnBlueInfantry = null!;
        private Button _btnBlueWave = null!;

        // Red Action Buttons
        private Button _btnRedFighter = null!;
        private Button _btnRedTank = null!;
        private Button _btnRedArtillery = null!;
        private Button _btnRedShip = null!;
        private Button _btnRedInfantry = null!;
        private Button _btnRedWave = null!;

        // Bottom Operations & Clash
        private Button _btnAllOutClash = null!;
        private Button _btnAutoWave = null!;
        private Button _btnSpeed = null!;
        private Button _btnPause = null!;
        private Button _btnClear = null!;
        private Button _btnCamera = null!;
        private Button _btnExit = null!;

        // Mobile Drawer Toggle
        private Button? _btnToggleDeck;
        private Control _bottomCommandDeck = null!;
        private bool _isDeckExpanded = false;

        // Teammate Voice & Radio Communication
        private Label _lblVoiceStatus = null!;
        private Label _lblVoiceWave = null!;
        private Label _lblVoiceSpeaker = null!;
        private Button _btnVoiceMic = null!;
        private Button _btnQuickRadio = null!;
        private Control _quickRadioMenu = null!;
        private Button _btnRadioCall1 = null!;
        private Button _btnRadioCall2 = null!;
        private Button _btnRadioCall3 = null!;

        // Combat Log & Event Prompts
        private VBoxContainer _logContainer = null!;
        private float _radioChatterTimer = 3.5f;
        private int _waveAnimFrame = 0;
        private double _waveAnimTimer = 0.0;
        private bool _isMicTransmitting = false;
        private int _chatterIdx = 0;
        private readonly (string name, string callout)[] _chatterPool = new[]
        {
            ("Alpha 侦察先锋", "发现敌装甲主力推进，正在标定前线坐标！"),
            ("Bravo 铁甲先锋", "重型主战坦克已就绪，准备发起穿插破障！"),
            ("Eagle 战鹰编队", "已越过中线水道，空对空制空权掌握中！"),
            ("Ghost 潜艇中队", "北部水道巡航正常，战列舰主炮准备装填！"),
            ("Charlie 突击队", "步兵已进入掩体射击位，火力压制有效！")
        };

        public override void _Ready()
        {
            // Bind Telemetry
            _lblBlueUnits = GetNode<Label>("%LblBlueUnits");
            _lblBlueKills = GetNode<Label>("%LblBlueKills");
            _lblRedUnits = GetNode<Label>("%LblRedUnits");
            _lblRedKills = GetNode<Label>("%LblRedKills");
            _lblClock = GetNode<Label>("%LblClock");
            _tugOfWarBar = GetNode<ProgressBar>("%TugOfWarBar");
            _lblAlertTicker = GetNode<Label>("%LblAlertTicker");

            // Bind Tabs & Grids
            _btnTabBlue = GetNode<Button>("%BtnTabBlue");
            _btnTabRed = GetNode<Button>("%BtnTabRed");
            _blueGrid = GetNode<Control>("%BlueGrid");
            _redGrid = GetNode<Control>("%RedGrid");

            _btnTabBlue.Pressed += () => SwitchTab(true);
            _btnTabRed.Pressed += () => SwitchTab(false);

            // Bind Blue Buttons
            _btnBlueFighter = GetNode<Button>("%BtnBlueFighter");
            _btnBlueTank = GetNode<Button>("%BtnBlueTank");
            _btnBlueArtillery = GetNode<Button>("%BtnBlueArtillery");
            _btnBlueShip = GetNode<Button>("%BtnBlueShip");
            _btnBlueInfantry = GetNode<Button>("%BtnBlueInfantry");
            _btnBlueWave = GetNode<Button>("%BtnBlueWave");

            _btnBlueFighter.Pressed += () => EmitSignal(SignalName.SpawnRequested, "fighter", true);
            _btnBlueTank.Pressed += () => EmitSignal(SignalName.SpawnRequested, "heavy_tank", true);
            _btnBlueArtillery.Pressed += () => EmitSignal(SignalName.SpawnRequested, "artillery", true);
            _btnBlueShip.Pressed += () => EmitSignal(SignalName.SpawnRequested, "destroyer_ship", true);
            _btnBlueInfantry.Pressed += () => EmitSignal(SignalName.SpawnRequested, "infantry", true);
            _btnBlueWave.Pressed += () => EmitSignal(SignalName.WaveRequested, true);

            // Bind Red Buttons
            _btnRedFighter = GetNode<Button>("%BtnRedFighter");
            _btnRedTank = GetNode<Button>("%BtnRedTank");
            _btnRedArtillery = GetNode<Button>("%BtnRedArtillery");
            _btnRedShip = GetNode<Button>("%BtnRedShip");
            _btnRedInfantry = GetNode<Button>("%BtnRedInfantry");
            _btnRedWave = GetNode<Button>("%BtnRedWave");

            _btnRedFighter.Pressed += () => EmitSignal(SignalName.SpawnRequested, "fighter", false);
            _btnRedTank.Pressed += () => EmitSignal(SignalName.SpawnRequested, "heavy_tank", false);
            _btnRedArtillery.Pressed += () => EmitSignal(SignalName.SpawnRequested, "artillery", false);
            _btnRedShip.Pressed += () => EmitSignal(SignalName.SpawnRequested, "destroyer_ship", false);
            _btnRedInfantry.Pressed += () => EmitSignal(SignalName.SpawnRequested, "infantry", false);
            _btnRedWave.Pressed += () => EmitSignal(SignalName.WaveRequested, false);

            // Bind Command Deck
            _btnAllOutClash = GetNode<Button>("%BtnAllOutClash");
            _btnAutoWave = GetNode<Button>("%BtnAutoWave");
            _btnSpeed = GetNode<Button>("%BtnSpeed");
            _btnPause = GetNode<Button>("%BtnPause");
            _btnClear = GetNode<Button>("%BtnClear");
            _btnCamera = GetNode<Button>("%BtnCamera");
            _btnExit = GetNode<Button>("%BtnExit");

            _btnAllOutClash.Pressed += () => EmitSignal(SignalName.ClashRequested);
            _btnAutoWave.Pressed += () => EmitSignal(SignalName.AutoWaveToggled);
            _btnSpeed.Pressed += () => EmitSignal(SignalName.SpeedToggled);
            _btnPause.Pressed += () => EmitSignal(SignalName.PauseToggled);
            _btnClear.Pressed += () => EmitSignal(SignalName.ClearRequested);
            _btnCamera.Pressed += () => EmitSignal(SignalName.CameraModeRequested);
            _btnExit.Pressed += () => EmitSignal(SignalName.ExitRequested);

            // Bind Mobile Drawer
            _bottomCommandDeck = GetNode<Control>("%BottomCommandDeck");
            _btnToggleDeck = GetNodeOrNull<Button>("%BtnToggleDeck");
            var cmdArgs = CommandLineArgs.Get();
            if (_btnToggleDeck != null)
            {
                _btnToggleDeck.Pressed += ToggleDeck;
                bool startExpanded = System.Array.Exists(cmdArgs, a => a == "--hud-expanded");
                SetDeckExpanded(startExpanded, false);
            }

            // Bind Voice Comm & Combat Log
            _lblVoiceStatus = GetNodeOrNull<Label>("%LblVoiceStatus")!;
            _lblVoiceWave = GetNodeOrNull<Label>("%LblVoiceWave")!;
            _lblVoiceSpeaker = GetNodeOrNull<Label>("%LblVoiceSpeaker")!;
            _btnVoiceMic = GetNodeOrNull<Button>("%BtnVoiceMic")!;
            _btnQuickRadio = GetNodeOrNull<Button>("%BtnQuickRadio")!;
            _quickRadioMenu = GetNodeOrNull<Control>("%QuickRadioMenu")!;
            _btnRadioCall1 = GetNodeOrNull<Button>("%BtnRadioCall1")!;
            _btnRadioCall2 = GetNodeOrNull<Button>("%BtnRadioCall2")!;
            _btnRadioCall3 = GetNodeOrNull<Button>("%BtnRadioCall3")!;
            _logContainer = GetNodeOrNull<VBoxContainer>("%LogContainer")!;

            if (_btnVoiceMic != null)
                _btnVoiceMic.Pressed += ToggleMicTransmit;
            if (_btnQuickRadio != null && _quickRadioMenu != null)
                _btnQuickRadio.Pressed += () => _quickRadioMenu.Visible = !_quickRadioMenu.Visible;

            if (_btnRadioCall1 != null)
                _btnRadioCall1.Pressed += () => {
                    TriggerVoiceTransmission("我方指挥", "全体集中火力打击敌方核心装甲！", true);
                    if (_quickRadioMenu != null) _quickRadioMenu.Visible = false;
                };
            if (_btnRadioCall2 != null)
                _btnRadioCall2.Pressed += () => {
                    TriggerVoiceTransmission("我方指挥", "注意敌军侧翼迂回，立即回防掩护阵地！", true);
                    if (_quickRadioMenu != null) _quickRadioMenu.Visible = false;
                };
            if (_btnRadioCall3 != null)
                _btnRadioCall3.Pressed += () => {
                    TriggerVoiceTransmission("我方指挥", "请求空军战鹰实施俯冲轰炸与对地扫射！", true);
                    if (_quickRadioMenu != null) _quickRadioMenu.Visible = false;
                };

            AddCombatLog("📡", "战区加密无线电已连接，战术频道就绪", new Color(0.35f, 0.85f, 1f));

            if (System.Array.Exists(cmdArgs, a => a == "--radio-open") && _quickRadioMenu != null)
            {
                _quickRadioMenu.Visible = true;
            }

            // Fade Entrance Animation
            var topBar = GetNode<Control>("%TopTelemetryBar");
            topBar.Modulate = new Color(1, 1, 1, 0);
            var tween = CreateTween();
            tween.TweenProperty(topBar, "modulate:a", 1.0f, 0.35f);
        }

        public void ToggleDeck()
        {
            SetDeckExpanded(!_isDeckExpanded, true);
        }

        public void SetDeckExpanded(bool expanded, bool animate)
        {
            _isDeckExpanded = expanded;
            if (_btnToggleDeck != null)
            {
                _btnToggleDeck.Text = _isDeckExpanded ? "✕ 隐藏控制 ▾" : "⚔️ 战术指令 ▴";
            }
            if (_bottomCommandDeck != null)
            {
                if (animate)
                {
                    var tween = CreateTween();
                    if (_isDeckExpanded)
                    {
                        _bottomCommandDeck.Visible = true;
                        _bottomCommandDeck.Modulate = new Color(1, 1, 1, 0);
                        tween.TweenProperty(_bottomCommandDeck, "modulate:a", 1.0f, 0.2f);
                    }
                    else
                    {
                        tween.TweenProperty(_bottomCommandDeck, "modulate:a", 0.0f, 0.15f);
                        tween.Finished += () => { if (!_isDeckExpanded) _bottomCommandDeck.Visible = false; };
                    }
                }
                else
                {
                    _bottomCommandDeck.Visible = _isDeckExpanded;
                    _bottomCommandDeck.Modulate = new Color(1, 1, 1, _isDeckExpanded ? 1.0f : 0.0f);
                }
            }
        }

        private void SwitchTab(bool isBlue)
        {
            _isBlueTabActive = isBlue;
            _blueGrid.Visible = isBlue;
            _redGrid.Visible = !isBlue;

            _btnTabBlue.Modulate = isBlue ? new Color(1f, 1f, 1f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            _btnTabRed.Modulate = !isBlue ? new Color(1f, 1f, 1f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
        }

        public override void _UnhandledInput(InputEvent evt)
        {
            if (evt is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            switch (keyEvent.Keycode)
            {
                case Key.Q:
                    EmitSignal(SignalName.SpawnRequested, "fighter", _isBlueTabActive);
                    break;
                case Key.W:
                    EmitSignal(SignalName.SpawnRequested, "heavy_tank", _isBlueTabActive);
                    break;
                case Key.E:
                    EmitSignal(SignalName.SpawnRequested, "artillery", _isBlueTabActive);
                    break;
                case Key.R:
                    EmitSignal(SignalName.SpawnRequested, "destroyer_ship", _isBlueTabActive);
                    break;
                case Key.A:
                    EmitSignal(SignalName.SpawnRequested, "infantry", _isBlueTabActive);
                    break;
                case Key.S:
                    EmitSignal(SignalName.WaveRequested, _isBlueTabActive);
                    break;
                case Key.D:
                    EmitSignal(SignalName.ClashRequested);
                    break;
                case Key.F:
                    EmitSignal(SignalName.AutoWaveToggled);
                    break;
                case Key.H:
                    ToggleDeck();
                    break;
                case Key.Space:
                    EmitSignal(SignalName.PauseToggled);
                    break;
                case Key.Tab:
                    SwitchTab(!_isBlueTabActive);
                    break;
            }
        }

        public void UpdateBattleTelemetry(int blueCount, int blueKills, int redCount, int redKills, float gameTime, int fps, double timeScale, bool isPaused, bool autoWave)
        {
            _lblBlueUnits.Text = $"🛡️ {blueCount}";
            _lblBlueKills.Text = $"⚔️ {blueKills}";
            _lblRedUnits.Text = $"{redCount} 🛡️";
            _lblRedKills.Text = $"{redKills} ⚔️";

            int mins = (int)(gameTime / 60);
            int secs = (int)(gameTime % 60);
            _lblClock.Text = $"⏱️ {mins:D2}:{secs:D2}  |  {timeScale:0.0}x";

            int totalUnits = Math.Max(1, blueCount + redCount);
            float bluePercent = (float)blueCount / totalUnits * 100f;
            _tugOfWarBar.Value = Mathf.Clamp(bluePercent, 5f, 95f);

            _btnAutoWave.Text = autoWave ? "🔄 自动战意 [F]" : "⏸️ 自动战意 [F]";
            _btnPause.Text = isPaused ? "▶️ 继续" : "⏸️ 暂停";
            _btnSpeed.Text = $"⏩ {timeScale:0.0}x";

            // 动态战况战报滚动
            if (blueCount > redCount * 1.5f && blueCount >= 6)
            {
                _lblAlertTicker.Text = "📡 战况简报: 蓝方同盟力量占据战场战略优势，重型装甲集群正向红方阵地实施分割合围";
            }
            else if (redCount > blueCount * 1.5f && redCount >= 6)
            {
                _lblAlertTicker.Text = "📡 战况简报: 红方军团火力反扑强烈，战机俯冲压制蓝方前哨，全防线进入一级防御状态";
            }
            else if (blueKills + redKills > 0)
            {
                _lblAlertTicker.Text = $"📡 战况简报: 双方在中线爆发白热化交火，累计战果 蓝方 {blueKills} vs 红方 {redKills}，制空与制海权正在激烈争夺";
            }
            else
            {
                _lblAlertTicker.Text = "📡 战况简报: 三维立体战役展开，海陆空部队已按战术序列推进并建立接敌接触面";
            }
        }

        public override void _Process(double delta)
        {
            _waveAnimTimer += delta;
            if (_waveAnimTimer >= 0.18)
            {
                _waveAnimTimer = 0.0;
                _waveAnimFrame = (_waveAnimFrame + 1) % 4;
                string[] waves = { " ▂▃ ", "▂▃▅", "▃▅▆", "▅▆▇" };
                if (_lblVoiceWave != null)
                {
                    _lblVoiceWave.Text = waves[_waveAnimFrame];
                }
            }

            _radioChatterTimer += (float)delta;
            if (_radioChatterTimer >= 7.5f)
            {
                _radioChatterTimer = 0f;
                TriggerRandomTeammateChatter();
            }
        }

        public void TriggerVoiceTransmission(string speaker, string message, bool isPlayer = false)
        {
            if (_lblVoiceSpeaker != null)
            {
                _lblVoiceSpeaker.Text = $"{speaker}: {message}";
                _lblVoiceSpeaker.AddThemeColorOverride("font_color", isPlayer ? new Color(0.4f, 1.0f, 0.7f) : new Color(0.35f, 0.9f, 1.0f));
            }

            AddCombatLog("📻", $"{speaker}: {message}", isPlayer ? new Color(0.4f, 1.0f, 0.7f) : new Color(0.35f, 0.9f, 1.0f));

            if (_lblVoiceWave != null)
            {
                _lblVoiceWave.Modulate = new Color(0.3f, 1.0f, 0.5f, 1.0f);
                var tw = CreateTween();
                tw.TweenProperty(_lblVoiceWave, "modulate:a", 0.4f, 2.5f);
            }
        }

        private void TriggerRandomTeammateChatter()
        {
            var item = _chatterPool[_chatterIdx % _chatterPool.Length];
            _chatterIdx++;
            TriggerVoiceTransmission(item.name, item.callout, false);
        }

        private void ToggleMicTransmit()
        {
            _isMicTransmitting = !_isMicTransmitting;
            if (_isMicTransmitting)
            {
                _btnVoiceMic.Text = "🔴 说话中";
                _btnVoiceMic.Modulate = new Color(0.3f, 1f, 0.4f);
                TriggerVoiceTransmission("我方指挥 (您)", "已确认当前交火线，执行协同攻坚！", true);
                GetTree().CreateTimer(2.0).Timeout += () =>
                {
                    if (_isMicTransmitting)
                    {
                        _isMicTransmitting = false;
                        if (GodotObject.IsInstanceValid(_btnVoiceMic))
                        {
                            _btnVoiceMic.Text = "🎙️ 通话";
                            _btnVoiceMic.Modulate = Colors.White;
                        }
                    }
                };
            }
        }

        public void AddCombatLog(string tag, string message, Color tagColor)
        {
            if (_logContainer == null || !GodotObject.IsInstanceValid(_logContainer)) return;

            while (_logContainer.GetChildCount() >= 4)
            {
                var oldest = _logContainer.GetChild(0);
                _logContainer.RemoveChild(oldest);
                oldest.QueueFree();
            }

            var logItem = new PanelContainer();
            logItem.MouseFilter = Control.MouseFilterEnum.Ignore;
            logItem.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 2);
            margin.AddThemeConstantOverride("margin_top", 1);
            margin.AddThemeConstantOverride("margin_right", 6);
            margin.AddThemeConstantOverride("margin_bottom", 1);
            logItem.AddChild(margin);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);
            margin.AddChild(hbox);

            var lblTag = new Label { Text = tag };
            lblTag.AddThemeColorOverride("font_color", tagColor);
            lblTag.AddThemeFontSizeOverride("font_size", 9);
            lblTag.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            lblTag.AddThemeConstantOverride("outline_size", 2);
            hbox.AddChild(lblTag);

            var lblMsg = new Label { Text = message };
            lblMsg.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 1f, 0.95f));
            lblMsg.AddThemeFontSizeOverride("font_size", 9);
            lblMsg.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            lblMsg.AddThemeConstantOverride("outline_size", 2);
            lblMsg.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
            lblMsg.AddThemeConstantOverride("shadow_offset_x", 1);
            lblMsg.AddThemeConstantOverride("shadow_offset_y", 1);
            hbox.AddChild(lblMsg);

            _logContainer.AddChild(logItem);

            logItem.Modulate = new Color(1, 1, 1, 0);
            var tween = logItem.CreateTween();
            tween.TweenProperty(logItem, "modulate:a", 1.0f, 0.2f);
            tween.TweenInterval(4.5f);
            tween.TweenProperty(logItem, "modulate:a", 0.0f, 0.5f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(logItem))
                    logItem.QueueFree();
            }));
        }

        public override void _ExitTree()
        {
            if (_btnToggleDeck != null)
                _btnToggleDeck.Pressed -= ToggleDeck;
            if (_btnVoiceMic != null)
                _btnVoiceMic.Pressed -= ToggleMicTransmit;
        }
    }
}
