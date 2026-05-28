using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mobile-friendly RTS camera panning. A one-finger drag pans the camera on a
/// horizontal ground plane so the map feels attached to the finger.
/// </summary>
[DefaultExecutionOrder(9990)]
public sealed class MobileCameraTouchPan : MonoBehaviour
{
    public bool EnableTouchPan = true;
    public bool MapFollowsFinger = true;
    public bool IgnoreTouchesOverUI = true;
    public bool SimulateWithMouseInEditor = true;
    public float GroundPlaneY = 0f;
    public float PanMultiplier = 1f;
    public float MaxPanStep = 8f;
    public bool UseWorldBounds = false;
    public Vector2 MinWorldPosition = new Vector2(-80f, -80f);
    public Vector2 MaxWorldPosition = new Vector2(80f, 80f);

    Camera targetCamera;
    bool dragging;
    int activeFingerId = -1;
    Vector3 dragAnchorWorld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindObjectOfType<MobileCameraTouchPan>() != null)
            return;

        var go = new GameObject("MobileCameraTouchPan");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<MobileCameraTouchPan>();
    }

    void LateUpdate()
    {
        if (!EnableTouchPan || !ResolveCamera())
            return;

        if (RTSPlayerController.Instance != null && RTSPlayerController.Instance.IsBoxSelectArmed)
        {
            EndDrag();
            return;
        }

        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (SimulateWithMouseInEditor)
            HandleMouseSimulation();
#endif
    }

    bool ResolveCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = FindObjectOfType<Camera>();
        if (camera == null)
            return false;

        targetCamera = camera;
        return true;
    }

    void HandleTouchInput()
    {
        if (Input.touchCount != 1)
        {
            EndDrag();
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
        {
            BeginDrag(touch.position, touch.fingerId);
            return;
        }

        if (!dragging || touch.fingerId != activeFingerId)
            return;

        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            UpdateDrag(touch.position);
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            EndDrag();
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    void HandleMouseSimulation()
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginDrag(Input.mousePosition, -2);
            return;
        }

        if (!dragging || activeFingerId != -2)
            return;

        if (Input.GetMouseButton(0))
            UpdateDrag(Input.mousePosition);
        else
            EndDrag();
    }
#endif

    void BeginDrag(Vector2 screenPosition, int fingerId)
    {
        if (IgnoreTouchesOverUI && IsPointerOverUI(fingerId))
            return;

        Vector3 worldPoint;
        if (!ScreenToGround(screenPosition, out worldPoint))
            return;

        dragging = true;
        activeFingerId = fingerId;
        dragAnchorWorld = worldPoint;
    }

    void UpdateDrag(Vector2 screenPosition)
    {
        Vector3 currentWorld;
        if (!ScreenToGround(screenPosition, out currentWorld))
            return;

        Vector3 delta = dragAnchorWorld - currentWorld;
        if (!MapFollowsFinger)
            delta = -delta;

        delta *= Mathf.Max(0f, PanMultiplier);
        delta = Vector3.ClampMagnitude(delta, Mathf.Max(0.01f, MaxPanStep));
        targetCamera.transform.position += delta;
        ClampCameraToWorldBounds();

        if (!MapFollowsFinger)
        {
            Vector3 refreshedAnchor;
            if (ScreenToGround(screenPosition, out refreshedAnchor))
                dragAnchorWorld = refreshedAnchor;
        }
    }

    void EndDrag()
    {
        dragging = false;
        activeFingerId = -1;
    }

    bool ScreenToGround(Vector2 screenPosition, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (targetCamera == null)
            return false;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, GroundPlaneY, 0f));
        float distance;
        if (!ground.Raycast(ray, out distance))
            return false;

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null)
            return false;

        if (fingerId >= 0)
            return EventSystem.current.IsPointerOverGameObject(fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }

    void ClampCameraToWorldBounds()
    {
        if (!UseWorldBounds || targetCamera == null)
            return;

        Vector3 position = targetCamera.transform.position;
        position.x = Mathf.Clamp(position.x, MinWorldPosition.x, MaxWorldPosition.x);
        position.z = Mathf.Clamp(position.z, MinWorldPosition.y, MaxWorldPosition.y);
        targetCamera.transform.position = position;
    }
}
