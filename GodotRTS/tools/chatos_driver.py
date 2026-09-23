import os
import sys
import json
import time
import uuid
import base64
import urllib.parse
from typing import Dict, Any, AsyncGenerator, Optional

import httpx
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.backends import default_backend

CONFIG_PATH = os.path.join(os.path.dirname(__file__), "chatos_config.json")

def load_config() -> Dict[str, Any]:
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            print(f"[ChatOS] Error reading config: {e}")
    return {
        "token": "i/+JaKioKUbULw+3o+rZGN12LfU0wiFTGVaSZYA/OPc=",
        "user_id": "2378416",
        "group_id": "901128216",
        "base_url": "https://siomi.aichat83.com",
        "iv": "hj6cdzrhj72x8ht1",
        "models": {
            "chatos-gpt4o": "gpt-4o",
            "chatos-claude-3-5-sonnet": "claude-3-5-sonnet-20240620",
            "chatos-deepseek": "deepseek-chat"
        }
    }

def decrypt_chatos(data_str: str, iv_str: str = "hj6cdzrhj72x8ht1") -> dict:
    key_str = data_str[:16]
    cipher_b64 = data_str[16:]
    raw_cipher = base64.b64decode(cipher_b64)
    iv = iv_str.encode("utf-8")
    cipher = Cipher(algorithms.AES(key_str.encode("utf-8")), modes.CBC(iv), backend=default_backend())
    decryptor = cipher.decryptor()
    padded = decryptor.update(raw_cipher) + decryptor.finalize()
    unpadder = padding.PKCS7(128).unpadder()
    unpadded = unpadder.update(padded) + unpadder.finalize()
    return json.loads(unpadded.decode("utf-8"))

def format_messages_to_prompt(messages: list) -> str:
    system_parts = []
    dialogue_parts = []
    
    for m in messages:
        role = m.get("role", "user")
        content = m.get("content", "")
        if isinstance(content, list):
            text_parts = []
            for item in content:
                if isinstance(item, dict) and item.get("type") == "text":
                    text_parts.append(item.get("text", ""))
                elif isinstance(item, str):
                    text_parts.append(item)
            content = " ".join(text_parts)
            
        if role == "system":
            system_parts.append(str(content))
        elif role == "user":
            dialogue_parts.append(f"User: {content}")
        elif role == "assistant":
            dialogue_parts.append(f"Assistant: {content}")
            
    if not dialogue_parts and system_parts:
        return "\n\n".join(system_parts)
        
    full_prompt = ""
    if system_parts:
        full_prompt += "[Instructions]\n" + "\n".join(system_parts) + "\n\n"
        
    if len(dialogue_parts) == 1 and dialogue_parts[0].startswith("User: "):
        full_prompt += dialogue_parts[0][6:]
    else:
        full_prompt += "\n".join(dialogue_parts)
        
    return full_prompt.strip()

