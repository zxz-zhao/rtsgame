import requests
import json

url = "http://127.0.0.1:3000/v1/messages"
headers = {
    "x-api-key": "pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q",
    "anthropic-version": "2023-06-01",
    "content-type": "application/json"
}

payload = {
    "model": "deepseek-flash",
    "max_tokens": 100,
    "messages": [
        {"role": "user", "content": "请回复：【测试连接成功】"}
    ]
}

try:
    resp = requests.post(url, headers=headers, json=payload, timeout=30)
    print("Status code:", resp.status_code)
    print("Response body:", resp.text)
except Exception as e:
    print("Error:", e)
