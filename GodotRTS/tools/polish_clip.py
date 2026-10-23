import os
import sys
import argparse
from pathlib import Path

if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

BASE_DIR = Path("H:/AnimeStudio")
sys.path.insert(0, str(BASE_DIR))

from core.video_composer import VideoComposer
from core.tts_engine import TTSEngine
from core.config import OUTPUTS_DIR, BGM_DIR

def polish_free_ai_video(
    raw_video_path: str,
    subtitle_text: str = "",
    voice_type: str = "zh-CN-YunxiNeural",
    custom_audio_path: str = "",
    output_path: str = ""
) -> str:
    """
    【一键电影级包装工具】
    将从【豆包 / 即梦 / 可灵】等免费平台下载的裸视频，一键升级为带有：
    1. 角色高燃配音 (Edge-TTS 热血少年音/史诗音)
    2. 2.39:1 电影级宽画幅黑边
    3. 金色书法体高透字幕铭牌
    4. 摇滚/史诗战斗 BGM 与动态人声闪避混音
    直接输出发布级的成品大片！
    """
    if not os.path.exists(raw_video_path):
        raise FileNotFoundError(f"找不到输入的视频文件: {raw_video_path}")

    composer = VideoComposer()
    tts = TTSEngine()

    base_name = Path(raw_video_path).stem
    out_file = output_path or str(OUTPUTS_DIR / f"{base_name}_cinematic_masterpiece.mp4")
    temp_clip = str(OUTPUTS_DIR / f"{base_name}_temp_burned.mp4")

    # 1. 准备配音
    audio_file = custom_audio_path
    if not audio_file or not os.path.exists(audio_file):
        if subtitle_text:
            audio_file = str(OUTPUTS_DIR / f"{base_name}_voice.mp3")
            print(f">> 正在合成角色台词配音: \"{subtitle_text}\"...")
            tts.generate_speech(subtitle_text, voice_type, audio_file)
        else:
            audio_file = None

    # 获取音频时长
    duration = tts.get_audio_duration(audio_file) if audio_file else 5.0

    print(f">> 正在为视频压制 2.39:1 宽荧幕黑边与金色书法字幕...")
    composer.burn_cinematic_elements(raw_video_path, audio_file or raw_video_path, subtitle_text, duration, temp_clip)

    print(f">> 正在混入史诗战斗 BGM 并进行动态音频闪避...")
    candidates = list(BGM_DIR.glob("*.mp3")) + list(BGM_DIR.glob("*.wav"))
    battle_bgms = [b for b in candidates if "battle" in b.name.lower() or "rock" in b.name.lower()]
    bgm_path = str(battle_bgms[0] if battle_bgms else candidates[0]) if candidates else None

    composer.concat_and_mix_bgm([temp_clip], out_file, bgm_path)

    if os.path.exists(temp_clip):
        os.remove(temp_clip)

    print("=" * 65)
    print("🎉 【电影级大片包装完成！】可以直接发布：")
    print(f"📁 成品文件: {out_file}")
    print(f"📊 文件大小: {os.path.getsize(out_file)/(1024*1024):.2f} MB")
    print("=" * 65)
    return out_file

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="一键包装豆包/即梦/可灵视频为电影级大片")
    parser.add_argument("--video", type=str, required=True, help="从豆包/即梦下载的原始视频路径 (.mp4)")
    parser.add_argument("--sub", type=str, default="玄重尺出，天崩地裂！接我这一式——焰分噬浪尺！！", help="角色台词字幕")
    parser.add_argument("--voice", type=str, default="zh-CN-YunxiNeural", help="配音音色")
    parser.add_argument("--audio", type=str, default="", help="可选自定义配音音频路径 (.mp3)")
    parser.add_argument("--output", type=str, default="", help="输出文件路径")

    args = parser.parse_args()
    polish_free_ai_video(
        raw_video_path=args.video,
        subtitle_text=args.sub,
        voice_type=args.voice,
        custom_audio_path=args.audio,
        output_path=args.output
    )
