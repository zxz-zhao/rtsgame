# -*- coding: utf-8 -*-
import sqlite3
import json

db_path = r'E:\code\c++\UnityRTS\GodotRTS\tools\new-api\one-api.db'
conn = sqlite3.connect(db_path)
c = conn.cursor()

# 1. Update ModelRatio in options
row = c.execute("SELECT value FROM options WHERE key = 'ModelRatio'").fetchone()
if row:
    ratios = json.loads(row[0])
    ratios['dify-agent'] = 0.2
    ratios['dify-architect'] = 0.2
    ratios['deepseek-flash'] = 0.2
    ratios['deepseek-v4-pro'] = 0.2
    c.execute("UPDATE options SET value = ? WHERE key = 'ModelRatio'", (json.dumps(ratios, separators=(',', ':')),))
    print("Updated ModelRatio with dify-agent!")

# 2. Update Channel 1: Remove deepseek-flash and deepseek-v4-pro mappings
ch1_mapping = '{"claude-3-7-sonnet": "claude-sonnet-4-6", "claude-3-5-sonnet": "claude-sonnet-4-6"}'
c.execute("UPDATE channels SET model_mapping = ? WHERE id = 1", (ch1_mapping,))
print("Updated Channel 1 model_mapping (removed deepseek-flash).")

# 3. Update Channel 2: Dify-Agent-Bridge
ch2_models = 'dify-agent,deepseek-flash,deepseek-v4-pro,dify-architect'
ch2_mapping = '{"deepseek-flash": "dify-agent", "deepseek-v4-pro": "dify-agent"}'
c.execute("""
UPDATE channels SET 
    models = ?,
    model_mapping = ?,
    response_time = 50,
    test_time = 1791704500,
    channel_info = '{"is_multi_key":false,"multi_key_size":0,"multi_key_status_list":null,"multi_key_polling_index":0,"multi_key_mode":""}'
WHERE id = 2
""", (ch2_models, ch2_mapping))
print("Updated Channel 2 settings.")

conn.commit()

# Print current channels
rows = c.execute("SELECT id, name, priority, base_url, models, model_mapping FROM channels").fetchall()
for r in rows:
    print(r)

conn.close()
