import os
import sys
import time
import socket
import logging
import ctypes
import argparse
import subprocess

# Configure absolute paths for log file
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOG_FILE = os.path.join(SCRIPT_DIR, "restart_wlan.log")

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler(LOG_FILE, encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)

TASK_NAME = "RestartWLANOnBoot"

def is_admin():
    """Check if the script is running with administrative privileges."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False

def check_internet(timeout=3):
    """
    Check internet connectivity by attempting to connect to reliable endpoints.
    Uses socket connection to IP:Port first (fast, bypasses DNS), then fallback HTTP.
    """
    targets = [
        ("114.114.114.114", 53),  # Public DNS (China)
        ("8.8.8.8", 53),          # Google DNS
        ("www.baidu.com", 80),    # Baidu HTTP
        ("www.bing.com", 80)      # Bing HTTP
    ]
    for host, port in targets:
        try:
            # Set default timeout for socket operations
            socket.setdefaulttimeout(timeout)
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.connect((host, port))
            s.close()
            return True
        except Exception:
            continue
    return False

def restart_wlan(interface_name="WLAN"):
    """Restart the specified WLAN network interface."""
    logging.info(f"Attempting to restart network interface: {interface_name}")
    
    if not is_admin():
        logging.error("Administrator privileges are required to restart network interfaces!")
        return False

    try:
        # Disable the interface
        logging.info(f"Disabling interface '{interface_name}'...")
        disable_cmd = ["netsh", "interface", "set", "interface", f"name={interface_name}", "admin=disabled"]
        res_dis = subprocess.run(disable_cmd, capture_output=True, text=True, check=True)
        logging.info(f"Disable output: {res_dis.stdout.strip()}")
        
        # Wait 5 seconds to ensure it is completely disabled
        time.sleep(5)
        
        # Enable the interface
        logging.info(f"Enabling interface '{interface_name}'...")
        enable_cmd = ["netsh", "interface", "set", "interface", f"name={interface_name}", "admin=enabled"]
        res_en = subprocess.run(enable_cmd, capture_output=True, text=True, check=True)
        logging.info(f"Enable output: {res_en.stdout.strip()}")
        
        logging.info(f"Interface '{interface_name}' restarted successfully.")
        return True
    except subprocess.CalledProcessError as e:
        logging.error(f"Failed to restart WLAN adapter. Command returned exit code {e.returncode}")
        logging.error(f"Error output: {e.stderr.strip() if e.stderr else str(e)}")
        return False
    except Exception as e:
        logging.error(f"Unexpected error when restarting WLAN: {str(e)}")
        return False

def run_retry_monitoring_loop(max_retries=5, interval_minutes=3, interface_name="WLAN"):
    """
    Monitor internet connectivity on boot:
    - Checks internet connection up to max_retries (default 5 times).
    - Waits interval_minutes (default 3 min) before each check.
    - If connected, logs success and exits.
    - If disconnected, restarts WLAN adapter and re-checks.
    - Exits after max_retries.
    """
    logging.info("Starting boot internet connection monitor...")
    logging.info(f"Configuration: Max Retries = {max_retries}, Interval = {interval_minutes} min, Target Interface = '{interface_name}'")
    
    interval_seconds = interval_minutes * 60
    
    for attempt in range(1, max_retries + 1):
        logging.info(f"Waiting {interval_minutes} minute(s) before check {attempt}/{max_retries}...")
        time.sleep(interval_seconds)
        
        logging.info(f"Check {attempt}/{max_retries}: Testing internet connectivity...")
        if check_internet():
            logging.info(f"Internet connection detected on check {attempt}/{max_retries}! System is online. Exiting monitor.")
            return True
        
        logging.warning(f"Check {attempt}/{max_retries}: No internet connection detected! Restarting WLAN adapter '{interface_name}'...")
        success = restart_wlan(interface_name)
        if success:
            logging.info("WLAN restart completed. Waiting 10 seconds for IP address assignment...")
            time.sleep(10)
            if check_internet():
                logging.info("Internet connection restored successfully after WLAN restart! Exiting monitor.")
                return True
            else:
                logging.warning("Internet still offline after WLAN restart.")
        else:
            logging.error("Failed to execute WLAN restart command.")
            
    logging.error(f"Completed all {max_retries} checks. Internet connection could not be established. Exiting monitor.")
    return False

def install_task():
    """Register this script to run at system boot using Windows Task Scheduler."""
    if not is_admin():
        print("Error: You must run this script with Administrator privileges to install the boot task.")
        print("Please run command prompt / powershell as Administrator, then run this command again.")
        return False
    
    python_exe = sys.executable
    script_path = os.path.abspath(__file__)
    
    # Construct the command to run the script.
    task_command = f'"{python_exe}" "{script_path}"'
    
    # We create a Task Scheduled on Startup (/sc onstart) running under SYSTEM account (/ru "SYSTEM")
    # This runs with the highest privileges before any user logs in.
    cmd = [
        "schtasks", "/create", "/f",
        "/tn", TASK_NAME,
        "/tr", task_command,
        "/sc", "onstart",
        "/ru", "SYSTEM"
    ]
    
    try:
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
        print(f"Successfully registered startup task '{TASK_NAME}'.")
        print(res.stdout.strip())
        logging.info(f"Startup task '{TASK_NAME}' installed successfully.")
        return True
    except subprocess.CalledProcessError as e:
        print(f"Failed to register startup task. Exit code: {e.returncode}")
        print(e.stderr.strip() if e.stderr else str(e))
        logging.error(f"Installation of startup task failed: {e.stderr.strip() if e.stderr else str(e)}")
        return False

def uninstall_task():
    """Remove the boot task from Windows Task Scheduler."""
    if not is_admin():
        print("Error: You must run this script with Administrator privileges to uninstall the boot task.")
        return False
        
    cmd = ["schtasks", "/delete", "/tn", TASK_NAME, "/f"]
    try:
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
        print(f"Successfully unregistered startup task '{TASK_NAME}'.")
        print(res.stdout.strip())
        logging.info(f"Startup task '{TASK_NAME}' uninstalled successfully.")
        return True
    except subprocess.CalledProcessError as e:
        print(f"Failed to unregister startup task. Exit code: {e.returncode}")
        print(e.stderr.strip() if e.stderr else str(e))
        logging.error(f"Uninstallation of startup task failed: {e.stderr.strip() if e.stderr else str(e)}")
        return False

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Check internet connection on boot and restart WLAN if offline (retries 5 times every 3 minutes).")
    parser.add_argument("--install", action="store_true", help="Install this script to run automatically at boot (requires admin privileges)")
    parser.add_argument("--uninstall", action="store_true", help="Remove the registered startup task (requires admin privileges)")
    parser.add_argument("--interface", type=str, default="WLAN", help="Name of the wireless network interface (default: WLAN)")
    parser.add_argument("--retries", type=int, default=5, help="Maximum number of detection attempts (default: 5)")
    parser.add_argument("--interval", type=int, default=3, help="Interval in minutes between checks (default: 3)")
    
    args = parser.parse_args()
    
    if args.install:
        install_task()
    elif args.uninstall:
        uninstall_task()
    else:
        # Run monitoring loop
        run_retry_monitoring_loop(max_retries=args.retries, interval_minutes=args.interval, interface_name=args.interface)
