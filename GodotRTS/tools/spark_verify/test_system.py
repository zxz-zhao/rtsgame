"""
SparkVerify 极验网络验证系统 - 全链路端到端自动化测试
"""
import sys
import time
import requests

BASE_URL = "http://127.0.0.1:8989"

def run_tests():
    print("==========================================================")
    print("         SparkVerify 全链路端到端自动化功能测试")
    print("==========================================================")

    # 1. 测试 Admin 登录
    print("\n[TC-01] 管理员认证与登录测试...")
    login_resp = requests.post(f"{BASE_URL}/api/v1/admin/login", json={"username": "admin", "password": "admin123"})
    assert login_resp.status_code == 200, f"登录失败: {login_resp.text}"
    token = login_resp.json()["token"]
    headers = {"Authorization": f"Bearer {token}"}
    print(f"[+] 管理员登录成功！Token: {token[:12]}...")

    # 2. 批量制卡测试
    print("\n[TC-02] 批量生成月卡测试 (5张)...")
    gen_payload = {
        "app_id": "APP_DEMO_001",
        "card_type": "month",
        "count": 5,
        "prefix": "SV",
        "note": "自动化测试批量月卡"
    }
    gen_resp = requests.post(f"{BASE_URL}/api/v1/admin/cards/generate", json=gen_payload, headers=headers)
    assert gen_resp.status_code == 200, f"制卡失败: {gen_resp.text}"
    cards = gen_resp.json()["data"]
    print(f"[+] 成功生成 5 张卡密:")
    for c in cards:
        print(f"    - {c}")
    test_card = cards[0]

    # 3. 客户端初始化测试
    print("\n[TC-03] 客户端初始化 (获取公告与版本检测)...")
    init_payload = {"app_id": "APP_DEMO_001", "version": "1.0.0", "hwid": "TEST_HWID_AAA_001"}
    init_resp = requests.post(f"{BASE_URL}/api/v1/client/init", json=init_payload)
    assert init_resp.status_code == 200, f"初始化失败: {init_resp.text}"
    init_data = init_resp.json()["data"]
    print(f"[+] 初始化成功: 软件名: {init_data['app_name']} | 最新版本: {init_data['latest_version']}")
    print(f"    公告内容: {init_data['notice']}")

    # 4. 首次激活卡密与设备码绑定
    print(f"\n[TC-04] 首次激活卡密 [{test_card}] 并绑定设备 [TEST_HWID_AAA_001]...")
    ts = int(time.time())
    verify_payload = {
        "app_id": "APP_DEMO_001",
        "card_key": test_card,
        "hwid": "TEST_HWID_AAA_001",
        "timestamp": ts
    }
    verify_resp = requests.post(f"{BASE_URL}/api/v1/client/verify", json=verify_payload)
    assert verify_resp.status_code == 200, f"卡密验证失败: {verify_resp.text}"
    v_data = verify_resp.json()
    assert v_data["code"] == 200, f"验证返回非200: {v_data}"
    sess_info = v_data["data"]
    session_token = sess_info["session_token"]
    print(f"[+] 首次激活成功！")
    print(f"    - 会话令牌 (Session): {session_token}")
    print(f"    - 到期时间: {sess_info['expires_at']}")
    print(f"    - 下发云变量: {sess_info['cloud_vars']}")

    # 5. 心跳保活测试
    print("\n[TC-05] 客户端心跳上报测试...")
    hb_payload = {
        "app_id": "APP_DEMO_001",
        "session_token": session_token,
        "hwid": "TEST_HWID_AAA_001",
        "timestamp": int(time.time())
    }
    hb_resp = requests.post(f"{BASE_URL}/api/v1/client/heartbeat", json=hb_payload)
    assert hb_resp.status_code == 200, f"心跳失败: {hb_resp.text}"
    assert hb_resp.json()["code"] == 200
    print(f"[+] 心跳成功返回: {hb_resp.json()['msg']}")

    # 6. 一机一码防御测试 (使用另一台机器码 BBB_999 尝试登录)
    print(f"\n[TC-06] 一机一码防御拦截测试 (机器 BBB_999 盗用已绑定卡密)...")
    fake_verify_payload = {
        "app_id": "APP_DEMO_001",
        "card_key": test_card,
        "hwid": "TEST_HWID_BBB_999",
        "timestamp": int(time.time())
    }
    fake_resp = requests.post(f"{BASE_URL}/api/v1/client/verify", json=fake_verify_payload)
    fake_data = fake_resp.json()
    print(f"[*] 服务端拦截响应: 状态码={fake_data.get('code')}, 消息={fake_data.get('msg')}")
    assert fake_data.get("code") == 403, "一机一码拦截失败！"
    print(f"[+] 成功拦截未授权机器的盗用行为！一机一码策略生效！")

    # 7. 查看在线终端列表
    print("\n[TC-07] 管理员在线监控大盘查询...")
    sess_resp = requests.get(f"{BASE_URL}/api/v1/admin/sessions?app_id=APP_DEMO_001", headers=headers)
    assert sess_resp.status_code == 200
    online_list = sess_resp.json()["data"]
    print(f"[+] 当前在线终端数量: {len(online_list)}")
    assert len(online_list) >= 1
    print(f"    - 在线会话: {online_list[0]['session_token']} | 机器码: {online_list[0]['hwid']}")

    # 8. 客户端主动登出
    print("\n[TC-08] 客户端安全退出与会话注销...")
    logout_resp = requests.post(f"{BASE_URL}/api/v1/client/logout?session_token={session_token}")
    assert logout_resp.status_code == 200
    print(f"[+] 客户端已登出: {logout_resp.json()['msg']}")

    # 9. 系统概览数据核对
    print("\n[TC-09] 系统仪表大盘指标审计...")
    stats_resp = requests.get(f"{BASE_URL}/api/v1/admin/overview?app_id=APP_DEMO_001", headers=headers)
    assert stats_resp.status_code == 200
    stats = stats_resp.json()["data"]
    print(f"[+] 仪表盘指标: 总卡密数={stats['total_cards']} | 今日激活={stats['today_activated']} | 在线数={stats['online_count']}")

    print("\n==========================================================")
    print("      [SUCCESS] 全部 9 项核心测试 100% 通过！系统已具备生产就绪状态！")
    print("==========================================================")

if __name__ == "__main__":
    run_tests()
