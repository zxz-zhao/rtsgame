# Project Customization Rules (Godot 4 C# RTS)

## 1. Permission Pre-authorization Rules (权限预申请规范)
- **Command Permissions**: In any task requiring build, preview, or terminal operations, prioritize invoking `ask_permission` for common command prefixes (`dotnet`, `powershell`, `git`) to grant scoped access and minimize user approval prompts.
- **Directory Write Access**: Prioritize scoped `write_file` pre-authorization for workspace source paths (such as `scripts/`, `scenes/`, `assets/`) during multi-file edits.

## 2. Development & Build Workflow (开发与构建规范)
- **Godot C# Compilation**: Always verify code edits by compiling via `dotnet build GodotRTS.csproj` before reporting success.
- **Headless & Preview Verification**: When capturing 3D model previews or UI layouts, run the Godot 4 mono executable with non-headless `--capture-preview` parameters to ensure visual correctness.

## 3. Communication Style (交互风格)
- Keep status updates concise and provide github-style clickable links for all modified files and key code symbols.
