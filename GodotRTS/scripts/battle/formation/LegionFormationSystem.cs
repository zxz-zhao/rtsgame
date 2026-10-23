using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GodotRTS.Battle.Formation
{
    public enum FormationLayout
    {
        BattleLine,    // 战列线阵：前排重装推进，中排步兵伴随掩护，后排火炮纵深支援
        Wedge,         // 突击楔形阵：尖刀突破，重型战车居前穿插
        Column,        // 双路纵队行军阵：行军转移与通过狭窄隘口
        PerimeterBox   // 全向方阵：环形警戒，全方位防卫核心支援部队
    }

    /// <summary>
    /// 成体系军团协同作战阵型与战术分配系统
    /// 提供：战术梯次排布（重装/伴随/火炮）、军团智能集火与防过度杀伤分流、行军编队朝向保持
    /// </summary>
    public static class LegionFormationSystem
    {
        /// <summary>
        /// 为所选单位计算军团阵型中的目标世界坐标
        /// </summary>
        public static Vector3 CalculateFormationOffset(
            int unitIndex,
            int totalCount,
            string unitKey,
            Vector3 marchHeading,
            FormationLayout layout = FormationLayout.BattleLine)
        {
            if (totalCount <= 1)
                return Vector3.Zero;

            Vector3 forward = marchHeading;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.01f)
            {
                forward = forward.Normalized();
            }
            else
            {
                forward = Vector3.Forward;
            }

            Vector3 right = Vector3.Up.Cross(forward).Normalized();

            switch (layout)
            {
                case FormationLayout.Wedge:
                    return CalculateWedgeOffset(unitIndex, totalCount, forward, right);

                case FormationLayout.Column:
                    return CalculateColumnOffset(unitIndex, totalCount, forward, right);

                case FormationLayout.PerimeterBox:
                    return CalculatePerimeterBoxOffset(unitIndex, totalCount, forward, right);

                case FormationLayout.BattleLine:
                default:
                    return CalculateBattleLineOffset(unitIndex, totalCount, unitKey, forward, right);
            }
        }

        static Vector3 CalculateBattleLineOffset(int index, int count, string unitKey, Vector3 forward, Vector3 right)
        {
            int tier = BattleUnitCatalog.GetFormationTier(unitKey);
            // tier: 0 = 重装前锋, 1 = 伴随步兵/防空, 2 = 纵深火炮

            float longitudinalOffset = tier switch
            {
                0 => 2.8f,    // 前沿锋线
                1 => 0.0f,    // 战术中线
                2 => -5.8f,   // 纵深后线
                _ => 0.0f
            };

            // 宽幅横向分布：根据总数量动态计算横向队列
            int columns = Mathf.Min(8, Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(count * 1.5f))));
            int col = index % columns;
            int row = index / columns;

            float spacing = BattleUnitCatalog.IsInfantryLike(unitKey) ? 2.6f : 3.8f;
            float lateralOffset = (col - (columns - 1) * 0.5f) * spacing;
            float rowOffset = row * -3.2f;

            return right * lateralOffset + forward * (longitudinalOffset + rowOffset);
        }

        static Vector3 CalculateWedgeOffset(int index, int count, Vector3 forward, Vector3 right)
        {
            if (index == 0)
                return forward * 3.5f; // 箭头尖兵

            int pairIndex = (index - 1) / 2 + 1;
            bool isLeft = (index % 2) != 0;

            float lateralDist = pairIndex * 2.8f * (isLeft ? -1f : 1f);
            float forwardDist = 3.5f - pairIndex * 2.6f;

            return right * lateralDist + forward * forwardDist;
        }

        static Vector3 CalculateColumnOffset(int index, int count, Vector3 forward, Vector3 right)
        {
            int lane = index % 2; // 左右双列
            int rank = index / 2; // 前后纵深行

            float lateralDist = (lane == 0 ? -1.8f : 1.8f);
            float forwardDist = -rank * 3.6f;

            return right * lateralDist + forward * forwardDist;
        }

        static Vector3 CalculatePerimeterBoxOffset(int index, int count, Vector3 forward, Vector3 right)
        {
            float radius = Mathf.Clamp(count * 0.85f, 3.5f, 18f);
            float angle = (index / (float)count) * Mathf.Tau;

            float x = Mathf.Sin(angle) * radius;
            float z = Mathf.Cos(angle) * radius;

            return right * x + forward * z;
        }

        /// <summary>
        /// 军团智能集火与火力分流 (Anti-Overkill Focus Fire Distribution)
        /// 避免全军团 20 辆重坦同时轰击 1 名残血残兵，而是智能分配火力至主目标及周围伴随敌军
        /// </summary>
        public static void DistributeLegionAttackTargets(
            List<RtsUnit> attackers,
            Node3D primaryTarget,
            SceneTree tree)
        {
            if (attackers.Count == 0 || !GodotObject.IsInstanceValid(primaryTarget))
                return;

            // 1. 若攻击者少于 4 个单位，全员直接集火主目标
            if (attackers.Count <= 3)
            {
                float arc = Mathf.Clamp((attackers.Count - 1) * 0.25f, 0.3f, 1.8f);
                for (int i = 0; i < attackers.Count; i++)
                {
                    float slot = attackers.Count > 1 ? ((float)i / (attackers.Count - 1) - 0.5f) * arc : 0f;
                    attackers[i].Attack(primaryTarget, slot);
                    GameRelay.Instance?.SendAttack(attackers[i].NetId, ResolveNetId(primaryTarget), primaryTarget.IsInGroup("rts_buildings"));
                }
                return;
            }

            // 2. 评估主目标剩余生命值与所需歼灭火力
            float targetHp = 200f;
            if (primaryTarget is RtsUnit u && GodotObject.IsInstanceValid(u))
            {
                targetHp = u.Health + u.Shield;
            }
            else if (primaryTarget is RtsBuilding b && GodotObject.IsInstanceValid(b))
            {
                targetHp = b.Health;
            }

            // 3. 收集主目标周围 24 米范围内的其他活体敌军目标
            var nearbyEnemies = new List<Node3D>();
            var targetPos = primaryTarget.GlobalPosition;
            var currentScene = tree.CurrentScene;

            if (currentScene is not null)
            {
                foreach (var enemyNode in currentScene.GetTree().GetNodesInGroup("enemy_owned"))
                {
                    if (enemyNode is Node3D enemy3D && GodotObject.IsInstanceValid(enemy3D) && enemy3D != primaryTarget)
                    {
                        if (enemy3D is RtsUnit enemyUnit && (enemyUnit.IsDead || enemyUnit.Health <= 0f))
                            continue;
                        if (enemy3D is RtsBuilding enemyBldg && enemyBldg.Health <= 0f)
                            continue;

                        if (enemy3D.GlobalPosition.DistanceSquaredTo(targetPos) <= 24f * 24f)
                        {
                            nearbyEnemies.Add(enemy3D);
                        }
                    }
                }
            }

            // 4. 按伤害配额分配主目标攻击者（确保 1.35x 杀伤配额即可，其余单位火力分流）
            float accumulatedDamage = 0f;
            var primarySquad = new List<RtsUnit>();
            var secondarySquad = new List<RtsUnit>();

            for (int i = 0; i < attackers.Count; i++)
            {
                var att = attackers[i];
                if (accumulatedDamage < targetHp * 1.35f || primarySquad.Count < 2 || nearbyEnemies.Count == 0)
                {
                    primarySquad.Add(att);
                    accumulatedDamage += att.AttackDamage;
                }
                else
                {
                    secondarySquad.Add(att);
                }
            }

            // 5. 命令主突击组呈环抱扇形压制主目标
            float primaryArc = Mathf.Clamp((primarySquad.Count - 1) * 0.22f, 0.35f, 2.1f);
            for (int i = 0; i < primarySquad.Count; i++)
            {
                float slot = primarySquad.Count > 1 ? ((float)i / (primarySquad.Count - 1) - 0.5f) * primaryArc : 0f;
                primarySquad[i].Attack(primaryTarget, slot);
                GameRelay.Instance?.SendAttack(primarySquad[i].NetId, ResolveNetId(primaryTarget), primaryTarget.IsInGroup("rts_buildings"));
            }

            // 6. 命令副突击组分流压制周围次要敌军威胁
            if (secondarySquad.Count > 0 && nearbyEnemies.Count > 0)
            {
                for (int i = 0; i < secondarySquad.Count; i++)
                {
                    var targetEnemy = nearbyEnemies[i % nearbyEnemies.Count];
                    secondarySquad[i].Attack(targetEnemy, 0f);
                    GameRelay.Instance?.SendAttack(secondarySquad[i].NetId, ResolveNetId(targetEnemy), targetEnemy.IsInGroup("rts_buildings"));
                }
            }
            else if (secondarySquad.Count > 0)
            {
                // 若周围无其他敌人，全员作为外圈包夹支援
                for (int i = 0; i < secondarySquad.Count; i++)
                {
                    float slot = ((float)(i + primarySquad.Count) / attackers.Count - 0.5f) * primaryArc;
                    secondarySquad[i].Attack(primaryTarget, slot);
                    GameRelay.Instance?.SendAttack(secondarySquad[i].NetId, ResolveNetId(primaryTarget), primaryTarget.IsInGroup("rts_buildings"));
                }
            }
        }

        static int ResolveNetId(Node node)
            => node switch
            {
                RtsUnit unit => unit.NetId,
                RtsBuilding building => building.NetId,
                _ => 0
            };
    }
}
