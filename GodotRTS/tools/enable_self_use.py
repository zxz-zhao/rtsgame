import sqlite3

conn = sqlite3.connect(r'E:\code\c++\UnityRTS\GodotRTS\tools\new-api\one-api.db')
c = conn.cursor()
val = c.execute("SELECT value FROM options WHERE key = 'SelfUseModeEnabled'").fetchone()
print('SelfUseModeEnabled was:', val)
c.execute("UPDATE options SET value = 'true' WHERE key = 'SelfUseModeEnabled'")
conn.commit()
print('SelfUseModeEnabled updated to true!')
conn.close()
