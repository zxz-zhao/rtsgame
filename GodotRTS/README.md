# Xinghuo RTS Godot Client

This is the Godot migration target for the existing Unity/Tuanjie RTS.

The intent is a fully free/open client stack:

- Godot 4.x engine.
- C# client gameplay on the Godot .NET editor.
- Existing Node.js server retained during migration.
- glTF/GLB, OBJ, PNG, and JPG asset pipeline.

Open this folder in the Godot 4.x .NET editor and run `scenes/battle/BattlePrototype.tscn`.

For the 3D map authoring path, open `scenes/map/MapStudio.tscn` and read `docs/MAP_STUDIO_SETUP.zh-CN.md`.

See `docs/MIGRATION_PLAN.md` for the technical migration map, or `docs/FREE_MIGRATION.zh-CN.md` for the Chinese free-stack route.

The Unity source art mirror status is tracked in `docs/ASSET_MIRROR_STATUS.md`. Visual/gameplay parity is tracked in `docs/PARITY_CHECKLIST.md`.
