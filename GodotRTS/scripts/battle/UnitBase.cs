using Godot;
using System;

namespace GodotRTS.Battle
{
    /// <summary>
    /// 所有战斗单位的抽象基类。
    /// 定义 BattleManager、AI 系统与网络快照所需的最小公共接口。
    /// CombatUnit 继承此类并提供完整实现。
    /// </summary>
    public abstract partial class UnitBase : CharacterBody3D
    {
        // ─── 公共属性（由子类实现）────────────────────────────────────────

        /// <summary>所属队伍 ID（0 = 玩家队，1 = 敌方队）。</summary>
        [Export] public int TeamId { get; set; } = 0;

        /// <summary>单位是否存活。</summary>
        public abstract bool IsAlive { get; }
        public virtual bool IsDead => !IsAlive;
        public event Action<uint> Died;
        protected void TriggerDied(uint id) => Died?.Invoke(id);

        // ─── 网络快照接口 ─────────────────────────────────────────────────

        /// <summary>
        /// 序列化单位状态快照，用于 UDP 广播。
        /// 固定 28 字节，零堆分配。
        /// </summary>
        public abstract void WriteSnapshot(Span<byte> buf);
    }
}
