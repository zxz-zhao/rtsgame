import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import json
import time
import hashlib
import urllib.parse
import urllib.request
from starlette.applications import Starlette
from starlette.responses import JSONResponse, HTMLResponse, RedirectResponse
from starlette.routing import Route
import uvicorn

PORT = 8005
MOCK_KEY = "mock_alipay_secret_2026"
MOCK_PID = "1001"

# 内存保存最近的订单
recent_orders = {}

def sign_epay(params: dict, key: str) -> str:
    # 易支付标准签名规则：排除 sign, sign_type 和空值，按 key 字典序升序拼接为 a=1&b=2，最后拼接 key，计算 md5
    filtered = {k: v for k, v in params.items() if k not in ["sign", "sign_type"] and v != "" and v is not None}
    sorted_items = sorted(filtered.items(), key=lambda x: x[0])
    query_str = "&".join([f"{k}={v}" for k, v in sorted_items])
    sign_str = query_str + key
    return hashlib.md5(sign_str.encode("utf-8")).hexdigest()

async def submit_page(request):
    # 处理 New-API 发来的支付跳转请求 (GET 或 POST)
    if request.method == "GET":
        params = dict(request.query_params)
    else:
        try:
            form = await request.form()
            params = dict(form)
        except Exception:
            params = {}
    
    out_trade_no = params.get("out_trade_no", f"ORD{int(time.time())}")
    money = params.get("money", "10.00")
    name = params.get("name", "New-API额度充值")
    notify_url = params.get("notify_url", "http://127.0.0.1:3000/api/user/epay/notify")
    return_url = params.get("return_url", "http://127.0.0.1:3000/topup")

    recent_orders[out_trade_no] = {
        "params": params,
        "out_trade_no": out_trade_no,
        "money": money,
        "notify_url": notify_url,
        "return_url": return_url,
        "time": time.time()
    }

    # 渲染一个仿真的支付宝收银台页面
    html = f"""
    <!DOCTYPE html>
    <html lang="zh-CN">
    <head>
        <meta charset="UTF-8">
        <title>支付宝收银台 - 仿真付款测试</title>
        <style>
            body {{ font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f0f2f5; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; }}
            .card {{ background: #fff; border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.08); width: 420px; padding: 32px; text-align: center; }}
            .logo {{ color: #1677ff; font-size: 26px; font-weight: bold; margin-bottom: 20px; display: flex; align-items: center; justify-content: center; gap: 8px; }}
            .price {{ font-size: 36px; font-weight: 700; color: #1f1f1f; margin: 16px 0; }}
            .price span {{ font-size: 20px; }}
            .order-info {{ background: #fafafa; border-radius: 8px; padding: 16px; margin: 20px 0; font-size: 14px; color: #666; text-align: left; line-height: 1.8; }}
            .qr-box {{ border: 2px dashed #1677ff; border-radius: 8px; padding: 20px; margin: 20px auto; width: 160px; height: 160px; display: flex; flex-direction: column; justify-content: center; align-items: center; background: #e6f4ff; color: #1677ff; font-weight: 600; }}
            .btn {{ background: #1677ff; color: #fff; border: none; border-radius: 8px; padding: 14px 28px; font-size: 16px; font-weight: 600; cursor: pointer; width: 100%; transition: 0.2s; }}
            .btn:hover {{ background: #0958d9; }}
        </style>
    </head>
    <body>
        <div class="card">
            <div class="logo">
                <svg width="28" height="28" viewBox="0 0 1024 1024" fill="#1677ff"><path d="M848 128H176c-26.5 0-48 21.5-48 48v672c0 26.5 21.5 48 48 48h672c26.5 0 48-21.5 48-48V176c0-26.5-21.5-48-48-48zm-96 688H272V640h480v176zm0-240H272v-80h480v80zm0-144H272V272h480v160z"/></svg>
                支付宝收银台 (Alipay)
            </div>
            <div style="color: #8c8c8c; font-size: 14px;">商品名称：{name}</div>
            <div class="price"><span>￥</span>{money}</div>
            <div class="order-info">
                <div>商户订单号：<b>{out_trade_no}</b></div>
                <div>支付方式：<b>支付宝扫码支付</b></div>
                <div>状态：<b>等待买家扫码付款...</b></div>
            </div>
            <div class="qr-box">
                <div style="font-size: 32px; margin-bottom: 8px;">📱</div>
                [支付宝扫码区]
            </div>
            <form action="/simulate_pay" method="POST">
                <input type="hidden" name="out_trade_no" value="{out_trade_no}">
                <button type="submit" class="btn">🚀 模拟买家扫码成功并付款</button>
            </form>
        </div>
    </body>
    </html>
    """
    return HTMLResponse(html)

