# -*- coding: utf-8 -*-
"""
逆向工程资料全面同步与知识库入库脚本
1. 扫描 knowledge/reverse_engineering 目录下的所有逆向与脱壳攻坚报告。
2. 切分并合并至本地知识库检索集 (knowledge/rts_qa_knowledge.json 与 .md)。
3. 同步导入至 Dify 数据库中的两个知识库：
   - 「大玩家逆向工程」 (ID: 696aeb16-0d8a-41c1-97b7-4eb8edbb2cde)
   - 「Godot RTS 研发问答与测试排查经验库」 (ID: d8b5c4a1-3e2a-4f5b-9c8d-7e6f5a4b3c2d)
"""

import os
import sys
import re
import json
import uuid
import time
import subprocess
from typing import List, Dict, Any

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KNOWLEDGE_DIR = os.path.join(WORKSPACE_DIR, "knowledge")
REVERSE_DIR = os.path.join(KNOWLEDGE_DIR, "reverse_engineering")

JSON_PATH = os.path.join(KNOWLEDGE_DIR, "rts_qa_knowledge.json")
MD_PATH = os.path.join(KNOWLEDGE_DIR, "rts_qa_knowledge.md")

TENANT_ID = "0073f7b6-19a1-4e26-b8ec-f00c498c4d4c"
USER_ID = "c79f82c9-2f25-40c1-bb83-4bb7d20e957c"

DATASET_REVERSE_ID = "696aeb16-0d8a-41c1-97b7-4eb8edbb2cde"  # 大玩家逆向工程
DATASET_RTS_ID = "d8b5c4a1-3e2a-4f5b-9c8d-7e6f5a4b3c2d"      # Godot RTS 研发问答与测试排查经验库


def chunk_markdown(content: str, max_chunk_size: int = 1200) -> List[str]:
    """按标题或段落智能切分 Markdown 为知识分段"""
    sections = re.split(r'\n(?=#{1,3}\s+)', content)
    chunks = []
    for sec in sections:
        sec = sec.strip()
        if not sec:
            continue
        if len(sec) <= max_chunk_size:
            chunks.append(sec)
        else:
            # 二次按段落切分
            paras = sec.split('\n\n')
            cur = ""
            for p in paras:
                if len(cur) + len(p) < max_chunk_size:
                    cur += (p + "\n\n")
                else:
                    if cur.strip():
                        chunks.append(cur.strip())
                    cur = p + "\n\n"
            if cur.strip():
                chunks.append(cur.strip())
    return chunks