async def chatos_stream_generator(body: Dict[str, Any]) -> AsyncGenerator[str, None]:
    config = load_config()
    model_req = body.get("model", "chatos-gpt4o")
    upstream_model = config.get("models", {}).get(model_req, "gpt-4o")
    messages = body.get("messages", [])
    prompt = format_messages_to_prompt(messages)
    
    token = config.get("token", "")
    user_id = config.get("user_id", "")
    group_id = config.get("group_id", "")
    base_url = config.get("base_url", "https://siomi.aichat83.com")
    iv = config.get("iv", "hj6cdzrhj72x8ht1")
    
    completion_id = f"chatcmpl-chatos-{uuid.uuid4().hex[:12]}"
    created_ts = int(time.time())
    
    # 1. First HTTP POST to /go/api/steam/see to register question
    see_url = f"{base_url}/go/api/steam/see"
    payload = {
        "version": "1.1.1",
        "os": "pc",
        "channel": "chatos",
        "language": "zh",
        "pars": {
            "user_id": user_id,
            "question": prompt,
            "group_id": group_id,
            "question_id": "",
            "server_id": ""
        }
    }
    
    headers = {
        "Content-Type": "application/json",
        "Authorization": token,
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    }
    
    qid = None
    try:
        async with httpx.AsyncClient(timeout=30.0, trust_env=False) as client:
            resp = await client.post(see_url, json=payload, headers=headers)
            if resp.status_code != 200:
                err_chunk = {
                    "id": completion_id,
                    "object": "chat.completion.chunk",
                    "created": created_ts,
                    "model": model_req,
                    "choices": [{
                        "index": 0,
                        "delta": {"content": f"⚠️ ChatOS HTTP Error {resp.status_code}: {resp.text[:200]}"},
                        "finish_reason": "stop"
                    }]
                }
                yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
                yield "data: [DONE]\n\n"
                return
                
            resp_text = resp.text.strip()
            if resp_text.startswith('"') and resp_text.endswith('"'):
                resp_text = json.loads(resp_text)
            decrypted = decrypt_chatos(resp_text, iv)
            if decrypted.get("retCode") != "ok":
                err_chunk = {
                    "id": completion_id,
                    "object": "chat.completion.chunk",
                    "created": created_ts,
                    "model": model_req,
                    "choices": [{
                        "index": 0,
                        "delta": {"content": f"⚠️ ChatOS Error: {decrypted.get('retMsg', 'unknown error')}"},
                        "finish_reason": "stop"
                    }]
                }
                yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
                yield "data: [DONE]\n\n"
                return
                
            qid = decrypted.get("data", {}).get("question_id")
    except Exception as e:
        err_chunk = {
            "id": completion_id,
            "object": "chat.completion.chunk",
            "created": created_ts,
            "model": model_req,
            "choices": [{
                "index": 0,
                "delta": {"content": f"⚠️ ChatOS Connect Exception: {e}"},
                "finish_reason": "stop"
            }]
        }
        yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
        yield "data: [DONE]\n\n"
        return

    # 2. Connect to SSE stream /go/api/event/see
    event_url = f"{base_url}/go/api/event/see?question_id={qid}&group_id={group_id}&user_id={user_id}&token={urllib.parse.quote(token)}&server_id=&model={upstream_model}"
    
    try:
        async with httpx.AsyncClient(timeout=120.0, trust_env=False) as client:
            async with client.stream("GET", event_url, headers={"Accept": "text/event-stream", "User-Agent": headers["User-Agent"]}) as stream_resp:
                buffer = ""
                async for chunk in stream_resp.aiter_text():
                    buffer += chunk
                    while "\n" in buffer:
                        line, buffer = buffer.split("\n", 1)
                        line = line.strip()
                        if not line or not line.startswith("data:"):
                            continue
                        data_json = line[5:].strip()
                        try:
                            msg_obj = json.loads(data_json)
                            delta_text = msg_obj.get("Data", "")
                            status = msg_obj.get("Status", "")
                            
                            if delta_text:
                                chunk_resp = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_req,
                                    "choices": [{
                                        "index": 0,
                                        "delta": {"content": delta_text},
                                        "finish_reason": None
                                    }]
                                }
                                yield f"data: {json.dumps(chunk_resp, ensure_ascii=False)}\n\n"
                                
                            if status == "stop":
                                stop_resp = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_req,
                                    "choices": [{
                                        "index": 0,
                                        "delta": {},
                                        "finish_reason": "stop"
                                    }]
                                }
                                yield f"data: {json.dumps(stop_resp)}\n\n"
                                yield "data: [DONE]\n\n"
                                return
                        except Exception:
                            pass
                            
    except Exception as e:
        err_chunk = {
            "id": completion_id,
            "object": "chat.completion.chunk",
            "created": created_ts,
            "model": model_req,
            "choices": [{
                "index": 0,
                "delta": {"content": f"\n\n[ChatOS Stream Error: {e}]"},
                "finish_reason": "stop"
            }]
        }
        yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
        yield "data: [DONE]\n\n"
        return

    yield "data: [DONE]\n\n"

async def chatos_non_stream(body: Dict[str, Any]) -> Dict[str, Any]:
    full_text = []
    async for chunk_str in chatos_stream_generator(body):
        if chunk_str.startswith("data:"):
            payload = chunk_str[5:].strip()
            if payload != "[DONE]":
                try:
                    data = json.loads(payload)
                    delta = data.get("choices", [{}])[0].get("delta", {}).get("content", "")
                    if delta:
                        full_text.append(delta)
                except Exception:
                    pass
                    
    content = "".join(full_text)
    model_req = body.get("model", "chatos-gpt4o")
    return {
        "id": f"chatcmpl-chatos-{uuid.uuid4().hex[:12]}",
        "object": "chat.completion",
        "created": int(time.time()),
        "model": model_req,
        "choices": [{
            "index": 0,
            "message": {
                "role": "assistant",
                "content": content
            },
            "finish_reason": "stop"
        }],
        "usage": {
            "prompt_tokens": len(str(body.get("messages", []))) // 4,
            "completion_tokens": len(content) // 4,
            "total_tokens": (len(str(body.get("messages", []))) + len(content)) // 4
        }
    }
