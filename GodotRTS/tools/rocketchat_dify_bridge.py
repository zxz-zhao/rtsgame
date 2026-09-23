# -*- coding: utf-8 -*-
"""
Rocket.Chat <-> Dify 智能桥接服务
接收 Rocket.Chat 的 Webhook 触发消息，调用本地 Dify 平台中的大模型与 Agent：
1. 捕获并解析 Dify 的深度推理思维链 (agent_thought - 思考链路)
2. 捕获 Agent 工具调用与中间观测数据 (tool calls & observations)
3. 整合模型最终深度分析研判结论 (answer)，并以结构化富文本 Markdown 回传给 Rocket.Chat
"""

import os
import sys
import json
import http.server
import socketserver
import urllib.request
import urllib.error
from typing import Optional, Dict, Any

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)
    sys.stderr.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

# 确保本地与局域网请求绕过代理
no_proxy = os.environ.get("NO_PROXY", "")
os.environ["NO_PROXY"] = f"{no_proxy},127.0.0.1,localhost,192.168.1.176,192.168.*".strip(",")
os.environ["no_proxy"] = os.environ["NO_PROXY"]

# Dify 配置 (默认连接本机 Docker 中的 Dify 服务)
DIFY_BASE_URL = os.environ.get("DIFY_BASE_URL", "http://127.0.0.1:9564")
DIFY_API_KEY = os.environ.get("DIFY_API_KEY", "app-VOinOntcv4Ok9Abkq2sml5qC")

# Rocket.Chat REST API 配置 (用于在 Webhook 超时或中断时兜底重发)
RC_API_URL = os.environ.get("RC_API_URL", "http://127.0.0.1:3000")
RC_ADMIN_TOKEN = os.environ.get("RC_ADMIN_TOKEN", "rocket_dify_admin_token_20260923")
RC_ADMIN_USER_ID = os.environ.get("RC_ADMIN_USER_ID", "ykykGuDpmeJqtiEHy")
LAN_HOST = os.environ.get("LAN_HOST", "192.168.1.176")

# Rocket.Chat Webhook 监听端口
PORT = int(os.environ.get("BRIDGE_PORT", "5005"))

# 用户多轮会话上下文缓存 {user_id: conversation_id}
USER_CONVERSATIONS: Dict[str, str] = {}


def upload_image_file(room_id: str, file_path: str, msg: str = "") -> Optional[str]:
    """通过 Rocket.Chat REST API /api/v1/rooms.upload/{roomId} 将本地图片作为原生附件上传"""
    if not os.path.exists(file_path):
        return None
    try:
        import requests
        url = f"{RC_API_URL.rstrip('/')}/api/v1/rooms.upload/{room_id}"
        headers = {
            "X-Auth-Token": RC_ADMIN_TOKEN,
            "X-User-Id": RC_ADMIN_USER_ID,
        }
        filename = os.path.basename(file_path)
        with open(file_path, "rb") as f:
            files = {"file": (filename, f, "image/png")}
            data = {"msg": msg, "description": filename}
            resp = requests.post(url, headers=headers, files=files, data=data, timeout=15)
            if resp.status_code == 200:
                print(f"✅ [Rocket.Chat 图片直传成功]: {filename} -> {room_id}", flush=True)
                return resp.json().get("message", {}).get("_id")
            else:
                print(f"⚠️ [Rocket.Chat 图片上传失败]: HTTP {resp.status_code} - {resp.text}", flush=True)
    except Exception as e:
        print(f"❌ [Rocket.Chat 图片上传异常]: {e}", flush=True)
    return None


def fallback_post_message(room_id: str, text: str):
    """当 Webhook 长连接被 Rocket.Chat 客户端中断时，通过 REST API 兜底直发"""
    try:
        url = f"{RC_API_URL.rstrip('/')}/api/v1/chat.postMessage"
        payload = {
            "roomId": room_id,
            "text": text
        }
        headers = {
            "X-Auth-Token": RC_ADMIN_TOKEN,
            "X-User-Id": RC_ADMIN_USER_ID,
            "Content-Type": "application/json; charset=utf-8"
        }
        req = urllib.request.Request(
            url,
            data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            headers=headers,
            method="POST"
        )
        with urllib.request.urlopen(req, timeout=10) as resp:
            print(f"✅ [Rocket.Chat REST 兜底] 消息已直发至房间 {room_id}！", flush=True)
    except Exception as e:
        print(f"❌ [Rocket.Chat REST 兜底失败]: {e}", flush=True)


