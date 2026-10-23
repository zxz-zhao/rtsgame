# -*- coding: utf-8 -*-
"""
Spark & Cockpit AI 模型中转站 - 分组路由与多账号换号管理中枢
核心功能:
1. 同类型模型归属同一分组 (Claude / Gemini / OpenAI / DeepSeek)
2. 固化中转 Master Key (sk-cockpit-master-2026)，客户端一次配置永久有效
3. 自由直接换号：额度未到期也可随时在 Web 控制台一键无感切换当前使用账号
4. 额度耗尽 / 429 自动故障转移 (Failover)
"""

import os
import sys
import json
import time
from typing import Dict, Any, List, Optional

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
GROUP_SETTINGS_FILE = os.path.join(TOOLS_DIR, "group_settings.json")
ACCOUNTS_POOL_FILE = os.path.join(TOOLS_DIR, "accounts_pool.json")

# 统一固化中转 Master Key
DEFAULT_MASTER_KEY = "sk-cockpit-master-2026"

MODEL_GROUPS = {
    "claude": {
        "id": "claude",
        "name": "Claude 旗舰智商组",
        "badge": "Anthropic",
        "icon": "fa-solid fa-brain",
        "badge_color": "bg-purple-950/80 text-purple-300 border-purple-700/60",
        "accent": "purple",
        "description": "顶尖架构设计、深度推理、长逻辑高保真代码生成",
        "models": [
            "claude-sonnet-4-6",
            "claude-opus-4-6",
            "claude-opus-4-6-thinking",
            "claude-3-7-sonnet",
            "claude-3-5-sonnet"
        ],
        "quota_key": "claude_pct",
        "provider": "google_codeassist"
    },
    "gemini": {
        "id": "gemini",
        "name": "Gemini 极速长文组",
        "badge": "Google",
        "icon": "fa-solid fa-bolt",
        "badge_color": "bg-cyan-950/80 text-cyan-300 border-cyan-700/60",
        "accent": "cyan",
        "description": "超高速响应、海量上下文窗口、代码分析与多模态检索",
        "models": [
            "gemini-3.8-flash",
            "gemini-3.8-flash-high",
            "gemini-3.1-pro",
            "gemini-2.5-flash",
            "gemini-pro",
            "gemini-2.5-flash-thinking"
        ],
        "quota_key": "gemini_pct",
        "provider": "google_codeassist"
    },
    "openai": {
        "id": "openai",
        "name": "OpenAI / GPT 前沿组",
        "badge": "OpenAI",
        "icon": "fa-solid fa-network-wired",
        "badge_color": "bg-emerald-950/80 text-emerald-300 border-emerald-700/60",
        "accent": "emerald",
        "description": "GPT-4o 与 GPT-5/6 前沿通用基模，多上游渠道热备",
        "models": [
            "gpt-4o",
            "gpt-5.5",
            "gpt-5.6-luna",
            "gpt-5.6-sol",
            "gpt-5.6-terra",
            "gpt-6-astra"
        ],
        "quota_key": "fixed",
        "provider": "sub2api_chatos"
    },
    "deepseek": {
        "id": "deepseek",
        "name": "DeepSeek 深度思考组",
        "badge": "DeepSeek",
        "icon": "fa-solid fa-water",
        "badge_color": "bg-blue-950/80 text-blue-300 border-blue-700/60",
        "accent": "blue",
        "description": "高性价比开源基模、数学推导与深度代码理解",
        "models": [
            "deepseek-chat",
            "chatos-deepseek",
            "deepseek-reasoner"
        ],
        "quota_key": "fixed",
        "provider": "chatos"
    }
}


