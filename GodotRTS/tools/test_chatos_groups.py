# -*- coding: utf-8 -*-
import urllib.request
import json
import sys
from chatos_driver import decrypt_chatos

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

token = "i/+JaKioKUbULw+3o+rZGN12LfU0wiFTGVaSZYA/OPc="
headers = {
    "Content-Type": "application/json",
    "Authorization": token,
    "User-Agent": "Mozilla/5.0"
}
url = "https://siomi.aichat83.com/go/api/group/list"
payload = {
    "version": "1.1.1",
    "os": "pc",
    "channel": "chatos",
    "language": "zh",
    "pars": {"user_id": "2378416"}
}
req = urllib.request.Request(url, data=json.dumps(payload).encode(), headers=headers)
raw = urllib.request.urlopen(req).read().decode().strip('"')
data = decrypt_chatos(raw)

print("RetCode:", data.get("retCode"))
res_data = data.get("data")
print("Data type:", type(res_data))
print("Data content:", res_data)
