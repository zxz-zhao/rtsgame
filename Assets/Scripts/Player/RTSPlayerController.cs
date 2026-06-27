using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 玩家控制器：单位选择、命令下达、建筑放置
public class RTSPlayerController : MonoBehaviour
{
    public static RTSPlayerController Instance { get; private set; }
    private const float MainBaseBuildRadius = MainBase.BuildRangeRadius;
    private const float UnitPickTolerancePixels = 38f;
    private const float BuildingPickTolerancePixels = 52f;

    [Header("设置")]
    public LayerMask GroundLayer;
    public LayerMask UnitLayer;
    public LayerMask BuildingLayer;
    public float SelectBoxMinSize = 5f;

    // 选中单位列表
    private List<RTSUnit> selectedUnits = new List<RTSUnit>();
    public RTSBuilding SelectedBuilding { get; private set; }

    // 框选相关
    private Vector2 selectStart;
    private bool bDragging  = false;
    private bool bBoxSelectArmed = false;
#if !UNITY_ANDROID || UNITY_EDITOR
    private bool bMouseDown = false;
#endif
    private Camera cam;

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android 触摸：判断是点击还是拖拽
    private Vector2 _touchStart;
    private bool    _touchMoved = false;
    private const float TouchDragThreshold = 22f;  // 像素，与 RTSCamera 保持一致
#endif

    // 框选边框纹理
    private Texture2D _boxFillTex;
    private Texture2D _boxBorderTex;

    // 建筑放置模式
    private bool bInPlacementMode = false;
    private GameObject placementGhost;
    private GameObject placementPrefab;
    private bool placementGhostHasValidPosition = false;
    private float placementFootprintRadius = 3f;
    private Vector2 placementFootprintSize = Vector2.one * 6f;
    private Vector2 placementFootprintCenter = Vector2.zero;
    private GameObject placementFootprintMarker;
    private Renderer placementFootprintRenderer;
    private readonly List<Renderer> placementFootprintRenderers = new List<Renderer>();
    private GameObject placementBuildRangeMarker;
    private GameObject placementOccupiedFootprintRoot;
    private readonly List<GameObject> placementOccupiedFootprintMarkers = new List<GameObject>();
    private float placementOccupiedFootprintRefreshTimer = 0f;
    private const float OccupiedFootprintRefreshInterval = 0.25f;
    private const float PlacementFootprintPadding = 0.55f;
    private const float NavalYardFrontWaterProbeFactor = 0.68f;
    private const float NavalYardBackLandProbeFactor = 0.55f;
    private const float NavalYardWaterProbeInset = 0.2f;

    private struct FootprintBounds2D
    {
        public Vector2 Center;
        public Vector2 Size;
        public float Radius;
    }

    private struct FootprintShape2D
    {
        public Vector2 Center;
        public Vector2 AxisX;
        public Vector2 AxisZ;
        public Vector2 HalfSize;
    }

    private RTSPlayerState playerState;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        cam = Camera.main;
        playerState = RTSPlayerState.Instance;
        // 创建框选纹理（只需 1×1 像素，靠 GUIStyle 拉伸）
        _boxBorderTex = new Texture2D(1, 1);
        _boxBorderTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.95f));
        _boxBorderTex.Apply();
        _boxFillTex = new Texture2D(1, 1);
        _boxFillTex.SetPixel(0, 0, new Color(0.6f, 0.9f, 1f, 0.06f));
        _boxFillTex.Apply();
    }

    void Update()
    {
        // 注意：先 HandleKeyboard 让 G/P/A 模式触发并消化对应的左/右键点击，
        // 再走 HandleTouch（普通选中/移动），否则会被普通选中先吃掉左键。
#if !UNITY_ANDROID || UNITY_EDITOR
        _consumedMouse0ThisFrame = false;
        _consumedMouse1ThisFrame = false;
#endif
        if (RTSHUD.Instance != null && RTSHUD.Instance.IsTechTargetingInputActive)
            return;
        HandleKeyboard();
        HandleTouch();
        if (bInPlacementMode) UpdatePlacementGhost();
    }

#if !UNITY_ANDROID || UNITY_EDITOR
    private bool _consumedMouse0ThisFrame = false;
    private bool _consumedMouse1ThisFrame = false;
#endif

    void HandleTouch()
    {
        // PC鼠标 + 安卓触摸统一处理
        bool tap = false;
        bool rightTap = false;
        Vector2 tapPos = Vector2.zero;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            tapPos = t.position;
            if (bBoxSelectArmed)
            {
                if (t.phase == TouchPhase.Began)
                {
                    selectStart = t.position;
                    _touchStart = t.position;
                    _touchMoved = false;
                    bDragging = false;
                }
                else if (t.phase == TouchPhase.Moved)
                {
                    if (!_touchMoved && Vector2.Distance(t.position, _touchStart) > TouchDragThreshold)
                        _touchMoved = true;
                    if (_touchMoved) bDragging = true;
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    bool wasDragging = bDragging;
                    _touchMoved = false;
                    bDragging = false;
                    bBoxSelectArmed = false;
                    if (t.phase == TouchPhase.Canceled) return;
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;
                    if (bInPlacementMode) { ConfirmPlacement(tapPos); return; }
                    if (wasDragging)
                    {
                        BoxSelect(selectStart, tapPos);
                        return;
                    }
                    HandleSelect(tapPos);
                    return;
                }
                return;
            }
            if (t.phase == TouchPhase.Began)
            {
                _touchStart = t.position;
                _touchMoved = false;
            }
            else if (t.phase == TouchPhase.Moved)
            {
                if (!_touchMoved && Vector2.Distance(t.position, _touchStart) > TouchDragThreshold)
                    _touchMoved = true;
            }
            else if (t.phase == TouchPhase.Ended)
            {
                // 若手指移动距离超过阈值，视为相机拖拽，不下令
                if (_touchMoved) { _touchMoved = false; return; }
                _touchMoved = false;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;
                if (bInPlacementMode) { ConfirmPlacement(tapPos); return; }
                if (TryConsumePendingPrimaryTap(t.position)) return;
                if (selectedUnits.Count > 0)
                {
                    bool hitOwn = TryPickUnitAtPointer(t.position, true, out _);
                    if (hitOwn) tap = true;
                    else rightTap = true;
                }
                else tap = true;
            }
        }
#else
        tapPos = Input.mousePosition;
        if (_consumedMouse0ThisFrame) return;
        // 框选：鼠标左键按下 / 拖拽 / 抬起
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            bMouseDown  = true;
            bDragging   = false;
            selectStart = tapPos;
        }
        if (bMouseDown && Input.GetMouseButton(0))
        {
            // 超过阈值才算拖拽
            if (!bDragging && Vector2.Distance(tapPos, selectStart) > SelectBoxMinSize)
                bDragging = true;
        }
        if (bMouseDown && Input.GetMouseButtonUp(0))
        {
            bMouseDown = false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            { bDragging = false; return; }
            if (bInPlacementMode) { bDragging = false; ConfirmPlacement(tapPos); return; }
            if (bDragging)
            {
                bDragging = false;
                bBoxSelectArmed = false;
                BoxSelect(selectStart, tapPos);
                return;
            }
            // 没拖就当单次点击（但若被 G/P 等 pending 模式消费过则跳过）
            if (!_consumedMouse0ThisFrame) tap = true;
        }
        if (Input.GetMouseButtonDown(1) && !_consumedMouse1ThisFrame) rightTap = true;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && !bMouseDown) return;
