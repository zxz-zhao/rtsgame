patch_content = """# Your patch layer for this dsh profile, applied after every bundle layer:
# a top-level YAML array of loader patch entries (id-targeted config
# overrides, disables, and insert lists; `!!js` expressions allowed).
- id: agent-default-model
  name: "@deepseek-ai/dsh-agent-default-model"
  config:
    provider: deepseek-official
    model: deepseek-flash
    reasoningEffort: high
- id: llm-deepseek
  name: "@deepseek-ai/dsh-llm-deepseek-api-key"
  config:
    baseURL: "http://127.0.0.1:3000"
    apiKeyEnv: "DEEPSEEK_API_KEY"
- id: ui-settings-account
  name: "@deepseek-ai/dsh-client-ui-settings-account"
  config:
    version: 1
    step: done
    purpose: development
    process: standard
    completion: completed
    usage: detailed
    developerTools: true
- id: ui-chat
  name: "@deepseek-ai/dsh-client-ui-chat"
  config:
    transcriptView: standard
    performanceUsage: detailed
- id: ui-settings
  name: "@deepseek-ai/dsh-client-ui-settings"
  config:
    enabled: true
"""

targets = [
    r"C:\Users\Administrator\.dsh\profiles\desktop\cordis.patch.yml",
    r"C:\Users\Administrator\.dsh-acc2\profiles\desktop\cordis.patch.yml"
]

for t in targets:
    with open(t, "w", encoding="utf-8") as f:
        f.write(patch_content)
    print(f"Updated {t}")
