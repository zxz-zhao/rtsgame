using System.Collections.Generic;
using UnityEngine;

// 浮动伤害数字：在世界空间创建 TextMesh，向上漂移并淡出
public static class DamageNumber
{
    // 连击跟踪：每个目标的最近一次受击时间 + 累计 hit 数
    private struct ComboState { public int Hits; public float LastTime; }
    private static readonly Dictionary<int, ComboState> _comboMap = new Dictionary<int, ComboState>();
    private const float ComboWindow = 0.55f;     // 0.55s 内的连续命中算同一连击
    private const int   ComboMaxScale = 8;       // 字号倍率上限对应的 hit 数
    private const int   ComboMapMaxSize = 96;    // _comboMap 触发清理的尺寸阈值（防止长会话泄漏）
    private static readonly List<int> _comboCleanupBuf = new List<int>(32);

    // 当 _comboMap 太大时，丢弃所有过期条目（last hit > ComboWindow 之前的）。
    static void CompactComboMap(float now)
    {
        if (_comboMap.Count < ComboMapMaxSize) return;
        _comboCleanupBuf.Clear();
        foreach (var kv in _comboMap)
        {
            if (now - kv.Value.LastTime > ComboWindow)
                _comboCleanupBuf.Add(kv.Key);
        }
        for (int i = 0; i < _comboCleanupBuf.Count; i++)
            _comboMap.Remove(_comboCleanupBuf[i]);
    }

    // 在 worldPos 处弹出伤害数字
    // dmg>0 = 伤害（红色），dmg<0 = 治疗（绿色）
    public static void Spawn(Vector3 worldPos, int dmg, bool isCritical = false, GameObject target = null, float sizeMultiplier = 1f)
    {
        sizeMultiplier = Mathf.Clamp(sizeMultiplier, 0.35f, 1.5f);
        // 累计连击
        int comboHits = 1;
        if (target != null && dmg > 0)
        {
            int id = target.GetInstanceID();
            float now = Time.time;
            if (_comboMap.TryGetValue(id, out var st) && now - st.LastTime <= ComboWindow)
                comboHits = st.Hits + 1;
            _comboMap[id] = new ComboState { Hits = comboHits, LastTime = now };
            CompactComboMap(now);
        }
        // 字号随连击数 1 → 1.6 倍线性增长，封顶
        float scaleMul = 1f + Mathf.Clamp01((comboHits - 1) / (float)ComboMaxScale) * 0.5f;
        if (dmg < 0)
            scaleMul *= 0.88f;

        var go = new GameObject("DmgNum");
        // 随机横向偏移，避免数字堆叠
        go.transform.position = worldPos + new Vector3(
            Random.Range(-0.3f, 0.3f), 0.5f, Random.Range(-0.3f, 0.3f));

        var tm = go.AddComponent<TextMesh>();
        // 连击 ≥3 时数字前加 "×N "
        tm.text      = (dmg > 0 && comboHits >= 3) ? $"×{comboHits}  -{dmg}"
                     : (dmg > 0 ? $"-{dmg}" : $"+{-dmg}");
        int baseFont = isCritical ? 56 : (dmg < 0 ? 36 : 42);
        tm.fontSize  = Mathf.RoundToInt(baseFont * scaleMul);
        tm.characterSize = 0.075f * sizeMultiplier;
        tm.fontStyle = (isCritical || comboHits >= 3) ? FontStyle.Bold : FontStyle.Normal;
        tm.anchor    = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;

        if (dmg > 0)
        {
            // 连击越多越红
            float r = Mathf.Lerp(1f, 1f, scaleMul);
            float g = Mathf.Lerp(0.55f, 0.18f, (scaleMul - 1f) / 0.6f);
            float b = Mathf.Lerp(0.15f, 0.05f, (scaleMul - 1f) / 0.6f);
            tm.color = isCritical
                ? new Color(1f, 0.22f, 0.05f)   // 暴击：深红橙
                : new Color(r, g, b);            // 普通：随连击渐红
        }
        else
            tm.color = new Color(0.22f, 0.95f, 0.38f); // 治疗：绿色

        // 始终朝向摄像机
        go.AddComponent<BillboardLabel>();

        // 让文字始终渲染在最前（穿透遮挡）
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.material.SetInt("_ZTest",
                (int)UnityEngine.Rendering.CompareFunction.Always);

        go.AddComponent<DamageNumberMover>().Init(isCritical || comboHits >= 5, sizeMultiplier);
    }
}

// 移动 + 淡出控制组件
public class DamageNumberMover : MonoBehaviour
{
    private float _lifetime;
    private float _elapsed;
    private float _riseSpeed;
    private float _sizeMultiplier = 1f;
    private TextMesh _tm;
    private Color _startColor;

    public void Init(bool critical, float sizeMultiplier = 1f)
    {
        _sizeMultiplier = Mathf.Clamp(sizeMultiplier, 0.35f, 1.5f);
        _tm        = GetComponent<TextMesh>();
        _startColor = _tm.color;
        _lifetime  = critical ? 1.1f : 0.8f;
        _riseSpeed = (critical ? 3.2f : 2.2f) * Mathf.Lerp(0.82f, 1f, _sizeMultiplier);
        _elapsed   = 0f;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _lifetime;

        // 上升（先快后慢）
        transform.position += Vector3.up * _riseSpeed * (1f - t * 0.7f) * Time.deltaTime;

        // 淡出（后半段开始消失）
        float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
        if (_tm != null)
            _tm.color = new Color(_startColor.r, _startColor.g, _startColor.b,
                Mathf.Clamp01(alpha));

        if (_elapsed >= _lifetime)
            Destroy(gameObject);
    }
}
