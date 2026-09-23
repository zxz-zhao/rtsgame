import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)
    sys.stderr.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json
import time
import uuid
import glob
import copy
import base64
import asyncio
import subprocess
import struct
from datetime import datetime
from typing import Optional, List, Dict, Any, AsyncGenerator

from cryptography.hazmat.primitives.ciphers.aead import AESGCM
import httpx
from starlette.applications import Starlette
from starlette.responses import JSONResponse, StreamingResponse, HTMLResponse, Response
from starlette.routing import Route, Mount
from starlette.staticfiles import StaticFiles
from starlette.middleware import Middleware
from starlette.middleware.cors import CORSMiddleware
import uvicorn

try:
    import chatos_driver
except Exception as e:
    chatos_driver = None

COCKPIT_DIR = r"C:\Users\Administrator\.antigravity_cockpit"
COCKPIT_KEY_FILE = os.path.join(COCKPIT_DIR, "secure-account-storage.key")
ACCOUNTS_DIR = os.path.join(COCKPIT_DIR, "accounts")
LOCAL_ACTIVE_ACCOUNT = os.path.join(os.path.dirname(__file__), "active_account.json")
ACCOUNTS_POOL_FILE = os.path.join(os.path.dirname(__file__), "accounts_pool.json")
PROXY_CANDIDATES = [
    "http://127.0.0.1:7890",
    "http://127.0.0.1:7897",
    "http://127.0.0.1:10808",
    "http://127.0.0.1:10809",
]

def get_best_proxy(exclude: Optional[str] = None) -> str:
    import socket
    candidates = [p for p in PROXY_CANDIDATES if p != exclude]
    for p in candidates:
        try:
            hp = p.split("://")[-1]
            h, port_s = hp.split(":")
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.settimeout(0.2)
                if s.connect_ex((h, int(port_s))) == 0:
                    return p
        except Exception:
            continue
    return candidates[0] if candidates else "http://127.0.0.1:7890"

LOCAL_PROXY = get_best_proxy()
CLOUDCODE_BASE = "https://daily-cloudcode-pa.googleapis.com"
PORT = 8000

_oauth_cfg_file = os.path.join(os.path.dirname(__file__), ".cockpit_oauth.json")
_oauth_cfg = {}
if os.path.exists(_oauth_cfg_file):
    try:
        with open(_oauth_cfg_file, "r", encoding="utf-8") as _fp:
            _oauth_cfg = json.load(_fp)
    except Exception:
        pass

OAUTH_CLIENT_ID = os.environ.get("COCKPIT_CLIENT_ID") or _oauth_cfg.get("client_id", "")
OAUTH_CLIENT_SECRET = os.environ.get("COCKPIT_CLIENT_SECRET") or _oauth_cfg.get("client_secret", "")

# Mapping from external OpenAI model names to internal Google CodeAssist models
MODEL_MAPPING = {
    "gemini-3.8-flash": "gemini-2.5-flash",
    "gemini-3.8-flash-high": "gemini-2.5-flash",
    "gemini-3.1-pro": "gemini-2.5-flash",
    "gemini-pro": "gemini-2.5-flash",
    "gemini-2.5-flash": "gemini-2.5-flash",
    "gemini-2.5-flash-thinking": "gemini-2.5-flash",
    "claude-sonnet-4-6": "gemini-2.5-flash",
    "claude-3-7-sonnet": "gemini-2.5-flash",
    "claude-opus-4-6": "gemini-2.5-flash",
}

FALLBACK_MODELS = {
    "claude-sonnet-4-6": "gemini-2.5-flash",
    "claude-3-7-sonnet": "gemini-2.5-flash",
    "claude-opus-4-6": "gemini-2.5-flash",
}

SUB2API_BASE_URL = "https://www.xn--ai-ku9cy41h.com/v1"
SUB2API_KEY = "sk-a2bfa9091801725daef2bf00f2a2bfb46b32d87ef5eeb541cf1b3fca7233d665"
CODEX_AUTH_JSON = r"C:\Users\Administrator\.codex\auth.json"
if os.path.exists(CODEX_AUTH_JSON):
    try:
        with open(CODEX_AUTH_JSON, "r", encoding="utf-8") as f:
            _cad = json.load(f)
            if _cad.get("OPENAI_API_KEY"):
                SUB2API_KEY = _cad.get("OPENAI_API_KEY")
    except Exception:
        pass

SUB2API_MODELS = {
    "gpt-6-astra",
    "gpt-5.5",
    "gpt-5.6-luna",
    "gpt-5.6-sol",
    "gpt-5.6-terra",
}

AVAILABLE_MODELS = [
    {
        "id": "gemini-3.8-flash",
        "object": "model",
        "created": 1700000000,
        "owned_by": "cockpit-gemini"
    },
    {
        "id": "gemini-3.1-pro",
        "object": "model",
        "created": 1700000000,
        "owned_by": "cockpit-gemini"
    },
    {
        "id": "claude-sonnet-4-6",
        "object": "model",
        "created": 1700000000,
        "owned_by": "cockpit-claude"
    },
    {
        "id": "gpt-6-astra",
        "object": "model",
        "created": 1700000000,
        "owned_by": "sub2api-openai"
    },
    {
        "id": "gpt-5.5",
        "object": "model",
        "created": 1700000000,
        "owned_by": "sub2api-openai"
    },
    {
        "id": "gpt-5.6-luna",
        "object": "model",
        "created": 1700000000,
        "owned_by": "sub2api-openai"
    },
    {
        "id": "gpt-5.6-sol",
        "object": "model",
        "created": 1700000000,
        "owned_by": "sub2api-openai"
    },
    {
        "id": "gpt-5.6-terra",
        "object": "model",
        "created": 1700000000,
        "owned_by": "sub2api-openai"
    },
    {
        "id": "chatos-gpt4o",
        "object": "model",
        "created": 1700000000,
        "owned_by": "chatos-openai"
    },
    {
        "id": "chatos-claude-3-5-sonnet",
        "object": "model",
        "created": 1700000000,
        "owned_by": "chatos-anthropic"
    },
    {
        "id": "chatos-deepseek",
        "object": "model",
        "created": 1700000000,
        "owned_by": "chatos-deepseek"
    },
    {
        "id": "gpt-4o",
        "object": "model",
        "created": 1700000000,
        "owned_by": "chatos-openai"
    },
    {
        "id": "claude-3-5-sonnet",
        "object": "model",
        "created": 1700000000,
        "owned_by": "chatos-anthropic"
    },
    {
        "id": "deepseek-chat",
        "object": "model",
        "created": 1700000000,
        "owned_by": "chatos-deepseek"
    }
]

class AccountPool:
    def __init__(self, accounts_dir: str):
        self.accounts_dir = accounts_dir
        self.accounts: List[Dict[str, Any]] = []
        self.exhausted_emails = set()
        self.current_idx = 0
        self.reload_accounts()

    def mark_account_exhausted(self, email: str):
        if email:
            print(f"[AccountPool] Marking account as quota-exhausted: {email}")
            self.exhausted_emails.add(email)

    def reload_accounts(self):
        self.accounts.clear()
        
        # 1. Primary: Load and decrypt directly from Cockpit storage
        if os.path.exists(COCKPIT_KEY_FILE) and os.path.exists(self.accounts_dir):
            try:
                with open(COCKPIT_KEY_FILE, "rb") as fp:
                    key_raw = base64.b64decode(fp.read().strip())
                aesgcm = AESGCM(key_raw)
                
                cockpit_accs = []
                for f in glob.glob(os.path.join(self.accounts_dir, "*.json")):
                    try:
                        with open(f, "r", encoding="utf-8") as fp:
                            data = json.load(fp)
                        if data.get("algorithm") == "AES-256-GCM":
                            nonce = base64.b64decode(data["nonce"])
                            ct = base64.b64decode(data["ciphertext"])
                            acc = json.loads(aesgcm.decrypt(nonce, ct, None).decode("utf-8"))
                        else:
                            acc = data
                            
                        email = acc.get("email")
                        disabled = acc.get("disabled", False)
                        token_obj = acc.get("token", {})
                        token = token_obj.get("access_token")
                        refresh_token = token_obj.get("refresh_token")
                        expiry_timestamp = token_obj.get("expiry_timestamp", 0)
                        
                        if disabled or not token or not refresh_token:
                            continue
                            
                        quota = acc.get("quota", {})
                        claude_pct = next((m["percentage"] for m in quota.get("models", []) if m.get("name") == "claude-sonnet-4-6"), 0)
                        gemini_pct = next((m["percentage"] for m in quota.get("models", []) if m.get("name") == "gemini-weekly"), 0)
                        
                        cockpit_accs.append({
                            "file": f,
                            "email": email,
                            "token": token,
                            "refresh_token": refresh_token,
                            "expiry_timestamp": expiry_timestamp,
                            "claude_pct": claude_pct,
                            "gemini_pct": gemini_pct,
                            "raw": acc
                        })
                    except Exception as fe:
                        print(f"[Warn] Decrypting {f} failed: {fe}")
                
                if cockpit_accs:
                    # Sort by quota remaining (highest first)
                    cockpit_accs.sort(key=lambda a: (a["claude_pct"], a["gemini_pct"]), reverse=True)
                    self.accounts.extend(cockpit_accs)
                    print(f"[AccountPool] Decrypted and loaded {len(cockpit_accs)} live accounts directly from Cockpit!")
            except Exception as e:
                print(f"[AccountPool] Error syncing from Cockpit: {e}")

        # 2. Secondary fallback: Load accounts_pool.json
        if not self.accounts and os.path.exists(ACCOUNTS_POOL_FILE):
            try:
                with open(ACCOUNTS_POOL_FILE, "r", encoding="utf-8") as fp:
                    pool_data = json.load(fp)
                    if isinstance(pool_data, list):
                        for acc_item in pool_data:
                            token = acc_item.get("token", {}).get("access_token")
                            refresh_token = acc_item.get("token", {}).get("refresh_token")
                            expiry_timestamp = acc_item.get("token", {}).get("expiry_timestamp", 0)
                            email = acc_item.get("email", "account@gmail.com")
                            if token:
                                self.accounts.append({
                                    "file": ACCOUNTS_POOL_FILE,
                                    "email": email,
                                    "token": token,
                                    "refresh_token": refresh_token,
                                    "expiry_timestamp": expiry_timestamp,
                                    "raw": acc_item
                                })
                        print(f"[AccountPool] Loaded {len(self.accounts)} accounts from {ACCOUNTS_POOL_FILE}")
            except Exception as e:
                print(f"[Warn] Failed to read {ACCOUNTS_POOL_FILE}: {e}")

        # 3. Tertiary fallback: Load active_account.json
        if not self.accounts and os.path.exists(LOCAL_ACTIVE_ACCOUNT):
            try:
                with open(LOCAL_ACTIVE_ACCOUNT, "r", encoding="utf-8") as fp:
                    data = json.load(fp)
                    token = data.get("token", {}).get("access_token")
                    refresh_token = data.get("token", {}).get("refresh_token")
                    expiry_timestamp = data.get("token", {}).get("expiry_timestamp", 0)
                    if token:
                        self.accounts.append({
                            "file": LOCAL_ACTIVE_ACCOUNT,
                            "email": data.get("email", "primary@gmail.com"),
                            "token": token,
                            "refresh_token": refresh_token,
                            "expiry_timestamp": expiry_timestamp,
                            "raw": data
                        })
                        print(f"[AccountPool] Loaded primary account from {LOCAL_ACTIVE_ACCOUNT}")
            except Exception as e:
                print(f"[Warn] Failed to read local active_account.json: {e}")

        print(f"[AccountPool] Total {len(self.accounts)} active accounts ready.")

    async def refresh_account(self, acc: Dict[str, Any]) -> bool:
        refresh_token = acc.get("refresh_token")
        if not refresh_token:
            print(f"[AccountPool] Account {acc.get('email')} has no refresh_token.")
            return False

        data = {
            "client_id": OAUTH_CLIENT_ID,
            "client_secret": OAUTH_CLIENT_SECRET,
            "refresh_token": refresh_token,
            "grant_type": "refresh_token"
        }

        try:
            async with httpx.AsyncClient(proxy=get_best_proxy(), trust_env=False, timeout=httpx.Timeout(20.0, connect=10.0)) as client:
                resp = await client.post("https://oauth2.googleapis.com/token", data=data)
                if resp.status_code == 200:
                    res_json = resp.json()
                    new_token = res_json["access_token"]
                    expires_in = res_json["expires_in"]
                    acc["token"] = new_token
                    acc["expiry_timestamp"] = int(time.time()) + expires_in

                    # Save back to accounts_pool.json or single file
                    try:
                        if acc.get("file") == ACCOUNTS_POOL_FILE and os.path.exists(ACCOUNTS_POOL_FILE):
                            with open(ACCOUNTS_POOL_FILE, "r", encoding="utf-8") as fp:
                                pool_list = json.load(fp)
                            for item in pool_list:
                                if item.get("email") == acc.get("email"):
                                    if "token" not in item:
                                        item["token"] = {}
                                    item["token"]["access_token"] = new_token
                                    item["token"]["expiry_timestamp"] = acc["expiry_timestamp"]
                            with open(ACCOUNTS_POOL_FILE, "w", encoding="utf-8") as fp:
                                json.dump(pool_list, fp, indent=2, ensure_ascii=False)
                        elif acc.get("file") and os.path.exists(acc.get("file")):
                            raw = acc.get("raw", {})
                            if "token" in raw:
                                raw["token"]["access_token"] = new_token
                                raw["token"]["expiry_timestamp"] = acc["expiry_timestamp"]
                    except Exception as fe:
                        print(f"[Warn] Failed to write updated token: {fe}")

                    print(f"[AccountPool] Successfully refreshed token for {acc.get('email')}, expires in {expires_in}s.")
                    return True
                else:
                    print(f"[AccountPool] Refresh token failed for {acc.get('email')}: {resp.status_code} {resp.text[:200]}")
                    return False
        except Exception as e:
            print(f"[AccountPool] Exception refreshing token for {acc.get('email')}: {e}")
            return False

    async def get_valid_account(self, exclude_emails: Optional[List[str]] = None) -> Optional[Dict[str, Any]]:
        if not self.accounts:
            self.reload_accounts()
        if not self.accounts:
            return None

        excludes = set(self.exhausted_emails)
        if exclude_emails:
            excludes.update(exclude_emails)

        candidates = [a for a in self.accounts if a.get("email") not in excludes]
        if not candidates:
            # If all candidates exhausted, reset to allow retry
            print("[AccountPool] All accounts marked exhausted. Resetting exhausted pool.")
            self.exhausted_emails.clear()
            candidates = self.accounts

        for _ in range(len(candidates)):
            acc = candidates[self.current_idx % len(candidates)]
            self.current_idx += 1

            # Check if token is expired or expiring in next 5 minutes
            now = int(time.time())
            if acc.get("expiry_timestamp", 0) - now < 300:
                print(f"[AccountPool] Token for {acc.get('email')} is expired or expiring soon, auto-refreshing...")
                success = await self.refresh_account(acc)
                if success:
                    return acc
            else:
                return acc

        if candidates:
            acc = candidates[0]
            if acc.get("expiry_timestamp", 0) - int(time.time()) < 300:
                if await self.refresh_account(acc):
                    return acc
            return acc
        return None
        return None

