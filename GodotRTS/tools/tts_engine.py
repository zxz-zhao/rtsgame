import os
import asyncio
import edge_tts
from pathlib import Path
from .config import VOICE_PRESETS, FFMPEG_PATH
import subprocess
import json

class TTSEngine:
    def __init__(self):
        self.proxy_candidate = "http://127.0.0.1:7897"

    async def generate_speech_async(self, text: str, voice: str, output_path: str, rate: str = "+0%", pitch: str = "+0Hz") -> float:
        if voice not in VOICE_PRESETS:
            voice = "zh-CN-YunxiNeural"
        
        # 尝试 1: 直接合成
        try:
            communicate = edge_tts.Communicate(text=text, voice=voice, rate=rate, pitch=pitch)
            await communicate.save(output_path)
            return self.get_audio_duration(output_path)
        except Exception as e_direct:
            print(f"[TTSEngine] Direct synthesize failed ({e_direct}), trying local proxy ({self.proxy_candidate})...")

        # 尝试 2: 走本地代理通道 (Clash 7897) 规避 403 地区阻断
        try:
            communicate = edge_tts.Communicate(text=text, voice=voice, rate=rate, pitch=pitch, proxy=self.proxy_candidate)
            await communicate.save(output_path)
            return self.get_audio_duration(output_path)
        except Exception as e_proxy:
            print(f"[TTSEngine] Proxy synthesize also failed: {e_proxy}")

        # 尝试 3: 本地静音/应急兜底，保障视频管线不中断
        print("[TTSEngine] Synthesizing emergency audio fallback...")
        cmd_fallback = [
            FFMPEG_PATH, "-y",
            "-f", "lavfi", "-i", "anullsrc=r=24000:cl=mono",
            "-t", "3.0",
            "-q:a", "9",
            output_path
        ]
        subprocess.run(cmd_fallback, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        return 3.0

    def generate_speech(self, text: str, voice: str, output_path: str, rate: str = "+0%", pitch: str = "+0Hz") -> float:
        return asyncio.run(self.generate_speech_async(text, voice, output_path, rate, pitch))

    def get_audio_duration(self, audio_path: str) -> float:
        try:
            cmd = [
                FFMPEG_PATH,
                "-i", audio_path,
                "-f", "null", "-"
            ]
            res = subprocess.run(cmd, stderr=subprocess.PIPE, stdout=subprocess.DEVNULL, text=True, errors="ignore")
            import re
            m = re.search(r"Duration:\s*(\d+):(\d+):(\d+\.\d+)", res.stderr)
            if m:
                hours = float(m.group(1))
                minutes = float(m.group(2))
                seconds = float(m.group(3))
                dur = hours * 3600 + minutes * 60 + seconds
                return max(dur, 2.0)
        except Exception as e:
            print(f"[TTSEngine] probe error: {e}")
        
        file_size = os.path.getsize(audio_path) if os.path.exists(audio_path) else 0
        est = file_size / 4000.0 if file_size > 0 else 3.0
        return max(est, 2.0)
