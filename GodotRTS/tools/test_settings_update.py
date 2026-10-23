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

r = s.post(f'http://{authority}/api/settings/update', json={
    'type': 'client-request',
    'rpcId': str(uuid.uuid4()),
    'method': 'settings/update',
    'payload': {'args': {
        'ns': 'llm-deepseek',
        'patch': {
            'baseURL': 'http://127.0.0.1:3000',
            'apiKeyEnv': 'DEEPSEEK_API_KEY'
        },
        'expectedRevision': 0
    }}
})
print("Update status:", r.status_code)
val = r.json()['result']['value']['value']
print("Updated baseURL:", val.get('baseURL'))
print("Updated apiKeyEnv:", val.get('apiKeyEnv'))