pool = AccountPool(ACCOUNTS_DIR)

async def health(request):
    if len(pool.accounts) == 0:
        pool.reload_accounts()
    acc_emails = [a.get("email") for a in pool.accounts]
    return JSONResponse({
        "status": "ok", 
        "provider": "cockpit-bridge", 
        "accounts_count": len(pool.accounts),
        "accounts": acc_emails
    })

async def list_models(request):
    return JSONResponse({"object": "list", "data": AVAILABLE_MODELS})

def parse_image_to_inline_data(img_url: str) -> Optional[Dict[str, Any]]:
    if not img_url:
        return None
    # 1. Base64 data URI: data:image/png;base64,xxxx
    if img_url.startswith("data:"):
        try:
            header, data = img_url.split(";base64,", 1)
            mime_type = header.replace("data:", "").strip()
            return {
                "inlineData": {
                    "mimeType": mime_type or "image/png",
                    "data": data.strip()
                }
            }
        except Exception as e:
            print(f"[Bridge] Error parsing base64 data URI: {e}")
            return None
    # 2. HTTP/HTTPS URL
    elif img_url.startswith("http://") or img_url.startswith("https://"):
        try:
            fetch_url = img_url.replace("host.docker.internal", "127.0.0.1")
            with httpx.Client(proxy=None, trust_env=False, timeout=15.0) as client:
                resp = client.get(fetch_url)
                if resp.status_code == 200:
                    mime = resp.headers.get("content-type", "image/png").split(";")[0].strip()
                    b64_data = base64.b64encode(resp.content).decode("utf-8")
                    return {
                        "inlineData": {
                            "mimeType": mime or "image/png",
                            "data": b64_data
                        }
                    }
                else:
                    print(f"[Bridge] Failed to fetch image {fetch_url}: status {resp.status_code}")
        except Exception as e:
            print(f"[Bridge] Error fetching image URL {img_url}: {e}")
            return None
    # 3. Plain base64 string
    elif len(img_url) > 100:
        return {
            "inlineData": {
                "mimeType": "image/png",
                "data": img_url.strip()
            }
        }
    return None

