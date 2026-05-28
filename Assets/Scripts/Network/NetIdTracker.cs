using System.Collections.Generic;
using UnityEngine;

// 网络 ID 注册中心
// Host(本地/蓝方) 单位 ID：1~4999
// Guest(远端/红方) 单位 ID：5000+
public static class NetIdTracker
{
    private static int _localCounter  = 1;
    private static int _remoteCounter = 5000;
    private static bool _guestMode    = false;

    private static readonly Dictionary<int, RTSUnit>     _units     = new Dictionary<int, RTSUnit>();
    private static readonly Dictionary<int, RTSBuilding> _buildings = new Dictionary<int, RTSBuilding>();

    public static void Reset()
    {
        _localCounter  = 1;
        _remoteCounter = 5000;
        _guestMode     = false;
        _units.Clear();
        _buildings.Clear();
    }

    // Guest 模式：本方单位 ID 于 5000+（避免与 Host 1-4999 的 ID 碰撞）
    public static bool IsGuestMode => _guestMode;

    public static void SetGuestMode()
    {
        _guestMode    = true;
        _localCounter = 5000;
    }

    // 本方单位分配 ID（Host: 1-4999，Guest: 5000+）
    public static int NextLocalId()  => _localCounter++;
    public static int NextRemoteId() => _remoteCounter++;

    public static void RegisterUnit(int netId, RTSUnit u)
    {
        if (u != null) { u.NetId = netId; _units[netId] = u; }
    }

    public static void RegisterBuilding(int netId, RTSBuilding b)
    {
        if (b != null) { b.NetId = netId; _buildings[netId] = b; }
    }

    public static void Unregister(int netId)
    {
        _units.Remove(netId);
        _buildings.Remove(netId);
    }

    public static RTSUnit     FindUnit(int netId)     => _units.TryGetValue(netId, out var u) ? u : null;
    public static RTSBuilding FindBuilding(int netId) => _buildings.TryGetValue(netId, out var b) ? b : null;
}
