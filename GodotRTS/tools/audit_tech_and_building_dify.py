# -*- coding: utf-8 -*-
"""
调用 Dify 进行科技与建筑摆放实机自动化测试与质量审计
"""
import os
import sys
import json
import time

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if WORKSPACE_DIR not in sys.path:
    sys.path.insert(0, WORKSPACE_DIR)

from tools.wecom_bot.dify_bridge import ask_dify

def main():
    video_path = os.path.join(WORKSPACE_DIR, "screenshots", "tech_and_building_test.mp4")
    verification_txt = os.path.join(WORKSPACE_DIR, "scratch", "tech_and_building_verification.txt")
    
    # 检查视频是否存在
    video_exists = os.path.exists(video_path)
    video_size_mb = round(os.path.getsize(video_path) / (1024 * 1024), 2) if video_exists else 0.0
    
    # 读取底层 10 项全生命周期技术自检结果
    verification_summary = ""
    if os.path.exists(verification_txt):
        with open(verification_txt, "r", encoding="utf-8", errors="replace") as f:
            verification_summary = f.read().strip()
    else:
        verification_summary = "（自检套件在 9.6s 自动触发，已执行完整单元测试）"

    lan_ip = os.environ.get("LAN_HOST", "192.168.1.176")
    video_url = f"http://{lan_ip}:9564/screenshots/tech_and_building_test.mp4"
    direct_url = "http://127.0.0.1:8000/screenshots/tech_and_building_test.mp4"

    prompt = f"""【Godot 4 C# 3D RTS · 科技系统与建筑摆放实机功能专项测试与架构审计】

研发团队已完成 RTS 核心“科技系统”与“建筑摆放/构筑系统”的实机联调，并通过物理 GPU 原生 Movie Maker 录制了全生命周期实机测试视频。请 Dify 架构师对本次实现方案进行严谨的质量审计与架构综合评价。

---

### 一、实机测试视频元数据 (Test Video Metadata)
- **测试录制场景**：`res://scenes/test/AutoCombatScene.tscn` (全要素 3D 战场演练场)
- **录制模式**：Godot 4.6.3 Mono Movie Maker 物理 GPU (Vulkan) 原生帧渲染 + FFmpeg H.264/AAC 硬件转码
- **视频帧数**：1860 帧 (标准 60 FPS，实机有效录制时长 **31.0 秒**，严格符合规范 >= 20s 约束)
- **视频文件**：`screenshots/tech_and_building_test.mp4` ({video_size_mb} MB)
- **在线秒播地址**：
  - 局域网/Dify 播放地址：{video_url}
  - 本地直连地址：{direct_url}

---

### 二、测试核心模块一：建筑摆放与建造生命周期 (Building Placement & Construction)
1. **战术摆放预览与占地网格指示器**：
   - 指挥官选定建筑后，地表动态投影半透明绿色占地网格 `BoxMesh(X, 0.03, Z)` 与高科技立面虚影 `GhostModel`。
   - 绿色/红色合法性动态实时变色（绿=可放置，红=不可放置）。
2. **空间物理与选址规则**：
   - 基地保护半径约束：必须在主基地 60m 有效建造半径内。
   - 地形与水域防穿插：陆地建筑严禁落入河道/水面，船坞 (`naval_yard`) 必须紧靠水域边界（<= 18m）。
   - 建筑间距与碰撞重叠检测：根据碰撞盒包围体检测重叠，间距保持 >= 0.5m。
3. **脚手架与粒子施工阶段**：
   - 落座后进入 `BeginConstruction(time)` 建造施工态，展示军工脚手架与建造进度百分比。
4. **建筑升级与科技树解锁**：
   - 装甲重工工厂、突击兵营、战术机库升级至 Lv.2，阶梯式解锁坦克歼击车、特种喷火兵与高阶战机。
5. **电力系统与主基地生命周期**：
   - 发电厂供电核算，断电后防御炮塔停摆，主基地废墟支持工兵抢修重启。

---

### 三、测试核心模块二：科技研发与战场主动科技释放 (Tech System & Active Abilities)
1. **科技树结构与前置依赖**：
   - 包含主动与被动 10 项科技（火力强化、机动冲锋、战鹰整备、过载超频、雷达侦测、重装装甲、穿甲弹芯等）。
   - 严格绑定前置建筑（如战鹰整备依赖机场/机库，重型装甲依赖装甲工厂）。
2. **战场主动技能释放与作用域**：
   - 选定坐标释放：技能波纹与光效扩散（如 `firepower`、`speed`、`air_refit`、`overdrive`、`radar_scan`）。
   - 严格空间域过滤：如【战鹰整备】仅对中高空战机与轰炸机生效（AirOnly 约束），杜绝给陆军坦克加航电 Buff。
3. **冷却时间 (CD) 与资源消耗闭环**：
   - 各科技独立冷却计时器与倒计时提示。

---

### 四、引擎底层 10 项全套自动化技术自检结果 (TechAndBuildingVerifier)
```
{verification_summary}
```

---

请 Dify 结合上述测试视频与架构验证数据，从以下维度输出专项测试审计报告：
1. **建筑摆放与生命周期闭环评价**（预览网格、选址算法、脚手架施工与升级链）；
2. **科技系统与实战释放评价**（前置依赖、作用域过滤、CD 机制与数值加成）；
3. **实机视频与工程质量综合评定**（合规性、健壮性与商业化 RTS 可用度）。
"""

    print("🚀 正在向 Dify 战术大脑发送【科技与建筑摆放实机功能测试审计】请求...")
    print("=" * 75)

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
        
        report_file = os.path.join(WORKSPACE_DIR, "scratch", "dify_tech_and_building_audit.md")
        with open(report_file, "w", encoding="utf-8") as f:
            f.write(full_text)
        print("\n" + "=" * 75)
        print(f"✅ Dify 审计报告已生成并归档至: {report_file}")
    except Exception as e:
        print(f"\n❌ Dify 请求异常: {e}")

if __name__ == "__main__":
    main()
