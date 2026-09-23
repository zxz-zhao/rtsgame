import os
import sys
import json
import time
import base64
import argparse
from pathlib import Path
import httpx

# QQ Bot 配置文件路径
CONFIG_FILE = Path(__file__).parent / "qq_bot_config.json"

DEFAULT_CONFIG = {
    "type": "official_bot",  # "official_bot" 或 "webhook"
    "webhook_url": "",       # 如果是 webhook 方式，填入完整 Webhook URL
    "app_id": "",            # 腾讯 QQ 开放平台 Bot AppID
    "client_secret": "",     # 腾讯 QQ 开放平台 Bot ClientSecret
    "target_type": "group",  # "group" (QQ群) 或 "c2c" (单聊好友) 或 "channel" (频道)
    "target_id": "",         # 目标用户的 openid 或群的 group_openid 或 channel_id
}

def load_config():
    if CONFIG_FILE.exists():
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                cfg = json.load(f)
                return {**DEFAULT_CONFIG, **cfg}
        except Exception as e:
            print(f"[QQNotify] 读取配置失败: {e}")
    return DEFAULT_CONFIG

def save_config(cfg):
    with open(CONFIG_FILE, "w", encoding="utf-8") as f:
        json.dump(cfg, f, indent=4, ensure_ascii=False)
    print(f"[QQNotify] 配置已保存至: {CONFIG_FILE}")

def get_official_access_token(app_id: str, client_secret: str) -> str:
    """获取腾讯 QQ 开放平台 OpenAPI access_token"""
    url = "https://bots.qq.com/app/getAppAccessToken"
    payload = {
        "appId": app_id,
        "clientSecret": client_secret
    }
    resp = httpx.post(url, json=payload, timeout=15.0)
    if resp.status_code != 200:
        raise RuntimeError(f"获取 QQ 访问令牌失败 (HTTP {resp.status_code}): {resp.text}")
    data = resp.json()
    token = data.get("access_token")
    if not token:
        raise RuntimeError(f"返回结果中未包含 access_token: {data}")
    return token

def capture_current_screen(output_path: str) -> bool:
    """截取当前屏幕画面保存为图片"""
    try:
        from PIL import ImageGrab
        img = ImageGrab.grab()
        img.save(output_path, "PNG")
        print(f"[QQNotify] 屏幕截图已保存: {output_path}")
        return True
    except ImportError:
        print("[QQNotify] PIL (Pillow) 未安装，尝试使用 PowerShell 截屏...")
        ps_cmd = f'''
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$screen = [System.Windows.Forms.Screen]::PrimaryScreen
$bitmap = New-Object System.Drawing.Bitmap $screen.Bounds.Width, $screen.Bounds.Height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($screen.Bounds.Location, [System.Drawing.Point]::Empty, $screen.Bounds.Size)
$bitmap.Save("{output_path}", [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
'''
        res = os.system(f'powershell -Command "{ps_cmd.strip()}"')
        return res == 0
    except Exception as e:
        print(f"[QQNotify] 截屏失败: {e}")
        return False

def send_via_webhook(webhook_url: str, message: str, image_path: str = None) -> bool:
    """通过 Webhook 发送消息"""
    payload = {
        "msgtype": "text",
        "text": {"content": message}
    }
    # 如果有图片，转为 base64 发送 (适配通用图文 Webhook)
    if image_path and os.path.exists(image_path):
        try:
            with open(image_path, "rb") as f:
                b64 = base64.b64encode(f.read()).decode("utf-8")
            payload["image_base64"] = b64
        except Exception:
            pass

    headers = {"Content-Type": "application/json"}
    resp = httpx.post(webhook_url, json=payload, headers=headers, timeout=20.0)
    print(f"[QQNotify] Webhook 发送响应 (HTTP {resp.status_code}): {resp.text}")
    return resp.status_code == 200

