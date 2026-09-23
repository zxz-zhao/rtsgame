# -*- coding: utf-8 -*-
import sys
from pathlib import Path

# Add H:\webbot to path
sys.path.insert(0, r"H:\webbot")
from dify_bridge import ask_dify

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

query = "请为步兵单位编写一个跳弹与后坐力衰减函数"
print(f"🎯 发送提问: {query}\n" + "=" * 60)

res = ask_dify(query, on_chunk=lambda chunk: print(chunk, end="", flush=True))

print("\n" + "=" * 60)
print(f"✅ 完成！会话ID: {res.get('conversation_id')}")
