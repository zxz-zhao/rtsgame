import os
import sys
import argparse
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

# 将当前目录加入模块搜索路径
sys.path.insert(0, str(Path(__file__).parent.resolve()))
from wecom_sender import load_config, save_config, send_full_report, send_markdown, send_image, capture_screenshot

DEFAULT_ITEMS = [
    "👣 **步兵真实行进微步扬尘 (Footstep Dust)**：按实时移速计算步频，脚底交替产生写实自然沙尘。",
    "💥 **步兵开火后坐力微动 (Recoil Impulse)**：射击瞬间枪口微仰、身形微震并在 0.12s 弹性平滑回位。",
    "🟡 **步枪兵右侧高速抛壳 (Brass Cartridge Ejection)**：抛出带翻滚物理角速度的黄铜弹壳与地面轻弹。",
    "🚀 **迫击炮/火箭兵尾喷冲击波 (Rocket Backblast)**：开火时向后方产生锥形火焰与贴地爆破光环。",
    "⚡ **大模型桥接服务升级**：自动突破单次 Token 长度限制，杜绝截断卡死与手工回复继续。"
]

def main():
    parser = argparse.ArgumentParser(description="企业微信群机器人 - 成果与截图推送工具")
    parser.add_argument("--set-webhook", help="快速保存企业微信 Webhook URL 到配置文件")
    parser.add_argument("--title", default="Godot RTS 核心架构成果播报", help="战报标题")
    parser.add_argument("--msg", help="自定义成果说明（多行请用 \\n 分隔）")
    parser.add_argument("--image", help="指定附带发送的图片路径")
    parser.add_argument("--capture", action="store_true", default=True, help="自动截取当前屏幕并随战报发送（默认开启）")
    parser.add_argument("--no-capture", dest="capture", action="store_false", help="不截取屏幕，仅发送文字战报")
    parser.add_argument("--test", action="store_true", help="发送一条测试连通性消息")

    args = parser.parse_args()

    cfg = load_config()

    if args.set_webhook:
        cfg["webhook_url"] = args.set_webhook.strip()
        save_config(cfg)
        print(f"✅ Webhook 已成功配置为:\n{cfg['webhook_url']}")
        sys.exit(0)

    if args.test:
        test_content = "### 🔔 企微机器人连通性测试\n> 状态：<font color=\"info\">成功连接</font>\n> 发送端：`H:\\webbot` 本地服务"
        ok = send_markdown(test_content)
        sys.exit(0 if ok else 1)

    items = DEFAULT_ITEMS
    if args.msg:
        items = [line.strip() for line in args.msg.split("\\n") if line.strip()]

    print(f"[SendReport] 准备推送战报: {args.title} (自动截屏: {args.capture})")
    ok = send_full_report(
        title=args.title,
        items=items,
        image_path=args.image,
        auto_capture=args.capture
    )
    if ok:
        print("🎉 全部内容（战报卡片 + 高清截图）已推送至企业微信群！")
    else:
        print("⚠️ 发送未全部成功，请检查 config.json 中的 Webhook URL 是否正确。")

if __name__ == "__main__":
    main()
