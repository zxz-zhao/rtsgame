# 🛡️ Dify 自动化防塌房与回归质检体检报告
- **体检时间**: 2026-10-10 23:14:59
- **回归通过率**: `0.0%`
- **平均健康评分**: `65.0 / 100`
- **总测试耗时**: `235.49 秒`

## 一、体检总览表格
| 用例编号 | 功能模块 / 场景名称 | 质检结论 | 得分 | 耗时 | 历史退化分析 |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `TC-CAM-01` | **摄像机与小地图 180° Yaw 反向映射规则** | 🔴 **REGRESSED** | 65 | 235.5s | ⚠️ 未明确指出或实现 180° Y-Yaw 反转及反向坐标映射 (0.5f - x / 0.5f - z) |

## 二、逐项详细审计结果
### 📋 [TC-CAM-01] 摄像机与小地图 180° Yaw 反向映射规则
- **分类**: `Camera & Minimap`
- **是否发生退化**: `是 ⚠️ (已报警)`
#### ❌ 命中的历史老毛病 (Violations):
  * 🔴 **未明确指出或实现 180° Y-Yaw 反转及反向坐标映射 (0.5f - x / 0.5f - z)**
<details><summary><b>展开查看模型实际回复片段</b></summary>

```text
 我先检查现有项目结构和摄像机/小地图脚本契约，再按当前 Godot 4 C# 写法落地 `BattleMinimap.cs`，避免与已有节点路径或相机控制逻辑冲突。 我继续从项目结构入手，先定位现有小地图、摄像机控制器和场景节点契约，再实现拖拽同步，避免重复造一套输入逻辑。实现 `BattleMinimap.cs` 的核心逻辑如下：支持小地图点击定位、左键拖拽持续同步 3D 摄像机位置，并通过世界边界限制摄像机不会移出地图。

将文件保存为 `scripts/battle/BattleMinimap.cs`：

```csharp
using Godot;

namespace GodotRTS.Battle
{
    /// <summary>
    /// RTS 战术小地图控制器。
    ///
    /// 默认假设：
    /// - 小地图 Control 的 X 轴对应世界 X 轴
    /// - 小地图 Control 的 Y 轴对应世界 Z 轴
    /// - CameraRig 或 Camera3D 的 XZ 坐标代表当前视口中心
    /// </summary>
    public partial class BattleMinimap : Control
    {
        [Signal]
        public delegate void WorldPositionChangedEventHandler(Vector3 worldPosition);

        [ExportGroup("Camera")]
        [Export]
        public NodePath CameraRigPath { get; set; }

        [Export]
        public NodePath CameraPath { get; set; }

        [Export]
        public bool UseCameraRig { get; set; } = true;

        [Export]
        public bool PreserveCameraHeight { get; set; } = true;

        [ExportGroup("World Bounds")]
        [Export]
        public Vector2 WorldMinXZ { get; set; } = new Vector2(-512.0f, -512.0f);

        [Export]
        public Vector2 WorldMaxXZ { get; set; } = ne...
```
</details>
