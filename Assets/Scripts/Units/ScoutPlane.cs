using UnityEngine;
/// <summary>
/// High-vision reconnaissance aircraft that favors map coverage over raw combat power.
/// </summary>
public class ScoutPlane : AirUnit
{
    const string ScoutHelicopterResourcePath = "Models/ScoutHelicopter/Helicopter3";
    const string ScoutHelicopterInstanceName = "ScoutHelicopter_Model";
    const string RuntimeTailRotorName = "RuntimeTailRotor";

    protected override float DesiredVisualHeight => 1.5f;
    protected override float DesiredVisualFootprint => 3.5f;
    // Recon aircraft should stay airborne instead of reserving an airfield parking slot.
    public override bool RequiresAirfieldSlot => false;

    /// <summary>
    /// Applies the scout plane's recon-focused stats before the shared aircraft startup runs.
    /// </summary>
    protected override void Awake()
    {
        DisplayName = "侦察机";
        MaxHP = 80; AttackDamage = 10; AttackRange = 12f;
        AttackInterval = 1f; SightRange = 50f;
        GoldCost = 150; PopCost = 1; MoveSpeed = 18f;
        FlyHeight = 7f;
        MaxFuelSeconds = DefaultBattleFuelSeconds;
        RefuelSeconds = 10f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 88f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 0.34f;
        ProjectileTint = new Color(0.9f, 0.96f, 1f, 1f);
        TracerDuration = 0.035f;
        InstallHelicopterVisual();
        base.Awake();
    }

    protected override void Start()
    {
        InstallHelicopterVisual();
        base.Start();
        RemovePlaneOnlyHelpers();
        EnsureRuntimeTailRotorVisual();
    }

    void InstallHelicopterVisual()
    {
        Transform modelRoot = transform.Find("Model");
        if (modelRoot == null)
        {
            GameObject modelGo = new GameObject("Model");
            modelGo.transform.SetParent(transform, false);
            modelRoot = modelGo.transform;
        }

        if (modelRoot.Find(ScoutHelicopterInstanceName) != null)
            return;

        GameObject helicopterPrefab = Resources.Load<GameObject>(ScoutHelicopterResourcePath);
        if (helicopterPrefab == null)
        {
            Debug.LogWarning($"[ScoutPlane] Missing helicopter visual at Resources/{ScoutHelicopterResourcePath}.");
            return;
        }

        for (int i = 0; i < modelRoot.childCount; i++)
        {
            Transform child = modelRoot.GetChild(i);
            if (child != null && child.name != "Muzzle")
                child.gameObject.SetActive(false);
        }

        GameObject helicopter = Instantiate(helicopterPrefab, modelRoot);
        helicopter.name = ScoutHelicopterInstanceName;
        helicopter.transform.localPosition = Vector3.zero;
        helicopter.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        helicopter.transform.localScale = Vector3.one;
        DisableImportedHelicopterAnimation(helicopter);

        Transform muzzle = modelRoot.Find("Muzzle");
        if (muzzle != null)
            muzzle.localPosition = new Vector3(0f, 0.25f, 1.2f);

        var visualAnimator = GetComponent<UnitVisualAnimator>();
        if (visualAnimator != null)
        {
            visualAnimator.VisualRoot = modelRoot;
            visualAnimator.PreferPropellerGroupNodes = true;
            visualAnimator.ForcePropellerLocalAxis = false;
            visualAnimator.ForcePropellerWorldAxis = true;
            visualAnimator.ForcedPropellerWorldAxis = Vector3.down;
            visualAnimator.UseHelicopterRotorAxes = true;
            visualAnimator.HelicopterTailRotorLocalAxis = Vector3.right;
            visualAnimator.HelicopterTailRotorSpeedMultiplier = 2.2f;
            visualAnimator.PropellerSpinSpeed = Mathf.Max(visualAnimator.PropellerSpinSpeed, 1600f);
        }
    }