def call_dify_agent(query: str, user_id: str) -> str:
    """
    调用 Dify API，收集模型的思维链路 (Reasoning/Thought)、工具调用以及最终分析结果
    """
    url = f"{DIFY_BASE_URL.rstrip('/')}/v1/chat-messages"
    conv_id = USER_CONVERSATIONS.get(user_id)

    payload = {
        "inputs": {},
        "query": query,
        "response_mode": "streaming",
        "user": f"rc_{user_id}"
    }
    if conv_id:
        payload["conversation_id"] = conv_id

    headers = {
        "Authorization": f"Bearer {DIFY_API_KEY}",
        "Content-Type": "application/json; charset=utf-8"
    }

    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers=headers,
        method="POST"
    )

    thought_steps: Dict[str, Dict[str, Any]] = {}
    answers = []

    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            for raw_line in resp:
                line = raw_line.decode("utf-8", errors="replace").strip()
                if not line.startswith("data:"):
                    continue
                json_str = line[5:].strip()
                if not json_str:
                    continue
                try:
                    data = json.loads(json_str)
                    event = data.get("event")

                    if data.get("conversation_id"):
                        USER_CONVERSATIONS[user_id] = data.get("conversation_id")

                    # 1. 抓取模型思考脉络 (Reasoning / 思维链拆解与工具调用)
                    if event == "agent_thought":
                        t_id = str(data.get("id") or data.get("thought_id") or len(thought_steps))
                        thought = (data.get("thought") or "").strip()
                        tool = (data.get("tool") or "").strip()
                        tool_input = data.get("tool_input")
                        observation = (data.get("observation") or "").strip()

                        if t_id not in thought_steps:
                            thought_steps[t_id] = {
                                "thought": thought,
                                "tool": tool,
                                "tool_input": tool_input,
                                "observation": observation
                            }
                        else:
                            if thought:
                                thought_steps[t_id]["thought"] = thought
                            if tool:
                                thought_steps[t_id]["tool"] = tool
                            if tool_input:
                                thought_steps[t_id]["tool_input"] = tool_input
                            if observation:
                                thought_steps[t_id]["observation"] = observation

                    # 2. 抓取模型回答流
                    elif event in ("agent_message", "message"):
                        ans = data.get("answer", "")
                        if ans:
                            answers.append(ans)

                    elif event == "error":
                        err = data.get("message", "未知错误")
                        answers.append(f"\n⚠️ **[Dify 错误]**: {err}")

                except json.JSONDecodeError:
                    continue

    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        return f"⚠️ **[Dify 请求失败 HTTP {e.code}]**: {body}"
    except Exception as e:
        return f"⚠️ **[Dify 通信异常]**: {e}"

    # 结构化排版组装
    reply_sections = []

    # 1. 思考过程与工具调用模块
    thought_texts = []
    tools_called = []
    for step in thought_steps.values():
        t_text = step.get("thought")
        if t_text and t_text not in thought_texts:
            thought_texts.append(t_text)
        tool_name = step.get("tool")
        if tool_name:
            t_desc = f"`{tool_name}`"
            t_inp = step.get("tool_input")
            if t_inp:
                t_desc += f" 参数: `{json.dumps(t_inp, ensure_ascii=False)}`"
            t_entry = f"- 🔧 **调用工具**: {t_desc}"
            obs = step.get("observation")
            if obs:
                obs_preview = obs[:250] + ("..." if len(obs) > 250 else "")
                t_entry += f"\n  > 观测结果: `{obs_preview}`"
            if t_entry not in tools_called:
                tools_called.append(t_entry)

    if thought_texts:
        thought_content = "\n\n".join([f"> {t}" for t in thought_texts])
        reply_sections.append(f"### 💭 **【模型思考脉络 / 分析思路】**\n{thought_content}\n")

    if tools_called:
        tools_content = "\n".join(tools_called)
        reply_sections.append(f"### 🛠️ **【外部工具与数据调用】**\n{tools_content}\n")

    # 2. 核心分析结论
    final_ans = "".join(answers).strip()
    if final_ans:
        reply_sections.append(f"### 📊 **【分析与研判结论】**\n{final_ans}")
    else:
        reply_sections.append("### 📊 **【分析与研判结论】**\n*(模型执行完成，未输出正文文本)*")

    full_reply = "\n---\n".join(reply_sections)

    # 将本地截图 URL 重写为局域网可访问的真实 IP 地址，确保手机/平板/多终端可直接预览图片
    if LAN_HOST:
        full_reply = full_reply.replace("http://localhost:9564/screenshots/", f"http://{LAN_HOST}:9564/screenshots/")
        full_reply = full_reply.replace("http://127.0.0.1:9564/screenshots/", f"http://{LAN_HOST}:9564/screenshots/")

    return full_reply


