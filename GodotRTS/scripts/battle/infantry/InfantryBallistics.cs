// =============================================================
// InfantryBallistics.cs
// 步兵弹道物理库 — 跳弹判定 & 反射弹道计算
// GodotRTS · 核心战斗系统
// =============================================================
using Godot;
using System.Runtime.CompilerServices;

namespace GodotRTS.Battle.Infantry
{
    /// <summary>
    /// 表面材质硬度预设（影响跳弹概率与能量保留率）
    /// </summary>
    public enum SurfaceMaterial : byte
    {
        SoftSoil    = 0,   // 软土/泥地     H ≈ 0.00
        Wood        = 1,   // 木材/木板     H ≈ 0.40
        Sandbag     = 2,   // 沙袋           H ≈ 0.20
        Concrete    = 3,   // 混凝土/石材   H ≈ 0.75
        Steel       = 4,   // 钢铁/装甲板   H ≈ 1.00
        Brick       = 5,   // 砖墙           H ≈ 0.60
    }

    /// <summary>
    /// 跳弹计算结果（值类型，零堆分配）
    /// </summary>
    public readonly struct RicochetResult
    {
        /// <summary>是否发生跳弹</summary>
        public readonly bool DidRicochet;

        /// <summary>跳弹后的世界空间方向向量（已归一化）</summary>
        public readonly Vector3 ReflectedDirection;

        /// <summary>残余速度标量（m/s）</summary>
        public readonly float ResidualSpeed;

        /// <summary>残余动能比率 [0, 1]</summary>
        public readonly float EnergyRetention;

        /// <summary>入射掠射角（弧度），0 = 垂直入射，π/2 = 平行掠射</summary>
        public readonly float GrazingAngleRad;

        public RicochetResult(
            bool didRicochet,
            Vector3 reflectedDirection,
            float residualSpeed,
            float energyRetention,
            float grazingAngleRad)
        {
            DidRicochet        = didRicochet;
            ReflectedDirection = reflectedDirection;
            ResidualSpeed      = residualSpeed;
            EnergyRetention    = energyRetention;
            GrazingAngleRad    = grazingAngleRad;
        }

        public static readonly RicochetResult NoRicochet = new RicochetResult(
            false, Vector3.Zero, 0f, 0f, 0f);
    }

    /// <summary>
    /// 步兵弹道物理静态库
    /// ─────────────────────────────────────────────────────────
    /// • 跳弹判定：入射角 + 材质硬度 + 弹速 三因子模型
    /// • 反射弹道：镜面反射 + 高斯散射锥（伪随机，无堆分配）
    /// • 残余动能：能量守恒衰减公式
    /// ─────────────────────────────────────────────────────────
    /// 所有方法为纯函数，线程安全，PhysicsProcess 可安全调用。
    /// </summary>
    public static class InfantryBallistics
    {
        // ── 材质硬度查找表（索引对应 SurfaceMaterial 枚举值）──
        private static readonly float[] s_hardness = { 0.00f, 0.40f, 0.20f, 0.75f, 1.00f, 0.60f };

        // ── 物理常数 ──
        /// <summary>强制跳弹的临界掠射角（弧度）≈ 15°</summary>
        public const float ForcedRicochetAngleRad = 0.2618f;

        /// <summary>跳弹散射锥半角（弧度）≈ 8°</summary>
        public const float RicochetConeHalfAngleRad = 0.1396f;

        /// <summary>速度阈值：低于此值弹丸无法产生跳弹（m/s）</summary>
        public const float MinRicochetSpeed = 150f;

        /// <summary>能量保留指数 k（cos^k 曲线陡峭度）</summary>
        public const float EnergyExponent = 1.8f;

        // =============================================================
        // 公开 API
        // =============================================================

