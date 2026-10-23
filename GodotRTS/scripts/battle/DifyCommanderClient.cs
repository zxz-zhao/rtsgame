using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public partial class DifyCommanderClient : Node
{
    public static DifyCommanderClient? Instance { get; private set; }

    [Export] public string EndpointUrl { get; set; } = "http://127.0.0.1:9564/v1/chat-messages";
    [Export] public string ApiToken { get; set; } = "app-rtsCommanderKey2026AI";
    [Export] public float QueryIntervalSeconds { get; set; } = 12.0f;
    [Export] public bool EnableDifyCommander { get; set; } = true;

    [Signal]
    public delegate void TacticalDecisionReceivedEventHandler(string analysis, string focus, string target, string stance);

    [Signal]
    public delegate void CommanderStatusChangedEventHandler(string statusText, bool isOnline);

    private static readonly System.Net.Http.HttpClient HttpClientInstance = new()
    {
        Timeout = TimeSpan.FromSeconds(25.0)
    };

    private float timer;
    private bool isRequestInProgress;
    private string lastAnalysis = "战术观察中：AI 正在评估双方兵力与防线态势...";
    private string currentFocus = "balanced";
    private string currentTarget = "field_units";
    private string currentStance = "aggressive";
    private readonly List<string> recommendedUnits = new();
    private string recommendedTech = "";
    private bool isDifyOnline = false;

    public string LastAnalysis => lastAnalysis;
    public string CurrentFocus => currentFocus;
    public string CurrentTarget => currentTarget;
    public string CurrentStance => currentStance;
    public IReadOnlyList<string> RecommendedUnits => recommendedUnits;
    public string RecommendedTech => recommendedTech;
    public bool IsDifyOnline => isDifyOnline;

    public override void _Ready()
    {
        Instance = this;
        timer = 1.0f; // 开局 1 秒后迅速触发首次大模型战况诊断
    }

    public override void _Process(double delta)
    {
        if (!EnableDifyCommander)
            return;

        var manager = BattleGameManager.Instance;
        if (manager is null || manager.GameOver)
            return;

        timer -= (float)delta;
        if (timer <= 0f && !isRequestInProgress)
        {
            timer = QueryIntervalSeconds;
            TriggerTacticalQueryAsync(manager);
        }
    }

    private void TriggerTacticalQueryAsync(BattleGameManager manager)
    {
        if (isRequestInProgress)
            return;

        isRequestInProgress = true;

        // 收集战场轻量态势感知数据 (在主线程完成，零 GC 开销)
        var aiUnits = manager.GetUnits(playerOwned: false);
        var playerUnits = manager.GetUnits(playerOwned: true);
        var aiGold = manager.EnemyGold;
        var gameTime = Mathf.RoundToInt(manager.GameTime);

        int playerTanks = 0, playerInfantry = 0, playerAir = 0, playerArtillery = 0;
        foreach (var u in playerUnits)
        {
            if (BattleUnitCatalog.IsAirUnit(u.UnitKey)) playerAir++;
            else if (BattleUnitCatalog.IsArtilleryLike(u.UnitKey)) playerArtillery++;
            else if (u.UnitKey.Contains("tank")) playerTanks++;
            else playerInfantry++;
        }

        int aiTanks = 0, aiInfantry = 0, aiAir = 0;
        foreach (var u in aiUnits)
        {
            if (BattleUnitCatalog.IsAirUnit(u.UnitKey)) aiAir++;
            else if (u.UnitKey.Contains("tank")) aiTanks++;
            else aiInfantry++;
        }

        string threatLevel = playerTanks >= 2 || playerAir >= 2 ? "high" : (playerUnits.Count >= 4 ? "medium" : "low");

        var queryPayload = new
        {
            game_time = gameTime,
            ai_gold = aiGold,
            ai_army = new { tanks = aiTanks, infantry = aiInfantry, air = aiAir },
            player_threats = new { tanks = playerTanks, infantry = playerInfantry, air = playerAir, artillery = playerArtillery },
            threat_level = threatLevel
        };

        string jsonQuery = JsonSerializer.Serialize(queryPayload);

        // 异步后台线程执行网络请求，绝不阻塞 Godot 渲染帧
        // 异步后台线程执行网络请求，绝不阻塞 Godot 渲染帧
        _ = Task.Run(async () =>
        {
            var targetUrls = new List<string> { EndpointUrl };
            if (OS.HasFeature("android"))
            {
                var lanFallback = "http://192.168.1.176:9564/v1/chat-messages";
                if (!targetUrls.Contains(lanFallback))
                    targetUrls.Add(lanFallback);
            }

            try
            {
                var requestBody = new
                {
                    inputs = new { },
                    query = jsonQuery,
                    response_mode = "blocking",
                    user = "rts-battle-commander"
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);

                foreach (var url in targetUrls)
                {
                    try
                    {
                        GD.Print($"[DifyCommander] 📡 正在向 Dify 战术大脑发起诊断 ({url})...");
                        using var request = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                        };
                        request.Headers.Add("Authorization", $"Bearer {ApiToken}");

                        using var response = await HttpClientInstance.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            var responseStr = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(responseStr);
                            if (doc.RootElement.TryGetProperty("answer", out var answerProp))
                            {
                                var answerText = answerProp.GetString() ?? "";
                                ParseDifyTacticalDecision(answerText);
                                isDifyOnline = true;
                                CallDeferred(nameof(NotifySuccess), lastAnalysis, currentFocus, currentTarget, currentStance);
                                return;
                            }
                        }
                        else
                        {
                            GD.Print($"[DifyCommander] 端点 {url} 返回 HTTP {(int)response.StatusCode}");
                        }
                    }
                    catch (Exception reqEx)
                    {
                        GD.Print($"[DifyCommander] 请求端点 {url} 失败: {reqEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.Print($"[DifyCommander] 异步网络异常 (无缝回退本地战术保底): {ex.Message}");
            }
            finally
            {
                isRequestInProgress = false;
            }

            // Dify 离线或异常时的智能战术保底 (Heuristic Fallback)
            ExecuteSmartHeuristicFallback(playerTanks, playerAir, playerArtillery, playerInfantry);
            isDifyOnline = false;
            CallDeferred(nameof(NotifyFallback), lastAnalysis, currentFocus, currentTarget, currentStance);
        });
    }

    private void ParseDifyTacticalDecision(string answerText)
    {
        try
        {
            var cleanJson = ExtractJsonFromMarkdown(answerText);
            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("tactical_analysis", out var aProp))
                lastAnalysis = aProp.GetString() ?? lastAnalysis;

            if (root.TryGetProperty("priority_focus", out var fProp))
                currentFocus = fProp.GetString() ?? currentFocus;

            if (root.TryGetProperty("attack_target", out var tProp))
                currentTarget = tProp.GetString() ?? currentTarget;

            if (root.TryGetProperty("stance", out var sProp))
                currentStance = sProp.GetString() ?? currentStance;

            recommendedUnits.Clear();
            if (root.TryGetProperty("produce_units", out var uArr) && uArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in uArr.EnumerateArray())
                {
                    var uKey = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(uKey))
                        recommendedUnits.Add(uKey);
                }
            }

            if (root.TryGetProperty("tech_research", out var techProp))
                recommendedTech = techProp.GetString() ?? "";
        }
        catch (Exception e)
        {
            GD.Print($"[DifyCommander] 解析 Dify JSON 失败，执行默认提取: {e.Message}");
        }
    }

    private static string ExtractJsonFromMarkdown(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text.Substring(start, end - start + 1);
        return text;
    }

    private void ExecuteSmartHeuristicFallback(int pTanks, int pAir, int pArtillery, int pInfantry)
    {
        recommendedUnits.Clear();
        if (pAir >= 1)
        {
            currentFocus = "air_defense";
            lastAnalysis = "侦测到敌方航空部队，防空高炮与拦截战机紧急就位。";
            recommendedUnits.Add("anti_air_gun");
            recommendedUnits.Add("fighter");
            recommendedTech = "composite_ammo";
            currentTarget = "field_units";
            currentStance = "defensive";
        }
        else if (pTanks >= 2)
        {
            currentFocus = "anti_armor";
            lastAnalysis = "敌军装甲集群压境，前线调动火箭兵与重型反坦克穿甲部队包抄。";
            recommendedUnits.Add("infantry_rocket");
            recommendedUnits.Add("tank");
            recommendedUnits.Add("heavy_tank");
            recommendedTech = "heavy_armor";
            currentTarget = "field_units";
            currentStance = "aggressive";
        }
        else if (pArtillery >= 1)
        {
            currentFocus = "counter_battery";
            lastAnalysis = "敌方远程火炮校射中，命令高速轻坦与空军快速突袭敌炮兵阵地。";
            recommendedUnits.Add("light_tank");
            recommendedUnits.Add("fighter");
            recommendedTech = "jet_engine";
            currentTarget = "field_units";
            currentStance = "flank";
        }
        else
        {
            currentFocus = "push_base";
            lastAnalysis = "战场局势可控，组织坦克与火箭步兵梯队向玩家阵地全面推进。";
            recommendedUnits.Add("tank");
            recommendedUnits.Add("infantry_rifle");
            recommendedTech = "radar_scan";
            currentTarget = "player_base";
            currentStance = "aggressive";
        }
    }

    private void NotifySuccess(string analysis, string focus, string target, string stance)
    {
        GD.Print($"[DifyCommander] ✅ 成功获取 Dify 战略决策: {analysis} | 重点: {focus} | 目标: {target} | 姿态: {stance} | 建议单位: [{string.Join(", ", recommendedUnits)}]");
        EmitSignal(SignalName.TacticalDecisionReceived, analysis, focus, target, stance);
        EmitSignal(SignalName.CommanderStatusChanged, $"🤖 Dify 战术指挥官: {analysis}", true);
    }

    private void NotifyFallback(string analysis, string focus, string target, string stance)
    {
        GD.Print($"[DifyCommander] 🛡️ 战术推演保底生效: {analysis} | 重点: {focus} | 目标: {target} | 姿态: {stance} | 建议单位: [{string.Join(", ", recommendedUnits)}]");
        EmitSignal(SignalName.TacticalDecisionReceived, analysis, focus, target, stance);
        EmitSignal(SignalName.CommanderStatusChanged, $"🛡️ 战术推演保底: {analysis}", false);
    }
}

