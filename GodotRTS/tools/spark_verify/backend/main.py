import os
import sys
import time
import asyncio
import secrets
from typing import Dict, Any, Optional, List
from datetime import datetime
from fastapi import FastAPI, Request, HTTPException, Depends, Header
from fastapi.responses import JSONResponse, FileResponse
from fastapi.staticfiles import StaticFiles
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

import database
import crypto_utils

app = FastAPI(title="SparkVerify 极验网络验证系统", version="1.0.0")

# 启用 CORS 跨域
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

ADMIN_PASSWORD = os.environ.get("SPARK_ADMIN_PWD", "admin123")
ADMIN_SECRET = "SPARK_ADMIN_SESSION_TOKEN_8899"

# ----------------- 数据模型 -----------------

class ClientInitReq(BaseModel):
    app_id: str
    version: Optional[str] = "1.0.0"
    hwid: Optional[str] = ""

class ClientVerifyReq(BaseModel):
    app_id: str
    card_key: str
    hwid: str
    timestamp: int
    sign: Optional[str] = ""

class ClientHeartbeatReq(BaseModel):
    app_id: str
    session_token: str
    hwid: str
    timestamp: int
    sign: Optional[str] = ""

class ClientUnbindReq(BaseModel):
    app_id: str
    card_key: str
    hwid: str
    timestamp: int
    sign: Optional[str] = ""

class AdminLoginReq(BaseModel):
    username: str
    password: str

class CardGenerateReq(BaseModel):
    app_id: str
    card_type: str
    count: int
    prefix: Optional[str] = "SV"
    custom_days: Optional[float] = 30.0
    custom_count: Optional[int] = 0
    note: Optional[str] = ""

class CardActionReq(BaseModel):
    card_key: str
    status: Optional[int] = 0

class CloudVarReq(BaseModel):
    app_id: str
    var_key: str
    var_value: str
    description: Optional[str] = ""

# ----------------- 会话 Token 与鉴权依赖 -----------------

SESSION_TOKENS: Dict[str, Dict[str, Any]] = {
    ADMIN_SECRET: {"username": "admin", "role": "ADMIN", "can_encrypt": 1}
}

def get_current_admin(authorization: Optional[str] = Header(None)) -> Dict[str, Any]:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="请先登录管理后台")
    token = authorization.split(" ")[1]
    if token in SESSION_TOKENS:
        return SESSION_TOKENS[token]
    # 动态查库
    cursor_admin = database.get_admin(token.replace("TOKEN_", "").split("_")[0]) if "TOKEN_" in token else None
    if token == ADMIN_SECRET:
        return {"username": "admin", "role": "ADMIN", "can_encrypt": 1}
    raise HTTPException(status_code=401, detail="管理凭据已失效或错误，请重新登录")

def verify_admin_auth(user: Dict[str, Any] = Depends(get_current_admin)):
    return user

def verify_super_admin(user: Dict[str, Any] = Depends(get_current_admin)):
    if user.get("role") != "SUPER_ADMIN":
        raise HTTPException(status_code=403, detail="无权操作：该功能仅限平台最高【超级管理员】访问")
    return user

# ----------------- 客户端接口 -----------------

@app.post("/api/v1/client/init")
async def client_init(req: ClientInitReq, request: Request):
    app_data = database.get_app(req.app_id)
    if not app_data:
        return JSONResponse({"code": 404, "msg": "应用不存在或已被禁用"})
    if app_data["status"] == 0:
        return JSONResponse({"code": 403, "msg": "该应用目前处于维护停用状态"})
        
    client_ip = request.client.host if request.client else "127.0.0.1"
    
    # 检查版本
    latest_ver = app_data["current_version"]
    has_update = (req.version != latest_ver)
    
    return {
        "code": 200,
        "msg": "初始化成功",
        "data": {
            "app_name": app_data["app_name"],
            "notice": app_data["notice"],
            "latest_version": latest_ver,
            "has_update": has_update,
            "force_update": bool(app_data["force_update"]) and has_update,
            "download_url": app_data["download_url"],
            "server_time": int(time.time())
        }
    }

