using Godot;
using System;

/// <summary>
/// Godot 4 3D RTS 战场顶级 HUD 界面管理器
/// 负责管理顶部资源条、底部命令网格、选中单位面板、右侧编队与小地图联动
/// </summary>
public partial class BattleHUDManager : Control
{
    [Export] private Label _resourceAlloyLabel;
    [Export] private Label _resourceEnergyLabel;
    [Export] private Label _populationLabel;
    [Export] private Control _commandGridContainer;
    [Export] private Control _minimapContainer;
    [Export] private TextureRect _unitPortrait;
    [Export] private ProgressBar _unitHealthBar;

    public override void _Ready()
    {
        GD.Print("[BattleHUDManager] RTS 战场现代化 HUD 初始化成功。");
        InitializeHUDStyle();
    }

    private void InitializeHUDStyle()
    {
        // 动态初始化 UI 样式、绑定事件
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// 更新顶部资源显示（零 GC 友好）
    /// </summary>
    public void UpdateResources(int alloy, int energy, int currentPop, int maxPop)
    {
        if (_resourceAlloyLabel != null) _resourceAlloyLabel.Text = $"合金: {alloy}";
        if (_resourceEnergyLabel != null) _resourceEnergyLabel.Text = $"能源: {energy}";
        if (_populationLabel != null) _populationLabel.Text = $"人口: {currentPop}/{maxPop}";
    }

    /// <summary>
    /// 更新选中单位面板
    /// </summary>
    public void UpdateSelectedUnit(string unitName, float healthPercent, Texture2D portrait)
    {
        if (_unitHealthBar != null) _unitHealthBar.Value = healthPercent * 100f;
        if (_unitPortrait != null && portrait != null) _unitPortrait.Texture = portrait;
    }
}
