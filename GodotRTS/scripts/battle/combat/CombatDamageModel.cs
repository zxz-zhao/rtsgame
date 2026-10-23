using Godot;
using System;

namespace GodotRTS.Battle.Combat
{
    /// <summary>
    /// 伤害计算判定结果（纯值类型，Zero-GC）
    /// </summary>
    public readonly struct DamageCalculationResult
    {
        public readonly float FinalDamage;
        public readonly float RawDamage;
        public readonly bool IsCritical;
        public readonly bool IsRearHit;
        public readonly bool IsRicochet;
        public readonly HitDirection Direction;
        public readonly float EffectiveArmor;
        public readonly float MitigationRatio;

        public DamageCalculationResult(
            float finalDamage,
            float rawDamage,
            bool isCritical,
            bool isRearHit,
            bool isRicochet,
            HitDirection direction,
            float effectiveArmor,
            float mitigationRatio)
        {
            FinalDamage = finalDamage;
            RawDamage = rawDamage;
            IsCritical = isCritical;
            IsRearHit = isRearHit;
            IsRicochet = isRicochet;
            Direction = direction;
            EffectiveArmor = effectiveArmor;
            MitigationRatio = mitigationRatio;
        }
    }

    /// <summary>
    /// 全局战术战斗伤害与装甲穿透核心计算模型
    /// 支持：兵种克制倍率、方位装甲（正/侧/背击）、穿甲对抗与跳弹、暴击加成、建筑穿透削减
    /// </summary>
    public static class CombatDamageModel
    {
        // 伪随机种子驱动暴击判定（免实例化 Random 触发 GC）
        static uint rngState = 123456789;

        static float NextFloat()
        {
            rngState = (rngState * 1664525u + 1013904223u);
            return (rngState & 0x00FFFFFF) / (float)0x01000000;
        }

        /// <summary>
        /// 综合计算攻击伤害与受击防御状态
        /// </summary>
        public static DamageCalculationResult CalculateDamage(
            string attackerKey,
            string targetKey,
            float rawDamage,
            Vector3 attackerPosition,
            Vector3 targetPosition,
            Vector3 targetForward,
            bool isBuilding = false)
        {
            // 1. 基础兵种克制与类型倍率
            float counterMultiplier = isBuilding
                ? BattleUnitCatalog.GetDamageMultiplierToBuilding(attackerKey)
                : BattleUnitCatalog.GetDamageMultiplier(attackerKey, targetKey);

            if (counterMultiplier <= 0.0001f || rawDamage <= 0.0001f)
            {
                return new DamageCalculationResult(0f, rawDamage, false, false, false, HitDirection.Front, 0f, 1f);
            }

            // 2. 方位装甲与入射角判定（方位向量在世界水平面上的点积）
            Vector3 incoming = attackerPosition - targetPosition;
            incoming.Y = 0f;
            if (incoming.LengthSquared() > 0.001f)
            {
                incoming = incoming.Normalized();
            }
            else
            {
                incoming = -targetForward; // 默认迎面
            }

            Vector3 forward = targetForward;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.001f)
            {
                forward = forward.Normalized();
            }
            else
            {
                forward = Vector3.Forward;
            }

            // 点积分析：
            // incoming 是从 target 指向 attacker 的向量
            // 若 attacker 在 target 正前方，incoming 与 forward 方向相同，dot 趋近 +1
            // 若 attacker 在 target 正后方，incoming 与 forward 反向，dot 趋近 -1
            float dot = forward.Dot(incoming);

            HitDirection direction;
            float directionalDamageBonus;
            float armorModifier;

            if (isBuilding)
            {
                // 建筑四周均为稳固要塞装甲，无侧背弱点
                direction = HitDirection.Front;
                directionalDamageBonus = 1.0f;
                armorModifier = 1.0f;
            }
            else if (dot < -0.38f)
            {
                // 背部弱点：发动机/散热栅/弹药架暴露
                direction = HitDirection.Rear;
                directionalDamageBonus = 1.35f; // +35% 穿透破坏力
                armorModifier = 0.50f;         // 背部装甲减半！
            }
            else if (dot > 0.38f)
            {
                // 正面交火：主装甲迎敌，倾斜防弹外形
                direction = HitDirection.Front;
                directionalDamageBonus = 1.00f;
                armorModifier = 1.25f;         // 正面首上装甲跳弹防御加成 +25%
            }
            else
            {
                // 侧翼包抄：侧履带与侧板
                direction = HitDirection.Flank;
                directionalDamageBonus = 1.15f; // +15% 侧面破坏力
                armorModifier = 0.85f;         // 侧面装甲略弱
            }

            // 3. 装甲防御与穿甲对抗
            float targetBaseArmor = BattleUnitCatalog.GetArmorRating(targetKey);
            float effectiveTargetArmor = targetBaseArmor * armorModifier;
            float attackerPenetration = BattleUnitCatalog.GetArmorPenetration(attackerKey);

            // 4. 正面跳弹检测 (Ricochet / Deflection)
            // 当目标为装甲载具或要塞，从正面受击，且武器穿深不足正面厚装甲的 35% 时触发跳弹
            bool isRicochet = false;
            if (!isBuilding && direction == HitDirection.Front && effectiveTargetArmor >= 35f && attackerPenetration < effectiveTargetArmor * 0.35f)
            {
                isRicochet = true;
                // 跳弹仅造成 15% 擦伤与跳弹火星
                float scratchDamage = Mathf.Max(1f, rawDamage * 0.15f * counterMultiplier);
                return new DamageCalculationResult(scratchDamage, rawDamage, false, false, true, HitDirection.Front, effectiveTargetArmor, 0.85f);
            }

            // 5. 等效装甲减伤曲线：
            // EffectiveArmor = Max(0, Armor - Pen)
            // Mitigation = EffectiveArmor / (EffectiveArmor + 75)
            float netArmor = Mathf.Max(0f, effectiveTargetArmor - attackerPenetration);
            float mitigationRatio = netArmor / (netArmor + 75f);
            float postArmorDamage = rawDamage * (1f - mitigationRatio) * directionalDamageBonus * counterMultiplier;

            // 6. 暴击判定 (Critical Strike)
            float baseCrit = BattleUnitCatalog.GetCritChance(attackerKey);
            if (direction == HitDirection.Rear)
            {
                baseCrit += 0.12f; // 背击增加 12% 致命弱点暴击率
            }

            bool isCritical = NextFloat() < baseCrit;
            if (isCritical)
            {
                postArmorDamage *= 1.55f; // 暴击 1.55 倍
            }

            float finalDamage = Mathf.Max(1f, postArmorDamage);

            return new DamageCalculationResult(
                finalDamage,
                rawDamage,
                isCritical,
                direction == HitDirection.Rear,
                false,
                direction,
                netArmor,
                mitigationRatio);
        }
    }
}
