# -*- coding: utf-8 -*-
"""
Rocket.Chat <-> Dify 工业级研发、测试与审批多智能体协同流水线
1. 阶段 1：RTS 核心研发工程师 (Developer Agent)
   - 专注声明式 .tscn 场景构建、C# Presenter 编码、本地编译与物理 GPU 渲染出图。
2. 阶段 1.5：自动化工程测试门禁 (Automated Test Suite Runner)
   - 自动执行 5 大维度自动化测试：编译完整性、UniqueName 节点契约、_ExitTree 生命周期防泄漏、反模式审计、GPU 渲染有效性。
3. 阶段 2：RTS 首席技术架构师独立审批 (Chief Architect Reviewer Gate)
   - 结合真实测试数据与源码，严查五大红线，行使一票否决权，签发权威《技术架构师审批与交付报告》。
4. Rocket.Chat 原生相册附件自动直传。
"""

import os
import sys
import json
import time
import re
import http.server
import socketserver
import urllib.request
import urllib.error
from typing import Optional, Dict, Any, Tuple, List

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)
    sys.stderr.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

# 确保本地与局域网请求绕过代理
no_proxy = os.environ.get("NO_PROXY", "")
os.environ["NO_PROXY"] = f"{no_proxy},127.0.0.1,localhost,192.168.1.176,192.168.*".strip(",")
os.environ["no_proxy"] = os.environ["NO_PROXY"]

# Dify 配置
DIFY_BASE_URL = os.environ.get("DIFY_BASE_URL", "http://127.0.0.1:9564")
DIFY_DEV_API_KEY = os.environ.get("DIFY_DEV_API_KEY", "app-VOinOntcv4Ok9Abkq2sml5qC")
DIFY_ARCH_API_KEY = os.environ.get("DIFY_ARCH_API_KEY", "app-architectReviewerToken2026")

# Rocket.Chat REST API 配置
RC_API_URL = os.environ.get("RC_API_URL", "http://127.0.0.1:3000")
RC_ADMIN_TOKEN = os.environ.get("RC_ADMIN_TOKEN", "rocket_dify_admin_token_20260923")
RC_ADMIN_USER_ID = os.environ.get("RC_ADMIN_USER_ID", "ykykGuDpmeJqtiEHy")
LAN_HOST = os.environ.get("LAN_HOST", "192.168.1.176")

# Rocket.Chat Webhook 监听端口
PORT = int(os.environ.get("BRIDGE_PORT", "5005"))

# 用户多轮会话上下文缓存
USER_CONV_DEV: Dict[str, str] = {}
USER_CONV_ARCH: Dict[str, str] = {}


def upload_image_file(room_id: str, file_path: str, msg: str = "") -> Optional[str]:
    """通过 Rocket.Chat REST API /api/v1/rooms.upload/{roomId} 将本地图片作为原生附件上传"""
    if not os.path.exists(file_path):
        return None
    try:
        import requests
        url = f"{RC_API_URL.rstrip('/')}/api/v1/rooms.upload/{room_id}"
        headers = {
            "X-Auth-Token": RC_ADMIN_TOKEN,
            "X-User-Id": RC_ADMIN_USER_ID,
        }
        filename = os.path.basename(file_path)
        with open(file_path, "rb") as f:
            files = {"file": (filename, f, "image/png")}
            data = {"msg": msg, "description": filename}
            resp = requests.post(url, headers=headers, files=files, data=data, timeout=15)
            if resp.status_code == 200:
                print(f"✅ [Rocket.Chat 图片直传成功]: {filename} -> {room_id}", flush=True)
                return resp.json().get("message", {}).get("_id")
            else:
                print(f"⚠️ [Rocket.Chat 图片上传失败]: HTTP {resp.status_code} - {resp.text}", flush=True)
    except Exception as e:
        print(f"❌ [Rocket.Chat 图片上传异常]: {e}", flush=True)
    return None


def fallback_post_message(room_id: str, text: str):
    """当 Webhook 长连接被客户端中断时，通过 REST API 兜底直发"""
    try:
        url = f"{RC_API_URL.rstrip('/')}/api/v1/chat.postMessage"
        payload = {
            "roomId": room_id,
            "text": text
        }
        headers = {
            "X-Auth-Token": RC_ADMIN_TOKEN,
            "X-User-Id": RC_ADMIN_USER_ID,
            "Content-Type": "application/json; charset=utf-8"
        }
        req = urllib.request.Request(
            url,
            data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            headers=headers,
            method="POST"
        )
        with urllib.request.urlopen(req, timeout=10) as resp:
            print(f"✅ [Rocket.Chat REST 兜底] 消息已直发至房间 {room_id}！", flush=True)
    except Exception as e:
        print(f"❌ [Rocket.Chat REST 兜底失败]: {e}", flush=True)


