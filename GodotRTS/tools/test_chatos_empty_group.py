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

for ep in [
    "/go/api/group/add", "/go/api/group/create", "/go/api/group/save",
    "/go/api/role/list", "/go/api/role/category", "/go/api/role/user",
    "/go/api/app/list", "/go/api/channel/list"
]:
    url = f"https://siomi.aichat83.com{ep}"
    payload = {
        "version": "1.1.1",
        "os": "pc",
        "channel": "chatos",
        "language": "zh",
        "pars": {"user_id": "2378416"}
    }
    req = urllib.request.Request(url, data=json.dumps(payload).encode(), headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=4) as resp:
            raw = resp.read().decode().strip('"')
            try:
                dec = decrypt_chatos(raw)
                print(f"{ep}: 200 OK | RetCode: {dec.get('retCode')} | Data keys: {list(dec.get('data', {}).keys()) if isinstance(dec.get('data'), dict) else type(dec.get('data'))}")
            except Exception as ex:
                print(f"{ep}: 200 OK | Decrypt err: {ex} | Raw: {raw[:60]}")
    except urllib.error.HTTPError as e:
        print(f"{ep}: HTTP {e.code}")
    except Exception as e:
        print(f"{ep}: {e}")
