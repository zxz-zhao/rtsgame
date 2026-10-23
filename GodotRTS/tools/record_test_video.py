# -*- coding: utf-8 -*-
"""
Godot 4 RTS 实机功能自动化测试视频录制引擎 (Automated Gameplay Test Video Recorder)
利用 Godot 4 原生 Movie Maker 模式在本地物理 GPU (Vulkan) 上按 60 FPS 录制实机功能运行画面，
并自动调用 FFmpeg 转码为高兼容、超低体积的 H.264/AAC MP4 视频，
供 Rocket.Chat 与 Web 端直接在线流畅播放。
"""

import os
import sys
import time
import subprocess
import shutil
from typing import Dict, Any, Optional, List

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCREENSHOTS_DIR = os.path.join(WORKSPACE_DIR, "screenshots")
SCRATCH_DIR = os.path.join(WORKSPACE_DIR, "scratch")

os.makedirs(SCREENSHOTS_DIR, exist_ok=True)
os.makedirs(SCRATCH_DIR, exist_ok=True)

GODOT_CONSOLE = r"G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe"
GODOT_EXE = r"G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe"
FFMPEG_EXE = r"E:\Lily\ffmpeg.exe"


def find_latest_modified_ui() -> Optional[str]:
    """寻找最近 15 分钟内修改过的 UI 场景"""
    scenes_ui = os.path.join(WORKSPACE_DIR, "scenes", "ui")
    if not os.path.exists(scenes_ui):
        return None

    now = time.time()
    latest_scene = None
    max_mtime = 0

    for fname in os.listdir(scenes_ui):
        if fname.endswith(".tscn"):
            fp = os.path.join(scenes_ui, fname)
            mtime = os.path.getmtime(fp)
            if mtime > max_mtime:
                max_mtime = mtime
                latest_scene = f"res://scenes/ui/{fname}"

    if latest_scene and (now - max_mtime <= 900):
        return latest_scene
    return None


