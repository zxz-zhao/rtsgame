# 方案 A 报告：读代码找破解方式 —— 结果与边界

> 回答"能不能通过看代码找出破解方式"：**骨架能读，判定点能精确定位，
> 但最内层比较是 VM 字节码；而且挂钩带逻辑会崩进程。**
> 这是本轮最实在的进展 —— 之前一直在盲打，现在有了完整地图。

---

## 一、能读的部分：授权逻辑骨架（全部原生 x86，可读）

### 1.1 关键函数一览

| RVA | 函数 | 状态 |
|---|---|---|
| `0x43890` | 取当前日期时间 → double(EAX:EDX) | ✅ 原生可读 |
| `0x438F0` | `LoadAuthFile(path)` 读 ca.sam | ✅ 原生可读 |
| `0x443D0` | `SaveAuthFile(path, data)` 写 ca.sam | ✅ 原生可读 |
| `0x45C10` | 年月日 → double | ✅ 原生可读 |
| **`0xDD9C`** | **授权校验主函数** | ✅ 原生可读（核心） |
| `0xEF6B` | 比较/格式化 | ❌ 虚拟化桩 `jmp 0xB5AFF1` |
| `0xD984` | 取条目数 | ⚠️ 待确认 |
| `0x14F0` | 条目比较 | ⚠️ 待确认 |

### 1.2 LoadAuthFile 完整反汇编（原生）

```asm
0x438F0  mov eax,[esp+8]              ; arg = 路径
0x438F5  push 0                       ; hTemplateFile
0x438F7  push 0x20                    ; dwFlagsAndAttributes
0x438F9  push 3                       ; OPEN_EXISTING
0x438FB  push 0                       ; lpSecurityAttributes
0x438FD  push 3                       ; FILE_SHARE_READ|WRITE
0x438FF  push 0x80000000              ; GENERIC_READ
0x43904  push eax
0x43906  call 0x5C3E72                ; CreateFileA
0x4390B  mov edi,eax
0x4390F  cmp edi,-1
0x43912  je 0x43959                   ; 失败 → 返回 0
0x4391D  call 0x2DE62B                ; 分配结构
0x43928  call 0x42680                 ; 分配 buffer
0x43948  call 0x55C9D4                ; ReadFile
0x4394D  ret
```

### 1.3 **核心：`0xDD9C` 授权校验主函数 —— 完整决策链**

```asm
; ---- 初始化 ----
0xDDF6   cmp  [0x581873], 0            ; 全局初始化标志 (RVA 0x231873)
0xDDFD   jne  0xDE17                   ; 已初始化则跳过
0xDE03   push 0x581877 ; call 0xE3B7   ; 初始化条目表对象 (RVA 0x231877)
0xDE0D   mov  [0x581873], 1            ; 置标志

; ---- 取数据 ----
0xDE17   mov  ebx, [0x581877]          ; 条目表对象
0xDE1D   call 0xD984                   ; 取条目数 → eax
0xDE22   mov  [ebp-4], eax             ; 保存条目数
0xDE2F   call 0x43890                  ; ★ 取当前日期时间
0xDE39   call 0xEF6B                   ; ★ 虚拟化：比较/格式化

; ---- 遍历条目 ----
0xDE56   cmp  ecx,eax / jg 0xDFAD      ; 循环
0xDE98   add  ebx, 8                   ; 条目+8 = 数据
0xDEA9   call 0x14F0                   ; 条目比较
0xDEB1   cmp  eax,0 / jne 0xDFA5       ; 不等 → 下一个
0xDF8E   mov  [ebp-0x18], -1           ; ← 循环走完 → 索引 = -1（未找到）
0xDF9D   mov  [ebp-0x18], eax          ; ← 找到 → 记录索引

; ---- 闸门 1 ----
0xDFB0   cmp  [ebp+0xc], 1 / jne 0xDFCB ; 参数必须 == 1
0xDFBA   cmp  [ebp-0x18], -1 / je 0xDFCB; 索引不能是 -1
0xDFC4   mov  eax,1
0xDFCB   xor  eax,eax
0xDFCF   test eax,eax / je 0xE338      ; ★★ 失败分支 #1

; ---- 闸门 2 ----
0xDFD5   cmp  [ebp+0x1c], 1 / jge 0xDFE6
0xDFDF   mov  [ebp+0x1c], 0x78         ; 默认 120
0xDFE6   cmp  [ebp-0x18], 0 / jle 0xE003
0xDFF3   cmp  [ebp-0x18], count / jg 0xE003
0xDFFC   mov  eax,1
0xE003   xor  eax,eax
0xE007   test eax,eax / je 0xE1AA      ; ★★ 失败分支 #2
0xE00D   ...                            ; 成功路径
```

**判定本质：`[ebp-0x18]` = 在授权条目表里找到的索引；`-1` = 没找到 → 失败。**
**两个失败出口：`0xE338`（闸门1）和 `0xE1AA`（闸门2）。**

### 1.4 关键数据

| RVA | 内容 |
|---|---|
| `0x23025C` | 默认认证文件路径（`0xE59B: mov eax,0x58025C`） |
| `0x23025D +4/+8` | 一个 `{len, data}` buffer 结构 |
| `0x231873` | 授权条目表初始化标志 |
| `0x231877` | 授权条目表对象指针 |

