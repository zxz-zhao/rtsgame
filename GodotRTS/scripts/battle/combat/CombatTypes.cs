using Godot;

namespace GodotRTS.Battle.Combat
{
    // ══════════════════════════════════════════════════════════════════════
    // 战斗动作类型
    // ══════════════════════════════════════════════════════════════════════
    public enum CombatActionType
    {
        Idle,
        MeleeAttack,
        RangedAttack,
        CastSkill,
        TakeDamage,
        Die,
        Revive,
    }

    // ══════════════════════════════════════════════════════════════════════
    // 特效类型
    // ══════════════════════════════════════════════════════════════════════
    public enum VFXType
    {
        None,
        MeleeHit,
        RangedHit,
        BloodSplash,
        SpellImpact,
        AoeBlast,
        DeathExplosion,
        HealAura,
        ShieldBlock,
        ProjectileArrow,
        ProjectileFireball,
        LevelUp,
        StatusPoison,
        StatusBurn,
        StatusFreeze,
    }

    // ══════════════════════════════════════════════════════════════════════
    // 伤害类型
    // ══════════════════════════════════════════════════════════════════════
    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Holy,
        True,   // 真实伤害，无视防御
    }

    // ══════════════════════════════════════════════════════════════════════
    // 状态效果类型
    // ══════════════════════════════════════════════════════════════════════
    public enum StatusEffectType
    {
        None,
        Poison,
        Burn,
        Freeze,
        Stun,
        Slow,
        Haste,
        Shield,
        Regen,
    }

    // ══════════════════════════════════════════════════════════════════════
    // 战斗动作事件（值类型，避免GC）
    // ══════════════════════════════════════════════════════════════════════
    public readonly struct CombatActionEvent
    {
        public readonly uint             AttackerId;
        public readonly uint             TargetId;
        public readonly CombatActionType ActionType;
        public readonly int              SkillId;
        public readonly float            Damage;
        public readonly bool             IsCritical;
        public readonly DamageType       DmgType;
        public readonly Vector3          HitPosition;
        public readonly long             FrameIndex;

        public CombatActionEvent(
            uint attackerId, uint targetId,
            CombatActionType actionType,
            int skillId, float damage,
            bool isCritical, DamageType dmgType,
            Vector3 hitPosition, long frameIndex)
        {
            AttackerId  = attackerId;
            TargetId    = targetId;
            ActionType  = actionType;
            SkillId     = skillId;
            Damage      = damage;
            IsCritical  = isCritical;
            DmgType     = dmgType;
            HitPosition = hitPosition;
            FrameIndex  = frameIndex;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // AOE 命中事件
    // ══════════════════════════════════════════════════════════════════════
    public readonly struct AoeHitEvent
    {
        public readonly int     SkillId;
        public readonly uint    CasterId;
        public readonly uint    TargetId;
        public readonly float   Damage;
        public readonly bool    IsCritical;
        public readonly Vector3 HitPosition;

        public AoeHitEvent(int skillId, uint casterId, uint targetId,
                           float damage, bool isCritical, Vector3 hitPos)
        {
            SkillId     = skillId;
            CasterId    = casterId;
            TargetId    = targetId;
            Damage      = damage;
            IsCritical  = isCritical;
            HitPosition = hitPos;
        }
    }
}
