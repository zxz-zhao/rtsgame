# Xinghuo RTS

Unity/Tuanjie mobile RTS prototype targeting Android ARM64. The current build contains login, lobby, matchmaking entry points, RTS combat, production, AI, minimap, HUD, generated prefabs, and local server integration.

## Required Tools

- Tuanjie/Unity `2022.3.62f3c1`
- Android Build Support, Android SDK/NDK Tools, and OpenJDK
- Node.js LTS for the local server
- MuMu emulator or an Android device for smoke testing

## Project Layout

```text
Assets/
  Scripts/        Runtime gameplay, UI, login, lobby, network, AI, HUD, camera
  Editor/         Scene, prefab, asset, and Android build automation
  Scenes/         LoginScene, LobbyScene, GameScene
  Resources/      Runtime-loaded generated assets
  Plugins/Android Android manifest and mobile integration
Packages/         Unity package manifest and lock file
ProjectSettings/  Unity project settings
Server/           Node.js lobby/account/match server
Tools/            Local maintenance and deployment scripts
```

## First-Time Setup

1. Open this folder with Tuanjie/Unity `2022.3.62f3c1`.
2. Install Android Build Support if the editor reports missing modules.
3. Restore the server dependencies:

```powershell
cd Server
npm install
```

4. Start the local server when testing login/lobby flows:

```powershell
cd Server
npm start
```

For cloud deployment and server hot updates, see `Server/DEPLOY.md`.

5. In Unity, use the RTS editor menus to regenerate prefabs/scenes if needed:

```text
RTS -> Build All
RTS -> Configure Android Build Settings
```

6. Build the Android APK from Unity or the existing build scripts. The expected local output is `Build/Android/UnityRTS.apk`.

## Release Rules

- Do not commit Unity generated folders such as `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, or `UserSettings/`.
- Do not commit `Server/node_modules/`, `Server/.env`, runtime JSON data, APKs, screenshots, crash dumps, or local build logs.
- Keep source assets under `Assets/`; keep local references, screenshots, and generated verification images outside source control unless they are intentional game assets.
- Use UTF-8 for all text files. If a document opens as mojibake, fix the encoding before editing further.
- Treat `Assets/Resources` as a compatibility layer. New gameplay assets should prefer explicit references, data assets, or a planned Addressables/AssetBundle pipeline.

## Current Release Gaps

This project is playable/prototypable, but it is not yet a production-grade online RTS. Before publishing broadly, finish these tracks:

- Replace file-backed or local-only server state with a durable database-backed flow.
- Make authentication secrets and database credentials environment-based.
- Harden matchmaking and gameplay synchronization so the server owns authoritative match state.
- Reduce runtime `Resources.Load`, `FindObjectOfType`, and name-based `GameObject.Find` dependencies.
- Split very large editor builders into smaller, testable modules.
- Add repeatable smoke tests for login, lobby, match entry, battle scene load, unit production, combat, and Android build.
- Run mobile profiling for package size, memory, CPU, battery, and loading time.

## Useful Commands

```powershell
# List source files without generated Unity and Node output
rg --files -g "!Library/**" -g "!Build/**" -g "!Server/node_modules/**"

# Dry-run local cleanup candidates
.\Tools\CleanLocalArtifacts.ps1

# Run quick release preflight checks
.\Tools\Preflight.ps1

# Start the local server
cd Server
npm start

# Build a release-style Android APK from batchmode
.\Tools\BuildReleaseAndroid.ps1

# Summarize the latest Unity build log
.\Tools\CheckBuildLog.ps1

# Install and launch the APK on an attached device/emulator
.\Tools\InstallAndLaunchAndroid.ps1 -Screenshot

# Capture filtered device logs after launch
.\Tools\CaptureAndroidLog.ps1
```

## OpenClaw One-Click Setup

This repo also includes local OpenClaw setup entry points:

- Windows: `.\OpenClaw-OneClick-Install.cmd`
- macOS / Linux / WSL2: `./OpenClaw-OneClick-Install.sh`

See `OPENCLAW.md` for the Chinese feature checklist, model-provider notes, chat-channel notes, and safety reminders.

## Notes

The project currently contains many local artifacts from build and emulator testing. The new `.gitignore` prevents future churn, but existing files on disk are left in place until explicitly cleaned.
