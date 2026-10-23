import sqlite3

conn = sqlite3.connect(r'E:\code\c++\UnityRTS\GodotRTS\tools\new-api\one-api.db')
c = conn.cursor()
tables = [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
print('Tables:', tables)

for t in tables:
    if 'option' in t.lower() or 'price' in t.lower() or 'model' in t.lower() or 'ratio' in t.lower():
        print(f'=== Table {t} ===')
        try:
            rows = c.execute(f'SELECT * FROM {t}').fetchall()
            for row in rows[:20]:
                print(row)
        except Exception as e:
            print('Error:', e)

conn.close()
