@echo off
chcp 65001 >nul
echo 正在使用 PyInstaller 编译为单文件加壳 EXE...
"E:\python13\python.exe" -m PyInstaller --onefile --noconsole --name "ProtectedApp" --add-data "payload.enc;." run_protected.py
pause
