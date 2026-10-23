import os
import sys
import subprocess
from pathlib import Path

DEST_DIR = Path("H:/AnimeStudio")
print(f"Deploying AnimeStudio 3D to {DEST_DIR}...")

scripts = [
    "tools/make_anime_core.py",
    "tools/make_anime_composer.py",
    "tools/make_anime_ui.py",
    "tools/make_anime_server.py",
    "tools/make_anime_dify.py"
]

python_exe = r"E:\python13\python.exe"
for s in scripts:
    print(f"Running {s}...")
    res = subprocess.run([python_exe, s], cwd=Path(__file__).parent.parent)
    if res.returncode != 0:
        print(f"Error running {s}")
        sys.exit(1)

print("Deploying run.bat...")
bat_src = Path(__file__).parent / "run_anime_studio.bat"
if bat_src.exists():
    import shutil
    shutil.copy(bat_src, DEST_DIR / "run.bat")

print("All components deployed to H:/AnimeStudio successfully!")
