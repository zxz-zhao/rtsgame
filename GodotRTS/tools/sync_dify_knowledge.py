# -*- coding: utf-8 -*-
"""
Dify 问答与缺陷经验知识库同步器
功能：
1. 从 Dify 数据库中提取所有高质量技术问答与缺陷排查历史（过滤游戏遥测心跳）。
2. 清洗、分类并生成结构化知识库文档 (JSON 与 Markdown 规范格式)。
3. 提供相似度匹配引擎：在自动化测试执行时，自动比对测试失败项/警告特征，检索历史类似问题与修复对策。
4. 提供实时追加写入接口：每次测试或人工对话时，可将新问题与解答自动沉淀入库。
"""

import os
import sys
import re
import json
import time
import subprocess
from typing import List, Dict, Any, Optional

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KNOWLEDGE_DIR = os.path.join(WORKSPACE_DIR, "knowledge")
os.makedirs(KNOWLEDGE_DIR, exist_ok=True)

KNOWLEDGE_JSON_PATH = os.path.join(KNOWLEDGE_DIR, "rts_qa_knowledge.json")
KNOWLEDGE_MD_PATH = os.path.join(KNOWLEDGE_DIR, "rts_qa_knowledge.md")


def extract_messages_from_dify() -> List[Dict[str, Any]]:
    """从 Dify 容器数据库提取所有非遥测的人工技术问答与架构记录"""
    sql = """
    SELECT json_build_object(
        'id', m.id,
        'app', a.name,
        'query', m.query,
        'answer', m.answer,
        'created_at', to_char(m.created_at, 'YYYY-MM-DD HH24:MI:SS')
    )
    FROM messages m
    JOIN apps a ON m.app_id = a.id
    WHERE a.name != 'RTS 战术战略指挥官 (AI Commander)'
      AND length(m.query) > 15
      AND m.query NOT LIKE '{"game_time"%'
    ORDER BY m.created_at DESC;
    """

    cmd = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t", "-A", "-c", sql]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
        if proc.returncode != 0:
            print(f"⚠️ [提取失败]: {proc.stderr}")
            return []

        records = []
        for line in proc.stdout.splitlines():
            line = line.strip()
            if not line:
                continue
            try:
                item = json.loads(line)
                # 过滤心跳或纯遥测 JSON
                q = item.get("query", "")
                if q.startswith("{") and "game_time" in q:
                    continue
                records.append(item)
            except Exception:
                continue

        print(f"✅ 从 Dify 成功提取 {len(records)} 条高质量历史问答记录")
        return records
    except Exception as e:
        print(f"⚠️ [提取异常]: {e}")
        return []


def categorize_entry(query: str, answer: str) -> str:
    """根据关键词智能归类"""
    text = (query + " " + answer).lower()
    if any(k in text for k in ["camera", "镜头", "视角", "yaw", "minimap", "小地图", "视锥", "全选时视角"]):
        return "镜头与视角控制"
    elif any(k in text for k in ["zero-gc", "gc", "内存泄漏", "exittree", "linq", "分配", "性能", "卡顿"]):
        return "零GC与性能规范"
    elif any(k in text for k in ["audio", "music", "音频", "音乐", "音效", "bgm", "总线", "audiobus"]):
        return "音频与音乐系统"
    elif any(k in text for k in ["shader", "水体", "着色器", "foam", "浪花", "vfx", "特效", "decal", "贴花", "焦痕"]):
        return "着色器与视觉特效"
    elif any(k in text for k in ["hud", "ui", "modal", "弹窗", "按钮", "卡片", "溢出", "metalui"]):
        return "UI界面与交互规范"
    elif any(k in text for k in ["vmp", "逆向", "脱壳", "反编译", "pe表", "x32dbg", "授权"]):
        return "逆向工程与底层安全"
    elif any(k in text for k in ["unit", "tank", "飞机", "直升机", "航母", "兵种", "装甲", "弱点", "朝向"]):
        return "兵种单位与战斗机制"
    elif any(k in text for k in ["android", "apk", "模拟器", "闪退", "移动端"]):
        return "移动端与跨平台"
    else:
        return "综合架构与核心逻辑"


