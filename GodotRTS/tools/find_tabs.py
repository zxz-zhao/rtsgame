import os
for root, dirs, files in os.walk('.'):
    if '.git' in root or '.godot' in root or '.agents' in root: continue
    for f in files:
        if f.endswith(('.cs', '.tscn')):
            p = os.path.join(root, f)
            try:
                with open(p, 'r', encoding='utf-8') as fp:
                    content = fp.read()
                    if '"机动"' in content:
                        print(p)
            except:
                pass
