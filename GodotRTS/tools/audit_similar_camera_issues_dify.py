# -*- coding: utf-8 -*-
"""
Dify 类似视角漂移/跳转缺陷排查脚本
将近期发现的“点击全选时视角突变”问题与项目中所有涉及镜头聚焦、选择交互、HUD指令触发的代码上下文提交给 Dify，
由 Dify 资深架构师进行深度审计，排查是否存在其他类似会导致玩家视角意外变动或操作中断的隐患。
"""
import os
import sys

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if WORKSPACE_DIR not in sys.path:
    sys.path.insert(0, WORKSPACE_DIR)

from tools.wecom_bot.dify_bridge import ask_dify

def main():
    prompt = """【RTS 战场镜头与交互控制专项审计与隐患排查】
指战员与测试团队反馈了一个严重的体验缺陷：
“点击全选的时候 视角会变 这不应该的”

### 根因回顾：
在 `BattleHud.cs` 的 `SelectAllOwnedUnits()` 方法中，原本在选择所有单位后，错误地附加了一句：
```csharp
if (units.Length > 0)
{
    BattleGameManager.Instance?.FocusCameraOnPlayerForces();
}
```
导致玩家在观察战场或前线时，点击全选按钮（或由于部队全选），镜头被突兀地拉回所有单位的平均位置/基地，打断了玩家的操作流。

### 审计任务目标：
请全面排查项目中所有可能导致“视角意外改变”、“操作中断”或“不符合 RTS 直觉”的类似交互与镜头问题。玩家要求：“排查类似问题进行修改，不要让我一个个的测试看”。

请针对以下代码机制与关键路径进行深入审查并给出排查结论与修复建议：

1. **选择与多选交互**：
   - 单击单位/建筑选择（`SelectAt`）
   - 双击同屏同类单位全选（`SelectNearbyUnitsOfSameType`）
   - 框选（`BoxSelect`）
   - 全选或选择集变更（`GameState.SetSelection`）时，是否还有其他地方被埋设了强制摄像机跳转（`JumpTo` / `FocusCamera`）？

2. **底部指令栏与 HUD 交互**：
   - 进攻（Attack Move）、停止（Stop）、驻守（Hold）、巡逻（Patrol）、散开（Spread）、炮击/轰炸（Special Command）、停机（Park）、架设（Deploy）
   - 建筑建造与取消（`BeginBuild`、`CancelConstruction`）
   - 科技升级与研发（`BeginTechTargeting`、`OpenUpgradeDialog`）
   - 维修单位/建筑（`RepairBuilding`、`RepairUnit`）
   - 占领据点/指挥部（`OccupyGlobalConquestMainBase`）
   - 在点击这些按钮或执行指令时，是否有任何意外的视角重置或镜头拉扯？

3. **小地图与战术雷达点击/拖拽交互**：
   - 小地图点击与拖拽（`BattleMinimap.cs`）
   - 雷达点击（`TacticalRadar.cs`）
   - 是否会发生误触、坐标跳变或反转不一致？

4. **快捷键与输入响应**：
   - 快捷键如 Space、Home、A、S、H、P、F、D、B、T 等，触发时是否有意外的全局事件或镜头副效应？
   - 顶部选择栏 `selectionLabel` 点击聚焦机制是否会误触？

5. **战局启动与重置机制**：
   - 游戏开局（`BattleBootstrap` / `BattleGameManager` 初始化）聚焦玩家部队的逻辑是否平滑且仅触发一次？

请列出具体的隐患点、所属文件与代码行号/方法名，并给出明确的修改方案。
"""

    print("🚀 正在向 Dify 提交【镜头与交互类似缺陷全面审计】请求...")
    print("=" * 70)

    collected_chunks = []
    def on_token(chunk: str):
        print(chunk, end="", flush=True)
        collected_chunks.append(chunk)

    try:
        res = ask_dify(
            prompt,
            user_id="lead_rts_architect",
            timeout=180,
            on_chunk=on_token
        )
        full_text = "".join(collected_chunks).strip() or res.get("answer", "")
        
        report_file = os.path.join(WORKSPACE_DIR, "scratch", "dify_camera_interaction_audit.md")
        os.makedirs(os.path.dirname(report_file), exist_ok=True)
        with open(report_file, "w", encoding="utf-8") as f:
            f.write(full_text)
        print("\n" + "=" * 70)
        print(f"✅ Dify 审计报告已保存至: {report_file}")
    except Exception as e:
        print(f"\n❌ Dify 请求异常: {e}")

if __name__ == "__main__":
    main()
