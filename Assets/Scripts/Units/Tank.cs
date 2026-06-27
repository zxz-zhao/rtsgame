using UnityEngine;
using System.Collections.Generic;

public class Tank : RTSUnit
{
    [Header("坦克属性")]
    public float HullRotSpeed = 55f;
    public float TurretRotSpeed = 50f;
    // 二战风 RTS 没有主动技能；仅保留字段供 GetArmorPierceCooldown() 兼容旧 HUD/网络协议读取
    private float armorPierceCooldown = 0f;

    // 履带印记
    private Vector3 _lastTrackPos;
    private bool _trackInit = false;
    private Transform _turretPivot;
    private Transform _turretAimEnd;
    private Transform _attackMuzzle;
    private bool _turretPivotSearched;
    private bool _hasLastTurretAim;
    private bool _hullAimedThisFrame;
    private Quaternion _lastTurretWorldRotation;
    private Quaternion _lastHullWorldRotation;
    private Vector3 _lastTurretAimTarget;
    private bool _turretMeshExtractionTried;
    protected virtual string TankUnitDisplayName => "坦克";
    protected virtual int TankMaxHP => 500;
    protected virtual int TankAttackDamage => 90;
    protected virtual float TankAttackRange => 13f;
    protected virtual float TankAttackInterval => 1.5f;
    protected virtual float TankSightRange => 18f;
    protected virtual int TankGoldCost => 450;
    protected virtual int TankPopCost => 3;
    protected virtual float TankMoveSpeed => 5f;
    protected virtual float TankSplashRadius => 3.5f;
    protected virtual float TankSplashFalloff => 0.45f;
    protected virtual float TankProjectileSpeed => 46f;
    protected virtual float TankProjectileArcHeight => 1.8f;
    protected virtual float TankProjectileImpactRadius => 1.25f;
    protected virtual Color TankProjectileTint => new Color(1f, 0.56f, 0.18f, 1f);
    protected virtual float TankTracerLifetime => 0.06f;
    protected virtual float TankVisualHeight => 3.0f;
    protected virtual float TankVisualFootprint => 3.6f;
    protected virtual float TankHealthBarHeight => 3.6f;
    protected virtual float TankUnitLabelHeight => 3.25f;
    protected virtual float TankSelectionRingRadius => 1.9f;
    protected virtual float TankCapsuleRadius => 1.2f;
    protected virtual float TankCapsuleHeight => 1f;
    protected virtual float TankTrackStep => 2.2f;
    protected virtual float TankTrackHalfWidth => 0.8f;
    protected virtual float TankTrackLifetime => 6f;
    protected virtual bool ShouldAutoOrientTankModelInstance => false;
    protected virtual Vector3[] TankModelInstanceEulerCandidates => null;
    protected virtual Vector3 TankModelInstanceParentEulerOffset => Vector3.zero;
    protected virtual float TankInfantryDamageMultiplier => 0.2f;
    protected virtual float TankFlamethrowerDamageMultiplier => 0.35f;
    protected virtual float TankInfantryArtilleryDamageMultiplier => 0.7f;
    protected virtual bool ShouldAimHullAtCombatTarget => true;
    protected virtual string RuntimeTankModelResourcePath => null;
    protected virtual string RuntimeTankModelInstanceName => null;
    protected virtual Vector3 RuntimeTankModelLocalPosition => Vector3.zero;
    protected virtual Vector3 RuntimeTankModelLocalEuler => Vector3.zero;
    protected virtual Vector3 RuntimeTankModelLocalScale => Vector3.one;
    protected virtual string[] TankTurretPartTokens => null;
    protected virtual string[] TankTurretPivotTokens => null;
    protected virtual string[] TankTurretAimEndTokens => null;
    protected virtual string[] TankTurretRendererTokens => null;
    protected virtual bool ShouldExtractTankTurretMesh => false;
    protected virtual bool ShouldUseTankTurretLocalAimAxis => false;
    protected virtual Vector3 TankTurretLocalAimAxis => Vector3.forward;
    protected virtual bool ShouldUseTankTurretPivot => true;
    protected virtual bool TryGetForcedTankModelInstanceEuler(out Vector3 euler)
    {
        euler = Vector3.zero;
        return false;
    }
    protected virtual bool ShouldRotateTurretPivotForAiming => true;

    protected override float DesiredVisualHeight => TankVisualHeight;
    protected override float DesiredVisualFootprint => TankVisualFootprint;
    protected override float HealthBarHeight => TankHealthBarHeight;
    protected override float UnitLabelHeight => TankUnitLabelHeight;
    protected override float SelectionRingRadius => TankSelectionRingRadius;
    protected override bool CanTraverseForestZones => false;

    protected override void Start()
    {
        base.Start();
        CenterTankVisualOnRoot();

        var visualAnimator = GetComponent<UnitVisualAnimator>();
        if (visualAnimator != null)
            visualAnimator.RebindVisualRootBasePose();
    }

    /// <summary>
    /// Applies the tank's combat stats before the shared unit startup flow runs.
    /// </summary>
    protected override void Awake()
    {
        DisplayName = TankUnitDisplayName;
        MaxHP = TankMaxHP;
        AttackDamage = TankAttackDamage;
        AttackRange = TankAttackRange;
        AttackInterval = TankAttackInterval;
        SightRange = TankSightRange;
        GoldCost = TankGoldCost;
        PopCost = TankPopCost;
        MoveSpeed = TankMoveSpeed;
        SplashRadius = TankSplashRadius;
        SplashFalloff = TankSplashFalloff;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = TankProjectileSpeed;
        ProjectileArcHeight = TankProjectileArcHeight;
        ProjectileImpactRadius = TankProjectileImpactRadius;
        ProjectileTint = TankProjectileTint;
        TracerDuration = TankTracerLifetime;
        ApplyRuntimeTankModelOverride();
        base.Awake();
        ApplyTankModelOrientationFix();
        ApplyTankPhysicalShape();
    }

