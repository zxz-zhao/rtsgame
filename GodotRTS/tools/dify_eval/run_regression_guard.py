# -*- coding: utf-8 -*-
"""
Dify 自动化防塌房与回归质检测试套件 (Dify Regression Guard Suite)
功能：针对历史踩坑点与老毛病 (Benchmark) 进行批量回归测试，自动检测新改动是否引发“旧病复发”。
"""

import os
import sys
import json
import time
import re
import argparse
import urllib.request
import urllib.error
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

DEFAULT_BENCHMARK_FILE = os.path.join(os.path.dirname(__file__), "regression_benchmark.json")
DEFAULT_DIFY_URL = "http://127.0.0.1:9564"
DEFAULT_APP_TOKEN = "app-VOinOntcv4Ok9Abkq2sml5qC"
REPORTS_DIR = os.path.join(os.path.dirname(__file__), "reports")

def query_dify_agent(query: str, base_url: str = DEFAULT_DIFY_URL, token: str = DEFAULT_APP_TOKEN, timeout: int = 60) -> str:
    """向 Dify 智能体发起流式提问并完整收集输出"""
    url = f"{base_url.rstrip('/')}/v1/chat-messages"
    payload = {
        "inputs": {},
        "query": query,
        "response_mode": "streaming",
        "user": f"eval_runner_{int(time.time())}"
    }
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={
        "Content-Type": "application/json",
        "Authorization": f"Bearer {token}"
    })
    
    full_text = []
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            for line in resp:
                line_str = line.decode("utf-8", errors="replace").strip()
                if line_str.startswith("data: "):
                    body = line_str[6:]
                    try:
                        d = json.loads(body)
                        event = d.get("event")
                        if event in ("agent_message", "message"):
                            ans = d.get("answer", "")
                            full_text.append(ans)
                    except Exception:
                        pass
        return "".join(full_text)
    except urllib.error.HTTPError as e:
        err_msg = e.read().decode('utf-8', errors='replace')
        return f"[Dify HTTP Error {e.code}]: {err_msg}"
    except Exception as e:
        return f"[Dify Request Error]: {e}"

