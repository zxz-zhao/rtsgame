import sys

import bpy


def main() -> None:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    if len(argv) != 2:
        raise SystemExit("Usage: blender --background --python _blender_fbx_to_gltf_worker.py -- input.fbx output.glb")
    src, dst = argv
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(filepath=src)
    bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB")


if __name__ == "__main__":
    main()
