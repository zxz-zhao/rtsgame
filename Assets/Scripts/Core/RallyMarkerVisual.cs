using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 集结点视觉：在建筑选中且有集结点时显示一面金色小旗 + 一条从建筑到集结点的虚线。
/// 同一建筑只保留一个 marker，建筑销毁或 ClearRallyPoint 时自动销毁。
/// 调用 ShowFor 即可（短暂闪烁动画 + 持续显示）。
/// </summary>
public class RallyMarkerVisual : MonoBehaviour
{
    private static readonly Dictionary<RTSBuilding, RallyMarkerVisual> _byBuilding =
        new Dictionary<RTSBuilding, RallyMarkerVisual>();

    private RTSBuilding _owner;
    private LineRenderer _line;
    private GameObject _flagPole;
    private GameObject _flagCloth;
    private float _bounceTimer = 0f;

    public static void ShowFor(RTSBuilding b, Vector3 pos)
    {
        if (b == null) return;
        RallyMarkerVisual existing;
        if (_byBuilding.TryGetValue(b, out existing) && existing != null)
        {
            existing.transform.position = new Vector3(pos.x, 0.05f, pos.z);
            existing._bounceTimer = 0.6f; // 重置弹跳
            return;
        }
        var go = new GameObject($"RallyMarker_{b.name}");
        go.transform.position = new Vector3(pos.x, 0.05f, pos.z);
        var rmv = go.AddComponent<RallyMarkerVisual>();
        rmv._owner = b;
        rmv.BuildVisual();
        rmv._bounceTimer = 0.6f;
        _byBuilding[b] = rmv;
    }

    public static void ClearFor(RTSBuilding b)
    {
        if (b == null) return;
        if (_byBuilding.TryGetValue(b, out var v) && v != null) Destroy(v.gameObject);
        _byBuilding.Remove(b);
    }

    void BuildVisual()
    {
        // 优先用 Kenney CastleKit 旗模型（OBJ）；失败回退到程序化几何
        var kenney = FxResources.TryInstantiateKenneyObj(
            "Assets/External/Kenney/CastleKit/Models/OBJ format/flag-banner-short.obj",
            new Color(1f, 0.78f, 0.18f), 0.65f);
        if (kenney != null)
        {
            kenney.name = "RallyFlagModel";
            kenney.transform.SetParent(transform, false);
            kenney.transform.localPosition = Vector3.zero;
            kenney.transform.localScale = Vector3.one * 4.5f;
            _flagPole = kenney;
            _flagCloth = null;
        }
        else
        {
            // 回退：旗杆细圆柱 + 旗布 Quad
            _flagPole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _flagPole.name = "FlagPole";
            Destroy(_flagPole.GetComponent<Collider>());
            _flagPole.transform.SetParent(transform, false);
            _flagPole.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            _flagPole.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
            var poleRD = _flagPole.GetComponent<Renderer>();
            var poleMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            poleMat.color = new Color(0.30f, 0.22f, 0.12f);
            poleRD.material = poleMat;
            poleRD.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _flagCloth = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _flagCloth.name = "FlagCloth";
            Destroy(_flagCloth.GetComponent<Collider>());
            _flagCloth.transform.SetParent(transform, false);
            _flagCloth.transform.localPosition = new Vector3(0.55f, 2.05f, 0f);
            _flagCloth.transform.localScale = new Vector3(1.0f, 0.55f, 1f);
            _flagCloth.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var clothRD = _flagCloth.GetComponent<Renderer>();
            var clothMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            clothMat.color = new Color(1f, 0.78f, 0.18f);
            clothRD.material = clothMat;
            clothRD.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 虚线（从建筑 → 集结点）
        var lineGO = new GameObject("RallyLine");
        lineGO.transform.SetParent(transform, false);
        _line = lineGO.AddComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.startWidth = 0.18f;
        _line.endWidth = 0.18f;
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        _line.material = new Material(sh);
        _line.startColor = new Color(1f, 0.78f, 0.18f, 0.85f);
        _line.endColor = new Color(1f, 0.78f, 0.18f, 0.10f);
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.useWorldSpace = true;
    }

    void Update()
    {
        // 建筑销毁/失去集结点 → 自销毁
        if (_owner == null || _owner.GetHP() <= 0 || !_owner.HasRallyPoint || !_owner.bPlayerOwned)
        {
            if (_owner != null) _byBuilding.Remove(_owner);
            Destroy(gameObject);
            return;
        }
        // 弹跳出现动画
        if (_bounceTimer > 0f)
        {
            _bounceTimer -= Time.deltaTime;
            float k = Mathf.Clamp01(1f - _bounceTimer / 0.6f);
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.4f;
            transform.localScale = new Vector3(s, s, s);
        }
        else transform.localScale = Vector3.one;
        // 旗布轻微飘动
        if (_flagCloth != null)
        {
            float wave = Mathf.Sin(Time.time * 4f) * 6f;
            _flagCloth.transform.localRotation = Quaternion.Euler(0f, 90f + wave, 0f);
        }
        // 虚线从建筑顶端到旗杆
        if (_line != null)
        {
            Vector3 a = _owner.transform.position + Vector3.up * 0.6f;
            Vector3 b = transform.position + Vector3.up * 1.0f;
            _line.SetPosition(0, a);
            _line.SetPosition(1, b);
        }
    }

    void OnDestroy()
    {
        if (_owner != null && _byBuilding.TryGetValue(_owner, out var v) && v == this)
            _byBuilding.Remove(_owner);
    }
}
