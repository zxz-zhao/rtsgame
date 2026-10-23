# -*- coding: utf-8 -*-
import urllib.request
import json
import sys
sys.path.insert(0, 'tools')
sys.path.insert(0, r'H:\api')
from chatos_driver import decrypt_chatos

token = 'i/+JaKioKUbULw+3o+rZGN12LfU0wiFTGVaSZYA/OPc='
headers = {'Content-Type': 'application/json', 'Authorization': token, 'User-Agent': 'Mozilla/5.0'}
url = 'https://siomi.aichat83.com/go/api/group/list'
payload = {'version': '1.1.1', 'os': 'pc', 'channel': 'chatos', 'language': 'zh', 'pars': {'user_id': '2378416'}}
req = urllib.request.Request(url, data=json.dumps(payload).encode(), headers=headers)
raw = urllib.request.urlopen(req).read().decode().strip('\"')
dec = decrypt_chatos(raw)
import asyncio
from chatos_core import execute_chatos_request, chatos_stream_generator

acc = {
    "user_id": "2378416",
    "token": token,
    "group_id": "901128299",
    "base_url": "https://siomi.aichat83.com",
    "iv": "hj6cdzrhj72x8ht1"
}

body = {
    "model": "deepseek-chat",
    "messages": [{"role": "user", "content": "你是谁？你能为我做什么？"}]
}


async def run_test():
    print("Testing group 901128301 (type 1)...")
    async for chunk in chatos_stream_generator(acc, body, "deepseek-chat"):
        if chunk.startswith("data: ") and not chunk.startswith("data: [DONE]"):
            d = json.loads(chunk[6:].strip())
            delta = d.get("choices", [{}])[0].get("delta", {}).get("content", "")
            print(delta, end="", flush=True)
    print("\n[DONE]")

asyncio.run(run_test())






