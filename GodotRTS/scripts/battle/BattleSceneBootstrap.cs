using Godot;
using GodotRTS.Battle.Combat;
using GodotRTS.Battle.Unit;
using GodotRTS.Battle.Network;
using GodotRTS.Core;

namespace GodotRTS.Battle
{
    /// <summary>
    /// 战斗场景启动引导器。
    /// 在 _Ready 最后阶段统一完成各系统的跨节点连接，
    /// 避免在各子系统中硬编码 NodePath。
    /// 挂载于战斗场景根节点，执行顺序应排在所有子系统之后。
    /// </summary>
    public partial class BattleSceneBootstrap : Node
    {
        // ── Inspector（所有子系统 NodePath）────────────────────────
        [Export] public NodePath BattleManagerPath      { get; set; }
        [Export] public NodePath UnitFactoryPath        { get; set; }
        [Export] public NodePath CombatActionSysPath    { get; set; }
        [Export] public NodePath CombatLoggerPath       { get; set; }
        [Export] public NodePath CombatAIManagerPath    { get; set; }
        [Export] public NodePath AoeSkillSysPath        { get; set; }
        [Export] public NodePath StatusEffectSysPath    { get; set; }
        [Export] public NodePath ThreatSysPath          { get; set; }
        [Export] public NodePath ShieldBlockSysPath     { get; set; }
        [Export] public NodePath WaveSpawnerPath        { get; set; }
        [Export] public NodePath BattleEventBusPath     { get; set; }
        [Export] public NodePath ScreenShakeCtrlPath    { get; set; }
        [Export] public NodePath CombatSoundBankPath    { get; set; }
        [Export] public NodePath UnitSyncReceiverPath   { get; set; }
        [Export] public NodePath BattleNetworkPath      { get; set; }
        [Export] public NodePath LootDropSysPath        { get; set; }
        [Export] public NodePath ComboSysPath           { get; set; }
        [Export] public NodePath ReplayRecorderPath     { get; set; }
        [Export] public NodePath UnitOutlineEffectPath  { get; set; }
        [Export] public NodePath UnitSelectorPath       { get; set; }
        [Export] public NodePath BattleResultUIPath     { get; set; }

        // ── 生命周期 ────────────────────────────────────────────────
        public override void _Ready()
        {
            // 读取 GameManager 配置
            var cfg = GameManager.Instance?.PendingBattleConfig;

            ConnectSystems(cfg);
            GD.Print("[BattleSceneBootstrap] 所有战斗系统连接完成。");
        }

        // ── 系统连接 ────────────────────────────────────────────────
        private void ConnectSystems(Core.BattleStartConfig cfg)
        {
            var battleMgr    = GetNodeOrNull<BattleManager>(BattleManagerPath);
            var combatSys    = GetNodeOrNull<CombatActionSystem>(CombatActionSysPath);
            var combatLogger = GetNodeOrNull<CombatLogger>(CombatLoggerPath);
            var aoeSys       = GetNodeOrNull<AoeSkillSystem>(AoeSkillSysPath);
            var statusSys    = GetNodeOrNull<StatusEffectSystem>(StatusEffectSysPath);
            var threatSys    = GetNodeOrNull<ThreatSystem>(ThreatSysPath);
            var eventBus     = GetNodeOrNull<BattleEventBus>(BattleEventBusPath);
            var shakeCtrl    = GetNodeOrNull<ScreenShakeController>(ScreenShakeCtrlPath);
            var soundBank    = GetNodeOrNull<CombatSoundBank>(CombatSoundBankPath);
            var syncRecv     = GetNodeOrNull<UnitSyncReceiver>(UnitSyncReceiverPath);
            var lootSys      = GetNodeOrNull<LootDropSystem>(LootDropSysPath);
            var replayRec    = GetNodeOrNull<CombatReplayRecorder>(ReplayRecorderPath);
            var waveSpawner  = GetNodeOrNull<WaveSpawner>(WaveSpawnerPath);

            // ── CombatActionSystem → 各监听器 ───────────────────────
            // 所有战斗事件通过 CombatActionSystem 的 _Process 分发
            // 以下订阅均在主线程，无需额外锁

            // CombatLogger 记录所有事件
            // （CombatLogger 通过 NodePath 直接引用 CombatActionSystem，
            //  此处补充：在 CombatActionSystem._Process 后手动转发）
            // 注：实际项目中可通过信号或在 CombatActionSystem 内部调用 Logger.Log

            // ── BattleManager 事件总线连接 ──────────────────────────
            if (battleMgr != null && eventBus != null)
            {
                battleMgr.BattleStarted += () => eventBus.PublishBattleStarted();
                battleMgr.BattleEnded   += (w) => eventBus.PublishBattleEnded(w);
            }

            // ── WaveSpawner → BattleEventBus ────────────────────────
            if (waveSpawner != null && eventBus != null)
            {
                waveSpawner.WaveStarted     += (i, t) => eventBus.PublishWaveStarted(i, t);
                waveSpawner.AllWavesCleared += ()      => eventBus.PublishAllWavesCleared();
            }

            // ── AoeSkillSystem → ScreenShakeController ───────────────
            if (aoeSys != null && shakeCtrl != null)
            {
                aoeSys.AoeHitUnit += (skillId, casterId, targetId, dmg, isCrit) =>
                    shakeCtrl.OnAoeSkill(1);
            }

            // ── BattleResultUI → GameManager ────────────────────────
            var resultUI = GetNodeOrNull<BattleResultUI>(BattleResultUIPath);
            if (resultUI != null)
                resultUI.CloseRequested += () => GameManager.Instance?.ReturnToLobby();

            // ── 离线/联机模式配置 ────────────────────────────────────
            bool isOffline = cfg?.IsOffline ?? true;
            if (isOffline)
            {
                GD.Print("[Bootstrap] 离线模式：跳过网络同步。");
            }
            else
            {
                GD.Print($"[Bootstrap] 联机模式：{cfg?.ServerIp}:{cfg?.ServerPort}");
            }

            // ── 玩家队伍配置 ─────────────────────────────────────────
            if (battleMgr != null && cfg != null)
                battleMgr.PlayerTeamId = cfg.PlayerTeamId;

            // ── 回放录制 ────────────────────────────────────────────
            replayRec?.StartRecording();

            // ── 战斗结束时自动保存回放 ──────────────────────────────
            if (battleMgr != null && replayRec != null)
            {
                battleMgr.BattleEnded += (winTeamId) =>
                {
                    replayRec.StopRecording();
                    replayRec.SaveReplay();
                };
            }
        }
    }
}
