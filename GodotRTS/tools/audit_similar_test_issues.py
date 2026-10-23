# -*- coding: utf-8 -*-
"""
Dify 知识库相似问题排查与经验联动模块 (Similar Issues & Pitfalls Auditor)
在每次自动化测试或构建执行时：
1. 自动比对测试用例的失败特征、异常报错与警告信息。
2. 检索 Dify 沉淀的历史研发问答库，查找是否存在“历史类似问题”。
3. 自动生成排查经验提示与历史根因参考，直接输出在测试报告与终端中。
"""

import os
import sys
from typing import List, Dict, Any, Optional

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if WORKSPACE_DIR not in sys.path:
    sys.path.insert(0, WORKSPACE_DIR)

try:
    from tools.sync_dify_knowledge import search_similar_issues, append_qa_entry
except ImportError:
    try:
        from sync_dify_knowledge import search_similar_issues, append_qa_entry
    except ImportError:
        search_similar_issues = None
        append_qa_entry = None


def audit_test_results_against_knowledge(results: List[Dict[str, Any]], query: str = "") -> Dict[str, Any]:
    """
    将测试结果与 Dify 历史问答/缺陷经验库进行相似度交叉审计
    """
    if not search_similar_issues:
        return {
            "has_matches": False,
            "md_section": "",
            "html_section": "",
            "matches": []
        }

    failed_cases = [r for r in results if not r.get("passed", True)]
    matched_issues = []

    if failed_cases:
        # 1. 存在失败项：针对每个失败特征进行高优先级精确历史匹配
        for fc in failed_cases:
            search_str = f"{fc.get('name', '')} {fc.get('detail', '')}"
            hits = search_similar_issues(search_str, top_k=2)
            for h in hits:
                matched_issues.append({
                    "case_id": fc.get("id"),
                    "case_name": fc.get("name"),
                    "status": "FAIL",
                    "hit": h
                })
    else:
        # 2. 全部通过：针对关键核心领域（镜头、零GC、渲染、音频、着色器）进行防回退（Regression Guard）审计
        sample_checks = [
            ("镜头与视角", "3D RTS 镜头 180度 Y-yaw 逆转 与 视口 4 角视锥地面边界裁剪"),
            ("零GC规范", "高频物理帧零 GC 内存泄漏 LINQ 堆分配卡顿"),
            ("水体与特效", "商用发布级水体着色器 depth foam 浪花泡沫 Gerstner"),
            ("音频总线", "音频系统 AudioBus Master Music SFX 分轨")
        ]
        for cat_name, check_str in sample_checks:
            hits = search_similar_issues(check_str, top_k=1)
            if hits and hits[0]["score"] >= 6.0:
                matched_issues.append({
                    "case_id": "GUARD",
                    "case_name": f"{cat_name}防回退守护",
                    "status": "PASS",
                    "hit": hits[0]
                })

    if not matched_issues:
        return {
            "has_matches": False,
            "md_section": "",
            "html_section": "",
            "matches": []
        }

    # 组装 Markdown 报告片段
    md_lines = [
        "### 🔍 【Dify 知识库相似问题排查与经验联动】",
        "> 💡 自动化测试流水线已自动联动 Dify 历史问答经验库，检索到以下相关历史记录与技术沉淀：",
        ""
    ]

    for item in matched_issues:
        hit = item["hit"]
        status_prefix = "🚨 **【失败项相似历史缺陷排查】**" if item["status"] == "FAIL" else "🛡️ **【历史相似技术规范对齐】**"
        md_lines.append(f"{status_prefix} `[{item['case_id']}]` **{hit['title']}**")
        md_lines.append(f"- **所属类别**: `{hit['category']}` | **相似度得分**: `{hit['score']}` | **匹配关键词**: `{', '.join(hit['matched_keywords'])}`")
        md_lines.append(f"- **历史沉淀时间**: `{hit['created_at']}`")
        md_lines.append("- **历史根因分析与方案参考**:")
        # 引用格式展示
        ans_preview = hit['answer_snippet'].replace("\n", "\n> ")
        md_lines.append(f"> {ans_preview}")
        md_lines.append("")

    md_section = "\n".join(md_lines)

    # 组装 HTML 报告片段
    html_items = ""
    for item in matched_issues:
        hit = item["hit"]
        border_color = "#ef4444" if item["status"] == "FAIL" else "#38bdf8"
        title_color = "#f87171" if item["status"] == "FAIL" else "#38bdf8"
        status_label = "【失败项相似缺陷】" if item["status"] == "FAIL" else "【历史技术规范对齐】"
        html_items += f"""
        <div style="background:#0f172a;border-left:4px solid {border_color};border-radius:6px;padding:12px 16px;margin-bottom:12px;">
            <div style="display:flex;align-items:center;gap:8px;font-size:14px;font-weight:bold;color:{title_color};">
                <span>{status_label}</span>
                <code>[{item['case_id']}]</code>
                <span>{hit['title']}</span>
            </div>
            <div style="color:#94a3b8;font-size:12px;margin:6px 0;">
                类别: <strong>{hit['category']}</strong> | 相似得分: <strong>{hit['score']}</strong> | 关键词: <code>{', '.join(hit['matched_keywords'])}</code> | 时间: {hit['created_at']}
            </div>
            <div style="background:#1e293b;border-radius:4px;padding:8px 12px;color:#cbd5e1;font-size:13px;line-height:1.5;">
                {hit['answer_snippet']}
            </div>
        </div>
        """

    html_section = f"""
    <div style="background:#1e293b;border-radius:8px;padding:18px;margin:20px 0;border:1px solid #334155;">
        <h3 style="color:#38bdf8;margin-top:0;display:flex;align-items:center;gap:8px;">
            🔍 Dify 知识库相似问题排查与经验联动
        </h3>
        <p style="color:#94a3b8;font-size:13px;">自动化流水线已自动在历史 400+ 条技术问答中检索相似案例，提供实时诊断与经验对照：</p>
        {html_items}
    </div>
    """

    return {
        "has_matches": True,
        "md_section": md_section,
        "html_section": html_section,
        "matches": matched_issues
    }
