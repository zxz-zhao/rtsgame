import sys
import json
import urllib.request
import urllib.error

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

ADMIN_TOKEN = "rocket_dify_admin_token_20260923"
ADMIN_USER_ID = "ykykGuDpmeJqtiEHy"

def post_message(text: str, channel: str = "#general"):
    payload = {
        "channel": channel,
        "text": text
    }
    req = urllib.request.Request(
        "http://127.0.0.1:3000/api/v1/chat.postMessage",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "X-Auth-Token": ADMIN_TOKEN,
            "X-User-Id": ADMIN_USER_ID,
            "Content-Type": "application/json"
        },
        method="POST"
    )

    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            print("PostMessage Success:\n", json.dumps(data, indent=2, ensure_ascii=False))
            return data
    except urllib.error.HTTPError as e:
        print(f"HTTP Error {e.code}: {e.read().decode('utf-8')}")
    except Exception as e:
        print("Error:", e)

if __name__ == "__main__":
    post_message("@ai 帮我看看当前游戏还有那些需要做的 你能发图吗")
