using Godot;
using System.Runtime.CompilerServices;

namespace GodotRTS.Weapons
{
    /// <summary>
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ///  RecoilSystem — 步兵武器后坐力积累 & 衰减核心
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ///  物理模型（弹簧-阻尼 Verlet 积分）：
    ///
    ///    每物理帧（dt ≈ 1/60 s）：
    ///      accel    = −spring × pos
    ///      vel      = vel × (1 − damping × dt) + accel × dt
    ///      pos     += vel × dt
    ///
    ///    稳定性条件：spring < 2 / dt²
    ///    60Hz 物理帧：spring < 7200  ✅（实用值 10~28，远低于上限）
    ///
    ///    散布半角（°）：
    ///      spreadDeg = BaseSpread
    ///                + |Pitch| × PitchToSpread
    ///                + |Yaw|   × YawToSpread
    ///
    ///  设计目标：
    ///    • 零 GC：全值类型，无堆分配，可嵌入 InfantryUnit 栈帧。
    ///    • 物理真实：弹簧-阻尼，连发积累，松开扳机后平滑归零。
    ///    • 可配置：RecoilProfile 按武器类型差异化，5 种内置预设。
    ///    • 相机联动：GetCameraOffset() 直接驱动 Camera3D 旋转。
    /// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    /// </summary>
    public static class RecoilSystem
    {
        // ─────────────────────────────────────────────────────────────────
        //  后坐力配置档（只读值类型，零 GC）
        // ─────────────────────────────────────────────────────────────────
        public readonly struct RecoilProfile
        {
            // ── 单发冲量 ──────────────────────────────────────────────────
            /// <summary>单发俯仰冲量（°）：正值 = 枪口上扬</summary>
            public readonly float ImpulsePitch;
            /// <summary>单发偏航冲量基础值（°）：正负随机</summary>
            public readonly float ImpulseYaw;
            /// <summary>单发翻滚冲量（°）</summary>
            public readonly float ImpulseRoll;

            // ── 连发堆叠 ──────────────────────────────────────────────────
            /// <summary>连发堆叠倍率增量（每发累加）</summary>
            public readonly float StackIncrement;
            /// <summary>连发堆叠上限倍率</summary>
            public readonly float StackMax;
            /// <summary>停火后堆叠归零速度（倍率/秒）</summary>
            public readonly float StackDecayRate;

            // ── 弹簧-阻尼参数 ─────────────────────────────────────────────
            /// <summary>弹簧刚度（°/s²/°）：值越大回正越快</summary>
            public readonly float Spring;
            /// <summary>阻尼系数（1/s）：值越大震荡越少</summary>
            public readonly float Damping;

            // ── 散布影响 ──────────────────────────────────────────────────
            /// <summary>俯仰偏移对散布的贡献系数</summary>
            public readonly float PitchToSpread;
            /// <summary>偏航偏移对散布的贡献系数</summary>
            public readonly float YawToSpread;
            /// <summary>基础准星散布（°），无后坐力时的固有不精准度</summary>
            public readonly float BaseSpread;

            // ── 姿态上限 ──────────────────────────────────────────────────
            /// <summary>俯仰最大偏移限制（°）</summary>
            public readonly float MaxPitch;
            /// <summary>偏航最大偏移限制（°）</summary>
            public readonly float MaxYaw;

            public RecoilProfile(
                float impulsePitch,
                float impulseYaw,
                float impulseRoll,
                float stackIncrement,
                float stackMax,
                float stackDecayRate,
                float spring,
                float damping,
                float pitchToSpread,
                float yawToSpread,
                float baseSpread,
                float maxPitch,
                float maxYaw)
            {
                ImpulsePitch   = impulsePitch;
                ImpulseYaw     = impulseYaw;
                ImpulseRoll    = impulseRoll;
                StackIncrement = stackIncrement;
                StackMax       = stackMax;
                StackDecayRate = stackDecayRate;
                Spring         = spring;
                Damping        = damping;
                PitchToSpread  = pitchToSpread;
                YawToSpread    = yawToSpread;
                BaseSpread     = baseSpread;
                MaxPitch       = maxPitch;
                MaxYaw         = maxYaw;
            }

            // ── 预设工厂方法 ──────────────────────────────────────────────

            /// <summary>突击步枪（AK / M4 类）：均衡型</summary>
            public static RecoilProfile AssaultRifle() => new RecoilProfile(
                impulsePitch:   1.8f,  impulseYaw:     0.6f,  impulseRoll:    0.25f,
                stackIncrement: 0.12f, stackMax:       2.5f,  stackDecayRate: 3.0f,
                spring:         18f,   damping:        8f,
                pitchToSpread:  0.40f, yawToSpread:    0.25f, baseSpread:     0.05f,
                maxPitch:       12f,   maxYaw:         5f);

            /// <summary>冲锋枪（MP5 / UMP 类）：偏航大，连发快，堆叠快</summary>
            public static RecoilProfile SMG() => new RecoilProfile(
                impulsePitch:   1.1f,  impulseYaw:     0.9f,  impulseRoll:    0.40f,
                stackIncrement: 0.18f, stackMax:       3.0f,  stackDecayRate: 4.0f,
                spring:         22f,   damping:        9f,
                pitchToSpread:  0.30f, yawToSpread:    0.35f, baseSpread:     0.08f,
                maxPitch:       8f,    maxYaw:         7f);

            /// <summary>狙击步枪（AWP / M24 类）：单发冲量大，回正慢</summary>
            public static RecoilProfile SniperRifle() => new RecoilProfile(
                impulsePitch:   5.5f,  impulseYaw:     0.3f,  impulseRoll:    0.15f,
                stackIncrement: 0.05f, stackMax:       1.2f,  stackDecayRate: 1.5f,
                spring:         10f,   damping:        6f,
                pitchToSpread:  0.60f, yawToSpread:    0.15f, baseSpread:     0.01f,
                maxPitch:       20f,   maxYaw:         3f);

            /// <summary>轻机枪（M249 / RPK 类）：极强堆叠，持续压制</summary>
            public static RecoilProfile LMG() => new RecoilProfile(
                impulsePitch:   2.2f,  impulseYaw:     1.1f,  impulseRoll:    0.50f,
                stackIncrement: 0.22f, stackMax:       4.0f,  stackDecayRate: 2.0f,
                spring:         14f,   damping:        7f,
                pitchToSpread:  0.50f, yawToSpread:    0.40f, baseSpread:     0.10f,
                maxPitch:       18f,   maxYaw:         9f);

            /// <summary>手枪（Glock / M1911 类）：回正最快</summary>
            public static RecoilProfile Pistol() => new RecoilProfile(
                impulsePitch:   2.8f,  impulseYaw:     0.4f,  impulseRoll:    0.20f,
                stackIncrement: 0.08f, stackMax:       1.8f,  stackDecayRate: 5.0f,
                spring:         28f,   damping:        12f,
                pitchToSpread:  0.35f, yawToSpread:    0.20f, baseSpread:     0.07f,
                maxPitch:       14f,   maxYaw:         4f);
        }