#endif

        if (tap)
        {
            if (bInPlacementMode) { ConfirmPlacement(tapPos); return; }
            if (_bombingRunPending && selectedUnits.Count > 0)
            {
                TryIssueBombingRunAtPointer(tapPos);
                bBoxSelectArmed = false;
                return;
            }
            if (_attackGroundPending && selectedUnits.Count > 0)
            {
                TryIssueAttackGroundAtPointer(tapPos);
                _attackGroundPending = false;
                bBoxSelectArmed = false;
                return;
            }
            if (_attackMovePending && selectedUnits.Count > 0)
            {
                TryIssueAttackMoveAtPointer(tapPos);
                _attackMovePending = false;
                bBoxSelectArmed = false;
                return;
            }
            HandleSelect(tapPos);
            bBoxSelectArmed = false;
        }
        if (rightTap)
        {
            if (_bombingRunPending)
            {
                CancelBombingRunMode();
                return;
            }
            if (_attackGroundPending && selectedUnits.Count > 0)
            {
                TryIssueAttackGroundAtPointer(tapPos);
                _attackGroundPending = false;
                return;
            }
            HandleRightClick(tapPos);
        }
    }

    public void IssueContextCommand(string command, Vector2 screenPos)
    {
        if (string.IsNullOrEmpty(command))
            return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        switch (command)
        {
            case "move":
                IssueMoveAtPointer(screenPos);
                break;
            case "attackMove":
                TryIssueAttackMoveAtPointer(screenPos);
                _attackMovePending = false;
                break;
            case "attackGround":
                TryIssueAttackGroundAtPointer(screenPos);
                _attackGroundPending = false;
                break;
            case "patrol":
                TryIssuePatrolAtPointer(screenPos);
                _patrolPending = false;
                break;
            case "guard":
                TryIssueGuardAtPointer(screenPos);
                _guardPending = false;
                break;
            case "stop":
                StopSelectedUnits();
                break;
            case "park":
                ParkSelectedAircraft();
                break;
        }
    }

    // 框选：将屏幕矩形内所有己方单位加入选择
    void BoxSelect(Vector2 start, Vector2 end)
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        float xMin = Mathf.Min(start.x, end.x);
        float xMax = Mathf.Max(start.x, end.x);
        float yMin = Mathf.Min(start.y, end.y);
        float yMax = Mathf.Max(start.y, end.y);
        if (!Input.GetKey(KeyCode.LeftShift)) { ClearSelectionVisuals(); selectedUnits.Clear(); SelectedBuilding = null; }
        var allForBox = GameManager.Instance?.GetAllUnits();
        if (allForBox == null) return;
        foreach (var unit in allForBox)
        {
            if (unit == null || !unit.IsPlayerOwned()) continue;
            Vector3 sp = cam.WorldToScreenPoint(unit.transform.position);
            if (sp.z > 0 && sp.x >= xMin && sp.x <= xMax && sp.y >= yMin && sp.y <= yMax)
                if (!selectedUnits.Contains(unit)) selectedUnits.Add(unit);
        }
        ApplySelectionVisuals(selectedUnits, null);
        RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
        if (selectedUnits.Count > 0) _UiClickAudio.PlayConfirm();
    }

    // 绘制框选矩形（细白边框 + 极淡填充）
    void OnGUI()
    {
        if (!bDragging) return;
        Vector2 cur = Input.mousePosition;
        // OnGUI 坐标 Y 轴与屏幕相反
        float x = Mathf.Min(selectStart.x, cur.x);
        float y = Screen.height - Mathf.Max(selectStart.y, cur.y);
        float w = Mathf.Abs(cur.x - selectStart.x);
        float h = Mathf.Abs(cur.y - selectStart.y);
        Rect r = new Rect(x, y, w, h);
        const float border = 1.5f;
        // 淡蓝填充
        GUI.DrawTexture(r, _boxFillTex);
        // 四条边（细白线）
        GUI.DrawTexture(new Rect(r.x,              r.y,              r.width, border), _boxBorderTex);
        GUI.DrawTexture(new Rect(r.x,              r.yMax - border,  r.width, border), _boxBorderTex);
        GUI.DrawTexture(new Rect(r.x,              r.y,              border,  r.height), _boxBorderTex);
        GUI.DrawTexture(new Rect(r.xMax - border,  r.y,              border,  r.height), _boxBorderTex);
    }

    void HandleSelect(Vector2 screenPos)
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        // 先检查是否点击单位
        if (TryPickUnitAtPointer(screenPos, true, out RTSUnit unit))
        {
            if (!Input.GetKey(KeyCode.LeftShift)) { ClearSelectionVisuals(); selectedUnits.Clear(); }
            if (!selectedUnits.Contains(unit)) { selectedUnits.Add(unit); unit.SetSelected(true); _UiClickAudio.PlayClick(); }
            SelectedBuilding = null;
            RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
            return;
        }
        // 检查建筑
        if (TryPickBuildingAtPointer(screenPos, true, out RTSBuilding b))
        {
            ClearSelectionVisuals();
            selectedUnits.Clear();
            SelectedBuilding = b;
            b.SetSelected(true);
            _UiClickAudio.PlayConfirm();
            RTSHUD.Instance?.OnSelectionChanged(selectedUnits, b);
            return;
        }
        // 点空地 - 清除选择
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            ClearSelectionVisuals();
            selectedUnits.Clear();
            SelectedBuilding = null;
            RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
        }
    }

    void HandleRightClick(Vector2 screenPos)
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        RTSHUD.Instance?.HideRightClickCommandMenu();
        if (_attackGroundPending && selectedUnits.Count > 0)
        {
            TryIssueAttackGroundAtPointer(screenPos);
            _attackGroundPending = false;
            return;
        }
        // 攻击移动模式：右键点地图 → 单位攻击移动到该点
        if (_attackMovePending && selectedUnits.Count > 0)
        {
            TryIssueAttackMoveAtPointer(screenPos);
            _attackMovePending = false;
            return;
        }
        // 已选中己方建筑（生产建筑）→ 右键设置集结点
        if (selectedUnits.Count == 0 && SelectedBuilding != null && SelectedBuilding.bPlayerOwned
            && SelectedBuilding.ProductionUnits != null && SelectedBuilding.ProductionUnits.Length > 0)
        {
            Ray rb = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(rb, out RaycastHit hb, 600f, GroundLayer))
            {
                SelectedBuilding.SetRallyPoint(hb.point);
                SpawnCommandMarker(hb.point, new Color(1f, 0.85f, 0.30f));
                _UiClickAudio.PlayConfirm();
                RallyMarkerVisual.ShowFor(SelectedBuilding, hb.point);
                return;
            }
        }
        if (selectedUnits.Count == 0) return;
        // 检查是否点击敌方单位（攻击）
        if (TryPickUnitAtPointer(screenPos, false, out RTSUnit target))
        {
            IssueAttackUnit(target);
            return;
        }
        // 检查是否点击敌方建筑（攻击建筑）
        if (TryPickBuildingAtPointer(screenPos, false, out RTSBuilding bldg))
        {
            IssueAttackBuilding(bldg);
            return;
        }
        if (RTSHUD.Instance != null)
        {
            RTSHUD.Instance.ShowRightClickCommandMenu(
                screenPos,
                HasSelectedAttackGroundUnits(),
                HasSelectedAircraft(),
                command => IssueContextCommand(command, screenPos));
            return;
        }

        IssueMoveAtPointer(screenPos);
    }

    bool IssueMoveAtPointer(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
        {
            Vector3 dest = hit.point;
            SpawnCommandMarker(dest, new Color(0.25f, 1f, 0.45f));
            _UiClickAudio.PlayConfirm();
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                RTSUnit unit = selectedUnits[i];
                if (unit == null || unit.IsDead() || !unit.IsPlayerOwned()) continue;
                Vector3 offset = GetFormationOffset(i, selectedUnits.Count);
                Vector3 finalDest = dest + offset;
                unit.ApplyMoveCommand(finalDest);
                if (GameNetworkSync.Instance?.IsNetworkGame == true && unit.NetId != 0)
                    GameNetworkSync.Instance.SendMove(unit.NetId, finalDest);
            }
            return true;
        }

        _UiClickAudio.PlayDeny();
        return false;
    }

    Vector3 GetFormationOffset(int index, int count)
    {
        if (count <= 1)
            return Vector3.zero;

        const float spacing = 2f;
        int columns = Mathf.Min(5, count);
        int row = index / columns;
        int col = index % columns;
        int rows = Mathf.CeilToInt(count / (float)columns);
        int countInRow = Mathf.Min(columns, count - row * columns);
        float x = (col - (countInRow - 1) * 0.5f) * spacing;
        float z = (row - (rows - 1) * 0.5f) * -spacing;
        return new Vector3(x, 0f, z);
    }

    bool TryIssueAttackMoveAtPointer(Vector2 screenPos)
    {
        if (TryPickUnitAtPointer(screenPos, false, out RTSUnit target))
        {
            IssueAttackUnit(target);
            return true;
        }

        if (TryPickBuildingAtPointer(screenPos, false, out RTSBuilding building))
        {
            IssueAttackBuilding(building);
            return true;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
        {
            IssueAttackMove(hit.point);
            return true;
        }

        _UiClickAudio.PlayDeny();
        return false;
    }

    void IssueAttackMove(Vector3 dest)
    {
        SpawnCommandMarker(dest, new Color(1f, 0.62f, 0.28f));
        _UiClickAudio.PlayWarn();
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            RTSUnit unit = selectedUnits[i];
            if (unit == null || unit.IsDead() || !unit.IsPlayerOwned()) continue;
            Vector3 offset = GetFormationOffset(i, selectedUnits.Count);
            Vector3 finalDest = dest + offset;
            unit.ApplyAttackMoveCommand(finalDest);
            if (GameNetworkSync.Instance?.IsNetworkGame == true && unit.NetId != 0)
                GameNetworkSync.Instance.SendAttackMove(unit.NetId, finalDest);
        }
    }

    bool TryIssueAttackGroundAtPointer(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
        {
            IssueAttackGround(hit.point);
            return true;
        }

        _UiClickAudio.PlayDeny();
        return false;
    }

    void IssueAttackGround(Vector3 point)
    {
        int issuedCount = 0;
        var sync = GameNetworkSync.Instance;
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            RTSUnit unit = selectedUnits[i];
            if (unit == null || unit.IsDead() || !unit.IsPlayerOwned() || !unit.CanAttackGroundPoint) continue;
            if (!unit.ApplyAttackGroundCommand(point)) continue;

            if (sync != null && sync.IsNetworkGame && unit.NetId != 0)
                sync.SendAttackGround(unit.NetId, point);
            issuedCount++;
        }

        if (issuedCount <= 0)
        {
            RTSHUD.Instance?.ShowAlert("当前选择中没有可炮击地点的单位");
            _UiClickAudio.PlayDeny();
            return;
        }

        SpawnCommandMarker(point, new Color(1f, 0.34f, 0.16f));
        RTSHUD.Instance?.ShowAlert(issuedCount == 1 ? "已指定炮击地点" : $"{issuedCount} 个单位已指定炮击地点");
        _UiClickAudio.PlayWarn();
    }

    void IssueAttackUnit(RTSUnit target)
    {
        if (target == null || target.IsDead() || target.IsPlayerOwned()) return;
        int issuedCount = 0;
        foreach (var unit in selectedUnits)
        {
            if (unit == null || unit.IsDead() || !unit.IsPlayerOwned() || !unit.CanAttackUnit(target)) continue;
            unit.ApplyAttackCommand(target);
            if (GameNetworkSync.Instance?.IsNetworkGame == true && unit.NetId != 0)
                GameNetworkSync.Instance.SendAttack(unit.NetId, target.NetId, false);
            issuedCount++;
        }

        if (issuedCount <= 0)
        {
            _UiClickAudio.PlayDeny();
            return;
        }

        SpawnCommandMarker(target.transform.position, new Color(1f, 0.28f, 0.20f));
        _UiClickAudio.PlayWarn();
    }

    void IssueAttackBuilding(RTSBuilding building)
    {
        if (building == null || building.GetHP() <= 0 || building.bPlayerOwned) return;
        SpawnCommandMarker(building.transform.position, new Color(1f, 0.28f, 0.20f));
        _UiClickAudio.PlayWarn();
        foreach (var unit in selectedUnits)
        {
            if (unit == null || unit.IsDead() || !unit.IsPlayerOwned()) continue;
            unit.ApplyAttackBuildingCommand(building);
            if (GameNetworkSync.Instance?.IsNetworkGame == true && unit.NetId != 0)
                GameNetworkSync.Instance.SendAttack(unit.NetId, building.NetId, true);
        }
    }

    bool TryIssueGuardAtPointer(Vector2 screenPos)
    {
        if (!TryPickUnitAtPointer(screenPos, true, out RTSUnit ally))
        {
            _UiClickAudio.PlayDeny();
            return false;
        }

        bool issued = false;
        foreach (var unit in selectedUnits)
        {
            if (unit == null || unit.IsDead() || !unit.IsPlayerOwned() || unit == ally) continue;
            unit.ApplyGuardCommand(ally);
            if (GameNetworkSync.Instance?.IsNetworkGame == true && unit.NetId != 0 && ally.NetId != 0)
                GameNetworkSync.Instance.SendGuard(unit.NetId, ally.NetId);
            issued = true;
        }

        if (issued)
        {
            SpawnCommandMarker(ally.transform.position, new Color(0.6f, 1f, 0.6f));
            _UiClickAudio.PlayConfirm();
        }
        else
        {
            _UiClickAudio.PlayDeny();
        }

        return issued;
    }

    bool TryIssuePatrolAtPointer(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        bool issued = false;
        if (Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
        {
            Vector3 dest = hit.point;
            SpawnCommandMarker(dest, new Color(0.55f, 0.85f, 1f));
            foreach (var u in selectedUnits)
            {
                if (u == null || u.IsDead() || !u.IsPlayerOwned()) continue;
                u.ApplyPatrolCommand(u.transform.position, dest);
                if (GameNetworkSync.Instance?.IsNetworkGame == true && u.NetId != 0)
                    GameNetworkSync.Instance.SendPatrol(u.NetId, u.transform.position, dest);
                issued = true;
            }
        }

        if (issued) _UiClickAudio.PlayConfirm();
        else _UiClickAudio.PlayDeny();
        return issued;
    }

    bool TryPickUnitAtPointer(Vector2 screenPos, bool playerOwned, out RTSUnit picked)
    {
        picked = null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 600f, UnitLayer);
        float bestRayDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RTSUnit candidate = hits[i].collider.GetComponentInParent<RTSUnit>();
            if (!IsPickableUnit(candidate, playerOwned)) continue;
            if (hits[i].distance >= bestRayDistance) continue;
            bestRayDistance = hits[i].distance;
            picked = candidate;
        }
        if (picked != null) return true;

        var units = GameManager.Instance?.GetAllUnits();
        if (units == null) return false;

        float bestScreenDistance = float.PositiveInfinity;
        float toleranceSqr = UnitPickTolerancePixels * UnitPickTolerancePixels;
        for (int i = 0; i < units.Count; i++)
        {
            RTSUnit candidate = units[i];
            if (!IsPickableUnit(candidate, playerOwned)) continue;

            Vector3 screen = cam.WorldToScreenPoint(GetPickCenter(candidate.gameObject, candidate.transform.position));
            if (screen.z <= 0f) continue;

            float dx = screen.x - screenPos.x;
            float dy = screen.y - screenPos.y;
            float d2 = dx * dx + dy * dy;
            if (d2 > toleranceSqr || d2 >= bestScreenDistance) continue;
            bestScreenDistance = d2;
            picked = candidate;
        }

        return picked != null;
    }

    bool TryPickBuildingAtPointer(Vector2 screenPos, bool playerOwned, out RTSBuilding picked)
    {
        picked = null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 600f, BuildingLayer);
        float bestRayDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RTSBuilding candidate = hits[i].collider.GetComponentInParent<RTSBuilding>();
            if (!IsPickableBuilding(candidate, playerOwned)) continue;
            if (hits[i].distance >= bestRayDistance) continue;
            bestRayDistance = hits[i].distance;
            picked = candidate;
        }
        if (picked != null) return true;

        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null) return false;

        float bestScreenDistance = float.PositiveInfinity;
        float toleranceSqr = BuildingPickTolerancePixels * BuildingPickTolerancePixels;
        for (int i = 0; i < buildings.Count; i++)
        {
            RTSBuilding candidate = buildings[i];
            if (!IsPickableBuilding(candidate, playerOwned)) continue;

            Vector3 screen = cam.WorldToScreenPoint(GetPickCenter(candidate.gameObject, candidate.transform.position));
            if (screen.z <= 0f) continue;

            float dx = screen.x - screenPos.x;
            float dy = screen.y - screenPos.y;
            float d2 = dx * dx + dy * dy;
            if (d2 > toleranceSqr || d2 >= bestScreenDistance) continue;
            bestScreenDistance = d2;
            picked = candidate;
        }

        return picked != null;
    }

    bool IsPickableUnit(RTSUnit unit, bool playerOwned)
    {
        return unit != null
            && !unit.IsDead()
            && unit.IsPlayerOwned() == playerOwned
            && IsVisibleForCommands(unit.gameObject);
    }

    bool IsPickableBuilding(RTSBuilding building, bool playerOwned)
    {
        return building != null
            && building.GetHP() > 0
            && building.bPlayerOwned == playerOwned
            && IsVisibleForCommands(building.gameObject);
    }

    bool IsVisibleForCommands(GameObject go)
    {
        if (go == null) return false;
        FogHideable fog = go.GetComponent<FogHideable>();
        return fog == null || fog.IsVisibleForCommands;
    }

    Vector3 GetPickCenter(GameObject go, Vector3 fallback)
    {
        if (go == null) return fallback;

        Bounds bounds = default;
        bool hasBounds = false;
        var colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || col.isTrigger) continue;
            if (!hasBounds) { bounds = col.bounds; hasBounds = true; }
            else bounds.Encapsulate(col.bounds);
        }

        return hasBounds ? bounds.center : fallback;
    }

    bool TryConsumePendingPrimaryTap(Vector2 screenPos)
    {
        if (!_attackGroundPending && !_patrolPending && !_guardPending) return false;
        if (cam == null) cam = Camera.main;
        if (cam == null) return true;

        if (_attackGroundPending)
        {
            TryIssueAttackGroundAtPointer(screenPos);
            _attackGroundPending = false;
            return true;
        }

        if (_guardPending)
        {
            bool issued = TryIssueGuardAtPointer(screenPos);
            _guardPending = false;
            return true;
        }

        if (_patrolPending)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            bool issued = false;
            if (Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
            {
                Vector3 dest = hit.point;
                SpawnCommandMarker(dest, new Color(0.55f, 0.85f, 1f));
                foreach (var u in selectedUnits)
                {
                    if (u == null || u.IsDead()) continue;
                    u.ApplyPatrolCommand(u.transform.position, dest);
                    if (GameNetworkSync.Instance?.IsNetworkGame == true && u.NetId != 0)
                        GameNetworkSync.Instance.SendPatrol(u.NetId, u.transform.position, dest);
                    issued = true;
                }
            }
            _patrolPending = false;
            if (issued) _UiClickAudio.PlayConfirm();
            else _UiClickAudio.PlayDeny();
            return true;
        }

        return false;
    }

    /// <summary>指令反馈圆环：内圈缩小 + 外圈扩散，双层动画。</summary>
    void SpawnCommandMarker(Vector3 worldPos, Color color)
    {
        // 内圈：实心圆盘从 1.6 缩到 0.2 同时淡出
        var inner = MakeMarkerCylinder("MoveMarkerInner", worldPos, color, 0.95f);
        StartCoroutine(AnimateMarker(inner.go, inner.mat, color, 1.6f, 0.2f, 0.55f, 0.95f, 0f));
        // 外圈：从 0.6 扩散到 3.2 同时淡出（波纹）
        var outer = MakeMarkerCylinder("MoveMarkerOuter", worldPos + Vector3.up * 0.005f, color, 0.6f);
        StartCoroutine(AnimateMarker(outer.go, outer.mat, color, 0.6f, 3.2f, 0.6f, 0.6f, 0f));
    }

    struct _Marker { public GameObject go; public Material mat; }
    _Marker MakeMarkerCylinder(string name, Vector3 worldPos, Color color, float startAlpha)
    {
        // 改用 FX 工厂的 Quad+ThickRing 贴图替代 Cylinder（波纹圆环视觉，2 三角形）
        var c = color; c.a = startAlpha;
        var go = FxResources.MakeGroundDisc(null, name, 0.5f, c, FxResources.DiscStyle.ThickRing);
        go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);
        var mat = go.GetComponent<Renderer>().sharedMaterial;
        return new _Marker { go = go, mat = mat };
    }

    System.Collections.IEnumerator AnimateMarker(GameObject go, Material mat, Color baseColor,
        float startScale, float endScale, float duration, float startAlpha, float endAlpha)
    {
        float t = 0f;
        while (t < duration && go != null)
        {
            t += Time.deltaTime;
            float r = Mathf.Clamp01(t / duration);
            // 缓出曲线，开头快后面慢
            float ease = 1f - Mathf.Pow(1f - r, 2.2f);
            float s = Mathf.Lerp(startScale, endScale, ease);
            // Quad 朝上后 X/Y 都是径向，Z=1 不参与缩放
            if (go != null) go.transform.localScale = new Vector3(s, s, 1f);
            if (mat != null)
            {
                var c = baseColor;
                c.a = Mathf.Lerp(startAlpha, endAlpha, r);
                RendererColorUtil.TrySetColor(mat, c);
            }
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_bombingRunPending)
            {
                CancelBombingRunMode();
                return;
            }
            if (_attackGroundPending || _attackMovePending || _patrolPending || _guardPending)
            {
                ClearPendingCommandModes();
                RTSHUD.Instance?.ShowAlert("已取消当前命令");
                _UiClickAudio.PlayClick();
                return;
            }
            if (bInPlacementMode)
            {
                CancelPlacement();
                return;
            }
            RTSHUD.Instance?.TogglePauseMenu();
        }
        if (bInPlacementMode) return;

        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrlHeld && Input.GetKeyDown(KeyCode.A))
        {
            SelectAllOwnedUnits();
            return;
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            SelectAllOwnedAircraft();
            return;
        }

        if (Input.GetKeyDown(KeyCode.X)) // 停止（X 键，避免与相机 S 键冲突）
        {
            StopSelectedUnits();
        }
        if (Input.GetKeyDown(KeyCode.H)) // 全选己方
        {
            SelectAllOwnedUnits();
        }
        // 守卫：选中单位后按 G → 下一次左键友军单位 = 跟随守卫
        if (Input.GetKeyDown(KeyCode.G) && selectedUnits.Count > 0)
        {
            RequestGuardMode();
        }
        if (_guardPending && Input.GetMouseButtonDown(0))
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            _consumedMouse0ThisFrame = true;
#endif
            TryIssueGuardAtPointer(Input.mousePosition);
            _guardPending = false;
        }
        // 攻击移动：选中单位后按 A → 下一次右键点地图设置攻击目标点
        if (Input.GetKeyDown(KeyCode.A) && selectedUnits.Count > 0)
        {
            RequestAttackMoveMode();
        }
        if (Input.GetKeyDown(KeyCode.B) && selectedUnits.Count > 0)
        {
            RequestBombingRunMode();
        }
        // 巡逻：选中单位后按 P → 进入巡逻设置模式，下一次左键点地图设置为 B 点（A 点 = 单位当前位置）
        if (Input.GetKeyDown(KeyCode.P) && selectedUnits.Count > 0)
        {
            RequestPatrolMode();
        }
        if (_patrolPending && Input.GetMouseButtonDown(0))
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            _consumedMouse0ThisFrame = true;
#endif
            if (cam == null) cam = Camera.main;
            Ray pr = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(pr, out RaycastHit ph, 600f, GroundLayer))
            {
                Vector3 dest = ph.point;
                SpawnCommandMarker(dest, new Color(0.55f, 0.85f, 1f));
                _UiClickAudio.PlayConfirm();
                foreach (var u in selectedUnits)
                {
                    if (u == null || u.IsDead()) continue;
                    u.ApplyPatrolCommand(u.transform.position, dest);
                    if (GameNetworkSync.Instance?.IsNetworkGame == true && u.NetId != 0)
                        GameNetworkSync.Instance.SendPatrol(u.NetId, u.transform.position, dest);
                }
            }
            _patrolPending = false;
        }
        // 取消生产：选中己方建筑时按 Backspace/Delete → 取消队列末尾
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
        {
            if (SelectedBuilding != null && SelectedBuilding.bPlayerOwned
                && SelectedBuilding.ProductionQueue != null
                && SelectedBuilding.ProductionQueue.Count > 0)
            {
                SelectedBuilding.CancelLast();
                _UiClickAudio.PlayWarn();
                RTSHUD.Instance?.AppendChatMessage("司令部",
                    "已取消队列末尾生产，退还金币",
                    new Color(1f, 0.62f, 0.28f));
            }
        }
        // 编队（Ctrl+1..5 创建，1..5 选中，双击 1..5 = 选中 + 镜头跳转）
        HandleControlGroups();
        // 建筑放置使用 HUD 建造菜单按钮触发，移动端无键盘快捷键。
    }

    // 全选己方所有单位（H 键 / 底部"全选"按钮共用）
    public void SelectAllOwnedUnits()
    {
        ClearSelectionVisuals();
        selectedUnits.Clear();
        var allUnits = GameManager.Instance?.GetAllUnits();
        if (allUnits == null) return;
        foreach (var u in allUnits)
        {
            if (u != null && !u.IsDead() && u.IsPlayerOwned()) selectedUnits.Add(u);
        }
        SelectedBuilding = null;
        ApplySelectionVisuals(selectedUnits, null);
        RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
        _UiClickAudio.PlayConfirm();
    }

    public void SelectAllOwnedAircraft()
    {
        ClearSelectionVisuals();
        selectedUnits.Clear();
        var allUnits = GameManager.Instance?.GetAllUnits();
        if (allUnits == null) return;
        foreach (var u in allUnits)
        {
            if (u != null && !u.IsDead() && u.IsPlayerOwned() && u is AirUnit) selectedUnits.Add(u);
        }
        SelectedBuilding = null;
        ApplySelectionVisuals(selectedUnits, null);
        RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
        if (selectedUnits.Count > 0)
            _UiClickAudio.PlayConfirm();
        else
            _UiClickAudio.PlayDeny();
    }

    public void SelectOwnedBuilding(RTSBuilding building, bool jumpCamera)
    {
        if (building == null || !building.bPlayerOwned || building.GetHP() <= 0)
            return;

        ClearSelectionVisuals();
        selectedUnits.Clear();
        SelectedBuilding = building;
        building.SetSelected(true);
        RTSHUD.Instance?.OnSelectionChanged(selectedUnits, building);

        if (jumpCamera)
        {
            var rtsCam = FindObjectOfType<RTSCamera>();
            if (rtsCam != null) rtsCam.JumpTo(building.transform.position);
        }
    }

    public void StopSelectedUnits()
    {
        ClearPendingCommandModes();

        if (selectedUnits.Count == 0)
        {
            _UiClickAudio.PlayDeny();
            RTSHUD.Instance?.ShowAlert("请先选择单位");
            return;
        }

        var sync = GameNetworkSync.Instance;
        foreach (var u in selectedUnits)
        {
            if (u == null || u.IsDead()) continue;
            u.ApplyStopCommand();
            if (sync != null && sync.IsNetworkGame && u.NetId != 0)
                sync.SendStop(u.NetId);
        }
        _UiClickAudio.PlayWarn();
    }

    public void ParkSelectedAircraft()
    {
        ClearPendingCommandModes();
        selectedUnits.RemoveAll(u => u == null || u.IsDead() || !u.IsPlayerOwned());

        if (selectedUnits.Count == 0)
        {
            RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
            RTSHUD.Instance?.ShowAlert("请先选择飞机");
            _UiClickAudio.PlayDeny();
            return;
        }

        int aircraftCount = 0;
        int issuedCount = 0;
        var sync = GameNetworkSync.Instance;
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            var air = selectedUnits[i] as AirUnit;
            if (air == null) continue;

            aircraftCount++;
            if (!air.RequestParkAtAirfield()) continue;

            issuedCount++;
            if (sync != null && sync.IsNetworkGame && air.NetId != 0)
                sync.SendPark(air.NetId);
        }

        if (issuedCount > 0)
        {
            RTSHUD.Instance?.ShowAlert(issuedCount == 1 ? "飞机正在返回停机场" : $"{issuedCount}架飞机正在返回停机场");
            _UiClickAudio.PlayConfirm();
        }
        else
        {
            RTSHUD.Instance?.ShowAlert(aircraftCount == 0 ? "请选择飞机" : "没有可停机的飞机");
            _UiClickAudio.PlayDeny();
        }
    }

    void ClearPendingCommandModes()
    {
        _attackMovePending = false;
        _attackGroundPending = false;
        _patrolPending = false;
        _guardPending = false;
        _bombingRunPending = false;
        _bombingRunHasStartPoint = false;
    }

    public void RequestAttackMoveMode()
    {
        if (!RequireSelectedUnits("请先选择单位")) return;
        _attackMovePending = true;
        _attackGroundPending = false;
        _patrolPending = false;
        _guardPending = false;
        RTSHUD.Instance?.AppendChatMessage("司令部",
            "[攻击移动] 点击目标地点", new Color(1f, 0.62f, 0.28f));
        _UiClickAudio.PlayClick();
    }

    public void RequestAttackGroundMode()
    {
        if (!RequireSelectedUnits("请先选择单位")) return;
        if (!HasSelectedAttackGroundUnits())
        {
            RTSHUD.Instance?.ShowAlert("请选择坦克、炮兵或驱逐舰等范围攻击单位");
            _UiClickAudio.PlayDeny();
            return;
        }

        _attackGroundPending = true;
        _attackMovePending = false;
        _patrolPending = false;
        _guardPending = false;
        _bombingRunPending = false;
        _bombingRunHasStartPoint = false;
        bBoxSelectArmed = false;
        RTSHUD.Instance?.AppendChatMessage("司令部",
            "[炮击地点] 点击要持续攻击的位置", new Color(1f, 0.42f, 0.18f));
        RTSHUD.Instance?.ShowAlert("炮击地点：点击地面指定持续攻击点");
        _UiClickAudio.PlayClick();
    }

    public void RequestPatrolMode()
    {
        if (!RequireSelectedUnits("请先选择单位")) return;
        _patrolPending = true;
        _attackMovePending = false;
        _attackGroundPending = false;
        _guardPending = false;
        RTSHUD.Instance?.AppendChatMessage("司令部",
            "[巡逻] 点击巡逻终点", new Color(0.55f, 0.85f, 1f));
        _UiClickAudio.PlayClick();
    }

    public void RequestGuardMode()
    {
        if (!RequireSelectedUnits("请先选择单位")) return;
        _guardPending = true;
        _attackMovePending = false;
        _attackGroundPending = false;
        _patrolPending = false;
        RTSHUD.Instance?.AppendChatMessage("司令部",
            "[守卫] 点击要保护的友军单位", new Color(0.6f, 1f, 0.6f));
        _UiClickAudio.PlayClick();
    }

    public void RequestBombingRunMode()
    {
        if (!RequireSelectedUnits("请先选择单位")) return;
        if (!HasSelectedBombers())
        {
            RTSHUD.Instance?.ShowAlert("当前选择中没有轰炸机");
            _UiClickAudio.PlayDeny();
            return;
        }

        _bombingRunPending = true;
        _bombingRunHasStartPoint = false;
        _attackMovePending = false;
        _attackGroundPending = false;
        _patrolPending = false;
        _guardPending = false;
        bBoxSelectArmed = false;
        RTSHUD.Instance?.AppendChatMessage("司令部",
            "[区域轰炸] 先点轰炸起点，再点终点确定轰炸方向", new Color(1f, 0.72f, 0.28f));
        RTSHUD.Instance?.ShowAlert("区域轰炸：先点起点，再点终点");
        _UiClickAudio.PlayClick();
    }

    public void ArmBoxSelectMode()
    {
        if (bInPlacementMode)
        {
            RTSHUD.Instance?.ShowAlert("建造放置中，无法框选");
            _UiClickAudio.PlayDeny();
            return;
        }

        ClearPendingCommandModes();
        bBoxSelectArmed = true;
        bDragging = false;
        RTSHUD.Instance?.ShowAlert("框选：拖动屏幕选择单位");
        _UiClickAudio.PlayClick();
    }

    bool RequireSelectedUnits(string alert)
    {
        selectedUnits.RemoveAll(u => u == null || u.IsDead() || !u.IsPlayerOwned());
        if (selectedUnits.Count > 0) return true;

        RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
        RTSHUD.Instance?.ShowAlert(alert);
        _UiClickAudio.PlayDeny();
        return false;
    }

    public bool IsAttackMovePending => _attackMovePending;
    public bool IsAttackGroundPending => _attackGroundPending;
    public bool IsPatrolPending => _patrolPending;
    public bool IsGuardPending => _guardPending;
    public bool IsBombingRunPending => _bombingRunPending;
    public bool IsBombingRunDirectionPending => _bombingRunPending && _bombingRunHasStartPoint;
    public bool IsInPlacementMode => bInPlacementMode;
    public bool IsBoxSelectArmed => bBoxSelectArmed;

    // 巡逻待设置标志（按 P 后等待左键点地图）
    private bool _patrolPending = false;
    // 攻击移动待设置标志（按 A 后等待右键点地图）
    private bool _attackMovePending = false;
    // 炮击地点待设置标志（按钮后等待点地图）
    private bool _attackGroundPending = false;
    // 守卫待设置标志（按 G 后等待左键点友军）
    private bool _guardPending = false;
    // 区域轰炸待设置标志（按 B 或轰炸按钮后等待两次地面点选）
    private bool _bombingRunPending = false;
    private bool _bombingRunHasStartPoint = false;
    private Vector3 _bombingRunStartPoint = Vector3.zero;

    // ── 编队系统（红警/星际经典快捷键）──
    private List<RTSUnit>[] _ctrlGroups = new List<RTSUnit>[5];
    private float[] _ctrlGroupLastSelectTime = new float[5];

    void HandleControlGroups()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        for (int i = 0; i < 5; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;
            if (!Input.GetKeyDown(key)) continue;
            if (ctrl)
            {
                // Ctrl+1..5：创建编队 = 当前选中己方单位
                _ctrlGroups[i] = new List<RTSUnit>(selectedUnits.Count);
                for (int k = 0; k < selectedUnits.Count; k++)
                {
                    var u = selectedUnits[k];
                    if (u != null && !u.IsDead() && u.IsPlayerOwned()) _ctrlGroups[i].Add(u);
                }
                if (_ctrlGroups[i].Count > 0)
                {
                    _UiClickAudio.PlayConfirm();
                    RTSHUD.Instance?.AppendChatMessage("司令部",
                        $"已创建 {i + 1} 号编队（{_ctrlGroups[i].Count} 个单位）",
                        new Color(0.55f, 0.95f, 0.55f));
                }
            }
            else
            {
                // 1..5：选中编队；连续按两次 = 镜头跳转到编队中心
                if (_ctrlGroups[i] == null || _ctrlGroups[i].Count == 0) continue;
                _ctrlGroups[i].RemoveAll(u => u == null || u.IsDead() || !u.IsPlayerOwned());
                if (_ctrlGroups[i].Count == 0) continue;
                ClearSelectionVisuals();
                selectedUnits.Clear();
                selectedUnits.AddRange(_ctrlGroups[i]);
                SelectedBuilding = null;
                ApplySelectionVisuals(selectedUnits, null);
                RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
                _UiClickAudio.PlayClick();
                // 0.4s 内连按 → 镜头跳转到编队中心
                if (Time.unscaledTime - _ctrlGroupLastSelectTime[i] < 0.4f)
                {
                    Vector3 center = Vector3.zero;
                    for (int k = 0; k < _ctrlGroups[i].Count; k++) center += _ctrlGroups[i][k].transform.position;
                    center /= _ctrlGroups[i].Count;
                    var rtsCam = FindObjectOfType<RTSCamera>();
                    if (rtsCam != null) rtsCam.JumpTo(center);
                }
                _ctrlGroupLastSelectTime[i] = Time.unscaledTime;
            }
        }
    }

    // 选中光环辅助
    void ClearSelectionVisuals()
    {
        foreach (var u in selectedUnits) if (u) u.SetSelected(false);
        if (SelectedBuilding) SelectedBuilding.SetSelected(false);
    }

    void ApplySelectionVisuals(List<RTSUnit> units, RTSBuilding building)
    {
        if (units != null) foreach (var u in units) if (u) u.SetSelected(true);
        if (building) building.SetSelected(true);
    }

    bool HasSelectedBombers()
    {
        for (int i = 0; i < selectedUnits.Count; i++)
            if (selectedUnits[i] is Bomber)
                return true;
        return false;
    }

    public bool HasSelectedAttackGroundUnits()
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            RTSUnit unit = selectedUnits[i];
            if (unit != null && !unit.IsDead() && unit.IsPlayerOwned() && unit.CanAttackGroundPoint)
                return true;
        }
        return false;
    }

    public bool HasSelectedAircraft()
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            RTSUnit unit = selectedUnits[i];
            if (unit != null && !unit.IsDead() && unit.IsPlayerOwned() && unit is AirUnit)
                return true;
        }
        return false;
    }

    void CancelBombingRunMode(bool notify = true)
    {
        bool hadPending = _bombingRunPending;
        _bombingRunPending = false;
        _bombingRunHasStartPoint = false;
        if (!notify || !hadPending)
            return;

        RTSHUD.Instance?.ShowAlert("已取消区域轰炸");
        _UiClickAudio.PlayClick();
    }

    bool TryIssueBombingRunAtPointer(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
        {
            _UiClickAudio.PlayDeny();
            return false;
        }

        Vector3 point = hit.point;
        point.y = 0f;

        if (!_bombingRunHasStartPoint)
        {
            _bombingRunStartPoint = point;
            _bombingRunHasStartPoint = true;
            SpawnCommandMarker(point, new Color(1f, 0.72f, 0.20f));
            RTSHUD.Instance?.ShowAlert("已锁定起点，请点击终点确定轰炸方向");
            RTSHUD.Instance?.AppendChatMessage("司令部",
                "[区域轰炸] 起点已锁定，请点击终点", new Color(1f, 0.82f, 0.42f));
            _UiClickAudio.PlayClick();
            return true;
        }

        Vector3 delta = point - _bombingRunStartPoint;
        delta.y = 0f;
        if (delta.magnitude < 2f)
        {
            RTSHUD.Instance?.ShowAlert("终点太近，请重新点击更远一点的方向");
            _UiClickAudio.PlayDeny();
            return false;
        }

        IssueBombingRun(_bombingRunStartPoint, point);
        return true;
    }

    void IssueBombingRun(Vector3 start, Vector3 end)
    {
        selectedUnits.RemoveAll(u => u == null || u.IsDead() || !u.IsPlayerOwned());

        var bombers = new List<Bomber>(selectedUnits.Count);
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Bomber bomber = selectedUnits[i] as Bomber;
            if (bomber != null)
                bombers.Add(bomber);
        }

        if (bombers.Count == 0)
        {
            CancelBombingRunMode(false);
            RTSHUD.Instance?.ShowAlert("当前选择中没有可执行轰炸的轰炸机");
            _UiClickAudio.PlayDeny();
            return;
        }

        Vector3 delta = end - start;
        delta.y = 0f;
        Vector3 direction = delta.normalized;
        float length = Mathf.Clamp(delta.magnitude, Bomber.MinBombingRunLength, Bomber.MaxBombingRunLength);
        Vector3 finalEnd = start + direction * length;
        Vector3 perpendicular = Vector3.Cross(Vector3.up, direction).normalized;

        var sync = GameNetworkSync.Instance;
        for (int i = 0; i < bombers.Count; i++)
        {
            Bomber bomber = bombers[i];
            float laneIndex = i - (bombers.Count - 1) * 0.5f;
            Vector3 laneOffset = perpendicular * (laneIndex * Bomber.BombingRunLaneSpacing);
            Vector3 laneStart = start + laneOffset;
            Vector3 laneEnd = finalEnd + laneOffset;

            bomber.ApplyBombingRunCommand(laneStart, laneEnd);
            if (sync != null && sync.IsNetworkGame && bomber.NetId != 0)
                sync.SendBombingRun(bomber.NetId, laneStart, laneEnd);
        }

        SpawnCommandMarker(start, new Color(1f, 0.72f, 0.20f));
        SpawnCommandMarker(finalEnd, new Color(1f, 0.32f, 0.18f));
        RTSHUD.Instance?.ShowAlert(bombers.Count == 1 ? "轰炸机开始执行区域轰炸" : $"{bombers.Count}架轰炸机开始执行区域轰炸");
        RTSHUD.Instance?.AppendChatMessage("司令部",
            $"[区域轰炸] 已下达轰炸航线（{bombers.Count} 架）", new Color(1f, 0.72f, 0.28f));
        _UiClickAudio.PlayWarn();
        CancelBombingRunMode(false);
    }

    // 建筑放置
    public void StartPlacement(GameObject prefab)
    {
        if (prefab == null) return;
        RTSBuilding template = prefab.GetComponent<RTSBuilding>();
        if (template != null && !MainBase.CanConstructBuilding(template, true, out string failureReason))
        {
            RTSHUD.Instance?.ShowAlert(failureReason);
            return;
        }
        if (bInPlacementMode) CancelPlacement(false);
        ClearPendingCommandModes();
        bInPlacementMode = true;
        placementPrefab = prefab;
        placementGhost = Instantiate(prefab);
        var ghostB = placementGhost.GetComponent<RTSBuilding>();
        if (ghostB != null) ghostB.ApplyDefinitionDefaults();
        FootprintBounds2D ghostFootprint = ComputeFootprintBounds2D(placementGhost, 3f);
        placementFootprintRadius = ghostFootprint.Radius;
        placementFootprintSize = ghostFootprint.Size;
        placementFootprintCenter = ghostFootprint.Center;
        // 禁用幽灵的逻辑组件和碰撞体，只保留视觉；防止注册到 GameManager 或干扰地面射线
        if (ghostB != null) ghostB.enabled = false;
        foreach (var ghostCol in placementGhost.GetComponentsInChildren<Collider>(true))
            if (ghostCol != null) ghostCol.enabled = false;
        SetGhostMaterial(placementGhost, true);
        CreatePlacementFootprintMarker();
        CreatePlacementBuildRangeMarker();
        RefreshPlacementOccupiedFootprintMarkers();
        placementGhostHasValidPosition = false;
        placementGhost.SetActive(false);
        if (TryGetPointerScreenPosition(out Vector2 screenPos))
            TryMovePlacementGhostToScreenPoint(screenPos);
        RTSHUD.Instance?.AppendChatMessage("工程部",
            "选择主基地建造范围内的位置，蓝紫色/橙色底板会标出已占地范围，施工中的建筑也会显示，点击地面确认，可用取消按钮或 Esc 退出",
            new Color(0.55f, 0.85f, 1f));
    }

    void UpdatePlacementGhost()
    {
        if (placementGhost == null) return;
        UpdatePlacementBuildRangeMarker();
        UpdatePlacementOccupiedFootprintMarkers();
        if (TryGetPointerScreenPosition(out Vector2 screenPos))
            TryMovePlacementGhostToScreenPoint(screenPos);
        if (!placementGhostHasValidPosition) return;
        // 实时冲突检测：主基地范围、占地半径、地图边界、建筑和地面单位占用
        bool conflict = HasPlacementConflict(placementGhost.transform.position);
        SetGhostTint(placementGhost, conflict);
    }

    bool TryGetPointerScreenPosition(out Vector2 screenPos)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount <= 0)
        {
            screenPos = Vector2.zero;
            return false;
        }
        screenPos = Input.GetTouch(0).position;
        return true;
