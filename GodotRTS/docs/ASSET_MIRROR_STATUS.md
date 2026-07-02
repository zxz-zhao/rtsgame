# Asset Mirror Status

The Unity source art assets have been mirrored into Godot-side storage:

`assets/unity_migrated/Assets/`

The older staging folder `assets_migrated/` is still present as a historical artifact from the first mirror pass, but the active Godot-imported mirror now lives under `assets/unity_migrated/Assets/`.

The mirror is generated from the Unity `Assets/` tree. Runtime-promoted resources continue to live under the normal Godot folders such as `assets/ui/`, `assets/units/`, and `assets/props/`.

## Verified Counts

- Total mirrored files: 3469.
- Total bytes: 533134624.
- Missing files compared to manifest: 0.
- Size mismatches compared to manifest: 0.

## Breakdown

- Textures ready for Godot: 670 files.
- FBX source models preserved: 1684 files.
- OBJ/GLB/GLTF ready models: 558 files.
- `.mtl` sidecar materials: 557 files.

## Important

This means the model and texture source files are present on the Godot side and have been imported by Godot. It does not mean the final Godot scenes, materials, animations, lighting, UI, and gameplay are visually identical yet.

One real model has been promoted into runtime resources for visual validation:

`assets/units/panzer_iv/pzIV.glb`

It is visible in:

`../PreviewOutput/godot_panzer_preview.png`

To reach visual parity, the next work is:

1. Convert FBX models to GLB.
2. Build Godot scenes for every gameplay prefab.
3. Recreate materials and shader settings.
4. Recreate Unity scene composition in Godot.
5. Capture Unity reference screenshots and compare Godot output against them.