def generate_test_video(
    scene: Optional[str] = None,
    output_name: Optional[str] = None,
    frames: int = 1860,
    timeout_sec: int = 180,
    login_flow_test: bool = False,
    extra_args: Optional[List[str]] = None
) -> Dict[str, Any]:
    """
    调度物理 GPU 渲染管线，录制并输出实机测试视频 MP4。
    """
    godot_bin = GODOT_CONSOLE if os.path.exists(GODOT_CONSOLE) else GODOT_EXE
    if not os.path.exists(godot_bin):
        return {
            "success": False,
            "error": f"未找到 Godot 可执行文件: {godot_bin}"
        }

    ts = time.strftime("%Y%m%d_%H%M%S")
    if not output_name or not isinstance(output_name, str):
        output_name = f"test_video_{ts}.mp4"
    if not output_name.lower().endswith(".mp4"):
        output_name += ".mp4"

    # 规范硬约束：实机测试视频时长必须在 20s 以上（以 60 FPS 计至少 1200 帧，默认 1860 帧 / 31 秒）
    frames = max(frames, 1200)

    output_name = os.path.basename(output_name)
    target_mp4 = os.path.join(SCREENSHOTS_DIR, output_name)
    temp_avi = os.path.join(SCRATCH_DIR, f"temp_rec_{ts}.avi")

    # 智能推断测试录制场景
    is_ui_preview = False
    if login_flow_test:
        scene = "res://scenes/login/LoginScene.tscn"
        is_ui_preview = False
        target_desc = "实机全链路测试 (登录 -> 大厅 -> 加载 -> 3D战场)"
        if extra_args is None:
            extra_args = []
        if "--login-flow-test" not in extra_args:
            extra_args.append("--login-flow-test")
    elif not scene:
        latest_ui = find_latest_modified_ui()
        if latest_ui:
            scene = latest_ui
            is_ui_preview = True
        else:
            scene = "res://scenes/test/AutoCombatScene.tscn"

    if not login_flow_test and ("scenes/ui/" in scene or "UiPreview" in scene):
        is_ui_preview = True

    # 构建 Godot 命令行
    if is_ui_preview:
        cmd = [
            godot_bin,
            "--path", WORKSPACE_DIR,
            "--write-movie", temp_avi,
            "scenes/debug/UiPreviewScene.tscn",
            "--",
            "--ui-scene", scene,
            "--capture-frames", str(max(20, frames))
        ]
        target_desc = f"UI 组件场景 ({os.path.basename(scene)})"
    else:
        cmd = [
            godot_bin,
            "--path", WORKSPACE_DIR,
            "--write-movie", temp_avi,
            scene,
            "--",
            "--capture-preview",
            "--capture-frames", str(max(30, frames))
        ]
        if not login_flow_test:
            target_desc = f"3D RTS 实机战斗场景 ({os.path.basename(scene)})"

    if extra_args:
        cmd.extend(extra_args)

    print(f"🎥 [VideoRecorder] 开始调度 GPU 录制: {target_desc}, 目标帧数: {frames}...", flush=True)

    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        encoding="utf-8",
        errors="replace",
        cwd=WORKSPACE_DIR
    )

    stdout_text, stderr_text = "", ""
    try:
        stdout_text, stderr_text = proc.communicate(timeout=timeout_sec)
    except subprocess.TimeoutExpired:
        proc.kill()
        stderr_text = f"Godot 视频录制进程超过 {timeout_sec}s 熔断阈值"

    if not os.path.exists(temp_avi) or os.path.getsize(temp_avi) == 0:
        err_msg = (stdout_text + "\n" + stderr_text).strip()
        print(f"❌ [VideoRecorder] Godot 视频原始帧捕获失败: {err_msg[:300]}", flush=True)
        return {
            "success": False,
            "error": f"Godot 无法生成原始视频帧文件: {temp_avi}\n{err_msg[-300:]}"
        }

    avi_size_kb = round(os.path.getsize(temp_avi) / 1024, 1)
    print(f"✅ [VideoRecorder] Godot 原始 AVI 录制就绪 ({avi_size_kb} KB)，开始转码 MP4...", flush=True)

    # 调用 FFmpeg 压缩转码为网页与移动端秒播的 H.264 / AAC MP4
    transcode_success = False
    if os.path.exists(FFMPEG_EXE):
        ffmpeg_cmd = [
            FFMPEG_EXE,
            "-y",
            "-i", temp_avi,
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-pix_fmt", "yuv420p",
            "-crf", "27",
            "-maxrate", "1.8M",
            "-bufsize", "3.6M",
            "-movflags", "+faststart",
            target_mp4
        ]
        try:
            ff_proc = subprocess.run(ffmpeg_cmd, capture_output=True, timeout=120)
            if ff_proc.returncode == 0 and os.path.exists(target_mp4) and os.path.getsize(target_mp4) > 0:
                transcode_success = True
        except Exception as ff_err:
            print(f"⚠️ [VideoRecorder] FFmpeg 转码异常: {ff_err}", flush=True)

    # 兜底：如果 FFmpeg 转码异常，使用 OpenCV cv2
    if not transcode_success:
        try:
            import cv2
            cap = cv2.VideoCapture(temp_avi)
            fps = cap.get(cv2.CAP_PROP_FPS) or 60.0
            width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH)) or 1280
            height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT)) or 720
            fourcc = cv2.VideoWriter_fourcc(*'mp4v')
            out = cv2.VideoWriter(target_mp4, fourcc, fps, (width, height))
            while cap.isOpened():
                ret, frame = cap.read()
                if not ret:
                    break
                out.write(frame)
            cap.release()
            out.release()
            if os.path.exists(target_mp4) and os.path.getsize(target_mp4) > 0:
                transcode_success = True
        except Exception as cv_err:
            print(f"⚠️ [VideoRecorder] OpenCV 转码异常: {cv_err}", flush=True)

    # 清理临时 AVI
    try:
        if os.path.exists(temp_avi):
            os.remove(temp_avi)
    except Exception:
        pass

    if not transcode_success or not os.path.exists(target_mp4) or os.path.getsize(target_mp4) == 0:
        return {
            "success": False,
            "error": "转码 MP4 失败"
        }

    mp4_size_kb = round(os.path.getsize(target_mp4) / 1024, 1)
    lan_ip = os.environ.get("LAN_HOST", "192.168.1.176")
    video_url = f"http://{lan_ip}:9564/screenshots/{output_name}"
    direct_url = f"http://127.0.0.1:8000/screenshots/{output_name}"

    print(f"🎉 [VideoRecorder] 自动化测试视频生成成功: {output_name} ({mp4_size_kb} KB, 60 FPS)", flush=True)

    return {
        "success": True,
        "filename": output_name,
        "target_file": target_mp4,
        "video_url": video_url,
        "direct_url": direct_url,
        "file_size": f"{mp4_size_kb} KB",
        "fps": 60,
        "frames": frames,
        "duration_sec": round(frames / 60.0, 2),
        "scene": scene,
        "target_desc": target_desc
    }


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Record Godot 4 RTS automated test video")
    parser.add_argument("--scene", type=str, default=None, help="Target scene path (e.g. res://scenes/test/AutoCombatScene.tscn)")
    parser.add_argument("--map", type=str, default=None, help="Target map name (e.g. 三路大会战 (3v3/5v5))")
    parser.add_argument("--frames", type=int, default=1860, help="Frames to capture (default 1860, min 1200 / 20s)")
    parser.add_argument("--output", type=str, default=None, help="Output MP4 file name")
    parser.add_argument("--login-flow-test", action="store_true", help="Record automated Login-to-Battle full flow test")
    parser.add_argument("--extra-args", nargs="*", default=None, help="Extra CLI arguments passed to Godot")
    args, unknown = parser.parse_known_args()

    combined_extra = []
    if args.map:
        combined_extra.extend(["--map", args.map])
    if args.extra_args:
        combined_extra.extend(args.extra_args)
    if unknown:
        combined_extra.extend(unknown)

    res = generate_test_video(
        scene=args.scene,
        output_name=args.output,
        frames=args.frames,
        login_flow_test=args.login_flow_test,
        extra_args=combined_extra if combined_extra else None
    )
    print(res)

