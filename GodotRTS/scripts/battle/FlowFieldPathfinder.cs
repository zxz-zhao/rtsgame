using Godot;
using System.Collections.Generic;

namespace GodotRTS.Battle
{
    /// <summary>
    /// 流场寻路器。
    /// 性能铁律：三个工作数组在 _Ready() 中一次性分配，
    /// BuildFlowField() 调用时仅清零复用，禁止重建堆分配。
    /// GetNeighbors 改为内联展开，消除 yield return 迭代器每次调用的 GC。
    /// </summary>
    public partial class FlowFieldPathfinder : Node
    {
        [Export] public int   GridWidth  = 50;
        [Export] public int   GridHeight = 50;
        [Export] public float CellSize   = 2f;

        private Vector2I   _goal;
        private float[,]   _costField;
        private float[,]   _integrationField;
        private Vector2[,] _flowField;

        // 零 GC：BFS 队列复用，预分配容量
        private readonly Queue<Vector2I> _bfsQueue = new Queue<Vector2I>(512);

        // 内联邻居方向（上下左右），静态只读，零实例分配
        private static readonly int[] _dx = { -1,  1,  0,  0 };
        private static readonly int[] _dz = {  0,  0, -1,  1 };

        public override void _Ready()
        {
            // 一次性分配三个工作数组，后续 BuildFlowField 只清零复用，不重建
            _costField        = new float[GridWidth, GridHeight];
            _integrationField = new float[GridWidth, GridHeight];
            _flowField        = new Vector2[GridWidth, GridHeight];

            // 默认通行代价全部为 1
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                    _costField[x, z] = 1f;
        }

        /// <summary>
        /// 构建流场。仅清零积分场和向量场，不重建数组（零 GC）。
        /// </summary>
        public void BuildFlowField(Vector3 goalWorld)
        {
            _goal   = WorldToGrid(goalWorld);
            _goal.X = Mathf.Clamp(_goal.X, 0, GridWidth  - 1);
            _goal.Y = Mathf.Clamp(_goal.Y, 0, GridHeight - 1);
            BuildIntegrationField();
            BuildVectorField();
        }

        /// <summary>
        /// 设置障碍物代价（高代价 = 绕行，255f = 不可通行）。
        /// </summary>
        public void SetObstacleCost(Vector3 worldPos, float cost)
        {
            var cell = WorldToGrid(worldPos);
            if (cell.X < 0 || cell.X >= GridWidth || cell.Y < 0 || cell.Y >= GridHeight) return;
            _costField[cell.X, cell.Y] = Mathf.Max(1f, cost);
        }

        /// <summary>
        /// 重置障碍物代价为默认通行值。
        /// </summary>
        public void ClearObstacleCost(Vector3 worldPos)
        {
            var cell = WorldToGrid(worldPos);
            if (cell.X < 0 || cell.X >= GridWidth || cell.Y < 0 || cell.Y >= GridHeight) return;
            _costField[cell.X, cell.Y] = 1f;
        }

        private void BuildIntegrationField()
        {
            // 清零：复用数组，不重建
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                    _integrationField[x, z] = float.MaxValue;

            _integrationField[_goal.X, _goal.Y] = 0f;

            _bfsQueue.Clear();
            _bfsQueue.Enqueue(_goal);

            while (_bfsQueue.Count > 0)
            {
                var cur     = _bfsQueue.Dequeue();
                float curCost = _integrationField[cur.X, cur.Y];

                // 内联展开邻居，零迭代器 GC
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.X + _dx[i];
                    int nz = cur.Y + _dz[i];
                    if (nx < 0 || nx >= GridWidth || nz < 0 || nz >= GridHeight) continue;

                    float newCost = curCost + _costField[nx, nz];
                    if (newCost < _integrationField[nx, nz])
                    {
                        _integrationField[nx, nz] = newCost;
                        _bfsQueue.Enqueue(new Vector2I(nx, nz));
                    }
                }
            }
        }

        private void BuildVectorField()
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int z = 0; z < GridHeight; z++)
                {
                    var   best     = Vector2.Zero;
                    float bestCost = _integrationField[x, z];

                    // 内联展开邻居，零迭代器 GC
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + _dx[i];
                        int nz = z + _dz[i];
                        if (nx < 0 || nx >= GridWidth || nz < 0 || nz >= GridHeight) continue;

                        float c = _integrationField[nx, nz];
                        if (c < bestCost)
                        {
                            bestCost = c;
                            best     = new Vector2(nx - x, nz - z);
                        }
                    }
                    _flowField[x, z] = best == Vector2.Zero ? Vector2.Zero : best.Normalized();
                }
            }
        }

        /// <summary>
        /// 查询世界坐标处的流场方向（世界 XZ 平面，Y=0）。
        /// </summary>
        public Vector3 GetFlowDirection(Vector3 worldPos)
        {
            var cell = WorldToGrid(worldPos);
            cell.X   = Mathf.Clamp(cell.X, 0, GridWidth  - 1);
            cell.Y   = Mathf.Clamp(cell.Y, 0, GridHeight - 1);
            var v2   = _flowField[cell.X, cell.Y];
            return new Vector3(v2.X, 0f, v2.Y);
        }

        private Vector2I WorldToGrid(Vector3 world)
        {
            int gx = (int)((world.X + GridWidth  * CellSize * 0.5f) / CellSize);
            int gz = (int)((world.Z + GridHeight * CellSize * 0.5f) / CellSize);
            return new Vector2I(gx, gz);
        }
    }
}
