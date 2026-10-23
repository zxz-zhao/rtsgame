import os
from pathlib import Path

DEST_DIR = Path("H:/AnimeStudio")
(DEST_DIR / "core").mkdir(parents=True, exist_ok=True)
(DEST_DIR / "assets/bgm").mkdir(parents=True, exist_ok=True)
(DEST_DIR / "assets/fonts").mkdir(parents=True, exist_ok=True)
(DEST_DIR / "outputs").mkdir(parents=True, exist_ok=True)

# 1. core/config.py
CONFIG_PY = '''import os
from pathlib import Path
import imageio_ffmpeg

BASE_DIR = Path("H:/AnimeStudio")
CORE_DIR = BASE_DIR / "core"
STATIC_DIR = BASE_DIR / "static"
ASSETS_DIR = BASE_DIR / "assets"
OUTPUTS_DIR = BASE_DIR / "outputs"
BGM_DIR = ASSETS_DIR / "bgm"
FONTS_DIR = ASSETS_DIR / "fonts"

for p in [OUTPUTS_DIR, BGM_DIR, FONTS_DIR]:
    p.mkdir(parents=True, exist_ok=True)

SERVER_HOST = "0.0.0.0"
SERVER_PORT = 8088

try:
    FFMPEG_PATH = imageio_ffmpeg.get_ffmpeg_exe()
except Exception:
    FFMPEG_PATH = "ffmpeg"

DEFAULT_LLM_BASE_URL = os.getenv("LLM_BASE_URL", "http://127.0.0.1:8000/v1")
DEFAULT_LLM_API_KEY = os.getenv("LLM_API_KEY", "sk-cockpit-master-2026")
DEFAULT_LLM_MODEL = os.getenv("LLM_MODEL", "chatos-deepseek")

# 动漫视觉风格预设 (全面升级为次世代 3D 国漫与 3D CG 渲染引擎，彻底杜绝扁平2D纸片感)
ANIME_STYLES = {
    "donghua_3d_ue5": {
        "name": "次世代 3D 国漫玄幻 (虚幻5/斗破·凡人CG风)",
        "is_3d": True,
        "prompt_prefix": "masterpiece, best quality, breathtaking 3D CGI anime, Unreal Engine 5 render, cinematic 3D character model, Octane Render 8k, ray tracing global illumination, subsurface scattering skin, realistic hair strands and cloth simulation, deep spatial perspective, dramatic 3D volumetric lighting, IMAX 3D donghua movie aesthetic, photorealistic 3D CG, highly detailed 3D scene",
        "negative_prompt": "2D flat, drawing, sketch, illustration, line art, flat color, low quality, cartoon, paper cutout, 2d animation, flat anime, anime drawing, watermark, text, lowres, blurry"
    },
    "donghua_3d_action": {
        "name": "3D 电影级玄幻对决 (极限景深战斗特写)",
        "is_3d": True,
        "prompt_prefix": "masterpiece, ultra-detailed 3D CGI anime battle, Unreal Engine 5 cinematic, dynamic 3D camera angle, extreme perspective foreshortening, glowing magical energy aura, volumetric smoke, ray traced reflections, photorealistic 3D donghua, IMAX 3D composition, motion blur, 8k render, hyper-detailed 3D sculpt",
        "negative_prompt": "2D, flat, drawing, sketch, illustration, low quality, cartoon, paper cutout, 2d animation, watermark"
    },
    "scifi_3d_mech": {
        "name": "次世代 3D 科幻机甲 (光追金属与纵深机舱)",
        "is_3d": True,
        "prompt_prefix": "masterpiece, 3D CGI render, Unreal Engine 5, high-tech mech armor, ray-traced metallic reflections, volumetric neon smoke, extreme depth of field, Octane render 8k, photorealistic 3D sci-fi anime, cinematic composition, futuristic cockpit view, 3D spatial depth",
        "negative_prompt": "2D, flat, drawing, sketch, illustration, low quality, cartoon, paper cutout, watermark"
    },
    "japanese_3d_cg": {
        "name": "日系 3D 电影级 CG (最终幻想/降临之子风)",
        "is_3d": True,
        "prompt_prefix": "masterpiece, Final Fantasy VII Remake 3D CGI style, ultra-detailed 3D character, Square Enix movie aesthetic, Unreal Engine 5 render, ray tracing, photorealistic 3D anime cinematic, volumetric lighting, 8k wallpaper, realistic depth of field",
        "negative_prompt": "2D, flat, drawing, sketch, illustration, low quality, cartoon, paper cutout, watermark"
    },
    "japanese_anime": {
        "name": "日系新海诚光影风 (2.5D Makoto Shinkai)",
        "is_3d": False,
        "prompt_prefix": "masterpiece, best quality, ultra-detailed anime screenshot, Makoto Shinkai style, vibrant colors, cinematic lighting, dramatic sky, volumetric dust particles, beautiful background, anime movie, 8k wallpaper",
        "negative_prompt": "lowres, bad anatomy, bad hands, text, error, missing fingers, cropped, worst quality, low quality, jpeg artifacts, signature, watermark, blurry"
    },
    "cyberpunk_anime": {
        "name": "赛博朋克二次元风 (Cyberpunk 2.5D)",
        "is_3d": False,
        "prompt_prefix": "masterpiece, best quality, cyberpunk anime style, neon lighting, glowing circuitry, high tech city, futuristic aesthetic, cinematic composition, Ghost in the Shell style, 8k",
        "negative_prompt": "lowres, blurry, dull, flat color, bad anatomy, watermark"
    },
    "ghibli": {
        "name": "吉卜力唯美治愈风 (Studio Ghibli 2.5D)",
        "is_3d": False,
        "prompt_prefix": "masterpiece, best quality, Studio Ghibli style, Hayao Miyazaki aesthetic, lush greenery, hand-painted aesthetic, warm natural sunlight, nostalgic atmosphere, 8k",
        "negative_prompt": "lowres, dark, realistic photo, watermark, blurry"
    }
}

# 3D 空间运镜模式清单
CAMERA_3D_MOTIONS = [
    "dolly_zoom_3d",    # 3D 空间畸变变焦 (希区柯克变焦，背景强烈收缩/扩张，立体纵深感拉满)
    "dive_push_3d",     # 3D 俯冲纵深推近 (摄像机从上方俯冲推近主体，透视仰俯形变)
    "orbit_3d",         # 3D 弧线环绕视差 (摄像机弧线移动，左右偏航透视形变，产生强影视差)
    "flythrough_3d",    # 3D 纵深穿梭飞掠 (高速向前推进，灵气粒子在镜头两侧极速掠过)
    "crane_rise_3d",    # 3D 摇臂升镜纵深 (摄像机向上拉升并俯视，展示壮丽空间层次)
    "parallax_left_3d", # 3D 深度视差左平移 (带透视倾角的向左轨道滑移)
    "parallax_right_3d" # 3D 深度视差右平移 (带透视倾角的向右轨道滑移)
]

VOICE_PRESETS = {
    "zh-CN-YunxiNeural": {
        "name": "云希 (热血男主 / 男频高燃动漫旁白)",
        "gender": "male",
        "style": "heroic"
    },
    "zh-CN-XiaoxiaoNeural": {
        "name": "晓晓 (灵动少女 / 女主甜美旁白 / 轻快治愈)",
        "gender": "female",
        "style": "gentle"
    },
    "zh-CN-YunjianNeural": {
        "name": "云健 (影视级深沉史诗 / 玄幻大帝解说)",
        "gender": "male",
        "style": "epic"
    },
    "zh-CN-YunyangNeural": {
        "name": "云扬 (专业动漫漫评 / 沉稳解说)",
        "gender": "male",
        "style": "narrator"
    },
    "zh-CN-XiaoyiNeural": {
        "name": "晓伊 (温婉知性 / 恋爱物语女声)",
        "gender": "female",
        "style": "warm"
    }
}

VIDEO_WIDTH = 1920
VIDEO_HEIGHT = 1080
VIDEO_FPS = 30
DEFAULT_FONT_PATH = r"C:\\Windows\\Fonts\\msyh.ttc"
'''