class ModelGroupManager:
    def __init__(self):
        self.settings: Dict[str, Any] = self._load_settings()
        self.exhausted_map: Dict[str, set] = {
            "claude": set(),
            "gemini": set(),
            "openai": set(),
            "deepseek": set()
        }

    def _load_settings(self) -> Dict[str, Any]:
        defaults = {
            "master_key": DEFAULT_MASTER_KEY,
            "active_accounts": {
                "claude": "adjisundoroo@gmail.com",
                "gemini": "nok0870207369@gmail.com",
                "openai": "Sub2API 主力渠道 (大王AI)",
                "deepseek": "ChatOS 镜像通道"
            },
            "auto_failover": True
        }
        if os.path.exists(GROUP_SETTINGS_FILE):
            try:
                with open(GROUP_SETTINGS_FILE, "r", encoding="utf-8") as f:
                    loaded = json.load(f)
                    defaults["master_key"] = loaded.get("master_key", DEFAULT_MASTER_KEY)
                    defaults["auto_failover"] = loaded.get("auto_failover", True)
                    if "active_accounts" in loaded and isinstance(loaded["active_accounts"], dict):
                        defaults["active_accounts"].update(loaded["active_accounts"])
            except Exception as e:
                print(f"[GroupManager] Warning loading settings: {e}")
        return defaults

    def save_settings(self):
        try:
            with open(GROUP_SETTINGS_FILE, "w", encoding="utf-8") as f:
                json.dump(self.settings, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"[GroupManager] Failed to save settings: {e}")

    @property
    def master_key(self) -> str:
        return self.settings.get("master_key", DEFAULT_MASTER_KEY)

    @property
    def auto_failover(self) -> bool:
        return self.settings.get("auto_failover", True)

    def set_auto_failover(self, enabled: bool):
        self.settings["auto_failover"] = bool(enabled)
        self.save_settings()

    def get_group_for_model(self, model_name: str) -> str:
        m = (model_name or "").lower().strip()
        if "claude" in m:
            return "claude"
        if "gemini" in m:
            return "gemini"
        if "deepseek" in m:
            return "deepseek"
        if "gpt" in m:
            return "openai"
        for gid, ginfo in MODEL_GROUPS.items():
            if model_name in ginfo["models"]:
                return gid
        return "gemini"

    def mark_account_exhausted(self, group_id: str, account_id: str):
        if group_id in self.exhausted_map and account_id:
            print(f"[GroupManager] Marking account [{account_id}] exhausted in group [{group_id}]")
            self.exhausted_map[group_id].add(account_id)

    def switch_account(self, group_id: str, account_id: str) -> Dict[str, Any]:
        if group_id not in MODEL_GROUPS:
            return {"code": 400, "msg": f"无效的模型分组: {group_id}"}

        # 换号时从该分组的耗尽黑名单中解除，恢复活跃
        if group_id in self.exhausted_map and account_id in self.exhausted_map[group_id]:
            self.exhausted_map[group_id].remove(account_id)

        self.settings["active_accounts"][group_id] = account_id
        self.save_settings()
        print(f"[GroupManager] Successfully switched active account for group [{group_id}] to [{account_id}]")
        return {
            "code": 200,
            "msg": f"已成功将 [{MODEL_GROUPS[group_id]['name']}] 当前生效账号切换为 [{account_id}]！",
            "group": group_id,
            "active_account": account_id
        }

    def get_active_account_for_group(
        self,
        group_id: str,
        accounts_pool: List[Dict[str, Any]],
        exclude_emails: Optional[List[str]] = None
    ) -> Optional[Dict[str, Any]]:
        """
        获取指定分组当前生效的账号：
        1. 优先使用用户在控制台手动指定的 active_account (非 disabled)
        2. 如果该账号触发熔断、被排除或额度耗尽，且开启了 auto_failover，则自动轮换到同分组下最高额度的健康账号
        """
        if not accounts_pool:
            return None

        excludes = set(self.exhausted_map.get(group_id, set()))
        if exclude_emails:
            excludes.update(exclude_emails)

        configured_active = self.settings.get("active_accounts", {}).get(group_id)

        # 1. 尝试使用当前用户配置指定的账号 (必须可用且非 disabled)
        if configured_active and configured_active not in excludes:
            matched = next((a for a in accounts_pool if a.get("email") == configured_active and not a.get("disabled")), None)
            if matched:
                return matched

        # 2. 如果配置账号已被排除或未设置，自动选择同类型模型分组下额度最高的健康账号
        quota_key = MODEL_GROUPS.get(group_id, {}).get("quota_key", "claude_pct")
        candidates = [a for a in accounts_pool if not a.get("disabled") and a.get("email") not in excludes]
        if not candidates:
            # 所有健康账号都在黑名单中，重置以恢复重试
            print(f"[GroupManager] All accounts in group [{group_id}] exhausted. Resetting exhausted map.")
            self.exhausted_map[group_id].clear()
            candidates = [a for a in accounts_pool if not a.get("disabled")]

        if candidates:
            if quota_key in ("claude_pct", "gemini_pct"):
                candidates.sort(key=lambda a: (a.get(quota_key, 0), a.get("gemini_pct", 0)), reverse=True)
            chosen = candidates[0]
            new_email = chosen.get("email")
            if new_email and new_email != configured_active:
                print(f"[GroupManager] Auto-switched group [{group_id}] active account: {configured_active} -> {new_email} (Remaining: {chosen.get(quota_key, 0)}%)")
                self.settings["active_accounts"][group_id] = new_email
                self.save_settings()
            return chosen

        return None

    def get_dashboard_summary(self, accounts_pool: List[Dict[str, Any]]) -> Dict[str, Any]:
        """生成供 Web 控制台展示的完整结构数据"""
        groups_data = []

        for gid, ginfo in MODEL_GROUPS.items():
            active_account = self.settings.get("active_accounts", {}).get(gid)
            quota_key = ginfo.get("quota_key", "fixed")

            accounts_list = []
            if gid in ("claude", "gemini"):
                for a in accounts_pool:
                    email = a.get("email", "")
                    c_pct = a.get("claude_pct", 0)
                    g_pct = a.get("gemini_pct", 0)
                    disabled = a.get("disabled", False)
                    is_active = (email == active_account)
                    is_exhausted = (email in self.exhausted_map.get(gid, set())) or (c_pct <= 0 and g_pct <= 0)

                    if disabled:
                        status = "DISABLED"
                    elif is_active:
                        status = "ACTIVE"
                    elif is_exhausted:
                        status = "EXHAUSTED"
                    else:
                        status = "STANDBY"

                    accounts_list.append({
                        "id": email,
                        "name": email,
                        "claude_pct": c_pct,
                        "gemini_pct": g_pct,
                        "disabled": disabled,
                        "status": status,
                        "is_active": is_active,
                        "expiry": a.get("expiry_timestamp") or (a.get("token", {}).get("expiry_timestamp", 0) if isinstance(a.get("token"), dict) else 0)
                    })
            elif gid == "openai":
                accounts_list = [
                    {
                        "id": "Sub2API 主力渠道 (大王AI)",
                        "name": "Sub2API 主力渠道 (大王AI)",
                        "claude_pct": 100,
                        "gemini_pct": 100,
                        "status": "ACTIVE" if active_account == "Sub2API 主力渠道 (大王AI)" else "STANDBY",
                        "is_active": (active_account == "Sub2API 主力渠道 (大王AI)"),
                        "note": "支持 gpt-6-astra, gpt-5.5, gpt-5.6 前沿模型"
                    },
                    {
                        "id": "ChatOS 镜像渠道 (OpenAI 通道)",
                        "name": "ChatOS 镜像渠道 (OpenAI 通道)",
                        "claude_pct": 100,
                        "gemini_pct": 100,
                        "status": "ACTIVE" if active_account == "ChatOS 镜像渠道 (OpenAI 通道)" else "STANDBY",
                        "is_active": (active_account == "ChatOS 镜像渠道 (OpenAI 通道)"),
                        "note": "支持 gpt-4o 等通用基模"
                    }
                ]
            elif gid == "deepseek":
                accounts_list = [
                    {
                        "id": "ChatOS 镜像通道",
                        "name": "ChatOS 镜像通道",
                        "claude_pct": 100,
                        "gemini_pct": 100,
                        "status": "ACTIVE" if active_account == "ChatOS 镜像通道" else "STANDBY",
                        "is_active": (active_account == "ChatOS 镜像通道"),
                        "note": "支持 deepseek-chat, chatos-deepseek"
                    }
                ]

            groups_data.append({
                "id": gid,
                "name": ginfo["name"],
                "badge": ginfo["badge"],
                "icon": ginfo["icon"],
                "badge_color": ginfo["badge_color"],
                "accent": ginfo["accent"],
                "description": ginfo["description"],
                "models": ginfo["models"],
                "active_account": active_account,
                "accounts": accounts_list
            })

        return {
            "code": 200,
            "master_key": self.master_key,
            "base_url": "http://127.0.0.1:8000/v1",
            "lan_url": "http://192.168.1.176:8000/v1",
            "auto_failover": self.auto_failover,
            "groups": groups_data
        }


