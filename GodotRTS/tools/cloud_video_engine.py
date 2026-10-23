import os
import sys
import time
import json
import base64
import httpx
from pathlib import Path
from typing import Optional, Dict, Any

class CloudVideoEngine:
    """
    【路线 B 核心组件】云端高拟真视频生成引擎适配器
    支持智谱清影 (CogVideoX)、阿里云百炼 (Wan2.1)、海螺 (MiniMax)、可灵 (Kling) 与云端算力实例 (AutoDL)。
    负责将本地生成的 3D 首帧原画 + 动作提示词上传至云端算力，生成真正四肢挥舞、武器劈砍的 1080P 动作视频。
    """

    def __init__(self, config_path: Optional[str] = None):
        self.config_path = config_path or r"H:\AnimeStudio\config_cloud_video.json"
        self.config = self._load_config()

    def _load_config(self) -> Dict[str, Any]:
        default_cfg = {
            "active_provider": "zhipu",  # 默认首选: zhipu (智谱 CogVideoX, 0.5元/条，简单稳定)
            "providers": {
                "zhipu": {
                    "name": "智谱清影 (CogVideoX)",
                    "api_key": os.getenv("ZHIPU_API_KEY", ""),
                    "model": "cogvideox-flash",  # cogvideox-flash / cogvideox-2
                    "endpoint": "https://open.bigmodel.cn/api/paas/v4/videos/generations",
                    "status_endpoint": "https://open.bigmodel.cn/api/paas/v4/async-result/{id}"
                },
                "dashscope": {
                    "name": "阿里云百炼 (Wan2.1 通义万相)",
                    "api_key": os.getenv("DASHSCOPE_API_KEY", ""),
                    "model": "wanx2.1-i2v-plus",
                    "endpoint": "https://dashscope.aliyuncs.com/api/v1/services/aigc/video-generation/generation"
                },
                "minimax": {
                    "name": "海螺 AI (MiniMax Video-01)",
                    "api_key": os.getenv("MINIMAX_API_KEY", ""),
                    "group_id": os.getenv("MINIMAX_GROUP_ID", ""),
                    "endpoint": "https://api.minimax.chat/v1/video_generation"
                },
                "kling": {
                    "name": "快手可灵 (Kling API)",
                    "access_key": os.getenv("KLING_ACCESS_KEY", ""),
                    "secret_key": os.getenv("KLING_SECRET_KEY", ""),
                    "endpoint": "https://api.klingai.com/v1/videos/image2video"
                },
                "autodl": {
                    "name": "云端算力租赁 (AutoDL 4090 运行 Wan2.1 / ComfyUI)",
                    "base_url": "http://127.0.0.1:8188",  # 填入 AutoDL 开放的公网隧道地址
                    "type": "comfyui_webhook"
                }
            }
        }
        if os.path.exists(self.config_path):
            try:
                with open(self.config_path, "r", encoding="utf-8") as f:
                    user_cfg = json.load(f)
                    default_cfg.update(user_cfg)
            except Exception as e:
                print(f"[CloudVideoEngine] Warning: failed to load {self.config_path}: {e}")
        else:
            self._save_config(default_cfg)
        return default_cfg

    def _save_config(self, cfg: Dict[str, Any]):
        try:
            with open(self.config_path, "w", encoding="utf-8") as f:
                json.dump(cfg, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"[CloudVideoEngine] Failed to save config: {e}")

    def update_api_key(self, provider: str, api_key: str):
        """快捷设置云端平台的 API Key"""
        if provider in self.config["providers"]:
            self.config["providers"][provider]["api_key"] = api_key
            self.config["active_provider"] = provider
            self._save_config(self.config)
            print(f"[CloudVideoEngine] 已保存 {provider} 的 API Key，当前激活平台: {provider}")

    def generate_action_video(
        self,
        image_path: str,
        action_prompt: str,
        output_video_path: str,
        provider: Optional[str] = None
    ) -> str:
        """
        核心接口：将首帧 3D 原画与动作提示词发送给云端算力，生成大刀挥舞与真实动作视频
        """
        target_provider = provider or self.config.get("active_provider", "zhipu")
        prov_info = self.config["providers"].get(target_provider, {})
        api_key = prov_info.get("api_key", "").strip()

        print("=" * 60)
        print(f"🎬 【云端算力视频生成】调度启动: {prov_info.get('name', target_provider)}")
        print(f"📌 输入原画: {image_path}")
        print(f"⚡ 动作提示词: {action_prompt}")
        print(f"💾 输出文件: {output_video_path}")
        print("=" * 60)

        # 检查是否配置了有效密钥
        if not api_key:
            print("\n⚠️ 【提示】检测到尚未配置该云端平台的 API Key！")
            print(f"配置文件路径: {self.config_path}")
            print(f"您可以：")
            print(f"1. 登录智谱开放平台 (bigmodel.cn) 或阿里云百炼获取免费体验 Key")
            print(f"2. 调用 engine.update_api_key('{target_provider}', 'your-key') 即可一键启用\n")
            raise ValueError(f"未配置 {target_provider} 的 API Key，请先配置后重试。")

        # 调度对应平台实现
        if target_provider == "zhipu":
            return self._call_zhipu_cogvideo(image_path, action_prompt, output_video_path, prov_info)
        elif target_provider == "dashscope":
            return self._call_dashscope_wan(image_path, action_prompt, output_video_path, prov_info)
        else:
            raise NotImplementedError(f"平台 {target_provider} 的调用逻辑正在对接中。")

    def _call_zhipu_cogvideo(self, image_path: str, action_prompt: str, output_path: str, prov_info: Dict[str, Any]) -> str:
        """调用智谱 CogVideoX (清影) 图生视频接口"""
        api_key = prov_info["api_key"]
        model = prov_info.get("model", "cogvideox-flash")
        endpoint = prov_info["endpoint"]

        # 读取图片转为 base64 data uri
        with open(image_path, "rb") as f:
            b64_data = base64.b64encode(f.read()).decode("utf-8")
        image_url = f"data:image/jpeg;base64,{b64_data}"

        headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json"
        }
        payload = {
            "model": model,
            "prompt": action_prompt,
            "image_url": image_url
        }

        print("[CloudVideo] 正在向智谱开放平台提交图生视频任务...")
        with httpx.Client(timeout=60.0) as client:
            resp = client.post(endpoint, json=payload, headers=headers)
            if resp.status_code != 200:
                raise RuntimeError(f"智谱 API 请求失败 ({resp.status_code}): {resp.text}")
            res_data = resp.json()
            task_id = res_data.get("id")
            if not task_id:
                raise RuntimeError(f"未获取到有效任务 ID: {res_data}")

        print(f"[CloudVideo] 任务已成功提交，任务 ID: {task_id}")
        status_url = f"https://open.bigmodel.cn/api/paas/v4/async-result/{task_id}"

        # 轮询等待视频生成完成（通常 1~2 分钟）
        t0 = time.time()
        video_url = None
        while time.time() - t0 < 300:  # 超时 5 分钟
            time.sleep(5)
            elapsed = int(time.time() - t0)
            try:
                with httpx.Client(timeout=30.0) as client:
                    chk_resp = client.get(status_url, headers=headers)
                    if chk_resp.status_code == 200:
                        chk_data = chk_resp.json()
                        task_status = chk_data.get("task_status", "")
                        print(f"[CloudVideo] 轮询任务状态 ({elapsed}s): {task_status}")
                        if task_status == "SUCCESS":
                            video_results = chk_data.get("video_result", [])
                            if video_results:
                                video_url = video_results[0].get("url")
                                break
                        elif task_status == "FAIL":
                            raise RuntimeError(f"视频生成失败: {chk_data}")
            except Exception as e:
                print(f"[CloudVideo] 轮询中抛出微小异常，重试中: {e}")

        if not video_url:
            raise TimeoutError("云端视频生成超时（已等待 5 分钟）")

        print(f"[CloudVideo] 视频生成成功！正在下载云端视频到本地: {video_url}")
        with httpx.Client(timeout=120.0) as client:
            v_resp = client.get(video_url)
            with open(output_path, "wb") as f:
                f.write(v_resp.content)

        print(f"[CloudVideo] 下载完成，已保存至本地: {output_path} ({os.path.getsize(output_path)/(1024*1024):.2f} MB)")
        return output_path

    def _call_dashscope_wan(self, image_path: str, action_prompt: str, output_path: str, prov_info: Dict[str, Any]) -> str:
        """调用阿里云百炼 (Wan2.1 通义万相) 图生视频接口"""
        api_key = prov_info["api_key"]
        model = prov_info.get("model", "wanx2.1-i2v-plus")
        endpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/video-generation/generation"

        with open(image_path, "rb") as f:
            b64_data = base64.b64encode(f.read()).decode("utf-8")
        img_data_uri = f"data:image/jpeg;base64,{b64_data}"

        headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            "X-DashScope-Async": "enable"
        }
        payload = {
            "model": model,
            "input": {
                "img_url": img_data_uri,
                "prompt": action_prompt
            },
            "parameters": {
                "resolution": "720p",
                "duration": 5
            }
        }

        print("[CloudVideo] 正在向阿里云百炼提交 Wan2.1 图生视频任务...")
        with httpx.Client(timeout=60.0) as client:
            resp = client.post(endpoint, json=payload, headers=headers)
            if resp.status_code != 200:
                raise RuntimeError(f"百炼 API 提交失败 ({resp.status_code}): {resp.text}")
            res_data = resp.json()
            task_id = res_data.get("output", {}).get("task_id")
            if not task_id:
                raise RuntimeError(f"未获取到百炼 task_id: {res_data}")

        print(f"[CloudVideo] 阿里云任务已提交，task_id: {task_id}")
        query_url = f"https://dashscope.aliyuncs.com/api/v1/tasks/{task_id}"

        t0 = time.time()
        video_url = None
        while time.time() - t0 < 360:
            time.sleep(5)
            elapsed = int(time.time() - t0)
            try:
                with httpx.Client(timeout=30.0) as client:
                    q_resp = client.get(query_url, headers={"Authorization": f"Bearer {api_key}"})
                    if q_resp.status_code == 200:
                        q_data = q_resp.json()
                        status = q_data.get("output", {}).get("task_status", "")
                        print(f"[CloudVideo] 百炼任务进度 ({elapsed}s): {status}")
                        if status == "SUCCEEDED":
                            video_url = q_data.get("output", {}).get("video_url")
                            break
                        elif status == "FAILED":
                            raise RuntimeError(f"百炼任务生成失败: {q_data}")
            except Exception as e:
                print(f"[CloudVideo] 百炼查询异常重试中: {e}")

        if not video_url:
            raise TimeoutError("百炼视频生成超时")

        print(f"[CloudVideo] 下载阿里云生成视频: {video_url}")
        with httpx.Client(timeout=120.0) as client:
            v_resp = client.get(video_url)
            with open(output_path, "wb") as f:
                f.write(v_resp.content)

        return output_path

if __name__ == "__main__":
    engine = CloudVideoEngine()
    print("CloudVideoEngine loaded successfully. Active provider:", engine.config.get("active_provider"))
