using Godot;
using System;

namespace GodotRTS.Scripts.UI
{
    /// <summary>
    /// 高阶 RTS 战术单位指挥与技能装配面板 (Tactical Command & Unit Combat Panel)
    /// 包含：单位战术识别卡、多维耐久/护盾/等离子计量槽、4组战术战法技能矩阵、实时微调遥测与模式切换逻辑
    /// </summary>
    public partial class TacticalCommandPanel : Control
    {
        // 核心信息控件引用
        [Export] private Label _unitNameLabel;
        [Export] private Label _unitSubTitleLabel;
        [Export] private Label _unitStatusBadge;
        [Export] private Label _killCountLabel;

        // 状态计量条
        [Export] private ProgressBar _hpBar;
        [Export] private Label _hpText;
        [Export] private ProgressBar _shieldBar;
        [Export] private Label _shieldText;
        [Export] private ProgressBar _plasmaBar;
        [Export] private Label _plasmaText;

        // 属性遥测指标
        [Export] private Label _dpsValue;
        [Export] private Label _armorValue;
        [Export] private Label _rangeValue;
        [Export] private Label _speedValue;

        // 技能按钮组
        [Export] private Button _skillBtnQ;
        [Export] private Button _skillBtnW;
        [Export] private Button _skillBtnE;
        [Export] private Button _skillBtnR;

        // 技能冷却与状态覆盖层
        [Export] private Label _skillCooldownE;
        [Export] private Label _modeLabel;

        // 运行时状态
        private bool _isSiegeMode = false;
        private float _empCooldown = 8.4f;
        private float _currentHp = 4850f;
        private float _maxHp = 5000f;
        private float _currentShield = 1200f;
        private float _maxShield = 1200f;
        private float _currentPlasma = 380f;
        private float _maxPlasma = 500f;

        public override void _Ready()
        {
            BindNodes();
            ConnectEvents();
            RefreshDisplay();
        }

        private void BindNodes()
        {
            _unitNameLabel ??= GetNodeOrNull<Label>("%UnitNameLabel");
            _unitSubTitleLabel ??= GetNodeOrNull<Label>("%UnitSubTitleLabel");
            _unitStatusBadge ??= GetNodeOrNull<Label>("%UnitStatusBadge");
            _killCountLabel ??= GetNodeOrNull<Label>("%KillCountLabel");

            _hpBar ??= GetNodeOrNull<ProgressBar>("%HpBar");
            _hpText ??= GetNodeOrNull<Label>("%HpText");
            _shieldBar ??= GetNodeOrNull<ProgressBar>("%ShieldBar");
            _shieldText ??= GetNodeOrNull<Label>("%ShieldText");
            _plasmaBar ??= GetNodeOrNull<ProgressBar>("%PlasmaBar");
            _plasmaText ??= GetNodeOrNull<Label>("%PlasmaText");

            _dpsValue ??= GetNodeOrNull<Label>("%DpsValue");
            _armorValue ??= GetNodeOrNull<Label>("%ArmorValue");
            _rangeValue ??= GetNodeOrNull<Label>("%RangeValue");
            _speedValue ??= GetNodeOrNull<Label>("%SpeedValue");

            _skillBtnQ ??= GetNodeOrNull<Button>("%SkillBtnQ");
            _skillBtnW ??= GetNodeOrNull<Button>("%SkillBtnW");
            _skillBtnE ??= GetNodeOrNull<Button>("%SkillBtnE");
            _skillBtnR ??= GetNodeOrNull<Button>("%SkillBtnR");

            _skillCooldownE ??= GetNodeOrNull<Label>("%SkillCooldownE");
            _modeLabel ??= GetNodeOrNull<Label>("%ModeLabel");
        }

        private void ConnectEvents()
        {
            if (_skillBtnQ != null) _skillBtnQ.Pressed += OnOverdrivePressed;
            if (_skillBtnW != null) _skillBtnW.Pressed += OnSiegeModeTogglePressed;
            if (_skillBtnE != null) _skillBtnE.Pressed += OnEmpSalvoPressed;
            if (_skillBtnR != null) _skillBtnR.Pressed += OnNaniteRepairPressed;
        }

