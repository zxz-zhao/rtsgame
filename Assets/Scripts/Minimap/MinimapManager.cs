using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

// 小地图管理器：RenderTexture渲染 + 点击跳转摄像机 + 相机视野框
public class MinimapManager : MonoBehaviour
{
    // 渲染期间暂存全局雾设置（minimap camera 距地面 400m 超过 fogEnd 会全黑，渲染时关掉）
    bool _savedFog;
    public static MinimapManager Instance { get; private set; }

    [Header("小地图摄像机")]
    public Camera MinimapCamera;
    public int    TexSize = 256;

    [Header("地图边界（与地面一致）")]
    public float MapHalfX = 200f;
    public float MapHalfZ = 200f;

    [Header("HUD 引用")]
    public RawImage   MinimapImage;     // 显示 RenderTexture 的 UI 图像
    public RectTransform CameraRect;    // 显示当前视野范围的白框

    [Header("主相机（用于视野框）")]
    public Camera MainCam;
    public RTSCamera RtsCam;

    private RenderTexture renderTex;
    private bool ownsRenderTex;
    private readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _raycastResults =
        new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
    private UnityEngine.EventSystems.PointerEventData _pointerEventData;

    [Header("战雾覆盖（小地图）")]
    public bool FogOverlayEnabled = true;
    [Range(32, 128)] public int FogTexSize = 64;
    [Range(0.1f, 1f)] public float FogUpdateInterval = 0.3f;

    private RawImage _fogOverlay;
    private Texture2D _fogTex;
    private Color32[] _fogPixels;
    private byte[] _exploredMax;       // 1 = 曾被探索过；0 = 从未探索
    private float _fogTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 自动查找主相机和RTS相机
        if (MainCam == null) MainCam = Camera.main;
        if (RtsCam == null)  RtsCam  = FindObjectOfType<RTSCamera>();

        if (MinimapCamera == null) return;

        renderTex = MinimapCamera.targetTexture;
        if (renderTex == null && MinimapImage != null)
            renderTex = MinimapImage.texture as RenderTexture;

        if (renderTex == null)
        {
            renderTex = new RenderTexture(TexSize, TexSize, 16, RenderTextureFormat.ARGB32);
            ownsRenderTex = true;
        }

        if (!renderTex.IsCreated())
            renderTex.Create();

        MinimapCamera.targetTexture = renderTex;
        if (MinimapImage) MinimapImage.texture = renderTex;

        // 注册渲染回调：minimap camera 渲染时关闭雾效，渲染完恢复
        Camera.onPreRender  += OnCamPreRender;
        Camera.onPostRender += OnCamPostRender;
        RenderPipelineManager.beginCameraRendering += OnSrpBegin;
        RenderPipelineManager.endCameraRendering   += OnSrpEnd;

