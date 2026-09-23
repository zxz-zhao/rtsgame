# -*- coding: utf-8 -*-
import subprocess

def run_sql(sql):
    cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    stdout, stderr = proc.communicate(input=sql.encode("utf-8"))
    return stdout.decode("utf-8", errors="replace")

sql = "SELECT id, name, mode, enable_api, api_rpm, api_rph FROM apps WHERE id = 'c271447f-5a9b-48d1-a403-af7b0306c7ee';"
print("APP:\n", run_sql(sql))

sql_cfg = "SELECT id, app_id, agent_mode, prompt_type, configs FROM app_model_configs WHERE app_id = 'c271447f-5a9b-48d1-a403-af7b0306c7ee';"
print("APP CONFIG:\n", run_sql(sql_cfg))
