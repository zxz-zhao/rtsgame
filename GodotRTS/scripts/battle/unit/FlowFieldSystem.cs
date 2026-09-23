using Godot;
using System;
using System.Collections.Generic;

namespace GodotRTS.Battle.Unit
{
    // ─────────────────────────────────────────────────────────────
    //  格子代价与流向数据
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 流场单元格（值类型，预分配数组使用）
    /// </summary>
    internal struct FlowCell
    {
        /// <summary>积分场代价（BFS 距离目标的步数）</summary>
        public int    Cost;
        /// <summary>流向向量（归一化，指向目标方向）</summary>
        public Vector2 FlowDir;
        /// <summary>是否不可通行（障碍物）</summary>
        public bool   IsBlocked;
    }

    // ─────────────────────────────────────────────────────────────
    //  流场（一次目标对应一个流场）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 流场数据对象（由 FlowFieldSystem 生成，由 UnitMover 消费）
    /// </summary>
    public sealed class FlowField
    {
        public readonly Vector2I GoalCell;
        public readonly int      Width;
        public readonly int      Height;
        private readonly FlowCell[] _cells;

        internal FlowField(int width, int height, Vector2I goalCell)
        {
            Width    = width;
            Height   = height;
            GoalCell = goalCell;
            _cells   = new FlowCell[width * height];
        }

        internal ref FlowCell GetRef(int x, int z) => ref _cells[z * Width + x];

        /// <summary>获取世界坐标对应的流向（已归一化）。</summary>
        public Vector2 GetFlowDir(int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height)
                return Vector2.Zero;
            return _cells[z * Width + x].FlowDir;
        }

        internal void SetBlocked(int x, int z, bool blocked)
        {
            if (x >= 0 && x < Width && z >= 0 && z < Height)
                _cells[z * Width + x].IsBlocked = blocked;
        }

        internal bool IsBlocked(int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height) return true;
            return _cells[z * Width + x].IsBlocked;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  流场寻路系统
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 流场寻路系统（Flow Field Pathfinding）。
    /// 挂载于战斗场景根节点，为编队单位提供统一的流场导航。
    ///
    /// 算法：
    ///   1. BFS 从目标格向外传播，计算每格「到目标代价」（积分场）
    ///   2. 每格流向 = 相邻格中代价最小方向（8方向）
    ///   3. 单位读取自身所在格的流向并移动
    /// </summary>
    public partial class FlowFieldSystem : Node
    {
        // ── Inspector ───────────────────────────────────────────────
        [Export] public int   GridWidth  { get; set; } = 100;
        [Export] public int   GridHeight { get; set; } = 100;
        [Export] public float CellSize   { get; set; } = 2.0f;

        // ── 8 方向偏移（包含对角线）────────────────────────────────
        private static readonly Vector2I[] s_dirs = {
            new(-1,  0), new( 1,  0), new( 0, -1), new( 0,  1),
            new(-1, -1), new( 1, -1), new(-1,  1), new( 1,  1),
        };
        // 对角线代价比直线高（近似欧氏距离）
        private static readonly int[] s_costs = { 10, 10, 10, 10, 14, 14, 14, 14 };

        // 障碍物格子集合（由外部注册，如建筑/地形）
        private readonly HashSet<Vector2I> _obstacles = new(256);

        // BFS 队列复用（避免每次 new Queue）
        private readonly Queue<Vector2I> _bfsQueue = new(1024);

        public Vector2I WorldToCell(Vector3 worldPos)
        {
            return new Vector2I(
                Mathf.Clamp(Mathf.FloorToInt(worldPos.X / CellSize), 0, GridWidth - 1),
                Mathf.Clamp(Mathf.FloorToInt(worldPos.Z / CellSize), 0, GridHeight - 1)
            );
        }

        // ── 公共接口 ────────────────────────────────────────────────

        /// <summary>
        /// 注册障碍物格子（建筑、地形障碍等）。
        /// </summary>
        public void AddObstacle(Vector2I cell)    => _obstacles.Add(cell);
        public void RemoveObstacle(Vector2I cell) => _obstacles.Remove(cell);
        public void ClearObstacles()              => _obstacles.Clear();

        /// <summary>
        /// 生成一个以 goalWorld 为目标的流场。
        /// 时间复杂度 O(W×H)，对 100×100 地图约 0.5ms。
        /// </summary>
        public FlowField BuildFlowField(Vector3 goalWorld)
        {
            Vector2I goalCell = WorldToCell(goalWorld);
            int      w        = GridWidth;
            int      h        = GridHeight;

            var field = new FlowField(w, h, goalCell);

            // 标记障碍物
            foreach (var obs in _obstacles)
                field.SetBlocked(obs.X, obs.Y, true);

            // ── 阶段1：BFS 积分场 ────────────────────────────────────
            // 初始化代价为 int.MaxValue
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                field.GetRef(x, z).Cost = int.MaxValue;

            _bfsQueue.Clear();

            if (!field.IsBlocked(goalCell.X, goalCell.Y))
            {
                field.GetRef(goalCell.X, goalCell.Y).Cost = 0;
                _bfsQueue.Enqueue(goalCell);
            }

            while (_bfsQueue.Count > 0)
            {
                var cur = _bfsQueue.Dequeue();
                int curCost = field.GetRef(cur.X, cur.Y).Cost;

                for (int d = 0; d < s_dirs.Length; d++)
                {
                    int nx = cur.X + s_dirs[d].X;
                    int nz = cur.Y + s_dirs[d].Y;

                    if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                    if (field.IsBlocked(nx, nz)) continue;

                    int newCost = curCost + s_costs[d];
                    if (newCost < field.GetRef(nx, nz).Cost)
                    {
                        field.GetRef(nx, nz).Cost = newCost;
                        _bfsQueue.Enqueue(new Vector2I(nx, nz));
                    }
                }
            }

            // ── 阶段2：计算流向 ──────────────────────────────────────
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                if (field.IsBlocked(x, z)) continue;

                int   curCost  = field.GetRef(x, z).Cost;
                if (curCost == int.MaxValue) continue; // 不可达格子

                int     bestCost = curCost;
                Vector2 bestDir  = Vector2.Zero;

                for (int d = 0; d < s_dirs.Length; d++)
                {
                    int nx = x + s_dirs[d].X;
                    int nz = z + s_dirs[d].Y;

                    if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                    if (field.IsBlocked(nx, nz)) continue;

                    int nc = field.GetRef(nx, nz).Cost;
                    if (nc < bestCost)
                    {
                        bestCost = nc;
                        bestDir  = new Vector2(s_dirs[d].X, s_dirs[d].Y).Normalized();
                    }
                }

                field.GetRef(x, z).FlowDir = bestDir;
            }

            return field;
        }

        /// <summary>
        /// 查询世界坐标处的流向（X/Z 平面，Y=0）。
        /// </summary>
        public Vector2 SampleFlowDir(FlowField field, Vector3 worldPos)
        {
            var cell = WorldToCell(worldPos);
            return field.GetFlowDir(cell.X, cell.Y);
        }
    }
}