        // ─────────────────────────────────────────────────────────────────
        //  后坐力运行时状态（值类型，嵌入 InfantryUnit 字段，零 GC）
        // ─────────────────────────────────────────────────────────────────
        public struct RecoilState
        {
            // 当前姿态偏移（°）
            public float Pitch;           // 俯仰偏移
            public float Yaw;             // 偏航偏移
            public float Roll;            // 翻滚偏移

            // 弹簧-阻尼速度项
            public float PitchVel;
            public float YawVel;
            public float RollVel;

            // 连发堆叠倍率（1.0 = 无堆叠）
            public float StackMultiplier;

            // 停火计时（秒）
            public float CeasefireTimer;

            // 累计射击次数（供外部 HUD / 动画查询）
            public int   ShotCount;

            /// <summary>重置为初始状态</summary>
            public void Reset()
            {
                Pitch = Yaw = Roll = 0f;
                PitchVel = YawVel = RollVel = 0f;
                StackMultiplier = 1f;
                CeasefireTimer  = 0f;
                ShotCount       = 0;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  API 1：开枪时施加后坐力冲量
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// 每次扣动扳机后调用一次，将后坐力冲量叠加到 state 中。
        /// </summary>
        /// <param name="state">  步兵单位持有的后坐力状态（ref 传递，零拷贝）</param>
        /// <param name="profile">武器配置档</param>
        /// <param name="rng">    调用方持有的随机数生成器（避免 GC）</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyImpulse(
            ref RecoilState         state,
            in  RecoilProfile       profile,
            RandomNumberGenerator   rng)
        {
            // ── 连发堆叠倍率（每发线性增长，clamp 上限）────────────────
            state.StackMultiplier = Mathf.Min(
                state.StackMultiplier + profile.StackIncrement,
                profile.StackMax);
            float stack = state.StackMultiplier;

            // ── 俯仰冲量（始终上扬）──────────────────────────────────────
            state.PitchVel += profile.ImpulsePitch * stack * 60f;

            // ── 偏航冲量（随机左右 + 连发右偏，模拟右手持枪疲劳）────────
            float yawSign = (rng.Randf() > 0.5f) ? 1f : -1f;
            float yawBias = Mathf.Clamp((stack - 1f) * 0.15f, 0f, 0.4f);
            if (rng.Randf() < yawBias) yawSign = 1f;
            state.YawVel += profile.ImpulseYaw * stack * yawSign * 60f;

            // ── 翻滚冲量（小幅随机）──────────────────────────────────────
            state.RollVel += profile.ImpulseRoll * (rng.Randf() * 2f - 1f) * 60f;

            // ── 硬限幅：防止极端值穿模 ───────────────────────────────────
            state.Pitch = Mathf.Clamp(state.Pitch, -profile.MaxPitch * 0.1f, profile.MaxPitch);
            state.Yaw   = Mathf.Clamp(state.Yaw,   -profile.MaxYaw,          profile.MaxYaw);

            // ── 重置停火计时 & 累计射击次数 ──────────────────────────────
            state.CeasefireTimer = 0f;
            state.ShotCount++;
        }