        /// <summary>
        /// 计算弹丸击中表面时的跳弹结果。
        /// </summary>
        /// <param name="incidentDir">入射方向（世界空间，已归一化）</param>
        /// <param name="surfaceNormal">表面法线（世界空间，已归一化，指向弹丸来源侧）</param>
        /// <param name="projectileSpeed">弹丸速度（m/s）</param>
        /// <param name="material">表面材质</param>
        /// <param name="randomSeed">外部传入随机种子（帧号 ^ 单位ID，避免全局状态竞争）</param>
        /// <returns>跳弹结果（值类型，零堆分配）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RicochetResult EvaluateRicochet(
            Vector3 incidentDir,
            Vector3 surfaceNormal,
            float   projectileSpeed,
            SurfaceMaterial material,
            uint    randomSeed = 0u)
        {
            // ── 速度门控：过慢的弹丸不产生跳弹 ──
            if (projectileSpeed < MinRicochetSpeed)
                return RicochetResult.NoRicochet;

            // ── 计算掠射角（入射方向与表面切平面的夹角）──
            // dot(incidentDir, -normal) = cos(入射角)，入射角 0 = 垂直入射
            // 掠射角 = π/2 - 入射角
            float cosIncident  = -incidentDir.Dot(surfaceNormal);
            cosIncident        = Mathf.Clamp(cosIncident, 0f, 1f);
            float grazingAngle = Mathf.Pi * 0.5f - Mathf.Acos(cosIncident);

            // ── 强制跳弹区：掠射角极小（子弹几乎平行表面飞过）──
            bool forcedRicochet = grazingAngle < ForcedRicochetAngleRad;

            // ── 概率跳弹区：三因子联合概率 ──
            float ricochetProb = 0f;
            if (!forcedRicochet)
            {
                float hardness    = GetHardness(material);
                float angleFactor = 1f - (grazingAngle / (Mathf.Pi * 0.5f));
                float speedFactor = Mathf.Clamp(projectileSpeed / 900f, 0f, 1f);
                ricochetProb      = hardness * angleFactor * angleFactor * speedFactor;
            }

            // ── 随机判定 ──
            float randVal    = NextFloat(randomSeed ^ (uint)(projectileSpeed * 7919f));
            bool  doRicochet = forcedRicochet || (randVal < ricochetProb);

            if (!doRicochet)
                return RicochetResult.NoRicochet;

            // ── 计算镜面反射方向 R = I - 2·(I·N)·N ──
            Vector3 reflected = incidentDir - 2f * incidentDir.Dot(surfaceNormal) * surfaceNormal;
            reflected = reflected.Normalized();

            // ── 叠加散射锥（模拟弹头变形与表面粗糙度）──
            reflected = ApplyConeScatter(reflected, surfaceNormal, RicochetConeHalfAngleRad, randomSeed + 1u);

            // ── 残余动能计算 ──
            float hardnessFactor  = GetHardness(material);
            float energyRetention = hardnessFactor * Mathf.Pow(Mathf.Cos(grazingAngle), EnergyExponent);
            energyRetention       = Mathf.Clamp(energyRetention, 0.05f, 0.92f);
            float residualSpeed   = projectileSpeed * Mathf.Sqrt(energyRetention);

            return new RicochetResult(
                didRicochet:        true,
                reflectedDirection: reflected,
                residualSpeed:      residualSpeed,
                energyRetention:    energyRetention,
                grazingAngleRad:    grazingAngle);
        }

        /// <summary>
        /// 根据跳弹结果计算对目标的最终伤害乘数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRicochetDamageMultiplier(in RicochetResult result)
        {
            if (!result.DidRicochet) return 1f;
            return result.EnergyRetention;
        }

        // =============================================================
        // 私有工具方法
        // =============================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetHardness(SurfaceMaterial mat)
            => s_hardness[(int)mat];

        /// <summary>
        /// 在方向向量周围施加随机散射锥（无堆分配，Xorshift 伪随机）。
        /// </summary>
        private static Vector3 ApplyConeScatter(
            Vector3 dir, Vector3 surfaceNormal, float halfAngle, uint seed)
        {
            float r1 = NextFloat(seed);
            float r2 = NextFloat(seed + 997u);

            float cosHalf  = Mathf.Cos(halfAngle);
            float cosTheta = 1f - r1 * (1f - cosHalf);
            float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
            float phi      = r2 * Mathf.Tau;

            Vector3 up      = Mathf.Abs(dir.Dot(Vector3.Up)) < 0.99f ? Vector3.Up : Vector3.Right;
            Vector3 tangent = dir.Cross(up).Normalized();
            Vector3 bitang  = dir.Cross(tangent);

            Vector3 scattered = sinTheta * Mathf.Cos(phi) * tangent
                              + sinTheta * Mathf.Sin(phi) * bitang
                              + cosTheta * dir;
            scattered = scattered.Normalized();

            if (scattered.Dot(surfaceNormal) < 0.01f)
                scattered = (scattered - 2f * scattered.Dot(surfaceNormal) * surfaceNormal).Normalized();

            return scattered;
        }

        /// <summary>Xorshift32 伪随机浮点数 [0, 1)，无堆分配。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float NextFloat(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0x007FFFFFu) * (1f / 8388608f);
        }
    }
}