def merge_consecutive_text_parts(parts: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    merged: List[Dict[str, Any]] = []
    for p in parts:
        if "text" in p and merged and "text" in merged[-1]:
            merged[-1]["text"] = f"{merged[-1]['text']}\n\n{p['text']}".strip()
        else:
            merged.append(copy.deepcopy(p))
    return merged

def transform_messages(messages: List[Dict[str, Any]]):
    system_parts = []
    raw_contents = []

    for msg in messages:
        role = msg.get("role", "user")
        content = msg.get("content", "")
        
        msg_image_parts = []
        text_parts = []

        if isinstance(content, list):
            for c in content:
                if not isinstance(c, dict):
                    continue
                c_type = c.get("type")
                if c_type == "text":
                    t = c.get("text", "")
                    if t:
                        text_parts.append(t)
                elif c_type in ["image_url", "input_image"]:
                    img_info = c.get("image_url", "")
                    if isinstance(img_info, dict):
                        img_url = img_info.get("url", "")
                    else:
                        img_url = str(img_info)
                    inline_part = parse_image_to_inline_data(img_url)
                    if inline_part:
                        msg_image_parts.append(inline_part)
        elif content is not None:
            text = str(content).strip()
            if text:
                text_parts.append(text)

        # Handle tool calls if assistant made them
        if msg.get("tool_calls"):
            calls_desc = []
            for tc in msg.get("tool_calls", []):
                fn = tc.get("function", {})
                name = fn.get("name", "tool")
                args = fn.get("arguments", "")
                calls_desc.append(f"[Call tool: {name}({args})]")
            call_text = "\n".join(calls_desc)
            text_parts.append(call_text)

        combined_text = "\n".join(text_parts).strip()

        # 1. System instructions
        if role == "system":
            if combined_text:
                system_parts.append(combined_text)
            continue

        # 2. Tool / function outputs -> mapped to user
        if role in ["tool", "function"]:
            tool_name = msg.get("name") or msg.get("tool_call_id") or "tool"
            tool_text = f"[Tool Output ({tool_name})]:\n{combined_text or 'Done'}"
            raw_contents.append(("user", [{"text": tool_text}]))
            continue

        final_parts = []
        if combined_text:
            final_parts.append({"text": combined_text})
        if msg_image_parts:
            final_parts.extend(msg_image_parts)
        if not final_parts:
            final_parts = [{"text": "..."}]

        # 3. User messages
        if role == "user":
            raw_contents.append(("user", final_parts))
            continue

        # 4. Assistant / model messages
        if role in ["assistant", "model"]:
            raw_contents.append(("model", final_parts))
            continue

    # Merge consecutive messages of the same role to strictly satisfy alternating user/model requirements
    contents = []
    for r, parts in raw_contents:
        if contents and contents[-1]["role"] == r:
            contents[-1]["parts"].extend(parts)
        else:
            contents.append({"role": r, "parts": list(parts)})

    # Merge consecutive text parts within each message
    for item in contents:
        item["parts"] = merge_consecutive_text_parts(item["parts"])

    # Ensure conversation starts with 'user'
    if contents and contents[0]["role"] == "model":
        contents.insert(0, {"role": "user", "parts": [{"text": "Hello"}]})

    # Ensure at least one message exists
    if not contents:
        contents.append({"role": "user", "parts": [{"text": "Hello"}]})

    system_instruction = None
    if system_parts:
        system_instruction = {
            "role": "user",
            "parts": [{"text": "\n\n".join(system_parts)}]
        }

    return contents, system_instruction

def is_meaningful_repetition_line(line: str) -> bool:
    stripped = line.strip()
    if len(stripped) <= 8:
        return False
    # If line contains only code punctuation, delimiters, or comment slashes, ignore it
    if all(c in "}{)();[],/*-+=#:'\" \t" for c in stripped):
        return False
    return True

def deduplicate_repetitive_text(text: str) -> str:
    lines = text.splitlines(keepends=True)
    stripped = [l.strip() for l in lines if is_meaningful_repetition_line(l)]
    for block_size in (1, 2, 3):
        max_repeats = 4
        if len(stripped) >= block_size * max_repeats:
            for start_idx in range(len(stripped) - block_size * max_repeats + 1):
                blocks = [stripped[start_idx + i * block_size : start_idx + (i + 1) * block_size] for i in range(max_repeats)]
                if all(b == blocks[0] for b in blocks) and sum(len(x) for x in blocks[0]) >= 25:
                    rep_sample = blocks[0]
                    count = 0
                    for idx, line in enumerate(lines):
                        if line.strip() in rep_sample:
                            count += 1
                            if count >= len(rep_sample) * 2:
                                return ''.join(lines[:idx + 1]).rstrip() + "\n"
    return text

def check_repetition(lines: List[str], max_repeats: int = 4) -> bool:
    meaningful = [l for l in lines if is_meaningful_repetition_line(l)]
    for block_size in (1, 2, 3):
        if len(meaningful) >= block_size * max_repeats:
            blocks = [meaningful[i:i+block_size] for i in range(len(meaningful) - block_size * max_repeats, len(meaningful), block_size)]
            if len(blocks) == max_repeats and all(b == blocks[0] for b in blocks):
                total_len = sum(len(x) for x in blocks[0])
                if total_len >= 25:
                    return True
    return False

async def chat_completions(request):
    try:
        body = await request.json()
        try:
            fname = "last_stream_request.json" if body.get("stream") else "last_title_request.json"
            with open(f"e:/code/c++/UnityRTS/GodotRTS/scratch/{fname}", "w", encoding="utf-8") as f:
                json.dump(body, f, ensure_ascii=False, indent=2)
        except Exception:
            pass
    except Exception:
        return JSONResponse({"error": {"message": "Invalid JSON"}}, status_code=400)

    model_req = body.get("model", "gemini-2.5-flash")
    target_model = MODEL_MAPPING.get(model_req, "gemini-2.5-flash")
    messages = body.get("messages", [])
    stream = body.get("stream", False)
    print(f"[Bridge Request] model={model_req}, stream={stream}, num_msgs={len(messages)}, last_role={messages[-1].get('role') if messages else None}, last_content={str(messages[-1].get('content'))[:100] if messages else None}", flush=True)

    # 1. 检查是否为 Sub2API / 大王AI 前沿模型
    if model_req in SUB2API_MODELS or model_req.startswith("sub2api-") or model_req.startswith("dawang-"):
        actual_model = model_req.replace("sub2api-", "").replace("dawang-", "")
        forward_body = dict(body)
        forward_body["model"] = actual_model
        
        async def sub2api_stream_generator():
            headers = {
                "Authorization": f"Bearer {SUB2API_KEY}",
                "Content-Type": "application/json"
            }
            async with httpx.AsyncClient(timeout=180.0, trust_env=False) as client:
                async with client.stream("POST", f"{SUB2API_BASE_URL}/chat/completions", json=forward_body, headers=headers) as upstream_resp:
                    async for chunk in upstream_resp.aiter_raw():
                        yield chunk

        if stream:
            headers = {
                "Cache-Control": "no-cache, no-transform",
                "Connection": "keep-alive",
                "Content-Type": "text/event-stream; charset=utf-8",
                "X-Accel-Buffering": "no"
            }
            return StreamingResponse(sub2api_stream_generator(), media_type="text/event-stream", headers=headers)
        else:
            headers = {
                "Authorization": f"Bearer {SUB2API_KEY}",
                "Content-Type": "application/json"
            }
            async with httpx.AsyncClient(timeout=180.0, trust_env=False) as client:
                resp = await client.post(f"{SUB2API_BASE_URL}/chat/completions", json=forward_body, headers=headers)
                return Response(content=resp.content, status_code=resp.status_code, media_type="application/json")

    # 2. 检查是否为 ChatOS 常用别名映射
    chatos_models_alias = {
        "gpt-4o": "chatos-gpt4o",
        "claude-3-5-sonnet": "chatos-claude-3-5-sonnet",
        "deepseek-chat": "chatos-deepseek"
    }
    if model_req in chatos_models_alias:
        body["model"] = chatos_models_alias[model_req]
        model_req = body["model"]

    if model_req.startswith("chatos-") and chatos_driver:
        if stream:
            headers = {
                "Cache-Control": "no-cache, no-transform",
                "Connection": "keep-alive",
                "Content-Type": "text/event-stream; charset=utf-8",
                "X-Accel-Buffering": "no"
            }
            return StreamingResponse(chatos_driver.chatos_stream_generator(body), media_type="text/event-stream", headers=headers)
        else:
            resp_data = await chatos_driver.chatos_non_stream(body)
            return JSONResponse(resp_data)

    acc = await pool.get_valid_account()
    if not acc or not acc.get("token"):
        pool.reload_accounts()
        acc = await pool.get_valid_account()
    if not acc or not acc.get("token"):
        return JSONResponse({"error": {"message": "No active Cockpit account token available"}}, status_code=500)

    contents, system_instruction = transform_messages(messages)

    inner_request = {
        "contents": contents,
        "generationConfig": {}
    }
    if system_instruction:
        inner_request["systemInstruction"] = system_instruction
    if "temperature" in body:
        temp = float(body["temperature"])
        if temp < 0.5:
            temp = 0.65
        inner_request["generationConfig"]["temperature"] = temp
    else:
        inner_request["generationConfig"]["temperature"] = 0.7

    inner_request["generationConfig"]["topP"] = 0.95
    inner_request["generationConfig"]["topK"] = 40

    # Ensure maxOutputTokens is generous enough for complete C# scripts (at least 8192)
    req_max_tokens = body.get("max_tokens")
    if req_max_tokens:
        try:
            inner_request["generationConfig"]["maxOutputTokens"] = max(int(req_max_tokens), 8192)
        except Exception:
            inner_request["generationConfig"]["maxOutputTokens"] = 8192
    else:
        inner_request["generationConfig"]["maxOutputTokens"] = 8192

    payload = {
        "model": target_model,
        "requestId": str(uuid.uuid4()),
        "request": inner_request
    }

    url = f"{CLOUDCODE_BASE}/v1internal:streamGenerateContent?alt=sse"

    def get_headers(token: str):
        return {
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
            "User-Agent": "antigravity/2.5.5 windows/amd64 google-api-nodejs-client/10.3.0"
        }

    completion_id = f"chatcmpl-{uuid.uuid4().hex[:12]}"
    created_ts = int(time.time())

    acc_ref = {"acc": acc}

    async def sse_generator() -> AsyncGenerator[str, None]:
        max_attempts = 6
        streamed_full_text = ""
        current_acc = acc_ref["acc"]
        current_token = current_acc["token"]
        current_proxy = get_best_proxy()
        auto_continue_count = 0
        max_auto_continues = 4
        current_payload = copy.deepcopy(payload)

        # Emit initial role chunk so Dify mounts UI bubble instantly with zero perceived lag
        initial_chunk = {
            "id": completion_id,
            "object": "chat.completion.chunk",
            "created": created_ts,
            "model": model_req,
            "choices": [{"index": 0, "delta": {"role": "assistant"}, "finish_reason": None}]
        }
        yield f"data: {json.dumps(initial_chunk)}\n\n"

        while True:
            round_received_chunk = False
            round_finished_with_max_tokens = False
            round_finished_normally = False

            for attempt in range(max_attempts):
                headers = get_headers(current_token)
                print(f"[Bridge] Calling Cloudcode streamGenerateContent (attempt {attempt+1}, model={current_payload.get('model')})...", flush=True)
                try:
                    async with httpx.AsyncClient(proxy=current_proxy, trust_env=False, timeout=httpx.Timeout(120.0, connect=20.0)) as client:
                        async with client.stream("POST", url, headers=headers, json=current_payload) as response:
                            print(f"[Bridge] Cloudcode HTTP response status: {response.status_code}", flush=True)
                            if response.status_code == 401 and attempt == 0:
                                print(f"[Bridge] Got 401, auto-refreshing token...")
                                if await pool.refresh_account(current_acc):
                                    current_token = current_acc["token"]
                                    continue

                            if response.status_code == 429:
                                err_text = await response.aread()
                                err_msg = err_text.decode("utf-8", errors="replace")
                                print(f"[Bridge] Account {current_acc.get('email')} hit 429 quota! Details: {err_msg[:200]}")
                                pool.mark_account_exhausted(current_acc.get("email"))
                                next_acc = await pool.get_valid_account(exclude_emails=[current_acc.get("email")])
                                if next_acc and next_acc.get("email") != current_acc.get("email"):
                                    print(f"[Bridge] Auto-switched to fallback account: {next_acc.get('email')}, retrying stream generation...")
                                    current_acc = next_acc
                                    acc_ref["acc"] = next_acc
                                    current_token = current_acc["token"]
                                    await asyncio.sleep(0.1)
                                    continue
                                elif current_payload.get("model") in FALLBACK_MODELS:
                                    fallback_model = FALLBACK_MODELS[current_payload["model"]]
                                    print(f"[Bridge] All accounts quota exhausted for {current_payload['model']}. Auto-falling back to {fallback_model}...")
                                    current_payload["model"] = fallback_model
                                    pool.exhausted_emails.clear()
                                    next_acc = await pool.get_valid_account()
                                    if next_acc:
                                        current_acc = next_acc
                                        acc_ref["acc"] = next_acc
                                        current_token = current_acc["token"]
                                        await asyncio.sleep(0.1)
                                        continue

                            if response.status_code == 503:
                                if attempt < max_attempts - 1:
                                    print(f"[Bridge] Status {response.status_code}, quick retrying in 0.3s...")
                                    await asyncio.sleep(0.3)
                                    continue

                            if response.status_code != 200:
                                err_text = await response.aread()
                                err_msg = err_text.decode("utf-8", errors="replace")
                                print(f"[Bridge] Status {response.status_code}: {err_msg[:300]}")
                                if attempt < max_attempts - 1 and not round_received_chunk:
                                    await asyncio.sleep(0.3)
                                    continue
                                
                                err_clean = err_msg.strip()
                                try:
                                    err_json = json.loads(err_clean)
                                    err_clean = err_json.get("error", {}).get("message", err_clean)
                                except Exception:
                                    pass
                                error_display = f"\n\n⚠️ **生成失败 (HTTP {response.status_code})**: {err_clean[:200]}"
                                err_chunk = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_req,
                                    "choices": [
                                        {
                                            "index": 0,
                                            "delta": {"role": "assistant", "content": error_display},
                                            "finish_reason": "stop"
                                        }
                                    ]
                                }
                                yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
                                yield "data: [DONE]\n\n"
                                return

                            buffer = ""
                            async for chunk in response.aiter_text():
                                buffer += chunk.replace("\r\n", "\n")
                                while "\n\n" in buffer:
                                    event_block, buffer = buffer.split("\n\n", 1)
                                    event_block = event_block.strip()
                                    if not event_block:
                                        continue
                                    data_lines = []
                                    for eline in event_block.split("\n"):
                                        eline = eline.strip()
                                        if eline.startswith("data:"):
                                            data_lines.append(eline[5:].strip())
                                        elif eline and not eline.startswith("event:") and not eline.startswith("id:"):
                                            data_lines.append(eline)
                                    if not data_lines:
                                        continue
                                    json_str = "\n".join(data_lines).strip()
                                    if not json_str or json_str == "[DONE]":
                                        continue
                                    try:
                                        data = json.loads(json_str)
                                    except Exception as json_err:
                                        print(f"[Bridge SSE JSON Error]: {json_err} on raw payload: {json_str[:80]}", flush=True)
                                        continue

                                    candidates = data.get("response", {}).get("candidates", [])
                                    if not candidates:
                                        print(f"[Bridge SSE No Candidates] keys={list(data.keys())}", flush=True)
                                    for cand in candidates:
                                        parts = cand.get("content", {}).get("parts", [])
                                        for p in parts:
                                            text = p.get("text", "")
                                            if not text:
                                                continue
                                            round_received_chunk = True
                                            is_thought = p.get("thought", False)
                                            if is_thought:
                                                delta = {"reasoning_content": text}
                                            else:
                                                delta = {"content": text}
                                            print(f"[Bridge SSE Chunk] text={repr(text[:40])}, is_thought={is_thought}", flush=True)

                                            chunk_resp = {
                                                "id": completion_id,
                                                "object": "chat.completion.chunk",
                                                "created": created_ts,
                                                "model": model_req,
                                                "choices": [
                                                    {
                                                        "index": 0,
                                                        "delta": delta,
                                                        "finish_reason": None
                                                    }
                                                ]
                                            }
                                            yield f"data: {json.dumps(chunk_resp, ensure_ascii=False)}\n\n"
                                            
                                            streamed_full_text += text
                                            streamed_lines = [l.strip() for l in streamed_full_text.splitlines() if l.strip()]
                                            if check_repetition(streamed_lines):
                                                print(f"[Bridge] Repetition loop detected in stream, gracefully terminating.")
                                                stop_resp = {
                                                    "id": completion_id,
                                                    "object": "chat.completion.chunk",
                                                    "created": created_ts,
                                                    "model": model_req,
                                                    "choices": [
                                                        {
                                                            "index": 0,
                                                            "delta": {},
                                                            "finish_reason": "stop"
                                                        }
                                                    ]
                                                }
                                                yield f"data: {json.dumps(stop_resp)}\n\n"
                                                yield "data: [DONE]\n\n"
                                                return

                                        finish = cand.get("finishReason")
                                        if finish:
                                            print(f"[Bridge Cloudcode Finish] finish={finish}, cand_keys={list(cand.keys())}", flush=True)
                                        if finish == "MAX_TOKENS":
                                            round_finished_with_max_tokens = True
                                        elif finish:
                                            round_finished_normally = True

                            if round_finished_with_max_tokens:
                                break
                            if round_finished_normally or round_received_chunk:
                                break

                except (asyncio.CancelledError, GeneratorExit):
                    print(f"[Bridge] Stream cancelled by client, cleanly exiting.", flush=True)
                    return
                except Exception as e:
                    err_name = type(e).__name__
                    err_str = str(e).strip()
                    print(f"[Bridge] Stream attempt {attempt+1} exception: {err_name} ({err_str})", flush=True)
                    if not round_received_chunk and attempt < max_attempts - 1:
                        old_p = current_proxy
                        current_proxy = get_best_proxy(exclude=current_proxy)
                        print(f"[Bridge] Auto-switched proxy from {old_p} to {current_proxy}, retrying in {0.4 * (attempt + 1)}s...", flush=True)
                        await asyncio.sleep(0.4 * (attempt + 1))
                        continue
                    if streamed_full_text and auto_continue_count < max_auto_continues:
                        round_finished_with_max_tokens = True
                        break
                    detail = err_str or "上游网络连接中断或代理超时，建议点击重试"
                    err_display = f"\n\n⚠️ **连接异常**: {err_name} ({detail})"
                    err_chunk = {
                        "id": completion_id,
                        "object": "chat.completion.chunk",
                        "created": created_ts,
                        "model": model_req,
                        "choices": [
                            {
                                "index": 0,
                                "delta": {"role": "assistant", "content": err_display},
                                "finish_reason": "stop"
                            }
                        ]
                    }
                    yield f"data: {json.dumps(err_chunk, ensure_ascii=False)}\n\n"
                    yield "data: [DONE]\n\n"
                    return

            if round_finished_with_max_tokens and auto_continue_count < max_auto_continues:
                auto_continue_count += 1
                print(f"[Bridge] Stream hit MAX_TOKENS at {len(streamed_full_text)} chars. Seamlessly auto-continuing (round {auto_continue_count}/{max_auto_continues})...", flush=True)
                req_contents = current_payload["request"]["contents"]
                req_contents.append({"role": "model", "parts": [{"text": streamed_full_text}]})
                req_contents.append({"role": "user", "parts": [{"text": "Continue directly from where you left off. Do NOT repeat previous text. Start immediately with the next character or line of code."}]})
                current_payload["requestId"] = str(uuid.uuid4())
                await asyncio.sleep(0.1)
                continue
            else:
                stop_resp = {
                    "id": completion_id,
                    "object": "chat.completion.chunk",
                    "created": created_ts,
                    "model": model_req,
                    "choices": [
                        {
                            "index": 0,
                            "delta": {},
                            "finish_reason": "stop"
                        }
                    ]
                }
                yield f"data: {json.dumps(stop_resp)}\n\n"
                yield "data: [DONE]\n\n"
                return

    if stream:
        headers = {
            "Cache-Control": "no-cache, no-transform",
            "Connection": "keep-alive",
            "Content-Type": "text/event-stream; charset=utf-8",
            "X-Accel-Buffering": "no"
        }
        return StreamingResponse(sse_generator(), media_type="text/event-stream", headers=headers)
    else:
        # Non-streaming aggregation
        current_token = acc["token"]
        max_non_stream_attempts = 6
        current_proxy = get_best_proxy()
        for attempt in range(max_non_stream_attempts):
            headers = get_headers(current_token)
            try:
                full_text = []
                async with httpx.AsyncClient(proxy=current_proxy, trust_env=False, timeout=httpx.Timeout(120.0, connect=20.0)) as client:
                    async with client.stream("POST", url, headers=headers, json=payload) as response:
                        if response.status_code == 401 and attempt == 0:
                            print(f"[Bridge] Got 401, auto-refreshing token...")
                            if await pool.refresh_account(acc):
                                current_token = acc["token"]
                                continue

                        if response.status_code == 429:
                            err_text = await response.aread()
                            err_msg = err_text.decode("utf-8", errors="replace")
                            print(f"[Bridge] Account {acc.get('email')} hit 429 non-stream! Details: {err_msg[:200]}")
                            pool.mark_account_exhausted(acc.get("email"))
                            next_acc = await pool.get_valid_account(exclude_emails=[acc.get("email")])
                            if next_acc and next_acc.get("email") != acc.get("email"):
                                print(f"[Bridge] Auto-switched to fallback account: {next_acc.get('email')}, retrying non-stream...")
                                acc = next_acc
                                current_token = acc["token"]
                                await asyncio.sleep(0.1)
                                continue
                            elif payload.get("model") in FALLBACK_MODELS:
                                fallback_model = FALLBACK_MODELS[payload["model"]]
                                print(f"[Bridge] All accounts quota exhausted for {payload['model']}. Auto-falling back to {fallback_model}...")
                                payload["model"] = fallback_model
                                pool.exhausted_emails.clear()
                                next_acc = await pool.get_valid_account()
                                if next_acc:
                                    acc = next_acc
                                    current_token = acc["token"]
                                    await asyncio.sleep(0.1)
                                    continue

                        if response.status_code == 503 and attempt < max_non_stream_attempts - 1:
                            await asyncio.sleep(0.3)
                            continue

                        if response.status_code != 200:
                            err_text = await response.aread()
                            err_msg = err_text.decode("utf-8", errors="replace").strip()
                            if attempt < max_non_stream_attempts - 1:
                                await asyncio.sleep(0.3)
                                continue
                            return JSONResponse({"error": {"message": err_msg or f"Upstream error HTTP {response.status_code}"}}, status_code=response.status_code)

                        buffer = ""
                        async for chunk in response.aiter_text():
                            buffer += chunk
                            while "\n" in buffer:
                                line, buffer = buffer.split("\n", 1)
                                line = line.strip()
                                if not line.startswith("data:"):
                                    continue
                                json_str = line[5:].strip()
                                try:
                                    data = json.loads(json_str)
                                    candidates = data.get("response", {}).get("candidates", [])
                                    for cand in candidates:
                                        for p in cand.get("content", {}).get("parts", []):
                                            full_text.append(p.get("text", ""))
                                except Exception:
                                    pass

                combined = "".join(full_text)
                combined = deduplicate_repetitive_text(combined)
                if not combined and attempt < max_non_stream_attempts - 1:
                    print(f"[Bridge] Non-stream empty content on attempt {attempt+1}, retrying...")
                    await asyncio.sleep(0.3)
                    continue

                return JSONResponse({
                    "id": completion_id,
                    "object": "chat.completion",
                    "created": created_ts,
                    "model": model_req,
                    "choices": [
                        {
                            "index": 0,
                            "message": {
                                "role": "assistant",
                                "content": combined
                            },
                            "finish_reason": "stop"
                        }
                    ],
                    "usage": {
                        "prompt_tokens": len(str(messages)) // 4,
                        "completion_tokens": len(combined) // 4,
                        "total_tokens": (len(str(messages)) + len(combined)) // 4
                    }
                })
            except Exception as e:
                print(f"[Bridge] Non-stream attempt {attempt+1} exception: {type(e).__name__} ({e})")
                if attempt < max_non_stream_attempts - 1:
                    current_proxy = get_best_proxy(exclude=current_proxy)
                    await asyncio.sleep(0.4 * (attempt + 1))
                    continue
                err_msg = str(e).strip() or f"{type(e).__name__}: Upstream connection dropped, please retry"
                return JSONResponse({"error": {"message": err_msg}}, status_code=500)

        return JSONResponse({"error": {"message": "上游模型响应超时或服务暂时不可用，请稍后重试"}}, status_code=500)