async def simulate_pay(request):
    form = await request.form()
    out_trade_no = form.get("out_trade_no")
    order = recent_orders.get(out_trade_no)
    if not order:
        return HTMLResponse(f"<h3>订单不存在或已过期: {out_trade_no}</h3>")
    
    trade_no = f"ALI{int(time.time())}{out_trade_no[-4:]}"
    money = order["money"]
    notify_url = order["notify_url"]
    return_url = order["return_url"]

    # 构造易支付异步回调参数
    notify_params = {
        "pid": MOCK_PID,
        "trade_no": trade_no,
        "out_trade_no": out_trade_no,
        "type": "alipay",
        "name": "New-API额度充值",
        "money": money,
        "trade_status": "TRADE_SUCCESS"
    }
    notify_params["sign"] = sign_epay(notify_params, MOCK_KEY)
    notify_params["sign_type"] = "MD5"

    # 向 New-API 发起异步回调 (GET)
    qs = urllib.parse.urlencode(notify_params)
    call_url = f"{notify_url}?{qs}"
    try:
        req = urllib.request.Request(call_url, headers={"User-Agent": "Epay-Mock-Gateway"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            resp_text = resp.read().decode("utf-8", errors="replace")
            print(f"[Epay Gateway] 回调 New-API 结果: {resp_text}")
    except Exception as e:
        print(f"[Epay Gateway] 回调 New-API 失败: {e}")
        resp_text = f"Callback error: {e}"

    html = f"""
    <!DOCTYPE html>
    <html lang="zh-CN">
    <head><meta charset="UTF-8"><title>支付成功</title><style>body {{ font-family: sans-serif; text-align: center; padding: 50px; background: #f6ffed; }} .box {{ background: white; border-radius: 12px; padding: 40px; display: inline-block; box-shadow: 0 4px 20px rgba(0,0,0,0.06); }}</style></head>
    <body>
        <div class="box">
            <h1 style="color: #52c41a; font-size: 48px; margin: 0 0 16px 0;">🎉</h1>
            <h2 style="color: #237804; margin: 0 0 12px 0;">支付宝付款成功！</h2>
            <p style="color: #595959;">订单号：{out_trade_no} | 金额：￥{money}</p>
            <p style="color: #8c8c8c; font-size: 13px;">New-API 异步通知响应: <b>{resp_text}</b></p>
            <div style="margin-top: 24px;">
                <a href="{return_url}" style="background: #52c41a; color: white; text-decoration: none; padding: 12px 24px; border-radius: 6px; font-weight: bold;">返回控制台查看充值额度</a>
            </div>
        </div>
    </body>
    </html>
    """
    return HTMLResponse(html)

routes = [
    Route("/submit.php", submit_page, methods=["GET", "POST"]),
    Route("/simulate_pay", simulate_pay, methods=["POST"]),
]

app = Starlette(routes=routes)

if __name__ == "__main__":
    print(f"Starting Mock Alipay Epay Gateway on port {PORT}...")
    uvicorn.run(app, host="127.0.0.1", port=PORT, log_level="warning")
