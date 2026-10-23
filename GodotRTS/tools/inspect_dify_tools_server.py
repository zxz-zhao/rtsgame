# -*- coding: utf-8 -*-
import subprocess
import json

cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t", "-c", "SELECT schema FROM tool_api_providers WHERE id = '5826800b-a7f3-4ab8-b300-e6ace2f3ea60';"]
out = subprocess.check_output(cmd).decode("utf-8").strip()
try:
    d = json.loads(out)
    print("OpenAPI Servers:", d.get("servers"))
    print("OpenAPI Endpoints:", list(d.get("paths", {}).keys()))
except Exception as e:
    print("Error parsing schema:", e)
    print("Raw output sample:", out[:300])
