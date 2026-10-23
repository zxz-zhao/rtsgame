# -*- coding: utf-8 -*-
import json

# 1. Update H:\api\accounts.json
try:
    with open(r'H:\api\accounts.json', 'r', encoding='utf-8') as f:
        accs = json.load(f)
    for a in accs:
        if a.get('group_id') == '901128216':
            a['group_id'] = '901128301'
    with open(r'H:\api\accounts.json', 'w', encoding='utf-8') as f:
        json.dump(accs, f, indent=2, ensure_ascii=False)
    print("Updated H:\\api\\accounts.json group_id to 901128301")
except Exception as e:
    print("Error updating H:\\api\\accounts.json:", e)

# 2. Update H:\api\account_pool.py
try:
    with open(r'H:\api\account_pool.py', 'r', encoding='utf-8') as f:
        content = f.read()
    new_content = content.replace('"901128216"', '"901128301"')
    with open(r'H:\api\account_pool.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Updated H:\\api\\account_pool.py default group_id to 901128301")
except Exception as e:
    print("Error updating H:\\api\\account_pool.py:", e)

# 3. Update tools/chatos_config.json
try:
    with open('tools/chatos_config.json', 'r', encoding='utf-8') as f:
        cfg = json.load(f)
    cfg['group_id'] = '901128301'
    with open('tools/chatos_config.json', 'w', encoding='utf-8') as f:
        json.dump(cfg, f, indent=2, ensure_ascii=False)
    print("Updated tools/chatos_config.json group_id to 901128301")
except Exception as e:
    print("Error updating tools/chatos_config.json:", e)
