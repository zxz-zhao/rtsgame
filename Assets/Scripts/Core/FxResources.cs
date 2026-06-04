using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 统一的运行时 FX 工厂：
/// 1) 程序生成圆环 / 实心圆 alpha 蒙版（首次缓存），用于地面光圈、阵营圈、治疗光环、点击波纹、飞机投影、放置占地圈等。
/// 2) 提供广告板 Quad（始终面朝相机），用于阵营标识、飞金币等。
/// 3) 尝试加载 Kenney OBJ 模型（仅 Editor / PC build 有效），失败回退由调用方自行处理。
/// 设计目标：替代项目内分散的 GameObject.CreatePrimitive(Cylinder/Sphere) 调用，统一观感、降低三角面数。
/// </summary>
public static class FxResources
{
    public enum DiscStyle
    {
        ThinRing,    // 选择圈（细金边）
        MediumRing,  // 阵营圈、占地圈
        ThickRing,   // 移动点击波纹（外圈）
        SoftDisc,    // 治疗光环、飞机投影（带边缘软过渡的实心圆）
        FullDisc     // 飞金币、阵营小球（完全实心圆 + 一点边缘抗锯齿）
    }

    static readonly Dictionary<DiscStyle, Texture2D> _discTexCache = new Dictionary<DiscStyle, Texture2D>();
    static readonly Dictionary<string, GameObject> _objPrototypeCache = new Dictionary<string, GameObject>();
    static Shader _unlitTransparent;

    static Shader GetUnlitTransparentShader()
    {
        if (_unlitTransparent == null)
        {
            _unlitTransparent = Shader.Find("Sprites/Default")
                                ?? Shader.Find("Unlit/Transparent")
                                ?? Shader.Find("Mobile/Particles/Alpha Blended")
                                ?? Shader.Find("Standard");
        }
        return _unlitTransparent;
    }

    public static Texture2D GetDiscTexture(DiscStyle style)
    {
        if (_discTexCache.TryGetValue(style, out var t) && t != null) return t;
        // 256×256 对 RTS 圆盘足够清晰，4 张共 ~1MB 内存
        const int size = 256;
        float inner, outer, edgeFade;
        switch (style)
        {
            case DiscStyle.ThinRing:
                inner = 0.86f; outer = 0.985f; edgeFade = 0.012f; break;
            case DiscStyle.MediumRing:
                inner = 0.70f; outer = 0.965f; edgeFade = 0.020f; break;
            case DiscStyle.ThickRing:
                inner = 0.55f; outer = 0.97f;  edgeFade = 0.030f; break;
            case DiscStyle.SoftDisc:
                inner = 0f;     outer = 0.92f; edgeFade = 0.18f;  break;
            case DiscStyle.FullDisc:
            default:
                inner = 0f;     outer = 0.96f; edgeFade = 0.04f;  break;
        }
        var tex = BuildDiscMask(size, inner, outer, edgeFade);
        _discTexCache[style] = tex;
        return tex;
    }

    static Texture2D BuildDiscMask(int size, float innerNorm, float outerNorm, float edgeFadeNorm)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        var px = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float maxR = center;
        float inR = innerNorm * maxR;
        float outR = outerNorm * maxR;
        float fade = Mathf.Max(0.5f, edgeFadeNorm * maxR);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                if (innerNorm <= 0f)
                {
                    if (r > outR + fade) a = 0f;
                    else if (r > outR) a = 1f - (r - outR) / fade;
                    else a = 1f;
                }
                else
                {
                    if (r < inR - fade || r > outR + fade) a = 0f;
                    else if (r < inR) a = 1f - (inR - r) / fade;
                    else if (r > outR) a = 1f - (r - outR) / fade;
                    else a = 1f;
                }
                a = Mathf.Clamp01(a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>
    /// 创建朝上贴在地面上的圆形 Quad（透明贴图）。返回的 GameObject 含 MeshRenderer + 独立材质（可继续动态修改颜色）。
    /// 若 parent != null，Quad 作为 parent 的子物体居中、抬高 yOffset。
    /// 若 parent == null，调用方需自行设置 transform.position；Quad 旋转固定为 (90,0,0) 平铺。
    /// </summary>
    public static GameObject MakeGroundDisc(Transform parent, string name, float radius, Color color, DiscStyle style, float yOffset = 0.04f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        }
        // Quad 默认法线 -Z，绕 X 旋转 90° 使其平铺并法线朝上
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        var rd = go.GetComponent<MeshRenderer>();
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rd.receiveShadows = false;
        var mat = new Material(GetUnlitTransparentShader());
        mat.mainTexture = GetDiscTexture(style);
        RendererColorUtil.TrySetColor(mat, color);
        rd.sharedMaterial = mat;
        return go;
    }

    /// <summary>创建广告板 Quad（始终朝向相机）+ 透明贴图（实心圆/圆环）。挂 _FxBillboard 自动旋转。</summary>
    public static GameObject MakeBillboardSprite(Transform parent, string name, float size, Color color, DiscStyle style, Vector3 localOffset)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
        }
        else
        {
            go.transform.position = localOffset;
        }
        go.transform.localScale = new Vector3(size, size, 1f);

        var rd = go.GetComponent<MeshRenderer>();
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rd.receiveShadows = false;
        var mat = new Material(GetUnlitTransparentShader());
        mat.mainTexture = GetDiscTexture(style);
        RendererColorUtil.TrySetColor(mat, color);
        rd.sharedMaterial = mat;
        go.AddComponent<_FxBillboard>();
        return go;
    }

    /// <summary>
    /// 尝试通过 SimpleObjModelLoader 加载 Kenney 包内 OBJ（Editor / PC build 可读 Assets/External 路径）。
    /// 失败返回 null，调用方需要回退到程序化几何。
    /// assetPath 形如 "Assets/External/Kenney/CastleKit/Models/OBJ format/flag-banner-short.obj"
    /// </summary>
    public static GameObject TryInstantiateKenneyObj(string assetPath, Color tint, float tintStrength = 0.55f)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
        string fullPath = Path.Combine(projectRoot, assetPath);
        if (!File.Exists(fullPath)) return null;

        if (!_objPrototypeCache.TryGetValue(assetPath, out var proto) || proto == null)
        {
            var prototype = SimpleObjModelLoader.LoadFromFile(fullPath, null);
            if (prototype == null) return null;
            prototype.name = "__FxObjProto_" + Path.GetFileNameWithoutExtension(assetPath);
            prototype.hideFlags = HideFlags.HideAndDontSave;
            prototype.SetActive(false);
            _objPrototypeCache[assetPath] = prototype;
            proto = prototype;
        }

        var inst = Object.Instantiate(proto);
        inst.hideFlags = HideFlags.None;
        inst.SetActive(true);

        var rends = inst.GetComponentsInChildren<Renderer>(true);
        for (int rIdx = 0; rIdx < rends.Length; rIdx++)
        {
            var r = rends[rIdx];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var m = new Material(mats[i]);
                if (m.HasProperty("_Color"))
                    m.color = Color.Lerp(m.color, tint, tintStrength);
                else if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), tint, tintStrength));
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        return inst;
    }
}

/// <summary>让带此组件的物体在 LateUpdate 始终面朝主摄像机（Quad 法线 -Z 朝向相机）。</summary>
internal class _FxBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 forward = transform.position - cam.transform.position;
        if (forward.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
