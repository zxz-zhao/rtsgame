# FreePixel Military Models

Source: https://freepixel.art/3d-assets

Downloaded assets planned for this project:
- `tank.glb` - Tank
- `jet_fighter.glb` - Jet Fighter
- `pirate_ship.glb` - Pirate Ship
- `rowboat.glb` - Rowboat

License note from the source page: FreePixel states that the 3D assets are free for commercial and personal projects and require no attribution.

The original `.glb` files are kept under `GLB/`. Unity 2022/Tuanjie does not import `.glb` as model assets in this project by default, so converted `.obj`/`.mtl` versions are kept under `OBJ/` for prefab integration.

The duplicated `.fpglb` files are the same GLB payloads with a project-specific extension. They are imported by `SimpleFreePixelGlbImporter` so only these downloaded models use the lightweight importer.
