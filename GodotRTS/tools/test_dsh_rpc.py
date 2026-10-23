import requests
import json
import uuid

# Check with session cookie or token
token = "Q1GUf6waF7jcM6hyQg3pBZJ3X4aD70tDLR5F5Kp3pYA"
base_url = "http://127.0.0.1:19387"

s = requests.Session()
# authenticate
resp_auth = s.get(f"{base_url}/?token={token}")
print("Auth response status:", resp_auth.status_code)
print("Cookies:", s.cookies.get_dict())

def call_rpc(namespace, method, args={}):
    rpc_id = str(uuid.uuid4())
    req_body = {
        "type": "client-request",
        "rpcId": rpc_id,
        "method": f"{namespace}/{method}",
        "payload": {"args": args}
    }
    r = s.post(f"{base_url}/api/{namespace}/{method}", json=req_body)
    print(f"=== {namespace}/{method} ({r.status_code}) ===")
    try:
        data = r.json()
        print(json.dumps(data, indent=2, ensure_ascii=False))
        return data
    except Exception:
        print(r.text)
        return None

call_rpc("llm", "listConfigurableProviders")
call_rpc("credentials", "describe", {"refs": ["DEEPSEEK_API_KEY"]})
call_rpc("settings", "describe")
