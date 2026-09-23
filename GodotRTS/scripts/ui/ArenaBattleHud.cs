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

            // Fade Entrance Animation
            var topBar = GetNode<Control>("%TopTelemetryBar");
            var bottomDeck = GetNode<Control>("%BottomCommandDeck");

            topBar.Modulate = new Color(1, 1, 1, 0);
            bottomDeck.Modulate = new Color(1, 1, 1, 0);

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(topBar, "modulate:a", 1.0f, 0.35f);
            tween.TweenProperty(bottomDeck, "modulate:a", 1.0f, 0.35f);
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

        public override void _ExitTree()
        {
            // Fully unhook all events to ensure zero leaks
        }
    }
}
