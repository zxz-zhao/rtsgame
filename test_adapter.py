import sys
import ctypes
import subprocess
import time

# 确保控制台输出 UTF-8
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

def is_admin():
    """检查当前脚本是否以管理员权限运行"""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False

def run_ps(ps_command: str):
    """运行 PowerShell 指令并返回 UTF-8 文本结果"""
    full_cmd = f'powershell -NoProfile -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {ps_command}"'
    return subprocess.run(full_cmd, capture_output=True, text=True, encoding='utf-8', errors='ignore', shell=True)

def list_adapters():
    """列出当前系统的所有网络适配器状态"""
    print("=== 当前网络适配器列表 ===")
    res = run_ps("Get-NetAdapter | Select-Object Name, Status, InterfaceDescription | Format-Table -AutoSize")
    print(res.stdout if res.stdout else "未能获取网卡列表。")

def set_adapter_state(adapter_name: str, enable: bool):
    """开启或禁用指定的网络适配器"""
    action_str = "开启" if enable else "禁用"
    action_cmd = "Enable-NetAdapter" if enable else "Disable-NetAdapter"
    
    print(f"\n[+] 正在尝试【{action_str}】网卡适配器: {adapter_name} ...")
    res = run_ps(f"{action_cmd} -Name '{adapter_name}' -Confirm:$false")
    
    if res.returncode == 0:
        print(f"[✓] 适配器【{adapter_name}】{action_str}操作成功！")
    else:
        err_msg = res.stderr.strip() or res.stdout.strip()
        print(f"[✗] 操作失败，错误信息：\n{err_msg}")
        if "Access is denied" in err_msg or "拒绝访问" in err_msg or "Requires Administrator" in err_msg:
            print("\n[!] 提示：请确保你以“管理员身份”运行命令行/PowerShell 后再执行此脚本。")

def main():
    if not is_admin():
        print("=" * 60)
        print("[!] 注意：在 Windows 下启用/禁用网络适配器必须具备管理员权限！")
        print("[!] 请右键以“管理员身份运行” CMD 或 PowerShell，再执行此 Python 脚本。")
        print("=" * 60 + "\n")
    
    list_adapters()
    
    if len(sys.argv) < 3:
        print("\n【使用方法】")
        print("  py -3 test_adapter.py <网卡名称> <操作指令>")
        print("\n【常用测试命令示例】")
        print("  1. 禁用网卡:  py -3 test_adapter.py \"WLAN\" disable")
        print("  2. 开启网卡:  py -3 test_adapter.py \"WLAN\" enable")
        print("  3. 自动测试:  py -3 test_adapter.py \"WLAN\" test  (禁用 5 秒后自动重新启用)")
        return

    adapter_name = sys.argv[1]
    action = sys.argv[2].lower()

    if action == "disable":
        set_adapter_state(adapter_name, enable=False)
    elif action == "enable":
        set_adapter_state(adapter_name, enable=True)
    elif action == "test":
        print(f"\n=================== 开始对【{adapter_name}】进行断网重连测试 ===================")
        set_adapter_state(adapter_name, enable=False)
        print("\n[i] 正在模拟断线，等待 5 秒...")
        time.sleep(5)
        set_adapter_state(adapter_name, enable=True)
        print("\n[i] 正在恢复网络，等待 2 秒后刷新状态...")
        time.sleep(2)
        list_adapters()
    else:
        print(f"[!] 未知指令 '{action}'，可选操作：enable / disable / test")

if __name__ == "__main__":
    main()
