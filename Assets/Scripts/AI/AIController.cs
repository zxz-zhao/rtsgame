using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

// AI控制器：多路进攻、动态难度、游斗时间加速升级
public class AIController : MonoBehaviour
{
    [Header("AI 基础设置")]
    public int   InitialGold        = 800;
    public float AttackWaveInterval  = 40f;  // attack interval
    public float InitialAttackDelay  = 180f; // first attack delay
    public int   WaveBaseSize        = 5;    // 基础波次规模
    public int   MaxAliveAIUnits     = 35;   // 场上最多存活 AI 单位数（防卡帧）
    public float GoldInterval        = 5f;
    public int   GoldPerInterval     = 55;

    [Header("AI 单位 Prefab")]
    public GameObject[] AIPrefabs;          // 留空则从 Resources 加载

    // 内部状态
    private int   aiGold      = 800;
    private float waveTimer   = 0f;
    private float goldTimer   = 0f;
    private int   waveCount   = 0;
    private float harassTimer = 0f;  // 骚扰计时器
    private readonly List<RTSUnit> aliveAIUnits = new List<RTSUnit>();  // 存活 AI 单位列表

    private RTSBuilding aiMainBase;
    private RTSBuilding playerMainBase;

    private float defenseTimer = 0f;       // 防御冷却
    private const float DefenseCooldown = 12f;
    private const float BaseAlertRadius  = 70f;

    // 建筑建造状态（各阶段只建一次）
    private bool builtBarracks    = false;
    private bool builtPowerPlant  = false;
    private bool builtTankFactory = false;
    private bool builtAirFactory  = false;
    private bool builtAirfield    = false;
    // 撤退检查间隔
    private float retreatCheckTimer = 0f;
    private const float RetreatCheckInterval = 3f;
    // 建筑检查间隔
    private float buildCheckTimer = 0f;
    private const float BuildCheckInterval = 15f;

    public int CurrentGold => aiGold;

    // 波次单位池（按游斗时长选择）
    static readonly string[] EarlyUnits  = { "Infantry", "Infantry", "Infantry" };
    static readonly string[] MidUnits    = { "Infantry", "LightTank", "Artillery" };
    static readonly string[] LateUnits   = { "LightTank", "MediumTank", "Artillery", "Flamethrower" };
    static readonly string[] EliteUnits  = { "MediumTank", "HeavyTank", "Artillery", "Flamethrower", "Fighter", "Bomber", "ScoutPlane" };

    void Start()
    {
        StartCoroutine(InitAfterNetworkReady());
    }

    System.Collections.IEnumerator InitAfterNetworkReady()
    {
        // 等待 0.8s，给 GameNetworkSync 足够时间完成 WS 握手并接收 role
        yield return new WaitForSeconds(0.8f);
        // 联机模式下 AI 不介入（双方均由真人控制）
        if (GameNetworkSync.Instance != null && GameNetworkSync.Instance.IsNetworkGame)
        {
            enabled = false;
            yield break;
        }
        aiGold = InitialGold;
        GameManager.Instance?.RecordEnemyGoldSnapshot(aiGold);
        // 查找双方基地
        foreach (var b in GameManager.Instance?.GetAllBuildings() ?? new List<RTSBuilding>())
        {
            if (b.bIsMainBase)
            {
                if (!b.bPlayerOwned && aiMainBase == null)     aiMainBase     = b;
                if (b.bPlayerOwned  && playerMainBase == null) playerMainBase = b;
            }
        }
        LoadPrefabs();
    }

    void LoadPrefabs()
    {
        if (AIPrefabs != null && AIPrefabs.Length > 0)
        {
            AIPrefabs = NormalizeEnemyPrefabs(AIPrefabs);
            return;
        }

        var list = new List<GameObject>();
        foreach (var n in new[] { "Infantry", "Artillery", "LightTank", "MediumTank", "HeavyTank", "Flamethrower", "Fighter", "Bomber", "ScoutPlane" })
        {
            var p = Resources.Load<GameObject>($"Prefabs/{n}_E");
            if (p == null) p = Resources.Load<GameObject>($"Prefabs/{n}");
            if (p != null) list.Add(p);
        }
        AIPrefabs = list.ToArray();
        if (AIPrefabs.Length == 0)
            Debug.LogWarning("AIController: Resources/Prefabs 中未找到单位Prefab");
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.bGameOver) return;
        if (GameNetworkSync.Instance != null && GameNetworkSync.Instance.IsNetworkGame) return;

