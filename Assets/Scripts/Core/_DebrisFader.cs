using UnityEngine;

/// <summary>建筑碎片：寿命到期后淡出 + 销毁。物理停止后停在地上一段时间。</summary>
internal class _DebrisFader : MonoBehaviour
{
    float life;
    float t;
    Renderer rd;
    Color startColor;

    public void Init(float lifeSeconds)
    {
        life = Mathf.Max(0.5f, lifeSeconds);
        rd = GetComponent<Renderer>();
        if (rd != null && rd.material != null) startColor = rd.material.color;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t > life * 0.7f)
        {
            float fadeR = Mathf.Clamp01((t - life * 0.7f) / (life * 0.3f));
            if (rd != null && rd.material != null && rd.material.HasProperty("_Color"))
            {
                var c = startColor;
                c.a = startColor.a * (1f - fadeR);
                rd.material.color = c;
            }
        }
        if (t >= life) Destroy(gameObject);
    }
}
