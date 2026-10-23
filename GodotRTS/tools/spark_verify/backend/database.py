import sqlite3
import os
import time
import secrets
from datetime import datetime, timedelta
from typing import List, Dict, Any, Optional

DB_PATH = os.path.join(os.path.dirname(__file__), "spark_verify.db")

def get_db():
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn

def init_db():
    conn = get_db()
    cursor = conn.cursor()
    
    # 1. 软件应用表
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS apps (
        app_id TEXT PRIMARY KEY,
        app_name TEXT NOT NULL,
        app_secret TEXT NOT NULL,
        current_version TEXT DEFAULT '1.0.0',
        force_update INTEGER DEFAULT 0,
        download_url TEXT DEFAULT '',
        notice TEXT DEFAULT '',
        unbind_cooldown_hours INTEGER DEFAULT 24,
        max_unbind_count INTEGER DEFAULT 3,
        heartbeat_timeout_sec INTEGER DEFAULT 60,
        status INTEGER DEFAULT 1,
        created_at TEXT NOT NULL
    )
    """)
    
    # 2. 卡密授权表
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS cards (
        card_key TEXT PRIMARY KEY,
        app_id TEXT NOT NULL,
        card_type TEXT NOT NULL, -- day, week, month, quarter, year, forever, count
        val_days REAL DEFAULT 0,
        remain_count INTEGER DEFAULT 0,
        status INTEGER DEFAULT 0, -- 0: 未使用, 1: 正常使用中, 2: 已冻结, 3: 已过期
        bound_hwid TEXT DEFAULT '',
        activated_at TEXT,
        expires_at TEXT,
        unbind_count INTEGER DEFAULT 0,
        last_unbind_at TEXT,
        note TEXT DEFAULT '',
        created_at TEXT NOT NULL,
        FOREIGN KEY (app_id) REFERENCES apps (app_id)
    )
    """)
    
    # 3. 在线心跳会话表
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS sessions (
        session_token TEXT PRIMARY KEY,
        card_key TEXT NOT NULL,
        app_id TEXT NOT NULL,
        hwid TEXT NOT NULL,
        client_ip TEXT NOT NULL,
        started_at TEXT NOT NULL,
        last_heartbeat TEXT NOT NULL,
        is_active INTEGER DEFAULT 1,
        FOREIGN KEY (card_key) REFERENCES cards (card_key)
    )
    """)
    
    # 4. 云变量表
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS cloud_vars (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        app_id TEXT NOT NULL,
        var_key TEXT NOT NULL,
        var_value TEXT NOT NULL,
        description TEXT DEFAULT '',
        UNIQUE(app_id, var_key)
    )
    """)
    
    # 5. 系统与安全日志表
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS logs (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        app_id TEXT NOT NULL,
        card_key TEXT DEFAULT '',
        event_type TEXT NOT NULL, -- VERIFY, HEARTBEAT, UNBIND, KICK, GENERATE, ERROR
        hwid TEXT DEFAULT '',
        ip TEXT DEFAULT '',
        detail TEXT NOT NULL,
        created_at TEXT NOT NULL
    )
    """)
    
    # 6. 管理员凭据表
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS admins (
        username TEXT PRIMARY KEY,
        password_hash TEXT NOT NULL,
        role TEXT DEFAULT 'ADMIN', -- SUPER_ADMIN: 总控超级管理员, ADMIN: 下级开发者管理端
        status INTEGER DEFAULT 1,  -- 1: 正常, 0: 封禁
        can_encrypt INTEGER DEFAULT 1, -- 1: 允许加密端工作, 0: 超级管理员已停止其加密端工作
        note TEXT DEFAULT '',
        token TEXT,
        token_expires TEXT,
        created_at TEXT DEFAULT ''
    )
    """)

    # 动态数据迁移 (兼容已有数据库)
    cursor.execute("PRAGMA table_info(admins)")
    admin_cols = [c[1] for c in cursor.fetchall()]
    if "role" not in admin_cols:
        cursor.execute("ALTER TABLE admins ADD COLUMN role TEXT DEFAULT 'ADMIN'")
    if "status" not in admin_cols:
        cursor.execute("ALTER TABLE admins ADD COLUMN status INTEGER DEFAULT 1")
    if "can_encrypt" not in admin_cols:
        cursor.execute("ALTER TABLE admins ADD COLUMN can_encrypt INTEGER DEFAULT 1")
    if "note" not in admin_cols:
        cursor.execute("ALTER TABLE admins ADD COLUMN note TEXT DEFAULT ''")
    if "created_at" not in admin_cols:
        cursor.execute("ALTER TABLE admins ADD COLUMN created_at TEXT DEFAULT ''")

    cursor.execute("PRAGMA table_info(apps)")
    app_cols = [c[1] for c in cursor.fetchall()]
    if "owner_admin" not in app_cols:
        cursor.execute("ALTER TABLE apps ADD COLUMN owner_admin TEXT DEFAULT 'admin'")

    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    # 初始化内置账号 (如果不存在)
    # 1. 总控超级管理员 (拥有查看所有管理端、停止/启用加密端的最高特权)
    cursor.execute("SELECT username FROM admins WHERE username = 'superadmin'")
    if not cursor.fetchone():
        cursor.execute("""
        INSERT INTO admins (username, password_hash, role, status, can_encrypt, note, created_at)
        VALUES (?, ?, 'SUPER_ADMIN', 1, 1, '平台总控最高超级管理员', ?)
        """, ("superadmin", "super123", now_str))

    # 2. 默认普通开发者管理端 1
    cursor.execute("SELECT username FROM admins WHERE username = 'admin'")
    if not cursor.fetchone():
        cursor.execute("""
        INSERT INTO admins (username, password_hash, role, status, can_encrypt, note, created_at)
        VALUES (?, ?, 'ADMIN', 1, 1, '开发者管理端 A (默认)', ?)
        """, ("admin", "admin123", now_str))

    # 3. 默认下级开发者管理端 2 (加盟代理)
    cursor.execute("SELECT username FROM admins WHERE username = 'dev_agent'")
    if not cursor.fetchone():
        cursor.execute("""
        INSERT INTO admins (username, password_hash, role, status, can_encrypt, note, created_at)
        VALUES (?, ?, 'ADMIN', 1, 1, '下级开发者管理端 B (加盟团队)', ?)
        """, ("dev_agent", "dev123", now_str))
    
    # 插入默认应用 1 (admin 名下)
    cursor.execute("SELECT app_id FROM apps WHERE app_id = 'APP_DEMO_001'")
    if not cursor.fetchone():
        cursor.execute("""
        INSERT INTO apps (
            app_id, app_name, app_secret, current_version, force_update,
            download_url, notice, unbind_cooldown_hours, max_unbind_count,
            heartbeat_timeout_sec, status, owner_admin, created_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, (
            "APP_DEMO_001",
            "极验演示软件套件 (RTS & Tools)",
            "sv_secret_88889999_k9",
            "1.0.0",
            0,
            "https://example.com/download/app_v1.0.0.zip",
            "欢迎使用极验云网络验证系统！系统已就绪，一机一码设备指纹与心跳保活引擎全开运行中。",
            24,
            3,
            60,
            1,
            "admin",
            now_str
        ))
        
        # 默认云变量
        cursor.execute("""
        INSERT INTO cloud_vars (app_id, var_key, var_value, description)
        VALUES (?, ?, ?, ?)
        """, ("APP_DEMO_001", "GAME_SERVER_IP", "127.0.0.1:9050", "主游戏分布式对战集群地址"))
        cursor.execute("""
        INSERT INTO cloud_vars (app_id, var_key, var_value, description)
        VALUES (?, ?, ?, ?)
        """, ("APP_DEMO_001", "ENABLE_SUPER_RADAR", "true", "核心扩展雷达功能授权开关"))

    # 插入默认应用 2 (dev_agent 名下)
    cursor.execute("SELECT app_id FROM apps WHERE app_id = 'APP_DEV_002'")
    if not cursor.fetchone():
        cursor.execute("""
        INSERT INTO apps (
            app_id, app_name, app_secret, current_version, force_update,
            download_url, notice, unbind_cooldown_hours, max_unbind_count,
            heartbeat_timeout_sec, status, owner_admin, created_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, (
            "APP_DEV_002",
            "加盟团队战术雷达工具包 (Agent 2)",
            "sv_agent_778899_xyz",
            "2.0.1",
            0,
            "https://example.com/download/agent_v2.0.1.zip",
            "加盟团队软件在线运行中，一机一码防护正常。",
            12,
            5,
            45,
            1,
            "dev_agent",
            now_str
        ))
    
    conn.commit()
    conn.close()

# ----------------- 数据操作函数 -----------------

def get_app(app_id: str) -> Optional[Dict[str, Any]]:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM apps WHERE app_id = ?", (app_id,))
    row = cursor.fetchone()
    conn.close()
    return dict(row) if row else None

def list_apps(owner_admin: Optional[str] = None) -> List[Dict[str, Any]]:
    conn = get_db()
    cursor = conn.cursor()
    if owner_admin and owner_admin != "superadmin":
        cursor.execute("SELECT * FROM apps WHERE owner_admin = ? ORDER BY created_at DESC", (owner_admin,))
    else:
        cursor.execute("SELECT * FROM apps ORDER BY created_at DESC")
    rows = cursor.fetchall()
    conn.close()
    return [dict(r) for r in rows]

def update_app(app_id: str, data: Dict[str, Any]) -> bool:
    conn = get_db()
    cursor = conn.cursor()
    fields = []
    values = []
    for k, v in data.items():
        if k in ["app_name", "app_secret", "current_version", "force_update", "download_url", 
                 "notice", "unbind_cooldown_hours", "max_unbind_count", "heartbeat_timeout_sec", "status"]:
            fields.append(f"{k} = ?")
            values.append(v)
    if not fields:
        conn.close()
        return False
    values.append(app_id)
    cursor.execute(f"UPDATE apps SET {', '.join(fields)} WHERE app_id = ?", values)
    conn.commit()
    conn.close()
    return True

def generate_cards(app_id: str, card_type: str, count: int, prefix: str = "SV", custom_days: float = 0, custom_count: int = 0, note: str = "") -> List[str]:
    type_days_map = {
        "day": 1.0,
        "week": 7.0,
        "month": 30.0,
        "quarter": 90.0,
        "year": 365.0,
        "forever": 99999.0,
        "count": 0.0
    }
    val_days = custom_days if card_type == "custom" else type_days_map.get(card_type, 30.0)
    remain_count = custom_count if card_type == "count" else 0
    
    conn = get_db()
    cursor = conn.cursor()
    created_keys = []
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    for _ in range(count):
        token_part = secrets.token_hex(8).upper()
        # 格式化例如: SV-MONTH-A1B2-C3D4-E5F6
        formatted = f"{prefix}-{card_type.upper()}-{token_part[:4]}-{token_part[4:8]}-{token_part[8:12]}-{token_part[12:16]}"
        cursor.execute("""
        INSERT INTO cards (
            card_key, app_id, card_type, val_days, remain_count, status,
            bound_hwid, note, created_at
        ) VALUES (?, ?, ?, ?, ?, 0, '', ?, ?)
        """, (formatted, app_id, card_type, val_days, remain_count, note, now_str))
        created_keys.append(formatted)
        
    conn.commit()
    conn.close()
    
    log_event(app_id, "", "GENERATE", "", "127.0.0.1", f"批量生成 {count} 张 {card_type} 卡密")
    return created_keys

def get_card(card_key: str) -> Optional[Dict[str, Any]]:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM cards WHERE card_key = ?", (card_key,))
    row = cursor.fetchone()
    conn.close()
    return dict(row) if row else None

def list_cards(app_id: Optional[str] = None, status: Optional[int] = None, search: str = "", limit: int = 100, offset: int = 0) -> (List[Dict[str, Any]], int):
    conn = get_db()
    cursor = conn.cursor()
    
    query = "SELECT * FROM cards WHERE 1=1"
    count_query = "SELECT COUNT(*) FROM cards WHERE 1=1"
    params = []
    
    if app_id:
        query += " AND app_id = ?"
        count_query += " AND app_id = ?"
        params.append(app_id)
    if status is not None:
        query += " AND status = ?"
        count_query += " AND status = ?"
        params.append(status)
    if search:
        query += " AND (card_key LIKE ? OR bound_hwid LIKE ? OR note LIKE ?)"
        count_query += " AND (card_key LIKE ? OR bound_hwid LIKE ? OR note LIKE ?)"
        params.extend([f"%{search}%", f"%{search}%", f"%{search}%"])
        
    cursor.execute(count_query, params)
    total = cursor.fetchone()[0]
    
    query += " ORDER BY created_at DESC LIMIT ? OFFSET ?"
    params.extend([limit, offset])
    
    cursor.execute(query, params)
    rows = cursor.fetchall()
    conn.close()
    return [dict(r) for r in rows], total

def update_card_status(card_key: str, status: int) -> bool:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("UPDATE cards SET status = ? WHERE card_key = ?", (status, card_key))
    changed = cursor.rowcount > 0
    conn.commit()
    conn.close()
    return changed

def unbind_card(card_key: str, admin_force: bool = False) -> (bool, str):
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM cards WHERE card_key = ?", (card_key,))
    row = cursor.fetchone()
    if not row:
        conn.close()
        return False, "卡密不存在"
    
    card = dict(row)
    if not card["bound_hwid"]:
        conn.close()
        return True, "该卡密当前未绑定任何设备"
        
    now = datetime.now()
    now_str = now.strftime("%Y-%m-%d %H:%M:%S")
    
    if not admin_force:
        # 校验换绑规则
        cursor.execute("SELECT * FROM apps WHERE app_id = ?", (card["app_id"],))
        app_row = cursor.fetchone()
        app = dict(app_row) if app_row else {}
        max_unbind = app.get("max_unbind_count", 3)
        cooldown_hours = app.get("unbind_cooldown_hours", 24)
        
        if card["unbind_count"] >= max_unbind:
            conn.close()
            return False, f"换绑次数已达上限（最大 {max_unbind} 次）"
            
        if card["last_unbind_at"]:
            last_dt = datetime.strptime(card["last_unbind_at"], "%Y-%m-%d %H:%M:%S")
            diff_hours = (now - last_dt).total_seconds() / 3600.0
            if diff_hours < cooldown_hours:
                remain = cooldown_hours - diff_hours
                conn.close()
                return False, f"换绑冷却中，还需等待 {remain:.1f} 小时"

    cursor.execute("""
    UPDATE cards SET bound_hwid = '', unbind_count = unbind_count + 1, last_unbind_at = ?
    WHERE card_key = ?
    """, (now_str, card_key))
    
    # 同时清理在线会话
    cursor.execute("UPDATE sessions SET is_active = 0 WHERE card_key = ?", (card_key,))
    conn.commit()
    conn.close()
    
    log_event(card["app_id"], card_key, "UNBIND", card["bound_hwid"], "", "解绑设备成功")
    return True, "解绑成功"

def delete_card(card_key: str) -> bool:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("DELETE FROM sessions WHERE card_key = ?", (card_key,))
    cursor.execute("DELETE FROM cards WHERE card_key = ?", (card_key,))
    changed = cursor.rowcount > 0
    conn.commit()
    conn.close()
    return changed

def register_or_activate_card(card_key: str, hwid: str, ip: str) -> (bool, str, Optional[Dict[str, Any]]):
    """客户端卡密验证与绑定激活"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM cards WHERE card_key = ?", (card_key,))
    row = cursor.fetchone()
    if not row:
        conn.close()
        return False, "卡密不存在或无效", None
        
    card = dict(row)
    now = datetime.now()
    now_str = now.strftime("%Y-%m-%d %H:%M:%S")
    
    if card["status"] == 2:
        conn.close()
        return False, "该卡密已被管理员冻结，请联系客服", None
        
    # 如果已绑定，校验一机一码
    if card["bound_hwid"]:
        if card["bound_hwid"] != hwid:
            conn.close()
            return False, f"设备不匹配！此卡密已绑定至其他机器 ({card['bound_hwid'][:8]}...)，请先解绑", None
    else:
        # 首次激活绑定
        card["bound_hwid"] = hwid
        
    # 计算过期时间
    if card["status"] == 0:
        # 首次激活
        card["activated_at"] = now_str
        if card["card_type"] == "forever":
            card["expires_at"] = (now + timedelta(days=99999)).strftime("%Y-%m-%d %H:%M:%S")
        elif card["card_type"] == "count":
            card["expires_at"] = (now + timedelta(days=3650)).strftime("%Y-%m-%d %H:%M:%S")
        else:
            card["expires_at"] = (now + timedelta(days=float(card["val_days"]))).strftime("%Y-%m-%d %H:%M:%S")
        card["status"] = 1
        
        cursor.execute("""
        UPDATE cards SET bound_hwid = ?, activated_at = ?, expires_at = ?, status = 1
        WHERE card_key = ?
        """, (hwid, card["activated_at"], card["expires_at"], card_key))
    else:
        # 已激活，检查是否过期
        if card["card_type"] != "forever" and card["card_type"] != "count":
            expires_dt = datetime.strptime(card["expires_at"], "%Y-%m-%d %H:%M:%S")
            if now > expires_dt:
                cursor.execute("UPDATE cards SET status = 3 WHERE card_key = ?", (card_key,))
                conn.commit()
                conn.close()
                return False, "卡密已过期，请充值续费", None
                
        if card["card_type"] == "count":
            if card["remain_count"] <= 0:
                conn.close()
                return False, "卡密可用次数已耗尽", None
            # 扣减次数
            cursor.execute("UPDATE cards SET remain_count = remain_count - 1 WHERE card_key = ?", (card_key,))
            card["remain_count"] -= 1

    # 会话创建与防多开判断
    cursor.execute("""
    SELECT * FROM sessions WHERE card_key = ? AND is_active = 1
    """, (card_key,))
    existing_session = cursor.fetchone()
    
    # 获取软件心跳超时时间
    cursor.execute("SELECT * FROM apps WHERE app_id = ?", (card["app_id"],))
    app_row = cursor.fetchone()
    app = dict(app_row) if app_row else {}
    timeout_sec = app.get("heartbeat_timeout_sec", 60)
    
    if existing_session:
        sess_data = dict(existing_session)
        last_hb = datetime.strptime(sess_data["last_heartbeat"], "%Y-%m-%d %H:%M:%S")
        # 如果还在心跳有效期内且机器码不同，拒绝多开
        if (now - last_hb).total_seconds() < timeout_sec:
            if sess_data["hwid"] != hwid:
                conn.close()
                return False, "检测到卡密正在另一台机器上运行，禁止多开！", None
        # 如果同一台机器重登，将旧会话标记离线
        cursor.execute("UPDATE sessions SET is_active = 0 WHERE card_key = ?", (card_key,))
        
    session_token = f"SESS_{secrets.token_hex(16)}"
    cursor.execute("""
    INSERT INTO sessions (
        session_token, card_key, app_id, hwid, client_ip, started_at, last_heartbeat, is_active
    ) VALUES (?, ?, ?, ?, ?, ?, ?, 1)
    """, (session_token, card_key, card["app_id"], hwid, ip, now_str, now_str))
    
    conn.commit()
    conn.close()
    
    log_event(card["app_id"], card_key, "VERIFY", hwid, ip, "卡密登录成功")
    
    res_data = {
        "session_token": session_token,
        "card_key": card_key,
        "app_id": card["app_id"],
        "card_type": card["card_type"],
        "expires_at": card["expires_at"],
        "remain_count": card.get("remain_count", 0),
        "heartbeat_interval": max(15, timeout_sec // 2)
    }
    return True, "验证成功", res_data

def process_heartbeat(session_token: str, hwid: str, ip: str) -> (bool, str):
    """客户端心跳保活"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM sessions WHERE session_token = ? AND is_active = 1", (session_token,))
    row = cursor.fetchone()
    if not row:
        conn.close()
        return False, "会话不存在或已被服务器强制下线"
        
    sess = dict(row)
    if sess["hwid"] != hwid:
        conn.close()
        return False, "心跳硬件特征码异常"
        
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    cursor.execute("""
    UPDATE sessions SET last_heartbeat = ?, client_ip = ? WHERE session_token = ?
    """, (now_str, ip, session_token))
    
    conn.commit()
    conn.close()
    return True, "心跳正常"

