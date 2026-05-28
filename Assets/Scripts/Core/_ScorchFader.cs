using UnityEngine;

/// <summary>
/// 焦痕（弹坑）淡出组件：地面爆炸后留下的深色 quad，25 秒缓慢淡出后销毁。
/// 由 EffectsManager.SpawnScorchMark 自动添加。
/// </summary>
public class _ScorchFader : MonoBehaviour
{
    public float Lifetime = 25f;
    private MeshRenderer _mr;
    private MaterialPropertyBlock _block;
    private float _t;
    private Color _baseColor = new Color(1f, 1f, 1f, 1f);

    void Awake()
    {
        _mr = GetComponent<MeshRenderer>();
        if (_mr != null) _block = new MaterialPropertyBlock();
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_mr != null && _block != null)
        {
            // 前 70% 时间保持，后 30% 线性淡出
            float a = _t < Lifetime * 0.7f ? 1f : Mathf.Clamp01(1f - (_t - Lifetime * 0.7f) / (Lifetime * 0.3f));
            var c = _baseColor; c.a = a;
            _mr.GetPropertyBlock(_block);
            _block.SetColor("_Color", c);
            _mr.SetPropertyBlock(_block);
        }
        if (_t >= Lifetime) Destroy(gameObject);
    }
}
