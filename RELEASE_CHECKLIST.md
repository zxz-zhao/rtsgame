# Release Checklist

Use this checklist before producing a build intended for external testers or stores.

## Source Hygiene

- [ ] Project is under version control.
- [ ] `git status --short` contains only intentional source changes.
- [ ] `.\Tools\Preflight.ps1` completes successfully.
- [ ] No `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, APKs, screenshots, or local logs are tracked.
- [ ] README and setup docs open as readable UTF-8.
- [ ] Server secrets are not committed.

## Unity Build

- [ ] Opened with Tuanjie/Unity `2022.3.62f3c1`.
- [ ] Android package id, version name, and version code are correct.
- [ ] Release build uses IL2CPP and ARM64.
- [ ] `AndroidBuildSetup.BuildAndroid` succeeds in batchmode.
- [ ] `.\Tools\CheckBuildLog.ps1` reports no build errors and no unexpected warnings.
- [ ] Development Build, Script Debugging, and Autoconnect Profiler are disabled.
- [ ] Scenes in build settings are Login, Lobby, and Game in the intended order.
- [ ] APK/AAB installs cleanly on a fresh device/emulator.
- [ ] `.\Tools\InstallAndLaunchAndroid.ps1 -Screenshot` launches the APK and captures the first screen.
- [ ] `.\Tools\CaptureAndroidLog.ps1` shows no Unity crashes, repeated network errors, or NavMesh warnings.

## Gameplay Smoke Test

- [ ] App launches to login without missing references or console errors.
- [ ] Guest/login flow reaches lobby.
- [ ] Matchmaking opens and enters battle.
- [ ] Camera pan/zoom works on touch.
- [ ] Unit selection, movement, production, building placement, and combat work.
- [ ] AI spawns and attacks.
- [ ] HUD, minimap, health bars, damage numbers, and production bars are visible.
- [ ] Back navigation and reconnect/error states are acceptable.

## Server

- [ ] Server starts from a clean `npm install`.
- [ ] Configuration comes from environment variables or `.env`, not hardcoded secrets.
- [ ] Database schema and migration path are documented.
- [ ] Account, room, match queue, invite, and friend data survive server restart.
- [ ] WebSocket/API errors are logged with enough context but without leaking secrets.

## Mobile Quality

- [ ] Cold start time is acceptable.
- [ ] Memory stays within target device budget after 10 minutes of play.
- [ ] Package size is within the target store/channel limit.
- [ ] No excessive runtime logging in release builds.
- [ ] Network loss and server unavailable states show a user-facing error.
