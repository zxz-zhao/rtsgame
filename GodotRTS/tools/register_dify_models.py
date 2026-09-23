# -*- coding: utf-8 -*-
"""
向 Dify 数据库注册 Sub2API (大王AI) 与 ChatOS 新模型配置
"""
import uuid
import subprocess
import json

TENANT_ID = "0073f7b6-19a1-4e26-b8ec-f00c498c4d4c"
PROVIDER_NAME = "langgenius/openai_api_compatible/openai_api_compatible"

# 复用已加密的 API Key 配置模板
ENCRYPTED_API_KEY = "SFlCUklEOljC0aEaYXQs9RdF4xKW9qeLusbSG1XPW25/HFQxXveCd29diyHv8IVlrvfA9JBfB3N+IJfBS3KiXb52DOkFYTmX+LG3+CXmDcahc2urZnJKSW8EoHqNY7s/xpGROqbneBuM5Mc6h0URjNIub8yBXt0NUpStGPhwlFGJy3V55dAdPnjE3a39AU+DA34klY+jLHhQ5hnYveM/TCIHVbJdXEpiCR4TF8Q81tAnm3p3Llp2VQSj9Z8KQl4SpJ7wcVFHSk/lE+/OF/clWNGisset0f5M/8suVkPHfjDiyVEvKuWBjjkBqTmibsS+fJX8oty98w3CKT5Q/CXTjtkwgpuzA2PC5pQq7jbWkxElPXzG7zVyRkcnm8RmrzEctKm1P/LlTNVtTSb+TQbDQ4DL05s="

NEW_MODELS = [
    {
        "cred_name": "Sub2API GPT-6 Astra",
        "model_name": "gpt-6-astra",
        "context_size": "131072",
        "max_tokens": "16384"
    },
    {
        "cred_name": "Sub2API GPT-5.5",
        "model_name": "gpt-5.5",
        "context_size": "131072",
        "max_tokens": "16384"
    },
    {
        "cred_name": "ChatOS GPT-4o (Direct)",
        "model_name": "gpt-4o",
        "context_size": "65536",
        "max_tokens": "8192"
    },
    {
        "cred_name": "ChatOS Claude 3.5 Sonnet (Direct)",
        "model_name": "claude-3-5-sonnet",
        "context_size": "65536",
        "max_tokens": "8192"
    },
    {
        "cred_name": "ChatOS DeepSeek Chat (Direct)",
        "model_name": "deepseek-chat",
        "context_size": "65536",
        "max_tokens": "8192"
    }
]

def run_psql(sql: str):
    cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    stdout, stderr = proc.communicate(input=sql.encode("utf-8"))
    return stdout.decode("utf-8", errors="replace"), stderr.decode("utf-8", errors="replace")

def register_models():
    for item in NEW_MODELS:
        m_name = item["model_name"]
        c_name = item["cred_name"]
        
        # 检查是否已存在
        out, _ = run_psql(f"SELECT id FROM provider_models WHERE model_name = '{m_name}' AND tenant_id = '{TENANT_ID}';")
        if m_name in out:
            print(f"[SKIP] Model already registered: {m_name}")
            continue
            
        cred_id = str(uuid.uuid4())
        model_id = str(uuid.uuid4())
        
        cfg = {
            "api_key": ENCRYPTED_API_KEY,
            "endpoint_url": "http://host.docker.internal:8000/v1",
            "mode": "chat",
            "context_size": item["context_size"],
            "max_tokens_to_sample": item["max_tokens"],
            "agent_thought_support": "supported",
            "web_search_support": "not_supported",
            "compatibility_mode": "strict",
            "api_type": "chat_completions",
            "token_param_name": "auto",
            "user_identity_support": "support",
            "stream_include_usage": "enabled",
            "function_calling_type": "no_call",
            "stream_function_calling": "not_supported",
            "vision_support": "support",
            "video_support": "no_support",
            "audio_support": "no_support",
            "document_support": "no_support",
            "structured_output_support": "not_supported",
            "stream_mode_auth": "not_use",
            "stream_mode_delimiter": "\n\n"
        }
        cfg_json = json.dumps(cfg).replace("'", "''")
        
        sql = f"""
        INSERT INTO provider_model_credentials (id, tenant_id, provider_name, model_name, model_type, credential_name, encrypted_config, created_at, updated_at)
        VALUES ('{cred_id}', '{TENANT_ID}', '{PROVIDER_NAME}', '{m_name}', 'llm', '{c_name}', '{cfg_json}', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        
        INSERT INTO provider_models (id, tenant_id, provider_name, model_name, model_type, is_valid, credential_id, created_at, updated_at)
        VALUES ('{model_id}', '{TENANT_ID}', '{PROVIDER_NAME}', '{m_name}', 'llm', true, '{cred_id}', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        """
        out, err = run_psql(sql)
        if "INSERT 0 1" in out:
            print(f"[SUCCESS] Registered {m_name} ({c_name})")
        else:
            print(f"[ERROR] Failed {m_name}: {out} {err}")

if __name__ == "__main__":
    register_models()