@app.post("/api/v1/client/verify")
async def client_verify(req: ClientVerifyReq, request: Request):
    app_data = database.get_app(req.app_id)
    if not app_data:
        return JSONResponse({"code": 404, "msg": "应用不存在"})
        
    # 时间戳防重放校验 (容差 5 分钟)
    now_ts = int(time.time())
    if abs(now_ts - req.timestamp) > 300:
        return JSONResponse({"code": 400, "msg": "请求时间戳超时或偏差过大，防重放校验失败"})
        
    # HMAC-SHA256 签名校验
    if req.sign:
        sign_str = f"app_id={req.app_id}&card_key={req.card_key}&hwid={req.hwid}&timestamp={req.timestamp}"
        if not crypto_utils.verify_signature(sign_str, req.sign, app_data["app_secret"]):
            return JSONResponse({"code": 401, "msg": "安全通信签名校验失败，数据可能被篡改"})
            
    client_ip = request.client.host if request.client else "127.0.0.1"
    
    ok, msg, session_info = database.register_or_activate_card(req.card_key, req.hwid, client_ip)
    if not ok:
        return JSONResponse({"code": 403, "msg": msg})
        
    # 获取下发云变量
    cloud_vars = database.get_cloud_vars(req.app_id)
    session_info["cloud_vars"] = cloud_vars
    session_info["server_time"] = now_ts
    
    return {
        "code": 200,
        "msg": "卡密授权验证通过",
        "data": session_info
    }

@app.post("/api/v1/client/heartbeat")
async def client_heartbeat(req: ClientHeartbeatReq, request: Request):
    app_data = database.get_app(req.app_id)
    if not app_data:
        return JSONResponse({"code": 404, "msg": "应用不存在"})
        
    client_ip = request.client.host if request.client else "127.0.0.1"
    ok, msg = database.process_heartbeat(req.session_token, req.hwid, client_ip)
    if not ok:
        return JSONResponse({"code": 403, "msg": msg})
        
    return {
        "code": 200,
        "msg": "心跳保活成功",
        "data": {
            "status": "alive",
            "server_time": int(time.time())
        }
    }

@app.post("/api/v1/client/unbind")
async def client_unbind(req: ClientUnbindReq, request: Request):
    app_data = database.get_app(req.app_id)
    if not app_data:
        return JSONResponse({"code": 404, "msg": "应用不存在"})
        
    ok, msg = database.unbind_card(req.card_key, admin_force=False)
    if not ok:
        return JSONResponse({"code": 400, "msg": msg})
        
    return {
        "code": 200,
        "msg": msg
    }

@app.post("/api/v1/client/logout")
async def client_logout(session_token: str):
    database.terminate_session(session_token)
    return {"code": 200, "msg": "已安全退出"}

# ----------------- 管理后台接口 -----------------

@app.post("/api/v1/admin/login")
async def admin_login(req: AdminLoginReq):
    admin = database.get_admin(req.username)
    if not admin:
        if req.username == "admin" and req.password == ADMIN_PASSWORD:
            token = ADMIN_SECRET
            SESSION_TOKENS[token] = {"username": "admin", "role": "ADMIN", "can_encrypt": 1}
            return {"code": 200, "msg": "登录成功", "token": token, "role": "ADMIN", "can_encrypt": 1, "username": "admin"}
        return JSONResponse({"code": 401, "msg": "管理端账号不存在"}, status_code=401)

    if admin["password_hash"] != req.password:
        return JSONResponse({"code": 401, "msg": "用户名或密码错误"}, status_code=401)

    if admin.get("status", 1) == 0:
        return JSONResponse({"code": 403, "msg": "该管理端账号已被总控超级管理员封禁停用！"}, status_code=403)

    token = f"TOKEN_{admin['role']}_{secrets.token_hex(16)}"
    SESSION_TOKENS[token] = {
        "username": admin["username"],
        "role": admin["role"],
        "can_encrypt": admin.get("can_encrypt", 1)
    }
    return {
        "code": 200,
        "msg": "登录成功",
        "token": token,
        "role": admin["role"],
        "can_encrypt": admin.get("can_encrypt", 1),
        "username": admin["username"]
    }