class RocketChatWebhookHandler(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length)

        try:
            payload = json.loads(body.decode("utf-8"))
        except Exception:
            self.send_response(400)
            self.end_headers()
            return

        # 获取 Rocket.Chat 消息内容
        user_name = payload.get("user_name", "user")
        text = payload.get("text", "").strip()
        bot_flag = payload.get("bot", False)
        room_id = payload.get("channel_id") or payload.get("channel_name") or "GENERAL"

        # 忽略自身机器人的循环消息
        if bot_flag or not text:
            self.send_response(200)
            self.end_headers()
            return

        # 剥离前缀指令 (例如 @dify、/ai 等)
        clean_query = text
        for trigger in ["@dify", "@ai", "/ai", "@bot"]:
            if clean_query.startswith(trigger):
                clean_query = clean_query[len(trigger):].strip()

        print(f"\n📩 [Rocket.Chat] 收到用户 [{user_name}] 的提问:\n{clean_query}", flush=True)

        # 调用 Dify 深度思考与分析
        ai_response = call_dify_agent(clean_query, user_id=user_name)

        # 检测回复中是否包含本地生成的截图路径，若包含则自动将图片文件直传为 Rocket.Chat 原生相册附件
        try:
            import re
            img_matches = re.findall(r'screenshots/([a-zA-Z0-9_\-\.]+\.(?:png|jpg|jpeg))', ai_response, re.IGNORECASE)
            workspace_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
            for img_name in set(img_matches):
                local_img_path = os.path.join(workspace_dir, "screenshots", img_name)
                if os.path.exists(local_img_path):
                    upload_image_file(room_id, local_img_path, f"📸 本地实机生成画面: {img_name}")
        except Exception as img_err:
            print(f"⚠️ [自动图片上传检测异常]: {img_err}", flush=True)

        # 回传给 Rocket.Chat
        reply_payload = {
            "text": ai_response
        }

        resp_bytes = json.dumps(reply_payload, ensure_ascii=False).encode("utf-8")
        try:
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)
            print("✅ [Rocket.Chat] 深度分析与思考链路已通过 Webhook 成功回传！\n", flush=True)
        except Exception as sock_err:
            print(f"⚠️ [Rocket.Chat] Webhook 回传异常 ({sock_err})，自动切换至 REST API 兜底推送...", flush=True)
            fallback_post_message(room_id, ai_response)

    def log_message(self, format, *args):
        # 简化日志输出
        pass


def run():
    print("=" * 60)
    print(f"🚀 Rocket.Chat <-> Dify 智能桥接服务已就绪！")
    print(f"📡 监听本地端口: http://127.0.0.1:{PORT}/webhook")
    print(f"🧠 Dify 接口地址: {DIFY_BASE_URL}")
    print("=" * 60)
    server_address = ("", PORT)
    with socketserver.TCPServer(server_address, RocketChatWebhookHandler) as httpd:
        httpd.serve_forever()


if __name__ == "__main__":
    run()
