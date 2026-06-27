using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏 vignette 红光脉冲：主基地低血量时屏幕边缘红色脉动。
/// 由 RTSHUD 在 Update 中调用 SetState(active, intensity) 控制。
/// </summary>
public class _HudVignettePulse : MonoBehaviour
{
    public Image[] Edges; // 上下左右四条边的 UI Image
    [Tooltip("基础颜色")]
    public Color BaseColor = new Color(1f, 0.18f, 0.12f, 1f);
    [Tooltip("脉动频率")]
    public float Frequency = 1.6f;
    bool active;
    float intensity = 1f;
    float currentAlpha;

    /// <summary>外部每帧调用：active 决定显示，intensity 0~1 控制强度。</summary>
    public void SetState(bool active, float intensity = 1f)
    {
        this.active = active;
        this.intensity = Mathf.Clamp01(intensity);
    }

    void Update()
    {
        float targetAlpha = 0f;
        if (active)
        {
            float pulse = (Mathf.Sin(Time.time * Frequency * Mathf.PI * 2f) + 1f) * 0.5f; // 0~1
            targetAlpha = Mathf.Lerp(0.18f, 0.55f, pulse) * intensity;
        }
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * 2.5f);
        if (Edges != null)
        {
            foreach (var img in Edges)
            {
                if (img == null) continue;
                var c = BaseColor;
                c.a = currentAlpha;
                img.color = c;
                if (img.gameObject.activeSelf != currentAlpha > 0.005f)
                    img.gameObject.SetActive(currentAlpha > 0.005f);
            }
        }
    }

    /// <summary>在指定 Canvas 下创建 4 条边缘渐变 UI Image，返回挂好脚本的对象。</summary>
    public static _HudVignettePulse CreateOnCanvas(Canvas canvas)
    {
        if (canvas == null) return null;
        var holder = new GameObject("_VignetteHolder");
        holder.transform.SetParent(canvas.transform, false);
        var hrt = holder.AddComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = Vector2.zero;
        hrt.offsetMax = Vector2.zero;
        // 让 vignette 永远在最上层但不挡输入
        holder.transform.SetAsLastSibling();

        var pulse = holder.AddComponent<_HudVignettePulse>();
        pulse.Edges = new Image[4];

        // 4 条边的渐变 sprite（程序化生成径向 alpha gradient 太重，简单用纯色长条 + 渐变贴图替代）
        var gradTex = MakeGradientTexture();
        var sprite = Sprite.Create(gradTex, new Rect(0, 0, gradTex.width, gradTex.height), new Vector2(0.5f, 0.5f));

        // top
        pulse.Edges[0] = MakeEdge(holder.transform, "Top", sprite,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -180f), new Vector2(0f, 0f), 0f);
        // bottom
        pulse.Edges[1] = MakeEdge(holder.transform, "Bottom", sprite,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 180f), 180f);
        // left
        pulse.Edges[2] = MakeEdge(holder.transform, "Left", sprite,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(180f, 0f), 90f);
        // right
        pulse.Edges[3] = MakeEdge(holder.transform, "Right", sprite,
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-180f, 0f), new Vector2(0f, 0f), -90f);

        foreach (var e in pulse.Edges) if (e != null) e.gameObject.SetActive(false);
        return pulse;
    }

    static Image MakeEdge(Transform parent, string name, Sprite spr, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, float zRot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        rt.localRotation = Quaternion.Euler(0f, 0f, zRot);
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.type = Image.Type.Simple;
        return img;
    }

    static Texture2D MakeGradientTexture()
    {
        // 1x64 渐变：从内侧透明到外侧实色
        const int w = 1, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1); // 0=内, 1=外
            float a = Mathf.Pow(t, 1.4f);
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }
}
