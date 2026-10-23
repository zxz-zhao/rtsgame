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
cookie_val = f"v1.{body_b64}.{base64.urlsafe_b64encode(sig).decode().rstrip('=')}"

s = requests.Session()
s.cookies.set(cname, cookie_val)

def call_rpc(namespace, method, request_args={}):
    r = s.post(f'http://{authority}/api/{namespace}/{method}', json={
        'type': 'client-request',
        'rpcId': str(uuid.uuid4()),
        'method': f'{namespace}/{method}',
        'payload': {'args': {'request': request_args}}
    }, timeout=30)
    return r.json()

sid = 'session-903f62a7-bbce-48fe-b8b1-097a1fa58560'
res = call_rpc('session', 'page', {
    'address': {'kind': 'session', 'sessionId': sid},
    'throughSeq': 18
})

if res.get('result', {}).get('ok'):
    records = res['result']['value'].get('records', [])
    print(f"Total records returned: {len(records)}")
    for r in records:
        evt = r.get('event', {})
        t = evt.get('type')
        data = evt.get('data')
        print(f"[{evt.get('seq')}] Event: {t}")
        if 'message' in t or 'title' in t:
            print("  -> Data:", json.dumps(data, ensure_ascii=False, indent=2))
else:
    print("Error:", res)