### 1.5 第二组校验（`0x2F7xx`）

同一模式：`0x2F813: mov ebx,0x268` + `0x2F818: call 0x438F0`（LoadAuthFile）
→ 说明**有两处独立调用 LoadAuthFile**（`0xE5AB` 和 `0x2F818`），是两套校验。

---

## 二、读不到的部分：1921 个虚拟化桩

```
.text 里跳进 .vmp0/.vmp1 的桩函数：1921 个
  .vmp0: 1914 个
  .vmp1:    7 个
```

`.text` 是个**空壳**——93.6% "解密"只是熵值低，实际有 1921 个函数体被替换成 5 字节 `jmp`：

```asm
0xEF6B  关键比较函数：
  push ebp ; mov ebp,esp ; sub esp,0x68
  jmp 0xB5AFF1        ← 跳进 .vmp1，函数体是 VM 字节码
  后面是垃圾字节
```

**所以：纯静态分析到此为止。** 骨架 + 判定点拿到了，最内层比较拿不到。

---

## 三、意外收获：Frida 能在 VMP 的 `.text` 上挂钩

这推翻了之前的判断。实测：

| 测试 | 结果 |
|---|---|
| 只读 `.text` 内存 | ✅ 可读（`0x43890 = 55 8b ec 83 ec 1c 8d`） |
| 挂钩 `kernel32!ReadFile` | ✅ 成功，进程存活 |
| 挂钩 `.text 0x438F0` | ✅ **成功**，进程存活 |
| 挂钩 `.text 0xDD9C` | ✅ **成功**，进程存活 |
| 挂钩 `.text 0xD984` | ✅ **成功**，进程存活 |
| `0xDD9C` + `0x438F0` 组合 | ✅ 成功，进程存活 |
| `CreateFileW` + `ReadFile` 组合 | ✅ 成功，进程存活 |

**Frida 找到了写入 `.text` 的路径**（我用 `NtProtectVirtualMemory` 做不到的它做到了）。

### 但有严格边界

| 钩子体内容 | 结果 |
|---|---|
| `onEnter: function(){}` （空） | ✅ 存活 |
| 钩子体里累加计数器 | ❌ `sc.load()` 崩（transport closed） |
| 钩子体里调 `send()` | ❌ 崩 |
| 钩子体里 `r.replace()` 改返回值 | ❌ 崩 |

**结论：挂钩本身能装，但一旦钩子体有实质逻辑，进程立刻死。**

原因推测：
1. 这些函数是热函数，启动期被高频调用
2. 同步回调进入 Frida 的 JS 运行时需要分配/加锁，而 VMP 正在保护自己的地址空间
3. 或者 VMP 的完整性校验发现代码被 patch → 惩罚

---

## 四、当前可用的绕过手段（已验证生效）

`.text` 挂钩这条路暂时堵住，但**之前验证过的 Win32 层钩子完全不受影响**：

| 手段 | 实测结果 |
|---|---|
| `--freeze`：钩 `WriteFile` 把 ca.sam 还原成快照 | ✅ 写入 11 次全部回退，SHA 零变化 |
| `--clock`：钩 `GetLocalTime` 返回 2024-01-01 | ✅ |
| `--bind`：钩 `DeviceIoControl` 固定盘型号 | ✅ `KINGSTON...` → `FIXEDMODEL0001` |

**而且静态分析给出了升级方向**：既然 `0x438F0 LoadAuthFile` 是原生函数、它调 `ReadFile`（在可写的 DLL 里），
那么可以在 `ReadFile` 返回时**把 ca.sam 内容替换成一份"索引永远 != -1"的构造数据**，
让 `0xDFCF` / `0xE007` 两个闸门全走成功路径。这比 `--freeze` 的"整体冻结"更精确。

---

## 五、下一步的三个选项

**① 攻挂钩稳定性（最直接）**
把钩子体从 JS 回调改成**纯数据记录 + 事后用独立线程读**：
- 钩子里只写 native 内存（`stats` 用 `Memory.alloc` 分配在进程内）
- 完全不进 JS 运行时、不分配、不加锁
- Python 侧定时 RPC 读那块内存

**② 换挂钩实现**
Frida 的 `Interceptor` 在 VMP 环境有副作用。改用：
- **硬件断点**（DR0-DR3）——不需要改页面，但需处理 VMP 的 DR 检测
- **VEH 断点**（只读页设 `PAGE_GUARD` / `PAGE_NOACCESS` + 异常处理器）——我已有 `str_xref.py` 这套
- **`Stalker`** ——但 32 位支持有限

**③ 走 API 层（保底，已可用）**
`--freeze` / `--clock` / `--bind` 三件套现在就能用；再按静态分析的结论，在 `ReadFile` 层注入构造数据，
把绕过从"冻结"升级成"结构级伪造"。

**我倾向 ①**——因为挂钩能装上是已验证的，只是钩子体不能碰 JS 运行时。
用 native 内存记录就绕开了这个限制，而且一旦通了，就能直接在 `0xDD9C` 上强制返回成功，
不再依赖 ca.sam 的具体格式。这是最短路径。
