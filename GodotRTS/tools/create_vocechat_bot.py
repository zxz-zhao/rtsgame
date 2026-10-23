# -*- coding: utf-8 -*-
"""
自动在 VoceChat 中注入并激活 RTS 专属 AI 协同机器人 (RTS-Dev-Bot)
1. 在 SQLite 数据库注入 Bot 用户与密钥
2. 将 Bot 关联至默认公开群组
3. 重启容器刷新内存缓存
4. 调用 Bot API 发送首条就绪打招呼测试消息
"""

import os
import sys
import sqlite3
import datetime
import subprocess
import time
import urllib.request
import urllib.error

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

DB_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "vocechat", "data", "db", "db.sqlite"))
BOT_NAME = "RTS-Dev-Bot"
BOT_SECRET = "voce_bot_secret_2026"
WEBHOOK_URL = "http://host.docker.internal:5006/webhook"
API_URL = "http://45.205.31.65:3009"

def inject_bot():
    print(f"📦 连接数据库: {DB_PATH}")
    if not os.path.exists(DB_PATH):
        print(f"❌ 数据库文件不存在: {DB_PATH}")
        return False

    now_iso = datetime.datetime.now(datetime.timezone.utc).isoformat()
    now_sql = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M:%S")

    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()

    # 检查是否已存在同名 Bot
    c.execute("SELECT uid FROM user WHERE name = ? OR email = ?", (BOT_NAME, "bot@rts.local"))
    row = c.fetchone()

    if row:
        bot_uid = row[0]
        print(f"🔄 检测到已有 Bot 记录 (uid={bot_uid})，就地更新配置...")
        c.execute("""
            UPDATE user SET 
                is_bot = 1,
                webhook_url = ?,
                bot_secret = ?,
                updated_at = ?
            WHERE uid = ?
        """, (WEBHOOK_URL, BOT_SECRET, now_iso, bot_uid))
    else:
        print("✨ 正在注入全新 Bot 用户...")
        c.execute("""
            INSERT INTO user (
                name, email, gender, language, is_admin, create_by,
                avatar_updated_at, created_at, updated_at, status,
                is_guest, webhook_url, is_bot, bot_secret, admin_guide_completed
            ) VALUES (
                ?, ?, 0, 'en-US', 0, 'admin',
                '1970-01-01 00:00:00', ?, ?, 0,
                0, ?, 1, ?, 1
            )
        """, (BOT_NAME, "bot@rts.local", now_iso, now_iso, WEBHOOK_URL, BOT_SECRET))
        bot_uid = c.lastrowid
        print(f"✅ Bot 用户注入成功: uid={bot_uid}")

    # 注入 / 更新 bot_key 表
    c.execute("SELECT id FROM bot_key WHERE uid = ? OR [key] = ?", (bot_uid, BOT_SECRET))
    bk_row = c.fetchone()
    if bk_row:
        c.execute("UPDATE bot_key SET [key] = ?, name = ? WHERE id = ?", (BOT_SECRET, BOT_NAME, bk_row[0]))
    else:
        c.execute("""
            INSERT INTO bot_key (uid, name, [key], created_at)
            VALUES (?, ?, ?, ?)
        """, (bot_uid, BOT_NAME, BOT_SECRET, now_sql))

    # 加入群组 1 (默认大厅群组)
    c.execute("SELECT id FROM group_user WHERE gid = 1 AND uid = ?", (bot_uid,))
    if not c.fetchone():
        c.execute("INSERT INTO group_user (gid, uid) VALUES (1, ?)", (bot_uid,))

    conn.commit()
    conn.close()
    print("💾 数据库修改已提交！")
    return True


def restart_container():
    print("🔄 重启 vocechat-server 容器以载入新 Bot 凭证...")
    res = subprocess.run(["docker", "restart", "vocechat-server"], capture_output=True, text=True)
    if res.returncode == 0:
        print("✅ 容器重启完成！等待 2 秒服务就绪...")
        time.sleep(2)
        return True
    else:
        print(f"⚠️ 容器重启失败: {res.stderr}")
        return False


def test_bot_send():
    print("📡 测试通过 Bot API 发送开机就绪问候...")
    url = f"{API_URL}/api/bot/send_to_group/1"
    headers = {
        "x-api-key": BOT_SECRET,
        "Content-Type": "text/markdown; charset=utf-8"
    }
    greeting = """👋 **你好！我是 RTS 工业级研发与架构协同机器人 (RTS-Dev-Bot)**

已成功完成全自动部署与接入！
- 🚀 **内存占用**：仅 12MB（相比 Rocket.Chat 降低 98%）
- 🧠 **底层大脑**：已打通 Dify 研发工程师与首席架构师双智能体
- 🧪 **自动化测试**：集成 Godot 4 RTS 14 项工程测试门禁与 60 FPS 录屏

在群里直接艾特我并发送任务（例如：`@RTS-Dev-Bot 优化直升机抗扭力`），我将立刻为您全自动推进研发、测试与报告交付！"""

    try:
        req = urllib.request.Request(
            url,
            data=greeting.encode("utf-8"),
            headers=headers,
            method="POST"
        )
        with urllib.request.urlopen(req, timeout=10) as resp:
            resp_body = resp.read().decode("utf-8")
            print(f"🎉 【测试消息发送成功】: mid={resp_body.strip()}")
            return True
    except Exception as e:
        print(f"❌ 【测试消息发送异常】: {e}")
        return False


if __name__ == "__main__":
    if inject_bot():
        restart_container()
        test_bot_send()
