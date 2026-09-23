# -*- coding: utf-8 -*-
"""
Telegram Bot connected to local Dify RTS Architecture Agent
Runs via Telegram Long Polling (zero public IP / webhook required).
"""
import os
import sys
import time
import json
import urllib.request
import urllib.parse
import urllib.error
import threading
from typing import Dict, Any, Optional

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

CONFIG_PATH = os.path.join(os.path.dirname(__file__), "tg_config.json")
DIFY_BASE_URL = "http://127.0.0.1:9564"
DIFY_API_KEY = "app-VOinOntcv4Ok9Abkq2sml5qC"

DEFAULT_CONFIG = {
    "bot_token": "YOUR_TELEGRAM_BOT_TOKEN_HERE",
    "dify_base_url": DIFY_BASE_URL,
    "dify_api_key": DIFY_API_KEY,
    "allowed_users": []  # Empty means allow all
}

def load_config() -> Dict[str, Any]:
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return DEFAULT_CONFIG.copy()

def save_config(cfg: Dict[str, Any]):
    with open(CONFIG_PATH, "w", encoding="utf-8") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=4)

class TelegramBot:
    def __init__(self, token: str):
        self.token = token.strip()
        self.base_url = f"https://api.telegram.org/bot{self.token}"
        self.offset = 0
        self.running = False
        self.bot_info = {}
        self.bot_username = ""
        self.user_conversations = {}  # chat_id -> conversation_id

    def api_call(self, method: str, params: Optional[Dict[str, Any]] = None, timeout: int = 30) -> Dict[str, Any]:
        url = f"{self.base_url}/{method}"
        data = None
        headers = {}
        if params:
            data = json.dumps(params).encode("utf-8")
            headers["Content-Type"] = "application/json"
        
        req = urllib.request.Request(url, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            err = e.read().decode("utf-8", errors="replace")
            print(f"[Telegram Error] HTTP {e.code}: {err}")
            return {"ok": False, "description": err}
        except Exception as e:
            print(f"[Telegram Error] Request failed: {e}")
            return {"ok": False, "description": str(e)}

    def get_me(self) -> bool:
        res = self.api_call("getMe")
        if res.get("ok"):
            self.bot_info = res.get("result", {})
            self.bot_username = self.bot_info.get("username", "").lower()
            print(f"🤖 [TelegramBot] Connected as @{self.bot_username} ({self.bot_info.get('first_name')})")
            return True
        else:
            print(f"❌ [TelegramBot] Failed to connect: {res.get('description')}")
            return False

    def send_chat_action(self, chat_id: int, action: str = "typing"):
        try:
            self.api_call("sendChatAction", {"chat_id": chat_id, "action": action}, timeout=5)
        except Exception:
            pass

    def send_message(self, chat_id: int, text: str, reply_to_message_id: Optional[int] = None) -> bool:
        # Telegram max message length is 4096
        max_len = 3800
        chunks = [text[i:i + max_len] for i in range(0, len(text), max_len)]
        for chunk in chunks:
            payload = {
                "chat_id": chat_id,
                "text": chunk,
                "parse_mode": "Markdown"
            }
            if reply_to_message_id:
                payload["reply_to_message_id"] = reply_to_message_id
            
            res = self.api_call("sendMessage", payload)
            # Fallback if markdown parsing fails
            if not res.get("ok") and "can't parse entities" in str(res.get("description", "")).lower():
                payload.pop("parse_mode", None)
                self.api_call("sendMessage", payload)
        return True

    def query_dify(self, query: str, user_id: str, chat_id: int) -> str:
        try:
            from dify_bridge import ask_dify
            conv_id = self.user_conversations.get(chat_id)
            res = ask_dify(query, user_id=f"tg_{user_id}", conversation_id=conv_id)
            if res.get("conversation_id"):
                self.user_conversations[chat_id] = res["conversation_id"]
            return res.get("answer", "")
        except Exception as e:
            return f"⚠️ 请求本地 Dify 异常: {e}"

    def handle_update(self, update: Dict[str, Any]):
        msg = update.get("message") or update.get("edited_message")
        if not msg:
            return
        
        chat = msg.get("chat", {})
        chat_id = chat.get("id")
        chat_type = chat.get("type", "private")
        text = msg.get("text", "").strip()
        user = msg.get("from", {})
        user_id = str(user.get("id", "unknown"))
        msg_id = msg.get("message_id")

        if not text:
            return

        # Commands
        if text.startswith("/start"):
            welcome = (
                "👋 **你好！我是 Godot 4 RTS 核心架构研发首席专家**\n\n"
                "由本地 Cockpit 高性能算力与 Dify 驱动，专注解决：\n"
                "• 🎮 RTS 视口地平坐标反转与地面视锥裁剪\n"
                "• ⚡ UDP 权威帧同步与断线重连协议\n"
                "• 🏹 战斗单位受击后坐力、子弹抛壳与扬尘特效\n"
                "• 🗺️ 小地图双向映射与无缝平移\n\n"
                "直接发送你的问题，或者在群聊中 @ 我即可提问！"
            )
            self.send_message(chat_id, welcome, reply_to_message_id=msg_id)
            return

        if text.startswith("/clear"):
            self.user_conversations.pop(chat_id, None)
            self.send_message(chat_id, "🧹 上下文已重置，已开启全新对话！", reply_to_message_id=msg_id)
            return

        if text.startswith("/status"):
            status_text = (
                "📊 **系统状态报告**\n\n"
                "• **Dify 引擎**: `http://127.0.0.1:9564` (在线)\n"
                "• **智能体角色**: `Godot 4 RTS 核心架构研发专家`\n"
                f"• **Telegram 机器人**: `@{self.bot_username}`\n"
                f"• **当前对话会话**: `{self.user_conversations.get(chat_id, '新会话')}`"
            )
            self.send_message(chat_id, status_text, reply_to_message_id=msg_id)
            return

        # Check if in group: only respond if @bot or reply to bot
        query = text
        if chat_type in ("group", "supergroup"):
            bot_tag = f"@{self.bot_username}"
            is_reply_to_me = msg.get("reply_to_message", {}).get("from", {}).get("username", "").lower() == self.bot_username
            has_mention = bot_tag in text.lower()
            
            if not has_mention and not is_reply_to_me:
                return  # Ignore messages not targeting this bot
            
            if has_mention:
                query = text.replace(bot_tag, "").strip()

        if not query:
            self.send_message(chat_id, "有什么关于 RTS 架构或 Godot C# 研发的问题需要我协助？", reply_to_message_id=msg_id)
            return

        print(f"[{chat_type.upper()}] From {user.get('first_name')} ({user_id}): {query}")

        # Send typing action in background thread
        stop_typing = threading.Event()
        def typing_loop():
            while not stop_typing.is_set():
                self.send_chat_action(chat_id, "typing")
                time.sleep(4)
        
        t = threading.Thread(target=typing_loop, daemon=True)
        t.start()

        try:
            answer = self.query_dify(query, user_id=user_id, chat_id=chat_id)
        finally:
            stop_typing.set()

        self.send_message(chat_id, answer, reply_to_message_id=msg_id)

    def start_polling(self):
        if not self.get_me():
            return
        
        self.running = True
        print("🚀 [TelegramBot] Long Polling started! Ready to receive messages.")
        
        while self.running:
            try:
                res = self.api_call("getUpdates", {"offset": self.offset, "timeout": 20}, timeout=30)
                if not res.get("ok"):
                    time.sleep(3)
                    continue
                
                updates = res.get("result", [])
                for up in updates:
                    up_id = up.get("update_id", 0)
                    self.offset = max(self.offset, up_id + 1)
                    try:
                        self.handle_update(up)
                    except Exception as e:
                        print(f"[Error handling update {up_id}]: {e}")
            except Exception as e:
                print(f"[Polling loop error]: {e}")
                time.sleep(2)

def main():
    cfg = load_config()
    token = cfg.get("bot_token", "").strip()
    
    if len(sys.argv) > 1 and sys.argv[1] == "--set-token":
        token = sys.argv[2] if len(sys.argv) > 2 else ""
        cfg["bot_token"] = token
        save_config(cfg)
        print(f"✅ Bot token saved to {CONFIG_PATH}")
    
    if not token or "YOUR_TELEGRAM" in token:
        print("=" * 60)
        print("【Telegram Bot 启动指引】")
        print("1. 在 Telegram 搜索 @BotFather 并点击 Start")
        print("2. 发送 /newbot 并按提示输入机器人名字和用户名")
        print("3. 把 BotFather 提供的 HTTP API Token 提供给我或填入 tg_config.json")
        print("   运行示例: python telegram_bot.py --set-token <YOUR_TOKEN>")
        print("=" * 60)
        return

    bot = TelegramBot(token)
    bot.start_polling()

if __name__ == "__main__":
    main()