def build_knowledge_base():
    """清洗并构建本地与 Dify 兼容的知识库文件"""
    records = extract_messages_from_dify()
    if not records and os.path.exists(KNOWLEDGE_JSON_PATH):
        print("ℹ️ 未提取到新数据，使用已有知识库")
        with open(KNOWLEDGE_JSON_PATH, "r", encoding="utf-8") as f:
            return json.load(f)

    # 规范化条目
    knowledge_entries = []
    seen_queries = set()

    for r in records:
        q = r.get("query", "").strip()
        a = r.get("answer", "").strip()
        if not q or not a:
            continue

        # 提取前 80 字作为特征去重
        q_sig = re.sub(r'\s+', '', q[:80])
        if q_sig in seen_queries:
            continue
        seen_queries.add(q_sig)

        category = categorize_entry(q, a)

        # 提取标题
        first_line = q.split("\n")[0].strip("# ").strip()
        if len(first_line) > 50:
            first_line = first_line[:50] + "..."

        entry = {
            "id": r.get("id"),
            "category": category,
            "title": first_line or "未命名架构技术问答",
            "app": r.get("app"),
            "created_at": r.get("created_at"),
            "query": q,
            "answer": a
        }
        knowledge_entries.append(entry)

    # 保存 JSON
    with open(KNOWLEDGE_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(knowledge_entries, f, ensure_ascii=False, indent=2)

    # 生成 Markdown 文档 (供 Dify 知识库直接导入/索引)
    md_lines = [
        "# Godot 4 RTS 核心研发与测试排查经验知识库",
        f"> 自动沉淀汇总 · 包含 {len(knowledge_entries)} 项历史技术问答、缺陷根因分析与架构规范",
        "",
        "---",
        ""
    ]

    # 按类别组织
    by_category = {}
    for e in knowledge_entries:
        by_category.setdefault(e["category"], []).append(e)

    for cat, items in by_category.items():
        md_lines.append(f"## 📁 分类：{cat} ({len(items)} 项)")
        md_lines.append("")
        for idx, item in enumerate(items, 1):
            md_lines.append(f"### 📌 【{cat} #{idx}】 {item['title']}")
            md_lines.append(f"- **来源应用**: `{item['app']}` | **沉淀时间**: `{item['created_at']}`")
            md_lines.append("")
            md_lines.append("#### ❓ 提问与问题现象")
            # 缩进引用展示
            q_preview = item['query'].strip()
            md_lines.append(q_preview)
            md_lines.append("")
            md_lines.append("#### 💡 诊断结论与解决方案")
            a_preview = item['answer'].strip()
            md_lines.append(a_preview)
            md_lines.append("")
            md_lines.append("---")
            md_lines.append("")

    with open(KNOWLEDGE_MD_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(md_lines))

    print(f"🎉 知识库生成完毕！")
    print(f"  - JSON 数据集: {KNOWLEDGE_JSON_PATH} ({len(knowledge_entries)} 条)")
    print(f"  - Markdown 文档: {KNOWLEDGE_MD_PATH} ({round(os.path.getsize(KNOWLEDGE_MD_PATH)/1024, 1)} KB)")

    return knowledge_entries


def tokenize_text(text: str) -> List[str]:
    """中英文混合高效分词器（英文词法 + 中文 2-gram / 3-gram 滑动窗口）"""
    tokens = []
    en_words = re.findall(r'[A-Za-z0-9_]{3,}', text.lower())
    tokens.extend(en_words)
    cn_chunks = re.findall(r'[\u4e00-\u9fa5]+', text)
    for chunk in cn_chunks:
        if len(chunk) <= 4:
            tokens.append(chunk)
        for i in range(len(chunk) - 1):
            tokens.append(chunk[i:i+2])
        for i in range(len(chunk) - 2):
            tokens.append(chunk[i:i+3])
    return list(set(tokens))


def search_similar_issues(target_text: str, top_k: int = 2) -> List[Dict[str, Any]]:
    """
    在知识库中匹配与当前测试问题/错误特征最相似的历史问题
    支持基于词根加权的关键词重叠相似度算法，零外部依赖，毫秒级响应。
    """
    if not os.path.exists(KNOWLEDGE_JSON_PATH):
        build_knowledge_base()

    if not os.path.exists(KNOWLEDGE_JSON_PATH):
        return []

    try:
        with open(KNOWLEDGE_JSON_PATH, "r", encoding="utf-8") as f:
            entries = json.load(f)
    except Exception:
        return []

    tokens = tokenize_text(target_text)
    if not tokens:
        return []

    scored = []
    high_weights = {
        "camera", "镜头", "视角", "yaw", "minimap", "小地图", "linq", "zero-gc", 
        "exittree", "leak", "audio", "foam", "decal", "shader", "modal", "focus",
        "全选", "突变", "跳转", "卡顿", "溢出", "闪退", "android", "vmp", "装甲", "航母"
    }

    for e in entries:
        full_text = (e["title"] + " " + e["category"] + " " + e["query"] + " " + e["answer"]).lower()
        score = 0.0
        matched_keywords = []
        for t in tokens:
            t_low = t.lower()
            if t_low in full_text:
                weight = 1.0
                if any(hw in t_low for hw in high_weights):
                    weight = 3.5
                score += weight
                matched_keywords.append(t)

        if score > 5.0:  # 设定基础置信度门槛
            scored.append({
                "score": round(score, 1),
                "entry": e,
                "matched_keywords": matched_keywords
            })

    scored.sort(key=lambda x: x["score"], reverse=True)

    results = []
    for item in scored[:top_k]:
        e = item["entry"]
        results.append({
            "score": item["score"],
            "title": e["title"],
            "category": e["category"],
            "matched_keywords": list(set(item["matched_keywords"]))[:6],
            "created_at": e["created_at"],
            "query_snippet": e["query"][:200] + ("..." if len(e["query"]) > 200 else ""),
            "answer_snippet": e["answer"][:300] + ("..." if len(e["answer"]) > 300 else "")
        })

    return results


def append_qa_entry(query: str, answer: str, app_name: str = "实时对话沉淀"):
    """追加新产生的提问与回答到知识库"""
    if not query or not answer or len(query.strip()) < 10:
        return

    entries = []
    if os.path.exists(KNOWLEDGE_JSON_PATH):
        try:
            with open(KNOWLEDGE_JSON_PATH, "r", encoding="utf-8") as f:
                entries = json.load(f)
        except Exception:
            entries = []

    cat = categorize_entry(query, answer)
    first_line = query.split("\n")[0].strip("# ").strip()
    if len(first_line) > 50:
        first_line = first_line[:50] + "..."

    new_item = {
        "id": f"qa_{int(time.time())}",
        "category": cat,
        "title": first_line,
        "app": app_name,
        "created_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "query": query.strip(),
        "answer": answer.strip()
    }
    entries.insert(0, new_item)

    with open(KNOWLEDGE_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(entries, f, ensure_ascii=False, indent=2)

    # 同步追加到 Markdown
    append_md = f"""
### 📌 【{cat} · 实时沉淀】 {new_item['title']}
- **来源**: `{app_name}` | **时间**: `{new_item['created_at']}`

#### ❓ 提问
{query.strip()}

#### 💡 方案
{answer.strip()}

---
"""
    with open(KNOWLEDGE_MD_PATH, "a", encoding="utf-8") as f:
        f.write(append_md)

    print(f"💾 成功沉淀一条新问答到知识库: {first_line}")


if __name__ == "__main__":
    entries = build_knowledge_base()
    print("\n🔍 测试相似问题检索示例（模拟输入：'点击全选按钮时视角突变跳转'）:")
    matches = search_similar_issues("点击全选按钮时视角突变跳转")
    for idx, m in enumerate(matches, 1):
        print(f"\n[{idx}] 相似度得分: {m['score']} | 类别: {m['category']}")
        print(f"    标题: {m['title']}")
        print(f"    命中关键词: {m['matched_keywords']}")
        print(f"    历史解答片段: {m['answer_snippet'][:120]}...")