WORKSPACE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

def resolve_workspace_path(path_str: str) -> str:
    path_str = (path_str or "").strip()
    if not path_str or path_str == ".":
        return WORKSPACE_DIR
    target = os.path.abspath(os.path.join(WORKSPACE_DIR, path_str))
    try:
        common = os.path.commonpath([WORKSPACE_DIR, target])
    except Exception:
        raise ValueError(f"Invalid path: {path_str}")
    if common != WORKSPACE_DIR:
        raise ValueError(f"Access denied: path '{path_str}' is outside workspace '{WORKSPACE_DIR}'")
    return target

async def tool_read_file(request):
    try:
        if request.method == "POST":
            data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        else:
            data = dict(request.query_params)

        file_path = data.get("file_path")
        if not file_path:
            return JSONResponse({"error": "Missing required parameter 'file_path'"}, status_code=400)

        abs_path = resolve_workspace_path(file_path)
        if not os.path.isfile(abs_path):
            return JSONResponse({"error": f"File not found: {file_path}"}, status_code=404)

        with open(abs_path, "r", encoding="utf-8", errors="replace") as f:
            lines = f.readlines()

        total_lines = len(lines)
        start_line = int(data.get("start_line") or 1)
        end_line = int(data.get("end_line") or total_lines)

        start_line = max(1, min(start_line, total_lines if total_lines > 0 else 1))
        end_line = max(start_line, min(end_line, total_lines))

        sliced = lines[start_line - 1 : end_line]
        content = "".join(sliced)
        rel_path = os.path.relpath(abs_path, WORKSPACE_DIR).replace("\\", "/")

        return JSONResponse({
            "file_path": rel_path,
            "total_lines": total_lines,
            "start_line": start_line,
            "end_line": end_line,
            "content": content
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_write_file(request):
    try:
        data = await request.json()
        file_path = data.get("file_path")
        content = data.get("content")
        if not file_path:
            return JSONResponse({"error": "Missing required parameter 'file_path'"}, status_code=400)
        if content is None:
            return JSONResponse({"error": "Missing required parameter 'content'"}, status_code=400)

        abs_path = resolve_workspace_path(file_path)
        os.makedirs(os.path.dirname(abs_path), exist_ok=True)
        with open(abs_path, "w", encoding="utf-8") as f:
            f.write(content)

        rel_path = os.path.relpath(abs_path, WORKSPACE_DIR).replace("\\", "/")
        return JSONResponse({
            "file_path": rel_path,
            "status": "success",
            "bytes_written": len(content.encode("utf-8"))
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_list_directory(request):
    try:
        if request.method == "POST":
            data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        else:
            data = dict(request.query_params)

        dir_path = data.get("dir_path", "")
        recursive = bool(data.get("recursive", False))
        abs_path = resolve_workspace_path(dir_path)

        if not os.path.isdir(abs_path):
            return JSONResponse({"error": f"Directory not found: {dir_path}"}, status_code=404)

        entries = []
        if recursive:
            for root, dirs, files in os.walk(abs_path):
                dirs[:] = [d for d in dirs if not d.startswith(".") and d not in ("bin", "obj", ".godot")]
                for d in dirs:
                    full = os.path.join(root, d)
                    entries.append({
                        "name": os.path.relpath(full, abs_path).replace("\\", "/"),
                        "type": "directory"
                    })
                for f in files:
                    if f.startswith("."):
                        continue
                    full = os.path.join(root, f)
                    entries.append({
                        "name": os.path.relpath(full, abs_path).replace("\\", "/"),
                        "type": "file",
                        "size_bytes": os.path.getsize(full)
                    })
                if len(entries) >= 500:
                    break
        else:
            for item in os.listdir(abs_path):
                if item.startswith("."):
                    continue
                full = os.path.join(abs_path, item)
                if os.path.isdir(full):
                    entries.append({"name": item, "type": "directory"})
                else:
                    entries.append({"name": item, "type": "file", "size_bytes": os.path.getsize(full)})

        rel_path = os.path.relpath(abs_path, WORKSPACE_DIR).replace("\\", "/")
        if rel_path == ".":
            rel_path = ""

        return JSONResponse({
            "dir_path": rel_path,
            "count": len(entries),
            "entries": entries
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_grep_search(request):
    try:
        import fnmatch
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        query = data.get("query")
        if not query:
            return JSONResponse({"error": "Missing required parameter 'query'"}, status_code=400)

        sub_dir = data.get("sub_dir", "scripts")
        file_pattern = data.get("file_pattern", "*.cs")
        search_root = resolve_workspace_path(sub_dir)

        matches = []
        for root, dirs, files in os.walk(search_root):
            dirs[:] = [d for d in dirs if not d.startswith(".") and d not in ("bin", "obj", ".godot")]
            for filename in fnmatch.filter(files, file_pattern):
                full_path = os.path.join(root, filename)
                try:
                    with open(full_path, "r", encoding="utf-8", errors="ignore") as f:
                        for idx, line in enumerate(f, 1):
                            if query.lower() in line.lower():
                                rel = os.path.relpath(full_path, WORKSPACE_DIR).replace("\\", "/")
                                matches.append({
                                    "file": rel,
                                    "line": idx,
                                    "text": line.strip()
                                })
                                if len(matches) >= 100:
                                    break
                except Exception:
                    pass
                if len(matches) >= 100:
                    break
            if len(matches) >= 100:
                break

        return JSONResponse({
            "query": query,
            "total_matches": len(matches),
            "matches": matches
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_run_build(request):
    try:
        data = {}
        if request.headers.get("content-length", "0") != "0":
            try:
                data = await request.json()
            except Exception:
                pass
        clean = bool(data.get("clean", False))

        if clean:
            proc_clean = await asyncio.create_subprocess_exec(
                "dotnet", "clean", "GodotRTS.csproj",
                cwd=WORKSPACE_DIR,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            await proc_clean.communicate()

        proc = await asyncio.create_subprocess_exec(
            "dotnet", "build", "GodotRTS.csproj",
            cwd=WORKSPACE_DIR,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE
        )
        stdout, stderr = await proc.communicate()
        out_text = stdout.decode("utf-8", errors="replace")
        err_text = stderr.decode("utf-8", errors="replace")

        return JSONResponse({
            "success": proc.returncode == 0,
            "exit_code": proc.returncode,
            "output": out_text.strip(),
            "errors": err_text.strip() if err_text else None
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_run_command(request):
    try:
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        command = data.get("command", "").strip()
        if not command:
            return JSONResponse({"error": "Missing required parameter 'command'"}, status_code=400)

        cwd_param = data.get("cwd", "").strip()
        target_dir = resolve_workspace_path(cwd_param) if cwd_param else WORKSPACE_DIR
        timeout = min(int(data.get("timeout", 120)), 300)

        print(f"[Bridge] Executing command: {command} in {target_dir} (timeout={timeout}s)...", flush=True)
        proc = await asyncio.create_subprocess_exec(
            "cmd.exe", "/c", command,
            cwd=target_dir,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE
        )
        try:
            stdout, stderr = await asyncio.wait_for(proc.communicate(), timeout=timeout)
        except asyncio.TimeoutError:
            try:
                proc.kill()
            except Exception:
                pass
            return JSONResponse({
                "success": False,
                "exit_code": -1,
                "error": f"Command timed out after {timeout} seconds"
            }, status_code=408)

        out_text = stdout.decode("utf-8", errors="replace")
        err_text = stderr.decode("utf-8", errors="replace")

        return JSONResponse({
            "success": proc.returncode == 0,
            "exit_code": proc.returncode,
            "stdout": out_text.strip()[:10000],
            "stderr": err_text.strip()[:10000] if err_text else None,
            "cwd": target_dir
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

SNIPASTE_EXE = r"E:\Snipaste-2.10.5-x64\Snipaste.exe"
GODOT_EXE = r"G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe"
GODOT_CONSOLE = r"G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe"
VSCODE_BIN = r"E:\Microsoft VS Code\bin\code.cmd"
SCREENSHOTS_DIR = os.path.join(WORKSPACE_DIR, "screenshots")
os.makedirs(SCREENSHOTS_DIR, exist_ok=True)

def launch_desktop_process(args, cwd=None):
    """Launch a Windows desktop GUI process detached without blocking or cmd.exe quoting issues."""
    flags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS
    return subprocess.Popen(
        args,
        cwd=cwd or WORKSPACE_DIR,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=flags,
        close_fds=True
    )

async def tool_take_screenshot(request):
    try:
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        action = data.get("action", "game_screenshot").strip().lower()
        save_path = data.get("save_path", "").strip()
        scene = data.get("scene", "res://scenes/test/AutoCombatScene.tscn").strip()

        screenshot_dir = SCREENSHOTS_DIR
        os.makedirs(screenshot_dir, exist_ok=True)

        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        if not save_path:
            save_path = f"screenshots/screenshot_{action}_{ts}.png"

        target_file = resolve_workspace_path(save_path)
        os.makedirs(os.path.dirname(target_file), exist_ok=True)
        filename = os.path.basename(target_file)
        
        # 使用局域网 IP 与 Dify Nginx 宿主端口，确保 PC 与手机客户端均可无缝加载
        lan_ip = os.environ.get("LAN_HOST", "192.168.1.176")
        image_url = f"http://{lan_ip}:9564/screenshots/{filename}"
        relative_url = f"/screenshots/{filename}"
        direct_url = f"http://127.0.0.1:8000/screenshots/{filename}"
        markdown_image = f"![渲染效果图]({image_url})"

        print(f"[Bridge] tool_take_screenshot: action={action}, scene={scene}, target={target_file}", flush=True)

        if action in ("game_screenshot", "godot_screenshot", "game", "arena", "preview", "ui_preview", "ui", "render_ui"):
            godot_bin = GODOT_CONSOLE if os.path.exists(GODOT_CONSOLE) else GODOT_EXE
            
            # 判断是否为独立 UI 场景预览
            is_custom_ui = (action in ("ui_preview", "ui", "render_ui")) or (
                scene and scene != "res://scenes/test/AutoCombatScene.tscn" and "AutoCombatScene" not in scene and "BattlePrototype" not in scene
            )

            if is_custom_ui:
                cmd = [
                    godot_bin,
                    "--path", WORKSPACE_DIR,
                    "res://scenes/debug/UiPreviewScene.tscn",
                    "--",
                    "--ui-scene", scene,
                    "--capture-path", target_file,
                    "--capture-frames", "15"
                ]
                render_desc = f"UI/组件场景 ({scene})"
            else:
                cmd = [
                    godot_bin,
                    "--path", WORKSPACE_DIR,
                    scene,
                    "--",
                    "--capture-preview",
                    "--capture-frames", "20",
                    "--capture-path", target_file
                ]
                render_desc = "3D RTS 实机对战画面"

            proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                encoding="utf-8",
                errors="replace",
                cwd=WORKSPACE_DIR
            )
            stdout_text, stderr_text = "", ""
            try:
                stdout_text, stderr_text = proc.communicate(timeout=25)
            except subprocess.TimeoutExpired:
                proc.kill()
                stderr_text = "Godot render process timed out after 25s"

            if os.path.exists(target_file) and os.path.getsize(target_file) > 0:
                file_size_kb = round(os.path.getsize(target_file) / 1024, 1)
                return JSONResponse({
                    "success": True,
                    "action": action,
                    "filename": filename,
                    "image_url": image_url,
                    "relative_url": relative_url,
                    "direct_url": direct_url,
                    "markdown_image": markdown_image,
                    "file_size": f"{file_size_kb} KB",
                    "message": f"已成功通过 Godot 引擎原生 GPU 渲染管道截取【{render_desc}】的高清渲染效果图！\n你必须在回复中使用 Markdown 语法直接插入此图片给用户展示，例如：\n\n{markdown_image}"
                })
            else:
                combined_err = (stdout_text + "\n" + stderr_text).strip()
                err_lines = [l.strip() for l in combined_err.split("\n") if "ERROR" in l or "Error" in l or "Exception" in l or "failed" in l or "Failed" in l or "漏" in l]
                err_summary = "\n".join(err_lines[:5]) if err_lines else (combined_err[-500:] if combined_err else "未知错误")
                return JSONResponse({
                    "success": False,
                    "error": f"Godot 渲染截屏未成功生成图片文件！场景：{scene}\n关键诊断信息：\n{err_summary}\n\n💡 修复建议：\n1. 如果编写了新的 C# 脚本，请务必先调用 run_command 执行 'dotnet build GodotRTS.csproj' 编译成功后再渲染！\n2. 严禁创建 Camera3D/DirectionalLight3D 包装场景！系统已提供完整渲染宿主，直接传入你的 UI 场景文件路径（如 res://scenes/ui/XXX.tscn）即可！"
                })

        elif action == "snipaste_interactive":
            launch_desktop_process([SNIPASTE_EXE, "snip"])
            return JSONResponse({
                "success": True,
                "action": "snipaste_interactive",
                "message": "已在桌面屏幕上唤起 Snipaste 交互截图准星，可自由拖拽框选、标注或贴图。"
            })
        elif action == "snipaste_full":
            proc = subprocess.Popen([SNIPASTE_EXE, "snip", "-o", target_file])
            try:
                proc.wait(timeout=8)
            except subprocess.TimeoutExpired:
                pass
            if os.path.exists(target_file) and os.path.getsize(target_file) > 0:
                return JSONResponse({
                    "success": True,
                    "action": "snipaste_full",
                    "filename": filename,
                    "image_url": image_url,
                    "relative_url": relative_url,
                    "direct_url": direct_url,
                    "markdown_image": markdown_image,
                    "message": f"全屏截图已成功截取并生成 Web 图像访问链接！请在回复中使用 Markdown 语法展示：\n\n{markdown_image}"
                })
            else:
                return JSONResponse({
                    "success": True,
                    "action": "snipaste_full",
                    "message": "已触发全屏截图指令。"
                })
        elif action == "snipaste_paste":
            launch_desktop_process([SNIPASTE_EXE, "paste"])
            return JSONResponse({
                "success": True,
                "action": "snipaste_paste",
                "message": "已将最近截图作为贴图固定在桌面屏幕置顶展示。"
            })
        elif action in ("windows_snipping_tool", "ms_screenclip", "snippingtool", "snipp"):
            os.startfile("ms-screenclip:")
            return JSONResponse({
                "success": True,
                "action": "windows_snipping_tool",
                "message": "已唤起 Windows 原生系统截图工具 (Win+Shift+S / ms-screenclip)。"
            })
        else:
            return JSONResponse({
                "error": f"Unknown action '{action}'. Supported: 'game_screenshot', 'snipaste_interactive', 'snipaste_full', 'snipaste_paste', 'windows_snipping_tool'"
            }, status_code=400)
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_operate_vscode(request):
    try:
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        action = data.get("action", "open_file").strip().lower()
        file_path = data.get("file", data.get("file_path", "")).strip()
        line = data.get("line")
        column = data.get("column", 1)
        file2 = data.get("file2", "").strip()
        extension_id = data.get("extension_id", "").strip()

        if not os.path.exists(VSCODE_BIN):
            return JSONResponse({"error": f"VS Code binary not found at {VSCODE_BIN}"}, status_code=500)

        if action == "open_file":
            if not file_path:
                launch_desktop_process([VSCODE_BIN, "-r", WORKSPACE_DIR])
                msg = f"已在当前 VS Code 窗口中打开工程根目录: {WORKSPACE_DIR}"
            else:
                abs_file = resolve_workspace_path(file_path)
                if line is not None and str(line).isdigit():
                    loc = f"{abs_file}:{line}:{column}"
                    launch_desktop_process([VSCODE_BIN, "-r", "-g", loc])
                    msg = f"已在 VS Code 中打开文件并精准定位到第 {line} 行: {file_path}"
                else:
                    launch_desktop_process([VSCODE_BIN, "-r", abs_file])
                    msg = f"已在 VS Code 中打开文件: {file_path}"
            return JSONResponse({"success": True, "action": "open_file", "message": msg})

        elif action == "open_workspace":
            target = resolve_workspace_path(file_path) if file_path else WORKSPACE_DIR
            launch_desktop_process([VSCODE_BIN, "-r", target])
            return JSONResponse({"success": True, "action": "open_workspace", "message": f"已在 VS Code 中载入工作区: {target}"})

        elif action == "diff":
            if not file_path or not file2:
                return JSONResponse({"error": "Action 'diff' requires both 'file' and 'file2'"}, status_code=400)
            f1 = resolve_workspace_path(file_path)
            f2 = resolve_workspace_path(file2)
            launch_desktop_process([VSCODE_BIN, "-r", "-d", f1, f2])
            return JSONResponse({"success": True, "action": "diff", "message": f"已在 VS Code 中分屏对比差异: {file_path} <-> {file2}"})

        elif action == "list_extensions":
            proc = subprocess.Popen([VSCODE_BIN, "--list-extensions"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            stdout, _ = proc.communicate(timeout=15)
            exts = stdout.strip().splitlines()
            return JSONResponse({"success": True, "action": "list_extensions", "count": len(exts), "extensions": exts})

        elif action == "install_extension":
            if not extension_id:
                return JSONResponse({"error": "Missing 'extension_id' for install_extension"}, status_code=400)
            proc = subprocess.Popen([VSCODE_BIN, "--install-extension", extension_id], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            stdout, stderr = proc.communicate(timeout=30)
            out_str = stdout + stderr
            return JSONResponse({"success": True, "action": "install_extension", "output": out_str.strip()})

        else:
            return JSONResponse({"error": f"Unknown action '{action}'. Supported: 'open_file', 'open_workspace', 'diff', 'list_extensions', 'install_extension'"}, status_code=400)
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_launch_software(request):
    try:
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        software = data.get("software", "").strip().lower()
        target_path = data.get("target_path", "").strip()

        if not software:
            return JSONResponse({"error": "Missing required parameter 'software'"}, status_code=400)

        print(f"[Bridge] tool_launch_software request: software={software}, target_path={target_path}", flush=True)

        if software in ("vscode", "code", "vs_code"):
            if target_path:
                if ":" in target_path and not os.path.isabs(target_path):
                    parts = target_path.split(":", 2)
                    fp = resolve_workspace_path(parts[0])
                    loc = f"{fp}:{parts[1]}" if len(parts) > 1 else fp
                    args = [VSCODE_BIN, "-r", "-g", loc]
                else:
                    dest = resolve_workspace_path(target_path)
                    args = [VSCODE_BIN, "-r", dest]
            else:
                args = [VSCODE_BIN, "-r", WORKSPACE_DIR]
            launch_desktop_process(args)
            return JSONResponse({
                "success": True,
                "software": "vscode",
                "message": f"已在当前 VS Code 中打开: {target_path or WORKSPACE_DIR}"
            })
        elif software in ("snipaste", "snipp"):
            launch_desktop_process([SNIPASTE_EXE])
            return JSONResponse({
                "success": True,
                "software": "snipaste",
                "message": f"已启动/唤醒 Snipaste 截图软件 ({SNIPASTE_EXE})"
            })
        elif software == "godot_editor":
            launch_desktop_process([GODOT_EXE, "-e", "--path", WORKSPACE_DIR])
            return JSONResponse({
                "success": True,
                "software": "godot_editor",
                "message": f"已启动 Godot 4 游戏编辑器并载入工程: {WORKSPACE_DIR}"
            })
        elif software in ("godot_run", "run_game", "run_battle", "godot"):
            scene = target_path if target_path else "res://scenes/test/AutoCombatScene.tscn"
            bat_path = os.path.join(WORKSPACE_DIR, "run_arena.bat")
            if (not target_path or target_path == "res://scenes/test/AutoCombatScene.tscn") and os.path.exists(bat_path):
                print(f"[Bridge] Launching Godot arena via run_arena.bat...", flush=True)
                os.startfile(bat_path)
            else:
                print(f"[Bridge] Launching Godot directly with scene: {scene}...", flush=True)
                launch_desktop_process([GODOT_EXE, "--path", WORKSPACE_DIR, scene])
            return JSONResponse({
                "success": True,
                "software": "godot_run",
                "message": f"已成功启动 Godot RTS 战斗游戏实机运行，场景: {scene}"
            })
        elif software == "explorer":
            dest = resolve_workspace_path(target_path) if target_path else WORKSPACE_DIR
            if os.path.isfile(dest):
                launch_desktop_process(["explorer.exe", f"/select,{dest}"])
            else:
                os.startfile(dest)
            return JSONResponse({
                "success": True,
                "software": "explorer",
                "message": f"已在 Windows 资源管理器中打开: {dest}"
            })
        elif software == "custom":
            if not target_path:
                return JSONResponse({"error": "Missing 'target_path' for custom software launch"}, status_code=400)
            flags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS
            subprocess.Popen(target_path, shell=True, cwd=WORKSPACE_DIR, creationflags=flags, close_fds=True)
            return JSONResponse({
                "success": True,
                "software": "custom",
                "message": f"已执行启动自定义命令: {target_path}"
            })
        else:
            return JSONResponse({
                "error": f"Unknown software '{software}'. Supported: 'vscode', 'snipaste', 'godot_editor', 'godot_run', 'explorer', 'custom'"
            }, status_code=400)
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_inspect_3d_asset(request):
    try:
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        asset_path = data.get("asset_path", data.get("path", "")).strip()
        if not asset_path:
            return JSONResponse({"error": "Missing required parameter 'asset_path'"}, status_code=400)

        target_file = resolve_workspace_path(asset_path)
        if not os.path.exists(target_file):
            return JSONResponse({"error": f"Asset file not found: {asset_path}"}, status_code=404)

        ext = os.path.splitext(target_file)[1].lower()
        file_size_kb = round(os.path.getsize(target_file) / 1024, 2)

        # 1. GLB binary inspect
        if ext == ".glb":
            with open(target_file, "rb") as f:
                header = f.read(12)
                if len(header) < 12:
                    return JSONResponse({"error": "Invalid GLB file: file too short"}, status_code=400)
                magic, version, length = struct.unpack("<4sII", header)
                if magic != b"glTF":
                    return JSONResponse({"error": "Not a valid binary glTF (GLB) file"}, status_code=400)
                chunk_len, chunk_type = struct.unpack("<I4s", f.read(8))
                if chunk_type != b"JSON":
                    return JSONResponse({"error": "First chunk is not JSON"}, status_code=400)
                json_data = json.loads(f.read(chunk_len).decode("utf-8", errors="replace"))

            nodes = [n.get("name", f"node_{i}") for i, n in enumerate(json_data.get("nodes", []))]
            meshes = [m.get("name", f"mesh_{i}") for i, m in enumerate(json_data.get("meshes", []))]
            anims = [a.get("name", f"anim_{i}") for i, a in enumerate(json_data.get("animations", []))]
            materials = [m.get("name", f"mat_{i}") for i, m in enumerate(json_data.get("materials", []))]

            return JSONResponse({
                "success": True,
                "asset_path": asset_path,
                "format": "GLB",
                "file_size_kb": file_size_kb,
                "nodes_count": len(nodes),
                "nodes": nodes[:50],
                "meshes_count": len(meshes),
                "meshes": meshes[:50],
                "animations_count": len(anims),
                "animations": anims,
                "materials_count": len(materials),
                "materials": materials
            })

        # 2. GLTF text inspect
        elif ext == ".gltf":
            with open(target_file, "r", encoding="utf-8", errors="replace") as f:
                json_data = json.load(f)
            nodes = [n.get("name", f"node_{i}") for i, n in enumerate(json_data.get("nodes", []))]
            meshes = [m.get("name", f"mesh_{i}") for i, m in enumerate(json_data.get("meshes", []))]
            anims = [a.get("name", f"anim_{i}") for i, a in enumerate(json_data.get("animations", []))]
            materials = [m.get("name", f"mat_{i}") for i, m in enumerate(json_data.get("materials", []))]
            return JSONResponse({
                "success": True,
                "asset_path": asset_path,
                "format": "GLTF",
                "file_size_kb": file_size_kb,
                "nodes_count": len(nodes),
                "nodes": nodes[:50],
                "meshes_count": len(meshes),
                "meshes": meshes[:50],
                "animations_count": len(anims),
                "animations": anims,
                "materials_count": len(materials),
                "materials": materials
            })

        # 3. Godot TSCN text scene inspect
        elif ext == ".tscn":
            with open(target_file, "r", encoding="utf-8", errors="replace") as f:
                lines = f.readlines()
            ext_resources = [l.strip() for l in lines if l.startswith("[ext_resource")]
            sub_resources = [l.strip() for l in lines if l.startswith("[sub_resource")]
            nodes = [l.strip() for l in lines if l.startswith("[node")]
            return JSONResponse({
                "success": True,
                "asset_path": asset_path,
                "format": "TSCN",
                "file_size_kb": file_size_kb,
                "nodes_count": len(nodes),
                "nodes": [n[:100] for n in nodes[:50]],
                "resources_count": len(ext_resources) + len(sub_resources),
                "ext_resources": ext_resources[:20]
            })

        else:
            return JSONResponse({
                "success": True,
                "asset_path": asset_path,
                "format": ext.upper().lstrip("."),
                "file_size_kb": file_size_kb,
                "message": f"Asset {ext} inspected. Size: {file_size_kb} KB."
            })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

async def tool_fix_normal_map(request):
    try:
        from PIL import Image
        data = await request.json() if request.headers.get("content-length", "0") != "0" else {}
        input_path = data.get("input_path", data.get("path", "")).strip()
        output_path = data.get("output_path", "").strip()

        if not input_path:
            return JSONResponse({"error": "Missing required parameter 'input_path'"}, status_code=400)

        abs_in = resolve_workspace_path(input_path)
        if not os.path.exists(abs_in):
            return JSONResponse({"error": f"Image file not found: {input_path}"}, status_code=404)

        if not output_path:
            base, ext = os.path.splitext(abs_in)
            abs_out = f"{base}_gl{ext}"
        else:
            abs_out = resolve_workspace_path(output_path)

        os.makedirs(os.path.dirname(abs_out), exist_ok=True)
        img = Image.open(abs_in).convert("RGBA")
        r, g, b, a = img.split()
        g_inverted = g.point(lambda i: 255 - i)
        fixed_img = Image.merge("RGBA", (r, g_inverted, b, a))
        fixed_img.save(abs_out)

        rel_out = os.path.relpath(abs_out, WORKSPACE_DIR).replace("\\", "/")
        return JSONResponse({
            "success": True,
            "message": f"法线贴图绿色通道(-Y)已成功反转为 Godot 4 OpenGL 规范(+Y)，并保存至: {rel_out}",
            "output_path": rel_out,
            "abs_path": abs_out
        })
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

OPENAPI_SPEC = {
    "openapi": "3.0.0",
    "info": {
        "title": "Godot RTS Workspace Tools",
        "description": "Tools for reading, writing, searching project files, and building the Godot 4 C# RTS game project.",
        "version": "1.0.0"
    },
    "servers": [
        {
            "url": "http://host.docker.internal:8000"
        }
    ],
    "paths": {
        "/api/tools/read_file": {
            "post": {
                "operationId": "read_file",
                "summary": "Read file contents",
                "description": "Read file text from the local Godot RTS workspace. Supports line range.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "file_path": {
                                        "type": "string",
                                        "description": "Relative file path in Godot RTS workspace, e.g. 'scripts/battle/BattleMinimap.cs'"
                                    },
                                    "start_line": {
                                        "type": "integer",
                                        "description": "Start line (1-based, optional)"
                                    },
                                    "end_line": {
                                        "type": "integer",
                                        "description": "End line (inclusive, optional)"
                                    }
                                },
                                "required": ["file_path"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "File content read successfully"
                    }
                }
            }
        },
        "/api/tools/write_file": {
            "post": {
                "operationId": "write_file",
                "summary": "Write or overwrite file",
                "description": "Write or overwrite file in the local Godot RTS workspace. Parent folders are auto-created.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "file_path": {
                                        "type": "string",
                                        "description": "Relative file path in Godot RTS workspace, e.g. 'scripts/battle/NewFeature.cs'"
                                    },
                                    "content": {
                                        "type": "string",
                                        "description": "The exact full file content to write"
                                    }
                                },
                                "required": ["file_path", "content"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "File written successfully"
                    }
                }
            }
        },
        "/api/tools/list_directory": {
            "post": {
                "operationId": "list_directory",
                "summary": "List directory contents",
                "description": "List files and subdirectories in a directory of the Godot RTS workspace.",
                "requestBody": {
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "dir_path": {
                                        "type": "string",
                                        "description": "Subdirectory path, e.g. 'scripts/battle'. Leave empty or '.' for root."
                                    },
                                    "recursive": {
                                        "type": "boolean",
                                        "description": "Whether to list recursively (default: false)"
                                    }
                                }
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Directory listing"
                    }
                }
            }
        },
        "/api/tools/grep_search": {
            "post": {
                "operationId": "grep_search",
                "summary": "Search text in workspace",
                "description": "Search text or keyword across C# scripts or other files in the Godot RTS workspace.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "query": {
                                        "type": "string",
                                        "description": "Keyword or text to search for"
                                    },
                                    "sub_dir": {
                                        "type": "string",
                                        "description": "Subdirectory to search in (default: 'scripts')"
                                    },
                                    "file_pattern": {
                                        "type": "string",
                                        "description": "File pattern to match, e.g. '*.cs' (default: '*.cs')"
                                    }
                                },
                                "required": ["query"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Search matches"
                    }
                }
            }
        },
        "/api/tools/run_build": {
            "post": {
                "operationId": "run_build",
                "summary": "Compile Godot RTS C# project",
                "description": "Executes 'dotnet build GodotRTS.csproj' to compile the C# codebase and returns build output and exit code.",
                "requestBody": {
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "clean": {
                                        "type": "boolean",
                                        "description": "Whether to clean before building (default: false)"
                                    }
                                }
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Build results"
                    }
                }
            }
        },
        "/api/tools/run_command": {
            "post": {
                "operationId": "run_command",
                "summary": "Execute terminal or shell command",
                "description": "Executes arbitrary terminal or cmd command lines in the Godot RTS workspace (e.g. 'dotnet build GodotRTS.csproj', 'git status', 'godot --headless') and returns stdout, stderr, and exit code.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "command": {
                                        "type": "string",
                                        "description": "The command line string to execute, e.g. 'dotnet build GodotRTS.csproj', 'git status', 'dir'"
                                    },
                                    "cwd": {
                                        "type": "string",
                                        "description": "Working directory (relative to workspace root, optional, defaults to project root)"
                                    },
                                    "timeout": {
                                        "type": "integer",
                                        "description": "Command timeout in seconds (default 120, max 300)"
                                    }
                                },
                                "required": ["command"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Command execution output"
                    }
                }
            }
        },
        "/api/tools/take_screenshot": {
            "post": {
                "operationId": "take_screenshot",
                "summary": "Capture 3D game screenshots or trigger desktop screenshot tools, returning web image URLs for chat display",
                "description": "Captures real-time GPU-rendered screenshots of the Godot RTS game scene or triggers desktop screenshot software (Snipaste / Windows Snipping Tool). Returns direct web image URLs and Markdown syntax so the agent can display real screenshots directly in the Dify chat window.",
                "requestBody": {
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "action": {
                                        "type": "string",
                                        "description": "Screenshot action: 'game_screenshot' (render real 3D Godot combat scene), 'ui_preview' (render any Godot UI control or custom scene with GPU and export screenshot), 'snipaste_interactive' (launch Snipaste interactive snipper on user screen), 'windows_snipping_tool' (launch Windows Snipping Tool), 'snipaste_full' (take full screen screenshot and return URL), 'snipaste_paste' (pin last screenshot to desktop)",
                                        "enum": ["game_screenshot", "ui_preview", "snipaste_interactive", "windows_snipping_tool", "snipaste_full", "snipaste_paste"],
                                        "default": "game_screenshot"
                                    },
                                    "scene": {
                                        "type": "string",
                                        "description": "Scene path to render and capture (e.g. 'res://scenes/ui/MyUI.tscn', 'res://scenes/login/LoginScene.tscn', 'res://GodotMahjong/scenes/MahjongLobbyScreen.tscn', or 'res://scenes/test/AutoCombatScene.tscn')"
                                    },
                                    "save_path": {
                                        "type": "string",
                                        "description": "Optional file path relative to workspace to save screenshot"
                                    }
                                }
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Screenshot result with web image URL and Markdown snippet"
                    }
                }
            }
        },
        "/api/tools/launch_software": {
            "post": {
                "operationId": "launch_software",
                "summary": "Launch or open desktop software applications (VS Code, Snipaste, Godot, Explorer, etc.)",
                "description": "Launches desktop software on the Windows workstation, including Visual Studio Code (Code.exe), Godot 4 Editor, running the Godot RTS game scene, Snipaste, Windows Explorer to reveal files/folders, or custom software/executables.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "software": {
                                        "type": "string",
                                        "description": "Software to launch: 'vscode' (Visual Studio Code), 'snipaste' (Snipaste screenshot tool), 'godot_editor' (Godot 4 Editor with current project), 'godot_run' (Run RTS combat scene), 'explorer' (Open Windows File Explorer), 'custom' (Custom command/executable)",
                                        "enum": ["vscode", "snipaste", "godot_editor", "godot_run", "explorer", "custom"]
                                    },
                                    "target_path": {
                                        "type": "string",
                                        "description": "Optional path: file/line for vscode, folder/file path for explorer, scene path for godot_run, or command line for custom"
                                    }
                                },
                                "required": ["software"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Software launch result"
                    }
                }
            }
        },
        "/api/tools/operate_vscode": {
            "post": {
                "operationId": "operate_vscode",
                "summary": "Control and operate Visual Studio Code IDE",
                "description": "Controls Visual Studio Code editor on Windows workstation. Supports opening files and jumping directly to a specific line and column (open_file), opening/switching workspace (open_workspace), side-by-side diff comparison between two files (diff), listing installed extensions (list_extensions), or installing new extensions (install_extension).",
                "requestBody": {
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "action": {
                                        "type": "string",
                                        "description": "Action to perform: 'open_file' (open file and jump to line), 'open_workspace' (open workspace folder), 'diff' (side-by-side diff compare two files), 'list_extensions' (list installed extensions), 'install_extension' (install extension)",
                                        "enum": ["open_file", "open_workspace", "diff", "list_extensions", "install_extension"],
                                        "default": "open_file"
                                    },
                                    "file": {
                                        "type": "string",
                                        "description": "Relative or absolute file path to open in VS Code"
                                    },
                                    "line": {
                                        "type": "integer",
                                        "description": "Optional line number to jump to in the opened file"
                                    },
                                    "column": {
                                        "type": "integer",
                                        "description": "Optional column number to jump to in the opened file"
                                    },
                                    "file2": {
                                        "type": "string",
                                        "description": "Second file path required when action is 'diff'"
                                    },
                                    "extension_id": {
                                        "type": "string",
                                        "description": "VS Code extension identifier when action is 'install_extension'"
                                    }
                                }
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "VS Code operation result"
                    }
                }
            }
        },
        "/api/tools/inspect_3d_asset": {
            "post": {
                "operationId": "inspect_3d_asset",
                "summary": "Inspect 3D model (GLB, GLTF, TSCN) hierarchy, animations, and materials",
                "description": "Analyzes 3D models and Godot scenes. Returns bone nodes, mesh list, animation track names, and materials for Godot 4 RTS units, vehicles, and buildings.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "asset_path": {
                                        "type": "string",
                                        "description": "Path to 3D model or scene (e.g. 'assets/models/tank.glb' or 'scenes/units/Tank.tscn')"
                                    }
                                },
                                "required": ["asset_path"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Inspection result"
                    }
                }
            }
        },
        "/api/tools/fix_normal_map": {
            "post": {
                "operationId": "fix_normal_map",
                "summary": "Fix normal map Green channel for Godot 4 (DirectX -Y to OpenGL +Y)",
                "description": "Inverts the Green (Y) channel of normal maps from DirectX standard (Unity default) to OpenGL standard (Godot 4 default). Solves inverted lighting and concave normal visual bugs.",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "input_path": {
                                        "type": "string",
                                        "description": "Path to source normal map image (e.g. 'assets/textures/tank_normal.png')"
                                    },
                                    "output_path": {
                                        "type": "string",
                                        "description": "Optional output path (defaults to '<filename>_gl.png')"
                                    }
                                },
                                "required": ["input_path"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "Normal map fix result"
                    }
                }
            }
        }
    }
}

