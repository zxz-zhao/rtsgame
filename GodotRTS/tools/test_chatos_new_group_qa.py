# -*- coding: utf-8 -*-
import asyncio
import json
import sys
from chatos_driver import chatos_stream_generator
import chatos_driver

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

async def test():
    cfg = chatos_driver.load_config()
    cfg['group_id'] = '901128299'
    chatos_driver.load_config = lambda: cfg

    req = {
        'model': 'chatos-deepseek',
        'messages': [{'role': 'user', 'content': '你是什么模型？由哪家公司研发？请准确回答。'}]
    }
    full = []
    print("Sending question to group 901128299 (model=deepseek-chat)...")
    async for chunk in chatos_stream_generator(req):
        if chunk.startswith('data: ') and chunk.strip() != 'data: [DONE]':
            try:
                d = json.loads(chunk[6:])
                c = d.get('choices', [{}])[0].get('delta', {}).get('content', '')
                full.append(c)
                print(c, end='', flush=True)
            except Exception: pass
    print("\n\nFull reply:\n", "".join(full))

asyncio.run(test())