@app.get("/api/v1/admin/overview")
async def admin_overview(app_id: Optional[str] = None, user: Dict[str, Any] = Depends(verify_admin_auth)):
    stats = database.get_dashboard_stats(app_id)
    return {"code": 200, "data": stats}

@app.get("/api/v1/admin/apps")
async def admin_apps(user: Dict[str, Any] = Depends(verify_admin_auth)):
    owner = None if user.get("role") == "SUPER_ADMIN" else user.get("username")
    apps = database.list_apps(owner_admin=owner)
    return {"code": 200, "data": apps}

# ================= 超级管理员总控专属接口 =================

@app.get("/api/v1/super/overview")
async def super_overview(super_user: Dict[str, Any] = Depends(verify_super_admin)):
    """超级管理员全局使用信息大盘"""
    data = database.get_super_overview()
    return {"code": 200, "data": data}

@app.get("/api/v1/super/admins")
async def super_list_admins(super_user: Dict[str, Any] = Depends(verify_super_admin)):
    """超级管理员查看系统内所有下级管理端"""
    admins = database.list_all_admins()
    return {"code": 200, "data": admins}

class ToggleEncryptReq(BaseModel):
    username: str
    can_encrypt: int

@app.post("/api/v1/super/admins/toggle_encrypt")
async def super_toggle_encrypt(req: ToggleEncryptReq, super_user: Dict[str, Any] = Depends(verify_super_admin)):
    """超级管理员核心权限：一键停止或恢复指定管理端的加密端工作！"""
    ok, msg = database.set_admin_encrypt_status(req.username, req.can_encrypt)
    return {"code": 200 if ok else 400, "msg": msg}

class AdminStatusReq(BaseModel):
    username: str
    status: int

@app.post("/api/v1/super/admins/status")
async def super_admin_status(req: AdminStatusReq, super_user: Dict[str, Any] = Depends(verify_super_admin)):
    """超级管理员冻结或解冻管理端账号"""
    ok, msg = database.set_admin_status(req.username, req.status)
    return {"code": 200 if ok else 400, "msg": msg}

class CreateAdminReq(BaseModel):
    username: str
    password: str
    note: Optional[str] = ""

@app.post("/api/v1/super/admins/create")
async def super_admin_create(req: CreateAdminReq, super_user: Dict[str, Any] = Depends(verify_super_admin)):
    """超级管理员开通新的下级管理端"""
    ok, msg = database.create_admin(req.username, req.password, role="ADMIN", note=req.note)
    return {"code": 200 if ok else 400, "msg": msg}

# ================= 加密端权限核验接口 (Packer Permission Check) =================

class PackerCheckReq(BaseModel):
    username: str

@app.post("/api/v1/packer/check_permission")
async def packer_check_permission(req: PackerCheckReq):
    """加密端每次执行打包前向上汇报并核验权限"""
    ok, msg, admin_info = database.check_admin_packer_permission(req.username)
    if not ok:
        return JSONResponse({"code": 403, "can_encrypt": 0, "msg": msg}, status_code=403)
    return {
        "code": 200,
        "can_encrypt": 1,
        "msg": "加密端工作权限正常",
        "data": {
            "username": admin_info.get("username"),
            "role": admin_info.get("role"),
            "can_encrypt": admin_info.get("can_encrypt", 1)
        }
    }

@app.post("/api/v1/admin/apps/update")
async def admin_apps_update(app_id: str, payload: Dict[str, Any], auth: bool = Depends(verify_admin_auth)):
    database.update_app(app_id, payload)
    return {"code": 200, "msg": "更新软件配置成功"}

@app.get("/api/v1/admin/cards")
async def admin_cards(
    app_id: Optional[str] = None,
    status: Optional[int] = None,
    search: str = "",
    page: int = 1,
    limit: int = 50,
    auth: bool = Depends(verify_admin_auth)
):
    offset = (page - 1) * limit
    cards, total = database.list_cards(app_id, status, search, limit, offset)
    return {
        "code": 200,
        "data": {
            "list": cards,
            "total": total,
            "page": page,
            "limit": limit
        }
    }

