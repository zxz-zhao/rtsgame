# -*- coding: utf-8 -*-
import subprocess
import json

cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t", "-c", "SELECT graph FROM workflows WHERE id = 'df82a113-cb93-445b-8f89-286e42b9a182';"]
proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
stdout, stderr = proc.communicate()

content = stdout.decode('utf-8', errors='replace').strip()
if content:
    data = json.loads(content)
    for n in data.get("nodes", []):
        ndata = n.get("data", {})
        title = ndata.get("title")
        ntype = ndata.get("type")
        nid = n.get("id")
        print(f"=== [{nid}] {title} ({ntype}) ===")
        if ntype == "llm":
            prompt_cfg = ndata.get("prompt_template", [])
            print(f"Model: {ndata.get('model', {}).get('name')}")
            for p in prompt_cfg:
                role = p.get("role")
                text = p.get("text", "")
                print(f"Role: {role} | Length: {len(text)}")
                print(text[:300] + "..." if len(text) > 300 else text)
        elif ntype == "code":
            print(f"Code:\n{ndata.get('code', '')}")
        elif ntype == "if-else":
            print(f"Cases: {ndata.get('conditions', [])}")
