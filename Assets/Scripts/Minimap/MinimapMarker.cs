using UnityEngine;
using UnityEngine.Rendering;

// 小地图标记：挂到单位/建筑，仅在小地图摄像机渲染时可见
[DisallowMultipleComponent]
public class MinimapMarker : MonoBehaviour
{
    [Header("标记外观")]
    public Color  MarkerColor  = Color.green;
    public float  MarkerSize   = 12f;   // 世界单位宽度
    public float  HeightOffset = 80f;   // 相对父物体高度（小地图相机 y=200 可见即可）

    /// <summary>被战雾隐藏时设 true，Marker 在小地图也不会显示。</summary>
    [System.NonSerialized] public bool HiddenByFog = false;

    private GameObject markerObj;
    private MeshRenderer markerRenderer;
    private static readonly System.Collections.Generic.Dictionary<Color, Material> _matCache
        = new System.Collections.Generic.Dictionary<Color, Material>();

    // 小地图摄像机名称（SceneBuilder 里设置）
    private const string MinimapCamName = "MinimapCamera";

    void Awake()
    {
        markerObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        markerObj.name = "MinimapDot";
        Destroy(markerObj.GetComponent<MeshCollider>());

        // 挂在父物体下，跟随移动
        markerObj.transform.SetParent(transform, false);
        markerObj.transform.localPosition = new Vector3(0, HeightOffset, 0);
        markerObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 朝上（面向小地图相机）
        markerObj.transform.localScale    = new Vector3(MarkerSize, MarkerSize, 1f);

        markerRenderer = markerObj.GetComponent<MeshRenderer>();
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows    = false;
        var sharedMat = GetSharedMat(MarkerColor);
        if (sharedMat != null) markerRenderer.material = sharedMat;

        // 默认隐藏，只在小地图相机渲染时显示
        markerRenderer.enabled = false;
        // Built-in 渲染管线
        Camera.onPreRender  += OnCamPreRender;
        Camera.onPostRender += OnCamPostRender;
        // SRP（URP / HDRP）渲染管线
        RenderPipelineManager.beginCameraRendering += OnSrpBeginCamera;
        RenderPipelineManager.endCameraRendering   += OnSrpEndCamera;
    }

    void OnDestroy()
    {
        Camera.onPreRender  -= OnCamPreRender;
        Camera.onPostRender -= OnCamPostRender;
        RenderPipelineManager.beginCameraRendering -= OnSrpBeginCamera;
        RenderPipelineManager.endCameraRendering   -= OnSrpEndCamera;
        if (markerObj) Destroy(markerObj);
    }

    void OnCamPreRender(Camera cam)
    {
        // 只有小地图相机开始渲染时才打开 Renderer，被战雾隐藏的保持关闭
        if (cam.name == MinimapCamName && markerRenderer != null && !HiddenByFog)
            markerRenderer.enabled = true;
    }

    void OnCamPostRender(Camera cam)
    {
        // 小地图相机渲染完后立即关闭 Renderer
        if (cam.name == MinimapCamName && markerRenderer != null)
            markerRenderer.enabled = false;
    }

    void OnSrpBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.name == MinimapCamName && markerRenderer != null && !HiddenByFog)
            markerRenderer.enabled = true;
    }

    void OnSrpEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.name == MinimapCamName && markerRenderer != null)
            markerRenderer.enabled = false;
    }

    // 运行时更换颜色（如单位受伤变红）
    public void SetColor(Color c)
    {
        MarkerColor = c;
        var mr = markerObj?.GetComponent<MeshRenderer>();
        if (mr) mr.material = GetSharedMat(c);
    }

    /// <summary>新单位生产时调用：小地图点放大 + 闪烁数次后回到原状。</summary>
    public void Pulse(float duration = 1.2f, float maxScaleMul = 2.0f)
    {
        if (markerObj == null) return;
        StartCoroutine(PulseCoroutine(duration, maxScaleMul));
    }

    System.Collections.IEnumerator PulseCoroutine(float duration, float maxScaleMul)
    {
        Vector3 baseScale = new Vector3(MarkerSize, MarkerSize, 1f);
        float t = 0f;
        // 用专属材质实例（避免改共享 material）
        var mr = markerObj.GetComponent<MeshRenderer>();
        Material instMat = mr != null ? new Material(GetSharedMat(MarkerColor)) : null;
        if (mr != null && instMat != null) mr.material = instMat;
        while (t < duration && markerObj != null)
        {
            t += Time.deltaTime;
            float r = Mathf.Clamp01(t / duration);
            // 放大 + 高频闪烁
            float pulse = (Mathf.Sin(t * 14f) + 1f) * 0.5f; // 0..1，频率 ~7Hz
            float scaleMul = Mathf.Lerp(maxScaleMul, 1f, r);
            float bright = Mathf.Lerp(1.4f, 1f, r);
            markerObj.transform.localScale = baseScale * scaleMul * Mathf.Lerp(0.8f, 1.2f, pulse);
            if (instMat != null)
            {
                Color c = MarkerColor;
                c.r = Mathf.Clamp01(c.r * bright);
                c.g = Mathf.Clamp01(c.g * bright);
                c.b = Mathf.Clamp01(c.b * bright);
                instMat.color = c;
            }
            yield return null;
        }
        if (markerObj != null)
            markerObj.transform.localScale = baseScale;
        // 恢复共享材质（释放 instance）
        if (mr != null) mr.material = GetSharedMat(MarkerColor);
    }

    static Material GetSharedMat(Color c)
    {
        if (_matCache.TryGetValue(c, out var cached)) return cached;
        var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        if (sh == null) return null;
        var mat = new Material(sh) { color = c };
        _matCache[c] = mat;
        return mat;
    }
}
