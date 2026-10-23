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
    // 装甲类型
    // ══════════════════════════════════════════════════════════════════════
    public enum ArmorType
    {
        Unarmored,   // 无装甲：步兵、轻型载具人员（易受轻武器、破片、火焰克制）
        Light,       // 轻装甲：轻型坦克、防空车、侦察机、巡逻艇
        Medium,      // 中型装甲：中型坦克、自行火炮、驱逐舰
        Heavy,       // 重装甲：重型坦克、攻击潜艇、运输舰
        Fortified    // 要塞装甲：战列舰、航空母舰、主基地、防卫防御塔
    }

    // ══════════════════════════════════════════════════════════════════════
    // 命中方位
    // ══════════════════════════════════════════════════════════════════════
    public enum HitDirection
    {
        Front, // 正面迎敌：装甲倾斜跳弹防御加成 (+25% 等效装甲)
        Flank, // 侧翼包抄：常规等效装甲，+15% 侧面破坏力
        Rear   // 背部弱点：发动机与弹药架薄弱点，装甲减半，+35% 伤害，暴击率翻倍
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
