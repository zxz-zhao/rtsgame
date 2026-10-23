import uuid
import subprocess
import json
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TENANT_ID = "0073f7b6-19a1-4e26-b8ec-f00c498c4d4c"
PROVIDER_NAME = "langgenius/openai_api_compatible/openai_api_compatible"
ENCRYPTED_API_KEY = "SFlCUklEOljC0aEaYXQs9RdF4xKW9qeLusbSG1XPW25/HFQxXveCd29diyHv8IVlrvfA9JBfB3N+IJfBS3KiXb52DOkFYTmX+LG3+CXmDcahc2urZnJKSW8EoHqNY7s/xpGROqbneBuM5Mc6h0URjNIub8yBXt0NUpStGPhwlFGJy3V55dAdPnjE3a39AU+DA34klY+jLHhQ5hnYveM/TCIHVbJdXEpiCR4TF8Q81tAnm3p3Llp2VQSj9Z8KQl4SpJ7wcVFHSk/lE+/OF/clWNGisset0f5M/8suVkPHfjDiyVEvKuWBjjkBqTmibsS+fJX8oty98w3CKT5Q/CXTjtkwgpuzA2PC5pQq7jbWkxElPXzG7zVyRkcnm8RmrzEctKm1P/LlTNVtTSb+TQbDQ4DL05s="

def run_psql(sql: str):
    cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    stdout, stderr = proc.communicate(input=sql.encode("utf-8"))
    return stdout.decode("utf-8", errors="replace"), stderr.decode("utf-8", errors="replace")

def ensure_claude_models():
    # 检查 claude-opus-4-6
    out, _ = run_psql(f"SELECT id FROM provider_models WHERE model_name = 'claude-opus-4-6' AND tenant_id = '{TENANT_ID}';")
    if "claude-opus-4-6" not in out:
        cred_id = str(uuid.uuid4())
        model_id = str(uuid.uuid4())
        cfg = {
            "api_key": ENCRYPTED_API_KEY,
            "endpoint_url": "http://host.docker.internal:8000/v1",
            "mode": "chat",
            "context_size": "200000",
            "max_tokens_to_sample": "8192",
            "agent_thought_support": "supported",
            "web_search_support": "not_supported",
            "compatibility_mode": "strict",
            "api_type": "chat_completions",
            "token_param_name": "auto",
            "user_identity_support": "support",
            "stream_include_usage": "enabled",
            "function_calling_type": "no_call",
            "stream_function_calling": "not_supported",
            "vision_support": "support"
        }
        cfg_json = json.dumps(cfg).replace("'", "''")
        sql = f"""
        INSERT INTO provider_model_credentials (id, tenant_id, provider_name, model_name, model_type, credential_name, encrypted_config, created_at, updated_at)
        VALUES ('{cred_id}', '{TENANT_ID}', '{PROVIDER_NAME}', 'claude-opus-4-6', 'llm', 'Cockpit Claude Opus 4.6', '{cfg_json}', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        
        INSERT INTO provider_models (id, tenant_id, provider_name, model_name, model_type, is_valid, credential_id, created_at, updated_at)
        VALUES ('{model_id}', '{TENANT_ID}', '{PROVIDER_NAME}', 'claude-opus-4-6', 'llm', true, '{cred_id}', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        """
        run_psql(sql)
        print("✅ Registered claude-opus-4-6 into Dify")

def switch_apps_to_claude():
    # 更新 app_model_configs 为 claude-sonnet-4-6
    sql = """
    UPDATE app_model_configs 
    SET model = '{"provider": "langgenius/openai_api_compatible/openai_api_compatible", "name": "claude-sonnet-4-6", "mode": "chat", "completion_params": {"temperature": 0.3, "max_tokens": 4096, "stop": []}}'
    WHERE app_id IN ('c271447f-5a9b-48d1-a403-af7b0306c7ee', 'a7546eca-608d-4dc3-b09e-07e0660202e3');
    """
    out, err = run_psql(sql)
    print("Switch apps result:", out.strip(), err.strip())

if __name__ == "__main__":
    ensure_claude_models()
    switch_apps_to_claude()
