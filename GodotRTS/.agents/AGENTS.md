# Project Customization Rules (Godot 4 C# RTS)

## 1. Zero-Prompt Permission Pre-authorization Rules (权限预申请免弹窗规范)
- **Command Pre-Authorization (强制预授权)**: In ANY task requiring command execution, build, test, script running, or terminal operations, the AI assistant MUST ALWAYS invoke `ask_permission` BEFORE calling `run_command` for all relevant command prefixes (`python`, `py`, `dotnet`, `powershell`, `cmd`, `git`, `node`, `npm`, `npx`, `pytest`). This pre-authorizes scoped command execution and prevents user approval dialogs ("Allow running this command?") from popping up.
- **Directory Write Access**: Prioritize scoped `write_file` pre-authorization for workspace source paths (such as `scripts/`, `scenes/`, `assets/`, `scratch/`) during file operations.

## 2. Development & Build Workflow (开发与构建规范)
- **Godot C# Compilation**: Always verify code edits by compiling via `dotnet build GodotRTS.csproj` before reporting success.
- **Headless & Preview Verification**: When capturing 3D model previews or UI layouts, run the Godot 4 mono executable with non-headless `--capture-preview` parameters to ensure visual correctness.

## 3. Communication Style (交互风格)
- Keep status updates concise and provide github-style clickable links for all modified files and key code symbols.

## 4. Minimap & Camera Coordinate Mapping Rules (小地图与镜头坐标关联规范)
- **Camera 180° Yaw Inversion**: The 3D RTS camera (`Camera3D`) has a 180° Y-yaw rotation (`Basis.X = (-1, 0, 0)`), making Screen Left = World `+X`, Screen Right = World `-X`, Screen Top = World `+Z`, Screen Bottom = World `-Z`. `BattleMinimap.cs` MUST keep the inverted coordinate mapping (`0.5f - world.X` and `0.5f - world.Z`) so minimap drag directions strictly match 3D screen view directions.
- **Full Viewport Boundary Clamping**: In `RtsCamera.cs`, `ClampVisibleAreaToMap()` MUST clamp all 4 ground polygon corners of the viewport (`TryGetViewportGroundPolygon`), NOT just the center point `GetGroundCenter()`. This guarantees the tilted camera frustum never looks past terrain bounds into the skybox void.
