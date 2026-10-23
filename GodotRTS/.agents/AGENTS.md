# Project Customization Rules (Godot 4 C# RTS)

## 1. Full Autonomous Execution & Zero-Prompt Rules (全受控与免审批自主闭环规范)
- **Full Autonomous Control (完全受控自主闭环)**: The AI assistant operates in full autonomous mode. Do NOT pause for trivial approvals or create blocking implementation plans unless major architectural ambiguities exist. Proactively complete coding, building, testing, and deployment end-to-end.
- **Zero-Prompt Command Execution (免弹窗指令执行)**: Execute commands directly. Prefer standard sandbox execution (`BypassSandbox: false`) for workspace-internal operations (such as `dotnet build`, internal python scripts, and git operations) to allow automatic background execution without prompting the user. Only request bypass when external network/system privileges are genuinely required.
- **Directory Write Access**: Directly write and modify files in workspace paths (`scripts/`, `scenes/`, `assets/`, `scratch/`, `tools/`) without requesting interactive confirmation.

## 2. Development & Build Workflow (开发与构建规范)
- **Godot C# Compilation**: Always verify code edits by compiling via `dotnet build GodotRTS.csproj` before reporting success.
- **Headless & Preview Verification**: When capturing 3D model previews or UI layouts, run the Godot 4 mono executable with non-headless `--capture-preview` parameters to ensure visual correctness.

## 3. Communication Style (交互风格)
- Keep status updates concise and provide github-style clickable links for all modified files and key code symbols.

## 4. Minimap & Camera Coordinate Mapping Rules (小地图与镜头坐标关联规范)
- **Camera 180° Yaw Inversion**: The 3D RTS camera (`Camera3D`) has a 180° Y-yaw rotation (`Basis.X = (-1, 0, 0)`), making Screen Left = World `+X`, Screen Right = World `-X`, Screen Top = World `+Z`, Screen Bottom = World `-Z`. `BattleMinimap.cs` MUST keep the inverted coordinate mapping (`0.5f - world.X` and `0.5f - world.Z`) so minimap drag directions strictly match 3D screen view directions.
- **Full Viewport Boundary Clamping**: In `RtsCamera.cs`, `ClampVisibleAreaToMap()` MUST clamp all 4 ground polygon corners of the viewport (`TryGetViewportGroundPolygon`), NOT just the center point `GetGroundCenter()`. This guarantees the tilted camera frustum never looks past terrain bounds into the skybox void.

## 5. Test Video Recording Duration (测试视频录制规范)
- **Minimum 20 Seconds Video Duration (视频时长必须在 20s 以上)**: All automated test gameplay video recordings MUST be at least 20 seconds long (`frames >= 1200` @ 60 FPS, standard default 1860 frames / 31 seconds) so full combat engagement cycles, air defense interception arcs, and tactical path progressions are completely visible and verifiable.