def terminate_session(session_token: str) -> bool:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("UPDATE sessions SET is_active = 0 WHERE session_token = ?", (session_token,))
    changed = cursor.rowcount > 0
    conn.commit()
    conn.close()
    return changed

def clean_dead_sessions():
    """扫描并关闭超时的死会话"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("""
    SELECT s.session_token, s.last_heartbeat, a.heartbeat_timeout_sec 
    FROM sessions s
    JOIN apps a ON s.app_id = a.app_id
    WHERE s.is_active = 1
    """)
    rows = cursor.fetchall()
    now = datetime.now()
    dead_tokens = []
    
    for r in rows:
        last_hb = datetime.strptime(r["last_heartbeat"], "%Y-%m-%d %H:%M:%S")
        timeout = r["heartbeat_timeout_sec"] or 60
        # 超过 1.5 倍心跳周期无响应，则判定离线
        if (now - last_hb).total_seconds() > (timeout * 1.5):
            dead_tokens.append(r["session_token"])
            
    if dead_tokens:
        cursor.executemany("UPDATE sessions SET is_active = 0 WHERE session_token = ?", [(t,) for t in dead_tokens])
        conn.commit()
        
    conn.close()
    return len(dead_tokens)

def list_active_sessions(app_id: Optional[str] = None) -> List[Dict[str, Any]]:
    clean_dead_sessions()
    conn = get_db()
    cursor = conn.cursor()
    query = """
    SELECT s.*, c.card_type, c.expires_at, a.app_name
    FROM sessions s
    JOIN cards c ON s.card_key = c.card_key
    JOIN apps a ON s.app_id = a.app_id
    WHERE s.is_active = 1
    """
    params = []
    if app_id:
        query += " AND s.app_id = ?"
        params.append(app_id)
    query += " ORDER BY s.last_heartbeat DESC"
    cursor.execute(query, params)
    rows = cursor.fetchall()
    conn.close()
    return [dict(r) for r in rows]

def get_cloud_vars(app_id: str) -> Dict[str, str]:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT var_key, var_value FROM cloud_vars WHERE app_id = ?", (app_id,))
    rows = cursor.fetchall()
    conn.close()
    return {r["var_key"]: r["var_value"] for r in rows}

def list_cloud_vars_full(app_id: str) -> List[Dict[str, Any]]:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM cloud_vars WHERE app_id = ?", (app_id,))
    rows = cursor.fetchall()
    conn.close()
    return [dict(r) for r in rows]

def save_cloud_var(app_id: str, var_key: str, var_value: str, description: str = "") -> bool:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("""
    INSERT INTO cloud_vars (app_id, var_key, var_value, description)
    VALUES (?, ?, ?, ?)
    ON CONFLICT(app_id, var_key) DO UPDATE SET
        var_value = excluded.var_value,
        description = excluded.description
    """, (app_id, var_key, var_value, description))
    conn.commit()
    conn.close()
    return True

def delete_cloud_var(var_id: int) -> bool:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("DELETE FROM cloud_vars WHERE id = ?", (var_id,))
    changed = cursor.rowcount > 0
    conn.commit()
    conn.close()
    return changed

def log_event(app_id: str, card_key: str, event_type: str, hwid: str, ip: str, detail: str):
    conn = get_db()
    cursor = conn.cursor()
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    cursor.execute("""
    INSERT INTO logs (app_id, card_key, event_type, hwid, ip, detail, created_at)
    VALUES (?, ?, ?, ?, ?, ?, ?)
    """, (app_id, card_key, event_type, hwid, ip, detail, now_str))
    conn.commit()
    conn.close()

def list_logs(app_id: Optional[str] = None, limit: int = 50) -> List[Dict[str, Any]]:
    conn = get_db()
    cursor = conn.cursor()
    query = "SELECT * FROM logs WHERE 1=1"
    params = []
    if app_id:
        query += " AND app_id = ?"
        params.append(app_id)
    query += " ORDER BY id DESC LIMIT ?"
    params.append(limit)
    cursor.execute(query, params)
    rows = cursor.fetchall()
    conn.close()
    return [dict(r) for r in rows]

def get_dashboard_stats(app_id: Optional[str] = None) -> Dict[str, Any]:
    clean_dead_sessions()
    conn = get_db()
    cursor = conn.cursor()
    
    today_str = datetime.now().strftime("%Y-%m-%d") + " 00:00:00"
    
    app_filter = " WHERE app_id = ?" if app_id else ""
    params = [app_id] if app_id else []
    
    # 1. 在线人数
    sess_sql = "SELECT COUNT(*) FROM sessions WHERE is_active = 1"
    if app_id:
        sess_sql += " AND app_id = ?"
    cursor.execute(sess_sql, params)
    online_count = cursor.fetchone()[0]
    
    # 2. 总卡密数
    cursor.execute(f"SELECT COUNT(*) FROM cards{app_filter}", params)
    total_cards = cursor.fetchone()[0]
    
    # 3. 今日激活数
    act_sql = "SELECT COUNT(*) FROM cards WHERE activated_at >= ?"
    act_params = [today_str]
    if app_id:
        act_sql += " AND app_id = ?"
        act_params.append(app_id)
    cursor.execute(act_sql, act_params)
    today_activated = cursor.fetchone()[0]
    
    # 4. 未使用卡密
    cursor.execute(f"SELECT COUNT(*) FROM cards WHERE status = 0" + (f" AND app_id = ?" if app_id else ""), params)
    unused_cards = cursor.fetchone()[0]
    
    # 5. 已冻结
    cursor.execute(f"SELECT COUNT(*) FROM cards WHERE status = 2" + (f" AND app_id = ?" if app_id else ""), params)
    frozen_cards = cursor.fetchone()[0]
    
    conn.close()
    return {
        "online_count": online_count,
        "total_cards": total_cards,
        "today_activated": today_activated,
        "unused_cards": unused_cards,
        "frozen_cards": frozen_cards
    }

# ----------------- 超级管理员专属：管理端管控与加密端熔断 -----------------

def get_admin(username: str) -> Optional[Dict[str, Any]]:
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM admins WHERE username = ?", (username,))
    row = cursor.fetchone()
    conn.close()
    return dict(row) if row else None

def list_all_admins() -> List[Dict[str, Any]]:
    """超级管理员专用：列出系统内所有下级管理端及其名下应用数、制卡数、在线人数、加密端工作状态"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT username, role, status, can_encrypt, note, created_at FROM admins ORDER BY created_at ASC")
    admins = [dict(r) for r in cursor.fetchall()]

    for a in admins:
        u = a["username"]
        # 1. 名下应用数
        cursor.execute("SELECT COUNT(*) FROM apps WHERE owner_admin = ?", (u,))
        a["apps_count"] = cursor.fetchone()[0]

        # 2. 名下制卡总量
        cursor.execute("""
        SELECT COUNT(*) FROM cards c
        JOIN apps a ON c.app_id = a.app_id
        WHERE a.owner_admin = ?
        """, (u,))
        a["cards_count"] = cursor.fetchone()[0]

        # 3. 名下当前在线终端数
        cursor.execute("""
        SELECT COUNT(*) FROM sessions s
        JOIN apps a ON s.app_id = a.app_id
        WHERE a.owner_admin = ? AND s.is_active = 1
        """, (u,))
        a["online_count"] = cursor.fetchone()[0]

    conn.close()
    return admins

