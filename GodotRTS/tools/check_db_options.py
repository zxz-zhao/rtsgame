import sqlite3

conn = sqlite3.connect(r'E:\code\c++\UnityRTS\GodotRTS\tools\new-api\one-api.db')
c = conn.cursor()
c.execute("SELECT key, value FROM options")
for k, v in c.fetchall():
    if any(w in k.lower() for w in ['tool', 'func', 'stream', 'claude', 'anthropic', 'prompt', 'convert']):
        print(f"{k}: {v}")
