# -*- coding: utf-8 -*-
"""
Script to register all tools (including take_screenshot and launch_software)
into Dify's godot_workspace_tools API tool provider and enable them in the RTS agent app.
"""
import subprocess

SCRIPT_IN_CONTAINER = '''
import json
import urllib.request
from app import create_app
from extensions.ext_database import db
from services.tools.api_tools_manage_service import ApiToolManageService
from core.tools.entities.tool_entities import ApiProviderSchemaType
from models.tools import ApiToolProvider
from models.model import AppModelConfig

# 1. Fetch latest schema from bridge
url = "http://host.docker.internal:8000/api/tools/openapi.json"
print(f"Fetching OpenAPI spec from {url}...")
with urllib.request.urlopen(url) as resp:
    updated_schema_str = resp.read().decode("utf-8")

_, app = create_app()
with app.app_context():
    provider = db.session.query(ApiToolProvider).filter_by(name="godot_workspace_tools").first()
    if not provider:
        print("Error: provider godot_workspace_tools not found!")
        exit(1)

    user_id = str(provider.user_id)
    tenant_id = str(provider.tenant_id)

    print("Calling ApiToolManageService.update_api_tool_provider...")
    res = ApiToolManageService.update_api_tool_provider(
        user_id=user_id,
        tenant_id=tenant_id,
        provider_name="godot_workspace_tools",
        original_provider="godot_workspace_tools",
        icon={"background": "#2E90FA", "content": "🛠️"},
        credentials={"auth_type": "none"},
        _schema_type=ApiProviderSchemaType.OPENAPI,
        schema=updated_schema_str,
        privacy_policy=None,
        custom_disclaimer="Local Godot 4 RTS development tools",
        labels=[],
    )
    print("Update result:", res)

    new_tools_to_enable = [
        "read_file", "write_file", "list_directory", "grep_search", 
        "run_build", "run_command", "take_screenshot", "launch_software", 
        "operate_vscode", "inspect_3d_asset", "fix_normal_map"
    ]
    configs = db.session.query(AppModelConfig).filter_by(app_id="c271447f-5a9b-48d1-a403-af7b0306c7ee").all()
    print(f"Found {len(configs)} configs for app c271447f-5a9b-48d1-a403-af7b0306c7ee")

    for cfg in configs:
        if not cfg.agent_mode:
            continue
        agent_mode = json.loads(cfg.agent_mode) if isinstance(cfg.agent_mode, str) else cfg.agent_mode
        tools = agent_mode.get("tools", [])
        tool_names = [t.get("tool_name") for t in tools]

        for tname in new_tools_to_enable:
            if tname not in tool_names:
                tools.append({
                    "provider_type": "api",
                    "provider_id": str(provider.id),
                    "tool_name": tname,
                    "tool_parameters": {},
                    "enabled": True
                })
                print(f"Added {tname} to config {cfg.id}")

        agent_mode["tools"] = tools
        cfg.agent_mode = json.dumps(agent_mode)

    db.session.commit()
    print("Successfully committed updated tools to all configs!")
'''

def main():
    print("Updating Dify tools inside docker-api-1...")
    cmd = ["docker", "exec", "-i", "docker-api-1", "python", "-c", SCRIPT_IN_CONTAINER]
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    stdout, stderr = proc.communicate()
    print("STDOUT:", stdout.decode("utf-8", errors="replace"))
    if stderr:
        print("STDERR:", stderr.decode("utf-8", errors="replace"))

if __name__ == "__main__":
    main()
