"""
SparkVerify 极验软件加密外壳 - 客户端图形化卡密验证与守护中枢
支持: 一机一码采集展示、卡密记忆、公告拉取、解密启动与心跳看门狗
"""

import os
import sys
import time
import json
import threading
import subprocess
import tkinter as tk
from tkinter import ttk, messagebox

# 导入 SDK
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "clients", "python")))
from spark_verify_client import SparkVerifyClient

KEY_CACHE_FILE = os.path.join(os.path.dirname(__file__), ".saved_license.json")

class SparkVerifyLoginDialog:
    def __init__(self, host="http://127.0.0.1:8989", app_id="APP_DEMO_001", app_secret="sv_secret_88889999_k9", on_success=None):
        self.host = host
        self.app_id = app_id
        self.app_secret = app_secret
        self.on_success = on_success
        self.verified = False
        
        self.client = SparkVerifyClient(self.host, self.app_id, self.app_secret)
        self.root = tk.Tk()
        self.root.title("软件安全授权认证 - SparkVerify")
        self.root.geometry("460x420")
        self.root.resizable(False, False)
        self.root.configure(bg="#0f172a")

        self._build_ui()
        self._load_saved_key()
        self._fetch_app_info()

    def _build_ui(self):
        # 顶部标题栏
        top_frame = tk.Frame(self.root, bg="#1e293b", height=60)
        top_frame.pack(fill="x")
        
        lbl_title = tk.Label(top_frame, text="🔒 软件卡密授权验证", font=("Microsoft YaHei UI", 12, "bold"), fg="#38bdf8", bg="#1e293b")
        lbl_title.pack(anchor="w", padx=16, pady=(12, 2))
        lbl_sub = tk.Label(top_frame, text="一机一码设备绑定 · 心跳保活与反多开保护", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#1e293b")
        lbl_sub.pack(anchor="w", padx=16, pady=(0, 10))

        # 中部主体区
        main_frame = tk.Frame(self.root, bg="#0f172a", padx=20, pady=15)
        main_frame.pack(fill="both", expand=True)

        # 公告栏
        self.lbl_notice = tk.Label(main_frame, text="正在连接验证服务器拉取公告...", font=("Microsoft YaHei UI", 8), fg="#cbd5e1", bg="#1e293b", wraplength=410, justify="left", padx=10, pady=6)
        self.lbl_notice.pack(fill="x", pady=(0, 15))

        # 硬件指纹 (HWID)
        lbl_hwid_tag = tk.Label(main_frame, text="本机硬件特征码 (一机一码指纹):", font=("Microsoft YaHei UI", 9, "bold"), fg="#e2e8f0", bg="#0f172a")
        lbl_hwid_tag.pack(anchor="w")

        hwid_box = tk.Frame(main_frame, bg="#0f172a")
        hwid_box.pack(fill="x", pady=(4, 12))

        self.entry_hwid = tk.Entry(hwid_box, font=("Consolas", 9), bg="#1e293b", fg="#38bdf8", relief="flat", insertbackground="#38bdf8")
        self.entry_hwid.insert(0, self.client.hwid)
        self.entry_hwid.configure(state="readonly")
        self.entry_hwid.pack(side="left", fill="x", expand=True, ipady=4)

        btn_copy = tk.Button(hwid_box, text="复制", font=("Microsoft YaHei UI", 8), bg="#334155", fg="#f8fafc", activebackground="#475569", activeforeground="#ffffff", relief="flat", command=self._copy_hwid, padx=8)
        btn_copy.pack(side="left", padx=(6, 0))

        # 卡密输入框
        lbl_key_tag = tk.Label(main_frame, text="请输入授权卡密 (天卡/月卡/年卡/永久卡):", font=("Microsoft YaHei UI", 9, "bold"), fg="#e2e8f0", bg="#0f172a")
        lbl_key_tag.pack(anchor="w")

        self.entry_key = tk.Entry(main_frame, font=("Consolas", 10, "bold"), bg="#1e293b", fg="#f8fafc", relief="flat", insertbackground="#38bdf8")
        self.entry_key.pack(fill="x", pady=(4, 8), ipady=5)

        # 选项框 (记住卡密)
        self.var_remember = tk.BooleanVar(value=True)
        chk_remember = tk.Checkbutton(main_frame, text="记住卡密 (下次启动免手动输入)", variable=self.var_remember, font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#0f172a", selectcolor="#1e293b", activebackground="#0f172a", activeforeground="#e2e8f0")
        chk_remember.pack(anchor="w", pady=(0, 10))

        # 状态提示文本
        self.lbl_status = tk.Label(main_frame, text="", font=("Microsoft YaHei UI", 8), fg="#f87171", bg="#0f172a")
        self.lbl_status.pack(anchor="w", pady=(0, 8))

        # 底部按钮区
        btn_box = tk.Frame(main_frame, bg="#0f172a")
        btn_box.pack(fill="x", pady=(0, 5))

        self.btn_verify = tk.Button(btn_box, text="立即验证并启动程序", font=("Microsoft YaHei UI", 10, "bold"), bg="#0284c7", fg="#ffffff", activebackground="#0369a1", activeforeground="#ffffff", relief="flat", command=self._do_verify)
        self.btn_verify.pack(fill="x", ipady=6)

        # 解绑按钮与辅助链接
        extra_box = tk.Frame(main_frame, bg="#0f172a")
        extra_box.pack(fill="x", pady=(8, 0))

        btn_unbind = tk.Button(extra_box, text="解绑当前设备", font=("Microsoft YaHei UI", 8), fg="#94a3b8", bg="#0f172a", relief="flat", activebackground="#0f172a", activeforeground="#38bdf8", command=self._do_unbind)
        btn_unbind.pack(side="left")

        lbl_version = tk.Label(extra_box, text="极验安全防护内核 v1.0", font=("Microsoft YaHei UI", 8), fg="#64748b", bg="#0f172a")
        lbl_version.pack(side="right")

    def _copy_hwid(self):
        self.root.clipboard_clear()
        self.root.clipboard_append(self.client.hwid)
        messagebox.showinfo("提示", "机器特征码已成功复制到剪贴板！")

    def _load_saved_key(self):
        if os.path.exists(KEY_CACHE_FILE):
            try:
                with open(KEY_CACHE_FILE, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    saved = data.get("card_key", "")
                    if saved:
                        self.entry_key.insert(0, saved)
            except Exception:
                pass

    def _save_key(self, card_key):
        if self.var_remember.get():
            try:
                with open(KEY_CACHE_FILE, "w", encoding="utf-8") as f:
                    json.dump({"card_key": card_key}, f)
            except Exception:
                pass
        else:
            if os.path.exists(KEY_CACHE_FILE):
                try:
                    os.remove(KEY_CACHE_FILE)
                except Exception:
                    pass

    def _fetch_app_info(self):
        def _worker():
            res = self.client.init_app()
            if res.get("code") == 200:
                data = res.get("data", {})
                notice = data.get("notice", "欢迎使用本软件！")
                self.lbl_notice.config(text=f"📢 官方公告: {notice}")
            else:
                self.lbl_notice.config(text=f"⚠️ 未能连接至授权服务器: {res.get('msg')}")
        threading.Thread(target=_worker, daemon=True).start()

    def _do_verify(self):
        card_key = self.entry_key.get().strip()
        if not card_key:
            self.lbl_status.config(text="请输入有效的卡密后再点击验证！", fg="#f87171")
            return

        self.btn_verify.config(state="disabled", text="正在校验授权...")
        self.lbl_status.config(text="连接服务器校验卡密与设备指纹...", fg="#38bdf8")

        def _worker():
            ok, msg, data = self.client.verify(card_key, auto_heartbeat=False)
            if ok:
                self._save_key(card_key)
                self.verified = True
                self.root.after(0, lambda: self._on_verify_passed(data))
            else:
                self.root.after(0, lambda: self._on_verify_failed(msg))

        threading.Thread(target=_worker, daemon=True).start()

    def _on_verify_passed(self, data):
        self.lbl_status.config(text="授权验证通过！正在进入程序...", fg="#4ade80")
        messagebox.showinfo("授权成功", f"欢迎使用！\n到期时间: {data.get('expires_at')}\n卡密类型: {data.get('card_type')}")
        self.root.destroy()
        if self.on_success:
            self.on_success(self.client, data)

    def _on_verify_failed(self, msg):
        self.btn_verify.config(state="normal", text="立即验证并启动程序")
        self.lbl_status.config(text=f"验证失败: {msg}", fg="#f87171")
        messagebox.showerror("授权受阻", f"未能通过授权验证:\n\n{msg}")

    def _do_unbind(self):
        card_key = self.entry_key.get().strip()
        if not card_key:
            messagebox.showwarning("提示", "请先在上方输入框填入需要解绑的卡密！")
            return
        if not messagebox.askyesno("解绑确认", "确定解绑当前机器吗？换绑可能有冷却时间或次数限制。"):
            return
        ok, msg = self.client.unbind(card_key)
        if ok:
            messagebox.showinfo("成功", f"解绑操作完成: {msg}")
        else:
            messagebox.showerror("失败", f"解绑失败: {msg}")

    def run(self):
        self.root.mainloop()
        return self.verified, self.client
