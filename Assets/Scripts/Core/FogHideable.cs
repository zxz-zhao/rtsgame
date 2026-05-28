using UnityEngine;

/// <summary>
/// 挂在敌方单位/建筑上：在视野外时隐藏所有 Renderer 和小地图标记。
/// 不影响 Collider/碰撞检测/AI 寻路（仅视觉）。
/// </summary>
[DisallowMultipleComponent]
public class FogHideable : MonoBehaviour
{
    private Renderer[] _renderers;
    private MinimapMarker _minimapMarker;
    private WorldHealthBar _healthBar;
    private WorldProductionBar _prodBar;
    private bool _wasVisible = true;
    private bool _initialized = false;

    void OnEnable()
    {
        FogOfWar.RegisterEnemy(this);
    }

    void OnDisable()
    {
        FogOfWar.UnregisterEnemy(this);
    }

    void Start()
    {
        // 延迟一帧采集 Renderer 列表（让 RTSUnit/RTSBuilding 完成模型挂载）
        _renderers = GetComponentsInChildren<Renderer>(true);
        _minimapMarker = GetComponent<MinimapMarker>();
        _healthBar = GetComponentInChildren<WorldHealthBar>(true);
        _prodBar = GetComponentInChildren<WorldProductionBar>(true);
        _initialized = true;
    }

    /// <summary>外部强制刷新 Renderer 列表（如运行时挂载新模型）。</summary>
    public void RefreshRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        ApplyVisibility();
    }

    public void SetVisible(bool v)
    {
        if (!_initialized) return;
        if (v == _wasVisible) return;
        _wasVisible = v;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        bool v = _wasVisible;
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.enabled = v;
            }
        }
        // 小地图标记：通过 HiddenByFog 标志位让 MinimapMarker 自己决定
        if (_minimapMarker != null) _minimapMarker.HiddenByFog = !v;
        // 头顶血条/生产进度条
        if (_healthBar != null) _healthBar.gameObject.SetActive(v);
        if (_prodBar != null) _prodBar.gameObject.SetActive(v);
    }
}
