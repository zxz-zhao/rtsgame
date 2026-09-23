import os
import sys
import json
import time
from pathlib import Path
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, str(Path(__file__).parent.resolve()))
from wecom_sender import load_config, send_full_report, send_markdown, send_image, capture_screenshot

class WeComGatewayHandler(BaseHTTPRequestHandler):
    def _send_json(self, status_code: int, data: dict):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/health":
            cfg = load_config()
            configured = bool(cfg.get("webhook_url") and "YOUR_WECOM" not in cfg.get("webhook_url"))
            self._send_json(200, {
                "status": "ok",
                "service": "WeCom-Webhook-Gateway",
                "webhook_configured": configured,
                "port": cfg.get("port", 5005)
            })
            return

        if parsed.path in ["/capture_and_send", "/trigger"]:
            params = parse_qs(parsed.query)
            title = params.get("title", ["实时屏幕与开发战报推送"])[0]
            items = params.get("item", ["开发者手动触发一键播报与截屏"])
            ok = send_full_report(title=title, items=items, auto_capture=True)
            self._send_json(200, {"success": ok, "message": "截图与战报已触发发送" if ok else "发送失败，请检查 Webhook 配置"})
            return

        self._send_json(404, {"error": "Not Found", "available_endpoints": ["/health", "/send (POST)", "/capture_and_send (GET)"]})

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path in ["/send", "/api/notify"]:
            try:
                length = int(self.headers.get("Content-Length", 0))
                raw = self.rfile.read(length) if length > 0 else b"{}"
                data = json.loads(raw.decode("utf-8"))
            except Exception as e:
                self._send_json(400, {"error": f"Invalid JSON body: {e}"})
                return

            title = data.get("title", "Godot RTS 开发成果更新")
            items = data.get("items", [])
            if not items and data.get("content"):
                items = [str(data["content"])]
            if not items:
                items = ["系统自动触发成果播报"]

            auto_cap = data.get("capture", True)
            img_path = data.get("image_path")

            ok = send_full_report(title=title, items=items, image_path=img_path, auto_capture=auto_cap)
            self._send_json(200, {"success": ok, "delivered": ok})
            return

        self._send_json(404, {"error": "Not Found"})

def run_server(port: int = 5005):
    cfg = load_config()
    server_port = cfg.get("port", port)
    server_address = ("0.0.0.0", server_port)
    httpd = HTTPServer(server_address, WeComGatewayHandler)
    print(f"==================================================")
    print(f"🚀 企业微信 Webhook 本地网关已启动")
    print(f"📡 监听地址: http://127.0.0.1:{server_port}")
    print(f"👉 健康检查: http://127.0.0.1:{server_port}/health")
    print(f"👉 快速触发截屏发送: http://127.0.0.1:{server_port}/capture_and_send")
    print(f"==================================================")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n服务已停止。")

if __name__ == "__main__":
    port = 5005
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            pass
    run_server(port)