async def get_openapi_schema(request):
    return JSONResponse(OPENAPI_SPEC)

NAV_JS = """(function() {
    var installedMap = {
        "6299c648-4775-4d7c-a4fe-a4b57236b587": "k3XZzC40AdSgADAN",
        "c271447f-5a9b-48d1-a403-af7b0306c7ee": "k3XZzC40AdSgADAN",
        "6e50e89f-af79-4ed6-8434-7a9fbf933df8": "K2I75pwQFm4eitRi",
        "a7546eca-608d-4dc3-b09e-07e0660202e3": "K2I75pwQFm4eitRi",
        "d78a7aa4-d995-4169-b10f-04179692bfa3": "aK4Z7N1mnGIqGxif",
        "511f67f3-4409-405b-9287-431afab50f22": "aK4Z7N1mnGIqGxif",
        "620d3349-8048-42ae-ba05-1c3f800efaac": "E8W4zenZFZu1Clnq",
        "ef72f8c8-a202-4a61-b756-3770808da851": "E8W4zenZFZu1Clnq",
        "f0ed8e6d-b258-4798-92f9-268cdd29c64e": "XGfFt2vOTQizDVnO",
        "65f8189c-59c5-41b7-9893-a638c7233eff": "XGfFt2vOTQizDVnO",
        "9a01866d-2fc3-4785-bace-dc1664619861": "bSqS0Xsz4CqBJZlW",
        "312a1b85-780c-4b72-8ae1-7e23edd3c542": "bSqS0Xsz4CqBJZlW"
    };

    var currentPath = window.location.pathname;

    if (document.getElementById('rts-portal-nav-bar')) return;
    if (currentPath === '/portal' || currentPath === '/portal/') return;

    var architects = [
        { name: "🤖 全局总架构师", code: "k3XZzC40AdSgADAN", role: "系统级整合与跨领域调度" },
        { name: "⚔️ 战斗与 C# 架构师", code: "K2I75pwQFm4eitRi", role: "数值公式、状态机与零GC寻路" },
        { name: "🎨 3D资产与画面架构师", code: "aK4Z7N1mnGIqGxif", role: "GLB逆向、骨骼动画与法线修复" },
        { name: "🌐 联机帧同步架构师", code: "E8W4zenZFZu1Clnq", role: "UDP二进制协议与定点数防漂移" },
        { name: "🎮 场景演练测试架构师", code: "XGfFt2vOTQizDVnO", role: "Vulkan实机截图直出与演练" }
    ];

    var style = document.createElement('style');
    style.id = 'rts-portal-nav-styles';
    style.textContent = `
        #rts-portal-nav-bar {
            position: fixed;
            top: 12px;
            right: 18px;
            z-index: 9999999;
            display: flex;
            align-items: center;
            gap: 8px;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", sans-serif;
            user-select: none;
        }
        .rts-nav-pill {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 14px;
            background: rgba(15, 23, 42, 0.88);
            backdrop-filter: blur(12px);
            -webkit-backdrop-filter: blur(12px);
            color: #38bdf8;
            border: 1px solid rgba(56, 189, 248, 0.4);
            border-radius: 20px;
            font-size: 13px;
            font-weight: 600;
            text-decoration: none;
            box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
            transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
            cursor: pointer;
        }
        .rts-nav-pill:hover {
            background: rgba(56, 189, 248, 0.2);
            border-color: #38bdf8;
            color: #7dd3fc;
            transform: translateY(-1px);
            box-shadow: 0 6px 20px rgba(56, 189, 248, 0.3);
        }
        .rts-nav-secondary {
            background: rgba(15, 23, 42, 0.85);
            border-color: rgba(148, 163, 184, 0.3);
            color: #94a3b8;
        }
        .rts-nav-secondary:hover {
            background: rgba(148, 163, 184, 0.18);
            border-color: rgba(148, 163, 184, 0.6);
            color: #f1f5f9;
        }
        .rts-dropdown-wrap {
            position: relative;
        }
        .rts-dropdown-menu {
            display: none;
            position: absolute;
            top: calc(100% + 8px);
            right: 0;
            width: 290px;
            background: rgba(15, 23, 42, 0.96);
            backdrop-filter: blur(16px);
            -webkit-backdrop-filter: blur(16px);
            border: 1px solid rgba(56, 189, 248, 0.35);
            border-radius: 12px;
            padding: 8px 0;
            box-shadow: 0 14px 40px rgba(0, 0, 0, 0.6);
        }
        .rts-dropdown-wrap:hover .rts-dropdown-menu {
            display: block;
            animation: rtsFadeIn 0.15s ease-out;
        }
        @keyframes rtsFadeIn {
            from { opacity: 0; transform: translateY(-4px); }
            to { opacity: 1; transform: translateY(0); }
        }
        .rts-dropdown-item {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 8px 14px;
            color: #cbd5e1;
            text-decoration: none;
            font-size: 12px;
            transition: background 0.15s;
        }
        .rts-dropdown-item:hover {
            background: rgba(56, 189, 248, 0.15);
            color: #38bdf8;
        }
        .rts-win-btn {
            padding: 3px 7px;
            font-size: 11px;
            border-radius: 4px;
            border: 1px solid rgba(148, 163, 184, 0.3);
            background: rgba(30, 41, 59, 0.7);
            color: #94a3b8;
            cursor: pointer;
            transition: all 0.15s;
        }
        .rts-win-btn:hover {
            border-color: #38bdf8;
            color: #38bdf8;
            background: rgba(56, 189, 248, 0.15);
        }
    `;
    document.head.appendChild(style);

    var container = document.createElement('div');
    container.id = 'rts-portal-nav-bar';

    var dropdownHtml = architects.map(function(a) {
        return '<div class="rts-dropdown-item">' +
            '<a href="/chat/' + a.code + '" target="_blank" rel="noopener noreferrer" style="color:inherit;text-decoration:none;font-weight:500;">' + a.name + ' ↗</a>' +
            '<button class="rts-win-btn" title="在新独立窗口打开" onclick="window.open(\'/chat/' + a.code + '\', \'_blank\', \'popup=yes,width=1400,height=900\')">新窗口 🪟</button>' +
        '</div>';
    }).join('');

    container.innerHTML = 
        '<a href="/portal" class="rts-nav-pill" title="返回 RTS 架构师作战指挥中心首页">' +
            '<span style="font-size:15px;">🏠</span>' +
            '<span>返回首页</span>' +
        '</a>' +
        '<div class="rts-dropdown-wrap">' +
            '<div class="rts-nav-pill rts-nav-secondary">' +
                '<span>切换架构师 ▾</span>' +
            '</div>' +
            '<div class="rts-dropdown-menu">' +
                '<div style="padding:4px 14px 8px;font-size:11px;color:#64748b;font-weight:600;text-transform:uppercase;border-bottom:1px solid rgba(255,255,255,0.06);">' +
                    'RTS 架构师团队 (点击在新标签页打开)' +
                '</div>' +
                dropdownHtml +
            '</div>' +
        '</div>' +
        '<a href="/apps" class="rts-nav-pill rts-nav-secondary" title="进入 Dify Studio 工作台">' +
            '<span>📁 控制台</span>' +
        '</a>';

    document.body.appendChild(container);

    // Mappings between Dify installed app / app IDs and standalone site codes
    var installedMap = {
        "6299c648-4775-4d7c-a4fe-a4b57236b587": "k3XZzC40AdSgADAN",
        "c271447f-5a9b-48d1-a403-af7b0306c7ee": "k3XZzC40AdSgADAN",
        "6e50e89f-af79-4ed6-8434-7a9fbf933df8": "K2I75pwQFm4eitRi",
        "a7546eca-608d-4dc3-b09e-07e0660202e3": "K2I75pwQFm4eitRi",
        "d78a7aa4-d995-4169-b10f-04179692bfa3": "aK4Z7N1mnGIqGxif",
        "511f67f3-4409-405b-9287-431afab50f22": "aK4Z7N1mnGIqGxif",
        "620d3349-8048-42ae-ba05-1c3f800efaac": "E8W4zenZFZu1Clnq",
        "ef72f8c8-a202-4a61-b756-3770808da851": "E8W4zenZFZu1Clnq",
        "f0ed8e6d-b258-4798-92f9-268cdd29c64e": "XGfFt2vOTQizDVnO",
        "65f8189c-59c5-41b7-9893-a638c7233eff": "XGfFt2vOTQizDVnO",
        "9a01866d-2fc3-4785-bace-dc1664619861": "bSqS0Xsz4CqBJZlW",
        "312a1b85-780c-4b72-8ae1-7e23edd3c542": "bSqS0Xsz4CqBJZlW"
    };

    var nameMap = {
        "全局总架构师": "k3XZzC40AdSgADAN",
        "战斗与 C#": "K2I75pwQFm4eitRi",
        "3D资产与画面": "aK4Z7N1mnGIqGxif",
        "联机帧同步": "E8W4zenZFZu1Clnq",
        "场景演练与测试": "XGfFt2vOTQizDVnO",
        "研发智能体": "bSqS0Xsz4CqBJZlW"
    };

    function resolveTargetCode(href, text) {
        if (!href) href = '';
        if (!text) text = '';
        for (var id in installedMap) {
            if (href.indexOf(id) !== -1) return installedMap[id];
        }
        for (var name in nameMap) {
            if (text.indexOf(name) !== -1) return nameMap[name];
        }
        return null;
    }

    // Periodically patch sidebar DOM links so right-click/middle-click also target new tab
    function patchSidebarLinks() {
        var links = document.querySelectorAll('a[href*="/installed/"], a[href*="/explore/installed/"], a[href*="/chat/"]');
        links.forEach(function(link) {
            var href = link.getAttribute('href') || '';
            var text = (link.textContent || '').trim();
            var code = resolveTargetCode(href, text);
            if (code) {
                link.setAttribute('href', '/chat/' + code);
                link.setAttribute('target', '_blank');
                link.setAttribute('rel', 'noopener noreferrer');
            } else {
                link.setAttribute('target', '_blank');
                link.setAttribute('rel', 'noopener noreferrer');
            }
        });
    }
    setInterval(patchSidebarLinks, 1000);
    setTimeout(patchSidebarLinks, 300);

    // Global capture-phase click interceptor: Ensure clicking WEB APPS in sidebar opens in new tab
    document.addEventListener('click', function(e) {
        // If clicking the three-dots action menu button (...) on the sidebar item, don't intercept
        var moreBtn = e.target.closest('button');
        if (moreBtn && (moreBtn.querySelector('[class*="more"]') || moreBtn.getAttribute('aria-haspopup'))) {
            return;
        }

        var link = e.target.closest('a');
        if (!link) {
            var item = e.target.closest('li, div.group');
            if (item) {
                link = item.querySelector('a');
            }
        }

        if (link) {
            var href = link.getAttribute('href') || '';
            var text = (link.textContent || '').trim();
            var code = resolveTargetCode(href, text);

            if (code) {
                e.preventDefault();
                e.stopPropagation();
                e.stopImmediatePropagation();
                window.open('/chat/' + code, '_blank');
                return false;
            } else if (href.indexOf('/installed/') !== -1 || href.indexOf('/explore/installed/') !== -1) {
                e.preventDefault();
                e.stopPropagation();
                e.stopImmediatePropagation();
                window.open(href, '_blank');
                return false;
            } else if (href.indexOf('/chat/') !== -1) {
                link.setAttribute('target', '_blank');
                link.setAttribute('rel', 'noopener noreferrer');
            }
        }
    }, true);

    document.addEventListener('auxclick', function(e) {
        if (e.button === 1) { // Middle click
            var link = e.target.closest('a');
            if (link) {
                var href = link.getAttribute('href') || '';
                var text = (link.textContent || '').trim();
                var code = resolveTargetCode(href, text);
                if (code) {
                    e.preventDefault();
                    window.open('/chat/' + code, '_blank');
                }
            }
        }
    }, true);
})();
"""

