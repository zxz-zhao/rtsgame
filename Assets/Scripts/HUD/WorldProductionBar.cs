using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 建筑头顶生产进度条（世界空间 Canvas）。空闲时隐藏，有队列时显示。
/// 显示：进度条 + 排队数量（"3"）。仅己方建筑显示。
/// </summary>
public class WorldProductionBar : MonoBehaviour
{
    public Image FillBar;
    public Text QueueText;
    public Image IconBg;
    public Text IconText;

    private Transform target;
    private float barOffset;
    private float targetFillAmount;
    private float displayedFillAmount;
    private bool hasDisplayState;

    private const float FillForwardSpeed = 8f;
    private const float FillResetSpeed = 3.5f;

    public static WorldProductionBar Create(Transform owner, float heightOffset)
    {
        var go = new GameObject("ProductionBar");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(3.2f, 0.7f);  // 更大，俯视清晰可读

        // 黑色外边框
        var border = new GameObject("Border");
        border.transform.SetParent(go.transform, false);
        var bdrRT = border.AddComponent<RectTransform>();
        bdrRT.anchorMin = Vector2.zero; bdrRT.anchorMax = Vector2.one;
        bdrRT.offsetMin = new Vector2(-2.5f, -2.5f);
        bdrRT.offsetMax = new Vector2(2.5f,  2.5f);
        border.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.90f);
        // 金色内边框
        var goldBorder = new GameObject("GoldBorder");
        goldBorder.transform.SetParent(go.transform, false);
        var gbRT = goldBorder.AddComponent<RectTransform>();
        gbRT.anchorMin = Vector2.zero; gbRT.anchorMax = Vector2.one;
        gbRT.offsetMin = new Vector2(-1.0f, -1.0f);
        gbRT.offsetMax = new Vector2(1.0f,  1.0f);
        goldBorder.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f, 0.90f);

        // 背景
        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

        // 填充（蓝色，区别血条），更高占比
        var fill = new GameObject("Fill");
        fill.transform.SetParent(go.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0.12f);
        fillRT.anchorMax = new Vector2(1, 0.88f);
        fillRT.offsetMin = new Vector2(1.5f, 0);
        fillRT.offsetMax = new Vector2(-1.5f, 0);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.30f, 0.78f, 1f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;

        // 队列数量（右上角）
        var qt = new GameObject("Queue");
        qt.transform.SetParent(go.transform, false);
        var qrt = qt.AddComponent<RectTransform>();
        qrt.anchorMin = new Vector2(1f, 0.5f);
        qrt.anchorMax = new Vector2(1f, 0.5f);
        qrt.pivot = new Vector2(0f, 0.5f);
        qrt.anchoredPosition = new Vector2(0.06f, 0f);
        qrt.sizeDelta = new Vector2(0.6f, 0.5f);
        var qText = qt.AddComponent<Text>();
        qText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (qText.font == null) qText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        qText.fontSize = 24;
        qText.alignment = TextAnchor.MiddleLeft;
        qText.color = new Color(1f, 0.92f, 0.55f);
        qText.fontStyle = FontStyle.Bold;
        qText.text = "";
        qText.horizontalOverflow = HorizontalWrapMode.Overflow;
        qText.verticalOverflow = VerticalWrapMode.Overflow;
        qText.transform.localScale = Vector3.one * 0.018f;

        // 单位图标（左侧方块 + 单字标签）
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(1f, 0.5f);
        iconRT.anchoredPosition = new Vector2(-0.06f, 0f);
        iconRT.sizeDelta = new Vector2(0.5f, 0.5f);
        var iconBg = iconGO.AddComponent<Image>();
        iconBg.color = new Color(0.30f, 0.78f, 1f);

        var iconLabelGO = new GameObject("Label");
        iconLabelGO.transform.SetParent(iconGO.transform, false);
        var ilrt = iconLabelGO.AddComponent<RectTransform>();
        ilrt.anchorMin = Vector2.zero; ilrt.anchorMax = Vector2.one;
        ilrt.offsetMin = ilrt.offsetMax = Vector2.zero;
        var iconText = iconLabelGO.AddComponent<Text>();
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (iconText.font == null) iconText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = Color.white;
        iconText.fontStyle = FontStyle.Bold;
        iconText.fontSize = 30;
        iconText.text = "";
        iconText.horizontalOverflow = HorizontalWrapMode.Overflow;
        iconText.verticalOverflow = VerticalWrapMode.Overflow;
        iconText.transform.localScale = Vector3.one * 0.012f;
        var iconOl = iconLabelGO.AddComponent<Outline>();
        iconOl.effectColor = new Color(0f, 0f, 0f, 0.85f);
        iconOl.effectDistance = new Vector2(1.4f, -1.4f);

        var pb = go.AddComponent<WorldProductionBar>();
        pb.FillBar = fillImg;
        pb.QueueText = qText;
        pb.IconBg = iconBg;
        pb.IconText = iconText;
        pb.target = owner;
        pb.barOffset = heightOffset;
        go.SetActive(false);
        return pb;
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        transform.position = target.position + Vector3.up * barOffset;
        // 平铺在地面（X轴90°朝天），Y轴跟随摄像机，从任意俯视角都能正读文字
        Camera cam = Camera.main;
        float camY = cam != null ? cam.transform.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, camY, 0f);
        UpdateFillVisual();
    }

    /// <summary>更新进度（0~1）和排队数。queueCount=0 时隐藏整个条。currentUnitName 用于图标。</summary>
    public void SetProgress(float ratio, int queueCount, string currentUnitName = null)
    {
        if (FillBar == null) return;
        bool show = queueCount > 0;
        if (gameObject.activeSelf != show) gameObject.SetActive(show);
        if (!show)
        {
            targetFillAmount = 0f;
            displayedFillAmount = 0f;
            hasDisplayState = false;
            FillBar.fillAmount = 0f;
            return;
        }

        targetFillAmount = Mathf.Clamp01(ratio);
        if (!hasDisplayState)
        {
            displayedFillAmount = targetFillAmount;
            FillBar.fillAmount = displayedFillAmount;
            hasDisplayState = true;
        }

        if (QueueText != null)
            QueueText.text = queueCount > 1 ? $"x{queueCount}" : "";
        if (IconBg != null && IconText != null)
        {
            (string label, Color tint) = ClassifyUnit(currentUnitName);
            IconBg.color = tint;
            IconText.text = label;
        }
    }

    void UpdateFillVisual()
    {
        if (FillBar == null || !gameObject.activeSelf || !hasDisplayState) return;
        float speed = targetFillAmount < displayedFillAmount ? FillResetSpeed : FillForwardSpeed;
        displayedFillAmount = Mathf.MoveTowards(displayedFillAmount, targetFillAmount, speed * Time.unscaledDeltaTime);
        FillBar.fillAmount = displayedFillAmount;
    }

    /// <summary>按单位 prefab 名称给出图标字符和底色。匹配顺序按特异性从高到低（先窄词后通配）。</summary>
    static (string label, Color color) ClassifyUnit(string unitName)
    {
        if (string.IsNullOrEmpty(unitName)) return ("?", new Color(0.45f, 0.55f, 0.75f));
        string n = unitName.ToLowerInvariant();
        // 特异词优先（避免 bomber 被 plane 抢匹配等）
        if (n.Contains("flamethrower") || n.Contains("flame")) return ("火", new Color(1f, 0.42f, 0.15f));
        if (n.Contains("artillery") || n.Contains("cannon"))   return ("炮", new Color(0.95f, 0.55f, 0.18f));
        if (n.Contains("bomber"))                              return ("轰", new Color(1f, 0.32f, 0.18f));
        if (n.Contains("scout") || n.Contains("recon"))        return ("侦", new Color(0.55f, 0.92f, 0.95f));
        if (n.Contains("infantry") || n.Contains("soldier"))   return ("步", new Color(0.30f, 0.78f, 0.45f));
        if (n.Contains("tank"))                                return ("坦", new Color(1f, 0.55f, 0.20f));
        if (n.Contains("fighter"))                             return ("战", new Color(0.40f, 0.78f, 1f));
        if (n.Contains("air") || n.Contains("plane"))          return ("机", new Color(0.40f, 0.78f, 1f));
        if (n.Contains("heavy") || n.Contains("mech"))         return ("重", new Color(0.95f, 0.30f, 0.30f));
        if (n.Contains("worker") || n.Contains("engineer") || n.Contains("miner")) return ("工", new Color(1f, 0.85f, 0.30f));
        if (n.Contains("hero"))                                return ("雄", new Color(0.85f, 0.45f, 1f));
        // 兜底：取首字
        return (unitName.Substring(0, 1).ToUpper(), new Color(0.55f, 0.65f, 0.85f));
    }
}
