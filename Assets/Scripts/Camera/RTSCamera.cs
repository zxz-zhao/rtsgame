using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// RTS俯视角相机：边缘滚动 + 触摸拖动
public class RTSCamera : MonoBehaviour
{
    [Header("移动")]
    public float MoveSpeed = 35f;
    public float EdgeScrollWidth = 30f;  // 像素
    // 相机本身坐标的可移动范围。考虑到俯视相机往 +z 方向看，
    // 当相机自身在 z=-200 时（地图南端），由于俯视角，画面中心实际看到的是更靠 +z 的区域。
    // 因此 MinZ 需比地图边界 -200 再往下扩 ~60，否则看不到地图最南端。
    public float MinX = -210f, MaxX = 210f;
    public float MinZ = -260f, MaxZ = 200f;

    [Header("缩放")]
    public float ZoomSpeed = 5f;
    public float WheelZoomMultiplier = 1.2f;
    public float MinY = 42f, MaxY = 125f;
    [Header("默认观感")]
    public float DefaultPitch = 54f;
    public float DefaultFieldOfView = 45f;

    private Vector2 lastTouchPos;
    private Vector2 touchStartPos;
    private bool bTouchDrag = false;
    private const float DragThreshold = 22f;  // 像素，超过才算拖拽移动相机

    // ── 镜头震动 ──────────────────────────────────────────────
    public static RTSCamera Instance { get; private set; }
    private Vector3 _shakeOffset;
    private bool _shaking;

    void Awake()
    {
        Instance = this;
        ApplyDefaultViewTuning();
    }

    void ApplyDefaultViewTuning()
    {
        var cam = GetComponent<Camera>();
        if (cam != null && !cam.orthographic)
            cam.fieldOfView = DefaultFieldOfView;

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(DefaultPitch, euler.y, euler.z);
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        HandleTouchInput();
        HandleKeyboardMove();
#else
        HandleMouseInput();
#endif
        HandleWheelZoom();
        ClampPosition();
        if (_shaking) transform.position += _shakeOffset;
    }

    // magnitude: 最大偏移幅度（世界单位）；duration: 持续时间（秒）
    public static void Shake(float magnitude = 0.6f, float duration = 0.22f)
    {
        if (Instance != null) Instance.StartCoroutine(Instance.DoShake(magnitude, duration));
    }

