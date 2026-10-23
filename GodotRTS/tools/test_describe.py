import requests, uuid, hmac, hashlib, base64, time, json, yaml

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

r = s.post(f'http://{authority}/api/credentials/describe', json={
    'type': 'client-request',
    'rpcId': str(uuid.uuid4()),
    'method': 'credentials/describe',
    'payload': {'args': {'request': {'refs': ['DEEPSEEK_API_KEY']}}}
}, timeout=5)
print('credentials/describe:', r.text)

r2 = s.post(f'http://{authority}/api/settings/describe', json={
    'type': 'client-request',
    'rpcId': str(uuid.uuid4()),
    'method': 'settings/describe',
    'payload': {'args': {'request': {}}}
}, timeout=5)
print('settings/describe:', r2.text)
