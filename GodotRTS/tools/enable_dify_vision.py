# -*- coding: utf-8 -*-
import subprocess
import json

# 1. Fetch current encrypted_config for the model credential
sql_fetch = "SELECT id, encrypted_config FROM provider_model_credentials WHERE id = '01a0aa02-63a5-7ca9-82b9-e0099e613512';"
proc = subprocess.Popen(["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t"], 
                        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
stdout, stderr = proc.communicate(sql_fetch.encode("utf-8"))

out_str = stdout.decode("utf-8").strip()
parts = out_str.split("|", 1)
if len(parts) == 2:
    cred_id = parts[0].strip()
    raw_cfg = parts[1].strip()
    cfg = json.loads(raw_cfg)
    print("Previous vision_support:", cfg.get("vision_support"))
    print("Previous agent_thought_support:", cfg.get("agent_thought_support"))

    # Update settings
    cfg["vision_support"] = "support"
    cfg["agent_thought_support"] = "supported"
    new_cfg_str = json.dumps(cfg)

    # SQL to update provider_model_credentials
    sql_update_cred = f"UPDATE provider_model_credentials SET encrypted_config = {repr(new_cfg_str)} WHERE id = '01a0aa02-63a5-7ca9-82b9-e0099e613512';"
    proc2 = subprocess.Popen(["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"], 
                             stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    out2, err2 = proc2.communicate(sql_update_cred.encode("utf-8"))
    print("Updated provider_model_credentials:", out2.decode("utf-8", errors="replace").strip())

# 2. Update app_model_configs file_upload to enable images
file_upload_val = json.dumps({
    "image": {
        "enabled": True,
        "number_limits": 6,
        "detail": "high",
        "transfer_methods": ["remote_url", "local_file"]
    }
})

sql_update_app = f"UPDATE app_model_configs SET file_upload = {repr(file_upload_val)} WHERE app_id = 'c271447f-5a9b-48d1-a403-af7b0306c7ee';"
proc3 = subprocess.Popen(["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"], 
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
out3, err3 = proc3.communicate(sql_update_app.encode("utf-8"))
print("Updated app_model_configs file_upload:", out3.decode("utf-8", errors="replace").strip())