        // 视野框样式安全 fallback：场景里 CameraRect 没设或没挂 Image 时，自动构造金色四边描边
        EnsureCameraRectVisuals();
    }

    /// <summary>确保视野框可见：缺失时自动在 MinimapImage 内创建一个 RectTransform，
    /// 并用 4 个细 Image 子节点画出金色矩形边框（中心不填充，避免遮挡战雾/单位标记）。</summary>
    void EnsureCameraRectVisuals()
    {
        if (MinimapImage == null) return;
        if (CameraRect == null)
        {
            var go = new GameObject("CameraRect");
            go.transform.SetParent(MinimapImage.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(40f, 30f);
            CameraRect = rt;
        }
        // 已有边框（之前自动创建过）就跳过
        if (CameraRect.Find("_Edge_Top") != null) return;
        Color edgeColor = new Color(1f, 0.85f, 0.25f, 0.95f);
        const float thick = 1.4f;
        CreateEdgeImage(CameraRect, "_Edge_Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thick), Vector2.zero,                edgeColor);
        CreateEdgeImage(CameraRect, "_Edge_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero,                new Vector2(0f, thick),  edgeColor);
        CreateEdgeImage(CameraRect, "_Edge_Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero,                new Vector2(thick, 0f),  edgeColor);
        CreateEdgeImage(CameraRect, "_Edge_Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thick, 0f),  Vector2.zero,                edgeColor);
    }

    static void CreateEdgeImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    void OnCamPreRender(Camera cam)
    {
        if (cam == MinimapCamera)
        {
            _savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
        }
    }

    void OnCamPostRender(Camera cam)
    {
        if (cam == MinimapCamera)
            RenderSettings.fog = _savedFog;
    }

    void OnSrpBegin(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == MinimapCamera)
        {
            _savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
        }
    }

    void OnSrpEnd(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == MinimapCamera)
            RenderSettings.fog = _savedFog;
    }

    void Update()
    {
        HandleMinimapClick();
        UpdateCameraRect();
        UpdateFogOverlay();
    }

    /// <summary>在小地图 RawImage 内创建一个全覆盖的灰色蒙板 RawImage。</summary>
    void EnsureFogOverlay()
    {
        if (_fogOverlay != null || MinimapImage == null) return;
        var go = new GameObject("FogOverlay");
        go.transform.SetParent(MinimapImage.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var ri = go.AddComponent<RawImage>();
        ri.raycastTarget = false;
        _fogTex = new Texture2D(FogTexSize, FogTexSize, TextureFormat.ARGB32, false);
        _fogTex.filterMode = FilterMode.Bilinear;
        _fogTex.wrapMode = TextureWrapMode.Clamp;
        _fogPixels = new Color32[FogTexSize * FogTexSize];
        _exploredMax = new byte[FogTexSize * FogTexSize];
        // 初始未探索（半透明灰，能透出底图地形轮廓）
        for (int i = 0; i < _fogPixels.Length; i++) _fogPixels[i] = new Color32(0, 0, 0, 150);
        _fogTex.SetPixels32(_fogPixels);
        _fogTex.Apply();
        ri.texture = _fogTex;
        _fogOverlay = ri;
    }

    /// <summary>每 0.3s 重绘小地图战雾蒙板，反映友军视野范围。</summary>
    void UpdateFogOverlay()
    {
        if (!FogOverlayEnabled) { if (_fogOverlay != null) _fogOverlay.enabled = false; return; }
        EnsureFogOverlay();
        if (_fogOverlay == null) return;
        _fogOverlay.enabled = true;

        _fogTimer -= Time.deltaTime;
        if (_fogTimer > 0f) return;
        _fogTimer = FogUpdateInterval;

        // 收集友军视野源（位置 XZ + 半径²）
        var srcs = new System.Collections.Generic.List<Vector3>(); // x=wx, y=wz, z=r²
        var gm = GameManager.Instance;
        if (gm == null) return;
        var units = gm.GetAllUnits();
        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || u.IsDead() || !u.IsPlayerOwned()) continue;
                float r = Mathf.Max(8f, u.SightRange);
                srcs.Add(new Vector3(u.transform.position.x, u.transform.position.z, r * r));
            }
        }
        var blds = gm.GetAllBuildings();
        if (blds != null)
        {
            for (int i = 0; i < blds.Count; i++)
            {
                var b = blds[i];
                if (b == null || b.GetHP() <= 0 || !b.bPlayerOwned) continue;
                float r = b.bIsMainBase ? 28f : (b.bIsGoldMine ? 12f : 16f);
                srcs.Add(new Vector3(b.transform.position.x, b.transform.position.z, r * r));
            }
        }

        int N = FogTexSize;
        float worldW = MapHalfX * 2f, worldH = MapHalfZ * 2f;
        // 视野边缘软化范围：当 dist² / r² 在 1.0 ~ EdgeFalloffEnd² 之间时，alpha 从 0 渐变到目标值，
        // 给小地图战雾一个柔和过渡而不是阶梯硬边。
        const float EdgeFalloffEnd = 1.40f;          // r 的 1.40 倍处完全 fade 到目标值
        const float EdgeFalloffEndSq = EdgeFalloffEnd * EdgeFalloffEnd;
        for (int yy = 0; yy < N; yy++)
        {
            float wz = ((yy + 0.5f) / N - 0.5f) * worldH;
            for (int xx = 0; xx < N; xx++)
            {
                float wx = ((xx + 0.5f) / N - 0.5f) * worldW;
                // 找最近视野源的"半径化距离平方"：minRatio = min over s of (d² / r²)
                float minRatio = float.MaxValue;
                for (int s = 0; s < srcs.Count; s++)
                {
                    float dx = wx - srcs[s].x, dz = wz - srcs[s].y;
                    float ratio = (dx * dx + dz * dz) / srcs[s].z;
                    if (ratio < minRatio) minRatio = ratio;
                }

                int idx = yy * N + xx;
                byte newAlpha;
                if (minRatio <= 1f)
                {
                    // 在视野范围内
                    newAlpha = 0;
                    _exploredMax[idx] = 1;
                }
                else if (minRatio < EdgeFalloffEndSq)
                {
                    // 视野边缘软化带：用 sqrt 把 ratio² 映射到线性 t∈[0,1]
                    float t = (Mathf.Sqrt(minRatio) - 1f) / (EdgeFalloffEnd - 1f);
                    byte target = _exploredMax[idx] == 1 ? (byte)90 : (byte)150;
                    newAlpha = (byte)(t * target);
                    if (t < 0.5f) _exploredMax[idx] = 1; // 接近视野（< 1.2r）也记为 explored
                }
                else if (_exploredMax[idx] == 1) newAlpha = 90;   // 曾探索：淡灰，地形清晰
                else newAlpha = 150;                              // 未探索：中灰，仍能看出地形轮廓
                _fogPixels[idx] = new Color32(0, 0, 0, newAlpha);
            }
        }
        _fogTex.SetPixels32(_fogPixels);
        _fogTex.Apply();
    }

    // 点击小地图 → 主相机跳转
    void HandleMinimapClick()
    {
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButton(0)) return;
        if (MinimapImage == null || RtsCam == null) return;

        // 只有小地图自身被点击时才跳转（避免其他 UI 按鈕触发）
        if (EventSystem.current != null)
        {
            if (_pointerEventData == null) _pointerEventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
            _pointerEventData.position = Input.mousePosition;
            _raycastResults.Clear();
            EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);
            bool overMinimap = false;
            foreach (var r in _raycastResults)
                if (r.gameObject == MinimapImage.gameObject) { overMinimap = true; break; }
            if (!overMinimap) return;
        }

        RectTransform rt = MinimapImage.rectTransform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, Input.mousePosition, null, out local)) return;

        Rect rect = rt.rect;
        if (!rect.Contains(local)) return;

        float nx = (local.x - rect.xMin) / rect.width;
        float nz = (local.y - rect.yMin) / rect.height;

        Vector3 worldPos = new Vector3(
            (nx - 0.5f) * MapHalfX * 2f,
            0f,
            (nz - 0.5f) * MapHalfZ * 2f
        );
        RtsCam.JumpTo(worldPos);
    }

    // 在小地图上绘制当前相机视野框（白色矩形）—— 用视锥体四角投影到地面
    void UpdateCameraRect()
    {
        if (CameraRect == null || MainCam == null || MinimapImage == null) return;

        Rect mmRect = MinimapImage.rectTransform.rect;
        float pw = mmRect.width, ph = mmRect.height;

        // 将屏幕四个角的射线投影到 y=0 地面
        Vector3[] viewport = {
            new Vector3(0,0,0), new Vector3(1,0,0),
            new Vector3(1,1,0), new Vector3(0,1,0)
        };
        float minNX = 1f, maxNX = 0f, minNZ = 1f, maxNZ = 0f;
        bool anyHit = false;
        foreach (var vp in viewport)
        {
            Ray ray = MainCam.ViewportPointToRay(vp);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) continue;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0) continue;
            Vector3 hit = ray.origin + ray.direction * t;
            float nx = Mathf.Clamp01(hit.x / (MapHalfX * 2f) + 0.5f);
            float nz = Mathf.Clamp01(hit.z / (MapHalfZ * 2f) + 0.5f);
            minNX = Mathf.Min(minNX, nx); maxNX = Mathf.Max(maxNX, nx);
            minNZ = Mathf.Min(minNZ, nz); maxNZ = Mathf.Max(maxNZ, nz);
            anyHit = true;
        }
        if (!anyHit) return;

        float cNX = (minNX + maxNX) * 0.5f;
        float cNZ = (minNZ + maxNZ) * 0.5f;
        CameraRect.sizeDelta      = new Vector2((maxNX - minNX) * pw, (maxNZ - minNZ) * ph);
        CameraRect.anchoredPosition = new Vector2((cNX - 0.5f) * pw, (cNZ - 0.5f) * ph);
    }

    void OnDestroy()
    {
        Camera.onPreRender  -= OnCamPreRender;
        Camera.onPostRender -= OnCamPostRender;
        RenderPipelineManager.beginCameraRendering -= OnSrpBegin;
        RenderPipelineManager.endCameraRendering   -= OnSrpEnd;
        if (ownsRenderTex && renderTex != null)
        {
            renderTex.Release();
            Destroy(renderTex);
        }
        if (_fogTex != null) Destroy(_fogTex);
    }
}
