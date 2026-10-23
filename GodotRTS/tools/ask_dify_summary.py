# -*- coding: utf-8 -*-
"""
Query Dify 'RTS 全局总架构师' for current unresolved project issues and roadmap.
"""
import os
import sys
import json

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT_DIR not in sys.path:
    sys.path.insert(0, ROOT_DIR)

from tools.wecom_bot.dify_bridge import ask_dify

def main():
    prompt = """开始全局审计：
请立即对当前 Godot 4 C# 3D RTS 项目进行全局架构审计与未解决问题全面梳理。

请系统评估并深入剖析以下七大核心维度（按 P0 / P1 / P2 严格分级，详细阐述现存缺陷与瓶颈根因、推荐架构解法与落地实施步骤、质量验证标准）：
一、网络联机与确定性帧同步 (Networking & Deterministic Lockstep)
二、大兵团流场寻路与编队防挤压 (Flow Field & Group Formation Pathfinding)
三、全动态战争迷雾与视距遮罩系统 (Fog of War & Tactical Line-of-Sight)
四、海陆空立体攻防与补给调度闭环 (Airfield/Carrier Return Cycle & Logistics)
五、Android 移动端触控交互与 Vulkan 渲染性能优化 (Mobile Gestures & Vulkan Optimization)
六、3D 空间音频与战斗拟真沉浸感 (Spatial Audio & Positional Feedback)
七、自动化测试流水线与长周期验证 (End-to-End Regression & Rule 5 Standards)
八、架构师综合实施建议与近期里程碑路线图 (Chief Architect's Milestone Roadmap)

请直接输出内容详尽、结构严密的专业 Markdown 报告。"""

    print("🚀 正在向 Dify [RTS 全局总架构师] 发送【开始全局审计】指令...")
    print("=" * 70)
    
    collected_text = []
    def on_token(chunk: str):
        print(chunk, end="", flush=True)
        collected_text.append(chunk)

    result = ask_dify(
        prompt, 
        user_id="lead_rts_architect", 
        conversation_id="21dd3ff5-c135-41f6-ae6d-d716850489dc",
        timeout=600, 
        on_chunk=on_token
    )
    
    full_output = "".join(collected_text).strip() or result.get("answer", "")
    
    output_path = os.path.join(os.path.dirname(__file__), "dify_unresolved_issues_summary.md")
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(full_output)
        
    print("\n" + "=" * 70)
    print(f"✅ Dify 架构师总结已完整保存至: {output_path}")

if __name__ == "__main__":
    main()
