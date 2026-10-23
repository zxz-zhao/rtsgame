# -*- coding: utf-8 -*-
import sqlite3
import time

conn = sqlite3.connect('tools/new-api/one-api.db')
c = conn.cursor()

c.execute("DELETE FROM channels WHERE id = 2")
sql = """INSERT INTO channels (
    id, type, key, open_ai_organization, test_model, status, name, weight, 
    created_time, test_time, response_time, base_url, other, balance, balance_updated_time, 
    models, "group", used_quota, model_mapping, status_code_mapping, priority, 
    auto_ban, other_info, tag, setting, param_override, header_override, remark, 
    channel_info, settings
) VALUES (
    2, 1, 'sk-dify-bridge-2026', '', 'dify-agent', 1, 'Dify-Agent-Bridge', 1,
    ?, 0, 0, 'http://127.0.0.1:8003', '', 0, 0,
    'dify-agent,deepseek-flash,dify-architect', 'default', 0,
    '{"deepseek-flash": "dify-agent"}', '', 50,
    0, '', '', '', '', '', 'Dify智能体专用通道',
    '', ''
)"""
c.execute(sql, (int(time.time()),))

# Remove deepseek-flash from Channel 1
c.execute("UPDATE channels SET models = replace(models, 'deepseek-flash,', '') WHERE id = 1")
c.execute("UPDATE channels SET models = replace(models, ',deepseek-flash', '') WHERE id = 1")
conn.commit()
print("Success! Channels configured.")

rows = c.execute("SELECT id, name, priority, base_url, models FROM channels").fetchall()
for r in rows:
    print(r)