        public override void _Process(double delta)
        {
            // 模拟 EMP 技能冷却倒计时
            if (_empCooldown > 0f)
            {
                _empCooldown -= (float)delta;
                if (_skillCooldownE != null)
                {
                    if (_empCooldown > 0f)
                    {
                        _skillCooldownE.Visible = true;
                        _skillCooldownE.Text = $"{_empCooldown:F1}s";
                        if (_skillBtnE != null) _skillBtnE.Disabled = true;
                    }
                    else
                    {
                        _skillCooldownE.Visible = false;
                        if (_skillBtnE != null) _skillBtnE.Disabled = false;
                    }
                }
            }
        }

        private void OnOverdrivePressed()
        {
            GD.Print("[TacticalCommandPanel] 触发技能: [Q] 战术过载推进 (Overdrive Thruster)！航速临时爆发提升75%！");
            _currentPlasma = Mathf.Max(0, _currentPlasma - 60f);
            RefreshDisplay();
        }

        private void OnSiegeModeTogglePressed()
        {
            _isSiegeMode = !_isSiegeMode;
            GD.Print($"[TacticalCommandPanel] 切换模式: [W] 攻城重炮架设 -> {(_isSiegeMode ? "已架设 (SIEGE)" : "巡航模式 (MOBILE)")}");
            RefreshDisplay();
        }

        private void OnEmpSalvoPressed()
        {
            if (_empCooldown <= 0f && _currentPlasma >= 120f)
            {
                _currentPlasma -= 120f;
                _empCooldown = 15.0f;
                GD.Print("[TacticalCommandPanel] 触发技能: [E] 电磁脉冲重型齐射 (EMP Heavy Salvo)！区域电磁瘫痪！");
                RefreshDisplay();
            }
        }

        private void OnNaniteRepairPressed()
        {
            GD.Print("[TacticalCommandPanel] 触发技能: [R] 纳米紧急损管 (Nanite Repair Field)！");
            _currentHp = Mathf.Min(_maxHp, _currentHp + 1000f);
            _currentShield = _maxShield;
            _currentPlasma = Mathf.Max(0, _currentPlasma - 100f);
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (_unitNameLabel != null) _unitNameLabel.Text = "M5A2 泰坦重型攻城战车";
            if (_unitSubTitleLabel != null) _unitSubTitleLabel.Text = "联邦第七重装突击军团 · 战略攻城单位";
            if (_killCountLabel != null) _killCountLabel.Text = "击毁目标: 14 目标 | VET ★★★★";

            if (_hpBar != null)
            {
                _hpBar.MaxValue = _maxHp;
                _hpBar.Value = _currentHp;
            }
            if (_hpText != null) _hpText.Text = $"{_currentHp:F0} / {_maxHp:F0} ({(int)(_currentHp / _maxHp * 100)}%)";

            if (_shieldBar != null)
            {
                _shieldBar.MaxValue = _maxShield;
                _shieldBar.Value = _currentShield;
            }
            if (_shieldText != null) _shieldText.Text = $"{_currentShield:F0} / {_maxShield:F0}";

            if (_plasmaBar != null)
            {
                _plasmaBar.MaxValue = _maxPlasma;
                _plasmaBar.Value = _currentPlasma;
            }
            if (_plasmaText != null) _plasmaText.Text = $"{_currentPlasma:F0} / {_maxPlasma:F0}";

            if (_isSiegeMode)
            {
                if (_modeLabel != null)
                {
                    _modeLabel.Text = "[ 攻城重炮形态 (SIEGE) ]";
                    _modeLabel.Modulate = new Color(1f, 0.4f, 0.2f);
                }
                if (_dpsValue != null) _dpsValue.Text = "850/s (AOE)";
                if (_armorValue != null) _armorValue.Text = "220 (+60)";
                if (_rangeValue != null) _rangeValue.Text = "1650m (极远)";
                if (_speedValue != null) _speedValue.Text = "0 m/s (锚定)";
            }
            else
            {
                if (_modeLabel != null)
                {
                    _modeLabel.Text = "[ 巡航机动形态 (MOBILE) ]";
                    _modeLabel.Modulate = new Color(0.2f, 0.8f, 1f);
                }
                if (_dpsValue != null) _dpsValue.Text = "480/s";
                if (_armorValue != null) _armorValue.Text = "160 (重甲)";
                if (_rangeValue != null) _rangeValue.Text = "850m";
                if (_speedValue != null) _speedValue.Text = "14.5 m/s";
            }
        }
    }
}
