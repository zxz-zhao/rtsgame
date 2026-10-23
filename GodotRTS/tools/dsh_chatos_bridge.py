# -*- coding: utf-8 -*-
import sys
import os
import json
import time
import uuid
import asyncio
from typing import Dict, Any

import uvicorn
from starlette.applications import Starlette
from starlette.responses import StreamingResponse, JSONResponse
from starlette.routing import Route

# Add tools to path for chatos_driver
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from chatos_driver import chatos_stream_generator

REQ_LOG_PATH = "e:/code/c++/UnityRTS/GodotRTS/tools/last_harness_request.json"

def clean_content(raw: Any) -> str:
    if isinstance(raw, str):
        return raw
    if isinstance(raw, list):
        parts = []
        for item in raw:
            if isinstance(item, dict) and item.get("type") == "text":
                parts.append(item.get("text", ""))
            elif isinstance(item, str):
                parts.append(item)
        return " ".join(parts)
    return str(raw)

async def handle_messages(request):
    try:
        body = await request.json()
        try:
            with open(REQ_LOG_PATH, "w", encoding="utf-8") as f:
                json.dump(body, f, ensure_ascii=False, indent=2)
        except Exception:
            pass
    except Exception:
        return JSONResponse({"error": "invalid json"}, status_code=400)

    model = body.get("model", "deepseek-flash")
    raw_messages = body.get("messages", [])
    system_raw = body.get("system", "")
    
    # Extract dialogue messages
    chatos_messages = []
    
    # Forward system instructions (from dsh-purge or harness)
    if system_raw:
        system_text = clean_content(system_raw).strip()
        if system_text:
            chatos_messages.append({"role": "system", "content": system_text})
    
    # We selectively extract user and assistant messages, keeping the prompt clean
    for m in raw_messages:
        role = m.get("role", "user")
        text = clean_content(m.get("content", ""))
        if not text.strip():
            continue
        chatos_messages.append({"role": role, "content": text})

    # If messages are empty, give a fallback
    if not chatos_messages:
        chatos_messages = [{"role": "user", "content": "你好"}]

    req_payload = {
        "model": "chatos-deepseek",
        "messages": chatos_messages
    }

    msg_id = f"msg_{uuid.uuid4().hex[:20]}"
    
    async def sse_stream():
        # 1. message_start
        start_payload = {
            "type": "message_start",
            "message": {
                "id": msg_id,
                "type": "message",
                "role": "assistant",
                "content": [],
                "model": model,
                "stop_reason": None,
                "stop_sequence": None,
                "usage": {"input_tokens": 10, "output_tokens": 1}
            }
        }
        yield f"event: message_start\ndata: {json.dumps(start_payload)}\n\n"
        
        # 2. content_block_start
        block_start = {
            "type": "content_block_start",
            "index": 0,
            "content_block": {"type": "text", "text": ""}
        }
        yield f"event: content_block_start\ndata: {json.dumps(block_start)}\n\n"
        
        # 3. Stream from ChatOS
        out_tokens = 0
        try:
            async for chunk in chatos_stream_generator(req_payload):
                if chunk.startswith("data: ") and chunk.strip() != "data: [DONE]":
                    try:
                        d = json.loads(chunk[6:].strip())
                        text = d.get("choices", [{}])[0].get("delta", {}).get("content", "")
                        if text:
                            out_tokens += len(text)
                            delta_payload = {
                                "type": "content_block_delta",
                                "index": 0,
                                "delta": {"type": "text_delta", "text": text}
                            }
                            yield f"event: content_block_delta\ndata: {json.dumps(delta_payload, ensure_ascii=False)}\n\n"
                    except Exception:
                        pass
        except Exception as e:
            if out_tokens == 0:
                err_delta = {
                    "type": "content_block_delta",
                    "index": 0,
                    "delta": {"type": "text_delta", "text": f"\n[ChatOS Proxy Error: {e}]"}
                }
                yield f"event: content_block_delta\ndata: {json.dumps(err_delta, ensure_ascii=False)}\n\n"

        # 4. content_block_stop
        block_stop = {"type": "content_block_stop", "index": 0}
        yield f"event: content_block_stop\ndata: {json.dumps(block_stop)}\n\n"
        
        # 5. message_delta
        msg_delta = {
            "type": "message_delta",
            "delta": {"stop_reason": "end_turn", "stop_sequence": None},
            "usage": {"output_tokens": max(1, out_tokens)}
        }
        yield f"event: message_delta\ndata: {json.dumps(msg_delta)}\n\n"
        
        # 6. message_stop
        yield "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n"

    return StreamingResponse(sse_stream(), media_type="text/event-stream")

async def handle_models(request):
    return JSONResponse({
        "data": [
            {"id": "deepseek-flash", "name": "DeepSeek-V41-Flash"},
            {"id": "deepseek-v4-pro", "name": "DeepSeek-V4-Pro"},
            {"id": "deepseek-chat", "name": "deepseek-chat"}
        ]
    })

routes = [
    Route("/messages", handle_messages, methods=["POST"]),
    Route("/v1/messages", handle_messages, methods=["POST"]),
    Route("/models", handle_models, methods=["GET"])
]

app = Starlette(routes=routes)

if __name__ == "__main__":
    port = int(os.environ.get("BRIDGE_PORT", 8002))
    print(f"Starting DSH <-> ChatOS Anthropic Protocol Bridge on port {port}...")
    uvicorn.run(app, host="127.0.0.1", port=port, log_level="warning")
