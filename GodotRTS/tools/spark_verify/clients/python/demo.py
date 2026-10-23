import time
from spark_verify_client import SparkVerifyClient

def main():
    # 1. 实例化客户端 (连接本地部署的 SparkVerify 服务端)
    client = SparkVerifyClient(
        host="http://127.0.0.1:8989",
        app_id="APP_DEMO_001",
        app_secret="sv_secret_88889999_k9",
        version="1.0.0"
    )

    print(f"[*] 本机硬件特征码 (HWID): {client.hwid}")

    # 2. 初始化应用公告
    init_res = client.init_app()
    print(f"[*] 应用初始化结果: {init_res.get('msg')}")
    if init_res.get("code") == 200:
        data = init_res["data"]
        print(f"    - 应用名称: {data['app_name']}")
        print(f"    - 官方公告: {data['notice']}")
        print(f"    - 最新版本: {data['latest_version']} (强制更新: {data['force_update']})")

    # 3. 输入卡密或使用测试卡密
    card_key = input("\n[?] 请输入卡密 (或直接回车使用测试卡密): ").strip()
    if not card_key:
        print("未输入卡密，退出。")
        return

    # 4. 执行验证与一机一码绑定
    print(f"\n[*] 正在向服务器校验卡密与绑定设备...")
    ok, msg, info = client.verify(card_key, auto_heartbeat=True)
    if not ok:
        print(f"[-] 授权失败: {msg}")
        return

    print(f"[+] 验证成功！欢迎使用！")
    print(f"    - 授权到期时间: {info.get('expires_at')}")
    print(f"    - 卡密类型: {info.get('card_type')}")
    print(f"    - 会话令牌 (Token): {info.get('session_token')}")
    print(f"    - 云端下发变量: {client.cloud_vars}")

    print("\n[*] 自动心跳保活线程已在后台启动 (按 Ctrl+C 安全退出)...")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n[*] 正在安全退出，注销在线会话...")
        client.logout()
        print("[*] 退出完成！")

if __name__ == "__main__":
    main()
