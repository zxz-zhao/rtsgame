using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 游戏内 WebSocket 同步中心（联机模式核心）
// 指令协议：{ "action":"move|attack|stop|place|produce|spawn|damage|death", ... }
public class GameNetworkSync : MonoBehaviour
{
    public static GameNetworkSync Instance { get; private set; }

    public bool IsNetworkGame  { get; private set; }
    public bool IsHost         { get; private set; }
    public bool PeerConnected  { get; private set; }
    public int  GameSeed       { get; private set; }

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private bool _wsOpen;

    private readonly Queue<string> _incoming = new Queue<string>();
    private readonly Queue<string> _outgoing  = new Queue<string>();
    private readonly object _inLock  = new object();
    private readonly object _outLock = new object();
    private readonly System.Threading.SemaphoreSlim _sendSem = new System.Threading.SemaphoreSlim(1, 1);

    private float _pingTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 外部调用：开始联机游戏，连接 WS ──────────────────────
    public void StartNetworkGame(string roomId)
    {
        IsNetworkGame = true;
        var net = NetworkClient.Instance;
        string token    = net?.Token    ?? "test-token-local";
        string serverIp = net?.ServerUrl?.Replace("http://", "").Split(':')[0] ?? "127.0.0.1";
        string wsUrl    = $"ws://{serverIp}:8081";
        Debug.Log($"[NetGame] 连接 {wsUrl}  roomId={roomId}");
        ConnectAsync(wsUrl, token, roomId);
    }

