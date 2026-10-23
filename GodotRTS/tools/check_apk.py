import zipfile

apk_path = "Build/Android/GodotRTS-debug.apk"
with zipfile.ZipFile(apk_path, "r") as z:
    names = z.namelist()
    res_entries = [n for n in names if n.startswith("res/")]
    print(f"Total entries in APK: {len(names)}")
    print("Found res/ entries:")
    for entry in sorted(res_entries):
        if "icon" in entry or "mipmap" in entry:
            print("  ", entry)
