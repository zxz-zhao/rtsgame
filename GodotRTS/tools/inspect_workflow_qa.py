# -*- coding: utf-8 -*-
import subprocess
import json

cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t", "-c", "SELECT graph FROM workflows WHERE id = 'df82a113-cb93-445b-8f89-286e42b9a182';"]
proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
stdout, stderr = proc.communicate()

content = stdout.decode('utf-8', errors='replace').strip()
if content:
    try:
        data = json.loads(content)
        nodes = data.get("nodes", [])
        print(f"Total Nodes: {len(nodes)}")
        for n in nodes:
            ndata = n.get("data", {})
            print(f"- Node: {n.get('id')} | Type: {ndata.get('type')} | Title: {ndata.get('title')}")
    except Exception as e:
        print("JSON parse error:", e)
        print("Raw prefix:", content[:300])
else:
    print("No content")
