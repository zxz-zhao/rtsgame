# -*- coding: utf-8 -*-
import urllib.request
import json
import sys
from chatos_driver import decrypt_chatos

token = "i/+JaKioKUbULw+3o+rZGN12LfU0wiFTGVaSZYA/OPc="
headers = {
    "Content-Type": "application/json",
    "Authorization": token,
    "User-Agent": "Mozilla/5.0"
}
url = "https://siomi.aichat83.com/go/api/group/add"
payload = {
    "version": "1.1.1",
    "os": "pc",
    "channel": "chatos",
    "language": "zh",
    "pars": {
        "user_id": "2378416",
        "group_name": "DeepSeek测试",
        "model": "deepseek-chat"
    }
}
req = urllib.request.Request(url, data=json.dumps(payload).encode(), headers=headers)
with urllib.request.urlopen(req) as resp:
    raw = resp.read().decode().strip('"')
    print(decrypt_chatos(raw))
