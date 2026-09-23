# -*- coding: utf-8 -*-
import httpx
import json
import asyncio
import sys

sys.path.append('e:/code/c++/UnityRTS/GodotRTS/tools')
from cockpit_bridge import pool, CLOUDCODE_BASE, get_best_proxy

async def test_sys_instruction():
    acc = await pool.get_valid_account()
    url = f'{CLOUDCODE_BASE}/v1internal:streamGenerateContent?alt=sse'
    headers = {
        'Authorization': f'Bearer {acc["token"]}',
        'Content-Type': 'application/json',
        'User-Agent': 'antigravity/2.5.5 windows/amd64 google-api-nodejs-client/10.3.0'
    }

    # Test 1: With systemInstruction
    payload1 = {
        'model': 'gemini-2.5-flash',
        'request': {
            'systemInstruction': {
                'role': 'user',
                'parts': [{'text': 'You are a helpful assistant.'}]
            },
            'contents': [
                {'role': 'user', 'parts': [{'text': 'Say hello'}]}
            ]
        }
    }
    print("Testing with systemInstruction streaming...")
    async with httpx.AsyncClient(proxy=get_best_proxy(), trust_env=False, timeout=15.0) as c:
        async with c.stream('POST', url, headers=headers, json=payload1) as resp:
            print("Status with systemInstruction:", resp.status_code)
            async for chunk in resp.aiter_text():
                print("CHUNK:", repr(chunk))

if __name__ == '__main__':
    asyncio.run(test_sys_instruction())