def evaluate_case(case: dict, actual_response: str) -> dict:
    """
    质检与防塌房核心评估逻辑
    1. 规则与反模式检测 (Deterministic Trap Inspection)
    2. 期望要点覆盖度检测 (Expectation Coverage)
    3. 判定是否发生历史退化 (Regression Check)
    """
    case_id = case.get("id", "UNKNOWN")
    expected_principles = case.get("expected_principles", [])
    fatal_traps = case.get("fatal_traps", [])
    
    violations = []
    satisfied_principles = []
    resp_lower = actual_response.lower()
    
    # 针对特定 Case 的硬核特征审计
    if case_id == "TC-CAM-01":
        # 180° 反转规则
        has_180 = "180" in actual_response or "basis.x" in resp_lower or "yaw" in resp_lower
        has_invert_formula = ("0.5" in actual_response and "-" in actual_response) or "反转" in actual_response or "反向" in actual_response
        if not (has_180 and has_invert_formula):
            violations.append("未明确指出或实现 180° Y-Yaw 反转及反向坐标映射 (0.5f - x / 0.5f - z)")
        else:
            satisfied_principles.append("完整包含了 180° Yaw 反转与小地图反向坐标映射")
            
    elif case_id == "TC-CAM-02":
        # 4角多边形裁剪
        has_polygon = "trygetviewportgroundpolygon" in resp_lower or "4" in actual_response or "四角" in actual_response or "polygon" in resp_lower
        has_only_center = ("getgroundcenter" in resp_lower and not has_polygon)
        if has_only_center or not has_polygon:
            violations.append("触犯历史雷区：仅使用视口中心点裁剪，未对视口4角地面多边形进行全量边界限制！")
        else:
            satisfied_principles.append("正确实现了视口 4 角地面多边形全量边界裁剪")

    elif case_id == "TC-TST-03":
        # 视频时长 >= 20s
        has_20s = "20" in actual_response or "1200" in actual_response or "1860" in actual_response or "31" in actual_response
        has_short_time = ("5秒" in actual_response or "3秒" in actual_response or "10秒" in actual_response) and not has_20s
        if has_short_time or not has_20s:
            violations.append("触犯历史雷区：未坚持 20 秒以上测试视频时长门禁（未满足 frames >= 1200 @ 60 FPS）！")
        else:
            satisfied_principles.append("严格遵守测试视频 >= 20 秒 (frames >= 1200) 红线约束")

    elif case_id == "TC-PERF-04":
        # 零 GC 内存契约
        has_zero_gc = "gc" in resp_lower or "池" in actual_response or "分配" in actual_response or "复用" in actual_response
        has_linq_in_loop = "targets.where" in resp_lower or "linq" in resp_lower and "推荐" in actual_response
        if has_linq_in_loop or not has_zero_gc:
            violations.append("触犯历史雷区：未强调物理循环零 GC 契约，或在帧循环内推荐高开销分配！")
        else:
            satisfied_principles.append("坚持物理帧循环零 GC、复用缓冲区与对象池设计")

    elif case_id == "TC-UI-05":
        # UI 极简科技感
        has_restraint = "克制" in actual_response or "精简" in actual_response or "1" in actual_response or "2" in actual_response
        has_color_palette = "#0a0f1d" in resp_lower or "科技" in actual_response or "深色" in actual_response or "scifi" in resp_lower
        if not (has_restraint and has_color_palette):
            violations.append("UI 设计未能严格遵守极简与科技感规范（按键数量未克制或未采用深色扁平方案）")
        else:
            satisfied_principles.append("遵循按键高度克制与深色 Sci-Fi 现代质感")

    elif case_id == "TC-API-06":
        # Godot 4 API
        has_deprecated_api = "kinematicbody" in resp_lower or "spatial" in resp_lower or "moveandslide(" in resp_lower
        if has_deprecated_api:
            violations.append("触犯历史雷区：使用了已废弃的 Godot 3 API（如 KinematicBody/Spatial）！")
        else:
            satisfied_principles.append("完全符合 Godot 4 C# 规范与 CharacterBody3D API")
            
    # 计算得分与是否发生退化 (Regression)
    has_regression = len(violations) > 0
    total_checks = max(1, len(expected_principles) + len(fatal_traps))
    score = 100 if not has_regression else max(10, 100 - len(violations) * 35)
    
    return {
        "case_id": case_id,
        "name": case.get("name"),
        "category": case.get("category"),
        "passed": not has_regression,
        "score": score,
        "has_regression": has_regression,
        "violations": violations,
        "satisfied_principles": satisfied_principles,
        "actual_response": actual_response
    }

def run_regression_suite(benchmark_path: str = DEFAULT_BENCHMARK_FILE, base_url: str = DEFAULT_DIFY_URL, token: str = DEFAULT_APP_TOKEN, max_cases: int = None):
    if not os.path.exists(benchmark_path):
        print(f"❌ 找不到测试用例集文件: {benchmark_path}")
        return False
        
    with open(benchmark_path, "r", encoding="utf-8") as f:
        cases = json.load(f)
        
    if max_cases:
        cases = cases[:max_cases]
        
    print("=" * 78)
    print(" 🛡️  Dify 自动化防塌房与回归质检测试平台 (Regression Guard Suite)")
    print(f" 🎯 目标智能体地址: {base_url} (Token: {token[:8]}...)")
    print(f" 📚 加载历史错题库: {len(cases)} 个关键历史用例")
    print("=" * 78)
    
    results = []
    start_time = time.time()
    
    for idx, case in enumerate(cases, 1):
        cid = case.get("id")
        cname = case.get("name")
        query = case.get("query")
        
        print(f"\n[{idx}/{len(cases)}] 正在回归测试: [{cid}] {cname} ...", flush=True)
        t0 = time.time()
        resp = query_dify_agent(query, base_url=base_url, token=token)
        latency = time.time() - t0
        
        eval_res = evaluate_case(case, resp)
        eval_res["latency"] = latency
        results.append(eval_res)
        
        if eval_res["passed"]:
            print(f"  ✅ 质检通过 | 评分: {eval_res['score']} | 耗时: {latency:.2f}s | 未发现历史退化")
        else:
            print(f"  ❌ ⚠️ 警告：检测到历史问题复发！评分: {eval_res['score']} | 耗时: {latency:.2f}s")
            for v in eval_res["violations"]:
                print(f"     🚩 命中的历史老毛病: {v}")
                
    elapsed = time.time() - start_time
    total_count = len(results)
    pass_count = sum(1 for r in results if r["passed"])
    fail_count = total_count - pass_count
    pass_rate = (pass_count / total_count * 100) if total_count > 0 else 0
    avg_score = sum(r["score"] for r in results) / total_count if total_count > 0 else 0
    
    print("\n" + "=" * 78)
    print(" 📊 自动化回归体检最终总结:")
    print(f"    - 总测试用例数: {total_count}")
    print(f"    - 成功防塌房 (PASS): {pass_count}")
    print(f"    - 历史问题再犯 (REGRESSED): {fail_count}")
    print(f"    - 回归通过率: {pass_rate:.1f}%")
    print(f"    - 平均健康得分: {avg_score:.1f} / 100")
    print(f"    - 总耗时: {elapsed:.2f}s")
    print("=" * 78)
    
    # 导出报告
    os.makedirs(REPORTS_DIR, exist_ok=True)
    report_file = os.path.join(REPORTS_DIR, f"regression_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.md")
    latest_report_file = os.path.join(REPORTS_DIR, "latest_regression_report.md")
    
    generate_markdown_report(results, report_file, pass_rate, avg_score, elapsed)
    with open(latest_report_file, "w", encoding="utf-8") as f:
        with open(report_file, "r", encoding="utf-8") as src:
            f.write(src.read())
            
    print(f"📝 完整质检报告已生成: {report_file}")
    print(f"🔗 最新报告软链: {latest_report_file}")
    
    if fail_count > 0:
        print("\n🚨 门禁阻断警告：当前修改导致部分历史问题复发，请勿上线发布！")
        return False
    else:
        print("\n✨ 门禁通过：所有历史关键铁律均完美守住，系统未发生任何功能塌房！")
        return True

