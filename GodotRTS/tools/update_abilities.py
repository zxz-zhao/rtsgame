# -*- coding: utf-8 -*-
import sqlite3

db_path = r'E:\code\c++\UnityRTS\GodotRTS\tools\new-api\one-api.db'
conn = sqlite3.connect(db_path)
c = conn.cursor()

# 1. Remove deepseek-flash and deepseek-v4-pro from Channel 1
c.execute("DELETE FROM abilities WHERE channel_id = 1 AND model IN ('deepseek-flash', 'deepseek-v4-pro')")
print("Removed deepseek-flash & deepseek-v4-pro from Channel 1 abilities.")

# 2. Add abilities for Channel 2 (Dify Agent Bridge)
c.execute("DELETE FROM abilities WHERE channel_id = 2")
for m in ['dify-agent', 'deepseek-flash', 'deepseek-v4-pro', 'dify-architect']:
    c.execute(
        'INSERT INTO abilities ("group", model, channel_id, enabled, priority, weight) VALUES (?, ?, ?, ?, ?, ?)',
        ('default', m, 2, 1, 50, 10)
    )

conn.commit()
print("Updated abilities count:", c.execute('SELECT count(*) FROM abilities').fetchone()[0])
print("\n--- All abilities for deepseek & dify ---")
for row in c.execute("SELECT * FROM abilities WHERE model LIKE '%deepseek%' OR model LIKE '%dify%'"):
    print(row)

conn.close()
