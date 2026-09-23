---
name: desktop-automation
description: >-
  Use this skill when automating desktop tasks, controlling VS Code (file jumping, diffing, workspace loading),
  capturing in-game GPU Vulkan screenshots, launching Godot runtime/editor, and executing compilation and shell commands.
---

# Desktop, VS Code & GPU In-Game Screenshot Automation Skill

This skill guides the agent through desktop environment control, editor automation, shell command execution, and GPU-accelerated in-game visual capture for Godot 4 RTS development.

---

## 1. GPU In-Game Screenshot & In-Chat Visual Display Loop

When a user requests to view the game in real-time ("看一眼画面", "截图", "实机战斗效果", "看一下效果"):

### Direct Vulkan Preview Capture Pipeline
The Godot engine can capture high-fidelity 3D frames directly through the physical GPU (Vulkan / Radeon RX 580) without requiring an interactive window:

```bash
# Godot 4 mono capture preview
"C:\Users\Administrator\Downloads\Godot_v4.3-stable_mono_win64\Godot_v4.3-stable_mono_win64\Godot_v4.3-stable_mono_win64.exe" \
  --path "E:\code\c++\UnityRTS\GodotRTS" \
  scenes/battle/BattleArena.tscn \
  --capture-preview \
  --capture-frames 20 \
  --capture-path "e:\code\c++\UnityRTS\GodotRTS\scratch\preview.png"
```

### Static Host & Reverse Proxy
1. Image is saved to `scratch/preview_<timestamp>.png`.
2. Static server on port `8000` serves `/screenshots/<filename>`.
3. Dify Nginx container (`9564`) reverse proxies `/screenshots/` to `http://host.docker.internal:8000/screenshots/`.
4. Output Markdown image syntax directly in chat:
   ```markdown
   ![Godot RTS 游戏实机截图](http://localhost:9564/screenshots/preview_12345.png)
   ```

---

## 2. VS Code IDE Automation (`operate_vscode`)

Control the local developer's VS Code instance via CLI protocol:

| Action | Parameters | Description |
| :--- | :--- | :--- |
| `open_file` | `file="scripts/battle/RtsCamera.cs"`, `line=50` | Opens file and navigates to the exact line number. |
| `open_workspace` | `workspace="."` | Loads the Godot RTS workspace in VS Code. |
| `diff` | `file1="file_a.cs"`, `file2="file_b.cs"` | Opens side-by-side graphical diff in VS Code. |
| `list_extensions` | None | Lists all active editor extensions. |

---

## 3. External Software Management (`launch_software`)

Trigger background desktop applications cleanly without blocking the shell:

```python
# Launch game arena via detached process
subprocess.Popen(
    [GODOT_EXE, "--path", WORKSPACE_DIR, "scenes/battle/BattleArena.tscn"],
    creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
)
```

Supported software targets:
- `godot_run`: Launches real-time combat arena scene.
- `godot_editor`: Opens Godot 4 project editor.
- `vscode`: Opens VS Code workspace.
- `explorer`: Opens Windows File Explorer at the project root.

---

## 4. Build & Command Execution Closed-Loop

Always verify C# code edits immediately using:
```bash
dotnet build GodotRTS.csproj
```
Ensure `0 Errors` and `0 Warnings` before presenting solutions to the user.