        // ─────────────────────────────────────────────────────────────────
        //  API 2：每帧衰减（_PhysicsProcess 中调用）
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// 每物理帧调用，驱动后坐力弹簧-阻尼衰减与连发堆叠衰减。
        /// </summary>
        /// <param name="state">   步兵单位持有的后坐力状态（ref 传递）</param>
        /// <param name="profile"> 武器配置档</param>
        /// <param name="dt">      物理帧时间步长（秒）</param>
        /// <param name="isFiring">当前帧是否正在射击</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decay(
            ref RecoilState   state,
            in  RecoilProfile profile,
            float             dt,
            bool              isFiring)
        {
            // ── 停火计时 ─────────────────────────────────────────────────
            if (!isFiring)
                state.CeasefireTimer += dt;

            // ── 三轴弹簧-阻尼 Verlet 积分 ────────────────────────────────
            DecayAxis(ref state.Pitch, ref state.PitchVel, profile.Spring,        profile.Damping,        dt);
            DecayAxis(ref state.Yaw,   ref state.YawVel,   profile.Spring,        profile.Damping,        dt);
            DecayAxis(ref state.Roll,  ref state.RollVel,  profile.Spring * 1.5f, profile.Damping * 1.3f, dt);

            // ── 连发堆叠衰减（停火 80ms 后线性归 1.0）───────────────────
            if (!isFiring && state.CeasefireTimer > 0.08f)
            {
                state.StackMultiplier = Mathf.MoveToward(
                    state.StackMultiplier, 1f,
                    profile.StackDecayRate * dt);
            }

            // ── 极小值归零（消除浮点抖动）────────────────────────────────
            const float eps = 0.0005f;
            if (Mathf.Abs(state.Pitch) < eps) { state.Pitch   = 0f; state.PitchVel = 0f; }
            if (Mathf.Abs(state.Yaw)   < eps) { state.Yaw     = 0f; state.YawVel   = 0f; }
            if (Mathf.Abs(state.Roll)  < eps) { state.Roll    = 0f; state.RollVel  = 0f; }
        }

        // ─────────────────────────────────────────────────────────────────
        //  API 3：查询当前散布角（°）
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// 返回当前后坐力状态对应的准星散布半角（°）。
        /// 可直接用于子弹发射方向的随机锥形偏移。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSpreadDeg(
            in RecoilState   state,
            in RecoilProfile profile)
        {
            return profile.BaseSpread
                 + Mathf.Abs(state.Pitch) * profile.PitchToSpread
                 + Mathf.Abs(state.Yaw)   * profile.YawToSpread;
        }

        // ─────────────────────────────────────────────────────────────────
        //  API 4：后坐力偏移转相机 Euler 角（°）
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// 返回应叠加到相机或瞄准骨骼的 Euler 角偏移（度，XYZ = Pitch/Yaw/Roll）。
        /// Godot 右手系：-X 轴 = 上扬，故 Pitch 取负。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetCameraOffset(in RecoilState state)
            => new Vector3(-state.Pitch, state.Yaw, state.Roll);

        // ─────────────────────────────────────────────────────────────────
        //  内部：单轴弹簧-阻尼衰减（Verlet 半隐式积分）
        // ─────────────────────────────────────────────────────────────────
        //
        //  物理方程：
        //    a(t)    = -spring × x(t)              ← 弹性回复力（胡克定律）
        //    v(t+dt) = v(t) × (1 - damp × dt)      ← 速度阻尼（指数近似）
        //            + a(t) × dt
        //    x(t+dt) = x(t) + v(t+dt) × dt
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecayAxis(
            ref float pos,
            ref float vel,
            float     spring,
            float     damping,
            float     dt)
        {
            float accel = -spring * pos;
            vel = vel * (1f - damping * dt) + accel * dt;
            pos += vel * dt;
        }
    }
}
