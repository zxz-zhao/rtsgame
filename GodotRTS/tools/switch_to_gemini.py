import subprocess

sql = """UPDATE app_model_configs SET model = '{"provider": "langgenius/openai_api_compatible/openai_api_compatible", "name": "gemini-3.8-flash", "mode": "chat", "completion_params": {"temperature": 0.3, "max_tokens": 8192, "stop": []}}' WHERE app_id = 'c271447f-5a9b-48d1-a403-af7b0306c7ee';"""
cmd = ['docker', 'exec', '-i', 'docker-db_postgres-1', 'psql', '-U', 'postgres', '-d', 'dify']
p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
out, err = p.communicate(sql.encode('utf-8'))
print('OUT:', out.decode())
if err:
    print('ERR:', err.decode())
