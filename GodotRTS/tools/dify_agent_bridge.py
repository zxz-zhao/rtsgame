# -*- coding: utf-8 -*-
"""
Dify <-> DeepSeek Harness / New-API 工业级通用智能体桥接服务 (Dify Agent Bridge)
支持协议：
1. OpenAI API: POST /v1/chat/completions (流式 SSE & 非流式)
2. Anthropic API: POST /v1/messages (流式 SSE & 非流式)
3. 模型发现: GET /v1/models, GET /models

下游连接：
- 本地 Dify Docker 智能体集群: http://127.0.0.1:9564/v1/chat-messages
- 默认智能体: 小猫破甲 · 通用助手 (app-81b13284bc9a44fd8c46c90a)
- 架构师智能体: RTS 全局总架构师 (app-VOinOntcv4Ok9Abkq2sml5qC)
"""

import os
import sys
import json
import time
import uuid
import asyncio
from typing import Dict, Any, List, Optional, AsyncGenerator

import httpx
import uvicorn
from starlette.applications import Starlette
from starlette.responses import StreamingResponse, JSONResponse, Response
from starlette.routing import Route
from starlette.middleware import Middleware
from starlette.middleware.cors import CORSMiddleware

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

# 配置项
DIFY_BASE_URL = os.environ.get("DIFY_BASE_URL", "http://127.0.0.1:9564")
DEFAULT_DIFY_KEY = os.environ.get("DIFY_DEFAULT_KEY", "app-81b13284bc9a44fd8c46c90a")
ARCHITECT_DIFY_KEY = os.environ.get("DIFY_ARCH_KEY", "app-VOinOntcv4Ok9Abkq2sml5qC")

# 会话缓存: user_key -> dify_conversation_id
CONVERSATION_CACHE: Dict[str, str] = {}

def get_dify_key(model_name: str, auth_header: str) -> str:
    """根据请求的模型名称或鉴权头挑选对应的 Dify 智能体密钥"""
    if "architect" in model_name.lower():
        return ARCHITECT_DIFY_KEY
    if auth_header.startswith("Bearer app-"):
        return auth_header[7:].strip()
    return DEFAULT_DIFY_KEY

def extract_query_and_history(messages: List[Dict[str, Any]]) -> str:
    """从 OpenAI / Anthropic 格式的 messages 提取提问与上下文"""
    if not messages:
        return "你好"
        
    last_user_msg = ""
    for m in reversed(messages):
        role = m.get("role", "")
        content = m.get("content", "")
        if isinstance(content, list):
            parts = []
            for item in content:
                if isinstance(item, dict) and item.get("type") == "text":
                    parts.append(item.get("text", ""))
                elif isinstance(item, str):
                    parts.append(item)
            content = " ".join(parts)
            
        if role == "user" and content.strip():
            last_user_msg = content.strip()
            break
            
    if last_user_msg:
        return last_user_msg
        
    # Fallback to last content
    last = messages[-1].get("content", "")
    return str(last) if last else "你好"