    System.Collections.IEnumerator DoShake(float mag, float dur)
    {
        if (_shaking) yield break;  // 防止叠加
        _shaking = true;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = 1f - elapsed / dur;
            _shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * mag * t,
                Random.Range(-0.3f, 0.3f) * mag * t,
                Random.Range(-1f, 1f) * mag * t);
            yield return null;
        }
        _shakeOffset = Vector3.zero;
        _shaking = false;
    }

    void HandleMouseInput()
    {
        Vector3 move = Vector3.zero;
        Vector2 mp = Input.mousePosition;

        // 边缘滚动（鼠标悬停在 UI 上时不触发）
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!overUI)
        {
            if (mp.x < EdgeScrollWidth) move.x = -1;
            else if (mp.x > Screen.width - EdgeScrollWidth) move.x = 1;
            if (mp.y < EdgeScrollWidth) move.z = -1;
            else if (mp.y > Screen.height - EdgeScrollWidth) move.z = 1;
        }

        // WASD（InputField 有焦点时不响应，避免打字时移动相机）
        bool inputFocused = EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
        if (!inputFocused)
        {
            // 注意：A 键留给"攻击移动"快捷键，相机左移用 Q 或 ←
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z = 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z = -1;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) move.x = -1;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x = 1;
        }

        transform.Translate(move * MoveSpeed * Time.deltaTime, Space.World);

        // 鼠标滚轮不参与平移；缩放统一交给 HandleWheelZoom 处理。
    }

    void HandleKeyboardMove()
    {
        if (IsTextInputFocused())
            return;

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z = 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z = -1;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) move.x = -1;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x = 1;

        if (move.sqrMagnitude > 0.01f)
            transform.Translate(move.normalized * MoveSpeed * Time.deltaTime, Space.World);
    }

    bool IsTextInputFocused()
    {
        return EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
    }

    void HandleWheelZoom()
    {
        if (IsPointerOverUI())
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
            scroll = Input.GetAxis("Mouse ScrollWheel") * 10f;
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        Camera cam = GetComponent<Camera>();
        if (cam != null && cam.orthographic)
        {
            float minSize = Mathf.Max(2f, MinY);
            float maxSize = Mathf.Max(minSize, MaxY);
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * ZoomSpeed, minSize, maxSize);
            return;
        }

        Vector3 direction = transform.forward;
        float distance = scroll * ZoomSpeed * WheelZoomMultiplier;
        Vector3 current = transform.position;
        Vector3 target = current + direction * distance;

        if (target.y < MinY || target.y > MaxY)
        {
            float targetY = Mathf.Clamp(target.y, MinY, MaxY);
            if (Mathf.Abs(direction.y) > 0.001f)
                target = current + direction * ((targetY - current.y) / direction.y);
            else
                target.y = targetY;
        }

        target.x = Mathf.Clamp(target.x, MinX, MaxX);
        target.z = Mathf.Clamp(target.z, MinZ, MaxZ);
        transform.position = target;
    }

    bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void HandleTouchInput()
    {
        if (RTSPlayerController.Instance != null && RTSPlayerController.Instance.IsBoxSelectArmed)
        {
            bTouchDrag = false;
            return;
        }

        if (Input.touchCount == 1)
        {
            if (RTSPlayerController.Instance != null && RTSPlayerController.Instance.IsInPlacementMode)
            {
                bTouchDrag = false;
                return;
            }

            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                lastTouchPos  = t.position;
                touchStartPos = t.position;
                bTouchDrag    = false;  // 还未确认是拖拽
            }
            else if (t.phase == TouchPhase.Moved)
            {
                // 超过阈值才开始移动相机，避免与短促点击单位/下令冲突
                if (!bTouchDrag && Vector2.Distance(t.position, touchStartPos) > DragThreshold)
                    bTouchDrag = true;

                if (bTouchDrag)
                {
                    Vector2 delta = t.position - lastTouchPos;
                    Vector3 move = new Vector3(-delta.x, 0, -delta.y) * MoveSpeed * Time.deltaTime * 0.05f;
                    transform.Translate(move, Space.World);
                }
                lastTouchPos = t.position;
            }
            else if (t.phase == TouchPhase.Ended)
            {
                bTouchDrag = false;
            }
        }
        else if (Input.touchCount == 2)
        {
            // 双指：中心点平移 + 捏合缩放
            Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);

            // 中心平移
            Vector2 prevCenter = ((t0.position - t0.deltaPosition) + (t1.position - t1.deltaPosition)) * 0.5f;
            Vector2 currCenter = (t0.position + t1.position) * 0.5f;
            Vector2 panDelta   = currCenter - prevCenter;
            Vector3 move2 = new Vector3(-panDelta.x, 0, -panDelta.y) * MoveSpeed * Time.deltaTime * 0.05f;
            transform.Translate(move2, Space.World);

            // 捏合缩放
            float prevDist = Vector2.Distance(t0.position - t0.deltaPosition, t1.position - t1.deltaPosition);
            float currDist = Vector2.Distance(t0.position, t1.position);
            float diff = currDist - prevDist;
            Vector3 pos = transform.position;
            pos.y -= diff * ZoomSpeed * 0.02f;
            pos.y = Mathf.Clamp(pos.y, MinY, MaxY);
            transform.position = pos;
        }
    }

    void ClampPosition()
    {
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, MinX, MaxX);
        p.z = Mathf.Clamp(p.z, MinZ, MaxZ);
        transform.position = p;
    }

    public void JumpTo(Vector3 target)
    {
        transform.position = new Vector3(target.x, transform.position.y, target.z - 10f);
    }
}
