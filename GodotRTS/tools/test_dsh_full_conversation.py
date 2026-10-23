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
    print(f'=== {namespace}/{method} ({r.status_code}) ===')
    print(json.dumps(data, indent=2, ensure_ascii=False))
    return data

# 1. Create a test session
create_res = call_rpc('session', 'create', {})
if not create_res.get('result', {}).get('ok'):
    print("Create session failed!")
    exit(1)

session_id = create_res['result']['value']['sessionId']
print("Created Session ID:", session_id)

# 2. Send prompt to the session
prompt_res = call_rpc('session', 'prompt', {
    'sessionId': session_id,
    'requestId': str(uuid.uuid4()),
    'mode': 'queue',
    'content': [
        {'type': 'text', 'text': '你好，请用一句话回答：1+1等于几？'}
    ]
})
print("Prompt accepted:", prompt_res)

# 3. Wait a few seconds for agent execution and fetch projections
print("Waiting for response...")
for i in range(12):
    time.sleep(2)
    proj_res = call_rpc('session', 'projections', {'sessionId': session_id})
    values = proj_res.get('result', {}).get('value', {}).get('values', {})
    print(f"Polling {i+1} keys:", list(values.keys()))
    if values.get('transcript'):
        print("Transcript turns count:", len(values['transcript']))
        for item in values['transcript']:
            print("Item:", item)
        break
