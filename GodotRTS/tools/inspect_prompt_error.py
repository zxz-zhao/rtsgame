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

r = s.post(f'http://{authority}/api/session/prompt', json={
    'type': 'client-request',
    'rpcId': str(uuid.uuid4()),
    'method': 'session/prompt',
    'payload': {'args': {'request': {
        'sessionId': 'session-0e72f3cb-f30f-420c-b516-19da7ba4a6fd',
        'requestId': str(uuid.uuid4()),
        'content': [{'type': 'text', 'text': 'test'}]
    }}}
})
print("Status:", r.status_code)
print(json.dumps(r.json(), indent=2, ensure_ascii=False))