@app.post("/api/v1/admin/cards/generate")
async def admin_cards_generate(req: CardGenerateReq, auth: bool = Depends(verify_admin_auth)):
    keys = database.generate_cards(
        app_id=req.app_id,
        card_type=req.card_type,
        count=req.count,
        prefix=req.prefix,
        custom_days=req.custom_days,
        custom_count=req.custom_count,
        note=req.note
    )
    return {"code": 200, "msg": f"成功生成 {len(keys)} 张卡密", "data": keys}

@app.post("/api/v1/admin/cards/status")
async def admin_cards_status(req: CardActionReq, auth: bool = Depends(verify_admin_auth)):
    ok = database.update_card_status(req.card_key, req.status)
    return {"code": 200 if ok else 400, "msg": "操作完成" if ok else "卡密不存在"}

@app.post("/api/v1/admin/cards/unbind")
async def admin_cards_unbind(card_key: str, auth: bool = Depends(verify_admin_auth)):
    ok, msg = database.unbind_card(card_key, admin_force=True)
    return {"code": 200 if ok else 400, "msg": msg}

@app.post("/api/v1/admin/cards/delete")
async def admin_cards_delete(card_key: str, auth: bool = Depends(verify_admin_auth)):
    ok = database.delete_card(card_key)
    return {"code": 200 if ok else 400, "msg": "删除卡密成功" if ok else "卡密不存在"}

@app.get("/api/v1/admin/sessions")
async def admin_sessions(app_id: Optional[str] = None, auth: bool = Depends(verify_admin_auth)):
    sessions = database.list_active_sessions(app_id)
    return {"code": 200, "data": sessions}

@app.post("/api/v1/admin/sessions/kick")
async def admin_sessions_kick(session_token: str, auth: bool = Depends(verify_admin_auth)):
    database.terminate_session(session_token)
    return {"code": 200, "msg": "已强制将会话踢下线"}

@app.get("/api/v1/admin/cloud_vars")
async def admin_cloud_vars(app_id: str, auth: bool = Depends(verify_admin_auth)):
    vars_list = database.list_cloud_vars_full(app_id)
    return {"code": 200, "data": vars_list}

@app.post("/api/v1/admin/cloud_vars/save")
async def admin_cloud_vars_save(req: CloudVarReq, auth: bool = Depends(verify_admin_auth)):
    database.save_cloud_var(req.app_id, req.var_key, req.var_value, req.description)
    return {"code": 200, "msg": "保存云变量成功"}

@app.post("/api/v1/admin/cloud_vars/delete")
async def admin_cloud_vars_delete(var_id: int, auth: bool = Depends(verify_admin_auth)):
    database.delete_cloud_var(var_id)
    return {"code": 200, "msg": "删除云变量成功"}

@app.get("/api/v1/admin/logs")
async def admin_logs(app_id: Optional[str] = None, limit: int = 50, auth: bool = Depends(verify_admin_auth)):
    logs = database.list_logs(app_id, limit)
    return {"code": 200, "data": logs}

# ----------------- 静态 Web 资源 -----------------
WEB_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "web"))
if os.path.exists(WEB_DIR):
    app.mount("/static", StaticFiles(directory=WEB_DIR), name="static")

@app.get("/")
@app.get("/admin")
async def serve_index():
    index_file = os.path.join(WEB_DIR, "index.html")
    if os.path.exists(index_file):
        return FileResponse(index_file)
    return {"message": "SparkVerify Service Running. Web dashboard coming up!"}

# ----------------- 后台心跳守护循环 -----------------

async def heartbeat_janitor_loop():
    """后台异步任务：每隔 15 秒扫描下线超时未心跳的僵尸客户端"""
    while True:
        try:
            database.clean_dead_sessions()
        except Exception as e:
            pass
        await asyncio.sleep(15)

@app.on_event("startup")
async def on_startup():
    database.init_db()
    asyncio.create_task(heartbeat_janitor_loop())

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8989, reload=False)
