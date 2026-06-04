import os, re

d = r'e:\code\c++\UnityRTS\Assets\External\Kenney\BlockyCharacters\Models\FBX format'
pattern = re.compile(r'(    globalScale: )1(\r?\n    meshCompression)')

for fn in os.listdir(d):
    if not fn.endswith('.meta'):
        continue
    path = os.path.join(d, fn)
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    new_content = pattern.sub(r'\g<1>0.01\2', content)
    if new_content != content:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f'Fixed: {fn}')
    else:
        print(f'Skip (already ok or not matched): {fn}')
