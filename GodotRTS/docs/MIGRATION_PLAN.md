# Unity/Tuanjie to Godot C# Migration Plan

This folder is the Godot replacement client for the RTS. The goal is a fully free client stack while preserving the Unity game design, server API, reusable art assets, and as much C# gameplay logic as practical.

## Stack

- Engine: Godot 4.6.3 .NET, MIT licensed.
- Client scripting: C#.
- Target SDK: `Godot.NET.Sdk/4.6.3`.
- Server: keep the existing Node.js service during migration.
- Runtime model format: GLB first, OBJ for static map props.
- FBX conversion: Blender, currently found at `E:\Blender 5.1\blender.exe`.

## Current State

The Godot client now contains a playable vertical slice:

- Login/register/guest flow into lobby.
- Lobby with promoted Unity UI art, mode entries, and map selection.
- Battle scene with generated themed maps.
- Real Panzer IV GLB model.
- Converted light tank, heavy tank, artillery, and scout helicopter GLB smoke tests.
- Scout helicopter scale, centering, health bar, and selection feedback calibrated.
- Imported Kenney character GLB wired into infantry, artillery infantry, and flamethrower infantry visuals.
- Imported Kenney SpaceKit FBX scenes wired into fighter and bomber visuals as free placeholder aircraft models.
- OBJ map props for rocks, trees, walls, and environment details.
- Imported military OBJ ship models for naval units, plus OBJ structure/cannon props layered into several building visuals.
- Unit selection, box selection, move, attack, attack-move, projectile feedback, health bars, and selection rings.
- Idle guard acquisition and first-pass target-priority scoring for anti-air, flamethrowers, artillery, bombers, and naval units.
- Real fog-of-war hiding/reveal for enemy units and buildings, plus selection/attack/minimap filtering outside player vision.
- Air movement keeps units on the flight layer, while naval spawn, move, attack-move, and rally targets snap to authored water strips.
- Resources, population, production queues, building placement, and build menu.
- Building placement rejects water; naval yards require nearby water.
- Tank factory roster now produces land armor/artillery, with naval production isolated to naval yard.
- First-pass battle research is playable: each relevant production building exposes one research item, purchases spend gold, update existing and future player units, and feed fog-of-war vision bonuses where applicable.
- First-pass building level upgrades are playable up to Lv.3, with HP, production speed, income, population, power, and turret stat changes applied at runtime.
- Multi-unit movement now uses centered formation offsets, and selected units expose a Stop command in the battle HUD.
- Patrol and guard commands are now wired: Alt+right-click patrols, Ctrl+right-click on an allied unit guards/follows, and the HUD exposes these command hints.
- Building construction progress with delayed activation, HUD countdown, and construction health feedback.
- Construction cancellation with partial gold refund.
- Power supply/usage rules with low-power pause for production, income, and defense.
- Production building rally points with visible rally flags and automatic unit move orders.
- Player building repair with gold cost, HP restoration, and live HUD refresh.
- Player unit repair with gold cost, HP restoration, command-panel button, and live HUD refresh.
- Main base, barracks, armor factory, tank factory, airfield, air factory, naval yard, turret, power plant, and gold mine definitions.
- Land, infantry, air, and naval unit catalog using the first pass of Unity-side costs, HP, ranges, cooldowns, and population.
- AI income, base defense, attack waves, and win/loss panel.
- Tactical minimap with terrain, units, buildings, camera rectangle, and fog-style overlay.

This is still not the full game. It is a running migration slice that proves the core route.

## Module Mapping

| Unity module | Godot C# destination |
| --- | --- |
| `NetworkClient.cs` | `scripts/network/NetClient.cs` |
| `GameNetworkSync.cs` | `scripts/network/GameRelay.cs` |
| `RTSUnit.cs` | `scripts/battle/RtsUnit.cs` |
| `RTSBuilding.cs` / `UpgradeableBuilding.cs` | `scripts/battle/RtsBuilding.cs`, `BattleBuildingCatalog.cs` |
| Unity unit subclasses | `BattleUnitCatalog.cs`, `BattleGameManager.cs` visual builders |
| `RTSPlayerController.cs` | `scripts/battle/PlayerController.cs` |
| `RTSCamera.cs` | `scripts/battle/RtsCamera.cs` |
| `GameManager.cs` / `GameInitializer.cs` | `scripts/battle/BattleGameManager.cs`, `BattleBootstrap.cs` |
| `AIController.cs` | `scripts/battle/BattleAiController.cs` |
| `RTSHUD.cs` / minimap scripts | `scripts/ui/BattleHud.cs`, `BattleMinimap.cs`, `SelectionBoxOverlay.cs` |
| Login/lobby UI | `scenes/login`, `scenes/lobby`, `scripts/ui/LoginScreen.cs`, `LobbyScreen.cs` |
| `BattleMapCatalog.cs` | `scripts/map/MapCatalog.cs` |
| `RuntimeBattleMapBuilder.cs` | `scripts/map/BattleMapRenderer.cs` |

## Asset Pipeline

Source mirror:

```text
GodotRTS/assets_migrated/
```

Runtime promoted assets:

```text
GodotRTS/assets/ui/
GodotRTS/assets/units/
GodotRTS/assets/props/
```

Useful commands:

```powershell
cd E:\code\c++\UnityRTS
dotnet build GodotRTS\GodotRTS.csproj
& "G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe" --headless --path "E:\code\c++\UnityRTS\GodotRTS" --import
python GodotRTS\tools\convert_fbx_to_gltf_blender.py --blender "E:\Blender 5.1\blender.exe" --unity-root . --output GodotRTS\assets\units\converted --limit 1
```

## Next Phases

1. Tune scale/material/orientation for the converted heavy tank and artillery.
2. Convert and validate remaining priority FBX models: infantry animations, final aircraft, naval units, and buildings.
3. Replace procedural stand-ins with converted model scenes.
4. Port Unity-authored building upgrade requirements, deeper tech-tree prerequisites, special abilities, attack-ground, collision-aware formations, and damage/death VFX.
5. Polish fog-of-war memory/shroud, minimap click behavior, naval water pathfinding, and Android layout pass.
6. Complete multiplayer command sync and reconciliation against the existing Node server.
7. Run side-by-side Unity/Godot screenshot parity checks for login, lobby, battle start, mid-combat, victory, and defeat.
