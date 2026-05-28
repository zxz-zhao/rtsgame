using UnityEngine;
using UnityEngine.UI;

// 单位/建筑头顶血条（世界空间Canvas，始终朝向摄像机）
public class WorldHealthBar : MonoBehaviour
{
    [Header("绑定")]
    public Image   FillBar;     // 绿色血量填充
    public Image   BgBar;       // 灰色背景

    private Transform target;
    private Camera    mainCam;
    private float     barOffset = 3f;  // 高于单位中心多少单位
    private Color     _fillBaseColor = Color.white; // SetHP 设定的基色，脉动时乘到该基色上

    public static WorldHealthBar Create(Transform owner, float heightOffset = 3f)
    {
        // 创建世界空间 Canvas
        var go = new GameObject("HealthBar");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2f, 0.3f); // 世界单位：2宽 0.3高

        // 黑色外边框 + 金色装饰内边框（军用风格）
        var border = new GameObject("Border");
        border.transform.SetParent(go.transform, false);
        var bdrRT = border.AddComponent<RectTransform>();
        bdrRT.anchorMin = Vector2.zero; bdrRT.anchorMax = Vector2.one;
        bdrRT.offsetMin = new Vector2(-2f, -2f);
        bdrRT.offsetMax = new Vector2(2f, 2f);
        border.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
        var goldBorder = new GameObject("GoldBorder");
        goldBorder.transform.SetParent(go.transform, false);
        var gbRT = goldBorder.AddComponent<RectTransform>();
        gbRT.anchorMin = Vector2.zero; gbRT.anchorMax = Vector2.one;
        gbRT.offsetMin = new Vector2(-0.7f, -0.7f);
        gbRT.offsetMax = new Vector2(0.7f, 0.7f);
        goldBorder.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f, 0.78f);

        // 背景
        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);

        // 填充
        var fill = new GameObject("Fill");
        fill.transform.SetParent(go.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0.12f);
        fillRT.anchorMax = new Vector2(1, 0.88f);
        fillRT.offsetMin = new Vector2(1.5f, 0);
        fillRT.offsetMax = new Vector2(-1.5f, 0);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.85f, 0.2f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        // 50% 中间刻度线
        var mid = new GameObject("MidMark");
        mid.transform.SetParent(go.transform, false);
        var midRT = mid.AddComponent<RectTransform>();
        midRT.anchorMin = new Vector2(0.5f, 0.05f);
        midRT.anchorMax = new Vector2(0.5f, 0.95f);
        midRT.offsetMin = new Vector2(-0.8f, 0);
        midRT.offsetMax = new Vector2(0.8f, 0);
        mid.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        var hb = go.AddComponent<WorldHealthBar>();
        hb.FillBar    = fillImg;
        hb.BgBar      = bgImg;
        hb.target     = owner;
        hb.barOffset  = heightOffset;
        hb.mainCam    = Camera.main;
        go.SetActive(false);
        return hb;
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        if (mainCam == null) mainCam = Camera.main;

        // 跟随目标
        transform.position = target.position + Vector3.up * barOffset;

        // 始终朝向摄像机（Billboard）
        if (mainCam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);

        // 低血量脉动：血量 < 25% 时血条亮度周期闪烁。乘到 _fillBaseColor 上，
        // 避免「每帧再乘一遍」导致颜色单调变暗（旧代码 bug）。
        if (FillBar != null)
        {
            if (FillBar.fillAmount < 0.25f)
            {
                float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 6f);
                FillBar.color = new Color(_fillBaseColor.r * pulse,
                                          _fillBaseColor.g * pulse,
                                          _fillBaseColor.b * pulse,
                                          _fillBaseColor.a);
            }
            else if (FillBar.color != _fillBaseColor)
            {
                FillBar.color = _fillBaseColor; // 脉动结束恢复基色
            }
        }
    }

    // 更新血量（0~1）
    public void SetHP(float ratio, bool isEnemy = false)
    {
        if (FillBar == null) return;
        float clamped = Mathf.Clamp01(ratio);
        FillBar.fillAmount = clamped;

        // 颜色：高血量绿色 → 低血量红色（己方）/ 橙红 → 暗红（敌方）
        Color c;
        if (isEnemy)
            c = ratio > 0.5f
                ? Color.Lerp(new Color(0.9f, 0.35f, 0.1f), new Color(0.95f, 0.65f, 0.1f), (ratio - 0.5f) / 0.5f)
                : Color.Lerp(new Color(0.55f, 0.05f, 0.05f), new Color(0.9f, 0.35f, 0.1f), ratio / 0.5f);
        else
            c = ratio > 0.55f
                ? Color.Lerp(new Color(0.85f, 0.82f, 0.1f), new Color(0.15f, 0.85f, 0.35f), (ratio - 0.55f) / 0.45f)
                : Color.Lerp(new Color(0.9f, 0.18f, 0.18f), new Color(0.85f, 0.82f, 0.1f), ratio / 0.55f);
        FillBar.color = c;
        _fillBaseColor = c;

        // 满血时隐藏
        gameObject.SetActive(clamped < 0.999f);
    }
}
