import sys, os
sys.path.append(r'H:\api')
import asyncio, json, urllib.parse, httpx
from chatos_core import execute_chatos_request

with open(r'H:\api\accounts.json', 'r', encoding='utf-8') as f:
    accs = json.load(f)
acc = accs[0]

prompt = "请不要只做口头确认。请直接输出方案 B（模型中间层核心逻辑）的完整 Python 实现代码，包含核心拦截逻辑、FastAPI 服务和测试脚本。"

async def run():
    qid = await execute_chatos_request(acc, prompt, 'deepseek-chat')
    print("QID created:", qid)
    event_url = f"{acc['base_url']}/go/api/event/see?question_id={qid}&group_id={acc['group_id']}&user_id={acc['user_id']}&token={urllib.parse.quote(acc['token'])}&server_id=&model=deepseek-chat"
    headers = {
        'Accept': 'text/event-stream',
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
    }
    async with httpx.AsyncClient(timeout=60.0, trust_env=False) as client:
        async with client.stream('GET', event_url, headers=headers) as resp:
            full_text = []
            async for chunk in resp.aiter_text():
                for line in chunk.splitlines():
                    if line.startswith("data:"):
                        try:
                            d = json.loads(line[5:].strip())
                            txt = d.get("Data", "")
                            if txt:
                                full_text.append(txt)
                        except:
                            pass
            print("Total response length:", len("".join(full_text)))
            print("Preview first 500 chars:\n", "".join(full_text)[:500])

asyncio.run(run())