def sync_to_local_knowledge():
    """将逆向文档提取并注入本地知识库检索集"""
    if not os.path.exists(REVERSE_DIR):
        print(f"❌ 目录不存在: {REVERSE_DIR}")
        return

    entries = []
    if os.path.exists(JSON_PATH):
        try:
            with open(JSON_PATH, "r", encoding="utf-8") as f:
                entries = json.load(f)
        except Exception:
            entries = []

    existing_titles = set(e["title"] for e in entries)

    md_files = [f for f in os.listdir(REVERSE_DIR) if f.endswith(".md")]
    print(f"📂 发现 {len(md_files)} 个逆向工程核心文档...")

    new_entries_count = 0
    for fname in md_files:
        fpath = os.path.join(REVERSE_DIR, fname)
        with open(fpath, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()

        chunks = chunk_markdown(content)
        for idx, ch in enumerate(chunks, 1):
            first_line = ch.split("\n")[0].strip("# ").strip()
            title = f"【逆向攻坚】{fname.replace('.md', '')} - {first_line[:40]}"
            if title in existing_titles:
                continue

            entry = {
                "id": f"re_{uuid.uuid4().hex[:8]}",
                "category": "逆向工程与底层安全",
                "title": title,
                "app": "逆向工程与协议代码分析架构师",
                "created_at": time.strftime("%Y-%m-%d %H:%M:%S"),
                "query": f"关于《{fname}》的第 {idx} 部分：{first_line}",
                "answer": ch
            }
            entries.append(entry)
            existing_titles.add(title)
            new_entries_count += 1

    with open(JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(entries, f, ensure_ascii=False, indent=2)

    print(f"✅ 本地 JSON 知识集更新完成！新增 {new_entries_count} 个逆向知识切片，总计 {len(entries)} 条条目。")


def sync_to_dify_db():
    """将逆向文档直接入库到 Dify Postgres 数据库"""
    md_files = [f for f in os.listdir(REVERSE_DIR) if f.endswith(".md")]
    
    # 获取 DB 中已有文档名
    cmd_check = ["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify", "-t", "-A", "-c", 
                 "SELECT dataset_id, name FROM documents;"]
    proc = subprocess.run(cmd_check, capture_output=True, text=True, encoding="utf-8")
    existing_in_db = set(line.strip() for line in proc.stdout.splitlines() if line.strip())

    target_datasets = [
        (DATASET_REVERSE_ID, "大玩家逆向工程"),
        (DATASET_RTS_ID, "Godot RTS 研发问答与测试排查经验库")
    ]

    for ds_id, ds_name in target_datasets:
        print(f"\n🔄 正在向 Dify 知识库 【{ds_name}】 导入逆向工程资料...")
        for fname in md_files:
            key = f"{ds_id}|{fname}"
            if key in existing_in_db:
                print(f"  ⏩ 已存在跳过: {fname}")
                continue

            fpath = os.path.join(REVERSE_DIR, fname)
            with open(fpath, "r", encoding="utf-8", errors="replace") as f:
                full_text = f.read()

            chunks = chunk_markdown(full_text)
            doc_id = str(uuid.uuid4())
            batch_str = time.strftime("%Y%m%d%H%M%S") + "0000"
            word_count = len(full_text)

            # 插入 documents
            doc_sql = f"""
            INSERT INTO documents (
                id, tenant_id, dataset_id, position, data_source_type, 
                data_source_info, batch, name, created_from, created_by, 
                indexing_status, doc_form, doc_language, word_count, tokens, enabled
            ) VALUES (
                '{doc_id}', '{TENANT_ID}', '{ds_id}', 1, 'upload_file',
                '{{"upload_file_id": ""}}', '{batch_str}', '{fname}', 'api', '{USER_ID}',
                'completed', 'text_model', 'Chinese', {word_count}, 0, true
            );
            """

            # 插入 segments
            seg_sqls = []
            for s_idx, ch in enumerate(chunks, 1):
                seg_id = str(uuid.uuid4())
                ch_escaped = ch.replace("'", "''")
                seg_sql = f"""
                INSERT INTO document_segments (
                    id, tenant_id, dataset_id, document_id, position, 
                    content, word_count, tokens, hit_count, status, 
                    created_by, enabled
                ) VALUES (
                    '{seg_id}', '{TENANT_ID}', '{ds_id}', '{doc_id}', {s_idx},
                    '{ch_escaped}', {len(ch)}, 0, 0, 'completed',
                    '{USER_ID}', true
                );
                """
                seg_sqls.append(seg_sql)

            full_batch_sql = doc_sql + "\n" + "\n".join(seg_sqls)
            
            p = subprocess.Popen(["docker", "exec", "-i", "docker-db_postgres-1", "psql", "-U", "postgres", "-d", "dify"],
                                 stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            out, err = p.communicate(input=full_batch_sql.encode("utf-8"))
            if p.returncode == 0:
                print(f"  ✅ 导入成功: {fname} (共 {len(chunks)} 个切片, {word_count} 字符)")
            else:
                print(f"  ❌ 导入失败: {fname}, 错误: {err.decode('utf-8', errors='replace')[:200]}")


if __name__ == "__main__":
    sync_to_local_knowledge()
    sync_to_dify_db()
    print("\n🎉 逆向工程资料已全量归档并同步至 Dify 与本地测试排查知识库！")
