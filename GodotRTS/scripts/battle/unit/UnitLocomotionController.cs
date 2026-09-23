using Godot;

namespace GodotRTS.Battle.Unit
{
    /// <summary>
    /// 步兵真人化自然移动与姿态控制器。
    /// 自动实现：起步加速度惯性、平滑转身、行走上下起伏(Bobbing)、移动侧倾(Lean)。
    /// 零 GC，完全兼容 Godot 4.3+ 物理引擎。
    /// </summary>
    public partial class UnitLocomotionController : Node
    {
        [Export] public NodePath CharacterBodyPath { get; set; }
        [Export] public NodePath VisualMeshPath    { get; set; }

        [Export] public float MoveSpeed      { get; set; } = 4.5f;
        [Export] public float Acceleration   { get; set; } = 14f;
        [Export] public float RotationSpeed  { get; set; } = 12f;

        // 自然行走姿态参数
        [Export] public float BobFreq        { get; set; } = 9.0f;  // 起伏频率
        [Export] public float BobAmp         { get; set; } = 0.06f; // 起伏幅度（米）
        [Export] public float LeanFactor     { get; set; } = 0.12f; // 移动侧倾系数

        private CharacterBody3D _body;
        private Node3D          _mesh;
        private Vector3         _currentVelocity;
        private float           _bobTimer;
        private Vector3         _meshOriginalPos;

        public override void _Ready()
        {
            if (!string.IsNullOrEmpty(CharacterBodyPath))
                _body = GetNode<CharacterBody3D>(CharacterBodyPath);
            else
                _body = GetParent<CharacterBody3D>();

            if (!string.IsNullOrEmpty(VisualMeshPath))
                _mesh = GetNode<Node3D>(VisualMeshPath);
            else if (_body != null && _body.GetChildCount() > 0)
            {
                for (int i = 0; i < _body.GetChildCount(); i++)
                {
                    if (_body.GetChild(i) is Node3D n3d && !(n3d is CollisionShape3D))
                    {
                        _mesh = n3d;
                        break;
                    }
                }
            }

            if (_mesh != null)
                _meshOriginalPos = _mesh.Position;
        }

        /// <summary>
        /// 外部逻辑调用：驱动单位向目标速度移动并呈现自然的身体惯性与姿态
        /// </summary>
        public void UpdateLocomotion(Vector3 targetVelocity, float delta)
        {
            if (_body == null) return;

            // 1. 平滑加速度惯性（起步和停止不再生硬突兀）
            _currentVelocity = _currentVelocity.Lerp(targetVelocity, Acceleration * delta);
            _body.Velocity = _currentVelocity;
            _body.MoveAndSlide();

            if (_mesh == null) return;

            // 2. 平滑旋转朝向运动方向（彻底消除瞬时硬转）
            if (targetVelocity.LengthSquared() > 0.01f)
            {
                Vector3 lookDir = new Vector3(targetVelocity.X, 0f, targetVelocity.Z).Normalized();
                if (lookDir.LengthSquared() > 0.001f)
                {
                    float targetAngle = Mathf.Atan2(-lookDir.X, -lookDir.Z);
                    float currentAngle = _body.Rotation.Y;
                    float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, RotationSpeed * delta);
                    _body.Rotation = _body.Rotation with { Y = newAngle };
                }
            }

            // 3. 行走时身体上下起伏 (Bobbing) 与侧倾 (Lean)
            float speedXZ = new Vector3(_currentVelocity.X, 0f, _currentVelocity.Z).Length();
            if (speedXZ > 0.2f && _body.IsOnFloor())
            {
                _bobTimer += delta * speedXZ * BobFreq;
                float bobOffset = Mathf.Sin(_bobTimer) * BobAmp;
                
                // 侧倾：根据横向速度计算身体倾斜
                float lean = _currentVelocity.X * LeanFactor;

                _mesh.Position = _meshOriginalPos + new Vector3(0f, Mathf.Abs(bobOffset), 0f);
                _mesh.Rotation = _mesh.Rotation with { Z = -lean };
            }
            else
            {
                // 停止时平滑恢复初始姿态
                _bobTimer = 0f;
                _mesh.Position = _mesh.Position.Lerp(_meshOriginalPos, 12f * delta);
                _mesh.Rotation = _mesh.Rotation with { Z = Mathf.Lerp(_mesh.Rotation.Z, 0f, 12f * delta) };
            }
        }
    }
}