        // 追踪基地（可能中途被摧毁）
        if (aiMainBase == null || playerMainBase == null) RefreshBases();

        UpdateGold();
        UpdateWave();
        UpdateHarass();
        UpdateDefense();
        UpdateBuilding();
        UpdateRetreat();
    }

    // ── 基地防御（玩家进攻时立即响应）──────────────────────
    void UpdateDefense()
    {
        defenseTimer -= Time.deltaTime;
        if (defenseTimer > 0f || aiMainBase == null) return;

        // 找出离 AI 基地最近的玩家单位
        RTSUnit closest = null;
        float closestDist = BaseAlertRadius;
        var all = GameManager.Instance?.GetAllUnits();
        if (all == null) return;
        foreach (var u in all)
        {
            if (u == null || u.IsDead() || !u.IsPlayerOwned()) continue;
            float d = Vector3.Distance(u.transform.position, aiMainBase.transform.position);
            if (d < closestDist) { closestDist = d; closest = u; }
        }
        if (closest == null) { defenseTimer = 3f; return; }  // 无威胁时 3s 后再检查

        // 有玩家单位威胁基地，派遣防御分队
        aliveAIUnits.RemoveAll(u => u == null || u.IsDead());
        if (aliveAIUnits.Count >= MaxAliveAIUnits) return;

        defenseTimer = DefenseCooldown;
        int defenders = waveCount >= 5 ? 3 : 2;
        Vector3 spawnBase = aiMainBase.transform.position;
        for (int i = 0; i < defenders; i++)
        {
            Vector3 sp = spawnBase + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
            SpawnAIUnit(sp, closest.transform.position);
        }
    }

    void RefreshBases()
    {
        foreach (var b in GameManager.Instance?.GetAllBuildings() ?? new List<RTSBuilding>())
        {
            if (b == null) continue;
            if (b.bIsMainBase && !b.bPlayerOwned && aiMainBase == null)     aiMainBase     = b;
            if (b.bIsMainBase &&  b.bPlayerOwned && playerMainBase == null) playerMainBase = b;
        }
    }

    void UpdateGold()
    {
        goldTimer += Time.deltaTime;
        if (goldTimer >= GoldInterval)
        {
            goldTimer = 0f;
            // 随游斗时长增加收益
            float timeBonus = Mathf.Min(GameManager.Instance.GameTime / 180f, 2f);
            int income = Mathf.RoundToInt(GoldPerInterval * (1f + timeBonus));
            aiGold += income;
            GameManager.Instance?.RecordGoldIncome(false, income);
            GameManager.Instance?.RecordEnemyGoldSnapshot(aiGold);
        }
    }

    // ── AI 建筑重建（被摧毁后补建）──────────────────────────
    void UpdateBuilding()
    {
        if (aiMainBase == null) return;
        buildCheckTimer += Time.deltaTime;
        if (buildCheckTimer < BuildCheckInterval) return;
        buildCheckTimer = 0f;
        // builtXxx = 30s 冷却标记，避免同一建筑连续下单重建
        bool hasBarracks = false, hasPowerPlant = false, hasTankFactory = false, hasAirFactory = false, hasAirfield = false;
        foreach (var b in GameManager.Instance.GetAllBuildings())
        {
            if (b == null || b.bPlayerOwned) continue;
            if (b is Barracks)    hasBarracks    = true;
            if (b is PowerPlant)  hasPowerPlant  = true;
            if (b is TankFactory) hasTankFactory = true;
            if (b is AirFactory)  hasAirFactory  = true;
            if (b is Airfield)    hasAirfield    = true;
        }
        if (!hasBarracks    && !builtBarracks    && aiGold >= 300) { PlaceAIBuilding("Barracks",    300); builtBarracks    = true; StartCoroutine(ResetBuildFlag(()=>builtBarracks    = false, 30f)); }
        if (!hasPowerPlant  && !builtPowerPlant  && aiGold >= 200) { PlaceAIBuilding("PowerPlant",  200); builtPowerPlant  = true; StartCoroutine(ResetBuildFlag(()=>builtPowerPlant  = false, 30f)); }
        if (!hasTankFactory && !builtTankFactory && aiGold >= 400) { PlaceAIBuilding("TankFactory", 400); builtTankFactory = true; StartCoroutine(ResetBuildFlag(()=>builtTankFactory = false, 30f)); }
        if (!hasAirfield    && !builtAirfield    && aiGold >= 260) { PlaceAIBuilding("Airfield",    260); builtAirfield    = true; StartCoroutine(ResetBuildFlag(()=>builtAirfield    = false, 30f)); }
        if (!hasAirFactory  && !builtAirFactory  && aiGold >= 400) { PlaceAIBuilding("AirFactory",  400); builtAirFactory  = true; StartCoroutine(ResetBuildFlag(()=>builtAirFactory  = false, 30f)); }
    }

    System.Collections.IEnumerator ResetBuildFlag(System.Action reset, float delay)
    {
        yield return new WaitForSeconds(delay);
        reset();
    }

    void PlaceAIBuilding(string prefabBase, int cost)
    {
        var prefab = Resources.Load<GameObject>($"Prefabs/{prefabBase}_E");
        if (prefab == null) prefab = Resources.Load<GameObject>($"Prefabs/{prefabBase}");
        if (prefab == null && prefabBase != "Airfield") { Debug.LogWarning($"AIController: 找不到 {prefabBase}_E Prefab"); return; }
        Type buildingType = prefabBase == "Airfield"
            ? typeof(Airfield)
            : prefab != null && prefab.GetComponent<RTSBuilding>() != null
                ? prefab.GetComponent<RTSBuilding>().GetType()
                : null;
        if (buildingType != null
            && !MainBase.CanConstructBuilding(buildingType, string.Empty, false, out _))
            return;
        // 在基地附近随机选一个偏移位置放置
        Vector3 basePos = aiMainBase.transform.position;
        Vector3 offset  = new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
        GameObject go = prefab != null
            ? Object.Instantiate(prefab, basePos + offset, Quaternion.identity)
            : CreateFallbackAirfield("Airfield_E", basePos + offset, false);
        var b = go.GetComponent<RTSBuilding>();
        if (b != null)
        {
            b.bPlayerOwned = false;
            b.BeginConstruction();
        }
        aiGold -= cost;
        GameManager.Instance?.RecordGoldSpent(false, cost);
        GameManager.Instance?.RecordEnemyGoldSnapshot(aiGold);
    }

    // ── AI 单位撤退（低血量回基地）────────────────────────
    void UpdateRetreat()
    {
        if (aiMainBase == null) return;
        retreatCheckTimer += Time.deltaTime;
        if (retreatCheckTimer < RetreatCheckInterval) return;
        retreatCheckTimer = 0f;
        aliveAIUnits.RemoveAll(u => u == null || u.IsDead());
        foreach (var u in aliveAIUnits)
        {
            if (u == null || u.IsDead()) continue;
            // HP 低于 25% 且不在基地附近时撤退
            float hpRatio = (float)u.GetHP() / u.GetMaxHP();
            float distToBase = Vector3.Distance(u.transform.position, aiMainBase.transform.position);
            if (hpRatio < 0.25f && distToBase > 25f)
                u.ApplyMoveCommand(aiMainBase.transform.position
                    + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)));
        }
    }

    // ── 主波进攻 ─────────────────────────────────────────
    void UpdateWave()
    {
        // 随游斗时长缩短间隔（最少到 12s）
        float dynamicInterval = Mathf.Max(12f, AttackWaveInterval - waveCount * 2f);
        waveTimer += Time.deltaTime;
        if (!IsOffenseUnlocked()) return;
        if (waveTimer < dynamicInterval) return;
        waveTimer = 0f;

        if (aiMainBase == null || playerMainBase == null) return;

        // 清理已死亡的引用
        aliveAIUnits.RemoveAll(u => u == null || u.IsDead());
        // 存活数超上限则跳过本波
        if (aliveAIUnits.Count >= MaxAliveAIUnits) return;

        waveCount++;
        int count = Mathf.Min(WaveBaseSize + Mathf.RoundToInt(waveCount * 1.5f), 15); // 每波最多15

        // 游斗后期改为两路进攻
        bool twoProng = waveCount >= 4;
        LaunchGroup(count, playerMainBase.transform.position, twoProng);

        // 通知 HUD：屏幕顶部预警横幅 + 短摄像机震动
        if (RTSHUD.Instance != null)
            RTSHUD.Instance.ShowWaveWarning(waveCount, count, twoProng);
        RTSCamera.Shake(0.6f, 0.18f);
    }

    void LaunchGroup(int count, Vector3 target, bool twoProng)
    {
        Vector3 spawn = aiMainBase.transform.position;
        // 两路：一半单位绕侧翼进攻（基于AI基地→玩家基地方向的垂直偏移）
        Vector3 mainDir = (target - aiMainBase.transform.position).normalized;
        for (int i = 0; i < count; i++)
        {
            Vector3 flankOff = new Vector3(-mainDir.z, 0, mainDir.x) * 80f;
            Vector3 flanktarget = twoProng && i >= count / 2 ? target + flankOff : target;
            SpawnAIUnit(spawn + new Vector3(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f)), flanktarget);
        }
    }

    // ── 骚扰攻击（小条随机进攻）──────────────────────
    void UpdateHarass()
    {
        if (!IsOffenseUnlocked()) return;
        if (waveCount < 3) return;  // 第3波后开始骚扰
        harassTimer += Time.deltaTime;
        if (harassTimer < 20f) return;
        harassTimer = 0f;

        if (aiMainBase == null || playerMainBase == null) return;

        aliveAIUnits.RemoveAll(u => u == null || u.IsDead());
        if (aliveAIUnits.Count >= MaxAliveAIUnits) return;

        // 小队骚扰：2个单位走侧路
        Vector3 flankPos = playerMainBase.transform.position +
                           new Vector3(Random.Range(-160f, 160f), 0, Random.Range(-80f, 80f));
        SpawnAIUnit(aiMainBase.transform.position + new Vector3(-5, 0, 0), flankPos);
        SpawnAIUnit(aiMainBase.transform.position + new Vector3( 5, 0, 0), flankPos);
    }

    // ── 生成单位 ───────────────────────────────────────
    void SpawnAIUnit(Vector3 pos, Vector3 attackTarget)
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) return;
        // 空中单位需要高度偏移，地面单位 y 固定为 0
        AirUnit airPrefab = prefab.GetComponent<AirUnit>();
        bool isAir = airPrefab != null;
        bool requiresAirfieldSlot = airPrefab != null && airPrefab.RequiresAirfieldSlot;
        Airfield launchAirfield = null;
        if (requiresAirfieldSlot)
        {
            launchAirfield = FindAvailableAirfield(false);
            if (launchAirfield == null) return;
            pos = launchAirfield.GetAirApproachPoint(null, 12f);
        }
        Vector3 safePos = isAir
            ? new Vector3(pos.x, 12f, pos.z)
            : new Vector3(pos.x, 0f, pos.z);
        GameObject go = Object.Instantiate(prefab, safePos, Quaternion.identity);
        RTSUnit unit = go.GetComponent<RTSUnit>();
        if (unit != null)
        {
            // 金币不足则取消生成
            if (aiGold < unit.GoldCost) { Destroy(go); return; }
            aiGold -= unit.GoldCost;
            GameManager.Instance?.RecordGoldSpent(false, unit.GoldCost);
            GameManager.Instance?.RecordUnitProduced(false);
            GameManager.Instance?.RecordEnemyGoldSnapshot(aiGold);
            unit.bPlayerOwned = false;
            if (requiresAirfieldSlot && launchAirfield != null)
            {
                var air = unit as AirUnit;
                if (air != null) launchAirfield.TryAcceptAircraft(air);
            }
            aliveAIUnits.Add(unit);
            var agent = isAir ? null : go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            StartCoroutine(DelayCommand(unit, agent, safePos, attackTarget));
        }
    }

    IEnumerator DelayCommand(RTSUnit unit, UnityEngine.AI.NavMeshAgent agent, Vector3 spawnPos, Vector3 target)
    {
        yield return null;  // 等一帧
        // Warp agent 到 NavMesh 最近点
        if (agent != null && agent.enabled)
        {
            UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit nh, 15f, UnityEngine.AI.NavMesh.AllAreas);
            if (nh.hit) agent.Warp(nh.position);
        }
        if (unit != null && !unit.IsDead())
            unit.ApplyAttackMoveCommand(target);
    }

    GameObject PickPrefab()
    {
        if (AIPrefabs == null || AIPrefabs.Length == 0) { LoadPrefabs(); return null; }
        float t = GameManager.Instance.GameTime;
        string[] pool = t < 60f  ? EarlyUnits :
                        t < 180f ? MidUnits   :
                        t < 360f ? LateUnits  : EliteUnits;
        bool hasAirfieldSlot = FindAvailableAirfield(false) != null;
        // 金币感知：金币不足 200 时优先选步兵（最便宜）
        if (aiGold < 200)
        {
            foreach (var p in AIPrefabs)
                if (IsUnitPrefab(p, "Infantry")) return p;
        }
        // 尝试从池中选，金币足够才允许贵单位
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string name = pool[Random.Range(0, pool.Length)];
            foreach (var p in AIPrefabs)
            {
                if (!IsUnitPrefab(p, name)) continue;
                var proto = p.GetComponent<RTSUnit>();
                if (!hasAirfieldSlot && RequiresAirfieldSlot(p)) continue;
                if (proto == null || aiGold >= proto.GoldCost) return p;
            }
        }
        // 最终 fallback：找金币够用的最便宜单位
        GameObject cheapest = null; int cheapestCost = int.MaxValue;
        foreach (var p in AIPrefabs)
        {
            if (p == null) continue;
            var proto = p.GetComponent<RTSUnit>();
            if (proto == null) continue;
            if (!hasAirfieldSlot && RequiresAirfieldSlot(p)) continue;
            if (proto.GoldCost <= aiGold && proto.GoldCost < cheapestCost)
            { cheapestCost = proto.GoldCost; cheapest = p; }
        }
        return cheapest;
    }

    Airfield FindAvailableAirfield(bool playerOwned)
    {
        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null) return null;

        Airfield best = null;
        float bestDist = float.MaxValue;
        Vector3 anchor = aiMainBase != null ? aiMainBase.transform.position : transform.position;
        foreach (var b in buildings)
        {
            var airfield = b as Airfield;
            if (airfield == null || airfield.bPlayerOwned != playerOwned) continue;
            if (airfield.GetHP() <= 0 || airfield.bUnderConstruction || airfield.FreeAircraftSlots <= 0) continue;
            float d = Vector3.Distance(anchor, airfield.transform.position);
            if (d < bestDist)
            {
                best = airfield;
                bestDist = d;
            }
        }
        return best;
    }

    static GameObject CreateFallbackAirfield(string name, Vector3 pos, bool playerOwned)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(pos.x, 0f, pos.z);
        var collider = go.AddComponent<BoxCollider>();
        Airfield.ConfigureCollider(collider);
        BuildFallbackAirfieldVisual(go.transform, playerOwned);
        var airfield = go.AddComponent<Airfield>();
        airfield.bPlayerOwned = playerOwned;
        return go;
    }

    static void BuildFallbackAirfieldVisual(Transform parent, bool playerOwned)
    {
        Airfield.BuildVisual(parent, playerOwned, AddFallbackPart);
    }

    static void AddFallbackPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        var col = part.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }

    bool IsOffenseUnlocked()
    {
        if (GameManager.Instance == null)
            return false;

        return GameManager.Instance.GameTime >= Mathf.Max(0f, InitialAttackDelay);
    }

    static bool RequiresAirfieldSlot(GameObject prefab)
    {
        var air = prefab != null ? prefab.GetComponent<AirUnit>() : null;
        return air != null && air.RequiresAirfieldSlot;
    }

    static bool IsUnitPrefab(GameObject prefab, string baseName)
    {
        if (prefab == null || string.IsNullOrEmpty(baseName))
            return false;

        string name = prefab.name;
        if (name == baseName)
            return true;

        return name == baseName + "_E"
            || name == baseName + "_Enemy"
            || name == baseName + "_P"
            || name == baseName + "_Player";
    }

    static GameObject[] NormalizeEnemyPrefabs(GameObject[] prefabs)
    {
        if (prefabs == null)
            return prefabs;

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject enemyPrefab = ResolveEnemyPrefab(prefabs[i]);
            if (enemyPrefab != null)
                prefabs[i] = enemyPrefab;
        }

        return prefabs;
    }

    static GameObject ResolveEnemyPrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;

        string baseName = GetUnitBaseName(prefab.name);
        GameObject enemyPrefab = Resources.Load<GameObject>($"Prefabs/{baseName}_E");
        return enemyPrefab != null ? enemyPrefab : prefab;
    }

    static string GetUnitBaseName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return prefabName;

        string[] suffixes = { "_Enemy", "_Player", "_E", "_P" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (prefabName.EndsWith(suffix, System.StringComparison.Ordinal))
                return prefabName.Substring(0, prefabName.Length - suffix.Length);
        }

        return prefabName;
    }
}