PORTAL_HTML = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>RTS 架构师协同作战中心 · Godot 4.3 C#</title>
    <style>
        :root {
            --bg: #030712;
            --surface: rgba(15, 23, 42, 0.75);
            --surface-hover: rgba(30, 41, 59, 0.85);
            --border: rgba(56, 189, 248, 0.25);
            --border-glow: rgba(56, 189, 248, 0.6);
            --primary: #38bdf8;
            --primary-rgb: 56, 189, 248;
            --accent-green: #34d399;
            --accent-purple: #c084fc;
            --accent-amber: #fbbf24;
            --accent-cyan: #22d3ee;
            --text: #f8fafc;
            --text-muted: #94a3b8;
        }
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        body {
            background-color: var(--bg);
            background-image: 
                radial-gradient(at 0% 0%, rgba(56, 189, 248, 0.12) 0px, transparent 50%),
                radial-gradient(at 100% 0%, rgba(192, 132, 252, 0.10) 0px, transparent 50%),
                radial-gradient(at 50% 100%, rgba(34, 211, 238, 0.08) 0px, transparent 50%),
                linear-gradient(rgba(255, 255, 255, 0.02) 1px, transparent 1px),
                linear-gradient(90deg, rgba(255, 255, 255, 0.02) 1px, transparent 1px);
            background-size: 100% 100%, 100% 100%, 100% 100%, 40px 40px, 40px 40px;
            color: var(--text);
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "PingFang SC", "Microsoft YaHei", sans-serif;
            min-height: 100vh;
            padding: 32px 24px 60px;
        }
        .container {
            max-width: 1400px;
            margin: 0 auto;
        }
        header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-wrap: wrap;
            gap: 20px;
            padding-bottom: 28px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.08);
            margin-bottom: 32px;
        }
        .brand {
            display: flex;
            align-items: center;
            gap: 16px;
        }
        .brand-icon {
            width: 52px;
            height: 52px;
            border-radius: 14px;
            background: linear-gradient(135deg, #0284c7, #6366f1);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 28px;
            box-shadow: 0 0 24px rgba(56, 189, 248, 0.4);
        }
        .brand-text h1 {
            font-size: 24px;
            font-weight: 700;
            letter-spacing: -0.5px;
            background: linear-gradient(90deg, #f8fafc, #38bdf8);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        .brand-text p {
            font-size: 13px;
            color: var(--text-muted);
            margin-top: 4px;
        }
        .header-actions {
            display: flex;
            align-items: center;
            gap: 12px;
        }
        .btn {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 10px 18px;
            border-radius: 10px;
            font-size: 13px;
            font-weight: 600;
            text-decoration: none;
            cursor: pointer;
            transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
            border: 1px solid transparent;
        }
        .btn-primary {
            background: linear-gradient(135deg, #0284c7, #2563eb);
            color: #ffffff;
            box-shadow: 0 4px 16px rgba(37, 99, 235, 0.35);
        }
        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 24px rgba(37, 99, 235, 0.5);
            background: linear-gradient(135deg, #0369a1, #1d4ed8);
        }
        .btn-outline {
            background: rgba(15, 23, 42, 0.8);
            border-color: rgba(148, 163, 184, 0.3);
            color: #cbd5e1;
        }
        .btn-outline:hover {
            border-color: var(--primary);
            color: var(--primary);
            background: rgba(56, 189, 248, 0.1);
        }
        .status-strip {
            display: flex;
            align-items: center;
            gap: 24px;
            flex-wrap: wrap;
            background: rgba(15, 23, 42, 0.6);
            backdrop-filter: blur(12px);
            border: 1px solid rgba(255, 255, 255, 0.06);
            border-radius: 12px;
            padding: 14px 20px;
            margin-bottom: 32px;
            font-size: 13px;
        }
        .status-item {
            display: flex;
            align-items: center;
            gap: 8px;
        }
        .dot {
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background: var(--accent-green);
            box-shadow: 0 0 8px var(--accent-green);
            animation: pulse 2s infinite;
        }
        @keyframes pulse {
            0%, 100% { opacity: 1; transform: scale(1); }
            50% { opacity: 0.5; transform: scale(0.9); }
        }
        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(420px, 1fr));
            gap: 24px;
            margin-bottom: 36px;
        }
        .card {
            background: var(--surface);
            backdrop-filter: blur(16px);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 24px;
            display: flex;
            flex-direction: column;
            justify-content: space-between;
            transition: all 0.25s ease;
            position: relative;
            overflow: hidden;
        }
        .card::before {
            content: "";
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 3px;
            background: linear-gradient(90deg, transparent, var(--card-color, var(--primary)), transparent);
            opacity: 0.6;
        }
        .card:hover {
            transform: translateY(-4px);
            border-color: var(--border-glow);
            box-shadow: 0 12px 32px rgba(0, 0, 0, 0.5), 0 0 24px rgba(var(--primary-rgb), 0.15);
        }
        .card-header {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 16px;
            margin-bottom: 16px;
        }
        .card-title-group {
            display: flex;
            align-items: center;
            gap: 14px;
        }
        .card-icon {
            font-size: 32px;
            width: 52px;
            height: 52px;
            border-radius: 12px;
            background: rgba(255, 255, 255, 0.05);
            border: 1px solid rgba(255, 255, 255, 0.1);
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .card-name {
            font-size: 18px;
            font-weight: 700;
            color: #f8fafc;
            transition: color 0.15s ease;
        }
        .card-name:hover {
            color: var(--primary);
        }
        .card-role-badge {
            display: inline-block;
            margin-top: 4px;
            padding: 3px 8px;
            background: rgba(56, 189, 248, 0.12);
            border: 1px solid rgba(56, 189, 248, 0.3);
            border-radius: 6px;
            font-size: 11px;
            color: #7dd3fc;
            font-weight: 500;
        }
        .card-desc {
            font-size: 13px;
            line-height: 1.6;
            color: var(--text-muted);
            margin-bottom: 18px;
            min-height: 42px;
        }
        .tags-group {
            display: flex;
            flex-wrap: wrap;
            gap: 6px;
            margin-bottom: 22px;
        }
        .tag {
            padding: 3px 8px;
            border-radius: 6px;
            font-size: 11px;
            background: rgba(30, 41, 59, 0.6);
            border: 1px solid rgba(255, 255, 255, 0.08);
            color: #cbd5e1;
        }
        .card-actions {
            display: grid;
            grid-template-columns: 1.4fr 1fr 0.8fr;
            gap: 8px;
            padding-top: 16px;
            border-top: 1px solid rgba(255, 255, 255, 0.06);
        }
        .action-btn {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 6px;
            padding: 9px 12px;
            border-radius: 8px;
            font-size: 12px;
            font-weight: 600;
            text-decoration: none;
            cursor: pointer;
            transition: all 0.18s ease;
            border: 1px solid rgba(255, 255, 255, 0.08);
            background: rgba(30, 41, 59, 0.5);
            color: #cbd5e1;
        }
        .action-btn:hover {
            background: rgba(56, 189, 248, 0.18);
            border-color: var(--primary);
            color: #ffffff;
            transform: translateY(-1px);
        }
        .action-btn-main {
            background: linear-gradient(135deg, rgba(2, 132, 199, 0.5), rgba(37, 99, 235, 0.5));
            border-color: rgba(56, 189, 248, 0.5);
            color: #ffffff;
            box-shadow: 0 2px 10px rgba(56, 189, 248, 0.2);
        }
        .action-btn-main:hover {
            background: linear-gradient(135deg, #0284c7, #2563eb);
            color: #ffffff;
            border-color: transparent;
            box-shadow: 0 4px 16px rgba(56, 189, 248, 0.4);
        }
        footer {
            text-align: center;
            padding-top: 24px;
            border-top: 1px solid rgba(255, 255, 255, 0.06);
            color: #64748b;
            font-size: 13px;
        }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <div class="brand">
                <div class="brand-icon">⚡</div>
                <div class="brand-text">
                    <h1>RTS 架构师协同作战中心</h1>
                    <p>Godot 4.3 C# 实时战略引擎 · 独立多窗口架构师并行开发平台</p>
                </div>
            </div>
            <div class="header-actions">
                <button class="btn btn-primary" onclick="openAllArchitects()" title="在独立的并排窗口中同时启动 5 位架构师">
                    <span>⚡</span>
                    <span>一键并行开全套 (5 窗口)</span>
                </button>
                <a href="/apps" class="btn btn-outline" title="前往 Dify Studio 原生工作台">
                    <span>📁</span>
                    <span>Dify 原生控制台</span>
                </a>
            </div>
        </header>

        <div class="status-strip">
            <div class="status-item">
                <div class="dot"></div>
                <span><strong>Cockpit Bridge</strong>: 8000 端口活跃 (11 项工作区工具就绪)</span>
            </div>
            <div class="status-item">
                <div class="dot"></div>
                <span><strong>Dify LLM</strong>: Gemini 3.8 Flash (Function Calling)</span>
            </div>
            <div class="status-item">
                <div class="dot"></div>
                <span><strong>PostgreSQL</strong>: 独立应用与站点已就绪</span>
            </div>
            <div class="status-item">
                <div class="dot"></div>
                <span><strong>Godot 工作区</strong>: GodotRTS (C# Mono 4.3)</span>
            </div>
        </div>

        <div class="grid">
            <!-- 1. 全局总架构师 -->
            <div class="card" style="--card-color: #38bdf8;">
                <div>
                    <div class="card-header">
                        <div class="card-title-group">
                            <div class="card-icon">🤖</div>
                            <div>
                                <a href="/chat/k3XZzC40AdSgADAN" target="_blank" rel="noopener noreferrer" style="text-decoration:none;">
                                    <div class="card-name" style="display:flex;align-items:center;gap:6px;">
                                        <span>RTS 全局总架构师</span>
                                        <span style="font-size:13px;color:#38bdf8;">↗</span>
                                    </div>
                                </a>
                                <div class="card-role-badge">全链路系统编排 · 11 工具全开</div>
                            </div>
                        </div>
                    </div>
                    <div class="card-desc">
                        统筹 Godot 4.3 RTS 顶层架构，负责多模块接口规范制定、跨领域任务分配与协同，拥有全量 11 项工作区工具调用权限。
                    </div>
                    <div class="tags-group">
                        <span class="tag">顶层架构</span>
                        <span class="tag">模块解耦</span>
                        <span class="tag">11 项工具</span>
                        <span class="tag">统一协同</span>
                    </div>
                </div>
                <div class="card-actions">
                    <a href="/chat/k3XZzC40AdSgADAN" target="_blank" rel="noopener noreferrer" class="action-btn action-btn-main">📑 新标签页打开</a>
                    <button class="action-btn" onclick="openWin('k3XZzC40AdSgADAN', 20, 20)">🪟 独立窗口</button>
                    <a href="/chat/k3XZzC40AdSgADAN" class="action-btn">➡️ 进入</a>
                </div>
            </div>

            <!-- 2. 战斗与 C# 架构师 -->
            <div class="card" style="--card-color: #ef4444;">
                <div>
                    <div class="card-header">
                        <div class="card-title-group">
                            <div class="card-icon">⚔️</div>
                            <div>
                                <a href="/chat/K2I75pwQFm4eitRi" target="_blank" rel="noopener noreferrer" style="text-decoration:none;">
                                    <div class="card-name" style="display:flex;align-items:center;gap:6px;">
                                        <span>RTS 战斗与 C# 架构师</span>
                                        <span style="font-size:13px;color:#ef4444;">↗</span>
                                    </div>
                                </a>
                                <div class="card-role-badge">战斗公式 · 零GC寻路 · VS Code 联动</div>
                            </div>
                        </div>
                    </div>
                    <div class="card-desc">
                        负责战斗数值计算、护甲减伤矩阵、单位有限状态机、流场寻路（Integration Grid）、零 GC 高性能优化与 VS Code 光标联动。
                    </div>
                    <div class="tags-group">
                        <span class="tag">operate_vscode</span>
                        <span class="tag">run_build</span>
                        <span class="tag">流场寻路</span>
                        <span class="tag">状态机</span>
                        <span class="tag">零GC优化</span>
                    </div>
                </div>
                <div class="card-actions">
                    <a href="/chat/K2I75pwQFm4eitRi" target="_blank" rel="noopener noreferrer" class="action-btn action-btn-main">📑 新标签页打开</a>
                    <button class="action-btn" onclick="openWin('K2I75pwQFm4eitRi', 60, 60)">🪟 独立窗口</button>
                    <a href="/chat/K2I75pwQFm4eitRi" class="action-btn">➡️ 进入</a>
                </div>
            </div>

            <!-- 3. 3D资产与画面架构师 -->
            <div class="card" style="--card-color: #a855f7;">
                <div>
                    <div class="card-header">
                        <div class="card-title-group">
                            <div class="card-icon">🎨</div>
                            <div>
                                <a href="/chat/aK4Z7N1mnGIqGxif" target="_blank" rel="noopener noreferrer" style="text-decoration:none;">
                                    <div class="card-name" style="display:flex;align-items:center;gap:6px;">
                                        <span>RTS 3D资产与画面架构师</span>
                                        <span style="font-size:13px;color:#a855f7;">↗</span>
                                    </div>
                                </a>
                                <div class="card-role-badge">3D逆向 · 骨骼重定向 · 法线通道修复</div>
                            </div>
                        </div>
                    </div>
                    <div class="card-desc">
                        负责 GLB/GLTF 结构深度逆向排查、骨骼动画 retargeting、DirectX -Y 到 OpenGL +Y 法线贴图绿色通道自动反转与实机抓图。
                    </div>
                    <div class="tags-group">
                        <span class="tag">inspect_3d_asset</span>
                        <span class="tag">fix_normal_map</span>
                        <span class="tag">take_screenshot</span>
                        <span class="tag">骨骼重定向</span>
                    </div>
                </div>
                <div class="card-actions">
                    <a href="/chat/aK4Z7N1mnGIqGxif" target="_blank" rel="noopener noreferrer" class="action-btn action-btn-main">📑 新标签页打开</a>
                    <button class="action-btn" onclick="openWin('aK4Z7N1mnGIqGxif', 100, 100)">🪟 独立窗口</button>
                    <a href="/chat/aK4Z7N1mnGIqGxif" class="action-btn">➡️ 进入</a>
                </div>
            </div>

            <!-- 4. 联机帧同步架构师 -->
            <div class="card" style="--card-color: #3b82f6;">
                <div>
                    <div class="card-header">
                        <div class="card-title-group">
                            <div class="card-icon">🌐</div>
                            <div>
                                <a href="/chat/E8W4zenZFZu1Clnq" target="_blank" rel="noopener noreferrer" style="text-decoration:none;">
                                    <div class="card-name" style="display:flex;align-items:center;gap:6px;">
                                        <span>RTS 联机帧同步架构师</span>
                                        <span style="font-size:13px;color:#3b82f6;">↗</span>
                                    </div>
                                </a>
                                <div class="card-role-badge">UDP 双工 · 二进制封包 · 定点数同步</div>
                            </div>
                        </div>
                    </div>
                    <div class="card-desc">
                        负责确定性锁步网络同步、UDP 双工队列、二进制封包协议（Magic 2B, MsgId 2B, Tick 4B...）、定点数防漂移与零 GC 内存解析。
                    </div>
                    <div class="tags-group">
                        <span class="tag">锁步帧同步</span>
                        <span class="tag">UDP 双工</span>
                        <span class="tag">定点数</span>
                        <span class="tag">Span/MemoryMarshal</span>
                    </div>
                </div>
                <div class="card-actions">
                    <a href="/chat/E8W4zenZFZu1Clnq" target="_blank" rel="noopener noreferrer" class="action-btn action-btn-main">📑 新标签页打开</a>
                    <button class="action-btn" onclick="openWin('E8W4zenZFZu1Clnq', 140, 140)">🪟 独立窗口</button>
                    <a href="/chat/E8W4zenZFZu1Clnq" class="action-btn">➡️ 进入</a>
                </div>
            </div>

            <!-- 5. 场景演练与测试架构师 -->
            <div class="card" style="--card-color: #10b981;">
                <div>
                    <div class="card-header">
                        <div class="card-title-group">
                            <div class="card-icon">🎮</div>
                            <div>
                                <a href="/chat/XGfFt2vOTQizDVnO" target="_blank" rel="noopener noreferrer" style="text-decoration:none;">
                                    <div class="card-name" style="display:flex;align-items:center;gap:6px;">
                                        <span>RTS 场景演练与测试架构师</span>
                                        <span style="font-size:13px;color:#10b981;">↗</span>
                                    </div>
                                </a>
                                <div class="card-role-badge">Vulkan实机截图 · 场景起动 · 闭环质检</div>
                            </div>
                        </div>
                    </div>
                    <div class="card-desc">
                        负责 Vulkan GPU 真机画面抓取并在 Web 聊天中直出 Markdown 截图、一键拉起 Godot 4 编辑器与战场、端到端闭环验证。
                    </div>
                    <div class="tags-group">
                        <span class="tag">take_screenshot</span>
                        <span class="tag">launch_software</span>
                        <span class="tag">operate_vscode</span>
                        <span class="tag">闭环演练</span>
                    </div>
                </div>
                <div class="card-actions">
                    <a href="/chat/XGfFt2vOTQizDVnO" target="_blank" rel="noopener noreferrer" class="action-btn action-btn-main">📑 新标签页打开</a>
                    <button class="action-btn" onclick="openWin('XGfFt2vOTQizDVnO', 180, 180)">🪟 独立窗口</button>
                    <a href="/chat/XGfFt2vOTQizDVnO" class="action-btn">➡️ 进入</a>
                </div>
            </div>
        </div>

        <footer>
            <p>Godot 4.3 C# RTS 协同作战平台 · 每个架构师页面均具备专属系统级提示词与定制工具链</p>
        </footer>
    </div>

    <script>
        function openWin(code, leftOffset, topOffset) {
            var w = 1380;
            var h = 880;
            var l = (window.screen.width - w) / 2 + (leftOffset || 0);
            var t = (window.screen.height - h) / 2 + (topOffset || 0);
            window.open('/chat/' + code, '_blank', 'popup=yes,left=' + l + ',top=' + t + ',width=' + w + ',height=' + h);
        }

        function openAllArchitects() {
            var list = [
                { code: 'k3XZzC40AdSgADAN', x: -100, y: -80 },
                { code: 'K2I75pwQFm4eitRi', x: -50, y: -40 },
                { code: 'aK4Z7N1mnGIqGxif', x: 0, y: 0 },
                { code: 'E8W4zenZFZu1Clnq', x: 50, y: 40 },
                { code: 'XGfFt2vOTQizDVnO', x: 100, y: 80 }
            ];
            list.forEach(function(item) {
                openWin(item.code, item.x, item.y);
            });
        }
    </script>
</body>
</html>
"""

async def portal_page(request):
    return HTMLResponse(PORTAL_HTML)

async def nav_js(request):
    return Response(NAV_JS, media_type="application/javascript")

async def reload_route(request):
    pool.reload_accounts()
    return JSONResponse({
        "status": "ok",
        "message": "Accounts reloaded successfully",
        "accounts_count": len(pool.accounts),
        "accounts": [a.get("email") for a in pool.accounts]
    })

routes = [
    Mount("/screenshots", app=StaticFiles(directory=SCREENSHOTS_DIR), name="screenshots"),
    Route("/portal", portal_page, methods=["GET"]),
    Route("/portal/nav.js", nav_js, methods=["GET"]),
    Route("/health", health, methods=["GET"]),
    Route("/", health, methods=["GET"]),
    Route("/reload", reload_route, methods=["GET", "POST"]),
    Route("/api/reload", reload_route, methods=["GET", "POST"]),
    Route("/v1/models", list_models, methods=["GET"]),
    Route("/models", list_models, methods=["GET"]),
    Route("/v1/chat/completions", chat_completions, methods=["POST"]),
    Route("/chat/completions", chat_completions, methods=["POST"]),
    Route("/api/tools/openapi.json", get_openapi_schema, methods=["GET"]),
    Route("/api/tools/read_file", tool_read_file, methods=["POST", "GET"]),
    Route("/api/tools/write_file", tool_write_file, methods=["POST"]),
    Route("/api/tools/list_directory", tool_list_directory, methods=["POST", "GET"]),
    Route("/api/tools/grep_search", tool_grep_search, methods=["POST"]),
    Route("/api/tools/run_build", tool_run_build, methods=["POST"]),
    Route("/api/tools/run_command", tool_run_command, methods=["POST"]),
    Route("/api/tools/take_screenshot", tool_take_screenshot, methods=["POST"]),
    Route("/api/tools/launch_software", tool_launch_software, methods=["POST"]),
    Route("/api/tools/operate_vscode", tool_operate_vscode, methods=["POST"]),
    Route("/api/tools/inspect_3d_asset", tool_inspect_3d_asset, methods=["POST"]),
    Route("/api/tools/fix_normal_map", tool_fix_normal_map, methods=["POST"]),
]

middleware = [
    Middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])
]

app = Starlette(routes=routes, middleware=middleware)

if __name__ == "__main__":
    import traceback
    while True:
        try:
            print(f"Starting Cockpit Bridge Server on 0.0.0.0:{PORT}...", flush=True)
            uvicorn.run(app, host="0.0.0.0", port=PORT, log_level="info")
        except KeyboardInterrupt:
            print("[Bridge] Server stopped by user.", flush=True)
            break
        except SystemExit as e:
            if e.code != 0:
                print(f"[Bridge] Server exited with code {e.code}, auto-restarting in 2s...", flush=True)
                time.sleep(2)
                continue
            break
        except BaseException as e:
            print(f"\n[FATAL SERVER ERROR] {type(e).__name__}: {e}", flush=True)
            traceback.print_exc(file=sys.stdout)
            time.sleep(1)
