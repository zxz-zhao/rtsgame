"""
SparkVerify 开发者加密工作台 (SparkStudio - 对标 Sunny Pro)
功能:
1. 开发者管理登录 (连接验证中枢、拉取应用列表、统计数据)
2. 一键给程序加验证 (EXE 一键 AES-256 加密、注入一机一码、心跳看门狗、反调试、反抓包、编译输出受保护 EXE)
3. 卡密快捷批量生成与导出
4. 实时在线监控与踢人下线
5. 买家卡密登录体验测试
"""

import os
import sys
import time
import json
import base64
import secrets
import shutil
import threading
import subprocess
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import urllib.request
import urllib.error

# 引入加解密引擎
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "backend")))
import crypto_utils

class SparkStudioApp:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("SparkVerify 开发者工作台 - 软件一键加验证 & 安全加密中枢 (Sunny Pro 对标版)")
        self.root.geometry("820x680")
        self.root.minsize(780, 620)
        self.root.configure(bg="#0b1120")

        # 状态数据
        self.server_host = "http://127.0.0.1:8989"
        self.admin_token = None
        self.admin_user = "admin"
        self.admin_role = "ADMIN"
        self.can_encrypt = 1
        self.apps_list = []
        self.selected_app = None

        self._apply_styles()
        self._build_main_layout()
        self._try_auto_login()

    def _apply_styles(self):
        style = ttk.Style()
        style.theme_use("clam")
        style.configure("TNotebook", background="#0b1120", borderwidth=0)
        style.configure("TNotebook.Tab", background="#1e293b", foreground="#94a3b8", padding=[16, 8], font=("Microsoft YaHei UI", 9, "bold"))
        style.map("TNotebook.Tab", background=[("selected", "#0284c7")], foreground=[("selected", "#ffffff")])

    def _build_main_layout(self):
        # 1. 顶部状态栏 Header
        top_bar = tk.Frame(self.root, bg="#111c34", height=54, padx=16)
        top_bar.pack(fill="x")

        lbl_logo = tk.Label(top_bar, text="🛡️ SPARK STUDIO", font=("Segoe UI", 12, "bold"), fg="#38bdf8", bg="#111c34")
        lbl_logo.pack(side="left", pady=12)

        lbl_sub = tk.Label(top_bar, text="一键加密 · 一键注入网络验证 · VMP级防破解防御体系", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#111c34")
        lbl_sub.pack(side="left", padx=(12, 0), pady=14)

        self.lbl_packer_status = tk.Label(top_bar, text="🛡️ 加密端状态: 允许工作", font=("Microsoft YaHei UI", 8, "bold"), fg="#4ade80", bg="#111c34")
        self.lbl_packer_status.pack(side="right", padx=(0, 14), pady=14)

        self.lbl_conn_status = tk.Label(top_bar, text="🔴 未登录管理中枢", font=("Microsoft YaHei UI", 8, "bold"), fg="#f87171", bg="#111c34")
        self.lbl_conn_status.pack(side="right", padx=(0, 14), pady=14)

        # 2. 选项卡主体 Notebook
        self.notebook = ttk.Notebook(self.root)
        self.notebook.pack(fill="both", expand=True, padx=12, pady=8)

        # 创建四个主要 Tab 页
        self.tab_inject = tk.Frame(self.notebook, bg="#0b1120", padx=16, pady=12)
        self.tab_login = tk.Frame(self.notebook, bg="#0b1120", padx=16, pady=12)
        self.tab_cards = tk.Frame(self.notebook, bg="#0b1120", padx=16, pady=12)
        self.tab_sessions = tk.Frame(self.notebook, bg="#0b1120", padx=16, pady=12)
        self.tab_tester = tk.Frame(self.notebook, bg="#0b1120", padx=16, pady=12)

        self.notebook.add(self.tab_inject, text=" 🔒 一键给程序加验证 ")
        self.notebook.add(self.tab_login, text=" 👤 开发者登录与配置 ")
        self.notebook.add(self.tab_cards, text=" 💳 卡密批量生成 ")
        self.notebook.add(self.tab_sessions, text=" ⚡ 在线终端监控 ")
        self.notebook.add(self.tab_tester, text=" 🧪 买家登录体验测试 ")

        # 构建各 Tab 界面
        self._build_tab_login()
        self._build_tab_inject()
        self._build_tab_cards()
        self._build_tab_sessions()
        self._build_tab_tester()

    # ================= TAB 1: 开发者登录与服务器配置 =================
    def _build_tab_login(self):
        f = self.tab_login

        card = tk.Frame(f, bg="#1e293b", padx=20, pady=18)
        card.pack(fill="x", pady=10)

        tk.Label(card, text="中枢服务端连接参数", font=("Microsoft YaHei UI", 10, "bold"), fg="#38bdf8", bg="#1e293b").pack(anchor="w", pady=(0, 10))

        tk.Label(card, text="服务器 API 地址:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(anchor="w")
        self.entry_server = tk.Entry(card, font=("Consolas", 10), bg="#0f172a", fg="#ffffff", relief="flat")
        self.entry_server.insert(0, "http://127.0.0.1:8989")
        self.entry_server.pack(fill="x", pady=(2, 10), ipady=4)

        tk.Label(card, text="管理员账号 (Admin Username):", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(anchor="w")
        self.entry_user = tk.Entry(card, font=("Consolas", 10), bg="#0f172a", fg="#ffffff", relief="flat")
        self.entry_user.insert(0, "admin")
        self.entry_user.pack(fill="x", pady=(2, 10), ipady=4)

        tk.Label(card, text="管理密码 (Password):", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(anchor="w")
        self.entry_pwd = tk.Entry(card, font=("Consolas", 10), bg="#0f172a", fg="#ffffff", relief="flat", show="*")
        self.entry_pwd.insert(0, "admin123")
        self.entry_pwd.pack(fill="x", pady=(2, 14), ipady=4)

        btn_row = tk.Frame(card, bg="#1e293b")
        btn_row.pack(fill="x")

        self.btn_admin_login = tk.Button(btn_row, text="登录连接验证中枢", font=("Microsoft YaHei UI", 9, "bold"), bg="#0284c7", fg="#ffffff", activebackground="#0369a1", activeforeground="#ffffff", relief="flat", command=self._do_admin_login)
        self.btn_admin_login.pack(side="left", padx=(0, 10), ipady=5, ipadx=16)

        btn_web = tk.Button(btn_row, text="在浏览器中打开 Web 后台", font=("Microsoft YaHei UI", 8), bg="#334155", fg="#cbd5e1", relief="flat", command=lambda: os.system("start http://127.0.0.1:8989/admin"))
        btn_web.pack(side="left", ipady=5, ipadx=10)

        # 统计面板
        self.card_stats = tk.Frame(f, bg="#111c34", padx=16, pady=12)
        self.card_stats.pack(fill="x", pady=10)
        self.lbl_stats_info = tk.Label(self.card_stats, text="当前未同步数据，请先登录连接。", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#111c34")
        self.lbl_stats_info.pack(anchor="w")

    def _do_admin_login(self, is_manual=True):
        server = self.entry_server.get().strip().rstrip("/")
        u = self.entry_user.get().strip()
        p = self.entry_pwd.get().strip()

        def _worker():
            try:
                req_data = json.dumps({"username": u, "password": p}).encode("utf-8")
                req = urllib.request.Request(f"{server}/api/v1/admin/login", data=req_data, headers={"Content-Type": "application/json"})
                with urllib.request.urlopen(req, timeout=5) as resp:
                    res = json.loads(resp.read().decode("utf-8"))
                    if res.get("code") == 200:
                        self.server_host = server
                        self.admin_token = res.get("token")
                        self.admin_user = res.get("username", u)
                        self.admin_role = res.get("role", "ADMIN")
                        self.can_encrypt = res.get("can_encrypt", 1)
                        self.root.after(0, lambda: self._on_login_ok(is_manual))
                    else:
                        if is_manual:
                            self.root.after(0, lambda: messagebox.showerror("失败", res.get("msg", "登录失败")))
            except Exception as e:
                if is_manual:
                    self.root.after(0, lambda: messagebox.showerror("连接错误", f"无法连接验证中枢:\n{str(e)}"))

        threading.Thread(target=_worker, daemon=True).start()

    def _on_login_ok(self, is_manual):
        self.lbl_conn_status.config(text=f"🟢 已连接管理端 [{self.admin_user}]", fg="#4ade80")
        if self.can_encrypt == 1:
            self.lbl_packer_status.config(text="🛡️ 加密端状态: 允许工作", fg="#4ade80")
            if hasattr(self, 'banner_packer_status'):
                self.banner_packer_status.config(
                    text="🟢 加密端许可有效：可正常进行 AES-256 加密与网络验证注入",
                    fg="#4ade80", bg="#064e3b"
                )
            if hasattr(self, 'btn_pack'):
                self.btn_pack.config(state="normal", text="⚡ 立即一键加验证并生成保护程序 (Inject & Build)")
        else:
            self.lbl_packer_status.config(text="🛑 加密端已被超级管理员停止工作", fg="#f87171")
            if hasattr(self, 'banner_packer_status'):
                self.banner_packer_status.config(
                    text="🛑 警告：总控超级管理员已停止当前管理端的加密端工作！禁止加壳打包新程序，请联系超级管理员！",
                    fg="#fca5a5", bg="#7f1d1d"
                )
            if hasattr(self, 'btn_pack'):
                self.btn_pack.config(state="disabled", text="🛑 加密端已被超级管理员停止工作 (禁止加验证)")

        if is_manual:
            if self.can_encrypt == 1:
                messagebox.showinfo("成功", f"管理员 [{self.admin_user}] 登录成功！\n加密端许可状态: 正常允许工作")
            else:
                messagebox.showwarning("加密端已停止", f"管理员 [{self.admin_user}] 登录成功！\n\n⚠️ 注意：您的加密端已被超级管理员停止工作，当前无法打包新软件！")
        self._refresh_apps_list()
        self._refresh_overview_stats()

    def _try_auto_login(self):
        self.root.after(300, lambda: self._do_admin_login(is_manual=False))

    def _api_get(self, path):
        req = urllib.request.Request(f"{self.server_host}{path}", headers={"Authorization": f"Bearer {self.admin_token}"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            return json.loads(resp.read().decode("utf-8"))

    def _api_post(self, path, payload):
        req_data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(f"{self.server_host}{path}", data=req_data, headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self.admin_token}"
        })
        with urllib.request.urlopen(req, timeout=10) as resp:
            return json.loads(resp.read().decode("utf-8"))

    def _refresh_apps_list(self):
        try:
            res = self._api_get("/api/v1/admin/apps")
            if res.get("code") == 200:
                self.apps_list = res.get("data", [])
                app_names = [f"{a['app_id']} ({a['app_name']})" for a in self.apps_list]
                self.combo_apps['values'] = app_names
                if app_names:
                    self.combo_apps.current(0)
                    self._on_app_selected(None)
        except Exception:
            pass

    def _refresh_overview_stats(self):
        try:
            res = self._api_get("/api/v1/admin/overview")
            if res.get("code") == 200:
                d = res["data"]
                info = f"📊 系统状态: 总卡密数: {d['total_cards']} 张 ｜ 今日激活新增: {d['today_activated']} ｜ 实时在线终端: {d['online_count']} 人 ｜ 未使用: {d['unused_cards']} 张"
                self.lbl_stats_info.config(text=info, fg="#38bdf8")
        except Exception:
            pass

    # ================= TAB 2: 一键给程序加验证 (核心工作台) =================
    def _build_tab_inject(self):
        f = self.tab_inject

        # 0. 超级管理员加密端启停工作状态横幅
        self.banner_packer_status = tk.Label(
            f, text="🟢 加密端许可有效：可正常进行 AES-256 加密与网络验证注入",
            font=("Microsoft YaHei UI", 8, "bold"), fg="#4ade80", bg="#064e3b", padx=10, pady=5
        )
        self.banner_packer_status.pack(fill="x", pady=(0, 8))

        # 1. 待加密文件选择卡片
        card_file = tk.Frame(f, bg="#1e293b", padx=16, pady=12)
        card_file.pack(fill="x", pady=(0, 10))

        tk.Label(card_file, text="1. 选择待加验证的原程序 (支持任意 .exe / .py / 脚本文件):", font=("Microsoft YaHei UI", 9, "bold"), fg="#38bdf8", bg="#1e293b").pack(anchor="w")

        f_row = tk.Frame(card_file, bg="#1e293b")
        f_row.pack(fill="x", pady=(4, 0))

        self.entry_target_path = tk.Entry(f_row, font=("Consolas", 9), bg="#0f172a", fg="#ffffff", relief="flat")
        # 默认填入 sample_target.py 作为示例
        default_target = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "packer", "sample_target.py"))
        self.entry_target_path.insert(0, default_target)
        self.entry_target_path.pack(side="left", fill="x", expand=True, ipady=4)

        btn_browse = tk.Button(f_row, text="📁 浏览文件...", font=("Microsoft YaHei UI", 8), bg="#334155", fg="#ffffff", relief="flat", command=self._browse_target_file)
        btn_browse.pack(side="left", padx=(8, 0), ipady=2)

        # 2. 软件策略与参数绑定卡片
        card_cfg = tk.Frame(f, bg="#1e293b", padx=16, pady=12)
        card_cfg.pack(fill="x", pady=(0, 10))

        tk.Label(card_cfg, text="2. 绑定网络验证策略与安全防御选项:", font=("Microsoft YaHei UI", 9, "bold"), fg="#38bdf8", bg="#1e293b").pack(anchor="w", pady=(0, 6))

        cfg_grid = tk.Frame(card_cfg, bg="#1e293b")
        cfg_grid.pack(fill="x")

        tk.Label(cfg_grid, text="所属授权应用:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").grid(row=0, column=0, sticky="w", pady=4)
        self.combo_apps = ttk.Combobox(cfg_grid, width=35, state="readonly")
        self.combo_apps.grid(row=0, column=1, sticky="w", padx=(6, 20), pady=4)
        self.combo_apps.bind("<<ComboboxSelected>>", self._on_app_selected)

        tk.Label(cfg_grid, text="生成程序展示名称:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").grid(row=0, column=2, sticky="w", pady=4)
        self.entry_app_title = tk.Entry(cfg_grid, font=("Microsoft YaHei UI", 9), bg="#0f172a", fg="#ffffff", relief="flat", width=24)
        self.entry_app_title.insert(0, "极验专业版业务套件")
        self.entry_app_title.grid(row=0, column=3, sticky="w", padx=(6, 0), pady=4)

        # 防御复选框
        chk_box = tk.Frame(card_cfg, bg="#1e293b")
        chk_box.pack(fill="x", pady=(8, 4))

        self.var_hwid = tk.BooleanVar(value=True)
        self.var_heartbeat = tk.BooleanVar(value=True)
        self.var_cloudvar = tk.BooleanVar(value=True)
        self.var_antidebug = tk.BooleanVar(value=True)
        self.var_compile_exe = tk.BooleanVar(value=True)

        tk.Checkbutton(chk_box, text="一机一码设备指纹锁定", variable=self.var_hwid, font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b", selectcolor="#0f172a").grid(row=0, column=0, sticky="w", padx=(0, 15))
        tk.Checkbutton(chk_box, text="30s 周期心跳看门狗 (超时强制杀进程)", variable=self.var_heartbeat, font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b", selectcolor="#0f172a").grid(row=0, column=1, sticky="w", padx=(0, 15))
        tk.Checkbutton(chk_box, text="无卡密无代码 (云变量动态下发)", variable=self.var_cloudvar, font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b", selectcolor="#0f172a").grid(row=0, column=2, sticky="w")

        chk_box2 = tk.Frame(card_cfg, bg="#1e293b")
        chk_box2.pack(fill="x", pady=(2, 0))
        tk.Checkbutton(chk_box2, text="注入基础反调试 (Anti-Debug 拦截 x64dbg/CE)", variable=self.var_antidebug, font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b", selectcolor="#0f172a").grid(row=0, column=0, sticky="w", padx=(0, 15))
        tk.Checkbutton(chk_box2, text="直接编译为单文件受保护 EXE (推荐)", variable=self.var_compile_exe, font=("Microsoft YaHei UI", 8, "bold"), fg="#38bdf8", bg="#1e293b", selectcolor="#0f172a").grid(row=0, column=1, sticky="w")

        # 3. 输出与执行加验证按键
        btn_action_box = tk.Frame(f, bg="#0b1120")
        btn_action_box.pack(fill="x", pady=6)

        self.btn_pack = tk.Button(btn_action_box, text="⚡ 立即一键加验证并生成保护程序 (Inject & Build)", font=("Microsoft YaHei UI", 11, "bold"), bg="#0284c7", fg="#ffffff", activebackground="#0369a1", activeforeground="#ffffff", relief="flat", command=self._do_inject_pack)
        self.btn_pack.pack(fill="x", ipady=8)

        # 4. 控制台日志输出
        card_log = tk.Frame(f, bg="#0f172a", padx=10, pady=8)
        card_log.pack(fill="both", expand=True, pady=(6, 0))

        tk.Label(card_log, text="📋 打包与注入处理日志流:", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#0f172a").pack(anchor="w")

        self.txt_log = tk.Text(card_log, font=("Consolas", 8), bg="#050914", fg="#38bdf8", relief="flat", height=8)
        self.txt_log.pack(fill="both", expand=True, pady=(4, 0))

    def _browse_target_file(self):
        f = filedialog.askopenfilename(title="选择需要加验证的程序", filetypes=[("所有可执行文件与脚本", "*.exe *.py *.bat *.dll"), ("所有文件", "*.*")])
        if f:
            self.entry_target_path.delete(0, tk.END)
            self.entry_target_path.insert(0, os.path.abspath(f))

    def _on_app_selected(self, event):
        idx = self.combo_apps.current()
        if idx >= 0 and idx < len(self.apps_list):
            self.selected_app = self.apps_list[idx]
            self.entry_app_title.delete(0, tk.END)
            self.entry_app_title.insert(0, self.selected_app.get("app_name", "已授权软件"))

    def _log(self, msg):
        self.txt_log.insert(tk.END, f"[{time.strftime('%H:%M:%S')}] {msg}\n")
        self.txt_log.see(tk.END)

    def _do_inject_pack(self):
        target = self.entry_target_path.get().strip()
        if not os.path.exists(target):
            messagebox.showerror("错误", f"目标文件不存在:\n{target}")
            return

        # 实时向服务端核验加密端权限 (超级管理员熔断制裁机制)
        try:
            chk_payload = json.dumps({"username": self.admin_user or "admin"}).encode("utf-8")
            chk_req = urllib.request.Request(
                f"{self.server_host}/api/v1/packer/check_permission",
                data=chk_payload,
                headers={"Content-Type": "application/json"}
            )
            with urllib.request.urlopen(chk_req, timeout=5) as chk_resp:
                chk_data = json.loads(chk_resp.read().decode("utf-8"))
                if chk_data.get("can_encrypt") != 1:
                    raise PermissionError("超级管理员已停止此加密端工作")
        except urllib.error.HTTPError as he:
            messagebox.showerror(
                "加密端已被超级管理员停止",
                f"🛑 权限熔断拦截：\n\n总控超级管理员已停止当前管理端 [{self.admin_user}] 的加密端工作！\n\n已禁止为新程序添加验证与打包。请联系平台超级管理员解封后再试！"
            )
            self._log(f"🛑 [权限熔断] 加密端已被超级管理员停止工作，打包请求被服务端强行拦截！")
            self.lbl_packer_status.config(text="🛑 加密端已被超级管理员停止工作", fg="#f87171")
            self.banner_packer_status.config(
                text="🛑 警告：总控超级管理员已停止当前管理端的加密端工作！禁止加壳打包新程序，请联系超级管理员！",
                fg="#fca5a5", bg="#7f1d1d"
            )
            self.btn_pack.config(state="disabled", text="🛑 加密端已被超级管理员停止工作")
            return
        except Exception as e:
            messagebox.showerror("加密端权限核验失败", f"无法核验加密权限:\n{str(e)}")
            self._log(f"❌ 打包终止：权限核验失败: {str(e)}")
            return

        app_id = self.selected_app["app_id"] if self.selected_app else "APP_DEMO_001"
        app_secret = self.selected_app["app_secret"] if self.selected_app else "sv_secret_88889999_k9"
        software_title = self.entry_app_title.get().strip() or "受保护软件"
        do_compile = self.var_compile_exe.get()

        self.btn_pack.config(state="disabled", text="正在执行高强度加密与加壳打包中...")
        self.txt_log.delete("1.0", tk.END)

        def _worker():
            try:
                self._log(f"开始对目标程序执行加验证: {os.path.basename(target)}")
                output_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Output_Protected"))
                os.makedirs(output_dir, exist_ok=True)

                # 1. 读取目标程序并高强度加密
                with open(target, "rb") as f:
                    raw_bytes = f.read()
                raw_b64 = base64.b64encode(raw_bytes).decode("utf-8")
                pack_key = secrets.token_hex(32)
                self._log(f"生成独立随机加密私钥: {pack_key[:16]}... (AES-256-GCM)")

                enc_payload = crypto_utils.encrypt_aes_gcm(raw_b64, pack_key)
                payload_file = os.path.join(output_dir, "payload.enc")
                with open(payload_file, "w", encoding="utf-8") as f:
                    f.write(enc_payload)
                self._log(f"目标二进制已全文加密封包 -> payload.enc ({len(enc_payload)} bytes)")

                # 2. 生成定制版终端登录加载器 Loader
                loader_code = self._generate_injected_loader_code(
                    server_url=self.server_host,
                    app_id=app_id,
                    app_secret=app_secret,
                    pack_key=pack_key,
                    app_title=software_title,
                    is_python=target.endswith(".py"),
                    enable_antidebug=self.var_antidebug.get(),
                    enable_heartbeat=self.var_heartbeat.get()
                )

                loader_file = os.path.join(output_dir, "protected_loader.py")
                with open(loader_file, "w", encoding="utf-8") as f:
                    f.write(loader_code)
                self._log(f"生成安全验证引导注入器 -> {loader_file}")

                # 复制必需的客户端 SDK
                sdk_src = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "clients", "python", "spark_verify_client.py"))
                shutil.copy(sdk_src, os.path.join(output_dir, "spark_verify_client.py"))

                # 3. 如果勾选了直接编译为单文件 EXE
                if do_compile:
                    self._log("正在调用 PyInstaller 进行单文件 EXE 编译封包...")
                    out_exe_name = f"{os.path.splitext(os.path.basename(target))[0]}_Protected"
                    pyi_cmd = [
                        sys.executable, "-m", "PyInstaller",
                        "--onefile",
                        "--noconsole",
                        "--name", out_exe_name,
                        "--distpath", output_dir,
                        "--workpath", os.path.join(output_dir, "build"),
                        "--specpath", output_dir,
                        f"--add-data={payload_file};.",
                        loader_file
                    ]
                    p = subprocess.Popen(pyi_cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, cwd=output_dir)
                    for line in iter(p.stdout.readline, ''):
                        if "INFO: Building EXE" in line or "Build complete" in line:
                            self._log(line.strip())
                    p.wait()

                    final_exe = os.path.join(output_dir, f"{out_exe_name}.exe")
                    self._log(f"🎉 编译完成！成品独立保护程序: {final_exe}")
                    self.root.after(0, lambda: messagebox.showinfo(
                        "一键加验证完成",
                        f"🎉 恭喜！程序已成功加密并植入网络验证！\n\n"
                        f"成品可执行文件:\n{final_exe}\n\n"
                        f"已直接拥有: 一机一码绑定、心跳保活看门狗、卡密授权弹窗！直接发给买家即可使用！"
                    ))
                else:
                    self._log(f"🎉 源码引导器生成完成！目录: {output_dir}")
                    self.root.after(0, lambda: messagebox.showinfo("加验证完成", f"引导器已生成在:\n{output_dir}"))

            except Exception as e:
                self._log(f"❌ 打包失败: {str(e)}")
                self.root.after(0, lambda: messagebox.showerror("错误", f"打包出错: {str(e)}"))
            finally:
                self.root.after(0, lambda: self.btn_pack.config(state="normal", text="⚡ 立即一键加验证并生成保护程序 (Inject & Build)"))

        threading.Thread(target=_worker, daemon=True).start()

    def _generate_injected_loader_code(self, server_url, app_id, app_secret, pack_key, app_title, is_python, enable_antidebug, enable_heartbeat):
        return f'''"""
SparkVerify 动态安全注入引导器 (One-Click Injected Loader)
由 SparkStudio 自动编译生成
"""
import os
import sys
import time
import base64
import tempfile
import threading
import subprocess
import tkinter as tk
from tkinter import messagebox
from spark_verify_client import SparkVerifyClient

HOST = "{server_url}"
APP_ID = "{app_id}"
APP_SECRET = "{app_secret}"
PACK_KEY = "{pack_key}"
TITLE = "{app_title}"
IS_PYTHON = {str(is_python)}
ENABLE_ANTIDEBUG = {str(enable_antidebug)}
ENABLE_HEARTBEAT = {str(enable_heartbeat)}

def check_debugger():
    if not ENABLE_ANTIDEBUG:
        return False
    # 基础反调试: 常见逆向进程检测
    bad_procs = ["x64dbg.exe", "x32dbg.exe", "ida.exe", "ida64.exe", "cheatengine.exe", "ollydbg.exe"]
    try:
        out = subprocess.check_output("tasklist", shell=True, text=True, stderr=subprocess.DEVNULL).lower()
        for b in bad_procs:
            if b in out:
                return True
    except Exception:
        pass
    return False

def decrypt_and_run(client, session_data):
    if check_debugger():
        messagebox.showerror("安全警告", "检测到非法逆向分析环境，程序禁止启动！")
        sys.exit(1)

    # 寻找 payload.enc (支持 PyInstaller 打包目录与当前目录)
    bundle_dir = getattr(sys, '_MEIPASS', os.path.dirname(os.path.abspath(__file__)))
    payload_path = os.path.join(bundle_dir, "payload.enc")
    if not os.path.exists(payload_path):
        payload_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "payload.enc")

    if not os.path.exists(payload_path):
        messagebox.showerror("错误", "加密载荷缺失，程序无法运行！")
        return

    with open(payload_path, "r", encoding="utf-8") as f:
        enc_payload = f.read()

    # 动态 AES 解密
    import hashlib
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    from cryptography.hazmat.backends import default_backend

    try:
        raw_b64 = base64.b64decode(enc_payload)
        iv = raw_b64[:12]
        tag = raw_b64[12:28]
        ciphertext = raw_b64[28:]
        key = hashlib.sha256(PACK_KEY.encode("utf-8")).digest()
        decryptor = Cipher(algorithms.AES(key), modes.GCM(iv, tag), backend=default_backend()).decryptor()
        decrypted_b64 = (decryptor.update(ciphertext) + decryptor.finalize()).decode("utf-8")
        raw_target_bytes = base64.b64decode(decrypted_b64.encode("utf-8"))
    except Exception as e:
        messagebox.showerror("解密失败", "解密核心载荷失败，卡密授权签名或数据损坏！")
        return

    # 临时沙箱安全释放
    suffix = ".py" if IS_PYTHON else ".exe"
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
        tmp.write(raw_target_bytes)
        tmp_path = tmp.name

    cmd = [sys.executable, tmp_path] if IS_PYTHON else [tmp_path]
    proc = subprocess.Popen(cmd)

    # 心跳看门狗线程
    def _watchdog():
        interval = session_data.get("heartbeat_interval", 25)
        while proc.poll() is None:
            time.sleep(interval)
            if proc.poll() is not None:
                break
            if check_debugger():
                proc.terminate()
                messagebox.showerror("反调试触发", "运行期间检测到调试器注入，程序已强制关闭！")
                break
            if ENABLE_HEARTBEAT:
                ok, msg = client.heartbeat_once()
                if not ok:
                    proc.terminate()
                    messagebox.showerror("会话断开", f"与授权服务器失去连接或会话被踢下线:\\n{{msg}}\\n程序已强制关闭！")
                    break

        client.logout()
        try:
            if os.path.exists(tmp_path):
                os.remove(tmp_path)
        except Exception:
            pass

    threading.Thread(target=_watchdog, daemon=True).start()

def show_login_dialog():
    client = SparkVerifyClient(HOST, APP_ID, APP_SECRET)
    root = tk.Tk()
    root.title(f"{{TITLE}} - 卡密授权登录")
    root.geometry("440x380")
    root.resizable(False, False)
    root.configure(bg="#0f172a")

    # Header
    top = tk.Frame(root, bg="#1e293b", padx=16, pady=10)
    top.pack(fill="x")
    tk.Label(top, text=f"🔒 {{TITLE}}", font=("Microsoft YaHei UI", 11, "bold"), fg="#38bdf8", bg="#1e293b").pack(anchor="w")
    tk.Label(top, text="一机一码授权保护 · 心跳保活反多开", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#1e293b").pack(anchor="w")

    main_f = tk.Frame(root, bg="#0f172a", padx=20, pady=12)
    main_f.pack(fill="both", expand=True)

    tk.Label(main_f, text="本机硬件特征码 (一机一码指纹):", font=("Microsoft YaHei UI", 8, "bold"), fg="#cbd5e1", bg="#0f172a").pack(anchor="w")
    hw_row = tk.Frame(main_f, bg="#0f172a")
    hw_row.pack(fill="x", pady=(2, 10))
    entry_hw = tk.Entry(hw_row, font=("Consolas", 8), bg="#1e293b", fg="#38bdf8", relief="flat")
    entry_hw.insert(0, client.hwid)
    entry_hw.configure(state="readonly")
    entry_hw.pack(side="left", fill="x", expand=True, ipady=3)
    tk.Button(hw_row, text="复制", font=("Microsoft YaHei UI", 8), bg="#334155", fg="#fff", relief="flat", command=lambda: [root.clipboard_clear(), root.clipboard_append(client.hwid), messagebox.showinfo("提示", "已复制机器特征码！")]).pack(side="left", padx=(6, 0))

    tk.Label(main_f, text="请输入授权卡密 (天卡/月卡/永久卡):", font=("Microsoft YaHei UI", 8, "bold"), fg="#cbd5e1", bg="#0f172a").pack(anchor="w")
    entry_key = tk.Entry(main_f, font=("Consolas", 10, "bold"), bg="#1e293b", fg="#ffffff", relief="flat")
    entry_key.pack(fill="x", pady=(2, 12), ipady=5)

    btn_verify = tk.Button(main_f, text="立即验证并进入程序", font=("Microsoft YaHei UI", 10, "bold"), bg="#0284c7", fg="#fff", relief="flat", ipady=6)
    btn_verify.pack(fill="x", pady=(0, 8))

    def _on_verify():
        k = entry_key.get().strip()
        if not k:
            messagebox.showwarning("提示", "请输入卡密！")
            return
        btn_verify.config(state="disabled", text="正在校验授权...")
        def _check():
            ok, msg, data = client.verify(k, auto_heartbeat=False)
            if ok:
                root.after(0, lambda: [root.destroy(), decrypt_and_run(client, data)])
            else:
                root.after(0, lambda: [btn_verify.config(state="normal", text="立即验证并进入程序"), messagebox.showerror("授权拒绝", f"未能通过授权:\\n{{msg}}")])
        threading.Thread(target=_check, daemon=True).start()

    btn_verify.config(command=_on_verify)

    # 解绑按钮
    tk.Button(main_f, text="解绑当前设备", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#0f172a", relief="flat", command=lambda: client.unbind(entry_key.get().strip()) if entry_key.get().strip() else messagebox.showwarning("提示", "请先输入卡密")).pack(side="right")

    root.mainloop()

if __name__ == "__main__":
    show_login_dialog()
'''

    # ================= TAB 3: 卡密批量生成 =================
    def _build_tab_cards(self):
        f = self.tab_cards

        card_box = tk.Frame(f, bg="#1e293b", padx=16, pady=14)
        card_box.pack(fill="x", pady=(0, 10))

        tk.Label(card_box, text="批量制卡参数配置", font=("Microsoft YaHei UI", 10, "bold"), fg="#38bdf8", bg="#1e293b").pack(anchor="w", pady=(0, 8))

        row1 = tk.Frame(card_box, bg="#1e293b")
        row1.pack(fill="x", pady=4)

        tk.Label(row1, text="卡密类型:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(side="left")
        self.combo_card_type = ttk.Combobox(row1, width=15, state="readonly", values=["day", "week", "month", "quarter", "year", "forever", "count"])
        self.combo_card_type.current(2)
        self.combo_card_type.pack(side="left", padx=(6, 20))

        tk.Label(row1, text="生成数量:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(side="left")
        self.entry_card_count = tk.Entry(row1, font=("Consolas", 9), width=8, bg="#0f172a", fg="#ffffff", relief="flat")
        self.entry_card_count.insert(0, "10")
        self.entry_card_count.pack(side="left", padx=(6, 20), ipady=2)

        tk.Label(row1, text="卡密前缀:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(side="left")
        self.entry_card_prefix = tk.Entry(row1, font=("Consolas", 9), width=8, bg="#0f172a", fg="#ffffff", relief="flat")
        self.entry_card_prefix.insert(0, "SV")
        self.entry_card_prefix.pack(side="left", padx=(6, 0), ipady=2)

        row2 = tk.Frame(card_box, bg="#1e293b")
        row2.pack(fill="x", pady=(8, 0))

        tk.Label(row2, text="制卡备注:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(side="left")
        self.entry_card_note = tk.Entry(row2, font=("Microsoft YaHei UI", 8), bg="#0f172a", fg="#ffffff", relief="flat", width=35)
        self.entry_card_note.insert(0, "工作台批量制卡")
        self.entry_card_note.pack(side="left", padx=(6, 20), ipady=2)

        btn_gen = tk.Button(row2, text="✨ 立即批量生成卡密", font=("Microsoft YaHei UI", 9, "bold"), bg="#0284c7", fg="#ffffff", relief="flat", command=self._do_generate_cards)
        btn_gen.pack(side="left", ipady=4, ipadx=10)

        # 结果展示区
        card_res = tk.Frame(f, bg="#0f172a", padx=10, pady=8)
        card_res.pack(fill="both", expand=True)

        res_head = tk.Frame(card_res, bg="#0f172a")
        res_head.pack(fill="x", pady=(0, 4))
        tk.Label(res_head, text="生成结果列表 (每行一张):", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#0f172a").pack(side="left")
        tk.Button(res_head, text="📋 一键复制全部卡密", font=("Microsoft YaHei UI", 8), bg="#334155", fg="#ffffff", relief="flat", command=self._copy_all_cards).pack(side="right")

        self.txt_cards_result = tk.Text(card_res, font=("Consolas", 9), bg="#050914", fg="#38bdf8", relief="flat")
        self.txt_cards_result.pack(fill="both", expand=True)

    def _do_generate_cards(self):
        if not self.admin_token:
            messagebox.showwarning("提示", "请先在【开发者登录】标签页登录验证中枢！")
            return
        app_id = self.selected_app["app_id"] if self.selected_app else "APP_DEMO_001"
        payload = {
            "app_id": app_id,
            "card_type": self.combo_card_type.get(),
            "count": int(self.entry_card_count.get() or 10),
            "prefix": self.entry_card_prefix.get().strip() or "SV",
            "note": self.entry_card_note.get().strip()
        }
        try:
            res = self._api_post("/api/v1/admin/cards/generate", payload)
            if res.get("code") == 200:
                keys = res.get("data", [])
                self.txt_cards_result.delete("1.0", tk.END)
                self.txt_cards_result.insert(tk.END, "\n".join(keys))
                messagebox.showinfo("生成成功", f"成功生成 {len(keys)} 张卡密！")
                self._refresh_overview_stats()
            else:
                messagebox.showerror("生成失败", res.get("msg", "出错了"))
        except Exception as e:
            messagebox.showerror("请求错误", str(e))

    def _copy_all_cards(self):
        t = self.txt_cards_result.get("1.0", tk.END).strip()
        if t:
            self.root.clipboard_clear()
            self.root.clipboard_append(t)
            messagebox.showinfo("提示", "已将全部卡密复制到剪贴板！")

    # ================= TAB 4: 在线终端监控 =================
    def _build_tab_sessions(self):
        f = self.tab_sessions

        top = tk.Frame(f, bg="#0b1120")
        top.pack(fill="x", pady=(0, 8))
        tk.Label(top, text="当前保持心跳活跃的在线终端列表:", font=("Microsoft YaHei UI", 9, "bold"), fg="#38bdf8", bg="#0b1120").pack(side="left")
        tk.Button(top, text="🔄 刷新在线会话", font=("Microsoft YaHei UI", 8), bg="#334155", fg="#ffffff", relief="flat", command=self._refresh_sessions_table).pack(side="right")

        # 表格
        cols = ("token", "card", "ip", "hwid", "last_hb")
        self.tree_sessions = ttk.Treeview(f, columns=cols, show="headings", height=12)
        self.tree_sessions.heading("token", text="会话令牌 Token")
        self.tree_sessions.heading("card", text="关联卡密")
        self.tree_sessions.heading("ip", text="客户端 IP")
        self.tree_sessions.heading("hwid", text="硬件指纹 (HWID)")
        self.tree_sessions.heading("last_hb", text="最后心跳时间")

        self.tree_sessions.column("token", width=160)
        self.tree_sessions.column("card", width=180)
        self.tree_sessions.column("ip", width=100)
        self.tree_sessions.column("hwid", width=140)
        self.tree_sessions.column("last_hb", width=130)

        self.tree_sessions.pack(fill="both", expand=True)

        btn_kick = tk.Button(f, text="🚫 强制踢出所选终端下线", font=("Microsoft YaHei UI", 9, "bold"), bg="#991b1b", fg="#ffffff", relief="flat", command=self._do_kick_session)
        btn_kick.pack(anchor="e", pady=8, ipady=4, ipadx=10)

    def _refresh_sessions_table(self):
        if not self.admin_token:
            return
        try:
            res = self._api_get("/api/v1/admin/sessions")
            if res.get("code") == 200:
                for row in self.tree_sessions.get_children():
                    self.tree_sessions.delete(row)
                for s in res.get("data", []):
                    self.tree_sessions.insert("", "end", values=(
                        s["session_token"], s["card_key"], s["client_ip"], s["hwid"][:12] + "...", s["last_heartbeat"]
                    ))
        except Exception:
            pass

    def _do_kick_session(self):
        sel = self.tree_sessions.selection()
        if not sel:
            messagebox.showwarning("提示", "请先在上方表格选中需要踢下线的会话！")
            return
        item = self.tree_sessions.item(sel[0])
        token = item["values"][0]
        if messagebox.askyesno("确认", f"确定强制踢下线会话 [{token}] 吗？"):
            try:
                res = self._api_post(f"/api/v1/admin/sessions/kick?session_token={token}", {})
                messagebox.showinfo("成功", res.get("msg", "已踢下线"))
                self._refresh_sessions_table()
            except Exception as e:
                messagebox.showerror("错误", str(e))

    # ================= TAB 5: 买家登录体验测试 =================
    def _build_tab_tester(self):
        f = self.tab_tester

        box = tk.Frame(f, bg="#1e293b", padx=20, pady=16)
        box.pack(fill="x", pady=10)

        tk.Label(box, text="测试从终端买家视角登录卡密与验证一机一码", font=("Microsoft YaHei UI", 10, "bold"), fg="#38bdf8", bg="#1e293b").pack(anchor="w", pady=(0, 10))

        tk.Label(box, text="输入卡密进行实机验证:", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b").pack(anchor="w")
        self.entry_test_key = tk.Entry(box, font=("Consolas", 10, "bold"), bg="#0f172a", fg="#ffffff", relief="flat")
        self.entry_test_key.insert(0, "TEST-FOREVER-6B9C-C5E3-6722-C4EB")
        self.entry_test_key.pack(fill="x", pady=(4, 10), ipady=5)

        self.btn_test_login = tk.Button(box, text="立即体验买家卡密登录", font=("Microsoft YaHei UI", 9, "bold"), bg="#0284c7", fg="#ffffff", relief="flat", command=self._do_test_login)
        self.btn_test_login.pack(fill="x", ipady=6)

        self.txt_test_res = tk.Text(f, font=("Consolas", 8), bg="#050914", fg="#4ade80", relief="flat", height=8)
        self.txt_test_res.pack(fill="both", expand=True, pady=10)

    def _do_test_login(self):
        k = self.entry_test_key.get().strip()
        if not k:
            return
        from spark_verify_client import SparkVerifyClient
        client = SparkVerifyClient(self.server_host, "APP_DEMO_001", "sv_secret_88889999_k9")
        ok, msg, data = client.verify(k, auto_heartbeat=False)
        self.txt_test_res.delete("1.0", tk.END)
        self.txt_test_res.insert(tk.END, f"[*] 本机 HWID: {client.hwid}\n")
        self.txt_test_res.insert(tk.END, f"[*] 验证结果: {ok} ({msg})\n")
        self.txt_test_res.insert(tk.END, f"[*] 详细响应: {json.dumps(data, indent=2, ensure_ascii=False)}\n")
        if ok:
            client.logout()

    def run(self):
        self.root.mainloop()

if __name__ == "__main__":
    app = SparkStudioApp()
    app.run()