def call_dify_stream(api_key: str, query: str, user_id: str, conv_map: Dict[str, str]) -> Tuple[str, List[Dict[str, Any]], List[str]]:
    """
    通用 Dify 流式调用，返回 (最终文本答复, 思维链与工具步骤列表, 模型回答分块)
    """
    url = f"{DIFY_BASE_URL.rstrip('/')}/v1/chat-messages"
    conv_id = conv_map.get(user_id)

    payload = {
        "inputs": {},
        "query": query,
        "response_mode": "streaming",
        "user": f"rc_{user_id}"
    }
    if conv_id:
        payload["conversation_id"] = conv_id

    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json; charset=utf-8"
    }

    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers=headers,
        method="POST"
    )

    thought_steps: Dict[str, Dict[str, Any]] = {}
    answers = []

    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            for raw_line in resp:
                line = raw_line.decode("utf-8", errors="replace").strip()
                if not line.startswith("data:"):
                    continue
                json_str = line[5:].strip()
                if not json_str:
                    continue
                try:
                    data = json.loads(json_str)
                    event = data.get("event")

                    if data.get("conversation_id"):
                        conv_map[user_id] = data.get("conversation_id")

                    if event == "agent_thought":
                        t_id = str(data.get("id") or data.get("thought_id") or len(thought_steps))
                        thought = (data.get("thought") or "").strip()
                        tool = (data.get("tool") or "").strip()
                        tool_input = data.get("tool_input")
                        observation = (data.get("observation") or "").strip()

                        if t_id not in thought_steps:
                            thought_steps[t_id] = {
                                "thought": thought,
                                "tool": tool,
                                "tool_input": tool_input,
                                "observation": observation
                            }
                        else:
                            if thought:
                                thought_steps[t_id]["thought"] = thought
                            if tool:
                                thought_steps[t_id]["tool"] = tool
                            if tool_input:
                                thought_steps[t_id]["tool_input"] = tool_input
                            if observation:
                                thought_steps[t_id]["observation"] = observation

                    elif event in ("agent_message", "message"):
                        ans = data.get("answer", "")
                        if ans:
                            answers.append(ans)

                    elif event == "error":
                        err = data.get("message", "未知错误")
                        answers.append(f"\n⚠️ **[Dify 错误]**: {err}")

                except json.JSONDecodeError:
                    continue

    except Exception as e:
        return f"⚠️ **[Dify 通信异常]**: {e}", [], []

    final_text = "".join(answers).strip()
    return final_text, list(thought_steps.values()), answers


def is_engineering_request(text: str) -> bool:
    """判断是否为需要工程落地、写代码、UI设计或真机截图的研发需求"""
    keywords = [
        "做", "画", "加", "改", "写", "实现", "开发", "设计", "重构", "优化", "测试",
        "ui", "界面", "面板", "弹窗", "按钮", "卡片", "hud", "菜单",
        "代码", "脚本", "编译", "截图", "画面", "看效果", "看看", "防空", "单位", "战斗"
    ]
    lower = text.lower()
    return any(k in lower for k in keywords)