#else
        screenPos = Input.mousePosition;
        return true;
#endif
    }

    bool TryMovePlacementGhostToScreenPoint(Vector2 screenPos)
    {
        if (placementGhost == null) return false;
        if (cam == null) { cam = Camera.main; if (cam == null) return false; }
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 600f, GroundLayer))
        {
            Vector3 placementPos = hit.point;
            Quaternion placementRot = placementGhost.transform.rotation;
            AlignNavalYardPlacement(ref placementPos, ref placementRot);
            placementGhost.transform.SetPositionAndRotation(placementPos, placementRot);
            placementGhostHasValidPosition = true;
            if (!placementGhost.activeSelf) placementGhost.SetActive(true);
            return true;
        }
        return false;
    }

    bool IsActivePlacementNavalYard()
    {
        return placementGhost != null && placementGhost.GetComponent<NavalYard>() != null;
    }

    void AlignNavalYardPlacement(ref Vector3 pos, ref Quaternion rot)
    {
        if (!IsActivePlacementNavalYard()) return;
        if (!TryGetNearestWaterDirection(pos, out Vector3 waterDir, out _)) return;
        rot = Quaternion.LookRotation(waterDir, Vector3.up);
    }

    bool TryGetNearestWaterDirection(Vector3 pos, out Vector3 waterDir, out float waterDistance)
    {
        Vector3 nearestWater = NavalWaterNavigator.ClampPointToWater(pos, NavalYardWaterProbeInset);
        Vector3 planar = new Vector3(nearestWater.x - pos.x, 0f, nearestWater.z - pos.z);
        waterDistance = planar.magnitude;
        if (waterDistance <= 0.1f)
        {
            waterDir = Vector3.zero;
            return false;
        }

        waterDir = planar / waterDistance;
        return true;
    }

    bool IsNavalYardPlacementValid(Vector3 pos, float radius)
    {
        if (NavalWaterNavigator.IsPointOnWater(pos, NavalYardWaterProbeInset))
            return false;
        if (!TryGetNearestWaterDirection(pos, out Vector3 waterDir, out float waterDistance))
            return false;

        float frontProbeDistance = Mathf.Max(2.8f, radius * NavalYardFrontWaterProbeFactor);
        float backProbeDistance = Mathf.Max(2.1f, radius * NavalYardBackLandProbeFactor);
        float maxShoreDistance = Mathf.Max(frontProbeDistance + 1.2f, radius + 0.75f);
        if (waterDistance > maxShoreDistance)
            return false;

        Vector3 frontProbe = pos + waterDir * frontProbeDistance;
        Vector3 backProbe = pos - waterDir * backProbeDistance;
        if (!NavalWaterNavigator.IsPointOnWater(frontProbe, NavalYardWaterProbeInset))
            return false;
        if (NavalWaterNavigator.IsPointOnWater(backProbe, NavalYardWaterProbeInset))
            return false;

        return true;
    }

    /// <summary>放置位置是否超出主基地范围，或与现有建筑/地图范围冲突。</summary>
    bool HasPlacementConflict(Vector3 pos)
    {
        float radius = Mathf.Max(2.5f, placementFootprintRadius);
        FootprintBounds2D activeFootprint = new FootprintBounds2D
        {
            Center = placementFootprintCenter,
            Size = placementFootprintSize,
            Radius = radius
        };
        FootprintShape2D placementShape = BuildFootprintShape(placementGhost != null ? placementGhost.transform : null, activeFootprint, PlacementFootprintPadding);
        if (placementGhost != null)
        {
            Vector2 delta = new Vector2(pos.x - placementGhost.transform.position.x, pos.z - placementGhost.transform.position.z);
            placementShape.Center += delta;
        }

        if (!IsInsideMainBaseBuildRange(pos, radius)) return true;
        if (IsActivePlacementNavalYard() && !IsNavalYardPlacementValid(pos, radius)) return true;

        var allBuildings = GameManager.Instance?.GetAllBuildings();
        if (allBuildings != null)
        {
            foreach (var b in allBuildings)
            {
                if (b == null || b.GetHP() <= 0 || b.gameObject == placementGhost) continue;
                FootprintBounds2D otherFootprint = ComputeFootprintBounds2D(b.gameObject, 3f);
                FootprintShape2D otherShape = BuildFootprintShape(b.transform, otherFootprint, PlacementFootprintPadding);
                if (FootprintShapesOverlap(placementShape, otherShape)) return true;
            }
        }

        var allUnits = GameManager.Instance?.GetAllUnits();
        if (allUnits != null)
        {
            foreach (var u in allUnits)
            {
                if (u == null || u.IsDead() || u.bFlying) continue;
                float unitRadius = ComputeFootprintRadius(u.gameObject, 0.9f);
                if (FootprintOverlapsCircle(placementShape, u.transform.position, unitRadius + 0.25f)) return true;
            }
        }
        // 地图边界（与 SceneBuilder 地面一致）
        if (!IsFootprintInsideMapBounds(placementShape, 4f)) return true;
        return false;
    }

    bool IsInsideMainBaseBuildRange(Vector3 pos, float footprintRadius)
    {
        if (!TryGetPlayerMainBase(out RTSBuilding mainBase)) return false;
        float allowedCenterRadius = Mathf.Max(0f, MainBaseBuildRadius - Mathf.Max(0f, footprintRadius));
        return PlanarDistance(mainBase.transform.position, pos) <= allowedCenterRadius;
    }

    bool TryGetPlayerMainBase(out RTSBuilding mainBase)
    {
        mainBase = null;
        var gm = GameManager.Instance;
        if (gm == null) return false;

        mainBase = gm.PlayerMainBase;
        if (IsUsablePlayerMainBase(mainBase)) return true;

        mainBase = null;
        var allBuildings = gm.GetAllBuildings();
        if (allBuildings == null) return false;
        foreach (var b in allBuildings)
        {
            if (!IsUsablePlayerMainBase(b)) continue;
            mainBase = b;
            return true;
        }
        return false;
    }

    bool IsUsablePlayerMainBase(RTSBuilding building)
    {
        return building != null
            && building.bIsMainBase
            && building.bPlayerOwned
            && building.GetHP() > 0;
    }

    float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    float ComputeFootprintRadius(GameObject go, float fallback)
    {
        return ComputeFootprintBounds2D(go, fallback).Radius;
    }

    FootprintBounds2D ComputeFootprintBounds2D(GameObject go, float fallbackRadius)
    {
        float safeFallback = Mathf.Max(0.1f, fallbackRadius);
        FootprintBounds2D fallback = new FootprintBounds2D
        {
            Center = Vector2.zero,
            Size = Vector2.one * safeFallback * 2f,
            Radius = safeFallback
        };
        if (go == null) return fallback;

        bool hasBounds = false;
        Bounds localBounds = default;
        Transform root = go.transform;
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            if (col == null || col.isTrigger) continue;
            if (ShouldIgnoreFootprintObject(col.gameObject)) continue;
            if (col is BoxCollider box)
                IncludeBoxColliderBounds(root, box, ref localBounds, ref hasBounds);
            else
                IncludeWorldBoundsAsLocal(root, col.bounds, ref localBounds, ref hasBounds);
        }

        if (!hasBounds)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
                if (ShouldIgnoreFootprintObject(r.gameObject)) continue;
                IncludeWorldBoundsAsLocal(root, r.bounds, ref localBounds, ref hasBounds);
            }
        }

        if (!hasBounds) return fallback;

        float scaleX = Mathf.Max(0.01f, Mathf.Abs(root.lossyScale.x));
        float scaleZ = Mathf.Max(0.01f, Mathf.Abs(root.lossyScale.z));
        float worldWidth = Mathf.Clamp(localBounds.size.x * scaleX, 2.4f, 24f);
        float worldDepth = Mathf.Clamp(localBounds.size.z * scaleZ, 2.4f, 24f);
        return new FootprintBounds2D
        {
            Center = new Vector2(localBounds.center.x, localBounds.center.z),
            Size = new Vector2(worldWidth / scaleX, worldDepth / scaleZ),
            Radius = Mathf.Clamp(Mathf.Sqrt(worldWidth * worldWidth + worldDepth * worldDepth) * 0.5f, 1.2f, 12f)
        };
    }

    void IncludeBoxColliderBounds(Transform root, BoxCollider box, ref Bounds localBounds, ref bool hasBounds)
    {
        if (root == null || box == null) return;
        Vector3 half = box.size * 0.5f;
        for (int ix = -1; ix <= 1; ix += 2)
        for (int iy = -1; iy <= 1; iy += 2)
        for (int iz = -1; iz <= 1; iz += 2)
        {
            Vector3 boxLocal = box.center + new Vector3(half.x * ix, half.y * iy, half.z * iz);
            IncludeLocalFootprintPoint(root.InverseTransformPoint(box.transform.TransformPoint(boxLocal)), ref localBounds, ref hasBounds);
        }
    }

    void IncludeWorldBoundsAsLocal(Transform root, Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        if (root == null || IsEmptyBounds(worldBounds)) return;
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;
        for (int ix = -1; ix <= 1; ix += 2)
        for (int iy = -1; iy <= 1; iy += 2)
        for (int iz = -1; iz <= 1; iz += 2)
        {
            Vector3 worldPoint = new Vector3(c.x + e.x * ix, c.y + e.y * iy, c.z + e.z * iz);
            IncludeLocalFootprintPoint(root.InverseTransformPoint(worldPoint), ref localBounds, ref hasBounds);
        }
    }

    void IncludeLocalFootprintPoint(Vector3 localPoint, ref Bounds localBounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
        }
        else
        {
            localBounds.Encapsulate(localPoint);
        }
    }

    bool ShouldIgnoreFootprintObject(GameObject obj)
    {
        if (obj == null) return true;
        Transform current = obj.transform;
        while (current != null)
        {
            string n = current.name;
            if (n == "SelectionRing" || n == "MinimapDot" || n == "HPLabel" || n == "HealthBar"
                || n == "ProductionBar" || n == "ConstructionDust" || n == "ProductionSteam"
                || n == "DamageSmoke" || n == "PlacementFootprint" || n == "PlacementBuildRange"
                || n == "MainBaseBuildRange" || n == "HealAura" || n == "GroundDisc" || n == "RangeDisc"
                || n == "AoEDisc" || n == "FogDisc" || n == "ScanDisc")
                return true;
            if (n.StartsWith("Label_", System.StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("LabelOutline_", System.StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("OccupiedFootprint_", System.StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Footprint", System.StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.parent;
        }
        return false;
    }

    FootprintShape2D BuildFootprintShape(Transform owner, FootprintBounds2D bounds, float padding)
    {
        if (owner == null)
        {
            return new FootprintShape2D
            {
                Center = Vector2.zero,
                AxisX = Vector2.right,
                AxisZ = Vector2.up,
                HalfSize = bounds.Size * 0.5f + Vector2.one * padding
            };
        }

        Vector3 worldCenter = owner.TransformPoint(new Vector3(bounds.Center.x, 0f, bounds.Center.y));
        Vector3 scale = owner.lossyScale;
        return new FootprintShape2D
        {
            Center = new Vector2(worldCenter.x, worldCenter.z),
            AxisX = PlanarAxis(owner.right, Vector2.right),
            AxisZ = PlanarAxis(owner.forward, Vector2.up),
            HalfSize = new Vector2(
                Mathf.Max(0.1f, bounds.Size.x * Mathf.Abs(scale.x) * 0.5f + padding),
                Mathf.Max(0.1f, bounds.Size.y * Mathf.Abs(scale.z) * 0.5f + padding))
        };
    }

    Vector2 PlanarAxis(Vector3 axis, Vector2 fallback)
    {
        Vector2 planar = new Vector2(axis.x, axis.z);
        return planar.sqrMagnitude > 0.0001f ? planar.normalized : fallback;
    }

    bool FootprintShapesOverlap(FootprintShape2D a, FootprintShape2D b)
    {
        return FootprintOverlapsOnAxis(a, b, a.AxisX)
            && FootprintOverlapsOnAxis(a, b, a.AxisZ)
            && FootprintOverlapsOnAxis(a, b, b.AxisX)
            && FootprintOverlapsOnAxis(a, b, b.AxisZ);
    }

    bool FootprintOverlapsOnAxis(FootprintShape2D a, FootprintShape2D b, Vector2 axis)
    {
        if (axis.sqrMagnitude < 0.0001f) return true;
        axis.Normalize();
        float distance = Mathf.Abs(Vector2.Dot(b.Center - a.Center, axis));
        return distance <= FootprintProjectionRadius(a, axis) + FootprintProjectionRadius(b, axis);
    }

    float FootprintProjectionRadius(FootprintShape2D shape, Vector2 axis)
    {
        return Mathf.Abs(Vector2.Dot(axis, shape.AxisX)) * shape.HalfSize.x
            + Mathf.Abs(Vector2.Dot(axis, shape.AxisZ)) * shape.HalfSize.y;
    }

    bool FootprintOverlapsCircle(FootprintShape2D footprint, Vector3 circleCenterWorld, float circleRadius)
    {
        Vector2 center = new Vector2(circleCenterWorld.x, circleCenterWorld.z);
        Vector2 diff = center - footprint.Center;
        float x = Mathf.Abs(Vector2.Dot(diff, footprint.AxisX));
        float z = Mathf.Abs(Vector2.Dot(diff, footprint.AxisZ));
        float outsideX = Mathf.Max(0f, x - footprint.HalfSize.x);
        float outsideZ = Mathf.Max(0f, z - footprint.HalfSize.y);
        float radius = Mathf.Max(0f, circleRadius);
        return outsideX * outsideX + outsideZ * outsideZ <= radius * radius;
    }

    bool IsFootprintInsideMapBounds(FootprintShape2D footprint, float edgeMargin)
    {
        const float halfX = 200f, halfZ = 200f;
        Vector2 x = footprint.AxisX * footprint.HalfSize.x;
        Vector2 z = footprint.AxisZ * footprint.HalfSize.y;
        return IsFootprintCornerInsideMap(footprint.Center + x + z, halfX, halfZ, edgeMargin)
            && IsFootprintCornerInsideMap(footprint.Center + x - z, halfX, halfZ, edgeMargin)
            && IsFootprintCornerInsideMap(footprint.Center - x + z, halfX, halfZ, edgeMargin)
            && IsFootprintCornerInsideMap(footprint.Center - x - z, halfX, halfZ, edgeMargin);
    }

    bool IsFootprintCornerInsideMap(Vector2 p, float halfX, float halfZ, float edgeMargin)
    {
        return Mathf.Abs(p.x) <= halfX - edgeMargin && Mathf.Abs(p.y) <= halfZ - edgeMargin;
    }

    bool IsEmptyBounds(Bounds bounds)
    {
        return bounds.size.sqrMagnitude < 0.0001f;
    }

    /// <summary>幽灵冲突时切红色，否则蓝色。仅改 _Color，材质模式仍是 SetGhostMaterial 设置的 Transparent。</summary>
    void SetGhostTint(GameObject go, bool conflict)
    {
        Color tint = conflict
            ? new Color(1f, 0.32f, 0.28f, 0.55f)   // 红
            : new Color(0.42f, 0.78f, 1f, 0.48f);  // 蓝
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var mat in r.materials)
                RendererColorUtil.TrySetColor(mat, tint);
        SetPlacementFootprintTint(conflict);
    }

    void CreatePlacementFootprintMarker()
    {
        if (placementGhost == null) return;
        placementFootprintRenderers.Clear();
        Vector2 localSize = ExpandLocalFootprintSize(placementGhost.transform, placementFootprintSize, PlacementFootprintPadding);
        placementFootprintMarker = CreatePlacementFootprintPlate(placementGhost.transform,
            "PlacementFootprint", placementFootprintCenter, localSize,
            new Color(0.13f, 0.92f, 0.62f, 0.30f),
            new Color(0.58f, 1f, 0.82f, 0.72f), 0.055f, placementFootprintRenderers);
        placementFootprintRenderer = placementFootprintRenderers.Count > 0 ? placementFootprintRenderers[0] : null;
        SetPlacementFootprintTint(false);
    }

    Vector2 ExpandLocalFootprintSize(Transform owner, Vector2 localSize, float worldPadding)
    {
        if (owner == null)
            return new Vector2(Mathf.Max(0.4f, localSize.x + worldPadding * 2f),
                Mathf.Max(0.4f, localSize.y + worldPadding * 2f));
        Vector3 scale = owner.lossyScale;
        float scaleX = Mathf.Max(0.01f, Mathf.Abs(scale.x));
        float scaleZ = Mathf.Max(0.01f, Mathf.Abs(scale.z));
        return new Vector2(
            Mathf.Max(0.4f, localSize.x + worldPadding * 2f / scaleX),
            Mathf.Max(0.4f, localSize.y + worldPadding * 2f / scaleZ));
    }

    Vector2 GetWorldFootprintSize(Transform owner, Vector2 localSize)
    {
        if (owner == null) return localSize;
        Vector3 scale = owner.lossyScale;
        return new Vector2(localSize.x * Mathf.Abs(scale.x), localSize.y * Mathf.Abs(scale.z));
    }

    GameObject CreatePlacementFootprintPlate(Transform parent, string name, Vector2 localCenter, Vector2 size,
        Color fillColor, Color edgeColor, float yOffset, List<Renderer> rendererCollector = null)
    {
        GameObject root = new GameObject(name);
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(localCenter.x, yOffset, localCenter.y);
            root.transform.localRotation = Quaternion.identity;
        }

        Vector2 safeSize = new Vector2(Mathf.Max(0.4f, size.x), Mathf.Max(0.4f, size.y));
        Renderer fill = CreatePlacementFootprintQuad(root.transform, "Fill", Vector2.zero, safeSize, fillColor, 0f);
        if (fill != null) rendererCollector?.Add(fill);

        float edge = Mathf.Clamp(Mathf.Min(safeSize.x, safeSize.y) * 0.045f, 0.10f, 0.28f);
        float halfX = safeSize.x * 0.5f - edge * 0.5f;
        float halfZ = safeSize.y * 0.5f - edge * 0.5f;
        AddPlacementFootprintEdge(root.transform, "EdgeN", new Vector2(0f, halfZ), new Vector2(safeSize.x, edge), edgeColor, rendererCollector);
        AddPlacementFootprintEdge(root.transform, "EdgeS", new Vector2(0f, -halfZ), new Vector2(safeSize.x, edge), edgeColor, rendererCollector);
        AddPlacementFootprintEdge(root.transform, "EdgeE", new Vector2(halfX, 0f), new Vector2(edge, safeSize.y), edgeColor, rendererCollector);
        AddPlacementFootprintEdge(root.transform, "EdgeW", new Vector2(-halfX, 0f), new Vector2(edge, safeSize.y), edgeColor, rendererCollector);
        return root;
    }

    void AddPlacementFootprintEdge(Transform parent, string name, Vector2 center, Vector2 size,
        Color color, List<Renderer> rendererCollector)
    {
        Renderer edge = CreatePlacementFootprintQuad(parent, name, center, size, color, 0.006f);
        if (edge != null) rendererCollector?.Add(edge);
    }

    Renderer CreatePlacementFootprintQuad(Transform parent, string name, Vector2 center, Vector2 size, Color color, float yOffset)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Collider col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(center.x, yOffset, center.y);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);

        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreatePlacementFootprintMaterial(color);
        }
        return renderer;
    }

    Material CreatePlacementFootprintMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.mainTexture = Texture2D.whiteTexture;
        RendererColorUtil.TrySetColor(mat, color);
        ApplyTransparentMaterialSettings(mat);
        mat.renderQueue = 3100;
        return mat;
    }

    void CreatePlacementBuildRangeMarker()
    {
        DestroyPlacementBuildRangeMarker();
        if (!TryGetPlayerMainBase(out RTSBuilding mainBase)) return;

        placementBuildRangeMarker = FxResources.MakeGroundDisc(null,
            "MainBaseBuildRange", MainBaseBuildRadius,
            new Color(0.34f, 0.82f, 1f, 0.26f), FxResources.DiscStyle.ThinRing, 0.055f);
        PositionPlacementBuildRangeMarker(mainBase);
    }

    void UpdatePlacementBuildRangeMarker()
    {
        if (!TryGetPlayerMainBase(out RTSBuilding mainBase))
        {
            DestroyPlacementBuildRangeMarker();
            return;
        }

        if (placementBuildRangeMarker == null)
            CreatePlacementBuildRangeMarker();
        else
            PositionPlacementBuildRangeMarker(mainBase);
    }

    void PositionPlacementBuildRangeMarker(RTSBuilding mainBase)
    {
        if (placementBuildRangeMarker == null || mainBase == null) return;
        Vector3 basePos = mainBase.transform.position;
        placementBuildRangeMarker.transform.position = new Vector3(basePos.x, basePos.y + 0.055f, basePos.z);
    }

    void DestroyPlacementBuildRangeMarker()
    {
        if (placementBuildRangeMarker != null)
            Destroy(placementBuildRangeMarker);
        placementBuildRangeMarker = null;
    }

    void UpdatePlacementOccupiedFootprintMarkers()
    {
        placementOccupiedFootprintRefreshTimer -= Time.unscaledDeltaTime;
        if (placementOccupiedFootprintRefreshTimer > 0f) return;
        RefreshPlacementOccupiedFootprintMarkers();
    }

    void RefreshPlacementOccupiedFootprintMarkers()
    {
        DestroyPlacementOccupiedFootprintMarkers();
        placementOccupiedFootprintRefreshTimer = OccupiedFootprintRefreshInterval;
        if (!bInPlacementMode) return;

        var allBuildings = GameManager.Instance?.GetAllBuildings();
        if (allBuildings == null || allBuildings.Count == 0) return;

        for (int i = 0; i < allBuildings.Count; i++)
        {
            RTSBuilding building = allBuildings[i];
            if (!ShouldShowPlacementOccupiedFootprint(building)) continue;

            if (placementOccupiedFootprintRoot == null)
                placementOccupiedFootprintRoot = new GameObject("PlacementOccupiedFootprints");

            FootprintBounds2D footprint = ComputeFootprintBounds2D(building.gameObject, 3f);
            Vector2 worldSize = GetWorldFootprintSize(building.transform, footprint.Size);
            Vector3 markerPos = building.transform.TransformPoint(new Vector3(footprint.Center.x, 0f, footprint.Center.y));
            GameObject markerRoot = new GameObject("OccupiedFootprint_" + building.name);
            markerRoot.transform.SetParent(placementOccupiedFootprintRoot.transform, false);
            markerRoot.transform.SetPositionAndRotation(
                new Vector3(markerPos.x, building.transform.position.y + 0.052f, markerPos.z),
                Quaternion.Euler(0f, building.transform.eulerAngles.y, 0f));

            CreatePlacementFootprintPlate(markerRoot.transform,
                "Plate", Vector2.zero,
                new Vector2(worldSize.x + PlacementFootprintPadding * 2f, worldSize.y + PlacementFootprintPadding * 2f),
                GetPlacementOccupiedFootprintFillColor(building),
                GetPlacementOccupiedFootprintRingColor(building), 0f);

            placementOccupiedFootprintMarkers.Add(markerRoot);
        }
    }

    bool ShouldShowPlacementOccupiedFootprint(RTSBuilding building)
    {
        if (building == null || building.GetHP() <= 0) return false;
        if (building.gameObject == placementGhost) return false;
        FogHideable fog = building.GetComponent<FogHideable>();
        return fog == null || fog.IsVisibleForCommands;
    }

    Color GetPlacementOccupiedFootprintFillColor(RTSBuilding building)
    {
        if (building != null && building.bUnderConstruction)
            return new Color(0.95f, 0.58f, 0.16f, 0.30f);
        if (building != null && !building.bPlayerOwned)
            return new Color(1f, 0.22f, 0.12f, 0.22f);
        if (building != null && building.bIsMainBase)
            return new Color(0.30f, 0.62f, 1f, 0.32f);
        return new Color(0.38f, 0.34f, 1f, 0.28f);
    }

    Color GetPlacementOccupiedFootprintRingColor(RTSBuilding building)
    {
        if (building != null && building.bUnderConstruction)
            return new Color(1f, 0.84f, 0.42f, 0.72f);
        if (building != null && !building.bPlayerOwned)
            return new Color(1f, 0.48f, 0.34f, 0.62f);
        if (building != null && building.bIsMainBase)
            return new Color(0.68f, 0.90f, 1f, 0.70f);
        return new Color(0.78f, 0.72f, 1f, 0.62f);
    }

    void DestroyPlacementOccupiedFootprintMarkers()
    {
        for (int i = 0; i < placementOccupiedFootprintMarkers.Count; i++)
            if (placementOccupiedFootprintMarkers[i] != null)
                Destroy(placementOccupiedFootprintMarkers[i]);
        placementOccupiedFootprintMarkers.Clear();

        if (placementOccupiedFootprintRoot != null)
            Destroy(placementOccupiedFootprintRoot);
        placementOccupiedFootprintRoot = null;
        placementOccupiedFootprintRefreshTimer = 0f;
    }

    void SetPlacementFootprintTint(bool conflict)
    {
        if (placementFootprintRenderers.Count == 0) return;
        Color fill = conflict
            ? new Color(1f, 0.12f, 0.08f, 0.36f)
            : new Color(0.13f, 0.92f, 0.62f, 0.30f);
        Color edge = conflict
            ? new Color(1f, 0.34f, 0.24f, 0.82f)
            : new Color(0.58f, 1f, 0.82f, 0.72f);
        for (int i = 0; i < placementFootprintRenderers.Count; i++)
        {
            Renderer renderer = placementFootprintRenderers[i];
            if (renderer == null) continue;
            Material mat = renderer.material;
            RendererColorUtil.TrySetColor(mat, i == 0 ? fill : edge);
        }
    }

    void ApplyTransparentMaterialSettings(Material mat)
    {
        if (mat == null) return;
        if (!mat.HasProperty("_Mode")) return;
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }

    void ConfirmPlacement(Vector2 screenPos)
    {
        if (placementGhost == null) return;
        if (!TryMovePlacementGhostToScreenPoint(screenPos) || !placementGhostHasValidPosition)
        {
            RTSHUD.Instance?.ShowAlert("请选择有效建造位置");
            return;
        }
        Vector3 pos = placementGhost.transform.position;
        float footprintRadius = Mathf.Max(2.5f, placementFootprintRadius);
        if (!IsInsideMainBaseBuildRange(pos, footprintRadius))
        {
            RTSHUD.Instance?.ShowAlert(TryGetPlayerMainBase(out _) ? "必须在主基地建造范围内" : "主基地已失效，无法建造");
            return;
        }
        if (IsActivePlacementNavalYard() && !IsNavalYardPlacementValid(pos, footprintRadius))
        {
            RTSHUD.Instance?.ShowAlert("船坞必须紧贴岸线建造");
            return;
        }
        // 拦截冲突位置（红色幽灵不允许确认）
        if (HasPlacementConflict(pos))
        {
            RTSHUD.Instance?.ShowAlert("位置冲突！请选择其他位置");
            return;
        }
        RTSBuilding ghost = placementGhost.GetComponent<RTSBuilding>();
        if (ghost != null && !MainBase.CanConstructBuilding(ghost, true, out string limitFailureReason))
        {
            RTSHUD.Instance?.ShowAlert(limitFailureReason);
            return;
        }
        if (ghost != null && playerState != null)
        {
            if (!playerState.SpendGold(ghost.GoldCost)) { RTSHUD.Instance?.ShowAlert("金币不足，无法建造！"); return; }
        }
        Quaternion rot = placementGhost.transform.rotation;
        Destroy(placementGhost);
        placementGhost = null;
        placementFootprintMarker = null;
        placementFootprintRenderer = null;
        placementFootprintRenderers.Clear();
        DestroyPlacementBuildRangeMarker();
        DestroyPlacementOccupiedFootprintMarkers();
        GameObject building = Instantiate(placementPrefab, pos, rot);
        building.SetActive(true);
        RTSBuilding b = building.GetComponent<RTSBuilding>();
        if (b != null)
        {
            b.bPlayerOwned = true;
            b.BeginConstruction();
            if (GameNetworkSync.Instance?.IsNetworkGame == true)
            {
                int nid = NetIdTracker.NextLocalId();
                NetIdTracker.RegisterBuilding(nid, b);
                GameNetworkSync.Instance.SendPlace(
                    placementPrefab.name.Replace("_P","").Replace("(Clone)",""),
                    pos,
                    nid,
                    rot.eulerAngles.y);
            }
        }
        bInPlacementMode = false;
        placementPrefab = null;
        placementGhostHasValidPosition = false;
        placementFootprintRadius = 3f;
        placementFootprintSize = Vector2.one * 6f;
        placementFootprintCenter = Vector2.zero;
    }

    public void CancelPlacement()
    {
        CancelPlacement(true);
    }

    void CancelPlacement(bool notify)
    {
        if (placementGhost != null) { Destroy(placementGhost); placementGhost = null; }
        placementFootprintMarker = null;
        placementFootprintRenderer = null;
        placementFootprintRenderers.Clear();
        DestroyPlacementBuildRangeMarker();
        DestroyPlacementOccupiedFootprintMarkers();
        bool wasInPlacementMode = bInPlacementMode;
        bInPlacementMode = false;
        placementPrefab = null;
        placementGhostHasValidPosition = false;
        placementFootprintRadius = 3f;
        placementFootprintSize = Vector2.one * 6f;
        placementFootprintCenter = Vector2.zero;
        if (notify && wasInPlacementMode)
        {
            _UiClickAudio.PlayWarn();
            RTSHUD.Instance?.ShowAlert("已取消建造");
        }
    }

    void SetGhostMaterial(GameObject go, bool ghost)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in r.materials)
            {
                if (ghost)
                {
                    // 切换到 Transparent 渲染模式（Standard Shader）
                    mat.SetFloat("_Mode", 3);           // 3 = Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                    if (RendererColorUtil.TryGetColor(mat, out Color c))
                    {
                        // 蓝色幽灵色调 + 半透明
                        RendererColorUtil.TrySetColor(mat, new Color(
                            c.r * 0.55f + 0.25f,
                            c.g * 0.55f + 0.35f,
                            c.b * 0.55f + 0.85f, 0.48f));
                    }
                }
                else
                {
                    mat.SetFloat("_Mode", 0);           // 0 = Opaque
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = -1;
                }
            }
        }
    }

    public void OnUnitSpawned(RTSUnit unit)
    {
        if (unit == null) return;
        if (playerState != null) playerState.PopUsed += unit.PopCost;
    }

    public List<RTSUnit> GetSelectedUnits() => selectedUnits;

    // 单位死亡时自动移出选中列表
    public void RemoveFromSelection(RTSUnit unit)
    {
        if (selectedUnits.Remove(unit))
            RTSHUD.Instance?.OnSelectionChanged(selectedUnits, null);
    }
}