# 2. core/storyboard.py
STORYBOARD_PY = '''import os
import json
import re
import httpx
from typing import List, Dict, Any
from .config import DEFAULT_LLM_BASE_URL, DEFAULT_LLM_API_KEY, DEFAULT_LLM_MODEL, ANIME_STYLES, CAMERA_3D_MOTIONS

STORYBOARD_PROMPT = """你是一位拥有千万播放量的顶级 3D 国漫与次世代 3D CG 电影总导演（曾执导《斗破苍穹》《凡人修仙传》《完美世界》级别 3D 动画电影）。
你的任务是将用户提供的文章、故事或小说片段，改编为极具 3D 空间立体纵深感和电影级镜头张力的 3D 动漫分镜剧本（Storyboard）。

镜头设计核心原则：
1. 【杜绝平面2D感】：每个镜头必须具有明确的三维空间层次（前景景深、中景角色雕刻、远景透视消失点）。
2. 【3D 运镜参数】：每个镜头的 "motion" 必须从以下 3D 运镜中选择：
   - "dolly_zoom_3d": 3D 希区柯克空间畸变变焦（背景剧烈拉伸/收缩，强烈 3D 空间冲击）
   - "dive_push_3d": 3D 俯冲纵深推近（高机位俯冲逼近主体，透视俯仰形变）
   - "orbit_3d": 3D 弧线环绕视差（摄像机绕主体弧度滑轨，左右视角空间视差）
   - "flythrough_3d": 3D 纵深穿梭飞掠（镜头向景深极速推进，空间微粒迎面掠过）
   - "crane_rise_3d": 3D 摇臂升镜（摄像机垂直升空俯瞰三维浩瀚场景）
   - "parallax_left_3d": 3D 视差左滑轨（带透视倾角的左移滑轨）
   - "parallax_right_3d": 3D 视差右滑轨（带透视倾角的右移滑轨）
3. 【画面 Prompt 规范】：
   - 必须使用英文书写高清视觉提示词。
   - 必须强调 3D CGI、虚幻引擎5渲染质感（Unreal Engine 5 render, cinematic 3D character, Octane 8k, volumetric lighting, ray tracing, depth of field）。
   - 描述角色的三维立体面貌、动态身姿、发丝细节、服饰材质、灵气光芒及周围三维空间环境。
4. 【输出规范】：
   - 拆解为 3 至 8 个镜头。
   - 字段包括：
     - "scene_id": 整数编号 (1, 2, 3...)
     - "narration": 中文旁白/台词（精炼、高燃、情绪饱满）
     - "prompt": 英文 3D CGI 画面提示词
     - "motion": 3D 运镜代号（上述 7 种之一）
   - 必须只输出合法的 JSON 数组，严禁前缀闲聊。
"""

class StoryboardEngine:
    def __init__(self, base_url: str = DEFAULT_LLM_BASE_URL, api_key: str = DEFAULT_LLM_API_KEY, model: str = DEFAULT_LLM_MODEL):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
        self.model = model

    def generate(self, article: str, style_key: str = "donghua_3d_ue5", max_scenes: int = 6) -> List[Dict[str, Any]]:
        style_info = ANIME_STYLES.get(style_key, ANIME_STYLES["donghua_3d_ue5"])
        system_content = STORYBOARD_PROMPT + f"\\n当前 3D 动漫视觉风格要求：{style_info['name']}"
        user_content = f"请将以下文章改编为约 {max_scenes} 个镜头的 3D 次世代电影级动漫分镜剧本：\\n\\n{article}"

        try:
            client = httpx.Client(trust_env=False, timeout=60.0)
            url = f"{self.base_url}/chat/completions"
            headers = {
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json"
            }
            payload = {
                "model": self.model,
                "messages": [
                    {"role": "system", "content": system_content},
                    {"role": "user", "content": user_content}
                ],
                "temperature": 0.7,
                "max_tokens": 2048
            }
            resp = client.post(url, headers=headers, json=payload)
            if resp.status_code == 200:
                data = resp.json()
                raw_text = data["choices"][0]["message"]["content"]
                parsed = self._extract_json(raw_text)
                if parsed and isinstance(parsed, list) and len(parsed) > 0:
                    return self._normalize_scenes(parsed, style_key)
        except Exception as e:
            print(f"[StoryboardEngine] LLM Call Warning: {e}, using rule-based 3D storyboard segmentation...")

        return self._fallback_segmentation(article, style_key, max_scenes)

    def _extract_json(self, text: str):
        text = text.strip()
        match = re.search(r"```(?:json)?\\s*(\\[[\\s\\S]*?\\])\\s*```", text)
        if match:
            text = match.group(1)
        else:
            match_arr = re.search(r"(\\[[\\s\\S]*?\\])", text)
            if match_arr:
                text = match_arr.group(1)
        try:
            return json.loads(text)
        except Exception:
            return None

    def _normalize_scenes(self, scenes: List[Dict[str, Any]], style_key: str) -> List[Dict[str, Any]]:
        style_info = ANIME_STYLES.get(style_key, ANIME_STYLES["donghua_3d_ue5"])
        normalized = []
        motions = CAMERA_3D_MOTIONS
        for idx, sc in enumerate(scenes):
            scene_id = sc.get("scene_id", idx + 1)
            narration = sc.get("narration", "").strip() or f"镜头 {scene_id}"
            user_prompt = sc.get("prompt", "").strip()
            if not user_prompt:
                user_prompt = "3D character model, majestic stance, dramatic Unreal Engine 5 lighting, volumetric depth"
            full_prompt = f"{style_info['prompt_prefix']}, {user_prompt}"
            motion = sc.get("motion", motions[idx % len(motions)])
            if motion not in motions:
                # 容错映射
                mapping = {
                    "zoom_in": "dive_push_3d",
                    "zoom_out": "dolly_zoom_3d",
                    "pan_left": "parallax_left_3d",
                    "pan_right": "parallax_right_3d"
                }
                motion = mapping.get(motion, motions[idx % len(motions)])
            normalized.append({
                "scene_id": scene_id,
                "narration": narration,
                "prompt": full_prompt,
                "user_prompt": user_prompt,
                "motion": motion
            })
        return normalized

    def _fallback_segmentation(self, article: str, style_key: str, max_scenes: int = 6) -> List[Dict[str, Any]]:
        sentences = [s.strip() for s in re.split(r"[。！？\\n]+", article) if len(s.strip()) > 3]
        if not sentences:
            sentences = [article.strip() or "在浩瀚的三维苍穹之下，一段次世代的传奇史诗正赫然降临。"]

        if len(sentences) > max_scenes:
            step = len(sentences) / max_scenes
            selected = [sentences[int(i * step)] for i in range(max_scenes)]
        else:
            selected = sentences

        style_info = ANIME_STYLES.get(style_key, ANIME_STYLES["donghua_3d_ue5"])
        motions = CAMERA_3D_MOTIONS
        result = []
        for i, sentence in enumerate(selected):
            motion = motions[i % len(motions)]
            prompt_core = f"3D CGI donghua hero character, intense martial presence, Unreal Engine 5 volumetric lighting, deep 3D perspective, dynamic {motion} composition"
            result.append({
                "scene_id": i + 1,
                "narration": sentence,
                "prompt": f"{style_info['prompt_prefix']}, {prompt_core}",
                "user_prompt": prompt_core,
                "motion": motion
            })
        return result
'''

