using Godot;
using System;
using System.Collections.Generic;
using GodotRTS.Battle.Unit;

namespace GodotRTS.Core
{
    public sealed class BattleStartConfig
    {
        public bool IsOffline { get; set; } = true;
        public string ServerIp { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 9999;
        public int PlayerTeamId { get; set; } = 0;
    }

    public sealed class GameManager
    {
        public static GameManager Instance { get; } = new();
        public BattleStartConfig PendingBattleConfig { get; set; } = new();
        public void ReturnToLobby()
        {
            GD.Print("[GameManager] Return to lobby requested.");
        }
    }
}

namespace GodotRTS.Battle.Combat
{
    public partial class CombatLogger : Node
    {
        public void Clear() {}
    }

    public partial class BattleResultUI : Control
    {
        public event Action CloseRequested;
        public void ShowResult(int winTeamId, int playerTeamId, IReadOnlyList<UnitBase> allUnits)
        {
            GD.Print($"[BattleResultUI] Winner={winTeamId}, Player={playerTeamId}, Units={allUnits?.Count ?? 0}");
        }
        public void RequestClose() => CloseRequested?.Invoke();
    }

    public partial class CombatAIManager : Node {}

    public partial class CombatActionSystem : Node {}

    public partial class AoeSkillSystem : Node
    {
        public delegate void AoeHitUnitHandler(int skillId, int casterId, int targetId, int dmg, bool isCrit);
        public event AoeHitUnitHandler AoeHitUnit;
    }

    public partial class StatusEffectSystem : Node {}

    public partial class ThreatSystem : Node {}

    public partial class ScreenShakeController : Node
    {
        public void OnAoeSkill(int intensity) {}
    }

    public partial class CombatSoundBank : Node {}

    public partial class CombatReplayRecorder : Node
    {
        public void StartRecording() {}
        public void StopRecording() {}
        public void SaveReplay() {}
    }

    public partial class WaveSpawner : Node
    {
        public delegate void WaveStartedHandler(int wave, int total);
        public delegate void AllWavesClearedHandler();
        public event WaveStartedHandler WaveStarted;
        public event AllWavesClearedHandler AllWavesCleared;
    }

    public partial class LootDropSystem : Node {}
}

namespace GodotRTS.Battle
{
    public partial class UnitSelector : Node {}

    public partial class BattleEventBus : Node
    {
        public void PublishBattleStarted() {}
        public void PublishBattleEnded(int winTeamId) {}
        public void PublishWaveStarted(int waveIndex, int totalWaves) {}
        public void PublishAllWavesCleared() {}
    }
}

namespace GodotRTS.Battle.Network
{
    public partial class UnitSyncReceiver : Node {}
}