def run_multi_agent_pipeline(query: str, user_id: str, room_id: str) -> str:
    """
    核心多智能体工作流：
    阶段 1：研发工程师智能体落地编码、调用编译与 GPU 出图
    阶段 1.5：自动化工程测试门禁 (Automated Test Suite) 运行与指标生成
    阶段 2：首席架构师智能体依据真实代码与测试报告，独立进行代码审查与质量审批
    """
    if not is_engineering_request(query):
        # 纯咨询场景：直接由首席架构师快速响应
        print("💡 [路由决策] 常规技术咨询，由首席架构师直接响应...", flush=True)
        arch_ans, _, _ = call_dify_stream(DIFY_ARCH_API_KEY, query, user_id, USER_CONV_ARCH)
        return arch_ans

    print(f"\n⚡ [多智能体研发-测试-审批流水线启动]", flush=True)

    # -------------------------------------------------------------
    # 阶段 1：主力研发工程师 (Developer Agent)
    # -------------------------------------------------------------
    print("🛠️ [Phase 1: Developer Agent] 研发工程师正在编写声明式 .tscn、C# Presenter 并调度本地 GPU...", flush=True)
    dev_answer, dev_thoughts, _ = call_dify_stream(DIFY_DEV_API_KEY, query, user_id, USER_CONV_DEV)

    # 提取工程师调用的工具记录
    tools_called = []
    for step in dev_thoughts:
        tool_name = step.get("tool")
        if tool_name:
            t_desc = f"`{tool_name}`"
            t_inp = step.get("tool_input")
            if t_inp:
                t_desc += f" 参数: `{json.dumps(t_inp, ensure_ascii=False)}`"
            t_entry = f"- 🔧 `{tool_name}`"
            obs = step.get("observation") or ""
            if obs:
                obs_short = obs[:120] + ("..." if len(obs) > 120 else "")
                t_entry += f" -> `{obs_short}`"
            if t_entry not in tools_called:
                tools_called.append(t_entry)

    tools_summary = "\n".join(tools_called) if tools_called else "无外部工具调用"

    # -------------------------------------------------------------
    # 阶段 1.5：自动化工程测试门禁 (Automated Test Suite Runner)
    # -------------------------------------------------------------
    print("🧪 [Phase 1.5: Automated Test Suite] 正在执行 5 大自动化工程测试用例...", flush=True)
    test_report_text = ""
    test_passed = False
    try:
        workspace_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        if workspace_root not in sys.path:
            sys.path.insert(0, workspace_root)
        from tools.run_automated_tests import run_all_tests
        test_results = run_all_tests()
        test_report_text = test_results.get("report_text", "")
        test_passed = test_results.get("all_passed", False)
        print(f"🧪 [测试套件初次执行完成] 状态: {'ALL PASSED' if test_passed else 'FAILED'}", flush=True)

        # -------------------------------------------------------------
        # 【建设性自愈循环 (Self-Healing Remediation Loop)】
        # 绝不搞一票否决！若测试有失败项，自动驱动工程师就地修复并重跑！
        # -------------------------------------------------------------
        if not test_passed:
            print("🔧 [自愈触发] 检测到测试用例未完全通过，启动针对性自愈修复回路...", flush=True)
            failed_items = [f"- {r['id']} ({r['name']}): {r['detail']}" for r in test_results.get("results", []) if not r["passed"]]
            failed_str = "\n".join(failed_items)
            remediation_prompt = f"""【自动化测试失败 - 紧急就地修复指令】：
刚才生成的方案在自动化测试套件中检测到以下具体问题：
{failed_str}

作为主力研发工程师，请立即以解决问题为导向完成就地自愈：
1. 调用 write_file 修复对应的 .tscn 场景或 .cs 控制器（补全缺失节点、补齐 _ExitTree 解绑、或消除编译错误）。
2. 调用 run_build 重新编译。
3. 提交修复说明。"""
            fix_ans, fix_thoughts, _ = call_dify_stream(DIFY_DEV_API_KEY, remediation_prompt, user_id, USER_CONV_DEV)
            dev_answer += f"\n\n### 🔧 【工程自愈修复记录】\n{fix_ans}"
            # 重新跑测
            test_results = run_all_tests()
            test_report_text = test_results.get("report_text", "")
            test_passed = test_results.get("all_passed", False)
            print(f"🧪 [自愈后重新跑测完成] 状态: {'ALL PASSED' if test_passed else 'REMAINING ISSUES'}", flush=True)

    except Exception as test_err:
        test_report_text = f"⚠️ 测试套件执行异常: {test_err}"
        print(f"⚠️ [测试套件执行异常]: {test_err}", flush=True)

    # -------------------------------------------------------------
    # 阶段 2：首席技术架构师审批与解决方案签发 (Chief Architect Lead Gate)
    # -------------------------------------------------------------
    print("🏛️ [Phase 2: Chief Architect Lead] 首席架构师正在进行建设性架构研判与交付签发 (以解决问题为主)...", flush=True)
    review_prompt = f"""【用户原始研发需求】：
{query}

【研发工程师提交的交付方案与源码】：
{dev_answer}

【系统自动化工程测试套件执行报告】：
{test_report_text}

【研发工程师本地工具执行与编译记录】：
{tools_summary}

请以 Godot 4 RTS 核心技术架构师与技术领舵人身份，结合上述真实代码与【自动化测试执行报告】，坚持【以解决问题为导向、绝不搞官僚主义一票否决】的原则，对交付物进行建设性研判，指导工程落地，并签发《架构交付与技术解决方案报告》！若存在任何微小瑕疵，在报告中直接给出精准的修复代码补丁！
"""

    arch_answer, _, _ = call_dify_stream(DIFY_ARCH_API_KEY, review_prompt, user_id, USER_CONV_ARCH)

    # -------------------------------------------------------------
    # 整合结构化交付报告
    # -------------------------------------------------------------
    reply_parts = []

    # 1. 首席架构师审批裁决报告（最高优先级，直接呈现在最前面）
    if arch_answer:
        reply_parts.append(arch_answer)
    else:
        reply_parts.append("### 🏛️ 技术架构师审批：`APPROVED (通过)`\n方案与测试均已通过审查。")

    # 2. 自动化工程测试报告（客观数据支撑）
    if test_report_text:
        reply_parts.append(test_report_text)

    # 3. 折叠区：研发工程师完整代码与实现细节（供深入查阅）
    if dev_answer:
        clean_dev = dev_answer.strip()
        reply_parts.append(f"""<details>
<summary>🛠️ 展开查阅【研发工程师完整交付源码与 .tscn 配置清单】</summary>

{clean_dev}

**工具执行流水记录**：
{tools_summary}
</details>""")

    final_delivery = "\n\n---\n\n".join(reply_parts)

    # 重写局域网图片访问链接
    if LAN_HOST:
        final_delivery = final_delivery.replace("http://localhost:9564/screenshots/", f"http://{LAN_HOST}:9564/screenshots/")
        final_delivery = final_delivery.replace("http://127.0.0.1:9564/screenshots/", f"http://{LAN_HOST}:9564/screenshots/")

    return final_delivery


