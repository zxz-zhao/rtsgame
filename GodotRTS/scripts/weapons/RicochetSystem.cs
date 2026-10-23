using Godot;
using System.Runtime.CompilerServices;

namespace GodotRTS.Weapons
{
    /// <summary>
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ///  RicochetSystem — 步兵子弹跳弹计算核心
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ///  物理模型说明：
    ///    1. 跳弹概率  = f(入射角, 表面硬度, 子弹速度)
    ///       当入射角 θ < 临界角 θ_crit 时，跳弹概率随 θ 减小而急剧升高。
    ///       θ_crit 由目标表面材质（SurfaceMaterial）决定。
    ///
    ///    2. 反弹速度  = 入射速度 × 能量保留系数 × 随机扰动
    ///       能量保留系数 ε = ε_base × (1 - k_drag × |v|²)
    ///       反弹方向   = Reflect(dir, normal) + RandomCone(maxDeviation)
    ///
    ///    3. 跳弹伤害  = 原始伤害 × 能量保留系数²  (动能 ∝ v²)
    ///
    ///    4. 最多跳弹次数由 RicochetResult.BounceCount 追踪，超过上限后穿透/停止。
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    /// </summary>
    public static class RicochetSystem
    {
        // ─────────────────────────────────────────────────────────────────
        //  表面材质枚举
        // ─────────────────────────────────────────────────────────────────
        public enum SurfaceMaterial
        {
            Soil        = 0,   // 泥土/沙地  — 强吸能，几乎不跳弹
            Concrete    = 1,   // 混凝土     — 中等跳弹
            SteelLight  = 2,   // 轻装甲钢板 — 较易跳弹
            SteelHeavy  = 3,   // 重装甲     — 高角度跳弹
            Rock        = 4,   // 岩石       — 高跳弹率，随机扰动大
            Wood        = 5,   // 木材       — 低跳弹率
            Water       = 6,   // 水面       — 极低角度跳弹（打水漂）
        }

        // ─────────────────────────────────────────────────────────────────
        //  每种材质的物理参数表（只读静态数据，零 GC）
        // ─────────────────────────────────────────────────────────────────
        private static readonly SurfaceParams[] s_SurfaceTable =
        {
            // Soil
            new SurfaceParams(critAngleDeg: 10f, energyRetain: 0.10f, maxDevDeg: 35f, maxBounces: 0),
            // Concrete
            new SurfaceParams(critAngleDeg: 25f, energyRetain: 0.42f, maxDevDeg: 18f, maxBounces: 2),
            // SteelLight
            new SurfaceParams(critAngleDeg: 35f, energyRetain: 0.65f, maxDevDeg: 10f, maxBounces: 3),
            // SteelHeavy
            new SurfaceParams(critAngleDeg: 45f, energyRetain: 0.72f, maxDevDeg:  8f, maxBounces: 4),
            // Rock
            new SurfaceParams(critAngleDeg: 30f, energyRetain: 0.50f, maxDevDeg: 28f, maxBounces: 3),
            // Wood
            new SurfaceParams(critAngleDeg: 12f, energyRetain: 0.20f, maxDevDeg: 22f, maxBounces: 1),
            // Water
            new SurfaceParams(critAngleDeg: 8f,  energyRetain: 0.80f, maxDevDeg:  5f, maxBounces: 5),
        };

        // ─────────────────────────────────────────────────────────────────
        //  跳弹结果（值类型，零堆分配）
        // ─────────────────────────────────────────────────────────────────
        public struct RicochetResult
        {
            /// <summary>是否发生跳弹</summary>
            public bool    DidRicochet;
            /// <summary>反弹后的速度向量（含衰减后的速率）</summary>
            public Vector3 NewVelocity;
            /// <summary>跳弹后的伤害（动能损失后）</summary>
            public float   NewDamage;
            /// <summary>能量保留系数 [0,1]</summary>
            public float   EnergyRetained;
            /// <summary>本次已累计跳弹次数（调用方传入并递增）</summary>
            public int     BounceCount;
        }

        // ─────────────────────────────────────────────────────────────────
        //  主函数：计算一次碰撞是否产生跳弹，并返回跳弹结果
        // ─────────────────────────────────────────────────────────────────
        /// <param name="inVelocity">   子弹入射速度向量（世界空间，单位 m/s）</param>
        /// <param name="surfaceNormal">碰撞点法线（单位向量，朝外）</param>
        /// <param name="inDamage">     子弹当前携带伤害值</param>
        /// <param name="material">     碰撞表面材质</param>
        /// <param name="bounceCount">  已发生的跳弹次数（用于限制上限）</param>
        /// <param name="rng">          调用方传入的随机数生成器（避免 GC）</param>
        public static RicochetResult Evaluate(
            Vector3         inVelocity,
            Vector3         surfaceNormal,
            float           inDamage,
            SurfaceMaterial material,
            int             bounceCount,
            RandomNumberGenerator rng)
        {
            ref readonly SurfaceParams sp = ref s_SurfaceTable[(int)material];

            // ── 1. 超过最大跳弹次数 → 直接停止 ──────────────────────────
            if (bounceCount >= sp.MaxBounces)
                return new RicochetResult { DidRicochet = false };

            // ── 2. 计算入射角 θ（子弹方向与表面法线夹角的余角）─────────
            //    inDir 为单位向量（朝向目标）
            Vector3 inDir     = inVelocity.Normalized();
            float   cosTheta  = Mathf.Abs(inDir.Dot(surfaceNormal));   // cos(θ), θ=入射角
            float   thetaDeg  = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(cosTheta, 0f, 1f)));
            // 注意：θ 是与法线的夹角；掠射角 = 90° - θ
            float   grazeDeg  = 90f - thetaDeg;