# 全局单例管理器
group_manager = ModelGroupManager()


# ================= Web 控制台 HTML 模板 =================
CONSOLE_HTML = """<!DOCTYPE html>
<html lang="zh-CN" class="dark">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>TT 智能模型中枢 - 分组路由与多账号换号管理平台</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
  <script>
    tailwind.config = {
      darkMode: 'class',
      theme: {
        extend: {
          colors: {
            cyber: {
              900: '#070a13',
              800: '#0e1424',
              700: '#1a233a',
              border: 'rgba(255, 255, 255, 0.08)'
            }
          }
        }
      }
    }
  </script>
  <style>
    body {
      background: radial-gradient(circle at 10% 20%, #0d1527 0%, #060913 100%);
      color: #e2e8f0;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .glass-card {
      background: rgba(14, 20, 36, 0.75);
      backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: 0 8px 32px 0 rgba(0, 0, 0, 0.4);
    }
    .glow-purple { box-shadow: 0 0 25px -4px rgba(168, 85, 247, 0.25); }
    .glow-cyan { box-shadow: 0 0 25px -4px rgba(6, 182, 212, 0.25); }
    .glow-emerald { box-shadow: 0 0 25px -4px rgba(16, 185, 129, 0.25); }
  </style>
</head>
<body class="min-h-screen flex flex-col antialiased">

  <!-- 顶部导航栏 -->
  <header class="h-16 glass-card border-b border-cyber-border px-6 flex items-center justify-between sticky top-0 z-40">
    <div class="flex items-center space-x-3">
      <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-500 to-purple-600 flex items-center justify-center text-white shadow-lg shadow-purple-500/20">
        <i class="fa-solid fa-bolt text-lg"></i>
      </div>
      <div>
        <div class="flex items-center space-x-2">
          <span class="font-extrabold tracking-wider text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 via-indigo-300 to-purple-400 text-lg">TT RELAY HUB</span>
          <span class="text-xs px-2 py-0.5 rounded-full bg-cyan-950/80 text-cyan-400 border border-cyan-800/50">TT 智能调度系统</span>
        </div>
        <p class="text-[11px] text-slate-400">同类型模型同分组 · TT 专属固化 Key 永不更换 · 额度未到期自由直接换号</p>
      </div>
    </div>

    <!-- 运行状态与刷新 -->
    <div class="flex items-center space-x-3">
      <div class="flex items-center space-x-2 bg-emerald-950/40 border border-emerald-800/40 rounded-lg px-3 py-1.5 text-xs text-emerald-400">
        <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
        <span>TT 核心中枢监听: <strong>0.0.0.0:8000</strong></span>
      </div>

      <button onclick="loadDashboard()" title="刷新面板数据" class="w-9 h-9 rounded-lg bg-slate-800/80 hover:bg-slate-700 border border-slate-700 text-slate-300 flex items-center justify-center transition">
        <i class="fa-solid fa-rotate-right"></i>
      </button>
    </div>
  </header>

  <!-- 主容器 -->
  <main class="flex-1 max-w-7xl w-full mx-auto p-6 space-y-6">

    <!-- 固化中转 Key 与接入 Base URL 展示横幅 -->
    <div class="glass-card rounded-2xl p-6 border border-cyan-800/30 bg-gradient-to-r from-cyan-950/30 via-slate-900/50 to-purple-950/30 flex flex-col md:flex-row md:items-center justify-between gap-6">
      <div class="space-y-1">
        <div class="flex items-center space-x-2">
          <span class="text-xs uppercase font-bold tracking-wider text-cyan-400 flex items-center space-x-1">
            <i class="fa-solid fa-shield-halved"></i>
            <span>TT 客户端固定配置参数 (一次填入 永久使用)</span>
          </span>
        </div>
        <h2 class="text-xl font-black text-white">TT 统一固定 API Key 与 Base URL</h2>
        <p class="text-xs text-slate-400 leading-relaxed max-w-2xl">
          在任何下游工具或应用中只需配置以下参数。后台任意直接换号或自动轮换，客户端 Key 永久有效，业务零中断！
        </p>
      </div>

      <div class="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
        <!-- Base URL 框 -->
        <div class="bg-slate-900/90 border border-slate-700/80 rounded-xl px-4 py-2 flex items-center justify-between space-x-3">
          <div class="text-left">
            <p class="text-[10px] text-slate-400 font-mono">API BASE URL</p>
            <p id="txtBaseUrl" class="text-xs font-mono font-bold text-cyan-300">http://127.0.0.1:8000/v1</p>
          </div>
          <button onclick="copyElementText('txtBaseUrl')" class="text-slate-400 hover:text-white p-1" title="复制 Base URL"><i class="fa-solid fa-copy"></i></button>
        </div>

        <!-- Master Key 框 -->
        <div class="bg-slate-900/90 border border-cyan-700/60 rounded-xl px-4 py-2 flex items-center justify-between space-x-3">
          <div class="text-left">
            <p class="text-[10px] text-cyan-400 font-mono">MASTER KEY (固化密钥)</p>
            <p id="txtMasterKey" class="text-xs font-mono font-bold text-white">sk-cockpit-master-2026</p>
          </div>
          <button onclick="copyElementText('txtMasterKey')" class="text-cyan-400 hover:text-cyan-200 p-1" title="复制 Master Key"><i class="fa-solid fa-copy"></i></button>
        </div>
      </div>
    </div>

    <!-- 快捷操作栏与全局开关 -->
    <div class="flex items-center justify-between">
      <div class="flex items-center space-x-2">
        <i class="fa-solid fa-layer-group text-purple-400 text-lg"></i>
        <h3 class="text-base font-bold text-white">同类型模型分组与实时换号看板</h3>
      </div>

      <div class="flex items-center space-x-3 text-xs">
        <label class="flex items-center space-x-2 cursor-pointer bg-slate-900/80 border border-slate-700 rounded-lg px-3 py-1.5">
          <input type="checkbox" id="chkAutoFailover" onchange="toggleAutoFailover()" checked class="rounded border-slate-700 text-purple-600 focus:ring-0">
          <span class="text-slate-300">额度耗尽自动故障换号 (Auto Failover)</span>
        </label>

        <button onclick="syncCockpitAccounts()" class="px-3 py-1.5 rounded-lg bg-indigo-950/80 hover:bg-indigo-900 border border-indigo-700/60 text-indigo-300 font-medium transition flex items-center space-x-1.5">
          <i class="fa-solid fa-arrows-rotate"></i>
          <span>一键同步 Cockpit 账号池</span>
        </button>
      </div>
    </div>

    <!-- 四大模型分组动态卡片网格 -->
    <div id="groupsGrid" class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <!-- 动态通过 JavaScript 渲染 -->
    </div>

    <!-- 底部常用客户端配置快速参考 -->
    <div class="glass-card rounded-2xl p-6 border border-slate-800 space-y-4 text-xs">
      <div class="flex items-center space-x-2 text-slate-300 font-bold">
        <i class="fa-solid fa-book-bookmark text-cyan-400"></i>
        <span>下游客户端免换 Key 接入指南</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 font-mono text-[11px]">
        <div class="bg-slate-900/70 p-3 rounded-xl border border-slate-800 space-y-1.5">
          <p class="font-bold text-purple-300">1. Dify 工作流配置</p>
          <p class="text-slate-400">模型供应商: OpenAI-API-compatible</p>
          <p class="text-slate-400">API 域名: <code>http://192.168.1.176:8000/v1</code></p>
          <p class="text-slate-400">API Key: <code>sk-cockpit-master-2026</code></p>
        </div>

        <div class="bg-slate-900/70 p-3 rounded-xl border border-slate-800 space-y-1.5">
          <p class="font-bold text-cyan-300">2. Claude Code 命令行助手</p>
          <p class="text-slate-400">ANTHROPIC_BASE_URL: <code>http://127.0.0.1:8000/v1</code></p>
          <p class="text-slate-400">ANTHROPIC_API_KEY: <code>sk-cockpit-master-2026</code></p>
          <p class="text-slate-400">MODEL: <code>claude-sonnet-4-6</code></p>
        </div>

        <div class="bg-slate-900/70 p-3 rounded-xl border border-slate-800 space-y-1.5">
          <p class="font-bold text-emerald-300">3. Cursor / NextChat / 本地脚本</p>
          <p class="text-slate-400">OpenAI Base URL: <code>http://127.0.0.1:8000/v1</code></p>
          <p class="text-slate-400">OpenAI API Key: <code>sk-cockpit-master-2026</code></p>
          <p class="text-slate-400">模型随意选: <code>claude-sonnet-4-6</code> / <code>gemini-3.8-flash</code></p>
        </div>
      </div>
    </div>
  </main>

  <!-- 提示浮层 Toast -->
  <div id="toast" class="fixed bottom-6 right-6 z-50 transform translate-y-20 opacity-0 transition-all duration-300 pointer-events-none px-4 py-3 rounded-xl shadow-xl border text-sm flex items-center space-x-2 bg-emerald-950/90 border-emerald-800 text-emerald-300">
    <i class="fa-solid fa-circle-check"></i>
    <span id="toastMsg">通知信息</span>
  </div>

  <script>
    function showToast(msg, isError = false) {
      const t = document.getElementById("toast");
      const m = document.getElementById("toastMsg");
      m.innerText = msg;
      if (isError) {
        t.className = "fixed bottom-6 right-6 z-50 opacity-100 translate-y-0 transition-all duration-300 px-4 py-3 rounded-xl shadow-xl border text-sm flex items-center space-x-2 bg-red-950/90 border-red-800 text-red-300";
      } else {
        t.className = "fixed bottom-6 right-6 z-50 opacity-100 translate-y-0 transition-all duration-300 px-4 py-3 rounded-xl shadow-xl border text-sm flex items-center space-x-2 bg-emerald-950/90 border-emerald-800 text-emerald-300";
      }
      setTimeout(() => {
        t.classList.add("translate-y-20", "opacity-0");
      }, 3000);
    }

    function copyElementText(id) {
      const text = document.getElementById(id).innerText;
      navigator.clipboard.writeText(text).then(() => {
        showToast("已成功复制到剪贴板！");
      });
    }

    async function loadDashboard() {
      try {
        const resp = await fetch("/api/groups");
        const data = await resp.json();
        if (data.code === 200) {
          document.getElementById("txtMasterKey").innerText = data.master_key;
          document.getElementById("chkAutoFailover").checked = !!data.auto_failover;
          renderGroups(data.groups);
        }
      } catch (e) {
        console.error(e);
      }
    }

    function renderGroups(groups) {
      const container = document.getElementById("groupsGrid");
      container.innerHTML = groups.map(g => {
        return `
          <div class="glass-card rounded-2xl p-6 border border-slate-800 flex flex-col justify-between space-y-4">
            <!-- 头部 -->
            <div>
              <div class="flex items-center justify-between pb-3 border-b border-slate-800/80">
                <div class="flex items-center space-x-2.5">
                  <div class="w-9 h-9 rounded-lg bg-slate-900 border border-slate-700/80 flex items-center justify-center text-sm">
                    <i class="${g.icon}"></i>
                  </div>
                  <div>
                    <h4 class="font-extrabold text-white text-sm">${g.name}</h4>
                    <p class="text-[11px] text-slate-400">${g.description}</p>
                  </div>
                </div>
                <span class="px-2.5 py-0.5 rounded-full text-[10px] font-mono border ${g.badge_color}">${g.badge}</span>
              </div>

              <!-- 包含模型 -->
              <div class="mt-3 flex flex-wrap gap-1.5">
                ${g.models.map(m => `<span class="px-2 py-0.5 rounded bg-slate-900/90 border border-slate-800 text-slate-300 text-[10px] font-mono">${m}</span>`).join('')}
              </div>

              <!-- 当前活跃账号提醒 -->
              <div class="mt-4 p-2.5 rounded-xl bg-slate-900/80 border border-slate-700/60 flex items-center justify-between text-xs">
                <span class="text-slate-400">当前使用账号:</span>
                <span class="font-mono font-bold text-emerald-400 flex items-center space-x-1.5">
                  <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                  <span class="truncate max-w-[200px]" title="${g.active_account}">${g.active_account || '自动优选'}</span>
                </span>
              </div>

              <!-- 账号列表与直接换号操作 -->
              <div class="mt-4 space-y-2">
                <div class="text-[11px] uppercase tracking-wider text-slate-500 font-bold px-1">候选账号池 (点击即可随时直接换号):</div>
                <div class="space-y-2">
                  ${g.accounts.map(acc => {
                    const isActive = acc.is_active;
                    const pct = (g.id === 'claude') ? acc.claude_pct : acc.gemini_pct;
                    const pctText = (g.id === 'claude' || g.id === 'gemini') ? `剩余额度: ${pct}%` : (acc.note || '热备就绪');
                    const barWidth = (g.id === 'claude' || g.id === 'gemini') ? `${pct}%` : '100%';

                    return `
                      <div class="p-3 rounded-xl border ${isActive ? 'bg-indigo-950/30 border-purple-600/60' : 'bg-slate-900/40 border-slate-800/80'} flex items-center justify-between text-xs transition">
                        <div class="space-y-1 flex-1 pr-3">
                          <div class="flex items-center space-x-2">
                            <span class="font-mono font-semibold ${isActive ? 'text-purple-300 font-bold' : 'text-slate-300'} truncate max-w-[210px]" title="${acc.name}">${acc.name}</span>
                            ${isActive ? '<span class="px-1.5 py-0.2 rounded text-[10px] bg-emerald-950 text-emerald-400 border border-emerald-800">使用中</span>' : ''}
                          </div>
                          <!-- 进度条 -->
                          <div class="flex items-center space-x-2">
                            <div class="flex-1 h-1.5 bg-slate-800 rounded-full overflow-hidden">
                              <div class="h-full ${pct > 40 ? 'bg-emerald-400' : (pct > 15 ? 'bg-amber-400' : 'bg-red-500')}" style="width: ${barWidth}"></div>
                            </div>
                            <span class="text-[10px] text-slate-400 font-mono">${pctText}</span>
                          </div>
                        </div>

                        <div>
                          ${isActive
                            ? `<span class="px-3 py-1.5 rounded-lg bg-emerald-950/60 text-emerald-300 border border-emerald-800/50 font-bold text-xs flex items-center space-x-1"><i class="fa-solid fa-check"></i><span>生效中</span></span>`
                            : `<button onclick="switchGroupAccount('${g.id}', '${acc.id}')" class="px-3 py-1.5 rounded-lg bg-gradient-to-r from-cyan-600 to-purple-600 hover:from-cyan-500 hover:to-purple-500 text-white font-bold text-xs shadow-md transition flex items-center space-x-1"><i class="fa-solid fa-bolt"></i><span>换为此号</span></button>`
                          }
                        </div>
                      </div>
                    `;
                  }).join('')}
                </div>
              </div>
            </div>
          </div>
        `;
      }).join('');
    }

    async function switchGroupAccount(groupId, accountId) {
      try {
        const resp = await fetch("/api/groups/switch", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ group: groupId, account: accountId })
        });
        const res = await resp.json();
        if (res.code === 200) {
          showToast(res.msg);
          loadDashboard();
        } else {
          showToast(res.msg || "换号失败", true);
        }
      } catch (e) {
        showToast("请求失败", true);
      }
    }

    async function toggleAutoFailover() {
      const chk = document.getElementById("chkAutoFailover").checked;
      try {
        const resp = await fetch("/api/groups/auto_failover", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ enabled: chk })
        });
        const res = await resp.json();
        showToast(res.msg);
      } catch (e) {
        showToast("设置失败", true);
      }
    }

    async function syncCockpitAccounts() {
      showToast("正在扫描并同步 Cockpit 账号池...");
      try {
        const resp = await fetch("/api/accounts/sync", { method: "POST" });
        const res = await resp.json();
        showToast(res.msg);
        loadDashboard();
      } catch (e) {
        showToast("同步失败", true);
      }
    }

    window.addEventListener("DOMContentLoaded", loadDashboard);
    setInterval(loadDashboard, 15000);
  </script>
</body>
</html>
"""
