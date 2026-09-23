using Godot;
using System;
using System.Collections.Generic;
using GodotRTS.Battle.Combat;
using GodotRTS.Battle.Unit;
using GodotRTS.Core;

namespace GodotRTS.Battle
{
    // ─────────────────────────────────────────────────────────────
    //  战斗状态枚举
    // ─────────────────────────────────────────────────────────────
    public enum BattleState
    {
        Idle,       // 未开始
        Countdown,  // 倒计时
        Running,    // 进行中
        Paused,     // 暂停
        Ended,      // 已结束
    }

    // ─────────────────────────────────────────────────────────────
    //  BattleManager — 战斗生命周期管理器
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 战斗生命周期管理器（场景单例）。
    /// 负责：倒计时开始 → 运行 → 胜负判定 → 结算 → 返回大厅。
    /// 挂载于战斗场景根节点。
    /// </summary>
    public partial class BattleManager : Node
    {
        // ── Inspector ───────────────────────────────────────────────
        [Export] public NodePath CombatLoggerPath    { get; set; }
        [Export] public NodePath BattleResultUIPath  { get; set; }
        [Export] public NodePath CombatAIManagerPath { get; set; }
        [Export] public NodePath BattleNetworkPath   { get; set; }
        [Export] public NodePath UnitSelectorPath    { get; set; }

        /// <summary>本地玩家队伍 ID。</summary>
        [Export] public int  PlayerTeamId      { get; set; } = 0;
        /// <summary>开战前倒计时（秒）。</summary>
        [Export] public float CountdownSeconds { get; set; } = 3f;
        /// <summary>胜负判定间隔（秒）。</summary>
        [Export] public float WinCheckInterval { get; set; } = 1f;
        /// <summary>战斗结束后延迟显示结算面板（秒）。</summary>
        [Export] public float ResultShowDelay  { get; set; } = 2f;

        // ── 信号 ────────────────────────────────────────────────────
        [Signal] public delegate void BattleStartedEventHandler();
        [Signal] public delegate void BattlePausedEventHandler(bool paused);
        [Signal] public delegate void BattleEndedEventHandler(int winTeamId);
        [Signal] public delegate void CountdownTickEventHandler(int secondsLeft);

        // ── 运行时 ──────────────────────────────────────────────────
        public BattleState State { get; private set; } = BattleState.Idle;
        public float       ElapsedSeconds { get; private set; }
        public int         WinnerTeamId   { get; private set; } = -1;

        private CombatLogger    _logger;
        private BattleResultUI  _resultUI;
        private CombatAIManager _aiMgr;
        private UnitSelector    _selector;

        private float _countdownTimer;
        private float _winCheckTimer;
        private float _resultDelayTimer;
        private bool  _pendingResult;

        // 注册的所有单位（由 UnitFactory 填充）
        private readonly List<UnitBase> _allUnits = new(256);

        // ── 生命周期 ────────────────────────────────────────────────
        public override void _Ready()
        {
            _logger   = GetNodeOrNull<CombatLogger>(CombatLoggerPath);
            _resultUI = GetNodeOrNull<BattleResultUI>(BattleResultUIPath);
            _aiMgr    = GetNodeOrNull<CombatAIManager>(CombatAIManagerPath);
            _selector = GetNodeOrNull<UnitSelector>(UnitSelectorPath);

            // 订阅结算面板关闭信号
            if (_resultUI != null)
                _resultUI.CloseRequested += OnResultClosed;

            // 自动开始倒计时
            BeginCountdown();
        }

        public override void _ExitTree()
        {
            if (GodotObject.IsInstanceValid(_resultUI))
                _resultUI.CloseRequested -= OnResultClosed;
        }

        // ── _Process ────────────────────────────────────────────────
        public override void _Process(double delta)
        {
            float dt = (float)delta;

            switch (State)
            {
                case BattleState.Countdown:
                    UpdateCountdown(dt);
                    break;

                case BattleState.Running:
                    ElapsedSeconds += dt;
                    UpdateWinCheck(dt);
                    break;
            }

            // 延迟显示结算面板
            if (_pendingResult)
            {
                _resultDelayTimer -= dt;
                if (_resultDelayTimer <= 0f)
                {
                    _pendingResult = false;
                    ShowResultPanel();
                }
            }
        }

        // ── 公共接口 ────────────────────────────────────────────────

