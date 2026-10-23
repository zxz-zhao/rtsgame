import requests
import uuid
import hmac
import hashlib
import base64
import time
import json
import yaml

with open(r'C:\Users\Administrator\.dsh\.credentials.yaml', 'r', encoding='utf-8') as f:
    creds = yaml.safe_load(f)

secret_bytes = base64.urlsafe_b64decode(creds['records']['client-connection/browser-session']['payload']['secret'] + '==')
authority = '127.0.0.1:19387'
cname = 'dsh-auth-' + base64.urlsafe_b64encode(hashlib.sha256(authority.encode()).digest()).decode().rstrip('=')
now_ms = int(time.time() * 1000)
payload = {'version': 1, 'authority': authority, 'issuedAt': now_ms, 'expiresAt': now_ms + 30 * 86400 * 1000}
body_json = json.dumps(payload, separators=(',', ':'))
body_b64 = base64.urlsafe_b64encode(body_json.encode()).decode().rstrip('=')
sig = hmac.new(secret_bytes, body_b64.encode(), hashlib.sha256).digest()
cookie_val = f'v1.{body_b64}.{base64.urlsafe_b64encode(sig).decode().rstrip("=")}'

s = requests.Session()
s.cookies.set(cname, cookie_val)

def call_rpc(namespace, method, request_args={}):
    r = s.post(f'http://{authority}/api/{namespace}/{method}', json={
        'type': 'client-request',
        'rpcId': str(uuid.uuid4()),
        'method': f'{namespace}/{method}',
        'payload': {'args': {'request': request_args}}
    }, timeout=30)
    data = r.json()
    return data

# 1. Create a session
create_res = call_rpc('session', 'create', {})
if not create_res.get('result', {}).get('ok'):
    print("Create session failed:", create_res)
    exit(1)

session_id = create_res['result']['value']['sessionId']
print("Live Session ID created:", session_id)

# 2. Prompt
prompt_res = call_rpc('session', 'prompt', {
    'sessionId': session_id,
    'requestId': str(uuid.uuid4()),
    'content': [
        {'type': 'text', 'text': '请回复一个暗号：【猫咪反代接入DeepSeek-Harness成功】'}
    ]
})
print("Prompt accepted:", prompt_res.get('result', {}).get('ok'))

# 3. Poll projections for tokenUsage and turnBoundary
for i in range(15):
    time.sleep(2)
    proj_res = call_rpc('session', 'projections', {'sessionId': session_id})
    values = proj_res.get('result', {}).get('value', {}).get('values', {})
    token_usage = values.get('tokenUsage', {})
    tb = values.get('turnBoundary', {})
    stats = values.get('sessionStats', {})
    print(f"Poll {i+1}: turns={stats.get('turns')}, steps={stats.get('steps')}, tokenUsage={token_usage.get('totals')}")
    if stats.get('turns', 0) > 0 or (token_usage.get('totals', {}).get('outputTokens', 0) > 0):
        print("Success! Completed turn received.")
        print("Final token usage:", token_usage)
        break
