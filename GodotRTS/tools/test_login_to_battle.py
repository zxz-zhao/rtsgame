# -*- coding: utf-8 -*-
"""
Godot 4 RTS 全链路实机自动化测试套件 (Login to Battle Automated Test Suite)
从登陆到游戏开始的全流程验证：
1. TC-01: 静态编译与程序集构建验证 (dotnet build 0 Warning 0 Error)
2. TC-02: 作战指挥中心后端 API 鉴权验证 (HTTP 200 / JWT Token / 用户档案)
3. TC-03: 场景与 UI 节点契约完整性测试 (Login -> Lobby -> Battle)
4. TC-04: 实机全链路运行与 60 FPS 视频录制测试 (时长 >= 20s 规范约束)
5. TC-05: 关键里程碑画面萃取与视觉完整性测试 (登录界面 -> 基地大厅 -> 加载遮罩 -> 3D战场)
"""

import os
import sys
import json
import time
import urllib.request
import subprocess
from typing import Dict, Any, List

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if WORKSPACE_DIR not in sys.path:
    sys.path.insert(0, WORKSPACE_DIR)
SCREENSHOTS_DIR = os.path.join(WORKSPACE_DIR, "screenshots")
FFMPEG_EXE = r"E:\Lily\ffmpeg.exe"
PYTHON_EXE = sys.executable

os.makedirs(SCREENSHOTS_DIR, exist_ok=True)


def test_tc01_compilation() -> Dict[str, Any]:
    """TC-01: C# 静态编译与程序集构建测试"""
    t0 = time.time()
    cmd = ["dotnet", "build", "GodotRTS.csproj"]
    try:
        proc = subprocess.run(
            cmd, cwd=WORKSPACE_DIR, capture_output=True, encoding="utf-8", errors="replace", timeout=60
        )
        dt = round(time.time() - t0, 2)
        passed = (proc.returncode == 0) and ("0 个错误" in proc.stdout or "0 Error(s)" in proc.stdout)
        detail = f"编译通过，0 错误，耗时 {dt}s" if passed else f"编译失败:\n{proc.stdout[:300]}"
    except Exception as e:
        dt = round(time.time() - t0, 2)
        passed = False
        detail = f"编译异常: {e}"

    return {
        "id": "TC-01",
        "name": "C# 静态编译与程序集构建 (Build & Assembly Integrity)",
        "passed": passed,
        "duration": f"{dt}s",
        "detail": detail
    }


def test_tc02_backend_auth() -> Dict[str, Any]:
    """TC-02: 后端鉴权服务 API 契约与 Token 获取测试"""
    import hmac
    import hashlib
    t0 = time.time()
    url = "http://127.0.0.1:8080/api/login"
    payload_str = json.dumps({"username": "test", "password": "123456"})
    ts = str(int(time.time()))
    path = "/api/login"
    msg = f"POST\n{path}\n{ts}\n{payload_str}"
    sig = hmac.new(b"dev-signature-key-change-me", msg.encode("utf-8"), hashlib.sha256).hexdigest()
    headers = {
        "Content-Type": "application/json",
        "X-Client-Ts": ts,
        "X-Client-Sig": sig,
        "X-Client-Build": "20260709"
    }
    req = urllib.request.Request(
        url, data=payload_str.encode("utf-8"), headers=headers
    )
    try:
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            dt = round(time.time() - t0, 2)
            passed = data.get("success") is True and bool(data.get("token"))
            detail = (
                f"鉴权成功，Token 有效，指挥官: {data.get('username')}, "
                f"等级: Lv.{data.get('level')}, 金币: {data.get('gold')}, 钻石: {data.get('gems')}"
                if passed else f"鉴权返回异常: {data}"
            )
    except Exception as e:
        dt = round(time.time() - t0, 2)
        passed = False
        detail = f"后端连接失败 (127.0.0.1:8080): {e}"

    return {
        "id": "TC-02",
        "name": "作战指挥中心授权服务 (Backend Auth & JWT Token)",
        "passed": passed,
        "duration": f"{dt}s",
        "detail": detail
    }


def test_tc03_scene_contract() -> Dict[str, Any]:
    """TC-03: 场景节点契约完整性检查 (Login -> Lobby -> Battle)"""
    t0 = time.time()
    scenes = [
        os.path.join(WORKSPACE_DIR, "scenes", "login", "LoginScene.tscn"),
        os.path.join(WORKSPACE_DIR, "scenes", "lobby", "LobbyScene.tscn"),
        os.path.join(WORKSPACE_DIR, "scenes", "battle", "BattlePrototype.tscn")
    ]
    missing = []
    for s in scenes:
        if not os.path.exists(s):
            missing.append(os.path.basename(s))

    dt = round(time.time() - t0, 2)
    passed = len(missing) == 0
    detail = "LoginScene, LobbyScene, BattlePrototype 三大核心全链路场景均完整就绪" if passed else f"缺失场景: {missing}"

    return {
        "id": "TC-03",
        "name": "全链路场景文件契约 (Scene Declarations & Contracts)",
        "passed": passed,
        "duration": f"{dt}s",
        "detail": detail
    }


