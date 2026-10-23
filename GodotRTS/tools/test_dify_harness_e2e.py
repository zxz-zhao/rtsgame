# -*- coding: utf-8 -*-
import urllib.request
import json
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

url = "http://127.0.0.1:3000/v1/messages"
headers = {
    "Content-Type": "application/json",
    "Authorization": "Bearer pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q",
    "anthropic-version": "2023-06-01"
}
payload = {
    "model": "deepseek-flash",
    "messages": [
        {"role": "user", "content": "请为我编写一个简单的 Python 脚本，用于列出当前目录下所有以 .py 结尾的文件，并统计总行数。请附带简短说明。"}
    ],
    "stream": True,
    "max_tokens": 2048
}

print(">> 发送端: DeepSeek Harness Anthropic Messages 协议")
print(">> 中转端: New-API (3000) -> Dify Agent Bridge (8003)")
print(">> 执行端: 本地 Dify 智能体 (9564) -> 破限/通用任务执行引擎")
print(">> 提问:", payload["messages"][0]["content"])
print("=" * 60)

req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"), headers=headers)
with urllib.request.urlopen(req, timeout=120) as resp:
    for raw_line in resp:
        line = raw_line.decode("utf-8", errors="replace").strip()
        if line.startswith("data: ") and not line.startswith("data: [DONE]"):
            try:
                data = json.loads(line[6:].strip())
                if data.get("type") == "content_block_delta":
                    text = data.get("delta", {}).get("text", "")
                    print(text, end="", flush=True)
            except Exception:
                pass

print("\n" + "=" * 60)
print(">> [SUCCESS] 端到端全链路流式传输完成！")
