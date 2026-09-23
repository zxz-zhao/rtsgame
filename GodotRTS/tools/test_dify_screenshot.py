import requests
import sys
import json

sys.stdout.reconfigure(encoding='utf-8')
url = 'http://127.0.0.1:9564/v1/chat-messages'
headers = {
    'Authorization': 'Bearer app-VOinOntcv4Ok9Abkq2sml5qC',
    'Content-Type': 'application/json'
}
payload = {
    'inputs': {},
    'query': '截一张 VictoryDialog 战斗胜利弹窗的实机画面发给我',
    'response_mode': 'streaming',
    'user': 'test_agent'
}
resp = requests.post(url, headers=headers, json=payload, stream=True, timeout=120)
for line in resp.iter_lines():
    if not line:
        continue
    line = line.decode('utf-8')
    if line.startswith('data:'):
        data = json.loads(line[5:])
        event = data.get('event')
        if event == 'agent_thought':
            tool = data.get('tool')
            thought = (data.get('thought') or '').strip()
            obs = (data.get('observation') or '').strip()
            if tool:
                print(f"[TOOL CALL] {tool} -> {data.get('tool_input')}")
            if obs:
                print(f"[OBS] {obs[:300]}")
            if thought:
                print(f"[THOUGHT] {thought[:100]}...")
        elif event in ('message', 'agent_message'):
            print(data.get('answer', ''), end='', flush=True)
print("\n=== FINISHED ===")
