import asyncio, json, urllib.parse, httpx

with open(r'H:\api\accounts.json', 'r', encoding='utf-8') as f:
    accs = json.load(f)
acc = accs[0]

qid = '1115156'
event_url = f"{acc['base_url']}/go/api/event/see?question_id={qid}&group_id={acc['group_id']}&user_id={acc['user_id']}&token={urllib.parse.quote(acc['token'])}&server_id=&model=deepseek-chat"
headers = {
    'Accept': 'text/event-stream',
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
}

async def stream():
    async with httpx.AsyncClient(timeout=30.0, trust_env=False) as client:
        async with client.stream('GET', event_url, headers=headers) as resp:
            async for chunk in resp.aiter_text():
                print(chunk, end='', flush=True)

asyncio.run(stream())
