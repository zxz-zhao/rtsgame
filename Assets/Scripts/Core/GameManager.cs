using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

// 游戏全局管理器，单例模式
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("游戏设置")]
    public int InitialGold = 800;

    // 单位和建筑列表
    private List<RTSUnit> allUnits = new List<RTSUnit>();
    private List<RTSBuilding> allBuildings = new List<RTSBuilding>();

    [Header("计时")]
    public float GameTime = 0f;

    // 游戏状态
    public bool bGameOver = false;
    public bool bPlayerWon = false;
    public int  PlayerKills = 0;
    public int  PlayerDamageDealt = 0;
    public int  PlayerDamageTaken = 0;
    public int  PlayerGoldIncome = 0;
    public int  PlayerGoldSpent = 0;
    public int  PlayerGoldRefunded = 0;
    public int  PlayerUnitKills = 0;
    public int  PlayerBuildingKills = 0;
    public int  PlayerUnitsLost = 0;
    public int  PlayerBuildingsLost = 0;
    public int  PlayerUnitsProduced = 0;
    public int  PlayerBuildingsConstructed = 0;
    public int  EnemyUnitsProduced = 0;
    public int  EnemyBuildingsConstructed = 0;
    public int  PlayerPeakPopUsed = 0;
    public int  PlayerPeakGold = 0;

    [Header("联机")]
    public bool IsNetworkGame = false;
    public bool IsHost        = false;

    [Header("警报")]
    public float BaseAlertCooldown = 10f;   // 基地被攻警报冷却
    private float baseAlertTimer = 0f;

    [Header("胜负判定")]
    public float WinLoseWarmupSeconds = 2f; // 进场缓冲，防止基地还在注册时被误判失败
    private bool basesEverReady = false;

    private float gameOverDelay = 0f;       // 结算面板延迟展示
    private RTSPlayerState cachedPlayerState;
    private RTSBuilding cachedPlayerBase;
    private int lastPlayerBaseLevel = 1;
    private int lastPlayerBaseHp = 0;
    private int lastPlayerBaseMaxHp = 0;
    /// <summary>玩家主基地引用（可能为 null，建筑被摧毁后会刷新）。</summary>
    public RTSBuilding PlayerMainBase => cachedPlayerBase;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        cachedPlayerState = RTSPlayerState.Instance;
        if (cachedPlayerState != null) cachedPlayerState.Gold = InitialGold;
        UpdatePeakBattleStats();
        NetIdTracker.Reset();
    }

    // 服务器分配角色后初始化联机环境
    public void OnNetworkRoleAssigned(bool isHost, int seed)
    {
        IsNetworkGame = true;
        IsHost        = isHost;
        UnityEngine.Random.InitState(seed);
        // 关闭 AI，改由远程玩家控制红方
        var ai = FindObjectOfType<AIController>();
        if (ai != null) ai.enabled = false;

        if (!isHost)
        {
            // Guest 单位 ID 从 5000+ 开始，避免与 Host(1-4999) 碰撞
            NetIdTracker.SetGuestMode();
            // 重置资源统计，避免 Start() 已统计蓝方建筑与 ReinitAfterOwnershipFlip 双重叠加
            var ps = RTSPlayerState.Instance;
            if (ps != null) { ps.PopCap = 20; ps.PowerCap = 0; ps.PowerUsed = 0; }
            // Guest = 红方：翻转所有建筑/单位归属，摄像机移至红方基地
            foreach (var b in allBuildings)
                if (b != null) b.bPlayerOwned = !b.bPlayerOwned;
            foreach (var u in allUnits)
                if (u != null) u.bPlayerOwned = !u.bPlayerOwned;
            // 补注翻转后建筑的 Owner/电力/人口（从重置后的基础值开始）
            foreach (var b in allBuildings)
                if (b != null && b.bPlayerOwned) b.ReinitAfterOwnershipFlip();
            // 摄像机移至红方基地旁
            var cam = FindObjectOfType<RTSCamera>();
            if (cam != null)
            {
                float camY = 52f;
                cam.transform.position = new Vector3(120f, camY, 74f);
                cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                var unityCamera = cam.GetComponent<Camera>();
                if (unityCamera != null && !unityCamera.orthographic)
                    unityCamera.fieldOfView = 45f;
            }
        }
        Debug.Log($"[NetGame] 联机对局就绪，角色={(isHost?"HOST蓝方":"GUEST红方")}");
    }

    public void OnPeerDisconnected()
    {
        if (bGameOver) return;
        bGameOver = true; bPlayerWon = true;
        OnGameOver(true);
    }

    // 建筑注册后缓存玩家基地引用
    void LateUpdate()
    {
        if (bGameOver) return;
        if (cachedPlayerBase == null || cachedPlayerBase.GetHP() <= 0)
        {
            cachedPlayerBase = null;
            foreach (var b in allBuildings)
                if (b != null && b.bIsMainBase && b.bPlayerOwned) { cachedPlayerBase = b; break; }
        }
        CachePlayerBaseSnapshot(cachedPlayerBase);
    }

    void CachePlayerBaseSnapshot(RTSBuilding baseBuilding)
    {
        if (baseBuilding == null || baseBuilding.GetMaxHP() <= 0) return;
        lastPlayerBaseLevel = Mathf.Max(1, baseBuilding.BuildingLevel);
        lastPlayerBaseHp = Mathf.Max(0, baseBuilding.GetHP());
        lastPlayerBaseMaxHp = baseBuilding.GetMaxHP();
    }

    void Update()
    {
        if (!bGameOver)
        {
            GameTime += Time.deltaTime;
            UpdatePeakBattleStats();
        }
        UpdateCombo();

        // 结算面板延迟（让玩家先看到战场结汀）
        if (bGameOver)
        {
            gameOverDelay -= Time.deltaTime;
            if (gameOverDelay <= 0f && RTSHUD.Instance != null &&
                !RTSHUD.Instance.IsGameOverVisible())
            {
                RTSHUD.Instance.ShowGameOver(bPlayerWon, PlayerKills, (int)GameTime);
            }
            return;
        }

        CheckWinLoseCondition();
        CheckBaseAlert();
    }

    void UpdatePeakBattleStats()
    {
        var ps = cachedPlayerState != null ? cachedPlayerState : RTSPlayerState.Instance;
        if (ps == null) return;
        PlayerPeakPopUsed = Mathf.Max(PlayerPeakPopUsed, ps.PopUsed);
        PlayerPeakGold = Mathf.Max(PlayerPeakGold, ps.Gold);
    }

    void CheckWinLoseCondition()
    {
        bool playerHasBase = false, enemyHasBase = false;
        foreach (var b in allBuildings)
        {
            if (b == null) continue;
            if (b.bIsMainBase && b.bPlayerOwned) playerHasBase = true;
            if (b.bIsMainBase && !b.bPlayerOwned) enemyHasBase = true;
        }

        // 战场生成和联机阵营翻转都有一小段异步窗口；必须等双方主基地都出现过，
        // 才开始执行胜负判定，避免“刚进入地图就失败/胜利”的假结算。
        if (playerHasBase && enemyHasBase)
            basesEverReady = true;
        if (!basesEverReady || GameTime < WinLoseWarmupSeconds)
            return;

        if (!enemyHasBase && allBuildings.Count > 0)
        {
            bGameOver = true; bPlayerWon = true;
            OnGameOver(true);
        }
        else if (!playerHasBase && allBuildings.Count > 0)
        {
            bGameOver = true; bPlayerWon = false;
            OnGameOver(false);
        }
    }

    void OnGameOver(bool win)
    {
        Time.timeScale = 1f;
        gameOverDelay = 1.5f;  // 1.5秒后展示结算面板
        ReportResult(win);
    }

    // 基地被攻警报（当敌方单位进入基地附近35个单位内）
    void CheckBaseAlert()
    {
        baseAlertTimer -= Time.deltaTime;
        if (baseAlertTimer > 0f) return;

        if (cachedPlayerBase == null) return;

        bool threatened = false;
        foreach (var u in allUnits)
        {
            if (u == null || u.bPlayerOwned) continue;
            if (Vector3.Distance(u.transform.position, cachedPlayerBase.transform.position) < 35f)
            {
                threatened = true;
                baseAlertTimer = BaseAlertCooldown;
                RTSHUD.Instance?.ShowAlert("警告：基地遭受攻击！");
                break;
            }
        }
        if (!threatened) baseAlertTimer = 2f;  // 无威胁时 2s 后再检查，避免逐帧扫描
    }

    // 击杀连击 combo
    private int   _comboCount = 0;
    private float _comboTimer = 0f;
    private const float ComboWindow = 3f;  // 3 秒内连续击杀计入 combo

    // 敌方单位/建筑死亡时增加击杀计数
    public void AddPlayerKill(string victimName = null)
    {
        PlayerKills++;
        if (cachedPlayerState != null) cachedPlayerState.EnemyKillCount = PlayerKills;

        // 连击逻辑
        if (_comboTimer > 0f) _comboCount++;
        else _comboCount = 1;
        _comboTimer = ComboWindow;
        // ≥3 连击通知 HUD 弹出 combo 文字
        if (_comboCount >= 3 && RTSHUD.Instance != null)
            RTSHUD.Instance.ShowCombo(_comboCount);
        // 击杀 feed：屏幕右侧弹出"击杀 步兵"小卡片
        if (RTSHUD.Instance != null && !string.IsNullOrEmpty(victimName))
            RTSHUD.Instance.ShowKillFeed(victimName);
    }

    public void AddPlayerBuildingKill(string victimName = null)
    {
        PlayerBuildingKills++;
        AddPlayerKill(victimName);
    }

    public void RecordUnitProduced(bool playerOwned)
    {
        if (playerOwned) PlayerUnitsProduced++;
        else EnemyUnitsProduced++;
    }

    public void RecordBuildingConstructed(bool playerOwned)
    {
        if (playerOwned) PlayerBuildingsConstructed++;
        else EnemyBuildingsConstructed++;
    }

    public void RecordDamage(bool attackerPlayerOwned, bool targetPlayerOwned, int amount)
    {
        if (amount <= 0) return;
        if (attackerPlayerOwned) PlayerDamageDealt += amount;
        if (targetPlayerOwned) PlayerDamageTaken += amount;
    }

    public void RecordGoldIncome(bool playerOwned, int amount)
    {
        if (playerOwned && amount > 0) PlayerGoldIncome += amount;
    }

    public void RecordGoldSpent(bool playerOwned, int amount)
    {
        if (playerOwned && amount > 0) PlayerGoldSpent += amount;
    }

    public void RecordGoldRefund(bool playerOwned, int amount)
    {
        if (playerOwned && amount > 0) PlayerGoldRefunded += amount;
    }

    public string BuildBattleReport(int kills, int seconds)
    {
        var ps = RTSPlayerState.Instance;
        int gold = ps != null ? ps.Gold : 0;
        int popUsed = ps != null ? ps.PopUsed : 0;
        int popCap = ps != null ? ps.PopCap : 0;
        int powerUsed = ps != null ? ps.PowerUsed : 0;
        int powerCap = ps != null ? ps.PowerCap : 0;
        int m = seconds / 60, s = seconds % 60;

        if (cachedPlayerBase != null)
            CachePlayerBaseSnapshot(cachedPlayerBase);

        int alliedUnits = 0, enemyUnits = 0, alliedBuildings = 0, enemyBuildings = 0;
        foreach (var u in allUnits)
        {
            if (u == null || u.IsDead()) continue;
            if (u.bPlayerOwned) alliedUnits++; else enemyUnits++;
        }
        foreach (var b in allBuildings)
        {
            if (b == null || b.GetHP() <= 0) continue;
            if (b.bPlayerOwned) alliedBuildings++; else enemyBuildings++;
        }

        string teamLine = IsNetworkGame
            ? $"队伍：{(IsHost ? "HOST" : "GUEST")} / 盟友在线"
            : "队伍：我方部队 / AI 敌军";
        return $"{teamLine}\n" +
               $"战果：摧毁 {kills}（单位 {PlayerUnitKills}/建筑 {PlayerBuildingKills}）  用时 {m:00}:{s:00}\n" +
               $"兵力：我方 {alliedUnits}兵/{alliedBuildings}建  敌方 {enemyUnits}兵/{enemyBuildings}建  损失 {PlayerUnitsLost}兵/{PlayerBuildingsLost}建\n" +
               $"生产：我方 出兵 {PlayerUnitsProduced}  建造 {PlayerBuildingsConstructed}    敌方 出兵 {EnemyUnitsProduced}  建造 {EnemyBuildingsConstructed}\n" +
               $"人口：{popUsed}/{popCap}  峰值 {PlayerPeakPopUsed}\n" +
               $"伤害：输出 {PlayerDamageDealt:N0}  承伤 {PlayerDamageTaken:N0}\n" +
               $"经济：金币 {gold:N0}  峰值 {PlayerPeakGold:N0}  收入 {PlayerGoldIncome:N0}  消耗 {PlayerGoldSpent:N0}  返还 {PlayerGoldRefunded:N0}\n" +
               $"电力：{powerUsed}/{powerCap}    主基地：{GetPlayerBaseSummaryText()}";
    }

    public string GetPlayerBaseSummaryText()
    {
        int baseLevel = lastPlayerBaseLevel;
        int baseHp = lastPlayerBaseHp;
        int baseMax = lastPlayerBaseMaxHp;
        if (cachedPlayerBase != null)
        {
            CachePlayerBaseSnapshot(cachedPlayerBase);
            baseLevel = Mathf.Max(1, cachedPlayerBase.BuildingLevel);
            baseHp = cachedPlayerBase.GetHP();
            baseMax = cachedPlayerBase.GetMaxHP();
        }

        return cachedPlayerBase != null && baseHp > 0
            ? $"Lv.{baseLevel} {baseHp}/{baseMax}"
            : $"Lv.{baseLevel} 已摧毁";
    }

    void UpdateCombo()
    {
        if (_comboTimer > 0f)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) _comboCount = 0;
        }
    }

    void ReportResult(bool win)
    {
        if (NetworkClient.Instance == null) return;
        NetworkClient.Instance.ReportMatchResult(win, PlayerKills, (int)GameTime);
    }

    // 注册/注销
    public void RegisterUnit(RTSUnit u) { if (!allUnits.Contains(u)) allUnits.Add(u); }
    public void UnregisterUnit(RTSUnit u)
    {
        if (!allUnits.Contains(u)) return;
        allUnits.Remove(u);
        // 敌方单位死亡计入击杀
        if (!u.bPlayerOwned)
        {
            PlayerUnitKills++;
            AddPlayerKill(u.DisplayName);
        }
        else
        {
            PlayerUnitsLost++;
        }
    }
    public void RegisterBuilding(RTSBuilding b)
    {
        if (!allBuildings.Contains(b)) allBuildings.Add(b);
        if (b != null && b.bIsMainBase && b.bPlayerOwned)
        {
            cachedPlayerBase = b;
            CachePlayerBaseSnapshot(b);
        }
    }

    public void UnregisterBuilding(RTSBuilding b)
    {
        if (b != null && b.bIsMainBase && b.bPlayerOwned)
            CachePlayerBaseSnapshot(b);
        if (b != null && b.bPlayerOwned)
            PlayerBuildingsLost++;
        allBuildings.Remove(b);
    }

    public List<RTSUnit> GetAllUnits() => allUnits;
    public List<RTSBuilding> GetAllBuildings() => allBuildings;

    // 重新开始
    public void RestartGame()
    {
        Time.timeScale = 1f;
        GameNetworkSync.Instance?.Disconnect();
        PlayerPrefs.SetInt("battle_resume_available", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToLobby()
    {
        Time.timeScale = 1f;
        GameNetworkSync.Instance?.Disconnect();
        PlayerPrefs.SetInt("battle_resume_available", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene("LobbyScene");
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        GameNetworkSync.Instance?.Disconnect();
        PlayerPrefs.SetInt("battle_resume_available", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("LoginScene");
    }
}
