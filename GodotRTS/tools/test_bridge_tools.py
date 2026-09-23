# -*- coding: utf-8 -*-
import sys
from rocketchat_dify_bridge import call_dify_agent

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

print("Testing Tool Invocation via call_dify_agent...")
query = "请读取 scripts/battle/BattleMinimap.cs 的前 15 行并给出简要说明"
reply = call_dify_agent(query, user_id="test_admin_tools")

print("\n" + "=" * 60)
print(reply)
print("=" * 60)
