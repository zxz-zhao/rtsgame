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
    def __init__(self, provider: str = "hybrid"):
        self.provider = provider
        self.headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
            "Referer": "https://safebooru.org/"
        }

    def generate_image(self, prompt: str, style_key: str, output_path: str, seed: int = None) -> str:
        """根据分镜提示词生成 16:9 1080P 超高清二次元动漫画面 (AI + 高品质原画双通道)"""
        if seed is None:
            seed = random.randint(100000, 999999)

        # 通道 1: 尝试从动漫图库匹配超清原画
        tags = self._extract_anime_tags(prompt, style_key)
        print(f"[ImageEngine] Searching anime illustration with tags: {tags}...")
        img_data = self._fetch_booru_illustration(tags)
        if img_data:
            with open(output_path, "wb") as f:
                f.write(img_data)
            self._resize_and_polish(output_path)
            print("[ImageEngine] Successfully rendered high-res anime artwork!")
            return output_path

        # 通道 2: 尝试 Pollinations 云端 AI 通道
        ai_data = self._fetch_pollinations_ai(prompt, style_key, seed)
        if ai_data:
            with open(output_path, "wb") as f:
                f.write(ai_data)
            self._resize_and_polish(output_path)
            print("[ImageEngine] Successfully rendered Pollinations AI artwork!")
            return output_path

        # 通道 3: 兜底星空氛围原画
        print("[ImageEngine] Falling back to atmospheric anime starfield canvas...")
        self._generate_ambient_canvas(output_path, style_key)
        return output_path

    def _extract_anime_tags(self, prompt: str, style_key: str) -> list:
        """从提示词与风格中提取标签"""
        prompt_lower = prompt.lower()
        tags = []

        # 角色标签
        if "girl" in prompt_lower or "female" in prompt_lower or "woman" in prompt_lower:
            tags.append("1girl")
        elif "boy" in prompt_lower or "male" in prompt_lower or "man" in prompt_lower:
            tags.append("1boy")

        # 风格核心标签
        style_tags = {
            "japanese_anime": ["scenery", "sky"],
            "chinese_manhua": ["scenery", "fantasy"],
            "cyberpunk_anime": ["scenery", "cyberpunk"],
            "ghibli": ["scenery", "clouds"],
            "dark_fantasy": ["scenery", "night"]
        }.get(style_key, ["scenery"])

        tags.extend(style_tags)

        # 场景环境词
        if "night" in prompt_lower or "dark" in prompt_lower:
            tags.append("night")
        elif "rain" in prompt_lower:
            tags.append("rain")
        elif "star" in prompt_lower or "meteor" in prompt_lower:
            tags.append("stars")
        elif "city" in prompt_lower or "street" in prompt_lower:
            tags.append("city")

        # 至少保留 2 个标签
        return list(dict.fromkeys(tags))[:4]

    def _fetch_booru_illustration(self, tags: list) -> bytes:
        """通过 Safebooru 免费检索下载 1080P 超高清无码动漫作品"""
        try:
            client = httpx.Client(trust_env=False, timeout=12.0, headers=self.headers)
            # 优先 3-4 标签，若无结果退化为 2 标签
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
            print(f"[ImageEngine] Booru fetch error: {e}")
        return None

    def _fetch_pollinations_ai(self, prompt: str, style_key: str, seed: int) -> bytes:
        """调用 Pollinations AI"""
        try:
            cleaned = re.sub(r'[\u4e00-\u9fa5]', '', prompt)
            cleaned = re.sub(r'[^a-zA-Z0-9,\s\-]', ' ', cleaned)
            words = [w.strip() for w in cleaned.split() if w.strip()][:20]
            clean_prompt = " ".join(words) or "anime scenic background"
            encoded = urllib.parse.quote(f"masterpiece, anime screenshot, {clean_prompt}")
            url = f"https://image.pollinations.ai/prompt/{encoded}"
            client = httpx.Client(trust_env=False, timeout=12.0, headers=self.headers)
            resp = client.get(url)
            if resp.status_code == 200 and len(resp.content) > 5000:
                return resp.content
        except Exception as e:
            print(f"[ImageEngine] Pollinations error: {e}")
        return None

    def _resize_and_polish(self, img_path: str):
        """采用二次元电影级居中裁切并缩放至标准 1920x1080"""
        try:
            with Image.open(img_path) as im:
                im = im.convert("RGB")
                w, h = im.size
                target_ratio = VIDEO_WIDTH / float(VIDEO_HEIGHT)
                current_ratio = w / float(h)

                if current_ratio > target_ratio:
                    # 图片过宽，裁剪两侧
                    new_w = int(h * target_ratio)
                    offset = (w - new_w) // 2
                    im_cropped = im.crop((offset, 0, offset + new_w, h))
                else:
                    # 图片过高，裁剪上下偏中上区域 (黄金视觉中心)
                    new_h = int(w / target_ratio)
                    offset = int((h - new_h) * 0.35)
                    im_cropped = im.crop((0, offset, w, offset + new_h))

                im_resized = im_cropped.resize((VIDEO_WIDTH, VIDEO_HEIGHT), Image.Resampling.LANCZOS)
                im_resized.save(img_path, quality=95)
        except Exception as e:
            print(f"[ImageEngine] Resize warning: {e}")

    def _generate_ambient_canvas(self, output_path: str, style_key: str):
        colors = {
            "japanese_anime": ((15, 23, 42), (59, 130, 246)),
            "chinese_manhua": ((10, 15, 30), (217, 119, 6)),
            "cyberpunk_anime": ((10, 10, 25), (139, 92, 246)),
            "ghibli": ((12, 35, 20), (34, 197, 94)),
            "dark_fantasy": ((15, 10, 20), (120, 20, 40))
        }.get(style_key, ((15, 23, 42), (59, 130, 246)))

        im = Image.new("RGB", (VIDEO_WIDTH, VIDEO_HEIGHT), colors[0])
        draw = ImageDraw.Draw(im)

        for y in range(VIDEO_HEIGHT):
            ratio = y / VIDEO_HEIGHT
            r = int(colors[0][0] * (1 - ratio) + colors[1][0] * ratio)
            g = int(colors[0][1] * (1 - ratio) + colors[1][1] * ratio)
            b = int(colors[0][2] * (1 - ratio) + colors[1][2] * ratio)
            draw.line([(0, y), (VIDEO_WIDTH, y)], fill=(r, g, b))

        rng = random.Random(42)
        for _ in range(150):
            sx = rng.randint(0, VIDEO_WIDTH)
            sy = rng.randint(0, int(VIDEO_HEIGHT * 0.7))
            s_rad = rng.randint(1, 3)
            draw.ellipse([sx, sy, sx + s_rad, sy + s_rad], fill=(255, 255, 255))

        im.save(output_path, quality=95)
