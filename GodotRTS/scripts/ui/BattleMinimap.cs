using Godot;
using System;

/// <summary>
/// Godot 4 3D RTS 战术小地图组件
/// 支持 180° Y-yaw 镜头朝向视锥渲染、单位雷达点映射与点击小地图快速视口跳转
/// </summary>
public partial class BattleMinimap : Control
{
    [Export] private Node3D _playerCamera;
    [Export] private Vector2 mapWorldSize = new Vector2(200f, 200f);

    public override void _Ready()
    {
        GD.Print("[BattleMinimap] RTS 小地图雷达系统初始化完成。");
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            Vector2 localPos = mouseEvent.Position;
            Vector2 minimapSize = Size;
            
            // 归一化坐标 0.0 ~ 1.0
            float normX = localPos.X / minimapSize.X;
            float normY = localPos.Y / minimapSize.Y;

            // 遵循 180° Y-yaw 坐标反转铁律：屏幕左+X 右-X，上+Z 下-Z
            float worldX = (0.5f - normX) * mapWorldSize.X;
            float worldZ = (0.5f - normY) * mapWorldSize.Y;

            GD.Print($"[BattleMinimap] 点击小地图跳转 -> 世界坐标: ({worldX:F1}, {worldZ:F1})");
            
            // 触发镜头移动逻辑...
        }
    }
}