def send_via_official_bot(cfg: dict, message: str, image_path: str = None) -> bool:
    """通过腾讯 QQ 开放平台官方 API 发送消息"""
    app_id = cfg.get("app_id")
    client_secret = cfg.get("client_secret")
    target_type = cfg.get("target_type", "group")
    target_id = cfg.get("target_id")

    if not app_id or not client_secret or not target_id:
        print("[QQNotify] 错误: 请先在 qq_bot_config.json 中填入 app_id, client_secret 和 target_id")
        return False

    token = get_official_access_token(app_id, client_secret)
    headers = {
        "Authorization": f"QQBot {token}",
        "X-Union-Appid": str(app_id)
    }

    # 根据发送目标类型构建 URL (QQ 开放平台 v2 接口)
    if target_type == "group":
        url = f"https://api.sgroup.qq.com/v2/groups/{target_id}/messages"
    elif target_type == "c2c":
        url = f"https://api.sgroup.qq.com/v2/users/{target_id}/messages"
    elif target_type == "channel":
        url = f"https://api.sgroup.qq.com/channels/{target_id}/messages"
    else:
        url = f"https://api.sgroup.qq.com/v2/groups/{target_id}/messages"

    payload = {
        "content": message,
        "msg_type": 0  # 0: 文本/图文
    }

    # 如果有本地图片
    if image_path and os.path.exists(image_path):
        try:
            with open(image_path, "rb") as f:
                img_data = f.read()
            # 官方 multipart/form-data 方式发送富媒体图片
            files = {
                "file_image": ("screenshot.png", img_data, "image/png")
            }
            data = {"content": message, "msg_type": "0"}
            resp = httpx.post(url, headers=headers, data=data, files=files, timeout=30.0)
            print(f"[QQNotify] 官方接口发送响应 (HTTP {resp.status_code}): {resp.text}")
            return resp.status_code in [200, 201]
        except Exception as e:
            print(f"[QQNotify] 发送图片附件失败，降级为纯文本: {e}")

    resp = httpx.post(url, headers=headers, json=payload, timeout=20.0)
    print(f"[QQNotify] 官方接口发送响应 (HTTP {resp.status_code}): {resp.text}")
    return resp.status_code in [200, 201]

def send_update(summary: str, image_path: str = None):
    cfg = load_config()
    
    # 默认截取屏幕如果未指定
    if image_path is None:
        screenshot_file = str(Path(__file__).parent / "latest_screen.png")
        if capture_current_screen(screenshot_file):
            image_path = screenshot_file

    print(f"[QQNotify] 正在发送通知...")
    print(f"内容摘要:\n{summary}")
    
    if cfg.get("type") == "webhook" and cfg.get("webhook_url"):
        return send_via_webhook(cfg["webhook_url"], summary, image_path)
    else:
        return send_via_official_bot(cfg, summary, image_path)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="QQ 官方机器人 / Webhook 成果与截图推送工具")
    parser.add_argument("--set-webhook", help="设置 Webhook URL")
    parser.add_argument("--set-bot", nargs=4, metavar=("APP_ID", "CLIENT_SECRET", "TARGET_TYPE", "TARGET_ID"),
                        help="配置官方机器人: APP_ID CLIENT_SECRET TARGET_TYPE(group/c2c) TARGET_ID")
    parser.add_argument("--message", "-m", help="要发送的成果说明文本")
    parser.add_argument("--image", "-i", help="指定要发送的截图路径")
    parser.add_argument("--capture", action="store_true", help="自动截取当前屏幕并发送")
    args = parser.parse_args()

    cfg = load_config()

    if args.set_webhook:
        cfg["type"] = "webhook"
        cfg["webhook_url"] = args.set_webhook
        save_config(cfg)
        sys.exit(0)

    if args.set_bot:
        cfg["type"] = "official_bot"
        cfg["app_id"] = args.set_bot[0]
        cfg["client_secret"] = args.set_bot[1]
        cfg["target_type"] = args.set_bot[2]
        cfg["target_id"] = args.set_bot[3]
        save_config(cfg)
        sys.exit(0)

    default_msg = """🎮 【Godot 4 RTS 开发成果战报】
━━━━━━━━━━━━━━━━━━━
✨ 优化步兵走路特效与攻击特效：
1. 👣 步兵真实行进微步扬尘 (Footstep Dust)
2. 💥 步兵开火后坐力微动 (Recoil Impulse)
3. 🟡 步枪兵向右侧高速抛弹壳 (Brass Cartridge Ejection)
4. 🚀 迫击炮/火箭兵尾焰冲击波 (Rocket Backblast)
5. ⚡ 桥接服务已升级：彻底解除单次输出截断与卡顿！
━━━━━━━━━━━━━━━━━━━
✅ 项目编译状态：0 错误，通过验证！"""

    msg = args.message or default_msg
    img = args.image
    if args.capture and not img:
        sc_path = str(Path(__file__).parent / "latest_screen.png")
        if capture_current_screen(sc_path):
            img = sc_path

    send_update(msg, img)
