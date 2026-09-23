# -*- coding: utf-8 -*-
import sys
from rocketchat_dify_bridge import call_dify_agent

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

print("Calling Dify Agent via rocketchat_dify_bridge...")
query = "帮我为游戏画一个现代科技感的 RTS 单位信息卡片 UI (UnitStatusCard.tscn)，完成后请使用工具把效果图发出来"
reply = call_dify_agent(query, user_id="test_admin_ui_draw")

print("\n" + "=" * 60)
print(reply)
print("=" * 60)
