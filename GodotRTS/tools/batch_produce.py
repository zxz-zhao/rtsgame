import os
import sys
import json
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

def batch_produce_masterpiece(
    project_dir: str,
    script_file: str = "",
    voice_type: str = "zh-CN-YunxiNeural",
    output_name: str = "final_anime_episode.mp4"
) -> str:
    """
    【多镜头商业成片一键装配引擎】
    将从【豆包 / 即梦 / 可灵】下载的多段 5 秒动作原片，
    按照剧本台词自动配音、压制金色字幕、2.39:1 宽画幅黑边，
    并无缝拼接为一部完整的 15~60 秒 3D 动漫商业大片！
    """
    p_dir = Path(project_dir)
    if not p_dir.exists():
        raise FileNotFoundError(f"找不到工程目录: {project_dir}")

    # 读取台词剧本
    scripts = []
    s_path = Path(script_file) if script_file else p_dir / "script.json"
    txt_path = p_dir / "script.txt"

    if s_path.exists():
        with open(s_path, "r", encoding="utf-8") as f:
            data = json.load(f)
            if isinstance(data, list):
                scripts = data
            elif isinstance(data, dict) and "scenes" in data:
                scripts = data["scenes"]
    elif txt_path.exists():
        with open(txt_path, "r", encoding="utf-8") as f:
            lines = [line.strip() for line in f if line.strip()]
            for idx, line in enumerate(lines, 1):
                scripts.append({"scene_id": idx, "narration": line})
    
    # 查找所有视频片段 (.mp4)
    video_files = sorted(list(p_dir.glob("*.mp4")))
    # 过滤掉已生成的成片
    video_files = [v for v in video_files if not v.name.endswith("_polished.mp4") and not v.name.startswith("final_")]

    if not video_files:
        raise FileNotFoundError(f"在 {project_dir} 下未找到可包装的原始 .mp4 视频文件！")

    print("=" * 65)
    print(f">> 【AnimeStudio 多镜头工业成片流水线启动】")
    print(f">> 扫描到素材片段: {len(video_files)} 个")
    print(f">> 配套剧本台词: {len(scripts)} 条")
    print("=" * 65)

    composer = VideoComposer()
    tts = TTSEngine()

    polished_clips = []
    temp_files = []

    for idx, v_path in enumerate(video_files):
        scene_num = idx + 1
        narration = ""
        if idx < len(scripts):
            item = scripts[idx]
            narration = item.get("narration", "") if isinstance(item, dict) else str(item)

        print(f"\n[镜头 {scene_num}/{len(video_files)}] 正在包装片段: {v_path.name}")
        
        # 1. 生成配音
        voice_path = None
        dur = 5.0
        if narration:
            voice_path = str(p_dir / f"scene_{scene_num:02d}_voice.mp3")
            print(f"  🎙️ 配音台词: \"{narration}\"")
            dur = tts.generate_speech(narration, voice_type, voice_path)
            temp_files.append(voice_path)

        # 2. 压制字幕与宽荧幕黑边
        polished_path = str(p_dir / f"scene_{scene_num:02d}_polished.mp4")
        print(f"  🎬 压制 2.39:1 宽画幅与金色书法台词字幕...")
        composer.burn_cinematic_elements(str(v_path), voice_path or str(v_path), narration, dur, polished_path)
        polished_clips.append(polished_path)
        temp_files.append(polished_path)

    # 3. 多镜头无缝拼接并混入全局史诗战歌
    print("\n" + "=" * 65)
    print(">> [最终合成] 正在将所有分镜拼接为完整商业大片，并混入史诗战斗 BGM...")
    final_output = str(OUTPUTS_DIR / output_name)

    candidates = list(BGM_DIR.glob("*.mp3")) + list(BGM_DIR.glob("*.wav"))
    battle_bgms = [b for b in candidates if "battle" in b.name.lower() or "rock" in b.name.lower()]
    bgm_path = str(battle_bgms[0] if battle_bgms else candidates[0]) if candidates else None

    composer.concat_and_mix_bgm(polished_clips, final_output, bgm_path)

    print("=" * 65)
    print("🎉 【商业级 3D 动漫大片总装成功！】")
    print(f"📁 最终成片: {final_output}")
    print(f"📊 文件大小: {os.path.getsize(final_output)/(1024*1024):.2f} MB")
    print(f"✨ 片段总数: {len(polished_clips)} 镜头")
    print("=" * 65)

    return final_output

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="多镜头 3D 动漫商业大片一键总装打包")
    parser.add_argument("--dir", type=str, required=True, help="存放豆包/即梦下载的原视频与剧本文件的目录")
    parser.add_argument("--script", type=str, default="", help="指定 script.json 或 script.txt 文件")
    parser.add_argument("--voice", type=str, default="zh-CN-YunxiNeural", help="配音角色音色")
    parser.add_argument("--output", type=str, default="masterpiece_commercial_product.mp4", help="成片文件名")

    args = parser.parse_args()
    batch_produce_masterpiece(
        project_dir=args.dir,
        script_file=args.script,
        voice_type=args.voice,
        output_name=args.output
    )
