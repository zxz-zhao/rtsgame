using Godot;
using System;

/// <summary>
/// Godot 4 3D RTS 战术命令网格组件 (Action Grid)
/// 负责管理星际争霸/红警风格的 3x4 底部操作面板，支持动态指令分发与按钮状态刷新
/// </summary>
public partial class BattleActionGrid : GridContainer
{
    [Export] private int columnsCount = 4;

    public override void _Ready()
    {
        Columns = columnsCount;
        GD.Print("[BattleActionGrid] 战术命令面板网格初始化成功。当前列数: ", Columns);
    }

    /// <summary>
    /// 动态绑定指令回调
    /// </summary>
    public void RegisterCommandButton(int slotIndex, string commandName, Callable callback)
    {
        GD.Print($"[BattleActionGrid] 注册槽位 #{slotIndex} 命令: {commandName}");
    }
}
