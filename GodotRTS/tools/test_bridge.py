# -*- coding: utf-8 -*-
import urllib.request
import json
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

req = urllib.request.Request(
    'http://127.0.0.1:8000/v1/chat/completions',
    data=json.dumps({
        'model': 'gemini-3.8-flash',
        'messages': [{'role': 'user', 'content': '请回复六个字：系统运行正常'}],
        'stream': True
    }).encode('utf-8'),
    headers={'Content-Type': 'application/json', 'Authorization': 'Bearer cockpit-token'}
)

print("Sending request to bridge...")
try:
    with urllib.request.urlopen(req, timeout=25) as resp:
        for line in resp:
            l = line.decode('utf-8', errors='replace').strip()
            if l.startswith('data:') and not l.endswith('[DONE]'):
                try:
                    d = json.loads(l[5:].strip())
                    chunk = d['choices'][0]['delta'].get('content', '')
                    print(chunk, end='', flush=True)
                except Exception:
                    pass
        print("\n[Done stream]")
except Exception as e:
    print('Bridge error:', e)
