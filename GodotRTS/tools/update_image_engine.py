import os
import re
import random
import urllib.parse
import httpx
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

from .config import ANIME_STYLES, VIDEO_WIDTH, VIDEO_HEIGHT

class AnimeImageEngine:
    def __init__(self, provider: str = "pollinations"):
        self.provider = provider

    def generate_image(self, prompt: str, style_key: str, output_path: str, seed: int = None) -> str:
        """根据提示词生成 16:9 高清动漫分镜画面 (多重冗余重试 + 纯净二次元关键词提炼)"""
        if seed is None:
            seed = random.randint(100000, 999999)

        style_info = ANIME_STYLES.get(style_key, ANIME_STYLES["japanese_anime"])
        
        # 提炼纯净英文二次元提示词 (过滤中文字符和特殊标点，防止 URL 畸形或远端报错)
        cleaned_user_prompt = re.sub(r'[\u4e00-\u9fa5]', '', prompt)
        cleaned_user_prompt = re.sub(r'[^a-zA-Z0-9,\s\-]', ' ', cleaned_user_prompt)
        words = [w.strip() for w in cleaned_user_prompt.split() if w.strip()]
        concise_user = " ".join(words[:25]) if words else "anime boy scenic landscape"

        # 组合风格关键词
        style_core = {
            "japanese_anime": "Makoto Shinkai style, vibrant anime scenery, cinematic lighting",
            "chinese_manhua": "3D Chinese donghua style, mystical fantasy aura, dynamic",
            "cyberpunk_anime": "cyberpunk anime style, neon city night, futuristic",
            "ghibli": "Studio Ghibli style, lush greenery, nostalgic anime",
            "dark_fantasy": "dark fantasy anime, dramatic battle scene, high contrast"
        }.get(style_key, "masterpiece anime screenshot")

        final_prompt = f"masterpiece, {style_core}, {concise_user}, ultra-detailed, 8k"

        print(f"[ImageEngine] Cleaned prompt: {final_prompt[:80]}... (seed: {seed})")

        # 尝试通过 Pollinations 生成
        for attempt in range(3):
            try:
                cur_seed = seed + attempt * 77
                encoded = urllib.parse.quote(final_prompt)
                url = f"https://image.pollinations.ai/prompt/{encoded}?width=1024&height=576&seed={cur_seed}&nologo=true&enhance=false"
                client = httpx.Client(trust_env=False, timeout=25.0)
                resp = client.get(url)
                if resp.status_code == 200 and len(resp.content) > 2000:
                    with open(output_path, "wb") as f:
                        f.write(resp.content)
                    self._resize_and_polish(output_path)
                    print(f"[ImageEngine] Successfully fetched image (attempt {attempt+1})")
                    return output_path
                else:
                    print(f"[ImageEngine] Attempt {attempt+1} got status {resp.status_code}, retrying...")
            except Exception as ex:
                print(f"[ImageEngine] Attempt {attempt+1} network error: {ex}")

        # 若云端网络极端偶发异常，生成唯美二次元氛围渐变画卷 (保证 100% 不崩溃)
        print("[ImageEngine] Generating atmospheric anime fallback canvas...")
        self._generate_ambient_canvas(output_path, style_key)
        return output_path

    def _resize_and_polish(self, img_path: str):
        try:
            with Image.open(img_path) as im:
                im = im.convert("RGB")
                if im.size != (VIDEO_WIDTH, VIDEO_HEIGHT):
                    im_resized = im.resize((VIDEO_WIDTH, VIDEO_HEIGHT), Image.Resampling.LANCZOS)
                    im_resized.save(img_path, quality=95)
        except Exception as e:
            print(f"[ImageEngine] Resize warning: {e}")

    def _generate_ambient_canvas(self, output_path: str, style_key: str):
        """生成唯美二次元暗黑/梦幻星空质感背景作为兜底"""
        colors = {
            "japanese_anime": ((15, 23, 42), (59, 130, 246), (236, 72, 153)),
            "chinese_manhua": ((10, 15, 30), (217, 119, 6), (239, 68, 68)),
            "cyberpunk_anime": ((10, 10, 25), (139, 92, 246), (6, 182, 212)),
            "ghibli": ((12, 35, 20), (34, 197, 94), (234, 179, 8)),
            "dark_fantasy": ((15, 10, 20), (120, 20, 40), (220, 38, 38))
        }.get(style_key, ((15, 23, 42), (59, 130, 246), (236, 72, 153)))

        im = Image.new("RGB", (VIDEO_WIDTH, VIDEO_HEIGHT), colors[0])
        draw = ImageDraw.Draw(im)

        # 渐变星空与光晕
        for y in range(VIDEO_HEIGHT):
            ratio = y / VIDEO_HEIGHT
            r = int(colors[0][0] * (1 - ratio) + colors[1][0] * ratio)
            g = int(colors[0][1] * (1 - ratio) + colors[1][1] * ratio)
            b = int(colors[0][2] * (1 - ratio) + colors[1][2] * ratio)
            draw.line([(0, y), (VIDEO_WIDTH, y)], fill=(r, g, b))

        # 随机点缀二次元星辰
        rng = random.Random(42)
        for _ in range(120):
            sx = rng.randint(0, VIDEO_WIDTH)
            sy = rng.randint(0, int(VIDEO_HEIGHT * 0.7))
            s_rad = rng.randint(1, 3)
            s_alpha = rng.randint(140, 255)
            draw.ellipse([sx, sy, sx + s_rad, sy + s_rad], fill=(s_alpha, s_alpha, 255))

        im.save(output_path, quality=95)