    void DisableImportedHelicopterAnimation(GameObject helicopter)
    {
        if (helicopter == null)
            return;

        foreach (var importedAnimator in helicopter.GetComponentsInChildren<Animator>(true))
            importedAnimator.enabled = false;

        foreach (var importedAnimation in helicopter.GetComponentsInChildren<Animation>(true))
            importedAnimation.enabled = false;
    }

    void RemovePlaneOnlyHelpers()
    {
        DestroyModelChild("AircraftForwardStripe");
        DestroyModelChild("FactionStripeL");
        DestroyModelChild("FactionStripeR");
        Transform contrail = transform.Find("Contrail");
        if (contrail != null)
            Destroy(contrail.gameObject);
    }

    void DestroyModelChild(string childName)
    {
        Transform modelRoot = transform.Find("Model");
        Transform child = modelRoot != null ? modelRoot.Find(childName) : null;
        if (child != null)
            Destroy(child.gameObject);
    }

    void EnsureRuntimeTailRotorVisual()
    {
        if (transform.Find(RuntimeTailRotorName) != null)
            return;

        if (!TryGetHelicopterBounds(out Bounds bounds))
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        forward.Normalize();

        float minForward = float.PositiveInfinity;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsHelicopterRenderer(renderer))
                continue;

            Bounds rendererBounds = renderer.bounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int c = 0; c < corners.Length; c++)
                minForward = Mathf.Min(minForward, Vector3.Dot(corners[c], forward));
        }

        if (float.IsInfinity(minForward))
            return;

        Vector3 tailWorld = bounds.center + forward * (minForward - Vector3.Dot(bounds.center, forward) + 0.08f);
        tailWorld.y = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.52f);

        GameObject pivot = new GameObject(RuntimeTailRotorName);
        pivot.transform.position = tailWorld;
        pivot.transform.SetParent(transform, true);
        pivot.transform.localRotation = Quaternion.identity;
        pivot.transform.localScale = Vector3.one;

        RuntimeRotorSpinner spinner = pivot.AddComponent<RuntimeRotorSpinner>();
        spinner.LocalAxis = Vector3.right;
        spinner.Speed = 3800f;

        Material bladeMaterial = CreateTailRotorMaterial();
        CreateTailRotorBlade(pivot.transform, "RuntimeTailRotorBladeV", new Vector3(0.035f, 0.44f, 0.045f), bladeMaterial);
        CreateTailRotorBlade(pivot.transform, "RuntimeTailRotorBladeH", new Vector3(0.035f, 0.045f, 0.44f), bladeMaterial);
    }

    bool TryGetHelicopterBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool found = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsHelicopterRenderer(renderer))
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    bool IsHelicopterRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.gameObject.activeInHierarchy)
            return false;
        if (renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer)
            return false;

        Transform current = renderer.transform;
        while (current != null && current != transform)
        {
            string name = current.name;
            if (name == ScoutHelicopterInstanceName)
                return true;
            if (name == RuntimeTailRotorName || name == "Muzzle" || name == "AirShadow" || name == "SelectionRing")
                return false;
            current = current.parent;
        }

        return false;
    }

    static Material CreateTailRotorMaterial()
    {
        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = new Color(0.45f, 0.82f, 1f, 0.95f);
        return material;
    }

    static void CreateTailRotorBlade(Transform parent, string name, Vector3 localScale, Material material)
    {
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = name;
        blade.transform.SetParent(parent, false);
        blade.transform.localPosition = Vector3.zero;
        blade.transform.localRotation = Quaternion.identity;
        blade.transform.localScale = localScale;

        Collider collider = blade.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = blade.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

public class RuntimeRotorSpinner : MonoBehaviour
{
    public Vector3 LocalAxis = Vector3.forward;
    public float Speed = 1200f;

    void Update()
    {
        Vector3 axis = LocalAxis.sqrMagnitude > 0.0001f ? LocalAxis.normalized : Vector3.forward;
        transform.Rotate(axis, Speed * Time.deltaTime, Space.Self);
    }
}
