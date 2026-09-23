# -*- coding: utf-8 -*-
import sys
import json
import urllib.request
import urllib.error

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ADMIN_TOKEN = "rocket_dify_admin_token_20260923"
ADMIN_USER_ID = "ykykGuDpmeJqtiEHy"

def get_history(room_id: str = "GENERAL", count: int = 5):
    url = f"http://127.0.0.1:3000/api/v1/channels.history?roomId={room_id}&count={count}"
    req = urllib.request.Request(
        url,
        headers={
            "X-Auth-Token": ADMIN_TOKEN,
            "X-User-Id": ADMIN_USER_ID,
        },
        method="GET"
    )

    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            messages = data.get("messages", [])
            print(f"Fetched {len(messages)} messages from #{room_id}:")
            for m in reversed(messages):
                u = m.get("u", {}).get("username", "unknown")
                msg = m.get("msg", "")
                print(f"[{u}]: {msg}\n---")
            return data
    except urllib.error.HTTPError as e:
        print(f"HTTP Error {e.code}: {e.read().decode('utf-8')}")
    except Exception as e:
        print("Error:", e)

if __name__ == "__main__":
    get_history()
