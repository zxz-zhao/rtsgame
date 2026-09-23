using Godot;
using System;

namespace GodotRTS.Scripts.UI
{
    public partial class TacticalRadar : Control
    {
        private float _sweepAngle = 0f;
        private double _queryTimer = 0.0;
        private readonly System.Collections.Generic.List<(Vector2 pos, bool isBlue)> _blips = new();

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(100, 100);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Process(double delta)
        {
            _sweepAngle += (float)delta * 2.4f;
            if (_sweepAngle > Mathf.Tau)
            {
                _sweepAngle -= Mathf.Tau;
            }

            _queryTimer += delta;
            if (_queryTimer >= 0.1) // 每 100ms 刷新一次单位态势点
            {
                _queryTimer = 0.0;
                RefreshBlips();
            }

            QueueRedraw();
        }

        private void RefreshBlips()
        {
            _blips.Clear();
            var tree = GetTree();
            if (tree == null) return;

            var nodes = tree.Root.FindChildren("*", "RtsUnit", true, false);
            foreach (var node in nodes)
            {
                if (node is RtsUnit u && GodotObject.IsInstanceValid(u) && !u.IsDead)
                {
                    // 映射战场坐标 (-50 ~ +50) 到归一化雷达坐标 (-1 ~ +1)
                    float nx = Mathf.Clamp(u.GlobalPosition.X / 48.0f, -1.0f, 1.0f);
                    float nz = Mathf.Clamp(u.GlobalPosition.Z / 48.0f, -1.0f, 1.0f);
                    _blips.Add((new Vector2(nx, nz), u.PlayerOwned));
                }
            }
        }

        public override void _Draw()
        {
            var center = Size * 0.5f;
            float radius = Mathf.Min(center.X, center.Y) - 4f;
            if (radius < 10f) return;

            // 1. 深色半透明雷达底盘
            DrawCircle(center, radius, new Color(0.03f, 0.06f, 0.10f, 0.92f));

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
            foreach (var (normPos, isBlue) in _blips)
            {
                var pt = center + new Vector2(normPos.X * radius * 0.85f, normPos.Y * radius * 0.85f);
                if (pt.DistanceTo(center) <= radius - 2f)
                {
                    if (isBlue)
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
