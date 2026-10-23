# -*- coding: utf-8 -*-
import os
import shutil

startup_dir = os.path.expandvars(r"%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup")
src = os.path.join(os.path.dirname(__file__), "start_rocketchat_services.vbs")
dst = os.path.join(startup_dir, "StartRocketChatServices.vbs")

shutil.copy2(src, dst)
print("Startup file exists:", os.path.exists(dst))
print("Destination path:", dst)
