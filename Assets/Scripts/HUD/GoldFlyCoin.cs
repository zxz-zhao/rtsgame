using UnityEngine;

/// <summary>
/// 金币飞行动画：金矿产出时从矿位飞向主基地，使用贝塞尔曲线（带高度弧线）。
/// 飞行结束时自毁；不需要外部资源（程序化生成黄色八面体）。
/// </summary>
public class GoldFlyCoin : MonoBehaviour
{
    Vector3 from;
    Vector3 to;
    Vector3 control;
    float duration;
    float t;
    Transform follow;          // 终点跟随（基地可能移动？保险）
    Material mat;
    Vector3 baseScale;

    public static void Spawn(Vector3 fromWorld, Transform target, float dur = 0.85f)
    {
        if (target == null) return;
        // 用 FX 工厂的 Billboard Sprite（实心圆 alpha 贴图，始终面朝相机）替代 Sphere
        Vector3 startPos = fromWorld + Vector3.up * 1.2f;
        var go = FxResources.MakeBillboardSprite(null, "GoldFlyCoin", 0.85f,
            new Color(1f, 0.85f, 0.18f, 1f), FxResources.DiscStyle.FullDisc, startPos);
        var mat = go.GetComponent<Renderer>().sharedMaterial;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.9f, 0.65f, 0.05f) * 0.6f);
        }

        var c = go.AddComponent<GoldFlyCoin>();
        c.from = go.transform.position;
        c.to = target.position + Vector3.up * 2f;
        Vector3 mid = (c.from + c.to) * 0.5f;
        float arcHeight = Mathf.Max(4f, Vector3.Distance(c.from, c.to) * 0.25f);
        c.control = mid + Vector3.up * arcHeight;
        c.duration = Mathf.Max(0.2f, dur);
        c.follow = target;
        c.mat = mat;
        c.baseScale = go.transform.localScale;
    }

    void Update()
    {
        t += Time.deltaTime;
        float r = Mathf.Clamp01(t / duration);
        // 终点跟踪基地
        if (follow != null) to = follow.position + Vector3.up * 2f;
        // 二次贝塞尔
        Vector3 p01 = Vector3.Lerp(from, control, r);
        Vector3 p12 = Vector3.Lerp(control, to, r);
        Vector3 pos = Vector3.Lerp(p01, p12, r);
        transform.position = pos;
        // Billboard 模式下 rotation 由 _FxBillboard 在 LateUpdate 覆盖，自旋无意义，已移除
        // 末端缩放收缩 + 闪烁
        if (r > 0.78f)
        {
            float k = 1f - (r - 0.78f) / 0.22f;
            transform.localScale = baseScale * k;
        }
        if (r >= 1f)
        {
            // 抵达基地：弹一个金光提示
            EffectsManager.PlayLevelUp(transform.position);
            Destroy(gameObject);
        }
    }
}