def set_admin_encrypt_status(username: str, can_encrypt: int) -> (bool, str):
    """超级管理员核心权力：一键停止或恢复指定管理端的加密端工作权限"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT username, role FROM admins WHERE username = ?", (username,))
    row = cursor.fetchone()
    if not row:
        conn.close()
        return False, "管理端账号不存在"

    if row["role"] == "SUPER_ADMIN":
        conn.close()
        return False, "无法停止超级管理员自身的加密权限"

    cursor.execute("UPDATE admins SET can_encrypt = ? WHERE username = ?", (1 if can_encrypt else 0, username))
    conn.commit()
    conn.close()

    action_text = "恢复工作" if can_encrypt else "停止工作 (已熔断)"
    log_event("SYSTEM", "", "SUPER_KILLSWITCH", "", "127.0.0.1", f"超级管理员设置管理端 [{username}] 加密端状态为: {action_text}")
    return True, f"已成功将管理端 [{username}] 的加密端设置为: {action_text}"

def set_admin_status(username: str, status: int) -> (bool, str):
    """超级管理员冻结或解冻指定管理端账号"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT username, role FROM admins WHERE username = ?", (username,))
    row = cursor.fetchone()
    if not row:
        conn.close()
        return False, "管理端账号不存在"

    if row["role"] == "SUPER_ADMIN":
        conn.close()
        return False, "无法封禁超级管理员自身"

    cursor.execute("UPDATE admins SET status = ? WHERE username = ?", (status, username))
    conn.commit()
    conn.close()
    return True, "操作成功"

