# -*- coding: utf-8 -*-
import json
import urllib.request
import urllib.error

ADMIN_TOKEN = "rocket_dify_admin_token_20260923"
ADMIN_USER_ID = "ykykGuDpmeJqtiEHy"

def create_integration():
    payload = {
        "type": "webhook-outgoing",
        "name": "Dify AI Agent",
        "enabled": True,
        "username": "rocket.cat",
        "channel": "all_public_channels",
        "event": "sendMessage",
        "urls": ["http://host.docker.internal:5005/webhook"],
        "triggerWords": ["@ai", "@dify", "/ai"],
        "triggerWordAnywhere": True,
        "runOnEdits": False,
        "scriptEnabled": True,
        "script": """class Script {
  process_outgoing_response({ request, response }) {
    var data = JSON.parse(response.content);
    return {
      content: {
        text: data.text
      }
    };
  }
}"""
    }

    req = urllib.request.Request(
        "http://127.0.0.1:3000/api/v1/integrations.create",
        data=json.dumps(payload).encode('utf-8'),
        headers={
            "X-Auth-Token": ADMIN_TOKEN,
            "X-User-Id": ADMIN_USER_ID,
            "Content-Type": "application/json"
        },
        method="POST"
    )

    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            print("Create Integration Response:\n", json.dumps(data, indent=2, ensure_ascii=False))
            return data
    except urllib.error.HTTPError as e:
        print(f"HTTP Error {e.code}: {e.read().decode('utf-8')}")
    except Exception as e:
        print("Error:", e)

if __name__ == '__main__':
    create_integration()