            // ── 3. 跳弹概率 p = Sigmoid 曲线，掠射角越小概率越高 ─────────
            //    p = 1 / (1 + exp( k × (grazeDeg - θ_crit) ))
            //    k 控制曲线陡峭程度（值越大，临界角附近概率变化越剧烈）
            const float k      = 0.35f;
            float       p      = 1f / (1f + Mathf.Exp(k * (grazeDeg - sp.CritAngleDeg)));

            // ── 4. 速度影响：高速子弹穿透力更强，跳弹概率略降 ───────────
            //    speedFactor = clamp(1 - (|v| - 300) / 700, 0.2, 1.0)
            //    即：子弹速度 300 m/s 以下不衰减，超出部分线性降低概率
            float speed       = inVelocity.Length();
            float speedFactor = Mathf.Clamp(1f - (speed - 300f) / 700f, 0.2f, 1.0f);
            p *= speedFactor;

            // ── 5. 掷骰子决定是否跳弹 ───────────────────────────────────
            float roll = rng.Randf();
            if (roll > p)
                return new RicochetResult { DidRicochet = false };

            // ── 6. 计算反弹方向（物理反射 + 随机锥形扰动）───────────────
            Vector3 reflected = ReflectAndPerturb(
                inDir, surfaceNormal, sp.MaxDeviationDeg, rng);

            // ── 7. 计算能量保留（高速子弹能量损失更大）──────────────────
            //    ε = ε_base × exp(-λ × bounceCount)  指数衰减：每次跳弹额外损耗
            const float lambda  = 0.25f;
            float       epsilon = sp.EnergyRetain * Mathf.Exp(-lambda * bounceCount);
            epsilon = Mathf.Clamp(epsilon, 0f, 1f);

            // ── 8. 新速度 & 新伤害 ──────────────────────────────────────
            Vector3 newVel    = reflected * (speed * epsilon);
            float   newDamage = inDamage  * (epsilon * epsilon);   // 动能 ∝ v²

            return new RicochetResult
            {
                DidRicochet    = true,
                NewVelocity    = newVel,
                NewDamage      = newDamage,
                EnergyRetained = epsilon,
                BounceCount    = bounceCount + 1,
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  辅助：物理反射 + 随机锥形扰动（零 GC）
        // ─────────────────────────────────────────────────────────────────
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ReflectAndPerturb(
            Vector3 inDir,
            Vector3 normal,
            float   maxDevDeg,
            RandomNumberGenerator rng)
        {
            // 标准物理反射：r = d - 2(d·n)n
            Vector3 reflected = inDir - 2f * inDir.Dot(normal) * normal;
            reflected = reflected.Normalized();

            // 随机锥形扰动（在反射向量周围的圆锥内均匀采样）
            float   maxDevRad = Mathf.DegToRad(maxDevDeg);
            float   phi       = rng.Randf() * Mathf.Tau;                  // 方位角 [0, 2π]
            float   cosMax    = Mathf.Cos(maxDevRad);
            float   cosTheta  = 1f - rng.Randf() * (1f - cosMax);         // 均匀球面采样
            float   sinTheta  = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));

            // 在反射向量的局部坐标系中构建扰动向量
            Vector3 tangent, bitangent;
            BuildOrthonormalBasis(reflected, out tangent, out bitangent);

            Vector3 perturbed = reflected * cosTheta
                              + tangent   * (sinTheta * Mathf.Cos(phi))
                              + bitangent * (sinTheta * Mathf.Sin(phi));

            // 确保扰动后的方向不会穿入表面
            if (perturbed.Dot(normal) < 0f)
                perturbed = reflected;   // 退化为纯反射

            return perturbed.Normalized();
        }

        // ─────────────────────────────────────────────────────────────────
        //  辅助：构建正交基（无 GC，无 new）
        // ─────────────────────────────────────────────────────────────────
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BuildOrthonormalBasis(
            Vector3 n, out Vector3 t, out Vector3 b)
        {
            // Frisvad / Duff-Wang 方法，避免 |n.z| ≈ 1 时的数值不稳定
            if (n.Z < -0.9999805f)
            {
                t = new Vector3( 0f, -1f, 0f);
                b = new Vector3(-1f,  0f, 0f);
                return;
            }
            float a =  1f / (1f + n.Z);
            float c = -n.X * n.Y * a;
            t = new Vector3(1f - n.X * n.X * a,  c, -n.X);
            b = new Vector3(c, 1f - n.Y * n.Y * a, -n.Y);
        }

        // ─────────────────────────────────────────────────────────────────
        //  内部：表面参数结构体（只读值类型，栈上分配）
        // ─────────────────────────────────────────────────────────────────
        private readonly struct SurfaceParams
        {
            public readonly float CritAngleDeg;    // 临界掠射角（°）
            public readonly float EnergyRetain;    // 基础能量保留系数
            public readonly float MaxDeviationDeg; // 最大随机扰动角（°）
            public readonly int   MaxBounces;       // 最大跳弹次数

            public SurfaceParams(
                float critAngleDeg,
                float energyRetain,
                float maxDevDeg,
                int   maxBounces)
            {
                CritAngleDeg    = critAngleDeg;
                EnergyRetain    = energyRetain;
                MaxDeviationDeg = maxDevDeg;
                MaxBounces      = maxBounces;
            }
        }
    }
}