def create_admin(username: str, password: str, role: str = "ADMIN", note: str = "") -> (bool, str):
    """超级管理员开通新的下级管理端"""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT username FROM admins WHERE username = ?", (username,))
    if cursor.fetchone():
        conn.close()
        return False, "该管理端账号已存在"

    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    cursor.execute("""
    INSERT INTO admins (username, password_hash, role, status, can_encrypt, note, created_at)
    VALUES (?, ?, ?, 1, 1, ?, ?)
    """, (username, password, role, note, now_str))
    conn.commit()
    conn.close()
    return True, "开通管理端成功"

def check_admin_packer_permission(username: str) -> (bool, str, Dict[str, Any]):
    """加密端鉴权：校验管理端是否有权执行程序加验证加密打包"""
    admin = get_admin(username)
    if not admin:
        return False, "管理端账号不存在，请先向总控申请注册！", {}

    if admin["status"] == 0:
        return False, "该管理端账号已被总控超级管理员封禁，无法登录或工作！", {}

    if admin["role"] != "SUPER_ADMIN" and admin.get("can_encrypt", 1) == 0:
        return False, "⚠️ 权限受限: 你的加密端工作已被超级管理员停止！目前无法执行一键加验证或加密打包，请联系总控解除限制。", admin

    return True, "授权有效，加密端允许工作", admin