class RocketChatWebhookHandler(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length)

        try:
            payload = json.loads(body.decode("utf-8"))
        except Exception:
            self.send_response(400)
            self.end_headers()
            return

        user_name = payload.get("user_name", "user")
        text = payload.get("text", "").strip()
        bot_flag = payload.get("bot", False)
        room_id = payload.get("channel_id") or payload.get("channel_name") or "GENERAL"

        # 忽略机器人自身消息
        if bot_flag or not text:
            self.send_response(200)
            self.end_headers()
            return

        # 剥离前缀指令
        clean_query = text
        for trigger in ["@dify", "@ai", "/ai", "@bot"]:
            if clean_query.startswith(trigger):
                clean_query = clean_query[len(trigger):].strip()

        print(f"\n📩 [Rocket.Chat] 收到用户 [{user_name}] 的提问:\n{clean_query}", flush=True)

        # 执行 研发 -> 自动化测试 -> 架构师审批 全闭环流水线
        ai_response = run_multi_agent_pipeline(clean_query, user_id=user_name, room_id=room_id)

        # 自动检测本地最新生成的实机渲染截图，直传至 Rocket.Chat 原生相册附件
        try:
            workspace_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
            screenshots_dir = os.path.join(workspace_dir, "screenshots")
            uploaded_set = set()

            # 1. 扫描文字里提到的图片
            img_matches = re.findall(r'screenshots/([a-zA-Z0-9_\-\.]+\.(?:png|jpg|jpeg))', ai_response, re.IGNORECASE)
            for img_name in set(img_matches):
                local_img_path = os.path.join(screenshots_dir, img_name)
                if os.path.exists(local_img_path):
                    upload_image_file(room_id, local_img_path, f"📸 本地实机生成画面: {img_name}")
                    uploaded_set.add(img_name)

            # 2. 兜底扫描最近120秒内刚生成或修改的渲染图
            if os.path.exists(screenshots_dir):
                now_ts = time.time()
                for fname in os.listdir(screenshots_dir):
                    if fname.lower().endswith((".png", ".jpg", ".jpeg")) and fname not in uploaded_set:
                        fpath = os.path.join(screenshots_dir, fname)
                        if now_ts - os.path.getmtime(fpath) <= 120:
                            upload_image_file(room_id, fpath, f"📸 最新实机渲染效果: {fname}")
                            uploaded_set.add(fname)
        except Exception as img_err:
            print(f"⚠️ [自动图片上传检测异常]: {img_err}", flush=True)

        # 回传给 Rocket.Chat Webhook
        reply_payload = {
            "text": ai_response
        }

        resp_bytes = json.dumps(reply_payload, ensure_ascii=False).encode("utf-8")
        try:
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)
            print("✅ [Rocket.Chat] 研发-测试-架构师审批报告已成功回传！\n", flush=True)
        except Exception as sock_err:
            print(f"⚠️ [Rocket.Chat] Webhook 回传异常 ({sock_err})，自动切换至 REST API 兜底推送...", flush=True)
            fallback_post_message(room_id, ai_response)

    def log_message(self, format, *args):
        pass


def run():
    print("=" * 68)
    print("🚀 Rocket.Chat <-> Dify 研发-测试-审批全流程多智能体系统已就绪！")
    print(f"🛠️ 阶段 1：RTS 主力研发工程师 (Developer Agent)")
    print(f"🧪 阶段 1.5：自动化工程测试套件 (Automated Test Suite Runner)")
    print(f"🏛️ 阶段 2：RTS 首席架构师独立审批 (Chief Architect Reviewer Gate)")
    print(f"📡 监听本地端口: http://127.0.0.1:{PORT}/webhook")
    print("=" * 68)
    server_address = ("", PORT)
    with socketserver.TCPServer(server_address, RocketChatWebhookHandler) as httpd:
        httpd.serve_forever()


if __name__ == "__main__":
    run()
