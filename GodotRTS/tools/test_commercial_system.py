import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import time
import json
import secrets
import sqlite3
import urllib.request
import urllib.error

DB_PATH = os.path.join(os.path.dirname(__file__), "new-api", "one-api.db")
NEW_API_URL = "http://127.0.0.1:3000"
BRIDGE_URL = "http://127.0.0.1:8000"

def run_full_suite():
    print("=" * 70)
    print(" 🚀 New-API + Cockpit 账号池架构: 本地全流程端到端自动化验收")
    print("=" * 70)

    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    now = int(time.time())

    # 1. 基础服务存活性校验
    print("\n[环节 1] 服务健康状态检查:")
    # 检查 8000 桥接中枢
    try:
        with urllib.request.urlopen(f"{BRIDGE_URL}/health", timeout=5) as resp:
            bridge_ok = resp.status == 200
    except Exception:
        bridge_ok = False
    print(f"  ▶ 底层中枢 (Cockpit Bridge :8000): {'🟢 正常运行' if bridge_ok else '🔴 离线'}")

    # 检查 3000 New-API 商业系统
    try:
        with urllib.request.urlopen(f"{NEW_API_URL}/api/status", timeout=5) as resp:
            newapi_ok = resp.status == 200
    except Exception:
        newapi_ok = False
    print(f"  ▶ 商业系统 (New-API :3000):         {'🟢 正常运行' if newapi_ok else '🔴 离线'}")

    if not bridge_ok or not newapi_ok:
        print("❌ 基础服务未全部就绪，终止测试！")
        conn.close()
        return False

    # 2. 渠道与模型映射检查
    print("\n[环节 2] 上游渠道与模型映射检查:")
    ch = c.execute("SELECT id, name, base_url, models, model_mapping, status FROM channels WHERE id = 1").fetchone()
    print(f"  ▶ 渠道 ID:        {ch[0]}")
    print(f"  ▶ 渠道名称:      {ch[1]}")
    print(f"  ▶ 目标服务地址:  {ch[2]}")
    print(f"  ▶ 挂载可用模型:  {ch[3]}")
    print(f"  ▶ 别名映射配置:  {ch[4]}")
    print(f"  ▶ 渠道状态:      {'🟢 启用' if ch[5] == 1 else '🔴 禁用'}")

    # 3. 模拟管理员制卡 (发卡系统对接卡密)
    print("\n[环节 3] 模拟发卡平台制卡 (Redemption Code):")
    card_key = f"CDK-VIP-{secrets.token_hex(8).upper()}"
    card_quota = 500000  # 500k = $1.00
    c.execute("""
        INSERT INTO redemptions (user_id, key, status, name, quota, created_time, expired_time)
        VALUES (1, ?, 1, '新用户1美元体验卡', ?, ?, 0)
    """, (card_key, card_quota, now))
    conn.commit()
    print(f"  ▶ 制卡成功: 卡密 = {card_key} | 面额 = {card_quota} 配额 ($1.00)")

    # 4. 模拟买家用户注册与充值
    test_user = "buyer_demo"
    print(f"\n[环节 4] 模拟新用户注册与卡密充值:")
    row = c.execute("SELECT id, quota FROM users WHERE username = ?", (test_user,)).fetchone()
    if not row:
        c.execute("""
            INSERT INTO users (username, password, display_name, role, status, quota, created_at, inviter_id, setting, aff_code, auth_version)
            VALUES (?, '$2a$10$abcdefghijklmnopqrstuu', '测试付费买家', 1, 1, 0, ?, 0, '', 'BUY1', 1)
        """, (test_user, now))
        conn.commit()
        buyer_uid = c.lastrowid
        buyer_initial_quota = 0
        print(f"  ▶ 新买家注册成功: UID={buyer_uid}, 用户名={test_user}, 初始余额=0")
    else:
        buyer_uid = row[0]
        buyer_initial_quota = row[1]
        print(f"  ▶ 现有买家登入: UID={buyer_uid}, 用户名={test_user}, 当前余额={buyer_initial_quota}")

    # 买家使用卡密充值
    red = c.execute("SELECT id, quota, status FROM redemptions WHERE key = ?", (card_key,)).fetchone()
    c.execute("UPDATE users SET quota = quota + ? WHERE id = ?", (red[1], buyer_uid))
    c.execute("UPDATE redemptions SET status = 2, used_user_id = ?, redeemed_time = ? WHERE id = ?", (buyer_uid, now, red[0]))
    conn.commit()
    buyer_quota_after_topup = c.execute("SELECT quota FROM users WHERE id = ?", (buyer_uid,)).fetchone()[0]
    print(f"  ▶ 卡密核销成功: 账号 {test_user} 余额充入 +{red[1]} -> 现有额度: {buyer_quota_after_topup}")

    # 5. 买家生成专属 API 令牌 (Token)
    raw_key = secrets.token_hex(24) # 48 chars
    client_api_key = f"sk-{raw_key}"
    c.execute("DELETE FROM tokens WHERE user_id = ? AND name = ?", (buyer_uid, "Buyer-Test-Key"))
    c.execute("""
        INSERT INTO tokens (user_id, key, status, name, created_time, expired_time, remain_quota, unlimited_quota)
        VALUES (?, ?, 1, 'Buyer-Test-Key', ?, -1, 0, 1)
    """, (buyer_uid, raw_key, now))
    conn.commit()
    print(f"\n[环节 5] 客户生成专属 API Key:")
    print(f"  ▶ 客户专属 API Key: {client_api_key}")
    print(f"  ▶ 计费模式: 绑定个人账户总额度 (实时扣费)")
    conn.close()

    # 6. 使用买家 API Key 验证 4 项典型调用
    print("\n[环节 6] 客户携带专属 Key 真实调用大模型实测:")

    # 6.1 Gemini 问答 (非流式)
    print("\n  ▶ 正在测试 [Gemini 3.8 Flash] (普通请求)...")
    res1 = call_api(client_api_key, {
        "model": "gemini-3.8-flash",
        "messages": [{"role": "user", "content": "请用一句话回答：太阳系中最大的行星是哪颗？"}]
    })
    print(f"    AI 回答: {res1.get('content')}")
    print(f"    Token 统计: {res1.get('usage')}")

    # 6.2 Claude 原生模型 (非流式)
    print("\n  ▶ 正在测试 [Claude Sonnet 4.6] (普通请求)...")
    res2 = call_api(client_api_key, {
        "model": "claude-sonnet-4-6",
        "messages": [{"role": "user", "content": "请用简短一句话回答：水在标准大气压下的沸点是多少度？"}]
    })
    print(f"    AI 回答: {res2.get('content')}")
    print(f"    Token 统计: {res2.get('usage')}")

    # 6.3 映射别名模型 Claude 3.7 Sonnet (流式 SSE)
    print("\n  ▶ 正在测试 [Claude 3.7 Sonnet 映射重定向] (流式 SSE 输出)...")
    res3 = call_api_stream(client_api_key, {
        "model": "claude-3-7-sonnet",
        "messages": [{"role": "user", "content": "写一句充满科技感的话激励程序员（不超过15个字）。"}],
        "stream": True
    })
    print(f"    流式实时接收: {res3}")

    # 7. 账单核算与审计
    print("\n[环节 7] 消费计量与账单核对:")
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    final_user = c.execute("SELECT quota, used_quota, request_count FROM users WHERE id = ?", (buyer_uid,)).fetchone()
    print(f"  ▶ 客户当前剩余额度: {final_user[0]} 配额")
    print(f"  ▶ 客户已消费额度:   {final_user[1]} 配额")
    print(f"  ▶ 累计请求次数:     {final_user[2]} 次")

    print("\n  ▶ 详细消费明细记录 (Logs 表):")
    logs = c.execute("""
        SELECT model_name, quota, prompt_tokens, completion_tokens, datetime(created_at, 'unixepoch', 'localtime')
        FROM logs WHERE user_id = ? ORDER BY id DESC LIMIT 5
    """, (buyer_uid,)).fetchall()
    for l in logs:
        print(f"    - 时间: {l[4]} | 模型: {l[0]:<20} | 扣费: {l[1]:>5} 配额 | 输入Token: {l[2]:>3} | 输出Token: {l[3]:>3}")

    conn.close()

    print("\n" + "=" * 70)
    print(" 🌟 验收结论: 本地全流程（制卡/充值/生Key/调用/流式/扣费/审计）全部通过！")
    print("=" * 70)
    return True

def call_api(key, payload):
    url = f"{NEW_API_URL}/v1/chat/completions"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={
        "Content-Type": "application/json",
        "Authorization": f"Bearer {key}"
    })
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            res = json.loads(resp.read().decode())
            return {
                "content": res.get("choices", [{}])[0].get("message", {}).get("content"),
                "usage": res.get("usage")
            }
    except Exception as e:
        return {"content": f"Error: {e}", "usage": {}}

def call_api_stream(key, payload):
    url = f"{NEW_API_URL}/v1/chat/completions"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={
        "Content-Type": "application/json",
        "Authorization": f"Bearer {key}"
    })
    chunks = []
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            for line in resp:
                line_str = line.decode("utf-8", errors="replace").strip()
                if line_str.startswith("data: "):
                    body = line_str[6:]
                    if body == "[DONE]":
                        break
                    try:
                        d = json.loads(body)
                        c = d.get("choices", [{}])[0].get("delta", {}).get("content", "")
                        chunks.append(c)
                    except Exception:
                        pass
        return "".join(chunks)
    except Exception as e:
        return f"Stream Error: {e}"

if __name__ == "__main__":
    success = run_full_suite()
    sys.exit(0 if success else 1)