    async void ConnectAsync(string url, string token, string roomId)
    {
        _cts = new CancellationTokenSource();
        _ws  = new ClientWebSocket();
        try
        {
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            _wsOpen = true;
            Debug.Log("[NetGame] WS 已连接");
            // 发送加入消息
            EnqueueSend($"{{\"type\":\"join\",\"token\":\"{token}\",\"roomId\":\"{roomId}\"}}");
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetGame] WS 连接失败: {e.Message}，退回单机模式");
            IsNetworkGame = false;
        }
    }

    async Task ReceiveLoop()
    {
        var buf = new byte[8192];
        while (_wsOpen && _ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                string msg = Encoding.UTF8.GetString(buf, 0, result.Count);
                lock (_inLock) _incoming.Enqueue(msg);
            }
            catch { break; }
        }
        _wsOpen = false;
    }

    void Update()
    {
        // 处理收到的消息（主线程）
        lock (_inLock)
            while (_incoming.Count > 0) ProcessIncoming(_incoming.Dequeue());

        // 发送排队消息
        if (_wsOpen && _ws?.State == WebSocketState.Open)
        {
            lock (_outLock)
                while (_outgoing.Count > 0) SendRaw(_outgoing.Dequeue());
        }

        // 心跳
        if (_wsOpen && IsNetworkGame)
        {
            _pingTimer -= Time.deltaTime;
            if (_pingTimer <= 0f) { _pingTimer = 15f; EnqueueSend("{\"type\":\"ping\"}"); }
        }

        // 双端各自每 3 秒广播己方单位位置，对端用来校正 remote 单位漂移
        if (IsNetworkGame && _wsOpen)
        {
            _posSyncTimer -= Time.deltaTime;
            if (_posSyncTimer <= 0f)
            {
                _posSyncTimer = 3f;
                SendAllPositions();
            }
        }
    }

    private float _posSyncTimer = 3f;

    void SendAllPositions()
    {
        var units = GameManager.Instance?.GetAllUnits();
        if (units == null) return;
        var sb = new System.Text.StringBuilder("{\"action\":\"posync\",\"units\":[");
        bool first = true;
        foreach (var u in units)
        {
            // 只发送己方（bPlayerOwned=true）单位，对端用来校正其 remote 副本
            if (u == null || u.IsDead() || u.NetId == 0 || !u.bPlayerOwned) continue;
            var p = u.transform.position;
            if (!first) sb.Append(',');
            sb.Append($"{{\"id\":{u.NetId},\"x\":{p.x:F1},\"z\":{p.z:F1}}}");
            first = false;
        }
        sb.Append("]}");
        if (!first) SendCmd(sb.ToString()); // 只在有己方单位时发送
    }

    // ── 处理服务器推送 ────────────────────────────────────────
    void ProcessIncoming(string json)
    {
        string type = ExtractStr(json, "type");
        switch (type)
        {
            case "role":
                IsHost   = ExtractStr(json, "role") == "host";
                GameSeed = int.TryParse(ExtractStr(json, "seed"), out int s) ? s : 12345;
                Debug.Log($"[NetGame] 角色={( IsHost ? "HOST(蓝方)" : "GUEST(红方)" )}  Seed={GameSeed}");
                // 通知 GameManager 初始化联机对局
                GameManager.Instance?.OnNetworkRoleAssigned(IsHost, GameSeed);
                break;

            case "peer_joined":
                PeerConnected = true;
                Debug.Log("[NetGame] 对手已加入！");
                RTSHUD.Instance?.ShowAlert("对手已加入，游戏开始！");
                break;

            case "cmd":
                ApplyRemoteCommand(ExtractObj(json, "data"));
                break;

            case "peer_left":
                Debug.LogWarning("[NetGame] 对手断线！");
                RTSHUD.Instance?.ShowAlert("⚠ 对手断线，胜利！");
                GameManager.Instance?.OnPeerDisconnected();
                break;

            case "pong":
                break;

            case "error":
                Debug.LogError("[NetGame] 服务器错误: " + ExtractStr(json, "msg"));
                IsNetworkGame = false;
                break;
        }
    }

    // ── 发送游戏指令给对手 ────────────────────────────────────
    public void SendCmd(string dataJson)
    {
        if (!IsNetworkGame || !_wsOpen) return;
        EnqueueSend($"{{\"type\":\"cmd\",\"data\":{dataJson}}}");
    }

    // ── 指令构建辅助 ──────────────────────────────────────────
    public void SendMove(int netId, Vector3 dest)
        => SendCmd($"{{\"action\":\"move\",\"id\":{netId},\"x\":{dest.x:F2},\"z\":{dest.z:F2}}}");

    public void SendAttack(int attackerId, int targetId, bool isBldg)
        => SendCmd($"{{\"action\":\"attack\",\"id\":{attackerId},\"tid\":{targetId},\"bldg\":{(isBldg?1:0)}}}");

    public void SendStop(int netId)
        => SendCmd($"{{\"action\":\"stop\",\"id\":{netId}}}");

    public void SendAttackMove(int netId, Vector3 dest)
        => SendCmd($"{{\"action\":\"amove\",\"id\":{netId},\"x\":{dest.x:F2},\"z\":{dest.z:F2}}}");

    public void SendPatrol(int netId, Vector3 a, Vector3 b)
        => SendCmd($"{{\"action\":\"patrol\",\"id\":{netId},\"ax\":{a.x:F2},\"az\":{a.z:F2},\"bx\":{b.x:F2},\"bz\":{b.z:F2}}}");

    public void SendGuard(int netId, int allyNetId)
        => SendCmd($"{{\"action\":\"guard\",\"id\":{netId},\"tid\":{allyNetId}}}");

    public void SendPlace(string btype, Vector3 pos, int netId = 0)
        => SendCmd($"{{\"action\":\"place\",\"btype\":\"{btype}\",\"x\":{pos.x:F2},\"z\":{pos.z:F2},\"nid\":{netId}}}");

    public void SendProduce(int bldgNetId, int unitIdx)
        => SendCmd($"{{\"action\":\"produce\",\"bid\":{bldgNetId},\"uidx\":{unitIdx}}}");

    public void SendSpawn(int netId, string utype, Vector3 pos, bool isPlayerOwned)
        => SendCmd($"{{\"action\":\"spawn\",\"id\":{netId},\"utype\":\"{utype}\",\"x\":{pos.x:F2},\"z\":{pos.z:F2},\"own\":{(isPlayerOwned?1:0)}}}");

    public void SendSkill(int casterId, int targetId, int stype)
        => SendCmd($"{{\"action\":\"skill\",\"id\":{casterId},\"tid\":{targetId},\"stype\":{stype}}}");

    // ── 接收远程指令并应用 ────────────────────────────────────
    void ApplyRemoteCommand(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        string action = ExtractStr(data, "action");
        switch (action)
        {
            case "move":
            {
                int id = ParseInt(data, "id");
                float x = ParseFloat(data, "x"), z = ParseFloat(data, "z");
                var u = NetIdTracker.FindUnit(id);
                if (u != null) u.ApplyMoveCommand(new Vector3(x, 0, z));
                break;
            }
            case "attack":
            {
                int id  = ParseInt(data, "id");
                int tid = ParseInt(data, "tid");
                bool bldg = ParseInt(data, "bldg") == 1;
                var attacker = NetIdTracker.FindUnit(id);
                if (attacker == null) break;
                if (bldg) { var b = NetIdTracker.FindBuilding(tid); if (b != null) attacker.ApplyAttackBuildingCommand(b); }
                else       { var t = NetIdTracker.FindUnit(tid);    if (t != null) attacker.ApplyAttackCommand(t); }
                break;
            }
            case "stop":
            {
                int id = ParseInt(data, "id");
                NetIdTracker.FindUnit(id)?.ApplyStopCommand();
                break;
            }
            case "amove":
            {
                int id = ParseInt(data, "id");
                float x = ParseFloat(data, "x"), z = ParseFloat(data, "z");
                NetIdTracker.FindUnit(id)?.ApplyAttackMoveCommand(new Vector3(x, 0, z));
                break;
            }
            case "patrol":
            {
                int id = ParseInt(data, "id");
                float ax = ParseFloat(data, "ax"), az = ParseFloat(data, "az");
                float bx = ParseFloat(data, "bx"), bz = ParseFloat(data, "bz");
                NetIdTracker.FindUnit(id)?.ApplyPatrolCommand(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));
                break;
            }
            case "guard":
            {
                int id = ParseInt(data, "id");
                int tid = ParseInt(data, "tid");
                var u = NetIdTracker.FindUnit(id);
                var ally = NetIdTracker.FindUnit(tid);
                if (u != null && ally != null) u.ApplyGuardCommand(ally);
                break;
            }
            case "place":
            {
                string btype = ExtractStr(data, "btype");
                float x = ParseFloat(data, "x"), z = ParseFloat(data, "z");
                int nid = ParseInt(data, "nid");
                RemotePlaceBuilding(btype, new Vector3(x, 0, z), nid);
                break;
            }
            case "produce":
            {
                int bid  = ParseInt(data, "bid");
                int uidx = ParseInt(data, "uidx");
                NetIdTracker.FindBuilding(bid)?.EnqueueUnit(uidx);
                break;
            }
            case "hp":
            {
                int id   = ParseInt(data, "id");
                int hp   = ParseInt(data, "hp");
                bool bld = ParseInt(data, "bldg") == 1;
                if (bld) NetIdTracker.FindBuilding(id)?.ForceSetHP(hp);
                else     NetIdTracker.FindUnit(id)?.ForceSetHP(hp);
                break;
            }
            case "death":
            {
                int id   = ParseInt(data, "id");
                bool bld = ParseInt(data, "bldg") == 1;
                if (bld)
                {
                    var b = NetIdTracker.FindBuilding(id);
                    if (b != null) { b.NetSyncIncoming = true; b.ForceSetHP(0); b.NetSyncIncoming = false; }
                }
                else
                {
                    var u = NetIdTracker.FindUnit(id);
                    if (u != null) { u.NetSyncIncoming = true; u.ForceSetHP(0); u.NetSyncIncoming = false; }
                }
                break;
            }
            case "posync":
            {
                // 批量位置校正：找到 units 数组并逐项解析
                int arrStart = data.IndexOf('[');
                int arrEnd   = data.LastIndexOf(']');
                if (arrStart < 0 || arrEnd <= arrStart) break;
                string arr = data.Substring(arrStart + 1, arrEnd - arrStart - 1);
                // 按 '}' 分割每条记录
                int pos = 0;
                while (pos < arr.Length)
                {
                    int ob = arr.IndexOf('{', pos);
                    int cb = arr.IndexOf('}', ob < 0 ? 0 : ob);
                    if (ob < 0 || cb < 0) break;
                    string entry = arr.Substring(ob, cb - ob + 1);
                    int uid = ParseInt(entry, "id");
                    float ux = ParseFloat(entry, "x"), uz = ParseFloat(entry, "z");
                    var u2 = NetIdTracker.FindUnit(uid);
                    // 只校正 remote（对端）单位，己方单位不被覆盖
                    if (u2 != null && !u2.IsDead() && !u2.bPlayerOwned)
                    {
                        Vector3 target = new Vector3(ux, u2.transform.position.y, uz);
                        // 偏差超过 3m 才校正（避免抖动）
                        if (Vector3.Distance(u2.transform.position, target) > 3f)
                        {
                            var ag = u2.GetComponent<UnityEngine.AI.NavMeshAgent>();
                            if (ag != null && ag.enabled && ag.isOnNavMesh) ag.Warp(target);
                            else u2.transform.position = target;
                        }
                    }
                    pos = cb + 1;
                }
                break;
            }
            case "skill":
            {
                int sid   = ParseInt(data, "id");
                int stid  = ParseInt(data, "tid");
                int stype = ParseInt(data, "stype");
                if (stype == 0) // 坦克穿甲弹
                {
                    var tank = NetIdTracker.FindUnit(sid) as Tank;
                    var tgt  = NetIdTracker.FindUnit(stid);
                    if (tank != null && tgt != null)
                        tank.UseArmorPierce(tgt, ignoreCooldown: true); // Host 强制执行，忽略副本冷却
                }
                break;
            }
            case "spawn":
            {
                // 对方生产了单位，在本地创建镜像（归属与对方相反）
                int   netId   = ParseInt(data, "id");
                string utype  = ExtractStr(data, "utype") ?? "";
                float sx      = ParseFloat(data, "x"), sz = ParseFloat(data, "z");
                bool  ownedBySender = ParseInt(data, "own") == 1;
                RemoteSpawnUnit(netId, utype, new Vector3(sx, 0, sz), !ownedBySender);
                break;
            }
        }
    }

    void RemoteSpawnUnit(int netId, string prefabTypeName, Vector3 pos, bool locallyPlayerOwned)
    {
        // 从 prefabTypeName 推断 Prefab 路径（去掉 _P/_E 后缀再加对应方后缀）
        string baseName = prefabTypeName.Replace("_P","").Replace("_E","").Replace("(Clone)","").Trim();
        string suffix   = locallyPlayerOwned ? "_P" : "_E";
        var prefab = Resources.Load<GameObject>($"Prefabs/{baseName}{suffix}");
        if (prefab == null) prefab = Resources.Load<GameObject>($"Prefabs/{baseName}_P");
        if (prefab == null) { Debug.LogWarning($"[NetGame] spawn: 找不到Prefab {baseName}{suffix}"); return; }
        var go   = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
        var unit = go.GetComponent<RTSUnit>();
        if (unit == null) return;
        unit.bPlayerOwned = locallyPlayerOwned;
        GameManager.Instance?.RecordUnitProduced(locallyPlayerOwned);
        // 用发送方的 netId 注册，使双方可互相引用同一逻辑单位
        NetIdTracker.RegisterUnit(netId, unit);
        Debug.Log($"[NetGame] 远程单位出生: {baseName}{suffix} netId={netId} own={locallyPlayerOwned}");
    }

    void RemotePlaceBuilding(string btype, Vector3 pos, int netId)
    {
        string prefabPath = $"Prefabs/{btype}_E"; // 敌方建筑 Prefab
        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogWarning($"[NetGame] 找不到远程建筑Prefab: {prefabPath}"); return; }
        var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
        var b  = go.GetComponent<RTSBuilding>();
        if (b != null) b.bPlayerOwned = false;
        GameManager.Instance?.RecordBuildingConstructed(false);
        // 使用发送方的 netId 保持双端一致
        int useId = netId > 0 ? netId : NetIdTracker.NextRemoteId();
        NetIdTracker.RegisterBuilding(useId, b);
        Debug.Log($"[NetGame] 远程建筑放置: {btype}_E netId={useId}");
    }

    // ── 内部发送 ──────────────────────────────────────────────
    void EnqueueSend(string raw)
    {
        lock (_outLock) _outgoing.Enqueue(raw);
    }

    async void SendRaw(string raw)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        await _sendSem.WaitAsync(_cts?.Token ?? CancellationToken.None);
        try
        {
            if (_ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(raw);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
        }
        catch (Exception e) { Debug.LogWarning("[NetGame] 发送失败: " + e.Message); }
        finally { _sendSem.Release(); }
    }

    // ── 简易 JSON 提取 ────────────────────────────────────────
    static string ExtractStr(string j, string key)
    {
        int idx = j.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        if (idx < 0) return null;
        int colon = j.IndexOf(':', idx);
        if (colon < 0) return null;
        int vs = colon + 1;
        while (vs < j.Length && j[vs] == ' ') vs++;
        if (vs >= j.Length) return null;
        if (j[vs] == '"')
        {
            int end = j.IndexOf('"', vs + 1);
            return end > vs ? j.Substring(vs + 1, end - vs - 1) : null;
        }
        int ve = vs;
        while (ve < j.Length && j[ve] != ',' && j[ve] != '}') ve++;
        return j.Substring(vs, ve - vs).Trim();
    }

    static string ExtractObj(string j, string key)
    {
        int idx = j.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        if (idx < 0) return null;
        int colon = j.IndexOf(':', idx);
        if (colon < 0) return null;
        int start = j.IndexOf('{', colon);
        if (start < 0) return null;
        int depth = 0, i = start;
        for (; i < j.Length; i++)
        {
            if (j[i] == '{') depth++;
            else if (j[i] == '}') { depth--; if (depth == 0) return j.Substring(start, i - start + 1); }
        }
        return null;
    }

    static int   ParseInt  (string j, string k) => int.TryParse(ExtractStr(j, k), out int r) ? r : 0;
    static float ParseFloat(string j, string k) => float.TryParse(ExtractStr(j, k), System.Globalization.NumberStyles.Float,
                                                       System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : 0f;

    // 游戏结束/返回大厅时主动断开，供 GameManager 调用
    public void Disconnect()
    {
        IsNetworkGame = false;
        PeerConnected = false;
        _wsOpen = false;
        _cts?.Cancel();
        _ws?.Dispose();
        _ws  = null;
        _cts = null;
        Debug.Log("[NetGame] WS 已主动断开");
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }
}
