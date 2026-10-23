import os
from pathlib import Path

DEST_DIR = Path("H:/AnimeStudio")

SERVER_PY = r'''import os
import sys
import uuid
import time
import shutil
from pathlib import Path
from typing import List, Optional
from pydantic import BaseModel, Field

import uvicorn
from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse, JSONResponse

from core.config import (
    SERVER_HOST, SERVER_PORT, ANIME_STYLES, VOICE_PRESETS,
    OUTPUTS_DIR, BGM_DIR, STATIC_DIR, BASE_DIR
)
from core.storyboard import StoryboardEngine
from core.tts_engine import TTSEngine
from core.image_engine import AnimeImageEngine
from core.video_composer import VideoComposer

app = FastAPI(
    title="Anime Studio 3D - 次世代 3D 动漫漫剧一键制片厂",
    description="支持一键输入文章直接生成 1080P 电影级 3D 空间透视漫剧动漫视频，支持 Dify 智能体调度",
    version="2.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

storyboard_engine = StoryboardEngine()
tts_engine = TTSEngine()
image_engine = AnimeImageEngine()
video_composer = VideoComposer()

# 挂载静态输出目录
app.mount("/outputs", StaticFiles(directory=str(OUTPUTS_DIR)), name="outputs")

# 请求与响应模型
class GenerateRequest(BaseModel):
    article: str = Field(..., description="输入的文章、小说章节或故事脚本文本", example="苍穹之上，青莲地心火与骨冷幽火在少年掌心剧烈盘旋。萧炎双眸如电，晶莹璀璨的佛怒火莲瞬间成型！")
    style: str = Field(default="donghua_3d_ue5", description="动漫视觉风格 (donghua_3d_ue5, donghua_3d_action, scifi_3d_mech, japanese_3d_cg, japanese_anime, cyberpunk_anime, ghibli)")
    voice: str = Field(default="zh-CN-YunxiNeural", description="旁白音色 (zh-CN-YunxiNeural, zh-CN-XiaoxiaoNeural, zh-CN-YunjianNeural)")
    max_scenes: int = Field(default=4, description="最大分镜数量 (3-8)", ge=2, le=12)
    include_bgm: bool = Field(default=True, description="是否混入 3D 史诗动漫背景音乐")

class SceneItem(BaseModel):
    scene_id: int
    narration: str
    prompt: str
    motion: str
    image_url: Optional[str] = None
    audio_url: Optional[str] = None
    duration: Optional[float] = None

class ComposeRequest(BaseModel):
    scenes: List[SceneItem]
    include_bgm: bool = True

@app.get("/api/v1/config")
def get_config():
    """获取系统支持的 3D 动漫风格与配音预设"""
    return {
        "styles": ANIME_STYLES,
        "voices": VOICE_PRESETS
    }

@app.post("/api/v1/storyboard")
def generate_storyboard(req: GenerateRequest):
    """仅拆解 3D 分镜剧本（供预览或 Dify 工作流第一阶段调用）"""
    if not req.article.strip():
        raise HTTPException(status_code=400, detail="Article text cannot be empty")
    scenes = storyboard_engine.generate(req.article, style_key=req.style, max_scenes=req.max_scenes)
    return {"success": True, "scenes": scenes}

@app.post("/api/v1/generate")
def generate_anime_video(req: GenerateRequest):
    """【核心端点】输入文章，全自动生成完整 1080P 3D 电影级动漫视频（支持直接在 Dify 中作为工具调用）"""
    article_text = req.article.strip()
    if not article_text:
        raise HTTPException(status_code=400, detail="Article text cannot be empty")

    project_id = f"anime_3d_{int(time.time())}_{uuid.uuid4().hex[:6]}"
    project_dir = OUTPUTS_DIR / project_id
    project_dir.mkdir(parents=True, exist_ok=True)

    print(f"[{project_id}] 1. Starting 3D storyboard extraction...")
    scenes = storyboard_engine.generate(article_text, style_key=req.style, max_scenes=req.max_scenes)
    if not scenes:
        raise HTTPException(status_code=500, detail="Failed to generate storyboard scenes")

    clip_paths = []
    total_duration = 0.0

    print(f"[{project_id}] 2. Rendering {len(scenes)} scenes in 3D perspective pipeline...")
    for idx, sc in enumerate(scenes):
        s_id = sc["scene_id"]
        narration = sc["narration"]
        prompt = sc["prompt"]
        motion = sc.get("motion", "dive_push_3d")

        img_path = str(project_dir / f"scene_{s_id:02d}.jpg")
        audio_path = str(project_dir / f"scene_{s_id:02d}.mp3")
        clip_path = str(project_dir / f"scene_{s_id:02d}.mp4")

        # 生成 3D 次世代画面
        try:
            image_engine.generate_image(prompt, req.style, img_path)
            sc["image_url"] = f"/outputs/{project_id}/scene_{s_id:02d}.jpg"
        except Exception as e:
            print(f"[{project_id}] Image gen error on scene {s_id}: {e}")
            raise HTTPException(status_code=500, detail=f"Image generation failed on scene {s_id}")

        # 生成配音并测量时长
        try:
            duration = tts_engine.generate_speech(narration, req.voice, audio_path)
            sc["audio_url"] = f"/outputs/{project_id}/scene_{s_id:02d}.mp3"
            sc["duration"] = round(duration, 2)
            total_duration += duration
        except Exception as e:
            print(f"[{project_id}] TTS error on scene {s_id}: {e}")
            raise HTTPException(status_code=500, detail=f"TTS generation failed on scene {s_id}")

        # 生成该镜头的 3D 透视运镜与体积粒子视频
        try:
            video_composer.generate_ken_burns_scene(img_path, audio_path, narration, motion, duration, clip_path)
            clip_paths.append(clip_path)
        except Exception as e:
            print(f"[{project_id}] Composer error on scene {s_id}: {e}")
            raise HTTPException(status_code=500, detail=f"Scene 3D motion rendering failed on scene {s_id}")

    # 合并片段与混音
    print(f"[{project_id}] 3. Concatenating and mixing 3D BGM...")
    final_video_path = str(OUTPUTS_DIR / f"{project_id}_final.mp4")
    bgm_file = str(BGM_DIR / "anime_calm.wav") if req.include_bgm else None

    try:
        video_composer.concat_and_mix_bgm(clip_paths, final_video_path, bgm_file)
    except Exception as e:
        print(f"[{project_id}] Concat error: {e}")
        raise HTTPException(status_code=500, detail="Final video concatenation failed")

    video_url = f"/outputs/{project_id}_final.mp4"
    return {
        "success": True,
        "project_id": project_id,
        "video_url": video_url,
        "duration_seconds": round(total_duration, 1),
        "scene_count": len(scenes),
        "scenes": scenes,
        "download_url": video_url
    }

@app.get("/api/v1/projects")
def list_projects():
    """获取已生成的动漫视频历史列表"""
    projects = []
    for f in OUTPUTS_DIR.glob("*_final.mp4"):
        stat = f.stat()
        projects.append({
            "name": f.name,
            "url": f"/outputs/{f.name}",
            "size_mb": round(stat.st_size / (1024 * 1024), 2),
            "created_time": time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(stat.st_mtime))
        })
    projects.sort(key=lambda x: x["created_time"], reverse=True)
    return {"projects": projects}

# 静态首页挂载
@app.get("/")
def index():
    return FileResponse(str(STATIC_DIR / "index.html"))

app.mount("/", StaticFiles(directory=str(STATIC_DIR)), name="static")

if __name__ == "__main__":
    print(f"Starting Anime Studio 3D on http://{SERVER_HOST}:{SERVER_PORT} ...")
    uvicorn.run("server:app", host=SERVER_HOST, port=SERVER_PORT, reload=False)
'''

with open(DEST_DIR / "server.py", "w", encoding="utf-8") as f:
    f.write(SERVER_PY)
print("Updated H:/AnimeStudio/server.py")
