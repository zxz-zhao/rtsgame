using Godot;

namespace GodotRTS.Battle.Unit
{
    /// <summary>
    /// RTS 战场单位基类。
    /// 集成属性同步、血量管理、以及真人化移动姿态控制器（UnitLocomotionController）。
    /// </summary>
    public partial class UnitBase : CharacterBody3D
    {
        [Export] public uint UnitId     { get; set; }
        [Export] public int  TeamId     { get; set; }
        [Export] public float MaxHp     { get; set; } = 500f;
        [Export] public float CurrentHp { get; set; } = 500f;
        [Export] public float MoveSpeed { get; set; } = 4.5f;

        public bool IsDead => CurrentHp <= 0f;
        public event System.Action<uint> Died;

        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - damage);
            if (IsDead)
            {
                Died?.Invoke(UnitId);
            }
        }

        private UnitLocomotionController _locomotion;
        private Vector3 _targetVelocity;

        public override void _Ready()
        {
            // 自动获取或添加自然移动控制器
            _locomotion = GetNodeOrNull<UnitLocomotionController>("UnitLocomotionController");
            if (_locomotion == null)
            {
                _locomotion = new UnitLocomotionController();
                _locomotion.Name = "UnitLocomotionController";
                AddChild(_locomotion);
            }
        }

        /// <summary>
        /// 由寻路系统或AI每帧调用：设置当前移动目标速度
        /// </summary>
        public void SetDesiredVelocity(Vector3 velocity)
        {
            _targetVelocity = velocity;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // 驱动真人化自然移动姿态控制器（惯性、侧倾、起伏、平滑转身）
            if (_locomotion != null)
            {
                _locomotion.UpdateLocomotion(_targetVelocity, dt);
            }
            else
            {
                // 降级基础移动
                Velocity = Velocity.Lerp(_targetVelocity, 12f * dt);
                MoveAndSlide();
            }
        }
    }
}