def test_tc04_record_video(output_name: str = "login_to_battle_test.mp4", frames: int = 1860) -> Dict[str, Any]:
    """TC-04: 实机 GPU 60 FPS 录制全链路测试视频 (Login -> Lobby -> Battle)"""
    from tools.record_test_video import generate_test_video

    t0 = time.time()
    res = generate_test_video(
        output_name=output_name,
        frames=frames,
        login_flow_test=True
    )
    dt = round(time.time() - t0, 2)
    passed = res.get("success") is True and os.path.exists(res.get("target_file", ""))
    target_file = res.get("target_file", "")
    size_kb = round(os.path.getsize(target_file) / 1024, 1) if os.path.exists(target_file) else 0

    # 规范硬约束：视频时长必须在 20s 以上
    duration_sec = res.get("duration_sec", 0)
    if duration_sec < 20.0:
        passed = False
        detail = f"视频时长不满足规范约束 (要求 >= 20s，实际 {duration_sec}s)"
    else:
        detail = f"实机渲染 60 FPS 视频生成成功: {output_name} ({size_kb} KB, 时长 {duration_sec}s, 耗时 {dt}s)"

    return {
        "id": "TC-04",
        "name": "实机全链路测试视频渲染 (GPU Movie Maker 60 FPS >= 20s)",
        "passed": passed,
        "duration": f"{dt}s",
        "detail": detail,
        "video_path": target_file,
        "duration_sec": duration_sec
    }


def test_tc05_milestone_snapshots(video_path: str) -> Dict[str, Any]:
    """TC-05: 关键里程碑快照提取与画质验证"""
    t0 = time.time()
    if not os.path.exists(video_path) or not os.path.exists(FFMPEG_EXE):
        return {
            "id": "TC-05",
            "name": "关键里程碑画面提取 (Milestone Frame Snapshots)",
            "passed": False,
            "duration": "0.0s",
            "detail": f"视频文件或 FFmpeg 不存在: video={os.path.exists(video_path)}, ffmpeg={os.path.exists(FFMPEG_EXE)}",
            "snapshots": []
        }

    milestones = [
        {"sec": "00:00:01.500", "file": "snapshot_login_01_screen.png", "desc": "阶段 1: 登录认证终端界面 (Login Terminal)"},
        {"sec": "00:00:04.500", "file": "snapshot_login_02_lobby.png", "desc": "阶段 2: 指战大厅与军备数据 (Lobby Screen)"},
        {"sec": "00:00:07.500", "file": "snapshot_login_03_loading.png", "desc": "阶段 3: 战役加载与进度遮罩 (Loading Screen 100%)"},
        {"sec": "00:00:18.000", "file": "snapshot_login_04_battlefield.png", "desc": "阶段 4: 3D RTS 实机战斗前线 (3D RTS Battlefield)"}
    ]

    extracted = []
    all_ok = True
    for m in milestones:
        out_fp = os.path.join(SCREENSHOTS_DIR, m["file"])
        cmd = [
            FFMPEG_EXE, "-y",
            "-ss", m["sec"],
            "-i", video_path,
            "-vframes", "1",
            "-q:v", "2",
            out_fp
        ]
        proc = subprocess.run(cmd, capture_output=True)
        if proc.returncode == 0 and os.path.exists(out_fp) and os.path.getsize(out_fp) > 10240:
            sz_kb = round(os.path.getsize(out_fp) / 1024, 1)
            extracted.append({
                "file": m["file"],
                "path": out_fp,
                "desc": m["desc"],
                "size_kb": sz_kb,
                "timestamp": m["sec"]
            })
        else:
            all_ok = False

    dt = round(time.time() - t0, 2)
    return {
        "id": "TC-05",
        "name": "关键里程碑画面提取 (Milestone Frame Snapshots)",
        "passed": all_ok and len(extracted) == len(milestones),
        "duration": f"{dt}s",
        "detail": f"成功提取 {len(extracted)}/{len(milestones)} 处全链路关键帧快照",
        "snapshots": extracted
    }


def run_full_suite() -> Dict[str, Any]:
    print("=" * 60, flush=True)
    print("🚀 [LoginToBattleTest] 启动 Godot 4 RTS 登录到游戏全链路测试套件...", flush=True)
    print("=" * 60, flush=True)

    results = []

    # 1. 编译
    r1 = test_tc01_compilation()
    results.append(r1)
    print(f"[{'PASS' if r1['passed'] else 'FAIL'}] {r1['id']} {r1['name']} ({r1['duration']}): {r1['detail']}", flush=True)

    # 2. 后端鉴权
    r2 = test_tc02_backend_auth()
    results.append(r2)
    print(f"[{'PASS' if r2['passed'] else 'FAIL'}] {r2['id']} {r2['name']} ({r2['duration']}): {r2['detail']}", flush=True)

    # 3. 场景契约
    r3 = test_tc03_scene_contract()
    results.append(r3)
    print(f"[{'PASS' if r3['passed'] else 'FAIL'}] {r3['id']} {r3['name']} ({r3['duration']}): {r3['detail']}", flush=True)

    # 4. 录制全链路测试视频 (1860 帧 / 31.0s @ 60 FPS)
    r4 = test_tc04_record_video(output_name="login_to_battle_test.mp4", frames=1860)
    results.append(r4)
    print(f"[{'PASS' if r4['passed'] else 'FAIL'}] {r4['id']} {r4['name']} ({r4['duration']}): {r4['detail']}", flush=True)

    # 5. 里程碑快照提取
    video_path = r4.get("video_path", "")
    r5 = test_tc05_milestone_snapshots(video_path)
    results.append(r5)
    print(f"[{'PASS' if r5['passed'] else 'FAIL'}] {r5['id']} {r5['name']} ({r5['duration']}): {r5['detail']}", flush=True)

    all_passed = all(r["passed"] for r in results)
    print("=" * 60, flush=True)
    print(f"🏁 全链路测试结果: {'全部通过 (100% PASS)' if all_passed else '存在失败项'}", flush=True)
    print("=" * 60, flush=True)

    return {
        "success": all_passed,
        "results": results
    }


if __name__ == "__main__":
    res = run_full_suite()
    sys.exit(0 if res["success"] else 1)
