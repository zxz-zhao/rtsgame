import sqlite3
import json

conn = sqlite3.connect(r'E:\code\c++\UnityRTS\GodotRTS\tools\new-api\one-api.db')
c = conn.cursor()

# Get all keys in options
rows = c.execute("SELECT key, value FROM options").fetchall()
print("All option keys:", [r[0] for r in rows])

# Update ModelRatio
model_ratio_row = c.execute("SELECT value FROM options WHERE key = 'ModelRatio'").fetchone()
if model_ratio_row:
    ratios = json.loads(model_ratio_row[0])
    ratios['deepseek-flash'] = 0.2
    ratios['deepseek-v4-pro'] = 0.2
    new_val = json.dumps(ratios, separators=(',', ':'))
    c.execute("UPDATE options SET value = ? WHERE key = 'ModelRatio'", (new_val,))
    conn.commit()
    print("Updated ModelRatio successfully!")

conn.close()
