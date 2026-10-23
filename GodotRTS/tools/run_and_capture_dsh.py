import subprocess
import time
import os

log_out = r"e:\code\c++\UnityRTS\GodotRTS\tools\dsh_stdout.log"
log_err = r"e:\code\c++\UnityRTS\GodotRTS\tools\dsh_stderr.log"

env = os.environ.copy()
env["ELECTRON_ENABLE_LOGGING"] = "1"
env["ELECTRON_ENABLE_STACK_DUMPING"] = "1"
env["DEBUG"] = "*"

with open(log_out, "wb") as fo, open(log_err, "wb") as fe:
    p = subprocess.Popen(
        [r"H:\deepseek\DeepSeek Harness.exe", "--enable-logging", "--v=1"],
        stdout=fo,
        stderr=fe,
        env=env
    )
    print("Process started PID:", p.pid)
    for i in range(10):
        time.sleep(1)
        if p.poll() is not None:
            print(f"Process exited at second {i+1} with code {p.returncode}")
            break
    if p.poll() is None:
        print("Still running after 10s!")
