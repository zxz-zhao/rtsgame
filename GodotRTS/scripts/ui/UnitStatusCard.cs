using Godot;

namespace GodotRTS.Scripts.UI
{
    /// <summary>
    /// 现代科技感 RTS 单位信息卡片控制器
    /// 负责展示选中单位的生命值、护盾、基础属性及技能状态
    /// </summary>
    public partial class UnitStatusCard : Control
    {
        [Export] private Label _unitNameLabel;
        [Export] private Label _unitTitleLabel;
        [Export] private ProgressBar _hpProgressBar;
        [Export] private ProgressBar _shieldProgressBar;
        [Export] private Label _atkLabel;
        [Export] private Label _defLabel;
        [Export] private Label _spdLabel;
        [Export] private Label _rngLabel;

        public override void _Ready()
        {
            _unitNameLabel ??= GetNodeOrNull<Label>("%NodeNameLabel");
            _unitTitleLabel ??= GetNodeOrNull<Label>("%NodeTitleLabel");
            _hpProgressBar ??= GetNodeOrNull<ProgressBar>("%HpBar");
            _shieldProgressBar ??= GetNodeOrNull<ProgressBar>("%ShieldBar");
            _atkLabel ??= GetNodeOrNull<Label>("%AtkVal");
            _defLabel ??= GetNodeOrNull<Label>("%DefVal");
            _spdLabel ??= GetNodeOrNull<Label>("%SpdVal");
            _rngLabel ??= GetNodeOrNull<Label>("%RngVal");

            // 初始化默认测试数据
            UpdateUnitInfo("MK-IV 暴风重型坦克", "联邦第一装甲师 - 战斗就绪", 850, 1000, 300, 300, 185, 42, 6.5f, 12.0f);
        }

        public void UpdateUnitInfo(
            string unitName, string title, 
            float currentHp, float maxHp, 
            float currentShield, float maxShield,
            int atk, int def, float spd, float rng)
        {
            if (_unitNameLabel != null) _unitNameLabel.Text = unitName;
            if (_unitTitleLabel != null) _unitTitleLabel.Text = title;

            if (_hpProgressBar != null)
            {
                _hpProgressBar.MaxValue = maxHp;
                _hpProgressBar.Value = currentHp;
            }

            if (_shieldProgressBar != null)
            {
                _shieldProgressBar.MaxValue = maxShield;
                _shieldProgressBar.Value = currentShield;
            }

            if (_atkLabel != null) _atkLabel.Text = $"ATK: {atk}";
            if (_defLabel != null) _defLabel.Text = $"DEF: {def}";
            if (_spdLabel != null) _spdLabel.Text = $"SPD: {spd:F1}";
            if (_rngLabel != null) _rngLabel.Text = $"RNG: {rng:F1}";
        }
    }
}