def get_super_overview() -> Dict[str, Any]:
    """超级管理员全局使用大盘统计"""
    clean_dead_sessions()
    conn = get_db()
    cursor = conn.cursor()

    cursor.execute("SELECT COUNT(*) FROM admins WHERE role = 'ADMIN'")
    total_admins = cursor.fetchone()[0]

    cursor.execute("SELECT COUNT(*) FROM admins WHERE role = 'ADMIN' AND can_encrypt = 1")
    active_packers = cursor.fetchone()[0]

    cursor.execute("SELECT COUNT(*) FROM admins WHERE role = 'ADMIN' AND can_encrypt = 0")
    stopped_packers = cursor.fetchone()[0]

    cursor.execute("SELECT COUNT(*) FROM apps")
    total_apps = cursor.fetchone()[0]

    cursor.execute("SELECT COUNT(*) FROM cards")
    total_cards = cursor.fetchone()[0]

    cursor.execute("SELECT COUNT(*) FROM sessions WHERE is_active = 1")
    online_count = cursor.fetchone()[0]

    today_str = datetime.now().strftime("%Y-%m-%d") + " 00:00:00"
    cursor.execute("SELECT COUNT(*) FROM cards WHERE activated_at >= ?", (today_str,))
    today_activated = cursor.fetchone()[0]

    conn.close()
    return {
        "total_admins": total_admins,
        "active_packers": active_packers,
        "stopped_packers": stopped_packers,
        "total_apps": total_apps,
        "total_cards": total_cards,
        "online_count": online_count,
        "today_activated": today_activated
    }
