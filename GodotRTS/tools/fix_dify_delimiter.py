# -*- coding: utf-8 -*-
import subprocess
import json

def run_sql(sql):
    cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    stdout, stderr = proc.communicate(input=sql.encode("utf-8"))
    return stdout.decode("utf-8", errors="replace"), stderr.decode("utf-8", errors="replace")

def main():
    sql = "SELECT id, encrypted_config FROM provider_model_credentials WHERE id = '01a0aa02-63a5-7ca9-82b9-e0099e613512';"
    out, err = run_sql(sql)
    for line in out.splitlines():
        if "{" in line:
            parts = line.split("|")
            id_val = parts[0].strip()
            cfg_str = parts[1].strip()
            cfg_obj = json.loads(cfg_str)
            print("Before:", repr(cfg_obj.get("stream_mode_delimiter")))
            cfg_obj["stream_mode_delimiter"] = "\n\n"
            new_json = json.dumps(cfg_obj)
            escaped_json = new_json.replace("'", "''")
            update_sql = f"UPDATE provider_model_credentials SET encrypted_config = '{escaped_json}' WHERE id = '{id_val}';"
            up_out, up_err = run_sql(update_sql)
            print("Update:", up_out, up_err)

    # Verify all
    sql_all = "SELECT id, encrypted_config FROM provider_model_credentials;"
    out_all, _ = run_sql(sql_all)
    for line in out_all.splitlines():
        if "{" in line:
            parts = line.split("|")
            id_val = parts[0].strip()
            cfg_str = parts[1].strip()
            cfg_obj = json.loads(cfg_str)
            print(f"Verified ID {id_val}: delimiter = {repr(cfg_obj.get('stream_mode_delimiter'))}")

if __name__ == "__main__":
    main()
