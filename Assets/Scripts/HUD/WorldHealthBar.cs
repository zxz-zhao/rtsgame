using UnityEngine;

// 单位/建筑头顶 HP 数字标签（TextMesh，与单位名称标签同款风格）
public class WorldHealthBar : MonoBehaviour
{
    private Transform  _target;
    private Camera     _cam;
    private float      _offset = 3f;
    private TextMesh   _tm;
    private float      _ratio  = 1f;
    private Color      _baseColor;

    // 兼容旧引用（不再使用）
    public UnityEngine.UI.Image FillBar;
    public UnityEngine.UI.Image BgBar;

    public static WorldHealthBar Create(Transform owner, float heightOffset = 3f)
    {
        var go = new GameObject("HPLabel");
        go.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        var tm = go.AddComponent<TextMesh>();
        tm.text          = "";
        tm.fontSize      = 42;
        tm.characterSize = 1f;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.fontStyle     = FontStyle.Bold;
        tm.color         = new Color(0.3f, 1f, 0.4f);

        go.AddComponent<BillboardLabel>();

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        var hb        = go.AddComponent<WorldHealthBar>();
        hb._tm        = tm;
        hb._target    = owner;
        hb._offset    = heightOffset;
        hb._cam       = Camera.main;
        hb._baseColor = tm.color;
        go.SetActive(false);
        return hb;
    }

    void LateUpdate()
    {
        if (_target == null) { Destroy(gameObject); return; }
        transform.position = _target.position + Vector3.up * _offset;

        // 低血量脉动（文字亮度闪烁）
        if (_tm != null && _ratio < 0.25f)
        {
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 6f);
            _tm.color = new Color(_baseColor.r * pulse, _baseColor.g * pulse, _baseColor.b * pulse, 1f);
        }
    }

    // 主调用：传入实际 HP 数值，显示 "150/200" 格式
    public void SetHP(int current, int max, bool isEnemy = false)
    {
        if (_tm == null) return;
        _ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 1f;

        if (_ratio >= 0.999f) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        _tm.text = $"{current}/{max}";
        _baseColor = HpColor(_ratio, isEnemy);
        _tm.color  = _baseColor;
    }

    // 兼容旧调用（仅 ratio，显示百分比）
    public void SetHP(float ratio, bool isEnemy = false)
    {
        if (_tm == null) return;
        _ratio = Mathf.Clamp01(ratio);

        if (_ratio >= 0.999f) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        _tm.text   = $"{Mathf.RoundToInt(_ratio * 100)}%";
        _baseColor = HpColor(_ratio, isEnemy);
        _tm.color  = _baseColor;
    }

    static Color HpColor(float r, bool isEnemy)
    {
        if (isEnemy)
            return r > 0.5f
                ? Color.Lerp(new Color(0.9f, 0.35f, 0.1f), new Color(0.95f, 0.65f, 0.1f), (r - 0.5f) / 0.5f)
                : Color.Lerp(new Color(0.55f, 0.05f, 0.05f), new Color(0.9f, 0.35f, 0.1f), r / 0.5f);
        return r > 0.55f
            ? Color.Lerp(new Color(0.85f, 0.82f, 0.1f), new Color(0.15f, 0.85f, 0.35f), (r - 0.55f) / 0.45f)
            : Color.Lerp(new Color(0.9f, 0.18f, 0.18f), new Color(0.85f, 0.82f, 0.1f), r / 0.55f);
    }
}
