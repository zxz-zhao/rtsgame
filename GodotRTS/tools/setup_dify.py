import urllib.request
import urllib.parse
import http.cookiejar
import json

BASE_URL = "http://127.0.0.1"

# 1. Setup Admin Account
setup_payload = {
    "email": "admin@rts.local",
    "name": "RTS_Admin",
    "password": "Admin123456!",
    "language": "zh-Hans"
}

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

print("Checking setup status...")
try:
    with opener.open(f"{BASE_URL}/console/api/setup") as resp:
        status_data = json.loads(resp.read().decode())
        print("Initial status:", status_data)
        if status_data.get("step") == "not_started":
            print("Registering admin account...")
            req = urllib.request.Request(
                f"{BASE_URL}/console/api/setup",
                data=json.dumps(setup_payload).encode(),
                headers={"Content-Type": "application/json"}
            )
            with opener.open(req) as setup_resp:
                print("Setup response status:", setup_resp.status)
                print(setup_resp.read().decode())
        else:
            print("Setup already finished or in progress.")
except Exception as e:
    print("Setup error:", e)

# 2. Login
print("Logging in...")
login_payload = {
    "email": "admin@rts.local",
    "password": "Admin123456!",
    "remember_me": True
}
try:
    req = urllib.request.Request(
        f"{BASE_URL}/console/api/login",
        data=json.dumps(login_payload).encode(),
        headers={"Content-Type": "application/json"}
    )
    with opener.open(req) as login_resp:
        print("Login status:", login_resp.status)
        login_data = json.loads(login_resp.read().decode())
        token = login_data.get("data", {}).get("access_token")
        print("Access Token:", token[:20] if token else "None")
        print("Cookies:", [c.name for c in cj])
except Exception as e:
    print("Login error:", e)
