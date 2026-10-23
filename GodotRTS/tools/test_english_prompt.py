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

create_res = call_rpc('session', 'create', {})
sid = create_res['result']['value']['sessionId']
print('Created session:', sid)

prompt_res = call_rpc('session', 'prompt', {
    'sessionId': sid,
    'requestId': str(uuid.uuid4()),
    'mode': 'queue',
    'content': [
        {'type': 'text', 'text': 'Hello, what is 1+1? Please answer in one word.'}
    ]
})
print('Prompt res:', prompt_res)

for i in range(12):
    time.sleep(2)
    # Check max cursor first via projections
    proj = call_rpc('session', 'projections', {'sessionId': sid})
    asOfSeq = proj.get('result', {}).get('value', {}).get('asOfSeq', 0)
    print(f"Polling {i+1}, asOfSeq = {asOfSeq}")
    if asOfSeq > 5:
        page_res = call_rpc('session', 'page', {
            'address': {'kind': 'session', 'sessionId': sid},
            'throughSeq': asOfSeq
        })
        if page_res.get('result', {}).get('ok'):
            records = page_res['result']['value'].get('records', [])
            for rec in records:
                evt = rec.get('event', {})
                if evt.get('type') == 'assistant/message':
                    print('ASSISTANT ANSWER FOUND!')
                    print(json.dumps(evt.get('data', {}).get('message', {}).get('content'), indent=2))
                    exit(0)
print('Done without answer.')