        /// <summary>注册单位（由 UnitFactory 调用）。</summary>
        public void RegisterUnit(UnitBase unit)
        {
            if (!_allUnits.Contains(unit))
            {
                _allUnits.Add(unit);
                unit.Died += OnUnitDied;
            }
        }

        /// <summary>注销单位。</summary>
        public void UnregisterUnit(UnitBase unit)
        {
            _allUnits.Remove(unit);
            unit.Died -= OnUnitDied;
        }

        /// <summary>暂停/继续战斗。</summary>
        public void SetPaused(bool paused)
        {
            if (State == BattleState.Ended) return;

            if (paused && State == BattleState.Running)
            {
                State = BattleState.Paused;
                GetTree().Paused = true;
                EmitSignal(SignalName.BattlePaused, true);
            }
            else if (!paused && State == BattleState.Paused)
            {
                State = BattleState.Running;
                GetTree().Paused = false;
                EmitSignal(SignalName.BattlePaused, false);
            }
        }

        /// <summary>强制结束战斗（调试/超时用）。</summary>
        public void ForceEnd(int winTeamId = -1)
            => EndBattle(winTeamId);

        /// <summary>获取指定队伍存活单位数量。</summary>
        public int GetAliveCount(int teamId)
        {
            int count = 0;
            foreach (var u in _allUnits)
                if (!u.IsDead && u.TeamId == teamId) count++;
            return count;
        }

        /// <summary>获取所有参战单位（只读）。</summary>
        public IReadOnlyList<UnitBase> AllUnits => _allUnits;

        // ── 内部逻辑 ────────────────────────────────────────────────

        private void BeginCountdown()
        {
            State            = BattleState.Countdown;
            _countdownTimer  = CountdownSeconds;
            EmitSignal(SignalName.CountdownTick, Mathf.CeilToInt(CountdownSeconds));
            GD.Print($"[BattleManager] 战斗倒计时开始：{CountdownSeconds}s");
        }

        private void UpdateCountdown(float dt)
        {
            _countdownTimer -= dt;
            int prev = Mathf.CeilToInt(_countdownTimer + dt);
            int curr = Mathf.CeilToInt(_countdownTimer);
            if (curr < prev && curr >= 0)
                EmitSignal(SignalName.CountdownTick, curr);

            if (_countdownTimer <= 0f)
                StartBattle();
        }

        private void StartBattle()
        {
            State          = BattleState.Running;
            ElapsedSeconds = 0f;
            _winCheckTimer = 0f;
            _logger?.Clear();

            GD.Print("[BattleManager] 战斗开始！");
            EmitSignal(SignalName.BattleStarted);
        }

        private void UpdateWinCheck(float dt)
        {
            _winCheckTimer += dt;
            if (_winCheckTimer < WinCheckInterval) return;
            _winCheckTimer = 0f;

            // 统计各队伍存活数
            var teamAlive = new Dictionary<int, int>(4);
            foreach (var u in _allUnits)
            {
                if (u.IsDead) continue;
                teamAlive.TryGetValue(u.TeamId, out int cnt);
                teamAlive[u.TeamId] = cnt + 1;
            }

            // 只剩一支队伍存活 → 胜利
            if (teamAlive.Count == 1)
            {
                foreach (var kv in teamAlive)
                    EndBattle(kv.Key);
            }
            // 全灭 → 平局
            else if (teamAlive.Count == 0)
            {
                EndBattle(-1);
            }
        }

        private void EndBattle(int winTeamId)
        {
            if (State == BattleState.Ended) return;

            State        = BattleState.Ended;
            WinnerTeamId = winTeamId;

            string result = winTeamId < 0 ? "平局"
                : winTeamId == PlayerTeamId ? "胜利" : "失败";
            GD.Print($"[BattleManager] 战斗结束！结果={result}  用时={ElapsedSeconds:F1}s");

            EmitSignal(SignalName.BattleEnded, winTeamId);

            // 延迟显示结算面板
            _pendingResult    = true;
            _resultDelayTimer = ResultShowDelay;
        }

        private void ShowResultPanel()
        {
            _resultUI?.ShowResult(WinnerTeamId, PlayerTeamId, _allUnits);
        }

        private void OnUnitDied(uint unitId)
        {
            // 单位死亡时立即触发一次胜负检查
            if (State == BattleState.Running)
                _winCheckTimer = WinCheckInterval; // 强制下一帧检查
        }

        private void OnResultClosed()
        {
            // 返回大厅
            GameManager.Instance?.ReturnToLobby();
        }
    }
}
