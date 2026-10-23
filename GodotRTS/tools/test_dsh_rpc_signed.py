import hmac
import hashlib
import base64
import time
import json
import requests
import uuid
import yaml

with open(r"C:\Users\Administrator\.dsh\.credentials.yaml", "r", encoding="utf-8") as f:
    creds = yaml.safe_load(f)

secret_b64 = creds["records"]["client-connection/browser-session"]["payload"]["secret"]
print("Raw secret b64:", secret_b64)

def decode_b64url(s):
    padding = '=' * ((4 - len(s) % 4) % 4)
    return base64.urlsafe_b64decode(s + padding)

def encode_b64url(b):
    return base64.urlsafe_b64encode(b).decode('utf-8').rstrip('=')

secret_bytes = decode_b64url(secret_b64)
authority = "127.0.0.1:19387"

# Cookie name: "dsh-auth-" + encodeBase64Url(createHash("sha256").update(authority).digest())
cname = "dsh-auth-" + encode_b64url(hashlib.sha256(authority.encode('utf-8')).digest())
print("Cookie name:", cname)

now_ms = int(time.time() * 1000)
payload = {
    "version": 1,
    "authority": authority,
    "issuedAt": now_ms,
    "expiresAt": now_ms + 30 * 86400 * 1000
}

body_json = json.dumps(payload, separators=(',', ':'))
body_b64 = encode_b64url(body_json.encode('utf-8'))
sig = hmac.new(secret_bytes, body_b64.encode('utf-8'), hashlib.sha256).digest()
sig_b64 = encode_b64url(sig)

cookie_val = f"v1.{body_b64}.{sig_b64}"
print("Cookie value:", cookie_val)

base_url = f"http://{authority}"
s = requests.Session()
s.cookies.set(cname, cookie_val)

def call_rpc(namespace, method, args={}):
    rpc_id = str(uuid.uuid4())
    req_body = {
        "type": "client-request",
        "rpcId": rpc_id,
        "method": f"{namespace}/{method}",
        "payload": {"args": args}
    }
    r = s.post(f"{base_url}/api/{namespace}/{method}", json=req_body)
    print(f"=== {namespace}/{method} ({r.status_code}) ===")
    try:
        data = r.json()
        print(json.dumps(data, indent=2, ensure_ascii=False))
        return data
    except Exception:
        print(r.text)
        return None

call_rpc("llm", "listConfigurableProviders")
call_rpc("credentials", "describe", {"refs": ["DEEPSEEK_API_KEY"]})
call_rpc("settings", "describe")
