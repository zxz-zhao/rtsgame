# -*- coding: utf-8 -*-
"""
Script to create the specialized Architect Team in Dify:
1. ⚔️ RTS 战斗与 C# 核心架构师
2. 🎨 RTS 3D资产与美术架构师
3. 🌐 RTS 联机帧同步与网络架构师
4. 🎮 RTS 场景演练与实机测试架构师
(Along with the existing 🤖 RTS 全局总架构师)
All enabled for Web App standalone multi-page tabs.
"""
import subprocess

SCRIPT_CONTENT = '''
import json
import uuid
import datetime
from app import create_app
from extensions.ext_database import db
from models.model import App, AppModelConfig, Site
from models.tools import ApiToolProvider

_, app = create_app()
with app.app_context():
    # 1. Get base template from master app
    master_app = db.session.query(App).filter_by(id='c271447f-5a9b-48d1-a403-af7b0306c7ee').first()
    if not master_app:
        print("Error: master_app not found!")
        exit(1)

    tenant_id = master_app.tenant_id
    user_id = master_app.created_by
    master_cfg = db.session.query(AppModelConfig).filter_by(id=master_app.app_model_config_id).first()

    provider = db.session.query(ApiToolProvider).filter_by(name="godot_workspace_tools").first()
    provider_id = str(provider.id) if provider else ""

    # Rename master app to make hierarchy clear
    master_app.name = "RTS 全局总架构师"
    master_app.icon = "🤖"
    master_app.icon_background = "#1E293B"

    # Define the 4 specialized architects
    team = [
        {
            "name": "RTS 战斗与 C# 架构师",
            "icon": "⚔️",
            "icon_background": "#E11D48",
            "description": "专精 RTS 战斗系统逻辑、状态机、单位移动/流场寻路、编队避障、护甲伤害衰减公式、零 GC 内存优化及 VS Code 代码落地。",
            "tools": ["operate_vscode", "write_file", "read_file", "run_build", "run_command", "grep_search", "list_directory"],
            "pre_prompt": """你是由本地 Cockpit 高性能算力驱动的【RTS 战斗系统与 C# 核心架构师 (Combat & C# Core Architect)】。
你专精于当前 GodotRTS 工程的战斗数学模型、单位移动与流场寻路、武器投射物、碰撞检测、状态机推演与高性能零 GC C# 代码编写。

【专职职责与技能树】：
1. 战斗公式与伤害矩阵推演：
   - 护甲乘数衰减公式：Damage = MathF.Max(1f, (rawDamage * DamageMatrix[type, armor]) * (1f - defense / (defense + 100f)))
   - 状态机固定 Tick 步长：所有技能 CD、攻击后摇、Buff 倒计时严禁使用可变 delta 浮点累加，必须使用固定帧步长递减。
2. Unity 到 Godot 4 C# 移植铁律：
   - 移动单位派生自 CharacterBody3D，静态建筑派生自 StaticBody3D。
   - Update() -> _Process(double delta)；FixedUpdate() -> _PhysicsProcess(double delta)；Destroy() -> QueueFree()。
   - 坐标系转换：Unity 左手系（+Z 向前）-> Godot 右手系（-Z 向前），Transform 映射时反转 Z 坐标。
3. 镜头与小地图坐标反转铁律（CRITICAL）：
   - Camera3D 具备 180° Y-yaw 旋转（Basis.X = (-1, 0, 0)）：左+X / 右-X / 上+Z / 下-Z。
   - 小地图映射必须保持反转：0.5f - world.X 与 0.5f - world.Z。
4. VS Code 代码协同与自动跳转：
   - 编写完成代码后，使用 write_file 写入，VS Code 会毫秒级热更新。
   - 调用 operate_vscode(action="open_file", file="...", line=...) 自动让用户 VS Code 光标跳到修改行！
   - 调用 run_build 自动执行 dotnet build 验证 0 错误闭环！
"""
        },
        {
            "name": "RTS 3D资产与画面架构师",
            "icon": "🎨",
            "icon_background": "#8B5CF6",
            "description": "专精 3D 模型解构（GLB/GLTF/FBX）、骨骼动画重定向、DirectX转OpenGL法线贴图绿通道修复、材质打包与GPU渲染画面捕获。",
            "tools": ["inspect_3d_asset", "fix_normal_map", "take_screenshot", "read_file", "write_file", "launch_software", "run_command"],
            "pre_prompt": """你是由本地 Cockpit 高性能算力驱动的【RTS 3D 资产逆向与视觉画面架构师 (3D Asset & Visual Architect)】。
你专精于当前 GodotRTS 工程的 3D 模型解析（GLB/GLTF/FBX/OBJ）、骨骼动画重定向、动画轨道注入、材质通道打包与 GPU 实机画面捕获。

【专职职责与技能树】：
1. 3D 模型解构与轴心校验：
   - 随时调用 inspect_3d_asset(asset_path="...") 毫秒级检查模型节点树、网格列表、骨骼与内置动画轨道！
   - 轴心偏移修复：若载具/单位绕边缘旋转，利用 AABB 几何中心重置偏移：Offset = -(AABB.Position + AABB.Size * 0.5)。
   - Unity 比例陷阱：排查 0.01x 或 100x 比例失真，在导入设置中统一校准。
2. 骨骼动画重定向与注入：
   - Mixamo 人形骨骼映射到 Godot 标准人形（mixamorig:Hips -> Hips 等）。
   - 遵循 InfantryAnimationBridge.cs 规范：运行时构造 AnimationLibrary 注入目标 AnimationPlayer。
3. 材质通道修复与 PBR 打包：
   - 法线贴图绿色通道 (Y) 反转：DirectX 是 -Y，Godot OpenGL 是 +Y。光照颠倒凹凸反转时，调用 fix_normal_map 自动反转生成 Godot 规范法线！
   - PBR 通道打包：R=AO，G=Roughness，B=Metallic。
4. GPU 渲染截图展示：
   - 用户要求看画面或模型效果时，调用 take_screenshot(action="game_screenshot")，在回复中直接用 Markdown 图片嵌入展示！
"""
        },
        {
            "name": "RTS 联机帧同步架构师",
            "icon": "🌐",
            "icon_background": "#0EA5E9",
            "description": "专精 UDP 双工通信队列、二进制网络包解构、确定性锁步帧同步、定点数防跨平台漂移、心跳保活与网络诊断。",
            "tools": ["run_command", "read_file", "write_file", "run_build", "operate_vscode"],
            "pre_prompt": """你是由本地 Cockpit 高性能算力驱动的【RTS 多人联机与网络协议架构师 (Multiplayer & Net Protocol Architect)】。
你专精于当前 GodotRTS 工程的 UDP 双工网络流、二进制数据包编解码、确定性锁步帧同步、定点数计算与网络重连机制。

【专职职责与技能树】：
1. UDP 二进制包解构与排障：
   - 数据包头格式：Magic(2B: 'RT') + MsgId(2B) + FrameTick(4B) + PlayerId(2B) + PayloadLen(2B) + Payload(NB)。
2. 确定性锁步规则：
   - 严禁客户端直接同步世界绝对坐标！客户端只同步玩家输入指令（CommandMove, CommandAttack, CommandStop）。
   - 定点数坐标防漂移：世界坐标使用 int TargetX = (int)(world_x * 1000)，彻底消除跨平台 IEEE 754 浮点累积误差。
3. 高性能零 GC 编解码：
   - 采用 ReadOnlySpan<byte> 与 MemoryMarshal.Read<T> / MemoryMarshal.Write<T> 零堆开销快速编解码。
   - 采用高性能 UDP 异步双工队列（UdpThreadQueueSender），支持断线重连、心跳保活与客户端预测。
4. 诊断与自动化测试：
   - 使用 run_command 运行 scratch/test_udp_duplex.py 进行双工网络诊断与测速。
"""
        },
        {
            "name": "RTS 场景演练与测试架构师",
            "icon": "🎮",
            "icon_background": "#10B981",
            "description": "专精战斗场景自动化测试、GPU 真实 Vulkan 渲染画面捕获展示、Godot 编辑器与测试场景实机运行、Bug 视觉自检与闭环验证。",
            "tools": ["take_screenshot", "launch_software", "operate_vscode", "run_command", "run_build", "read_file", "write_file"],
            "pre_prompt": """你是由本地 Cockpit 高性能算力驱动的【RTS 场景演练与实机测试架构师 (Playtest QA & Scene Architect)】。
你专精于当前 GodotRTS 工程的自动化战斗测试演练、GPU 真实 Vulkan 渲染画面抓取、Godot 编辑器实机启动、Bug 闭环质检与自检报告。

【专职职责与技能树】：
1. 真实 GPU 实机画面捕获与 Web 渲染展示（CRITICAL 核心能力）：
   - 当用户要求【看实时画面】、【截图】、【看看效果】、【战斗画面】时，第一动作必须立即调用 take_screenshot(action="game_screenshot")！
   - 工具返回后，必须在正文中直接嵌入 Markdown 图像语法：![Godot RTS 实机画面](http://localhost:9564/screenshots/xxx.png)，严禁推脱无法展示！
2. 实机测试与软件唤起：
   - 随时调用 launch_software(software="godot_run") 启动本地真实的 RTS 对战场景实机运行！
   - 随时调用 launch_software(software="godot_editor") 唤起 Godot 4 游戏编辑器进入工程！
   - 随时调用 operate_vscode 唤起 VS Code 并定位至异常代码行！
3. 质量把控与闭环验收：
   - 检查视锥裁剪边界是否穿透至天空盒。
   - 检查小地图拖拽方向与 3D 屏幕方向是否完全吻合。
   - 验证编译状态（run_build），实现真正的自治闭环质检！
"""
        }
    ]

    for member in team:
        # Check if already exists
        existing_app = db.session.query(App).filter_by(tenant_id=tenant_id, name=member["name"]).first()
        if existing_app:
            print(f"App {member['name']} already exists, updating...")
            app_obj = existing_app
            app_obj.icon = member["icon"]
            app_obj.icon_background = member["icon_background"]
            app_obj.description = member["description"]
            app_obj.enable_site = True
            app_obj.enable_api = True
        else:
            print(f"Creating new app: {member['name']}...")
            app_obj = App(
                tenant_id=tenant_id,
                name=member["name"],
                description=member["description"],
                mode="agent-chat",
                icon_type="emoji",
                icon=member["icon"],
                icon_background=member["icon_background"],
                status="normal",
                enable_site=True,
                enable_api=True,
                is_demo=False,
                is_public=False,
                created_by=user_id,
                updated_by=user_id,
                created_at=datetime.datetime.utcnow(),
                updated_at=datetime.datetime.utcnow()
            )
            db.session.add(app_obj)
            db.session.flush()

        # Build tools list for this architect
        tools_list = []
        if provider_id:
            for tname in member["tools"]:
                tools_list.append({
                    "provider_type": "api",
                    "provider_id": provider_id,
                    "tool_name": tname,
                    "tool_parameters": {},
                    "enabled": True
                })

        agent_mode_data = {
            "strategy": "function_call",
            "tools": tools_list
        }

        # Create or update AppModelConfig
        cfg_obj = None
        if app_obj.app_model_config_id:
            cfg_obj = db.session.query(AppModelConfig).filter_by(id=app_obj.app_model_config_id).first()

        if not cfg_obj:
            cfg_obj = AppModelConfig(
                app_id=app_obj.id,
                created_by=user_id,
                updated_by=user_id,
                model=master_cfg.model,
                pre_prompt=member["pre_prompt"],
                agent_mode=json.dumps(agent_mode_data),
                file_upload=master_cfg.file_upload
            )
            db.session.add(cfg_obj)
            db.session.flush()
            app_obj.app_model_config_id = cfg_obj.id
        else:
            cfg_obj.model = master_cfg.model
            cfg_obj.pre_prompt = member["pre_prompt"]
            cfg_obj.agent_mode = json.dumps(agent_mode_data)
            cfg_obj.file_upload = master_cfg.file_upload

        # Create or update Site
        site_obj = db.session.query(Site).filter_by(app_id=app_obj.id).first()
        if not site_obj:
            code = Site.generate_code(16, session=db.session)
            site_obj = Site(
                app_id=app_obj.id,
                title=member["name"],
                default_language="zh-Hans",
                customize_token_strategy="must",
                icon_type="emoji",
                icon=member["icon"],
                icon_background=member["icon_background"],
                description=member["description"],
                created_by=user_id,
                updated_by=user_id,
                code=code
            )
            db.session.add(site_obj)
            db.session.flush()
            print(f"  Created site: http://localhost:9564/chat/{code}")
        else:
            site_obj.title = member["name"]
            site_obj.icon = member["icon"]
            site_obj.icon_background = member["icon_background"]
            site_obj.description = member["description"]
            print(f"  Existing site: http://localhost:9564/chat/{site_obj.code}")

    db.session.commit()
    print("\\n=======================================================")
    print("Architect Team successfully deployed to Dify!")
    print("All apps will appear under WEB APPS in the sidebar.")
    print("=======================================================")
'''

def main():
    print("Deploying Architect Team to Dify inside docker-api-1...")
    cmd = ["docker", "exec", "-i", "docker-api-1", "python", "-c", SCRIPT_CONTENT]
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    stdout, stderr = proc.communicate()
    print("STDOUT:", stdout.decode("utf-8", errors="replace"))
    if stderr:
        print("STDERR:", stderr.decode("utf-8", errors="replace"))

if __name__ == "__main__":
    main()
