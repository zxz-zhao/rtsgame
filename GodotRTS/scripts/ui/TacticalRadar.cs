using Godot;
using System;
using System.Collections.Generic;

namespace GodotRTS.Scripts.UI
{
    /// <summary>
    /// 战术圆形雷达扫描器 (Tactical Radar Sensor HUD)
    /// 遵循 Camera3D 180° Y-yaw 逆转铁律：屏幕左+X 右-X，屏幕上+Z 下-Z
    /// 雷达 X 轴取 -X（使 +X 映射至雷达左侧），雷达 Y 轴取 -Z（使 +Z 映射至雷达上方）
    /// 支持动态扫描光束、发光粒子态势点与点击雷达镜头定位
    /// </summary>
    public partial class TacticalRadar : Control
    {
        private float _sweepAngle = 0f;
        private double _queryTimer = 0.0;
        private readonly List<(Vector2 pos, bool isPlayer)> _blips = new();
        private const float RadarRangeWorld = 52.0f;

        public override void _Ready()
        {
            if (CustomMinimumSize.X <= 0 || CustomMinimumSize.Y <= 0)
            {
                CustomMinimumSize = new Vector2(76, 76);
            }
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _Process(double delta)
        {
            _sweepAngle += (float)delta * 2.5f;
            if (_sweepAngle > Mathf.Tau)
            {
                _sweepAngle -= Mathf.Tau;
            }

            _queryTimer += delta;
            if (_queryTimer >= 0.08) // 每 80ms 刷新一次单位态势点
            {
                _queryTimer = 0.0;
                RefreshBlips();
            }

            QueueRedraw();
        }

        private void RefreshBlips()
        {
            _blips.Clear();
            var activeUnits = RtsUnit.AllActiveUnits;
            for (int i = 0; i < activeUnits.Count; i++)
            {
                var u = activeUnits[i];
                if (!GodotObject.IsInstanceValid(u) || u.IsDead)
                    continue;

                bool isRevealed = u.PlayerOwned 
                    || u.FogRevealed 
                    || BattleGameManager.DisableFogOfWar
                    || (BattleGameManager.Instance == null);

                if (!isRevealed)
                    continue;

                // 遵循 180° Y-yaw 逆转铁律：
                // 世界 +X 位于屏幕左侧 -> 映射至雷达 -X 轴
                // 世界 +Z 位于屏幕上方 -> 映射至雷达 -Y 轴 (GUI 坐标系 Y 轴向下)
                float nx = Mathf.Clamp(-u.GlobalPosition.X / RadarRangeWorld, -1.0f, 1.0f);
                float nz = Mathf.Clamp(-u.GlobalPosition.Z / RadarRangeWorld, -1.0f, 1.0f);
                _blips.Add((new Vector2(nx, nz), u.PlayerOwned));
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            Vector2? clickPos = null;
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                clickPos = mb.Position;
            }
            else if (@event is InputEventScreenTouch touch && touch.Pressed)
            {
                clickPos = touch.Position;
            }

            if (clickPos.HasValue)
            {
                var center = Size * 0.5f;
                float radius = Mathf.Min(center.X, center.Y) - 4f;
                var offset = clickPos.Value - center;
                float nx = offset.X / (radius * 0.85f);
                float ny = offset.Y / (radius * 0.85f);

                // 反推世界坐标：nx = -worldX / R => worldX = -nx * R
                float worldX = -nx * RadarRangeWorld;
                float worldZ = -ny * RadarRangeWorld;

                var rtsCam = RtsCamera.Instance;
                if (rtsCam is not null && GodotObject.IsInstanceValid(rtsCam))
                {
                    rtsCam.JumpTo(new Vector3(worldX, 0f, worldZ));
                }
                GetViewport()?.SetInputAsHandled();
            }
        }

        public override void _Draw()
        {
            var center = Size * 0.5f;
            float radius = Mathf.Min(center.X, center.Y) - 4f;
            if (radius < 10f) return;

            // 1. 深色半透明雷达底盘
            DrawCircle(center, radius, new Color(0.02f, 0.05f, 0.08f, 0.94f));

            // 2. 同心距离刻度环 (25m / 50m / 75m)
            DrawArc(center, radius * 0.35f, 0, Mathf.Tau, 24, new Color(0.12f, 0.45f, 0.65f, 0.28f), 1.0f);
            DrawArc(center, radius * 0.70f, 0, Mathf.Tau, 32, new Color(0.12f, 0.55f, 0.75f, 0.38f), 1.0f);
            DrawArc(center, radius, 0, Mathf.Tau, 48, new Color(0.0f, 0.85f, 1.0f, 0.85f), 1.5f);

            // 3. 十字准星与刻度标
            DrawLine(new Vector2(center.X - radius, center.Y), new Vector2(center.X + radius, center.Y), new Color(0.0f, 0.85f, 1.0f, 0.22f), 1.0f);
            DrawLine(new Vector2(center.X, center.Y - radius), new Vector2(center.X, center.Y + radius), new Color(0.0f, 0.85f, 1.0f, 0.22f), 1.0f);

            // 4. 旋转扫描光束与拖尾
            var sweepDir = new Vector2(Mathf.Cos(_sweepAngle), Mathf.Sin(_sweepAngle));
            DrawLine(center, center + sweepDir * radius, new Color(0.1f, 1.0f, 0.8f, 0.95f), 2.0f);
            DrawArc(center, radius * 0.85f, _sweepAngle - 0.45f, _sweepAngle, 10, new Color(0.1f, 1.0f, 0.8f, 0.35f), 2.5f);

            // 5. 蓝红双方实时态势点
            foreach (var (normPos, isPlayer) in _blips)
            {
                var pt = center + new Vector2(normPos.X * radius * 0.85f, normPos.Y * radius * 0.85f);
                if (pt.DistanceTo(center) <= radius - 2f)
                {
                    if (isPlayer)
                    {
                        DrawCircle(pt, 2.8f, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                        DrawArc(pt, 4.0f, 0, Mathf.Tau, 8, new Color(0.2f, 0.85f, 1.0f, 0.45f), 1.0f);
                    }
                    else
                    {
                        DrawCircle(pt, 2.8f, new Color(1.0f, 0.32f, 0.32f, 1.0f));
                        DrawArc(pt, 4.0f, 0, Mathf.Tau, 8, new Color(1.0f, 0.32f, 0.32f, 0.45f), 1.0f);
                    }
                }
            }
        }
    }
}
