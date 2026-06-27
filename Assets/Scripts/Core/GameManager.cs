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
    public int  EnemyDamageDealt = 0;
    public int  EnemyDamageTaken = 0;
    public int  PlayerGoldIncome = 0;
    public int  PlayerGoldSpent = 0;
    public int  PlayerGoldRefunded = 0;
    public int  EnemyGold = 0;
    public int  EnemyGoldIncome = 0;
    public int  EnemyGoldSpent = 0;
    public int  EnemyGoldRefunded = 0;
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
    public int  EnemyPeakPopUsed = 0;
    public int  EnemyPeakGold = 0;

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
    private RTSBuilding cachedEnemyBase;
    private AIController cachedAIController;
    private int lastPlayerBaseLevel = 1;
    private int lastPlayerBaseHp = 0;
    private int lastPlayerBaseMaxHp = 0;
    private int lastEnemyBaseLevel = 1;
    private int lastEnemyBaseHp = 0;
    private int lastEnemyBaseMaxHp = 0;
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
        if (cachedEnemyBase == null || cachedEnemyBase.GetHP() <= 0)
        {
            cachedEnemyBase = null;
            foreach (var b in allBuildings)
                if (b != null && b.bIsMainBase && !b.bPlayerOwned) { cachedEnemyBase = b; break; }
        }
        CachePlayerBaseSnapshot(cachedPlayerBase);
        CacheEnemyBaseSnapshot(cachedEnemyBase);
    }

    void CachePlayerBaseSnapshot(RTSBuilding baseBuilding)
    {
        if (baseBuilding == null || baseBuilding.GetMaxHP() <= 0) return;
        lastPlayerBaseLevel = Mathf.Max(1, baseBuilding.BuildingLevel);
        lastPlayerBaseHp = Mathf.Max(0, baseBuilding.GetHP());
        lastPlayerBaseMaxHp = baseBuilding.GetMaxHP();
    }

    void CacheEnemyBaseSnapshot(RTSBuilding baseBuilding)
    {
        if (baseBuilding == null || baseBuilding.GetMaxHP() <= 0) return;
        lastEnemyBaseLevel = Mathf.Max(1, baseBuilding.BuildingLevel);
        lastEnemyBaseHp = Mathf.Max(0, baseBuilding.GetHP());
        lastEnemyBaseMaxHp = baseBuilding.GetMaxHP();
    }

    void Update()
    {
        if (IsNetworkGame && !Mathf.Approximately(Time.timeScale, 1f))
            Time.timeScale = 1f;

        float frameDeltaTime = IsNetworkGame ? Time.unscaledDeltaTime : Time.deltaTime;
        if (!bGameOver)
        {
            GameTime += frameDeltaTime;
            UpdatePeakBattleStats();
        }
        UpdateCombo();

        // 结算面板延迟（让玩家先看到战场结汀）
        if (bGameOver)
        {
            gameOverDelay -= frameDeltaTime;
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
        if (ps != null)
        {
            PlayerPeakPopUsed = Mathf.Max(PlayerPeakPopUsed, ps.PopUsed);
            PlayerPeakGold = Mathf.Max(PlayerPeakGold, ps.Gold);
        }

        EnemyPeakPopUsed = Mathf.Max(EnemyPeakPopUsed, ComputeSidePopUsed(false));
        if (cachedAIController == null)
            cachedAIController = FindObjectOfType<AIController>();
        if (cachedAIController != null && cachedAIController.enabled && !IsNetworkGame)
            RecordEnemyGoldSnapshot(cachedAIController.CurrentGold);
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
        if (!attackerPlayerOwned) EnemyDamageDealt += amount;
        if (!targetPlayerOwned) EnemyDamageTaken += amount;
    }

    public void RecordGoldIncome(bool playerOwned, int amount)
    {
        if (amount <= 0) return;
        if (playerOwned) PlayerGoldIncome += amount;
        else EnemyGoldIncome += amount;
    }

    public void RecordGoldSpent(bool playerOwned, int amount)
    {
        if (amount <= 0) return;
        if (playerOwned) PlayerGoldSpent += amount;
        else EnemyGoldSpent += amount;
    }

    public void RecordGoldRefund(bool playerOwned, int amount)
    {
        if (amount <= 0) return;
        if (playerOwned) PlayerGoldRefunded += amount;
        else EnemyGoldRefunded += amount;
    }

    public void RecordEnemyGoldSnapshot(int currentGold)
    {
        EnemyGold = Mathf.Max(0, currentGold);
        EnemyPeakGold = Mathf.Max(EnemyPeakGold, EnemyGold);
    }

    public struct BattleReportRow
    {
        public string Label;
        public string PlayerValue;
        public string EnemyValue;

        public BattleReportRow(string label, string playerValue, string enemyValue)
        {
            Label = label;
            PlayerValue = playerValue;
            EnemyValue = enemyValue;
        }
    }

    public struct BattleReportData
    {
        public string TeamLine;
        public string PlayerHeader;
        public string EnemyHeader;
        public string DurationText;
        public BattleReportRow[] Rows;
    }

    public string BuildBattleReport(int kills, int seconds)
    {
        BattleReportData report = BuildBattleReportData(kills, seconds);
        var sb = new System.Text.StringBuilder();
        sb.Append(report.TeamLine).Append("    用时 ").Append(report.DurationText);
        if (report.Rows != null)
        {
            for (int i = 0; i < report.Rows.Length; i++)
            {
                BattleReportRow row = report.Rows[i];
                sb.Append('\n')
                    .Append(row.Label).Append("：")
                    .Append("我方 ").Append(row.PlayerValue)
                    .Append(" | 敌方 ").Append(row.EnemyValue);
            }
        }
        return sb.ToString();
    }

    public BattleReportData BuildBattleReportData(int kills, int seconds)
    {
        var ps = RTSPlayerState.Instance;
        int m = seconds / 60, s = seconds % 60;

        if (cachedPlayerBase != null)
            CachePlayerBaseSnapshot(cachedPlayerBase);
        if (cachedEnemyBase != null)
            CacheEnemyBaseSnapshot(cachedEnemyBase);
        if (cachedAIController == null)
            cachedAIController = FindObjectOfType<AIController>();
        if (cachedAIController != null && cachedAIController.enabled && !IsNetworkGame)
            RecordEnemyGoldSnapshot(cachedAIController.CurrentGold);

        var player = BuildSideSnapshot(true, ps);
        var enemy = BuildSideSnapshot(false, ps);

        string teamLine = IsNetworkGame
            ? $"{(IsHost ? "我方HOST" : "我方GUEST")}  VS  对手"
            : "我方部队  VS  AI敌军";
        return new BattleReportData
        {
            TeamLine = teamLine,
            PlayerHeader = IsNetworkGame ? (IsHost ? "我方 HOST" : "我方 GUEST") : "我方",
            EnemyHeader = IsNetworkGame ? "对手" : "敌方",
            DurationText = $"{m:00}:{s:00}",
            Rows = new BattleReportRow[]
            {
                new BattleReportRow("战果", $"摧毁{kills}({PlayerUnitKills}兵/{PlayerBuildingKills}建)", $"摧毁{PlayerUnitsLost + PlayerBuildingsLost}({PlayerUnitsLost}兵/{PlayerBuildingsLost}建)"),
                new BattleReportRow("兵力", $"{player.Units}兵/{player.Buildings}建", $"{enemy.Units}兵/{enemy.Buildings}建"),
                new BattleReportRow("生产", $"出兵{PlayerUnitsProduced} 建造{PlayerBuildingsConstructed}", $"出兵{EnemyUnitsProduced} 建造{EnemyBuildingsConstructed}"),
                new BattleReportRow("人口", $"{player.PopUsed}/{player.PopCap} 峰{PlayerPeakPopUsed}", $"{enemy.PopUsed}/{enemy.PopCap} 峰{EnemyPeakPopUsed}"),
                new BattleReportRow("伤害", $"出{PlayerDamageDealt:N0} 承{PlayerDamageTaken:N0}", $"出{EnemyDamageDealt:N0} 承{EnemyDamageTaken:N0}"),
                new BattleReportRow("经济", $"金{player.Gold:N0} 收{PlayerGoldIncome:N0} 耗{PlayerGoldSpent:N0}", $"金{enemy.Gold:N0} 收{EnemyGoldIncome:N0} 耗{EnemyGoldSpent:N0}"),
                new BattleReportRow("电力", $"{player.PowerUsed}/{player.PowerCap}", $"{enemy.PowerUsed}/{enemy.PowerCap}"),
                new BattleReportRow("主基", GetPlayerBaseSummaryText(), GetEnemyBaseSummaryText())
            }
        };
    }

    struct BattleSideSnapshot
    {
        public int Units;
        public int Buildings;
        public int PopUsed;
        public int PopCap;
        public int PowerUsed;
        public int PowerCap;
        public int Gold;
    }

    BattleSideSnapshot BuildSideSnapshot(bool playerOwned, RTSPlayerState ps)
    {
        var snap = new BattleSideSnapshot();
        snap.PopCap = 20;
        foreach (var u in allUnits)
        {
            if (u == null || u.IsDead() || u.bPlayerOwned != playerOwned) continue;
            snap.Units++;
            snap.PopUsed += Mathf.Max(1, u.PopCost);
        }
        foreach (var b in allBuildings)
        {
            if (b == null || b.GetHP() <= 0 || b.bUnderConstruction || b.bPlayerOwned != playerOwned) continue;
            snap.Buildings++;
            snap.PopCap += b.PopCapBonus;
            if (b.bIsPowerPlant) snap.PowerCap += b.PowerProvide;
            else if (b.PowerCost > 0) snap.PowerUsed += b.PowerCost;
        }

        if (playerOwned && ps != null)
        {
            snap.PopUsed = ps.PopUsed;
            snap.PopCap = ps.PopCap;
            snap.PowerUsed = ps.PowerUsed;
            snap.PowerCap = ps.PowerCap;
            snap.Gold = ps.Gold;
        }
        else if (!playerOwned)
        {
            snap.Gold = EnemyGold;
        }
        return snap;
    }

    int ComputeSidePopUsed(bool playerOwned)
    {
        int total = 0;
        foreach (var u in allUnits)
        {
            if (u == null || u.IsDead() || u.bPlayerOwned != playerOwned) continue;
            total += Mathf.Max(1, u.PopCost);
        }
        return total;
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

    public string GetEnemyBaseSummaryText()
    {
        int baseLevel = lastEnemyBaseLevel;
        int baseHp = lastEnemyBaseHp;
        int baseMax = lastEnemyBaseMaxHp;
        if (cachedEnemyBase != null)
        {
            CacheEnemyBaseSnapshot(cachedEnemyBase);
            baseLevel = Mathf.Max(1, cachedEnemyBase.BuildingLevel);
            baseHp = cachedEnemyBase.GetHP();
            baseMax = cachedEnemyBase.GetMaxHP();
        }

        return cachedEnemyBase != null && baseHp > 0
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
        if (b != null && b.bIsMainBase && !b.bPlayerOwned)
        {
            cachedEnemyBase = b;
            CacheEnemyBaseSnapshot(b);
        }
    }

    public void UnregisterBuilding(RTSBuilding b)
    {
        if (b != null && b.bIsMainBase && b.bPlayerOwned)
            CachePlayerBaseSnapshot(b);
        if (b != null && b.bIsMainBase && !b.bPlayerOwned)
            CacheEnemyBaseSnapshot(b);
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