    void ApplyRuntimeTankModelOverride()
    {
        string resourcePath = RuntimeTankModelResourcePath;
        if (string.IsNullOrWhiteSpace(resourcePath))
            return;

        Transform visualRoot = UnitScaleNormalizer.ResolveVisualRoot(transform);
        if (visualRoot == null)
            return;

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[{nameof(Tank)}] Missing runtime tank model resource: {resourcePath}", this);
            return;
        }

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visualRoot.GetChild(i);
            if (IsTankVisualHelperName(child.name))
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        GameObject instance = Instantiate(prefab, visualRoot);
        instance.name = string.IsNullOrWhiteSpace(RuntimeTankModelInstanceName)
            ? prefab.name + "_Runtime"
            : RuntimeTankModelInstanceName;
        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = RuntimeTankModelLocalPosition;
        instanceTransform.localRotation = Quaternion.Euler(RuntimeTankModelLocalEuler);
        instanceTransform.localScale = RuntimeTankModelLocalScale;
        HideTankVisualHelpers(visualRoot);

        _turretPivot = null;
        _turretAimEnd = null;
        _attackMuzzle = null;
        _turretPivotSearched = false;
        _turretMeshExtractionTried = false;
    }

    /// <summary>
    /// Aligns the tank's collider and pathing footprint with the variant-specific hull size.
    /// </summary>
    void ApplyTankPhysicalShape()
    {
        if (Agent != null)
        {
            Agent.radius = TankCapsuleRadius;
            Agent.height = Mathf.Max(1f, TankCapsuleHeight);
        }

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.radius = TankCapsuleRadius;
            capsule.height = TankCapsuleHeight;
            capsule.center = Vector3.zero;
        }
    }

    protected override bool CanAttackAirUnit(AirUnit target)
    {
        return target != null;
    }

    protected override float GetIncomingDamageMultiplier(RTSUnit attacker)
    {
        float multiplier = base.GetIncomingDamageMultiplier(attacker);
        if (attacker == null)
            return multiplier;

        if (attacker is InfantryArtillery)
            return multiplier * TankInfantryArtilleryDamageMultiplier;
        if (attacker is InfantryFlamethrower)
            return multiplier * TankFlamethrowerDamageMultiplier;
        if (attacker is Infantry)
            return multiplier * TankInfantryDamageMultiplier;

        return multiplier;
    }

    protected override void AimAtCombatTarget(Vector3 targetPosition)
    {
        Transform pivot = GetTurretPivot();
        if (pivot == null || ShouldAimHullAtCombatTarget)
        {
            AimHullAtCombatTarget(targetPosition, pivot);
        }
        else
        {
            _lastHullWorldRotation = transform.rotation;
            _hullAimedThisFrame = true;
            if (Agent != null)
                Agent.updateRotation = false;
        }
        if (pivot == null)
        {
            return;
        }
        if (!ShouldRotateTurretPivotForAiming)
        {
            _hasLastTurretAim = false;
            SyncAttackMuzzleToTurret(pivot, targetPosition);
            return;
        }

        Vector3 dir = targetPosition - pivot.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Vector3 currentDir = GetTurretAimDirection(pivot);
        currentDir.y = 0f;
        if (currentDir.sqrMagnitude < 0.0001f)
        {
            currentDir = pivot.forward;
            currentDir.y = 0f;
        }
        if (currentDir.sqrMagnitude < 0.0001f)
        {
            currentDir = transform.forward;
            currentDir.y = 0f;
        }
        if (currentDir.sqrMagnitude < 0.0001f)
            return;

        float angle = Vector3.SignedAngle(currentDir.normalized, dir.normalized, Vector3.up);
        float turnSpeed = Mathf.Max(1f, TurretRotSpeed) * 4f;
        float maxStep = ShouldSnapTurretForShot() ? Mathf.Abs(angle) : turnSpeed * Time.deltaTime;
        float step = Mathf.Clamp(angle, -maxStep, maxStep);
        if (Mathf.Abs(step) > 0.001f)
            pivot.rotation = Quaternion.AngleAxis(step, Vector3.up) * pivot.rotation;

        _lastTurretWorldRotation = pivot.rotation;
        _lastTurretAimTarget = targetPosition;
        _hasLastTurretAim = true;
        SyncAttackMuzzleToTurret(pivot, targetPosition);
    }

    void AimHullAtCombatTarget(Vector3 targetPosition, Transform pivot)
    {
        Transform muzzle = GetAttackMuzzle();
        Vector3 targetDir = targetPosition - transform.position;
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude < 0.0001f)
            return;

        Vector3 currentDir = transform.forward;
        currentDir.y = 0f;
        if (currentDir.sqrMagnitude < 0.0001f)
        {
            currentDir = muzzle != null ? muzzle.position - transform.position : Vector3.zero;
            currentDir.y = 0f;
        }
        if (currentDir.sqrMagnitude < 0.0001f)
            return;

        float angle = Vector3.SignedAngle(currentDir.normalized, targetDir.normalized, Vector3.up);
        float turnSpeed = Mathf.Max(1f, HullRotSpeed) * 4f;
        float maxStep = ShouldSnapTurretForShot() ? Mathf.Abs(angle) : turnSpeed * Time.deltaTime;
        float step = Mathf.Clamp(angle, -maxStep, maxStep);
        if (Mathf.Abs(step) > 0.001f)
            transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * transform.rotation;

        _lastHullWorldRotation = transform.rotation;
        _hullAimedThisFrame = true;
        if (Agent != null)
            Agent.updateRotation = false;
    }

    Transform GetTurretPivot()
    {
        if (_turretPivotSearched)
            return _turretPivot;

        _turretPivotSearched = true;
        if (!ShouldUseTankTurretPivot)
            return null;

        _attackMuzzle = FindChildByNameToken(transform, "Muzzle");
        if (ShouldExtractTankTurretMesh)
        {
            Transform extractedPivot = TryCreateExtractedTurretPivot();
            if (IsValidTurretPivot(extractedPivot))
            {
                AssignTurretPivot(extractedPivot);
                return _turretPivot;
            }
        }

        Transform groupedPivot = TryCreateGroupedTurretPivot();
        if (IsValidTurretPivot(groupedPivot))
        {
            AssignTurretPivot(groupedPivot);
            return _turretPivot;
        }

        string[] priorityTokens =
        {
            "TurretBase", "TankTurret", "Turret", "pt91-turret", "turret"
        };

        string[] customPivotTokens = TankTurretPivotTokens;
        if (customPivotTokens != null)
        {
            for (int i = 0; i < customPivotTokens.Length; i++)
            {
                Transform customMatch = FindTurretPivotByToken(transform, customPivotTokens[i]);
                if (IsValidTurretPivot(customMatch))
                {
                    AssignTurretPivot(customMatch);
                    return _turretPivot;
                }
            }
        }

        for (int i = 0; i < priorityTokens.Length; i++)
        {
            Transform match = FindTurretPivotByToken(transform, priorityTokens[i]);
            if (IsValidTurretPivot(match))
            {
                AssignTurretPivot(match);
                return _turretPivot;
            }
        }

        Transform rendererPivot = FindTurretPivotByRendererClue();
        if (IsValidTurretPivot(rendererPivot))
        {
            AssignTurretPivot(rendererPivot);
            return _turretPivot;
        }

        if (_attackMuzzle != null)
        {
            Transform muzzlePivot = FindTurretAncestor(_attackMuzzle);
            if (IsValidTurretPivot(muzzlePivot))
                AssignTurretPivot(muzzlePivot);
        }


        return _turretPivot;
    }

    void AssignTurretPivot(Transform pivot)
    {
        _turretPivot = pivot;
        _turretAimEnd = FindTurretAimEnd(_turretPivot);
    }

    Transform TryCreateGroupedTurretPivot()
    {
        string[] tokens = TankTurretPartTokens;
        if (tokens == null || tokens.Length == 0)
            return null;

        Transform modelInstance = FindTankModelInstance();
        if (modelInstance == null)
            return null;

        Transform existingPivot = FindChildByNameToken(modelInstance, "GroupedTurretPivot");
        if (existingPivot != null)
            return existingPivot;

        var matches = new List<Transform>();
        for (int i = 0; i < modelInstance.childCount; i++)
        {
            Transform child = modelInstance.GetChild(i);
            if (!child.gameObject.activeInHierarchy || IsTankVisualHelperName(child.name))
                continue;

            string childName = child.name;
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                if (!string.IsNullOrEmpty(token)
                    && childName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(child);
                    break;
                }
            }
        }

        if (matches.Count <= 1)
            return null;

        Bounds groupedBounds = new Bounds(matches[0].position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < matches.Count; i++)
        {
            if (TryGetVisualBounds(matches[i], out Bounds childBounds))
            {
                if (!hasBounds)
                {
                    groupedBounds = childBounds;
                    hasBounds = true;
                }
                else
                {
                    groupedBounds.Encapsulate(childBounds);
                }
            }
            else if (!hasBounds)
            {
                groupedBounds = new Bounds(matches[i].position, Vector3.zero);
                hasBounds = true;
            }
        }

        Vector3 pivotPosition = hasBounds
            ? new Vector3(groupedBounds.center.x, groupedBounds.min.y, groupedBounds.center.z)
            : modelInstance.position;

        var pivotGo = new GameObject("GroupedTurretPivot");
        Transform pivot = pivotGo.transform;
        pivot.SetParent(modelInstance, false);
        pivot.position = pivotPosition;
        pivot.rotation = modelInstance.rotation;
        pivot.localScale = Vector3.one;

        for (int i = 0; i < matches.Count; i++)
            matches[i].SetParent(pivot, true);

        return pivot;
    }

    Transform GetTurretAimEnd(Transform pivot)
    {
        if (_turretAimEnd == null && pivot != null)
            _turretAimEnd = FindTurretAimEnd(pivot);
        return _turretAimEnd;
    }

    Transform GetAttackMuzzle()
    {
        if (_attackMuzzle == null)
            _attackMuzzle = FindChildByNameToken(transform, "Muzzle");
        return _attackMuzzle;
    }

    Transform TryCreateExtractedTurretPivot()
    {
        if (_turretMeshExtractionTried)
            return null;

        _turretMeshExtractionTried = true;
        string[] tokens = TankTurretRendererTokens;
        if (tokens == null || tokens.Length == 0)
            return null;

        MeshRenderer sourceRenderer;
        MeshFilter sourceFilter;
        int[] turretSubMeshes;
        if (TryFindTurretSubMeshes(tokens, out sourceRenderer, out sourceFilter, out turretSubMeshes))
            return TryCreateExtractedTurretPivot(sourceRenderer, sourceFilter, turretSubMeshes);

        if (TryFindTurretGeometry(out sourceRenderer, out sourceFilter))
            return TryCreateExtractedTurretPivotFromGeometry(sourceRenderer, sourceFilter);

        return null;
    }

    Transform TryCreateExtractedTurretPivot(MeshRenderer sourceRenderer, MeshFilter sourceFilter, int[] turretSubMeshes)
    {
        if (sourceRenderer == null || sourceFilter == null || turretSubMeshes == null || turretSubMeshes.Length == 0)
            return null;

        Mesh sourceMesh = sourceFilter.sharedMesh;
        if (sourceMesh == null)
            return null;

        Mesh turretMesh = BuildSubMeshCopy(sourceMesh, turretSubMeshes);
        if (turretMesh == null)
            return null;

        Transform modelInstance = sourceRenderer.transform;
        Bounds turretBounds = TransformBounds(modelInstance, turretMesh.bounds);
        Vector3 pivotPosition = new Vector3(turretBounds.center.x, turretBounds.min.y, turretBounds.center.z);

        var pivotGo = new GameObject("ExtractedTurretPivot");
        Transform pivot = pivotGo.transform;
        pivot.SetParent(modelInstance.parent, false);
        pivot.position = pivotPosition;
        pivot.rotation = modelInstance.rotation;
        pivot.localScale = modelInstance.localScale;

        var turretGo = new GameObject("ExtractedTurretMesh");
        turretGo.transform.SetParent(pivot, false);
        turretGo.transform.position = modelInstance.position;
        turretGo.transform.rotation = modelInstance.rotation;
        turretGo.transform.localScale = Vector3.one;

        var filter = turretGo.AddComponent<MeshFilter>();
        filter.sharedMesh = turretMesh;
        var renderer = turretGo.AddComponent<MeshRenderer>();
        Material[] sourceMaterials = sourceRenderer.sharedMaterials;
        var turretMaterials = new Material[turretSubMeshes.Length];
        for (int i = 0; i < turretSubMeshes.Length; i++)
        {
            int materialIndex = turretSubMeshes[i];
            turretMaterials[i] = sourceMaterials != null && materialIndex >= 0 && materialIndex < sourceMaterials.Length
                ? sourceMaterials[materialIndex]
                : null;
        }
        renderer.sharedMaterials = turretMaterials;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;

        HideSourceTurretMaterials(sourceRenderer, turretSubMeshes);
        MoveMuzzleUnderExtractedTurret(pivot);
        return pivot;
    }

    Transform TryCreateExtractedTurretPivotFromGeometry(MeshRenderer sourceRenderer, MeshFilter sourceFilter)
    {
        Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
        if (sourceRenderer == null || sourceMesh == null || sourceMesh.vertexCount <= 0)
            return null;

        List<int> turretTriangles;
        List<int> hullTriangles;
        if (!SplitTurretTrianglesByHeight(sourceMesh, out turretTriangles, out hullTriangles))
            return null;

        Mesh turretMesh = BuildSingleSubMeshCopy(sourceMesh, turretTriangles, sourceMesh.name + "_ExtractedTurret");
        Mesh hullMesh = BuildSingleSubMeshCopy(sourceMesh, hullTriangles, sourceMesh.name + "_HullWithoutTurret");
        if (turretMesh == null || hullMesh == null)
            return null;

        Transform modelInstance = sourceRenderer.transform;
        Bounds turretBounds = TransformBounds(modelInstance, turretMesh.bounds);
        Vector3 pivotPosition = new Vector3(turretBounds.center.x, turretBounds.min.y, turretBounds.center.z);

        var pivotGo = new GameObject("ExtractedTurretPivot");
        Transform pivot = pivotGo.transform;
        pivot.SetParent(modelInstance.parent, false);
        pivot.position = pivotPosition;
        pivot.rotation = modelInstance.rotation;
        pivot.localScale = modelInstance.localScale;

        var turretGo = new GameObject("ExtractedTurretMesh");
        turretGo.transform.SetParent(pivot, false);
        turretGo.transform.position = modelInstance.position;
        turretGo.transform.rotation = modelInstance.rotation;
        turretGo.transform.localScale = Vector3.one;

        var filter = turretGo.AddComponent<MeshFilter>();
        filter.sharedMesh = turretMesh;
        var renderer = turretGo.AddComponent<MeshRenderer>();
        Material[] sourceMaterials = sourceRenderer.sharedMaterials;
        renderer.sharedMaterials = sourceMaterials != null && sourceMaterials.Length > 0
            ? new[] { sourceMaterials[0] }
            : sourceMaterials;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;

        sourceFilter.sharedMesh = hullMesh;
        if (sourceMaterials != null && sourceMaterials.Length > 0)
            sourceRenderer.sharedMaterials = new[] { sourceMaterials[0] };
        MoveMuzzleUnderExtractedTurret(pivot);
        return pivot;
    }

    bool TryFindTurretSubMeshes(string[] tokens, out MeshRenderer sourceRenderer, out MeshFilter sourceFilter, out int[] turretSubMeshes)
    {
        sourceRenderer = null;
        sourceFilter = null;
        turretSubMeshes = null;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        for (int f = 0; f < filters.Length; f++)
        {
            MeshFilter filter = filters[f];
            MeshRenderer renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Material[] materials = renderer != null ? renderer.sharedMaterials : null;
            if (mesh == null || renderer == null || materials == null)
                continue;

            List<int> matches = new List<int>();
            int count = Mathf.Min(mesh.subMeshCount, materials.Length);
            for (int i = 0; i < count; i++)
            {
                if (MaterialMatchesAnyToken(materials[i], tokens))
                    matches.Add(i);
            }

            if (matches.Count <= 0)
                continue;

            sourceRenderer = renderer;
            sourceFilter = filter;
            turretSubMeshes = matches.ToArray();
            return true;
        }

        return false;
    }

    bool TryFindTurretGeometry(out MeshRenderer sourceRenderer, out MeshFilter sourceFilter)
    {
        sourceRenderer = null;
        sourceFilter = null;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        int bestVertexCount = 0;
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            MeshRenderer renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (renderer == null || mesh == null || mesh.vertexCount <= bestVertexCount)
                continue;
            if (!IsMeaningfulRenderer(renderer) || IsTankVisualHelperName(filter.transform.name))
                continue;

            bestVertexCount = mesh.vertexCount;
            sourceRenderer = renderer;
            sourceFilter = filter;
        }

        return sourceRenderer != null && sourceFilter != null;
    }

    static bool SplitTurretTrianglesByHeight(Mesh source, out List<int> turretTriangles, out List<int> hullTriangles)
    {
        turretTriangles = new List<int>();
        hullTriangles = new List<int>();
        if (source == null || source.vertexCount <= 0)
            return false;

        Vector3[] vertices = source.vertices;
        Bounds bounds = source.bounds;
        float minY = bounds.min.y;
        float height = Mathf.Max(0.01f, bounds.size.y);
        float upperDeckY = minY + height * 0.50f;
        float turretCenterY = minY + height * 0.42f;
        Vector2 center = new Vector2(bounds.center.x, bounds.center.z);
        float centralRadius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.42f;
        float centralRadiusSqr = centralRadius * centralRadius;

        for (int s = 0; s < source.subMeshCount; s++)
        {
            int[] triangles = source.GetTriangles(s);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                Vector3 centroid = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                Vector2 flat = new Vector2(centroid.x, centroid.z);
                bool upperCentral = centroid.y >= turretCenterY && (flat - center).sqrMagnitude <= centralRadiusSqr;
                bool upperAny = centroid.y >= upperDeckY;
                List<int> target = upperCentral || upperAny ? turretTriangles : hullTriangles;
                target.Add(a);
                target.Add(b);
                target.Add(c);
            }
        }

        return turretTriangles.Count >= 3 && hullTriangles.Count >= 3;
    }

    static bool MaterialMatchesAnyToken(Material material, string[] tokens)
    {
        if (material == null || tokens == null)
            return false;

        if (ValueMatchesAnyToken(material.name, tokens))
            return true;

        Texture texture = material.mainTexture;
        return texture != null && ValueMatchesAnyToken(texture.name, tokens);
    }

    static bool ValueMatchesAnyToken(string value, string[] tokens)
    {
        if (string.IsNullOrEmpty(value) || tokens == null)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrEmpty(token)
                && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static Mesh BuildSubMeshCopy(Mesh source, int[] subMeshes)
    {
        if (source == null || subMeshes == null || subMeshes.Length == 0)
            return null;

        Mesh mesh = new Mesh();
        mesh.name = source.name + "_ExtractedTurret";
        mesh.indexFormat = source.indexFormat;
        CopyMeshVertexData(source, mesh);
        mesh.subMeshCount = subMeshes.Length;
        for (int i = 0; i < subMeshes.Length; i++)
            mesh.SetTriangles(source.GetTriangles(subMeshes[i]), i, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildSingleSubMeshCopy(Mesh source, List<int> triangles, string meshName)
    {
        if (source == null || triangles == null || triangles.Count < 3)
            return null;

        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.indexFormat = source.indexFormat;
        CopyMeshVertexData(source, mesh);
        mesh.subMeshCount = 1;
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void CopyMeshVertexData(Mesh source, Mesh target)
    {
        target.vertices = source.vertices;
        target.normals = source.normals;
        target.tangents = source.tangents;
        target.uv = source.uv;
        target.uv2 = source.uv2;
        target.colors = source.colors;
        target.bindposes = source.bindposes;
    }

    static Bounds TransformBounds(Transform transform, Bounds localBounds)
    {
        Vector3 center = transform.TransformPoint(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = transform.TransformVector(extents.x, 0f, 0f);
        Vector3 axisY = transform.TransformVector(0f, extents.y, 0f);
        Vector3 axisZ = transform.TransformVector(0f, 0f, extents.z);
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
        return new Bounds(center, extents * 2f);
    }

    void HideSourceTurretMaterials(MeshRenderer sourceRenderer, int[] turretSubMeshes)
    {
        if (sourceRenderer == null || turretSubMeshes == null)
            return;

        Material[] materials = sourceRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return;

        Material hidden = GetHiddenTankTurretMaterial();
        for (int i = 0; i < turretSubMeshes.Length; i++)
        {
            int index = turretSubMeshes[i];
            if (index >= 0 && index < materials.Length)
                materials[index] = hidden;
        }
        sourceRenderer.sharedMaterials = materials;
    }

    static Material GetHiddenTankTurretMaterial()
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = "HiddenExtractedTankTurret";
        material.color = new Color(0f, 0f, 0f, 0f);
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        return material;
    }

    void MoveMuzzleUnderExtractedTurret(Transform pivot)
    {
        Transform muzzle = GetAttackMuzzle();
        if (muzzle == null || pivot == null || muzzle.IsChildOf(pivot))
            return;

        Vector3 worldPosition = muzzle.position;
        Quaternion worldRotation = muzzle.rotation;
        muzzle.SetParent(pivot, true);
        muzzle.position = worldPosition;
        muzzle.rotation = worldRotation;
    }

    Vector3 GetTurretAimDirection(Transform pivot)
    {
        if (ShouldUseTankTurretLocalAimAxis)
        {
            Vector3 localAxis = TankTurretLocalAimAxis;
            if (localAxis.sqrMagnitude > 0.0001f)
                return pivot.TransformDirection(localAxis.normalized);
        }

        Transform end = GetTurretAimEnd(pivot);
        if (end != null)
        {
            Vector3 fromEnd = end.position - pivot.position;
            if (fromEnd.sqrMagnitude > 0.0001f)
                return fromEnd;
        }

        Transform muzzle = GetAttackMuzzle();
        if (muzzle != null && muzzle != pivot)
        {
            Vector3 fromMuzzle = muzzle.position - pivot.position;
            if (fromMuzzle.sqrMagnitude > 0.0001f)
                return fromMuzzle;
        }

        return pivot.forward;
    }

    bool ShouldSnapTurretForShot()
    {
        return AttackTimer <= Mathf.Max(Time.deltaTime, 0.02f);
    }

    void SyncAttackMuzzleToTurret(Transform pivot, Vector3 targetPosition)
    {
        Transform muzzle = GetAttackMuzzle();
        if (muzzle == null)
            return;

        Transform end = GetTurretAimEnd(pivot);
        bool muzzleFollowsPivot = muzzle == pivot || muzzle.IsChildOf(pivot);
        if (end != null && !muzzleFollowsPivot)
            muzzle.position = end.position;

        Vector3 dir = targetPosition - muzzle.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = GetTurretAimDirection(pivot);
            dir.y = 0f;
        }
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = transform.forward;
            dir.y = 0f;
        }
        if (dir.sqrMagnitude >= 0.0001f)
            muzzle.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    Transform FindTurretPivotByToken(Transform root, string token)
    {
        if (root == null || string.IsNullOrEmpty(token) || !root.gameObject.activeInHierarchy)
            return null;

        if (root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0 && IsValidTurretPivot(root))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindTurretPivotByToken(root.GetChild(i), token);
            if (found != null)
                return found;
        }

        return null;
    }

    Transform FindTurretAimEnd(Transform pivot)
    {
        string[] endTokens =
        {
            "gun_elevate_end", "Muzzle", "Nozzle", "BarrelEnd", "CannonEnd", "GunEnd", "Tip"
        };

        string[] customEndTokens = TankTurretAimEndTokens;
        if (customEndTokens != null)
        {
            for (int i = 0; i < customEndTokens.Length; i++)
            {
                Transform customMatch = FindChildByNameToken(pivot, customEndTokens[i], false);
                if (IsUsefulTurretEnd(pivot, customMatch))
                    return customMatch;
            }
        }

        for (int i = 0; i < endTokens.Length; i++)
        {
            Transform match = FindChildByNameToken(pivot, endTokens[i], false);
            if (IsUsefulTurretEnd(pivot, match))
                return match;
        }

        Transform muzzle = GetAttackMuzzle();
        if (IsUsefulTurretEnd(pivot, muzzle) && muzzle.IsChildOf(pivot))
            return muzzle;

        return FindFurthestTurretChild(pivot);
    }

    Transform FindFurthestTurretChild(Transform pivot)
    {
        Transform best = null;
        float bestSqr = 0.01f;
        Transform[] children = pivot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (!IsUsefulTurretEnd(pivot, child))
                continue;

            Vector3 delta = child.position - pivot.position;
            float sqr = delta.sqrMagnitude;
            if (sqr > bestSqr)
            {
                best = child;
                bestSqr = sqr;
            }
        }

        return best;
    }

    bool IsValidTurretPivot(Transform candidate)
    {
        if (candidate == null || candidate == transform)
            return false;

        return !candidate.name.Equals("Model", System.StringComparison.OrdinalIgnoreCase)
            && !IsEndMarkerName(candidate.name);
    }

    Transform FindTurretPivotByRendererClue()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Transform best = null;
        int bestScore = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsMeaningfulRenderer(renderer))
                continue;

            int score = GetTurretRendererScore(renderer);
            if (score <= bestScore)
                continue;

            Transform candidate = renderer.transform;
            if (IsBarrelLikeName(candidate.name))
                candidate = FindTurretAncestor(candidate);
            if (!IsValidTurretPivot(candidate) && IsValidTurretPivot(candidate.parent))
                candidate = candidate.parent;
            if (!IsValidTurretPivot(candidate))
                continue;

            best = candidate;
            bestScore = score;
        }

        return best;
    }

    int GetTurretRendererScore(Renderer renderer)
    {
        int score = GetTurretNameTokenScore(renderer.transform.name);
        Material[] materials = renderer.sharedMaterials;
        if (materials != null)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                score = Mathf.Max(score, GetTurretNameTokenScore(material.name));
                Texture texture = material.mainTexture;
                if (texture != null)
                    score = Mathf.Max(score, GetTurretNameTokenScore(texture.name));
            }
        }

        return score;
    }

    int GetTurretNameTokenScore(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int score = 0;
        if (value.IndexOf("turret", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score = Mathf.Max(score, 100);
        if (value.IndexOf("cannon", System.StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("barrel", System.StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("gun", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score = Mathf.Max(score, 70);

        string[] tokens = TankTurretRendererTokens;
        if (tokens != null)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token)
                    && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    score = Mathf.Max(score, 90);
            }
        }

        return score;
    }

    Transform FindTurretAncestor(Transform child)
    {
        Transform current = child;
        while (current != null && current != transform)
        {
            if (IsTurretLikeName(current.name))
                return current;
            current = current.parent;
        }

        current = child != null ? child.parent : null;
        while (current != null && current != transform)
        {
            if (!IsBarrelLikeName(current.name) && IsValidTurretPivot(current))
                return current;
            current = current.parent;
        }

        return null;
    }

    bool IsUsefulTurretEnd(Transform pivot, Transform candidate)
    {
        return candidate != null
            && candidate != pivot
            && candidate.gameObject.activeInHierarchy
            && !candidate.name.Equals("Model", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsEndMarkerName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.EndsWith("_end", System.StringComparison.OrdinalIgnoreCase)
            || objectName.EndsWith("End", System.StringComparison.OrdinalIgnoreCase)
            || objectName.IndexOf("Muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Nozzle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Tip", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsTurretLikeName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("turret", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsBarrelLikeName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.IndexOf("barrel", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("cannon", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("gun", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("nozzle", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void ApplyTankModelOrientationFix()
    {
        Transform modelInstance = FindTankModelInstance();
        if (modelInstance == null)
            return;

        Quaternion originalRotation = modelInstance.localRotation;
        if (TryGetForcedTankModelInstanceEuler(out Vector3 forcedEuler))
        {
            modelInstance.localRotation = ApplyTankModelParentEulerOffset(
                Quaternion.Euler(forcedEuler) * originalRotation);
            return;
        }

        if (!ShouldAutoOrientTankModelInstance)
        {
            modelInstance.localRotation = ApplyTankModelParentEulerOffset(originalRotation);
            return;
        }

        Vector3[] candidates = TankModelInstanceEulerCandidates;
        if (candidates == null || candidates.Length == 0)
            return;

        Quaternion bestRotation = originalRotation;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            modelInstance.localRotation = Quaternion.Euler(candidates[i]) * originalRotation;
            if (!TryGetVisualBounds(modelInstance, out Bounds bounds))
                continue;

            float score = ScoreTankModelOrientation(modelInstance, bounds);
            if (score < bestScore)
            {
                bestScore = score;
                bestRotation = modelInstance.localRotation;
            }
        }

        modelInstance.localRotation = ApplyTankModelParentEulerOffset(bestRotation);
    }

    Quaternion ApplyTankModelParentEulerOffset(Quaternion rotation)
    {
        Vector3 eulerOffset = TankModelInstanceParentEulerOffset;
        if (eulerOffset.sqrMagnitude <= 0.0001f)
            return rotation;

        return Quaternion.Euler(eulerOffset) * rotation;
    }

    Transform FindTankModelInstance()
    {
        Transform visualRoot = UnitScaleNormalizer.ResolveVisualRoot(transform);
        if (visualRoot == null)
            return null;

        Transform best = null;
        int bestRendererCount = 0;
        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            if (IsTankVisualHelperName(child.name))
                continue;

            int rendererCount = CountMeaningfulRenderers(child);
            if (rendererCount > bestRendererCount)
            {
                best = child;
                bestRendererCount = rendererCount;
            }
        }

        if (best != null)
            return best;

        return CountMeaningfulRenderers(visualRoot) > 0 ? visualRoot : null;
    }

    float ScoreTankModelOrientation(Transform modelInstance, Bounds bounds)
    {
        float horizontalSpan = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z), 0.01f);
        float score = bounds.size.y + (bounds.size.y / horizontalSpan) * 0.25f;

        if (TryGetModelGunAlignment(modelInstance, out float gunForwardDot))
            score += (1f - gunForwardDot) * 20f;

        if (TryGetTankSupportBounds(modelInstance, out Bounds supportBounds))
        {
            float supportHeight = Mathf.InverseLerp(bounds.min.y, bounds.max.y, supportBounds.center.y);
            score += supportHeight * Mathf.Max(0.25f, bounds.size.y * 0.35f);
        }

        float sidewaysPenalty = Mathf.Max(0f, bounds.size.x - bounds.size.z) * 0.03f;
        return score + sidewaysPenalty;
    }

    bool TryGetModelGunAlignment(Transform modelInstance, out float dot)
    {
        dot = 0f;
        if (!TryGetModelGunDirection(modelInstance, out Vector3 gunDir))
            return false;

        gunDir.y = 0f;
        Vector3 unitForward = transform.forward;
        unitForward.y = 0f;
        if (gunDir.sqrMagnitude < 0.0001f || unitForward.sqrMagnitude < 0.0001f)
            return false;

        dot = Mathf.Clamp(Vector3.Dot(gunDir.normalized, unitForward.normalized), -1f, 1f);
        return true;
    }

    static bool TryGetModelGunDirection(Transform modelInstance, out Vector3 direction)
    {
        direction = Vector3.zero;
        Transform pivot = FindModelGunPivot(modelInstance);
        if (pivot == null)
            return false;

        Transform end = FindModelGunEndMarker(pivot);
        if (end == null)
            end = FindModelGunEndMarker(modelInstance);
        if (end == null || end == pivot)
            return false;

        direction = end.position - pivot.position;
        return direction.sqrMagnitude > 0.0001f;
    }

    static Transform FindModelGunPivot(Transform root)
    {
        string[] pivotTokens =
        {
            "gun_elevate", "main_gun", "TurretBase", "TankTurret", "Turret", "KenneyTankCannon",
            "TankCannon", "Cannon", "Barrel"
        };

        for (int i = 0; i < pivotTokens.Length; i++)
        {
            Transform match = FindChildByNameTokenExcludingEnds(root, pivotTokens[i]);
            if (match != null)
                return match;
        }

        return null;
    }

    static Transform FindModelGunEndMarker(Transform root)
    {
        string[] endTokens =
        {
            "gun_elevate_end", "Muzzle", "Nozzle", "BarrelEnd", "CannonEnd", "GunEnd", "Tip"
        };

        for (int i = 0; i < endTokens.Length; i++)
        {
            Transform match = FindChildByNameToken(root, endTokens[i], false);
            if (match != null)
                return match;
        }

        return null;
    }

    static bool TryGetTankSupportBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        bool found = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsMeaningfulRenderer(renderer) || !HasTankSupportName(renderer.transform, root))
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

    static bool HasTankSupportName(Transform transform, Transform root)
    {
        Transform current = transform;
        while (current != null)
        {
            string name = current.name;
            if (name.IndexOf("track", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (current == root)
                break;
            current = current.parent;
        }

        return false;
    }

    static int CountMeaningfulRenderers(Transform root)
    {
        if (root == null)
            return 0;

        int count = 0;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsMeaningfulRenderer(renderers[i]))
                count++;
        }

        return count;
    }

    static bool IsMeaningfulRenderer(Renderer renderer)
    {
        return renderer != null
            && renderer.gameObject.activeInHierarchy
            && !(renderer is LineRenderer)
            && !(renderer is TrailRenderer)
            && !(renderer is ParticleSystemRenderer);
    }

    static bool IsTankVisualHelperName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.IndexOf("Muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Nozzle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Tip", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Ring", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void HideTankVisualHelpers(Transform root)
    {
        if (root == null)
            return;

        Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (node == null || !IsTankLocatorHelperName(node.name))
                continue;

            Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].enabled = false;
        }
    }

    static bool IsTankLocatorHelperName(string objectName)
    {
        return string.Equals(objectName, "Muzzle", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectName, "Nozzle", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectName, "Tip", System.StringComparison.OrdinalIgnoreCase);
    }

    void CenterTankVisualOnRoot()
    {
        Transform visualRoot = UnitScaleNormalizer.ResolveVisualRoot(transform);
        if (visualRoot == null || !TryGetVisualBounds(visualRoot, out Bounds bounds))
            return;

        Vector3 delta = new Vector3(
            transform.position.x - bounds.center.x,
            transform.position.y - bounds.min.y,
            transform.position.z - bounds.center.z);

        if (delta.sqrMagnitude > 0.000001f)
            visualRoot.position += delta;
    }

    static bool TryGetVisualBounds(Transform visualRoot, out Bounds bounds)
    {
        bounds = new Bounds(visualRoot.position, Vector3.zero);
        bool found = false;
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsMeaningfulRenderer(renderer))
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

    static Transform FindChildByNameToken(Transform root, string token, bool includeRoot = true)
    {
        if (root == null || string.IsNullOrEmpty(token))
            return null;

        if (!root.gameObject.activeInHierarchy)
            return null;

        if (includeRoot && root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByNameToken(root.GetChild(i), token);
            if (found != null)
                return found;
        }

        return null;
    }

    static Transform FindChildByNameTokenExcludingEnds(Transform root, string token, bool includeRoot = true)
    {
        if (root == null || string.IsNullOrEmpty(token))
            return null;

        if (!root.gameObject.activeInHierarchy)
            return null;

        if (includeRoot
            && root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0
            && !IsEndMarkerName(root.name))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByNameTokenExcludingEnds(root.GetChild(i), token);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Keeps spawning paired tread marks while the tank advances across the battlefield.
    /// </summary>
    protected override void Update()
    {
        base.Update();
        UpdateTrackMarks();
    }

    void LateUpdate()
    {
        if (_hullAimedThisFrame)
        {
            transform.rotation = _lastHullWorldRotation;
        }
        else if (Agent != null)
        {
            Agent.updateRotation = true;
        }

        if (_hasLastTurretAim)
        {
            Transform pivot = GetTurretPivot();
            if (pivot != null)
            {
                pivot.rotation = _lastTurretWorldRotation;
                SyncAttackMuzzleToTurret(pivot, _lastTurretAimTarget);
            }
        }

        _hullAimedThisFrame = false;
    }

    /// <summary>移动时每隔 TrackStep 米生成一对履带印。</summary>
    void UpdateTrackMarks()
    {
        if (IsDead() || !bPlayerOwned)
        {
            _trackInit = false;
            return;
        }

        if (!_trackInit)
        {
            _lastTrackPos = transform.position;
            _trackInit = true;
            return;
        }
        Vector3 cur = transform.position;
        if ((cur - _lastTrackPos).sqrMagnitude < TankTrackStep * TankTrackStep) return;
        // 当前坦克朝向（用 Agent.velocity 或 forward）
        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude < 0.001f) fwd = (cur - _lastTrackPos).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        EffectsManager.SpawnTrackMark(cur + right * TankTrackHalfWidth, fwd, TankTrackLifetime);
        EffectsManager.SpawnTrackMark(cur - right * TankTrackHalfWidth, fwd, TankTrackLifetime);
        _lastTrackPos = cur;
    }

    /// <summary>
    /// 二战风 RTS 没有主动技能：函数保留以保持 API 兼容（HUD/网络协议旧调用），但内部短路返回 false，
    /// 不再触发任何伤害或网络指令。如需移除可同步删除 GameNetworkSync 内的 stype=0 处理。
    /// </summary>
    public bool UseArmorPierce(RTSUnit target, bool ignoreCooldown = false) => false;

    /// <summary>
    /// Exposes the legacy armor-pierce cooldown field for old HUD and networking compatibility code.
    /// </summary>
    public float GetArmorPierceCooldown() => armorPierceCooldown;
}