async def stream_dify_openai(
    dify_key: str,
    query: str,
    model_name: str,
    user_id: str,
    conversation_id: Optional[str] = None
) -> AsyncGenerator[str, None]:
    """将 Dify SSE 流转为 OpenAI /v1/chat/completions 流式 SSE"""
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
        "Authorization": f"Bearer {dify_key}",
        "Content-Type": "application/json; charset=utf-8"
    }

    completion_id = f"chatcmpl-{uuid.uuid4().hex[:16]}"
    created_ts = int(time.time())
    seen_thoughts = set()

    async with httpx.AsyncClient(timeout=180.0, trust_env=False) as client:
        try:
            async with client.stream("POST", url, json=payload, headers=headers) as resp:
                if resp.status_code != 200:
                    err_text = await resp.aread()
                    err_chunk = {
                        "id": completion_id,
                        "object": "chat.completion.chunk",
                        "created": created_ts,
                        "model": model_name,
                        "choices": [{
                            "index": 0,
                            "delta": {"content": f"\n⚠️ [Dify 错误 HTTP {resp.status_code}]: {err_text.decode('utf-8', errors='replace')}"},
                            "finish_reason": "error"
                        }]
                    }
                    yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
                    yield "data: [DONE]\n\n"
                    return

                buffer = ""
                async for chunk in resp.aiter_text():
                    buffer += chunk
                    while "\n" in buffer:
                        line, buffer = buffer.split("\n", 1)
                        line = line.strip()
                        if not line or not line.startswith("data:"):
                            continue
                        json_str = line[5:].strip()
                        if not json_str:
                            continue
                        try:
                            data = json.loads(json_str)
                            event = data.get("event")
                            
                            if data.get("conversation_id"):
                                CONVERSATION_CACHE[user_id] = data.get("conversation_id")

                            # 1. 智能体思考过程 (Agent Thought / Tool Use)
                            if event == "agent_thought":
                                thought = (data.get("thought") or "").strip()
                                tool = (data.get("tool") or "").strip()
                                tool_input = data.get("tool_input")
                                observation = (data.get("observation") or "").strip()
                                
                                thought_parts = []
                                if thought and thought not in seen_thoughts:
                                    seen_thoughts.add(thought)
                                    thought_parts.append(f"\n> 💭 **【思考】**：{thought}\n")
                                if tool:
                                    desc = f"{tool}({json.dumps(tool_input, ensure_ascii=False)})" if tool_input else tool
                                    thought_parts.append(f"\n> 🛠️ **【执行工具】**：`{desc}`\n")
                                if observation:
                                    obs_short = observation[:200] + ("..." if len(observation) > 200 else "")
                                    thought_parts.append(f"\n> 📋 **【反馈】**：{obs_short}\n")
                                    
                                if thought_parts:
                                    thought_text = "".join(thought_parts)
                                    chunk_obj = {
                                        "id": completion_id,
                                        "object": "chat.completion.chunk",
                                        "created": created_ts,
                                        "model": model_name,
                                        "choices": [{
                                            "index": 0,
                                            "delta": {"content": thought_text},
                                            "finish_reason": None
                                        }]
                                    }
                                    yield f"data: {json.dumps(chunk_obj, ensure_ascii=False)}\n\n"

                            # 2. 智能体正式回复 token
                            elif event in ("agent_message", "message"):
                                ans = data.get("answer", "")
                                if ans:
                                    chunk_obj = {
                                        "id": completion_id,
                                        "object": "chat.completion.chunk",
                                        "created": created_ts,
                                        "model": model_name,
                                        "choices": [{
                                            "index": 0,
                                            "delta": {"content": ans},
                                            "finish_reason": None
                                        }]
                                    }
                                    yield f"data: {json.dumps(chunk_obj, ensure_ascii=False)}\n\n"

                            # 3. 会话结束
                            elif event == "message_end":
                                stop_obj = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_name,
                                    "choices": [{
                                        "index": 0,
                                        "delta": {},
                                        "finish_reason": "stop"
                                    }]
                                }
                                yield f"data: {json.dumps(stop_obj)}\n\n"
                                yield "data: [DONE]\n\n"
                                return

                            elif event == "error":
                                err_msg = data.get("message", "未知错误")
                                err_chunk = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_name,
                                    "choices": [{
                                        "index": 0,
                                        "delta": {"content": f"\n⚠️ [Dify 智能体错误]: {err_msg}"},
                                        "finish_reason": "error"
                                    }]
                                }
                                yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
                                yield "data: [DONE]\n\n"
                                return

                        except json.JSONDecodeError:
                            continue

        except Exception as e:
            err_chunk = {
                "id": completion_id,
                "object": "chat.completion.chunk",
                "created": created_ts,
                "model": model_name,
                "choices": [{
                    "index": 0,
                    "delta": {"content": f"\n⚠️ [Dify 桥接异常]: {str(e)}"},
                    "finish_reason": "error"
                }]
            }
            yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
            yield "data: [DONE]\n\n"

    yield "data: [DONE]\n\n"

