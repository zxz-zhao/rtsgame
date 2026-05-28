using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战场迷雾系统：友军视野半径外的敌方单位/建筑会被隐藏（仅渲染层面）。
/// 不影响地形显示，不影响 Collider 和游戏逻辑。
/// 性能：每 0.2s 评估一次，O(N_enemy * N_friendly_vision_sources)，水平距离平方比较，无 sqrt。
/// </summary>
public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance;

    [Header("开关")]
    public bool Enabled = true;
    [Range(0.05f, 1f)] public float UpdateInterval = 0.2f;

    [Header("建筑视野半径（米）")]
    public float DefaultBuildingVision = 16f;
    public float MainBaseVision = 28f;
    public float GoldMineVision = 12f;

    private float _timer;
    private static readonly List<FogHideable> _enemies = new List<FogHideable>();
    private readonly List<Vector3> _vsPos = new List<Vector3>();
    private readonly List<float> _vsR2 = new List<float>();

    public static FogOfWar EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("FogOfWar");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<FogOfWar>();
        return Instance;
    }

    void Awake() { if (Instance == null) Instance = this; }

    public static void RegisterEnemy(FogHideable h)
    {
        EnsureInstance();
        if (h != null && !_enemies.Contains(h)) _enemies.Add(h);
    }

    public static void UnregisterEnemy(FogHideable h)
    {
        _enemies.Remove(h);
    }

    void Update()
    {
        if (!Enabled) { ShowAll(); return; }
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = UpdateInterval;
        RebuildVisionSources();
        EvaluateEnemies();
    }

    void RebuildVisionSources()
    {
        _vsPos.Clear(); _vsR2.Clear();
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 己方单位视野
        var units = gm.GetAllUnits();
        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || u.IsDead() || !u.IsPlayerOwned()) continue;
                _vsPos.Add(u.transform.position);
                float r = Mathf.Max(8f, u.SightRange);
                _vsR2.Add(r * r);
            }
        }

        // 己方建筑视野
        var blds = gm.GetAllBuildings();
        if (blds != null)
        {
            for (int i = 0; i < blds.Count; i++)
            {
                var b = blds[i];
                if (b == null || b.GetHP() <= 0 || !b.bPlayerOwned) continue;
                float r = b.bIsMainBase ? MainBaseVision
                       : b.bIsGoldMine ? GoldMineVision
                       : DefaultBuildingVision;
                _vsPos.Add(b.transform.position);
                _vsR2.Add(r * r);
            }
        }
    }

    void EvaluateEnemies()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var h = _enemies[i];
            if (h == null) { _enemies.RemoveAt(i); continue; }
            bool visible = false;
            Vector3 hp = h.transform.position;
            for (int j = 0; j < _vsPos.Count; j++)
            {
                Vector3 d = _vsPos[j] - hp;
                // 仅水平距离（忽略飞行单位高度差）
                float dx = d.x, dz = d.z;
                if (dx * dx + dz * dz <= _vsR2[j]) { visible = true; break; }
            }
            h.SetVisible(visible);
        }
    }

    void ShowAll()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var h = _enemies[i];
            if (h == null) { _enemies.RemoveAt(i); continue; }
            h.SetVisible(true);
        }
    }
}
