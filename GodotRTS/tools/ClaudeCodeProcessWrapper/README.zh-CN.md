# ClaudeCodeProcessWrapper

这个工具现在主要是一个 Claude Code 设置编辑器。

它会直接修改 Claude Code 真正在读的用户配置文件：

`%USERPROFILE%\.claude\settings.json`

也就是当前机器上的：

`C:\Users\Administrator\.claude\settings.json`

## 它会改哪些配置

界面会直接编辑这些 Claude Code 设置项：

- `env.ANTHROPIC_BASE_URL`
- `env.ANTHROPIC_API_KEY`
- `env.ANTHROPIC_MODEL`
- `env.ANTHROPIC_REASONING_MODEL`
- `env.ANTHROPIC_DEFAULT_HAIKU_MODEL`
- `env.ANTHROPIC_DEFAULT_SONNET_MODEL`
- `env.ANTHROPIC_DEFAULT_OPUS_MODEL`
- 顶层 `model`

另外，`env` 下面其他未知字段也可以在 “Other env” 里继续编辑，不会被丢掉。

## 用法

1. 双击 `OpenClaudeConfig.cmd`
2. 或者直接运行 `ClaudeCodeProcessWrapper.exe --ui`
3. 修改 Claude Code 的 URL、Key、模型
4. 点击保存
5. 回到 VS Code，重新打开 Claude Code 面板，或者执行 `Reload Window`

保存前会自动备份旧文件到：

`%USERPROFILE%\.claude\backups\`

## 说明

- 这个工具改的是 Claude Code 自己的固定配置文件，不是单独一份 wrapper 私有配置
- 如果 VS Code 里的 Claude Code 面板已经打开，通常需要重开面板或 Reload Window 才会重新读取
- API Key 会明文保存在 `settings.json`，请只在你自己的机器上使用
