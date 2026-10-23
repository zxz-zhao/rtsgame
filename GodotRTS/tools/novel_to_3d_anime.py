import os
import sys
import argparse
import time
import uuid
from pathlib import Path

# Add AnimeStudio to sys.path
BASE_DIR = Path("H:/AnimeStudio")
sys.path.insert(0, str(BASE_DIR))

from core.config import OUTPUTS_DIR, BGM_DIR, ANIME_STYLES, VOICE_PRESETS
from core.storyboard import StoryboardEngine
from core.tts_engine import TTSEngine
from core.image_engine import AnimeImageEngine
from core.video_composer import VideoComposer
from core.cloud_video_engine import CloudVideoEngine

def is_action_scene(narration: str, prompt: str) -> bool:
    """自动判断分镜是否属于高潮武戏/动作挥砍镜头"""
    action_keywords = ["斩", "挥", "砍", "劈", "击", "破", "战", "尺出", "火莲", "拳", "剑", "决", "杀", "轰", "暴起"]
    text = narration + " " + prompt
    return any(k in text for k in action_keywords)

def novel_to_3d_anime(
    novel_text: str,
    output_name: str = None,
    style: str = "donghua_3d_ue5",
    voice: str = "zh-CN-YunxiNeural",
    max_scenes: int = 5,
    include_bgm: bool = True,
    use_cloud_action: bool = True,
    cloud_provider: str = "zhipu",
    cloud_key: str = ""
) -> str:
    """
    【路线 B 全自动工业管线】小说一键生成 3D 动漫大片
    - 70% 文戏/叙事镜头: 本地 3D 景深矩阵 + 粒子空间推拉 (0 成本、本地秒级出片)
    - 30% 核心武戏镜头: 自动调度云端超低成本视频算力 (智谱清影/阿里Wan2.1) 生成真实大刀挥舞与肢体打斗
    """
    if not novel_text.strip():
        raise ValueError("小说文本内容不能为空！")

    timestamp = int(time.time())
    proj_id = output_name or f"novel_3d_{timestamp}_{uuid.uuid4().hex[:6]}"
    proj_dir = OUTPUTS_DIR / proj_id
    proj_dir.mkdir(parents=True, exist_ok=True)

    cloud_engine = CloudVideoEngine()
    if cloud_key:
        cloud_engine.update_api_key(cloud_provider, cloud_key)
    elif cloud_provider:
        cloud_engine.config["active_provider"] = cloud_provider

    active_prov = cloud_engine.config.get("active_provider", "zhipu")
    has_cloud_key = bool(cloud_engine.config["providers"].get(active_prov, {}).get("api_key", "").strip())

    print("=" * 65)
    print(">> 【AnimeStudio 3D - 路线 B】本地工作台 + 云端低成本算力工业管线启动")
    print(f">> 项目工程: {proj_id}")
    print(f">> 视觉风格: {ANIME_STYLES.get(style, {}).get('name', style)}")
    print(f">> 配音音色: {VOICE_PRESETS.get(voice, {}).get('name', voice)}")
    print(f">> 镜头数量: {max_scenes} 幕")
    print(f">> 云端动作算力: {cloud_engine.config['providers'].get(active_prov, {}).get('name', active_prov)} (已配置Key: {has_cloud_key})")
    print("=" * 65)

    if sys.platform == "win32":
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

    # 1. 深度学习分镜解构 (DeepSeek)
    print("\n[Step 1/4] 正在调用 DeepSeek 进行 3D 电影镜头脚本分镜拆解与动静标注...")
    storyboard = StoryboardEngine()
    scenes = storyboard.generate(novel_text, style_key=style, max_scenes=max_scenes)
    print(f">> 成功生成 {len(scenes)} 幕分镜：")
    for s in scenes:
        action_flag = "【武戏·大招动作】" if is_action_scene(s['narration'], s['prompt']) else "【文戏·3D叙事】"
        print(f"   镜头 {s['scene_id']} {action_flag} [{s.get('motion', 'dolly_zoom_3d')}]: {s['narration']}")

    # 2. 引擎初始化
    tts = TTSEngine()
    img_engine = AnimeImageEngine()
    composer = VideoComposer()

    clip_paths = []

    # 3. 逐镜头生产
    print("\n[Step 2/4] 混合管线调度：本地 3D 渲染 + 云端动作大模型生成...")
    for sc in scenes:
        s_id = sc["scene_id"]
        narration = sc["narration"]
        prompt = sc["prompt"]
        motion = sc.get("motion", "dive_push_3d")
        is_action = is_action_scene(narration, prompt)

        img_path = str(proj_dir / f"scene_{s_id:02d}.jpg")
        audio_path = str(proj_dir / f"scene_{s_id:02d}.mp3")
        clip_path = str(proj_dir / f"scene_{s_id:02d}.mp4")

        print(f"\n--- 处理镜头 {s_id:02d} ---")
        print(f"原画生成中: {prompt[:70]}...")
        img_engine.generate_image(prompt, style, img_path)

        print(f"语音合成中: \"{narration}\"")
        dur = tts.generate_speech(narration, voice, audio_path)
        print(f"语音时长: {dur:.2f} 秒")

        # 核心判断：是否触发云端真实肢体动作生成
        if use_cloud_action and is_action and has_cloud_key:
            print(f"🚀 【触发云端算力】调用 {active_prov} 生成真实大刀挥舞与肢体打斗动作视频...")
            try:
                # 强化动作提示词
                enhanced_action_prompt = (
                    f"{prompt}, dynamic combat motion, character violently swinging giant sword downward, "
                    f"blazing energy dragon slash wave, dramatic martial arts action, cinematic battle animation, 60fps"
                )
                cloud_raw_clip = str(proj_dir / f"scene_{s_id:02d}_cloud_raw.mp4")
                cloud_engine.generate_action_video(img_path, enhanced_action_prompt, cloud_raw_clip, active_prov)
                
                # 为云端视频压制本地音频、字幕与 2.39:1 宽荧幕
                print("叠加本地音频、电影宽画幅黑边与金色字幕...")
                composer.burn_cinematic_elements(cloud_raw_clip, audio_path, narration, dur, clip_path)
                clip_paths.append(clip_path)
                continue
            except Exception as e:
                print(f"⚠️ 云端算力调用失败 ({e})，自动回落至本地 3D 矩阵运镜...")

        # 本地 3D 空间矩阵运镜（文戏或回落镜头）
        print(f"本地 3D 空间矩阵运镜合成 [{motion}] (3D 景深粒子 + 宽幅黑边 + 金色字幕)...")
        composer.generate_ken_burns_scene(img_path, audio_path, narration, motion, dur, clip_path)
        clip_paths.append(clip_path)

    # 4. 最终拼接与 BGM 动态闪避混音
    print("\n[Step 3/4] 拼接全部分镜并混入史诗战斗背景音乐...")
    final_output_path = str(OUTPUTS_DIR / f"{proj_id}_masterpiece.mp4")
    
    bgm_path = None
    if include_bgm:
        candidates = list(BGM_DIR.glob("*.mp3")) + list(BGM_DIR.glob("*.wav"))
        if candidates:
            battle_bgms = [b for b in candidates if "battle" in b.name.lower() or "rock" in b.name.lower()]
            bgm_path = str(battle_bgms[0] if battle_bgms else candidates[0])
            print(f">> 使用背景音乐: {bgm_path}")

    composer.concat_and_mix_bgm(clip_paths, final_output_path, bgm_path)

    print("\n" + "=" * 65)
    print(">> 【路线 B 工业成片输出成功！】")
    print(f">> 成片路径: {final_output_path}")
    print(f">> 文件大小: {os.path.getsize(final_output_path) / (1024*1024):.2f} MB")
    print("=" * 65)

    return final_output_path

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="【路线 B】小说一键生成 3D 动漫大片 (本地+云端低成本算力)")
    parser.add_argument("--novel", type=str, default="", help="小说文本内容")
    parser.add_argument("--file", type=str, default="", help="小说文本文件路径 (.txt)")
    parser.add_argument("--style", type=str, default="donghua_3d_ue5", help="3D 动漫视觉风格")
    parser.add_argument("--voice", type=str, default="zh-CN-YunxiNeural", help="配音角色音色")
    parser.add_argument("--scenes", type=int, default=5, help="镜头数量 (3-8)")
    parser.add_argument("--output", type=str, default="", help="输出工程名称")
    parser.add_argument("--cloud-provider", type=str, default="zhipu", help="云端平台 (zhipu / dashscope / minimax / kling)")
    parser.add_argument("--cloud-key", type=str, default="", help="设置对应云端平台的 API Key")
    parser.add_argument("--no-cloud", action="store_true", help="强制纯本地运行，不调用云端")

    args = parser.parse_args()

    content = args.novel
    if args.file and os.path.exists(args.file):
        with open(args.file, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()

    if not content:
        content = "中州云岚天阶之上，萧炎手托两色异火，青莲地心火与骨冷幽火激荡交融。他眼中神光暴涨，猛然合拢双掌！璀璨的佛怒火莲冲天而起，玄重尺横空斩出，气贯长虹！"

    novel_to_3d_anime(
        novel_text=content,
        output_name=args.output or None,
        style=args.style,
        voice=args.voice,
        max_scenes=args.scenes,
        include_bgm=True,
        use_cloud_action=not args.no_cloud,
        cloud_provider=args.cloud_provider,
        cloud_key=args.cloud_key
    )
