import os
from pathlib import Path

DEST_DIR = Path("H:/AnimeStudio")

COMPOSER_PY = r'''import os
import sys
import subprocess
import tempfile
from pathlib import Path
from typing import List, Dict, Any
from PIL import Image, ImageDraw, ImageFont
import cv2
import numpy as np

from .config import FFMPEG_PATH, VIDEO_WIDTH, VIDEO_HEIGHT, VIDEO_FPS, BGM_DIR, OUTPUTS_DIR, DEFAULT_FONT_PATH

class VideoComposer:
    def __init__(self, font_path: str = DEFAULT_FONT_PATH):
        self.font_path = font_path if os.path.exists(font_path) else r"C:\Windows\Fonts\msyh.ttc"
        if not os.path.exists(self.font_path):
            self.font_path = r"C:\Windows\Fonts\simhei.ttf"
        self._load_font()

    def _load_font(self):
        try:
            self.font = ImageFont.truetype(self.font_path, 42)
        except Exception:
            self.font = ImageFont.load_default()

    def generate_ken_burns_scene(self, image_path: str, audio_path: str, narration: str, motion: str, duration: float, output_clip_path: str) -> str:
        """为单镜头生成次世代 3D 空间摄像机透视运镜 + 3D 浮空微粒 + 丁达尔光束 + 电影级字幕与配音"""
        total_frames = max(int(duration * VIDEO_FPS), 30)
        
        # 加载基础大图 (BGR 格式用于 OpenCV 高性能 3D 透视矩阵运算)
        base_bgr = cv2.imread(image_path)
        if base_bgr is None:
            raise FileNotFoundError(f"Failed to load image: {image_path}")

        if (base_bgr.shape[1], base_bgr.shape[0]) != (VIDEO_WIDTH, VIDEO_HEIGHT):
            base_bgr = cv2.resize(base_bgr, (VIDEO_WIDTH, VIDEO_HEIGHT), interpolation=cv2.INTER_LANCZOS4)

        img_w, img_h = VIDEO_WIDTH, VIDEO_HEIGHT

        # 初始化镜头专属的 3D 空间体积粒子群 (灵气火星、丁达尔光斑、3D景深碎屑)
        particles = self._init_3d_particles(img_w, img_h, count=65)

        # 启动 ffmpeg 子进程接收 bgr24 视频帧流 (ultrafast 极速编码)
        cmd = [
            FFMPEG_PATH,
            "-y",
            "-f", "rawvideo",
            "-vcodec", "rawvideo",
            "-s", f"{VIDEO_WIDTH}x{VIDEO_HEIGHT}",
            "-pix_fmt", "bgr24",
            "-r", str(VIDEO_FPS),
            "-i", "-",          # 从 stdin 读取 BGR 原始帧
            "-i", audio_path,    # 输入该镜头的旁白配音
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-crf", "20",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-b:a", "192k",
            output_clip_path
        ]

        proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

        # 逐帧推流
        try:
            for frame_idx in range(total_frames):
                progress = frame_idx / float(total_frames - 1) if total_frames > 1 else 0.0
                frame = self._render_3d_frame(base_bgr, motion, progress, narration, particles)
                proc.stdin.write(frame.tobytes())
            proc.stdin.close()
        except BrokenPipeError:
            pass
        except Exception as e:
            if proc.stdin:
                try: proc.stdin.close()
                except Exception: pass
            proc.kill()
            raise e

        proc.wait()
        return output_clip_path

    def _init_3d_particles(self, w: int, h: int, count: int = 65) -> List[Dict[str, Any]]:
        """初始化在摄像机 3D 锥形视锥空间中的粒子群"""
        particles = []
        palette = [
            (80, 210, 255),   # 炽金玄火
            (60, 160, 255),   # 琉璃金芒
            (120, 245, 255),  # 灵气微尘
            (220, 220, 255)   # 空间星屑
        ]
        import random
        for _ in range(count):
            particles.append({
                "x": random.uniform(-w * 0.7, w * 0.7),
                "y": random.uniform(-h * 0.7, h * 0.7),
                "z": random.uniform(0.6, 3.8),
                "speed_z": random.uniform(0.45, 0.95),
                "drift_x": random.uniform(-0.15, 0.15),
                "drift_y": random.uniform(-0.15, 0.15),
                "size": random.uniform(3.5, 9.0),
                "color": random.choice(palette),
                "alpha": random.uniform(0.5, 0.9)
            })
        return particles

    def _render_3d_frame(self, base_bgr: np.ndarray, motion: str, t: float, text: str, particles: List[Dict[str, Any]]) -> np.ndarray:
        """核心 3D 摄像机矩阵投影 + 视差纵深 + 体积粒子 + 丁达尔光束渲染"""
        W, H = VIDEO_WIDTH, VIDEO_HEIGHT

        # 平滑缓动 (Cosine Ease-in-out)
        ease_t = 0.5 - 0.5 * np.cos(t * np.pi)

        # 1. 计算 3D 摄像机内参矩阵 K 与外参变换 (Pitch, Yaw, Roll, Translation)
        fx = W * 1.25
        fy = W * 1.25
        cx = W * 0.5
        cy = H * 0.5
        K = np.array([[fx, 0, cx], [0, fy, cy], [0, 0, 1]], dtype=np.float32)
        K_inv = np.linalg.inv(K)

        # 微物理摄像机手持微晃 (呼吸感 3D 微震)
        handheld_roll = 0.25 * np.sin(t * np.pi * 6.0)
        handheld_pitch = 0.15 * np.cos(t * np.pi * 4.0)

        # 根据 3D 运镜参数解算旋转与纵深位移
        if motion == "dolly_zoom_3d":
            # 希区柯克空间畸变变焦：推近主体同时背景透视扩张
            pitch = np.radians(1.5 * ease_t + handheld_pitch)
            yaw = np.radians(0.0)
            roll = np.radians(handheld_roll)
            tz = -0.22 * ease_t
        elif motion == "dive_push_3d":
            # 俯冲纵深推近：高机位俯冲逼近主体，透视仰俯形变
            pitch = np.radians(4.5 * ease_t + handheld_pitch)
            yaw = np.radians(-2.0 * np.sin(ease_t * np.pi))
            roll = np.radians(0.8 * ease_t + handheld_roll)
            tz = -0.18 * ease_t
        elif motion == "orbit_3d":
            # 3D 弧线环绕视差：左右偏航透视形变，产生强影视差
            pitch = np.radians(1.0 + handheld_pitch)
            yaw = np.radians(-4.5 * (ease_t - 0.5))
            roll = np.radians(1.2 * (ease_t - 0.5) + handheld_roll)
            tz = -0.10 * ease_t
        elif motion == "flythrough_3d":
            # 3D 纵深穿梭飞掠：高速向前推进
            pitch = np.radians(2.0 * ease_t + handheld_pitch)
            yaw = np.radians(1.5 * ease_t)
            roll = np.radians(handheld_roll)
            tz = -0.32 * ease_t
        elif motion == "crane_rise_3d":
            # 3D 摇臂升镜：垂直升空并俯视
            pitch = np.radians(-4.0 * ease_t + handheld_pitch)
            yaw = np.radians(0.0)
            roll = np.radians(handheld_roll)
            tz = -0.12 * ease_t
        elif motion == "parallax_left_3d":
            # 3D 深度视差左平移：带透视偏航的滑轨
            pitch = np.radians(handheld_pitch)
            yaw = np.radians(3.5 * ease_t)
            roll = np.radians(-0.6 * ease_t + handheld_roll)
            tz = -0.08 * ease_t
        elif motion == "parallax_right_3d":
            # 3D 深度视差右平移：带透视偏航的滑轨
            pitch = np.radians(handheld_pitch)
            yaw = np.radians(-3.5 * ease_t)
            roll = np.radians(0.6 * ease_t + handheld_roll)
            tz = -0.08 * ease_t
        else:
            # 容错与默认：温和 3D 推镜
            pitch = np.radians(2.0 * ease_t + handheld_pitch)
            yaw = np.radians(-1.5 * ease_t)
            roll = np.radians(handheld_roll)
            tz = -0.15 * ease_t

        # 3D 旋转矩阵计算
        Rx = np.array([[1, 0, 0], [0, np.cos(pitch), -np.sin(pitch)], [0, np.sin(pitch), np.cos(pitch)]], dtype=np.float32)
        Ry = np.array([[np.cos(yaw), 0, np.sin(yaw)], [0, 1, 0], [-np.sin(yaw), 0, np.cos(yaw)]], dtype=np.float32)
        Rz = np.array([[np.cos(roll), -np.sin(roll), 0], [np.sin(roll), np.cos(roll), 0], [0, 0, 1]], dtype=np.float32)
        R = Rz @ Ry @ Rx

        T = np.array([[0], [0], [tz]], dtype=np.float32)
        normal = np.array([0, 0, 1], dtype=np.float32).reshape(1, 3)

        # 单应透视单射矩阵 H = K * (R + T * n^T) * K^-1
        H_mat = K @ (R + T @ normal) @ K_inv
        H_mat = H_mat / H_mat[2, 2]

        # 2. 执行 OpenCV 极速 3D 空间单应透视投影形变
        warped = cv2.warpPerspective(base_bgr, H_mat, (W, H), borderMode=cv2.BORDER_REFLECT_101)

        # 3. 渲染三维空间立体漂移粒子 (随 Z 轴景深飞掠)
        p_overlay = np.zeros_like(warped)
        for p in particles:
            cur_z = p["z"] - p["speed_z"] * ease_t
            if cur_z < 0.25:
                cur_z += 3.2
            
            cur_x = p["x"] + p["drift_x"] * W * ease_t
            cur_y = p["y"] + p["drift_y"] * H * ease_t

            proj_x = int(cx + (cur_x / cur_z))
            proj_y = int(cy + (cur_y / cur_z))
            r = max(1, int(p["size"] / cur_z))

            if 0 <= proj_x < W and 0 <= proj_y < H:
                # 绘制立体多层光斑与内芯
                cv2.circle(p_overlay, (proj_x, proj_y), r * 2, p["color"], -1)
                cv2.circle(p_overlay, (proj_x, proj_y), max(1, r // 2), (255, 255, 255), -1)

        # 粒子柔光高斯光晕融合
        p_glow = cv2.GaussianBlur(p_overlay, (15, 15), 0)
        warped = cv2.addWeighted(warped, 1.0, p_glow, 0.65, 0)
        warped = cv2.addWeighted(warped, 1.0, p_overlay, 0.85, 0)

        # 4. 丁达尔体积光束动态扫描 (3D Volumetric Light Sweep)
        sweep_center_x = int(W * (0.15 + 0.70 * ease_t))
        col_dist = np.abs(np.arange(W) - sweep_center_x)
        beam_profile = np.exp(-0.5 * (col_dist / 170.0) ** 2)
        sweep_mask = np.tile(beam_profile, (H, 1))

        # 金白偏暖色丁达尔体积光
        sweep_bgr = np.zeros_like(warped, dtype=np.float32)
        sweep_bgr[:, :, 0] = 30 * sweep_mask  # B
        sweep_bgr[:, :, 1] = 60 * sweep_mask  # G
        sweep_bgr[:, :, 2] = 95 * sweep_mask  # R
        warped = np.clip(warped.astype(np.float32) + sweep_bgr, 0, 255).astype(np.uint8)

        # 5. 叠加电影级高燃金色描边字幕
        if text:
            warped = self._draw_subtitles_cv2(warped, text)

        return warped

    def _draw_subtitles_cv2(self, bgr_frame: np.ndarray, text: str) -> np.ndarray:
        """利用 PIL 高清字体与艺术特效将字幕烧录至 BGR 视频帧中"""
        rgb_frame = cv2.cvtColor(bgr_frame, cv2.COLOR_BGR2RGB)
        pil_img = Image.fromarray(rgb_frame)
        draw = ImageDraw.Draw(pil_img)

        # 拆分为不超过 18 个字符的行
        lines = []
        max_chars = 18
        words = list(text)
        current = ""
        for char in words:
            current += char
            if len(current) >= max_chars:
                lines.append(current)
                current = ""
        if current:
            lines.append(current)

        line_height = 58
        total_text_h = len(lines) * line_height
        start_y = VIDEO_HEIGHT - 110 - total_text_h

        for idx, line in enumerate(lines):
            bbox = draw.textbbox((0, 0), line, font=self.font)
            line_w = bbox[2] - bbox[0]
            x = (VIDEO_WIDTH - line_w) // 2
            y = start_y + idx * line_height

            # 半透明圆角磨砂深黑背景条
            pad_x = 28
            pad_y = 6
            bg_rect = [x - pad_x, y - pad_y, x + line_w + pad_x, y + line_height - 6 + pad_y]
            draw.rounded_rectangle(bg_rect, radius=14, fill=(0, 0, 0, 175))

            # 4 方向黑色深度描边
            stroke = 3
            for ox in range(-stroke, stroke + 1):
                for oy in range(-stroke, stroke + 1):
                    if ox != 0 or oy != 0:
                        draw.text((x + ox, y + oy), line, font=self.font, fill=(0, 0, 0, 255))
            
            # 主体电影级耀眼白金色字幕
            draw.text((x, y), line, font=self.font, fill=(255, 252, 235, 255))

        out_bgr = cv2.cvtColor(np.array(pil_img), cv2.COLOR_RGB2BGR)
        return out_bgr

    def concat_and_mix_bgm(self, clip_paths: List[str], final_output_path: str, bgm_path: str = None) -> str:
        """将多个分镜片段无缝合并，并混入次世代 3D 动漫史诗背景音乐"""
        if not clip_paths:
            raise ValueError("No clips to concatenate")

        if len(clip_paths) == 1 and not bgm_path:
            import shutil
            shutil.copy(clip_paths[0], final_output_path)
            return final_output_path

        concat_list_file = tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False, encoding="utf-8")
        try:
            for cp in clip_paths:
                clean_path = str(Path(cp).resolve()).replace("\\", "/")
                concat_list_file.write(f"file '{clean_path}'\n")
            concat_list_file.close()

            if bgm_path and os.path.exists(bgm_path):
                clean_bgm = str(Path(bgm_path).resolve()).replace("\\", "/")
                cmd = [
                    FFMPEG_PATH,
                    "-y",
                    "-f", "concat",
                    "-safe", "0",
                    "-i", concat_list_file.name,
                    "-stream_loop", "-1",
                    "-i", clean_bgm,
                    "-filter_complex",
                    "[1:a]volume=0.18[bgm];[0:a][bgm]amix=inputs=2:duration=first:dropout_transition=2[aout]",
                    "-map", "0:v",
                    "-map", "[aout]",
                    "-c:v", "copy",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    final_output_path
                ]
            else:
                cmd = [
                    FFMPEG_PATH,
                    "-y",
                    "-f", "concat",
                    "-safe", "0",
                    "-i", concat_list_file.name,
                    "-c", "copy",
                    final_output_path
                ]

            subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        finally:
            if os.path.exists(concat_list_file.name):
                try: os.remove(concat_list_file.name)
                except Exception: pass

        return final_output_path
'''

with open(DEST_DIR / "core/video_composer.py", "w", encoding="utf-8") as f:
    f.write(COMPOSER_PY)
print("Updated H:/AnimeStudio/core/video_composer.py")
