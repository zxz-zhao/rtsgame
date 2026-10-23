# -*- coding: utf-8 -*-
"""
Dify 科技目录修改验证脚本
向 Dify 发送验证请求，包含修复内容剖析与实机截图，由 Dify 架构师进行修改验证与质量审计。
"""
import os
import sys

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if WORKSPACE_DIR not in sys.path:
    sys.path.insert(0, WORKSPACE_DIR)

from tools.wecom_bot.dify_bridge import ask_dify

def main():
    lan_ip = os.environ.get("LAN_HOST", "192.168.1.176")
    screenshot_url_dify = f"http://{lan_ip}:8000/screenshots/tech_after.png"
    screenshot_url_local = "http://localhost:8000/screenshots/tech_after.png"

    prompt = f"""【科技目录/研究中心 UI 修复实机验证】
指战员与前端架构师已完成对 RTS 项目“科技目录”（科技研究中心弹窗）的专项整改：

### 1. 缺陷根因排查：
- **头都显示不全（Header Cutoff）根因**：原 `ExpandModalPanel` 锚点 `Rect2(0.120f, 0.100f, 0.760f, 0.800f)` 顶部与大厅指挥官顶栏（Y=76px）重叠，且面板因 `GrowVertical = GrowDirection.Both` 和内部卡片未收缩，在窗口/分辨率受限时向上挤压膨胀，导致标题“科技研究中心”与关闭按钮被推至屏幕上方视口之外。
- **没有滑动框（Missing ScrollContainer）根因**：原 `AddWarehouseSectionPanel` 直接使用 `GridContainer` 挂载于 `VBoxContainer`，设置了 `ClipContents = true` 却完全缺少 `ScrollContainer`。当科技数量增多或屏幕缩放时，无法滑动浏览。

### 2. 核心架构修复落地：
1. **视口与头部安全边界重塑**：
   - `ExpandModalPanel` 锚点调整为 `Rect2(0.080f, 0.110f, 0.840f, 0.820f)`，顶部起始于 Y=0.110（安全避开顶部指挥官信息栏）。
   - 将 `modalPanel.GrowVertical` 锁定为 `GrowDirection.End`，杜绝任何向上挤压视口外部的现象。
   - 增加金黄科技质感顶栏强调线 `AddTopAccentStripe` 与规范化的 `ModalHeader` 容器（高度 34px，字号 20），头部排版从容醒目。
2. **新增科技目录专用滑动框（ScrollContainer）**：
   - `AddWarehouseSectionPanel` 中新增 `WarehouseSectionScroll` 滚动容器，配置 `VerticalScrollMode = ShowAlways` 与 `HorizontalScrollMode = Disabled`。
   - 新增 `StyleScrollBar` 高科技风格滚动条（赛博蓝半透明滑块 `new Color(0.35f, 0.65f, 0.85f, 0.75f)`、深色金属滑轨、圆角 3px），在卡片侧边呈现清晰优雅的滑动条。
3. **科技卡片网格与统计卡片紧凑重构**：
   - 将 `StatCard` 最小尺寸由臃肿的 `(184, 96)` 精简为 `(180, 74)`，释放垂直空间。
   - 修复硬编码的“科技总数 4 项”，改为动态读取 `BattleTechCatalog.All.Length = 10 项`。
   - 科技库网格采用 5 列排布（2 行 x 5 列 = 10 项科技），居中规整对称。

### 3. 实机截图验证地址：
- **Dify/局域网图片**：![实机截图]({screenshot_url_dify})
- **本地地址**：`{screenshot_url_local}`

请 Dify 进行本次修改验证，并针对本次修复的完整性、视觉规范、滑动交互适配及代码健壮性给出审计评价报告。
"""

    print("🚀 正在向 Dify 发送【科技目录修改验证】请求...")
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
        
        report_file = os.path.join(WORKSPACE_DIR, "scratch", "dify_tech_modal_verification.md")
        with open(report_file, "w", encoding="utf-8") as f:
            f.write(full_text)
        print("\n" + "=" * 70)
        print(f"✅ Dify 验证报告已保存至: {report_file}")
    except Exception as e:
        print(f"\n❌ Dify 请求异常: {e}")

if __name__ == "__main__":
    main()
