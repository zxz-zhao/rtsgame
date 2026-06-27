using UnityEngine;

/// <summary>
/// Base behavior shared by ships: constrain movement to water and render a lightweight wake while cruising.
/// </summary>
public class NavalUnit : RTSUnit
{
    static Material s_playerSignalMastMaterial;
    static Material s_enemySignalMastMaterial;

    private GameObject _wakeRoot; // Runtime wake disc that trails behind the hull.
    private Material _wakeMaterial; // Cached material so the wake can be tinted by faction.

    // Ships need slightly roomier stop distances and presentation anchors than land units.
    protected override float MoveStoppingDistance => 0.65f;
    protected override float CombatStoppingDistance => Mathf.Max(1.0f, AttackRange * 0.72f);
    protected override float DesiredVisualHeight => 1.4f;
    protected override float DesiredVisualFootprint => 4.2f;
    protected override float HealthBarHeight => 2.6f;
    protected override float UnitLabelHeight => 2.25f;
    protected override float SelectionRingRadius => 2.1f;

    /// <summary>
    /// Initializes ship-specific visuals and immediately snaps stray placements back onto water.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        CreateWake();
        EnforceWaterBounds();
    }

    /// <summary>
    /// Keeps the ship within legal water and refreshes the wake animation each frame.
    /// </summary>
    protected override void Update()
    {
        base.Update();
        EnforceWaterBounds();
        UpdateWake();
    }

    /// <summary>
    /// Redirects generic move requests to the nearest valid water coordinate for the current map.
    /// </summary>
    public override Vector3 ClampWorldPosition(Vector3 requested)
    {
        return NavalWaterNavigator.ClampPointToWater(requested, GetWaterInset());
    }

    /// <summary>
    /// Cleans up the runtime wake helper when the unit is destroyed or unloaded.
    /// </summary>
    void OnDestroy()
    {
        if (_wakeRoot != null)
            Destroy(_wakeRoot);
    }

    /// <summary>
    /// Applies faction tinting while leaving UI and helper visuals untouched.
    /// </summary>
    protected override void ApplyMilitaryTint()
    {
        Color hullTint = bPlayerOwned
            ? new Color(0.22f, 0.48f, 0.56f)
            : new Color(0.58f, 0.34f, 0.24f);
        Color signalTint = bPlayerOwned
            ? new Color(0.20f, 0.72f, 0.95f)
            : new Color(0.96f, 0.58f, 0.18f);

        ApplySignalMastMaterial(signalTint);

        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", hullTint);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            string n = r.gameObject.name;
            if (n == "FactionRing" || n == "FactionDot" || n == "SelectionCircle") continue;
            if (n == "HPLabel" || n == "UnitLabel" || n == "AttackLine") continue;
            if (n.StartsWith("Wake", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (IsSignalMastRenderer(r.transform)) continue;

            Transform cur = r.transform;
            bool skip = false;
            while (cur != null && cur != transform)
            {
                if (cur.name.EndsWith("Aura") || cur.name.EndsWith("Disc")
                    || cur.name.EndsWith("Ring") || cur.name.StartsWith("Wake"))
                { skip = true; break; }
                cur = cur.parent;
            }
            if (skip) continue;
            r.SetPropertyBlock(block);
        }
    }

    void ApplySignalMastMaterial(Color tint)
    {
        Material mastMaterial = GetSignalMastMaterial(tint);
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!IsSignalMastRenderer(renderer.transform))
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == mastMaterial)
                    continue;

                materials[i] = mastMaterial;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;

            RendererColorUtil.ClearPropertyBlock(renderer);
        }
    }

    Material GetSignalMastMaterial(Color tint)
    {
        Material cached = bPlayerOwned ? s_playerSignalMastMaterial : s_enemySignalMastMaterial;
        if (cached != null)
        {
            RendererColorUtil.TrySetColor(cached, tint);
            return cached;
        }

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.name = bPlayerOwned ? "NavalSignalMast_Player_Runtime" : "NavalSignalMast_Enemy_Runtime";
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.16f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.02f);
        RendererColorUtil.TrySetColor(material, tint);

        if (bPlayerOwned)
            s_playerSignalMastMaterial = material;
        else
            s_enemySignalMastMaterial = material;

        return material;
    }

    static bool IsSignalMastRenderer(Transform node)
    {
        Transform cur = node;
        while (cur != null)
        {
            if (cur.name.IndexOf("SignalMast", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            cur = cur.parent;
        }

        return false;
    }

    /// <summary>
    /// Creates the soft disc used to fake a wake behind the ship.
    /// </summary>
    void CreateWake()
    {
        _wakeRoot = FxResources.MakeGroundDisc(null, "WakeTrail", SelectionRingRadius * 0.75f,
            new Color(0.56f, 0.88f, 1f, 0.22f), FxResources.DiscStyle.SoftDisc, 0.052f);
        _wakeMaterial = _wakeRoot.GetComponent<Renderer>()?.sharedMaterial;
    }

    /// <summary>
    /// Returns the safety inset ships should keep away from the shoreline mask.
    /// </summary>
    float GetWaterInset()
    {
        return Mathf.Max(0.75f, SelectionRingRadius * 0.55f);
    }

    /// <summary>
    /// Warps the ship back onto water if spawning, pathing, or physics nudges it onto land.
    /// </summary>
    void EnforceWaterBounds()
    {
        float inset = GetWaterInset();
        if (NavalWaterNavigator.IsPointOnWater(transform.position, inset))
            return;

        Vector3 clamped = ClampWorldPosition(transform.position);
        if ((clamped - transform.position).sqrMagnitude < 0.0001f)
            return;

        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.Warp(clamped);
            Agent.ResetPath();
        }
        else
            transform.position = clamped;
    }

    /// <summary>
    /// Shows and animates the wake only while the nav agent is actively moving.
    /// </summary>
    void UpdateWake()
    {
        if (_wakeRoot == null) return;
        bool moving = Agent != null && Agent.enabled && Agent.isOnNavMesh && Agent.velocity.sqrMagnitude > 0.18f;
        _wakeRoot.SetActive(moving && !IsDead());
        if (!moving) return;

        Vector3 back = transform.position - transform.forward * Mathf.Max(1.1f, SelectionRingRadius * 0.9f);
        _wakeRoot.transform.position = new Vector3(back.x, 0.055f, back.z);
        _wakeRoot.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
        float pulse = 0.85f + Mathf.Sin(Time.time * 6.2f) * 0.12f;
        _wakeRoot.transform.localScale = new Vector3(SelectionRingRadius * 1.8f, SelectionRingRadius * 0.7f, 1f) * pulse;
        if (_wakeMaterial != null)
            _wakeMaterial.color = bPlayerOwned
                ? new Color(0.58f, 0.92f, 1f, 0.20f)
                : new Color(1f, 0.64f, 0.42f, 0.17f);
    }
}
