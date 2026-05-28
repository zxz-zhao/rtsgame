using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 按钮点击/悬停反馈组件。挂在任意带 Button 的 GameObject 上，提供：
///  - 悬停：轻微缩放 + 暖色光晕（透明按钮也能看出区域）
///  - 按下：弹性缩放（0.96 然后回弹到 1.0）
/// 不需要资源，纯 RectTransform 缩放和子节点 Image 透明度。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonClickPulse : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("悬停时缩放比例")]
    public float HoverScale = 1.04f;
    [Tooltip("按下时回弹的最低缩放")]
    public float PressScale = 0.95f;
    [Tooltip("缩放动画时长")]
    public float Duration = 0.12f;
    [Tooltip("悬停时光晕颜色")]
    public Color HoverGlow = new Color(1f, 0.92f, 0.55f, 0.12f);

    RectTransform rt;
    Vector3 baseScale;
    float currentT;
    float targetScale = 1f;
    bool hovering;
    Image hoverGlowImage;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        baseScale = rt.localScale;
    }

    void Update()
    {
        currentT = Mathf.MoveTowards(currentT, targetScale, Time.unscaledDeltaTime / Mathf.Max(0.01f, Duration));
        // 弹性缓动
        float ease = currentT;
        rt.localScale = baseScale * Mathf.Lerp(1f, ease, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        targetScale = HoverScale;
        EnsureGlow(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        targetScale = 1f;
        EnsureGlow(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = PressScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = hovering ? HoverScale : 1f;
    }

    void EnsureGlow(bool show)
    {
        if (hoverGlowImage == null && show)
        {
            var go = new GameObject("HoverGlow");
            go.transform.SetParent(transform, false);
            var grt = go.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;
            hoverGlowImage = go.AddComponent<Image>();
            hoverGlowImage.color = HoverGlow;
            hoverGlowImage.raycastTarget = false;
            hoverGlowImage.transform.SetAsFirstSibling();
        }
        if (hoverGlowImage != null)
            hoverGlowImage.gameObject.SetActive(show);
    }
}
