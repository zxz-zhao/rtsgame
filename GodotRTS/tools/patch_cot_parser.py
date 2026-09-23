# -*- coding: utf-8 -*-
import subprocess

script = """
import re

path = '/app/api/core/agent/output_parser/cot_output_parser.py'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

target = '''        def parse_action(action) -> Union[str, AgentScratchpadUnit.Action]:
            action_name = None
            action_input = None
            if isinstance(action, str):
                try:
                    action = json.loads(action, strict=False)
                except json.JSONDecodeError:
                    return action or ""

            # cohere always returns a list
            if isinstance(action, list) and len(action) == 1:
                action = action[0]

            for key, value in action.items():
                if "input" in key.lower():
                    action_input = value
                else:
                    action_name = value

            if action_name is not None and action_input is not None:'''

replacement = '''        def parse_action(action) -> Union[str, AgentScratchpadUnit.Action]:
            action_name = None
            action_input = None
            if isinstance(action, str):
                try:
                    action = json.loads(action, strict=False)
                except json.JSONDecodeError:
                    return action or ""

            # cohere always returns a list
            if isinstance(action, list) and len(action) == 1:
                action = action[0]

            if isinstance(action, dict):
                if "action" in action:
                    action_name = action["action"]
                    if "action_input" in action:
                        action_input = action["action_input"]
                    else:
                        rem = {k: v for k, v in action.items() if k != "action"}
                        action_input = rem if rem else ""
                else:
                    for key, value in action.items():
                        if "input" in key.lower():
                            action_input = value
                        else:
                            action_name = value

            if action_name is not None and action_input is not None:'''

if target in content:
    content = content.replace(target, replacement)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("PATCHED SUCCESSFULLY")
else:
    print("TARGET NOT FOUND or ALREADY PATCHED")
"""

for c in ['docker-api-1', 'docker-worker-1']:
    p = subprocess.run(['docker', 'exec', '-i', c, 'python3', '-c', script], capture_output=True, text=True)
    print(c, p.stdout.strip(), p.stderr.strip())
