import os
import glob
import json
import base64
import time
import urllib.request
import urllib.parse
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

COCKPIT_DIR = r"C:\Users\Administrator\.antigravity_cockpit"
KEY_PATH = os.path.join(COCKPIT_DIR, "secure-account-storage.key")
ACCOUNTS_DIR = os.path.join(COCKPIT_DIR, "accounts")
TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
ACCOUNTS_POOL_FILE = os.path.join(TOOLS_DIR, "accounts_pool.json")
ACTIVE_ACCOUNT_FILE = os.path.join(TOOLS_DIR, "active_account.json")

_oauth_cfg_file = os.path.join(TOOLS_DIR, ".cockpit_oauth.json")
_oauth_cfg = {}
if os.path.exists(_oauth_cfg_file):
    try:
        with open(_oauth_cfg_file, "r", encoding="utf-8") as _fp:
            _oauth_cfg = json.load(_fp)
    except Exception:
        pass

OAUTH_CLIENT_ID = os.environ.get("COCKPIT_CLIENT_ID") or _oauth_cfg.get("client_id", "")
OAUTH_CLIENT_SECRET = os.environ.get("COCKPIT_CLIENT_SECRET") or _oauth_cfg.get("client_secret", "")
LOCAL_PROXY = "http://127.0.0.1:7897"

def refresh_oauth_token(refresh_token: str) -> dict:
    data = urllib.parse.urlencode({
        "client_id": OAUTH_CLIENT_ID,
        "client_secret": OAUTH_CLIENT_SECRET,
        "refresh_token": refresh_token,
        "grant_type": "refresh_token"
    }).encode("utf-8")

    req = urllib.request.Request(
        "https://oauth2.googleapis.com/token",
        data=data,
        headers={"Content-Type": "application/x-www-form-urlencoded"}
    )
    proxy_handler = urllib.request.ProxyHandler({
        "http": LOCAL_PROXY,
        "https": LOCAL_PROXY
    })
    opener = urllib.request.build_opener(proxy_handler)
    with opener.open(req, timeout=12) as resp:
        return json.loads(resp.read().decode("utf-8"))

def sync_accounts():
    if not os.path.exists(KEY_PATH) or not os.path.exists(ACCOUNTS_DIR):
        print(f"[Sync] Cockpit path not found: {COCKPIT_DIR}")
        return

    with open(KEY_PATH, "rb") as fp:
        key_raw = base64.b64decode(fp.read().strip())
    aesgcm = AESGCM(key_raw)

    healthy_accounts = []
    exhausted_accounts = []

    for f in glob.glob(os.path.join(ACCOUNTS_DIR, "*.json")):
        try:
            with open(f, "r", encoding="utf-8") as fp:
                data = json.load(fp)

            if data.get("algorithm") == "AES-256-GCM":
                nonce = base64.b64decode(data["nonce"])
                ct = base64.b64decode(data["ciphertext"])
                acc = json.loads(aesgcm.decrypt(nonce, ct, None).decode("utf-8"))
            else:
                acc = data

            email = acc.get("email")
            disabled = acc.get("disabled", False)
            token_obj = acc.get("token", {})
            refresh_token = token_obj.get("refresh_token")
            access_token = token_obj.get("access_token")

            if disabled or not refresh_token:
                continue

            quota = acc.get("quota", {})
            claude_pct = next((m["percentage"] for m in quota.get("models", []) if m.get("name") == "claude-sonnet-4-6"), 0)
            gemini_pct = next((m["percentage"] for m in quota.get("models", []) if m.get("name") == "gemini-weekly"), 0)

            # Check if nok0870207369 triggered 429 quota lock
            is_exhausted = (claude_pct <= 5 and gemini_pct <= 5) or email == "ottismcardle@gmail.com"

            entry = {
                "email": email,
                "claude_pct": claude_pct,
                "gemini_pct": gemini_pct,
                "token": {
                    "access_token": access_token,
                    "refresh_token": refresh_token,
                    "expiry_timestamp": token_obj.get("expiry_timestamp", int(time.time()) + 3600)
                }
            }

            if is_exhausted:
                exhausted_accounts.append(entry)
            else:
                healthy_accounts.append(entry)

        except Exception as e:
            print(f"[Sync] Error processing account {f}: {e}")

    # Sort healthy accounts by claude percentage descending
    healthy_accounts.sort(key=lambda a: (a["claude_pct"], a["gemini_pct"]), reverse=True)

    # Immediately refresh the top account's access token to ensure it's 100% active
    if healthy_accounts:
        top_acc = healthy_accounts[0]
        try:
            print(f"[Sync] Pre-refreshing token for top account: {top_acc['email']}...")
            refreshed = refresh_oauth_token(top_acc["token"]["refresh_token"])
            top_acc["token"]["access_token"] = refreshed["access_token"]
            top_acc["token"]["expiry_timestamp"] = int(time.time()) + refreshed.get("expires_in", 3600)
            print(f"[Sync] Token refreshed successfully.")
        except Exception as e:
            print(f"[Sync] Token refresh error: {e}")

    all_accounts = healthy_accounts + exhausted_accounts
    print(f"\n[Sync] Found {len(healthy_accounts)} healthy accounts and {len(exhausted_accounts)} exhausted accounts:")
    for a in all_accounts:
        print(f"  - {a['email']} (Claude Sonnet: {a['claude_pct']}%, Gemini Weekly: {a['gemini_pct']}%)")

    # Save to accounts_pool.json
    with open(ACCOUNTS_POOL_FILE, "w", encoding="utf-8") as fp:
        json.dump(all_accounts, fp, indent=2, ensure_ascii=False)
    print(f"[Sync] Wrote {len(all_accounts)} accounts to {ACCOUNTS_POOL_FILE}")

    # Save top healthy to active_account.json
    if healthy_accounts:
        with open(ACTIVE_ACCOUNT_FILE, "w", encoding="utf-8") as fp:
            json.dump({
                "email": healthy_accounts[0]["email"],
                "token": healthy_accounts[0]["token"]
            }, fp, indent=2, ensure_ascii=False)
        print(f"[Sync] Wrote active account {healthy_accounts[0]['email']} to {ACTIVE_ACCOUNT_FILE}")

if __name__ == "__main__":
    sync_accounts()
