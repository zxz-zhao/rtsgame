import argparse
import pathlib
import subprocess
import sys


def main() -> int:
    parser = argparse.ArgumentParser(description="Batch convert Unity FBX assets to glTF using Blender.")
    parser.add_argument("--blender", default="blender", help="Path to the Blender executable.")
    parser.add_argument("--unity-root", default="..", help="Path to the Unity project root.")
    parser.add_argument("--output", default="assets_migrated", help="Godot-side output directory.")
    parser.add_argument("--limit", type=int, default=0, help="Optional max file count for smoke tests.")
    args = parser.parse_args()

    unity_root = pathlib.Path(args.unity_root).resolve()
    asset_root = unity_root / "Assets"
    output_root = pathlib.Path(args.output).resolve()
    fbx_files = list(asset_root.rglob("*.fbx"))
    if args.limit > 0:
        fbx_files = fbx_files[: args.limit]

    if not fbx_files:
        print("No FBX files found.")
        return 0

    script = pathlib.Path(__file__).with_name("_blender_fbx_to_gltf_worker.py")
    if not script.exists():
        print(f"Missing worker script: {script}", file=sys.stderr)
        return 2

    for index, src in enumerate(fbx_files, start=1):
        relative = src.relative_to(unity_root)
        dst = output_root / relative.with_suffix(".glb")
        dst.parent.mkdir(parents=True, exist_ok=True)
        print(f"[{index}/{len(fbx_files)}] {relative} -> {dst.relative_to(output_root)}")
        subprocess.run(
            [
                args.blender,
                "--background",
                "--python",
                str(script),
                "--",
                str(src),
                str(dst),
            ],
            check=True,
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