# 3. core/tts_engine.py
TTS_ENGINE_PY = '''import os
import asyncio
import edge_tts
from pathlib import Path
from .config import VOICE_PRESETS, FFMPEG_PATH
import subprocess
import json

class TTSEngine:
    def __init__(self):
        pass

    async def generate_speech_async(self, text: str, voice: str, output_path: str, rate: str = "+0%", pitch: str = "+0Hz") -> float:
        if voice not in VOICE_PRESETS:
            voice = "zh-CN-YunxiNeural"
        
        communicate = edge_tts.Communicate(text=text, voice=voice, rate=rate, pitch=pitch)
        await communicate.save(output_path)
        
        duration = self.get_audio_duration(output_path)
        return duration

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
            m = re.search(r"Duration:\\s*(\\d+):(\\d+):(\\d+\\.\\d+)", res.stderr)
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
'''

# 4. core/image_engine.py
IMAGE_ENGINE_PY = '''import os
import re
import random
import urllib.parse
import httpx
from pathlib import Path
from PIL import Image, ImageEnhance, ImageFilter
import cv2
import numpy as np

from .config import ANIME_STYLES, VIDEO_WIDTH, VIDEO_HEIGHT

class AnimeImageEngine:
    def __init__(self, provider: str = "ai_3d_primary"):
        self.provider = provider
        self.headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
        }

    def generate_image(self, prompt: str, style_key: str, output_path: str, seed: int = None) -> str:
        """根据分镜提示词生成具备真 3D 立体空间与虚幻引擎渲染质感的高清画面"""
        if seed is None:
            seed = random.randint(100000, 999999)

        style_info = ANIME_STYLES.get(style_key, ANIME_STYLES["donghua_3d_ue5"])
        is_3d = style_info.get("is_3d", True)

        # 核心通道 1: 对于所有 3D 风格（或默认），优先使用次世代 AI 3D 渲染引擎
        if is_3d:
            print(f"[ImageEngine] Rendering Next-Gen 3D CGI (Unreal Engine 5) via AI pipeline for seed {seed}...")
            ai_data = self._fetch_pollinations_ai(prompt, style_key, seed)
            if ai_data:
                with open(output_path, "wb") as f:
                    f.write(ai_data)
                self._post_process_3d_depth(output_path)
                print("[ImageEngine] Successfully synthesized photorealistic 3D Donghua render!")
                return output_path

        # 通道 2: 仅在明确选择 2D 风格时，才尝试检索动漫插画库
        if not is_3d:
            tags = self._extract_anime_tags(prompt, style_key)
            print(f"[ImageEngine] 2D Mode: Searching illustration with tags {tags}...")
            img_data = self._fetch_booru_illustration(tags)
            if img_data:
                with open(output_path, "wb") as f:
                    f.write(img_data)
                self._post_process_3d_depth(output_path)
                return output_path

        # 通道 3: AI 再次重试与保底渲染
        print("[ImageEngine] Fallback AI synthesis...")
        ai_data = self._fetch_pollinations_ai(prompt, style_key, seed)
        if ai_data:
            with open(output_path, "wb") as f:
                f.write(ai_data)
            self._post_process_3d_depth(output_path)
            return output_path

        # 通道 4: 兜底渲染 3D 史诗宇宙纵深原画
        self._generate_3d_epic_canvas(output_path, style_key)
        return output_path

    def _fetch_pollinations_ai(self, prompt: str, style_key: str, seed: int) -> bytes:
        style_info = ANIME_STYLES.get(style_key, ANIME_STYLES["donghua_3d_ue5"])
        full_prompt = prompt
        if style_info["prompt_prefix"] not in prompt:
            full_prompt = f"{style_info['prompt_prefix']}, {prompt}"

        encoded_prompt = urllib.parse.quote(full_prompt)
        url = f"https://image.pollinations.ai/prompt/{encoded_prompt}?width=1280&height=720&seed={seed}&nologo=true&enhance=false"

        try:
            client = httpx.Client(trust_env=False, timeout=35.0)
            resp = client.get(url)
            if resp.status_code == 200 and len(resp.content) > 3000:
                return resp.content
        except Exception as e:
            print(f"[ImageEngine] Pollinations AI request warning: {e}")
        return None

    def _fetch_booru_illustration(self, tags: list) -> bytes:
        try:
            client = httpx.Client(trust_env=False, timeout=12.0, headers=self.headers)
            for t_slice in [tags, tags[:2], ["scenery", "sky"]]:
                query = "+".join(t_slice)
                api_url = f"https://safebooru.org/index.php?page=dapi&s=post&q=index&json=1&tags={query}&limit=20"
                resp = client.get(api_url)
                if resp.status_code == 200:
                    items = resp.json()
                    if items and isinstance(items, list) and len(items) > 0:
                        chosen = random.choice(items)
                        img_url = f"https://safebooru.org/images/{chosen['directory']}/{chosen['image']}"
                        img_resp = client.get(img_url)
                        if img_resp.status_code == 200 and len(img_resp.content) > 10000:
                            return img_resp.content
        except Exception as e:
            print(f"[ImageEngine] Booru fetch warning: {e}")
        return None

    def _extract_anime_tags(self, prompt: str, style_key: str) -> list:
        prompt_lower = prompt.lower()
        tags = []
        if "girl" in prompt_lower or "female" in prompt_lower:
            tags.append("1girl")
        elif "boy" in prompt_lower or "male" in prompt_lower:
            tags.append("1boy")
        tags.extend(["scenery", "dramatic_lighting"])
        return list(dict.fromkeys(tags))[:4]

    def _post_process_3d_depth(self, img_path: str):
        """增强图像的 3D 立体雕刻感与景深高光层次"""
        try:
            bgr = cv2.imread(img_path)
            if bgr is None:
                return

            if (bgr.shape[1], bgr.shape[0]) != (VIDEO_WIDTH, VIDEO_HEIGHT):
                bgr = cv2.resize(bgr, (VIDEO_WIDTH, VIDEO_HEIGHT), interpolation=cv2.INTER_LANCZOS4)

            # 1. CLAHE (局部对比度受限自适应直方图均衡化) 强化表面 3D 立体纹理与光照凹凸感
            lab = cv2.cvtColor(bgr, cv2.COLOR_BGR2LAB)
            l, a, b = cv2.split(lab)
            clahe = cv2.createCLAHE(clipLimit=1.8, tileGridSize=(8, 8))
            cl = clahe.apply(l)
            l_enhanced = cv2.addWeighted(l, 0.4, cl, 0.6, 0)
            lab = cv2.merge((l_enhanced, a, b))
            enhanced = cv2.cvtColor(lab, cv2.COLOR_LAB2BGR)

            # 2. 边缘光晕雕刻 (Unsharp Mask)
            gaussian = cv2.GaussianBlur(enhanced, (0, 0), 2.0)
            unsharp = cv2.addWeighted(enhanced, 1.35, gaussian, -0.35, 0)

            # 3. 电影级暗角 (Vignette) 增强 3D 摄像机向中心聚焦的立体景深
            H, W = VIDEO_HEIGHT, VIDEO_WIDTH
            X = np.linspace(-1, 1, W)
            Y = np.linspace(-1, 1, H)
            mesh_x, mesh_y = np.meshgrid(X, Y)
            radius = np.sqrt(mesh_x**2 + mesh_y**2)
            vignette = 1.0 - 0.25 * np.clip((radius - 0.45) / 0.8, 0, 1)
            vignette_3c = np.repeat(vignette[:, :, np.newaxis], 3, axis=2)

            final_bgr = np.clip(unsharp.astype(np.float32) * vignette_3c, 0, 255).astype(np.uint8)
            cv2.imwrite(img_path, final_bgr, [int(cv2.IMWRITE_JPEG_QUALITY), 96])
        except Exception as e:
            print(f"[ImageEngine] Post-process warning: {e}")

    def _generate_3d_epic_canvas(self, output_path: str, style_key: str):
        """生成次世代 3D 宇宙与山海玄幻空间深度背景"""
        H, W = VIDEO_HEIGHT, VIDEO_WIDTH
        canvas = np.zeros((H, W, 3), dtype=np.uint8)
        # 纵深渐变 (深邃星空到地平线金芒)
        for y in range(H):
            ratio = y / float(H)
            b = int(25 + 40 * ratio)
            g = int(10 + 60 * ratio)
            r = int(15 + 120 * ratio)
            canvas[y, :] = (b, g, r)

        # 3D 空间远景光晕与透视星尘
        cx, cy = W // 2, int(H * 0.6)
        cv2.circle(canvas, (cx, cy), 280, (120, 180, 255), -1)
        canvas = cv2.GaussianBlur(canvas, (101, 101), 0)
        cv2.imwrite(output_path, canvas)
'''

with open(DEST_DIR / "core/config.py", "w", encoding="utf-8") as f:
    f.write(CONFIG_PY)
print("Wrote H:/AnimeStudio/core/config.py")

with open(DEST_DIR / "core/storyboard.py", "w", encoding="utf-8") as f:
    f.write(STORYBOARD_PY)
print("Wrote H:/AnimeStudio/core/storyboard.py")

with open(DEST_DIR / "core/tts_engine.py", "w", encoding="utf-8") as f:
    f.write(TTS_ENGINE_PY)
print("Wrote H:/AnimeStudio/core/tts_engine.py")

with open(DEST_DIR / "core/image_engine.py", "w", encoding="utf-8") as f:
    f.write(IMAGE_ENGINE_PY)
print("Wrote H:/AnimeStudio/core/image_engine.py")