async def stream_dify_anthropic(
    dify_key: str,
    query: str,
    model_name: str,
    user_id: str,
    conversation_id: Optional[str] = None
) -> AsyncGenerator[str, None]:
    """将 Dify SSE 流转为 Anthropic /v1/messages 流式 SSE"""
    msg_id = f"msg_{uuid.uuid4().hex[:20]}"
    
    # 1. message_start
    start_payload = {
        "type": "message_start",
        "message": {
            "id": msg_id,
            "type": "message",
            "role": "assistant",
            "content": [],
            "model": model_name,
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
    
    # 3. Stream from Dify
    out_tokens = 0
    async for sse_chunk in stream_dify_openai(dify_key, query, model_name, user_id, conversation_id):
        if sse_chunk.startswith("data: ") and sse_chunk.strip() != "data: [DONE]":
            try:
                d = json.loads(sse_chunk[6:].strip())
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

# API 路由实现
async def handle_chat_completions(request):
    auth_header = request.headers.get("Authorization", "").strip()
    try:
        body = await request.json()
    except Exception:
        return JSONResponse({"error": {"message": "Invalid JSON"}}, status_code=400)

    model_name = body.get("model", "deepseek-flash")
    messages = body.get("messages", [])
    stream = body.get("stream", True)
    
    dify_key = get_dify_key(model_name, auth_header)
    query = extract_query_and_history(messages)
    user_id = body.get("user", "dsh_user_1")
    conv_id = CONVERSATION_CACHE.get(user_id)

    if stream:
        return StreamingResponse(
            stream_dify_openai(dify_key, query, model_name, user_id, conv_id),
            media_type="text/event-stream"
        )
    else:
        # 非流式处理：收集所有内容聚合返回
        full_text = []
        async for sse_chunk in stream_dify_openai(dify_key, query, model_name, user_id, conv_id):
            if sse_chunk.startswith("data: ") and sse_chunk.strip() != "data: [DONE]":
                try:
                    d = json.loads(sse_chunk[6:].strip())
                    text = d.get("choices", [{}])[0].get("delta", {}).get("content", "")
                    if text:
                        full_text.append(text)
                except Exception:
                    pass
        content_res = "".join(full_text).strip() or "（Dify 智能体已完成响应）"
        resp_obj = {
            "id": f"chatcmpl-{uuid.uuid4().hex[:16]}",
            "object": "chat.completion",
            "created": int(time.time()),
            "model": model_name,
            "choices": [{
                "index": 0,
                "message": {"role": "assistant", "content": content_res},
                "finish_reason": "stop"
            }],
            "usage": {
                "prompt_tokens": len(query),
                "completion_tokens": len(content_res),
                "total_tokens": len(query) + len(content_res)
            }
        }
        return JSONResponse(resp_obj)

async def handle_messages(request):
    auth_header = request.headers.get("Authorization", "").strip()
    try:
        body = await request.json()
    except Exception:
        return JSONResponse({"error": {"message": "Invalid JSON"}}, status_code=400)

    model_name = body.get("model", "deepseek-flash")
    messages = body.get("messages", [])
    stream = body.get("stream", True)
    
    dify_key = get_dify_key(model_name, auth_header)
    query = extract_query_and_history(messages)
    user_id = body.get("user", "dsh_user_1")
    conv_id = CONVERSATION_CACHE.get(user_id)

    if stream:
        return StreamingResponse(
            stream_dify_anthropic(dify_key, query, model_name, user_id, conv_id),
            media_type="text/event-stream"
        )
    else:
        full_text = []
        async for sse_chunk in stream_dify_openai(dify_key, query, model_name, user_id, conv_id):
            if sse_chunk.startswith("data: ") and sse_chunk.strip() != "data: [DONE]":
                try:
                    d = json.loads(sse_chunk[6:].strip())
                    text = d.get("choices", [{}])[0].get("delta", {}).get("content", "")
                    if text:
                        full_text.append(text)
                except Exception:
                    pass
        content_res = "".join(full_text).strip() or "（Dify 智能体已完成响应）"
        resp_obj = {
            "id": f"msg_{uuid.uuid4().hex[:20]}",
            "type": "message",
            "role": "assistant",
            "model": model_name,
            "content": [{"type": "text", "text": content_res}],
            "stop_reason": "end_turn",
            "usage": {"input_tokens": len(query), "output_tokens": len(content_res)}
        }
        return JSONResponse(resp_obj)

async def handle_models(request):
    return JSONResponse({
        "object": "list",
        "data": [
            {"id": "dify-agent", "object": "model", "name": "Dify Agent (小猫破甲/通用智能体)", "owned_by": "dify"},
            {"id": "deepseek-flash", "object": "model", "name": "Dify DeepSeek Flash Agent", "owned_by": "dify"},
            {"id": "deepseek-chat", "object": "model", "name": "Dify DeepSeek Chat Agent", "owned_by": "dify"},
            {"id": "dify-architect", "object": "model", "name": "Dify RTS Architect Agent", "owned_by": "dify"}
        ]
    })

routes = [
    Route("/v1/chat/completions", handle_chat_completions, methods=["POST"]),
    Route("/chat/completions", handle_chat_completions, methods=["POST"]),
    Route("/v1/messages", handle_messages, methods=["POST"]),
    Route("/messages", handle_messages, methods=["POST"]),
    Route("/v1/models", handle_models, methods=["GET"]),
    Route("/models", handle_models, methods=["GET"])
]

middleware = [
    Middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])
]

app = Starlette(routes=routes, middleware=middleware)

if __name__ == "__main__":
    port = int(os.environ.get("BRIDGE_PORT", 8003))
    print(f"=======================================================")
    print(f">> Dify <-> DeepSeek Harness Agent Bridge Starting...")
    print(f">> Bridge Port: http://127.0.0.1:{port}")
    print(f">> Upstream Dify: {DIFY_BASE_URL}")
    print(f">> Default Agent Key: {DEFAULT_DIFY_KEY}")
    print(f">> OpenAI Endpoint: http://127.0.0.1:{port}/v1/chat/completions")
    print(f">> Anthropic Endpoint: http://127.0.0.1:{port}/v1/messages")
    print(f"=======================================================")
    uvicorn.run(app, host="127.0.0.1", port=port, log_level="warning")
