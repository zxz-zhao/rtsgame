# -*- coding: utf-8 -*-
import subprocess
import json
import sys

cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t", "-c", "SELECT graph FROM workflows WHERE id = 'df82a113-cb93-445b-8f89-286e42b9a182';"]
proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
stdout, stderr = proc.communicate()

content = stdout.decode('utf-8', errors='replace').strip()
if content:
    data = json.loads(content)
    with open("tools/workflow_detail_dump.json", "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print("Dumped workflow details to tools/workflow_detail_dump.json")
