import subprocess
import time
import os
import sys

def main():
    docker_exe = r"D:\Program Files\Docker\Docker Desktop.exe"
    print(f"Starting {docker_exe} detached...")
    
    # Use wmic to create completely independent process outside current job object
    cmd = f'wmic process call create "{docker_exe}"'
    res = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    print("WMIC Output:", res.stdout.strip())
    
    # Now monitor docker engine status
    docker_cli = r"D:\Program Files\Docker\resources\bin\docker.exe"
    print("Monitoring Docker daemon readiness...")
    for i in range(60):
        time.sleep(3)
        check = subprocess.run([docker_cli, "info"], capture_output=True, text=True)
        if check.returncode == 0:
            print(f"Docker engine ready after {(i+1)*3}s!")
            return 0
        else:
            if i % 5 == 0:
                print(f"Still waiting... ({(i+1)*3}s)")
                
    print("Timed out waiting for Docker engine.")
    return 1

if __name__ == "__main__":
    sys.exit(main())
