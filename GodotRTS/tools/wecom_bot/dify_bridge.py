# -*- coding: utf-8 -*-
"""
Dify API Bridge for WeCom & Telegram Bot
Connects local Dify agent (http://127.0.0.1:9564) with full visibility into:
- 🧠 Agent Thinking & Reasoning (agent_thought)
- 🛠️ Action / Tool Calls & Observations
- 💻 Code Generation & Streaming Output
"""
import os
import sys
import json
import urllib.request
import urllib.error
from typing import Optional, Callable, Dict, Any

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

DIFY_BASE_URL = os.environ.get("DIFY_BASE_URL", "http://127.0.0.1:9564")
DIFY_API_KEY = os.environ.get("DIFY_API_KEY", "app-VOinOntcv4Ok9Abkq2sml5qC")

def ask_dify(
    query: str, 
    user_id: str = "rts_developer", 
    conversation_id: Optional[str] = None,
    timeout: int = 120,
    on_chunk: Optional[Callable[[str], None]] = None
) -> Dict[str, Any]:
    """
    Sends a query to local Dify agent and collects full text stream response,
    including thoughts, actions, and answer messages.
    """
    url = f"{DIFY_BASE_URL.rstrip('/')}/v1/chat-messages"
    payload = {
        "inputs": {},
        "query": query,
        "response_mode": "streaming",
        "user": user_id
    }
    if conversation_id:
        payload["conversation_id"] = conversation_id
    
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

    collected_chunks = []
    seen_thoughts = set()
    current_conv_id = conversation_id

    def emit(text: str):
        if text:
            collected_chunks.append(text)
            if on_chunk:
                on_chunk(text)

    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            for raw_line in response:
                line = raw_line.decode("utf-8", errors="replace").strip()
                if not line.startswith("data:"):
                    continue
                json_str = line[5:].strip()
                if not json_str:
                    continue
                try:
                    data = json.loads(json_str)
                    event = data.get("event")
                    if not current_conv_id and data.get("conversation_id"):
                        current_conv_id = data.get("conversation_id")

                    # 1. Capture inner agent thoughts (Reasoning / Step-by-step thinking)
                    if event == "agent_thought":
                        thought = (data.get("thought") or "").strip()
                        tool = (data.get("tool") or "").strip()
                        tool_input = data.get("tool_input")
                        observation = (data.get("observation") or "").strip()

                        if thought and thought not in seen_thoughts:
                            seen_thoughts.add(thought)
                            emit(f"\n> 💭 **【架构思考】**：{thought}\n\n")

                        if tool:
                            tool_desc = f"{tool}({json.dumps(tool_input, ensure_ascii=False)})" if tool_input else tool
                            emit(f"\n> 🛠️ **【执行动作】**：调用 `{tool_desc}`...\n\n")

                        if observation:
                            # Truncate long observation for readability
                            obs_short = observation[:300] + ("..." if len(observation) > 300 else "")
                            emit(f"\n> 📋 **【动作反馈】**：{obs_short}\n\n")

                    # 2. Capture streaming answer tokens
                    elif event in ("agent_message", "message"):
                        answer = data.get("answer", "")
                        if answer:
                            emit(answer)

                    # 3. Capture errors
                    elif event == "error":
                        err_msg = data.get("message", "未知错误")
                        emit(f"\n⚠️ **[Dify 错误]**: {err_msg}\n")

                except json.JSONDecodeError:
                    continue
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        emit(f"\n⚠️ **[Dify HTTP {e.code}]**: {body}\n")
    except Exception as e:
        emit(f"\n⚠️ **[Dify 请求失败]**: {e}\n")

    full_answer = "".join(collected_chunks).strip()
    return {
        "answer": full_answer or "（Dify 处理完毕，无返回文本）",
        "conversation_id": current_conv_id
    }

if __name__ == "__main__":
    test_query = sys.argv[1] if len(sys.argv) > 1 else "请为步兵单位编写一个跳弹与后坐力衰减函数"
    print(f"📡 连接本地 Dify ({DIFY_BASE_URL})...\n🎯 提问: {test_query}\n")
    print("=" * 60)
    res = ask_dify(test_query, on_chunk=lambda chunk: print(chunk, end="", flush=True))
    print("\n" + "=" * 60)
    print("✅ 输出结束，会话ID:", res.get("conversation_id"))
