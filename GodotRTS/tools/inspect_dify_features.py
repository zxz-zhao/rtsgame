import subprocess

sql = """
\\d app_model_configs;
SELECT id, app_id, user_input_form, file_upload FROM app_model_configs WHERE app_id = 'c271447f-5a9b-48d1-a403-af7b0306c7ee' ORDER BY created_at DESC LIMIT 1;
"""

cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"]
proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
stdout, stderr = proc.communicate(input=sql.encode("utf-8"))

print("STDOUT:\n", stdout.decode("utf-8", errors="replace"))
print("STDERR:\n", stderr.decode("utf-8", errors="replace"))
