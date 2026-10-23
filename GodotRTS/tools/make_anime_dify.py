import os
import json
from pathlib import Path

DEST_DIR = Path("H:/AnimeStudio/dify")
DEST_DIR.mkdir(parents=True, exist_ok=True)

# 1. dify_tool_schema.json (OpenAPI 3.0.0 format strictly compatible with Dify)
DIFY_TOOL_SCHEMA = {
    "openapi": "3.0.0",
    "info": {
        "title": "AnimeStudio 3D Video Generator",
        "description": "一键将文章/故事/小说生成 1080P 次世代 3D 电影级动漫漫剧视频 (虚幻引擎5 CG与 3D 空间单应透视投影引擎)",
        "version": "2.0.0"
    },
    "servers": [
        {
            "url": "http://host.docker.internal:8088",
            "description": "Docker 容器访问 Windows 宿主机 3D 动漫生成服务"
        },
        {
            "url": "http://127.0.0.1:8088",
            "description": "本机直连服务"
        }
    ],
    "paths": {
        "/api/v1/generate": {
            "post": {
                "operationId": "generate_anime_video",
                "summary": "一键生成 3D 电影级动漫漫剧视频",
                "description": "输入文章内容，自动拆解 3D 分镜、渲染次世代 3D CG 画面、合成情感配音并进行 3D 空间透视矩阵投影渲染与微粒合成",
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "article": {
                                        "type": "string",
                                        "description": "输入的文章、故事文案或小说片段"
                                    },
                                    "style": {
                                        "type": "string",
                                        "enum": [
                                            "donghua_3d_ue5",
                                            "donghua_3d_action",
                                            "scifi_3d_mech",
                                            "japanese_3d_cg",
                                            "japanese_anime",
                                            "cyberpunk_anime",
                                            "ghibli"
                                        ],
                                        "default": "donghua_3d_ue5",
                                        "description": "动漫画风风格：次世代3D国漫玄幻(donghua_3d_ue5)、3D热血对决(donghua_3d_action)、3D科幻机甲(scifi_3d_mech)、日系3DCG(japanese_3d_cg)、日系新海诚(japanese_anime)、赛博朋克(cyberpunk_anime)、吉卜力治愈(ghibli)"
                                    },
                                    "voice": {
                                        "type": "string",
                                        "enum": ["zh-CN-YunxiNeural", "zh-CN-XiaoxiaoNeural", "zh-CN-YunjianNeural", "zh-CN-YunyangNeural", "zh-CN-XiaoyiNeural"],
                                        "default": "zh-CN-YunxiNeural",
                                        "description": "配音音色：云希热血男主、晓晓灵动女主、云健深沉解说、云扬专业漫评、晓伊知性治愈"
                                    },
                                    "max_scenes": {
                                        "type": "integer",
                                        "default": 4,
                                        "minimum": 2,
                                        "maximum": 8,
                                        "description": "生成分镜镜头数（默认4镜头）"
                                    },
                                    "include_bgm": {
                                        "type": "boolean",
                                        "default": True,
                                        "description": "是否混入 3D 史诗动漫 BGM 背景音乐"
                                    }
                                },
                                "required": ["article"]
                            }
                        }
                    }
                },
                "responses": {
                    "200": {
                        "description": "动漫视频生成成功",
                        "content": {
                            "application/json": {
                                "schema": {
                                    "type": "object",
                                    "properties": {
                                        "success": {"type": "boolean"},
                                        "project_id": {"type": "string"},
                                        "video_url": {"type": "string", "description": "生成的最终 3D 动漫视频相对路径"},
                                        "download_url": {"type": "string", "description": "视频下载链接"},
                                        "duration_seconds": {"type": "number", "description": "视频总时长(秒)"},
                                        "scene_count": {"type": "integer", "description": "分镜数量"}
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

with open(DEST_DIR / "dify_tool_schema.json", "w", encoding="utf-8") as f:
    json.dump(DIFY_TOOL_SCHEMA, f, ensure_ascii=False, indent=2)
print("Wrote dify/dify_tool_schema.json")

# 2. Anime_Video_Workflow.yml (Dify DSL 工作流)
DIFY_WORKFLOW_DSL = r'''app:
  description: 输入一篇文章，全自动转换为 1080P 次世代 3D 电影级动漫漫剧视频
  icon: 🎬
  icon_background: '#FFE8EE'
  mode: workflow
  name: 3D 动漫漫剧一键制片厂
kind: app
version: 0.1.5
workflow:
  conversation_variables: []
  environment_variables: []
  features:
    file_upload:
      enabled: false
    opening_statement: ''
    retriever_resource:
      enabled: false
    sensitive_word_avoidance:
      enabled: false
    speech_to_text:
      enabled: false
    suggested_questions: []
    suggested_questions_after_answer:
      enabled: false
    text_to_speech:
      enabled: false
  graph:
    edges:
      - id: start-to-http
        source: start
        sourceHandle: source
        target: generate_anime_http
        targetHandle: target
      - id: http-to-end
        source: generate_anime_http
        sourceHandle: source
        target: end
        targetHandle: target
    nodes:
      - data:
          desc: ''
          selected: false
          title: 开始
          type: start
          variables:
            - label: 文章内容 (Article)
              max_length: 5000
              options: []
              required: true
              type: paragraph
              variable: article
            - label: 动漫风格 (Style)
              max_length: 50
              options:
                - donghua_3d_ue5
                - donghua_3d_action
                - scifi_3d_mech
                - japanese_3d_cg
                - japanese_anime
                - cyberpunk_anime
                - ghibli
              required: false
              default: donghua_3d_ue5
              type: select
              variable: style
        height: 115
        id: start
        position:
          x: 80
          y: 280
        positionAbsolute:
          x: 80
          y: 280
        selected: false
        sourcePosition: right
        targetPosition: left
        type: custom
        width: 244
      - data:
          authorization:
            config: null
            type: no-auth
          body:
            data:
              - type: text
                value: '{"article": {{#start.article#}}, "style": {{#start.style#}}, "max_scenes": 4, "include_bgm": true}'
            type: json
          desc: 调用本地 3D 动漫渲染引擎 (3D 空间单应透视投影 + 虚幻5 3D CG 原画 + 空间微粒)
          headers: 'Content-Type: application/json'
          method: post
          params: ''
          selected: false
          timeout:
            connect: 60
            read: 300
            write: 60
          title: 3D 动漫视频渲染引擎
          type: http-request
          url: http://host.docker.internal:8088/api/v1/generate
        height: 140
        id: generate_anime_http
        position:
          x: 380
          y: 280
        positionAbsolute:
          x: 380
          y: 280
        selected: false
        sourcePosition: right
        targetPosition: left
        type: custom
        width: 244
      - data:
          desc: ''
          outputs:
            - value_selector:
                - generate_anime_http
                - body
              variable: result
          selected: false
          title: 结束
          type: end
        height: 90
        id: end
        position:
          x: 680
          y: 280
        positionAbsolute:
          x: 680
          y: 280
        selected: false
        sourcePosition: right
        targetPosition: left
        type: custom
        width: 244
'''

with open(DEST_DIR / "Anime_Video_Workflow.yml", "w", encoding="utf-8") as f:
    f.write(DIFY_WORKFLOW_DSL)
print("Wrote dify/Anime_Video_Workflow.yml")
