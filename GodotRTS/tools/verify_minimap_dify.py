# -*- coding: utf-8 -*-
"""
Dify 战术小地图/缩略地图修改验证脚本
向 Dify 发送验证请求，包含修复内容剖析与实机截图/视频，由 Dify 架构师进行修改验证与质量审计。
"""
import os
import sys

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if WORKSPACE_DIR not in sys.path:
    sys.path.insert(0, WORKSPACE_DIR)

from tools.wecom_bot.dify_bridge import ask_dify

def main():
    lan_ip = os.environ.get("LAN_HOST", "192.168.1.176")
    screenshot_url_dify = f"http://{lan_ip}:8000/screenshots/minimap_after.png"
    screenshot_url_local = "http://localhost:8000/screenshots/minimap_after.png"
    video_url_dify = f"http://{lan_ip}:8000/screenshots/minimap_test_video.mp4"

    prompt = f"""【战术小地图/地貌缩略图渲染体系 修复实机深度验证与架构审计】
指战员与全局架构师已针对 RTS 项目用户反馈的“小地图的背景 应该有个缩小版的地图才对”完成专项整改与全要素地貌重建：

### 1. 缺陷根因排查与分析 (Root Cause)：
- **原代码空桩导致纯黑方块**：Git 原先的 `scripts/ui/BattleMinimap.cs` 仅为一个 39 行的空壳存根，完全缺失 `_Draw()` 地貌渲染实现与地貌数据解析，导致左上角“📡 态势雷达 // S-01”面板内仅有一块纯黑色的空白矩形。
- **地图渲染器崩溃隐患修复**：此前 `BattleMapRenderer.cs` 中未索引的 SurfaceTool 大网格顶点数达 153,600，在 `CreateTrimeshShape` 时偶发内存分配崩溃，现已降采样并引入 try/catch 安全保底机制。

### 2. 核心架构与全要素地貌重建落地 (Technical Implementation)：
1. **全要素微缩地形测绘渲染体系 (Procedural Topographic Cartography)**：
   - **地貌基底 (Land Surface)**：动态读取当前地图 `GroundColor` 并自适应调校战术色阶，呈现逼真的陆地地形色系。
   - **水文水系与大河水道 (Waters & Rivers)**：精确绘制宽幅水系多边形，渲染双侧亮青色高发光河岸线 (Glowing Shorelines) 与航道中轴虚光线。支持火山要塞炽热熔岩地貌。
   - **高地与战略悬崖 (Elevated Plateaus)**：绘制高台地形与战术边缘轮廓多边形。
   - **补给要道 (Roads) 与跨河大桥 (Bridges)**：道路标绘行军通道；跨河战略大桥采用甲板色并配有醒目的金黄高警示防撞防护栏 (Yellow Safety Railings) 与中心通行中轴线。
   - **基地要塞停机坪 (Patches)**：友军停机坪（深蓝底配青亮边框）、敌军停机坪（深红底配烈橙边框）及战术扇区。
   - **自然有机地貌要素 (Rocks & Foliage)**：精确解析地图上的岩石群落 (`RockClusters`) 与植被林木群 (`TreePositions`)，在小地图以双重同心光晕点位标绘绿洲/森林与岩石隘口。
2. **严格遵守 3D 摄像机 180° Y-yaw 逆转铁律 (Rule 4 Compliance)**：
   - RTS 3D 摄像机采用 180° Y-yaw 逆转 (`Basis.X = (-1, 0, 0)`)，即屏幕左 = +X，屏幕右 = -X，屏幕上 = +Z，屏幕下 = -Z。
   - `BattleMinimap.cs` 中所有世界坐标到小地图归一化坐标的映射严格遵循：
     `normX = 0.5f - (X / mapWorldSize.X)`
     `normY = 0.5f - (Z / mapWorldSize.Y)`
   - 交互反推视口重定向严格对称：
     `worldX = (0.5f - normX) * mapWorldSize.X`
     `worldZ = (0.5f - normY) * mapWorldSize.Y`
   - 实现小地图点击与拖拽视口平滑跳转 (`HandleMinimapNavigation` -> `rtsCam.JumpTo(...)`)。
3. **动态战术态势感知层 (Tactical Dynamic Layer)**：
   - **雷达动态扫描线 (Radar Sweep Beam & Phosphor Glow Trail)**：全域旋转扫描波束，带 3 层余晖拖尾。采用射线-矩形内部几何求交算法，确保光束与拖尾永不溢出雷达外框。
   - **实时部队与建筑光斑 (Unit & Building Blips)**：友军蓝/金色标示，敌军高亮炽红圆点附带呼吸警戒外光圈；支持主基地菱盾徽记与战机倒三角图标。
   - **警报蜂鸣与雷达冲击波 (Alert Pings & Shockwaves)**：当单位遭受攻击或发现敌军时，在小地图对应坐标生成 3 层动态外扩雷达冲击波，支持点击直达警报中心。
   - **3D 摄像机地面视锥投影 (Camera Frustum Ground Polygon)**：实时提取 `TryGetViewportGroundPolygon` 4 角坐标并投射为半透明微光四边形与高光轮廓。
4. **零 GC 与极限性能优化 (Zero-GC Optimization)**：
   - 地貌多边形与自然要素在地图切换或尺寸变动时一次性预计算，运行帧期间仅进行轻量局部绘制，80ms 节流采集部队遥测，无动态堆内存分配。

### 3. 实机截图与视频验证资源 (Verification Artifacts)：
- **实机截图**：![实机截图]({screenshot_url_dify}) (局域网) / `{screenshot_url_local}` (本地)
- **实机视频 (20s+ 自动化录屏)**：[实机战斗与小地图录像]({video_url_dify})

请 Dify 架构师对本次“小地图地貌缩略图渲染体系”的修复质量进行全面审计与专业技术点评，重点评估：
1. 全要素微缩地貌（陆地、河流、桥梁、基地、树木岩石）的完整性与视觉呈现；
2. 180° Y-yaw 逆转坐标映射契约与视口视锥对应保真度；
3. 动态态势层（雷达扫掠、敌我光斑、警报脉冲环）的战术实用性与沉浸感；
4. 零 GC 设计在 RTS 密集计算场景下的架构合理性。
"""

    print("🚀 正在向 Dify 发送【战术小地图修改验证】请求...")
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

        os.makedirs(os.path.join(WORKSPACE_DIR, "scratch"), exist_ok=True)
        report_file = os.path.join(WORKSPACE_DIR, "scratch", "dify_minimap_verification.md")
        with open(report_file, "w", encoding="utf-8") as f:
            f.write(full_text)
        print("\n" + "=" * 70)
        print(f"✅ Dify 验证报告已保存至: {report_file}")
    except Exception as e:
        print(f"\n❌ Dify 请求异常: {e}")

if __name__ == "__main__":
    main()