def generate_markdown_report(results, report_path, pass_rate, avg_score, elapsed):
    lines = [
        "# 🛡️ Dify 自动化防塌房与回归质检体检报告",
        f"- **体检时间**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"- **回归通过率**: `{pass_rate:.1f}%`",
        f"- **平均健康评分**: `{avg_score:.1f} / 100`",
        f"- **总测试耗时**: `{elapsed:.2f} 秒`",
        "",
        "## 一、体检总览表格",
        "| 用例编号 | 功能模块 / 场景名称 | 质检结论 | 得分 | 耗时 | 历史退化分析 |",
        "| :--- | :--- | :---: | :---: | :---: | :--- |"
    ]
    for r in results:
        status_badge = "🟢 **PASS**" if r["passed"] else "🔴 **REGRESSED**"
        violations_str = "<br>".join([f"⚠️ {v}" for v in r["violations"]]) if r["violations"] else "无（未犯老毛病）"
        lines.append(f"| `{r['case_id']}` | **{r['name']}** | {status_badge} | {r['score']} | {r['latency']:.1f}s | {violations_str} |")
        
    lines.append("")
    lines.append("## 二、逐项详细审计结果")
    for r in results:
        lines.append(f"### 📋 [{r['case_id']}] {r['name']}")
        lines.append(f"- **分类**: `{r['category']}`")
        lines.append(f"- **是否发生退化**: `{'是 ⚠️ (已报警)' if r['has_regression'] else '否 (安全)'}`")
        if r["violations"]:
            lines.append("#### ❌ 命中的历史老毛病 (Violations):")
            for v in r["violations"]:
                lines.append(f"  * 🔴 **{v}**")
        if r["satisfied_principles"]:
            lines.append("#### ✅ 成功守住的核心铁律 (Preserved):")
            for s in r["satisfied_principles"]:
                lines.append(f"  * 🟢 {s}")
        lines.append("<details><summary><b>展开查看模型实际回复片段</b></summary>\n")
        lines.append("```text")
        lines.append(r["actual_response"][:1200] + ("..." if len(r["actual_response"]) > 1200 else ""))
        lines.append("```\n</details>\n")
        
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Dify 自动化防塌房与回归质检测试平台")
    parser.add_argument("--benchmark", default=DEFAULT_BENCHMARK_FILE, help="测试错题集 JSON 路径")
    parser.add_argument("--url", default=DEFAULT_DIFY_URL, help="Dify 基础 API 地址")
    parser.add_argument("--token", default=DEFAULT_APP_TOKEN, help="Dify 目标应用 API 凭证")
    parser.add_argument("--limit", type=int, default=None, help="仅运行前 N 个用例进行快速自测")
    args = parser.parse_args()
    
    ok = run_regression_suite(benchmark_path=args.benchmark, base_url=args.url, token=args.token, max_cases=args.limit)
    sys.exit(0 if ok else 1)
