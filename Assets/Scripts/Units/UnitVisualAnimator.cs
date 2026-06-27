using UnityEngine;
using UnityEngine.AI;

// Lightweight visual motion for generated unit models.
// It animates child parts only, so NavMeshAgent movement stays authoritative.
public class UnitVisualAnimator : MonoBehaviour
{
    public enum VisualStyle
    {
        Infantry,
        Vehicle,
        Aircraft,
    }

    public VisualStyle Style = VisualStyle.Vehicle;
    public Transform VisualRoot;
    public float BobAmplitude = 0.04f;
    public float WheelSpinSpeed = 520f;
    public float PropellerSpinSpeed = 980f;
    public bool PreferPropellerGroupNodes = false;
    public bool ForcePropellerLocalAxis = false;
    public Vector3 ForcedPropellerLocalAxis = Vector3.forward;
    public bool ForcePropellerWorldAxis = false;
    public Vector3 ForcedPropellerWorldAxis = Vector3.up;
    public bool UseHelicopterRotorAxes = false;
    public Vector3 HelicopterTailRotorLocalAxis = Vector3.right;
    public float HelicopterTailRotorSpeedMultiplier = 1.8f;

    NavMeshAgent agent;
    Animator animator;
    Vector3 lastPosition;
    Vector3 baseRootLocalPosition;
    Quaternion baseRootLocalRotation;
    float clock;
    float fireKickTimer;
    float hitKickTimer;
    float hitKickDuration = 0.16f;
    bool hitKickHeavy;
    float deathTimer;
    int speedHash;
    int fireHash;
    int dieHash;
    bool hasSpeedParam;
    bool hasFireParam;
    bool hasDieParam;
    bool isDying;

    Transform[] wheels = new Transform[0];
    Transform[] propellers = new Transform[0];
    Vector3[] propellerSpinAxes = new Vector3[0];
    Transform[] arms = new Transform[0];
    Transform[] legs = new Transform[0];
    Transform weaponRoot;
    BasicShooterRifleHandBinder rifleHandBinder;
    bool isAircraftLike;
    bool disableAircraftBob;
    Quaternion[] armBaseRotations = new Quaternion[0];
    Quaternion[] legBaseRotations = new Quaternion[0];
    Quaternion weaponBaseLocalRotation;
    Vector3 weaponBaseLocalPosition;

    void Awake()
    {
        UseGeneratedInfantryFallbackIfNeeded();
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
        if (animator != null && !animator.enabled)
            animator = null;
        if (VisualRoot == null)
            VisualRoot = transform.Find("Model");

        if (VisualRoot != null)
        {
            baseRootLocalPosition = VisualRoot.localPosition;
            baseRootLocalRotation = VisualRoot.localRotation;
        }
        isAircraftLike = Style == VisualStyle.Aircraft || GetComponent<AirUnit>() != null;
        disableAircraftBob = GetComponent<ScoutPlane>() != null;

        speedHash = Animator.StringToHash("Speed");
        fireHash = Animator.StringToHash("Fire");
        dieHash = Animator.StringToHash("Die");
        CacheAnimatorParameters();

        wheels = FindParts("Wheel");
        propellers = PreferPropellerGroupNodes ? FindPropellerGroupParts() : FindParts("Propeller", "Rotor");
        if (propellers.Length == 0 && isAircraftLike)
            propellers = FindAircraftRotorCandidates();
        propellerSpinAxes = GetPropellerSpinAxes(propellers);
        arms = FindParts("Arm");
        legs = FindParts("Leg");
        rifleHandBinder = GetComponent<BasicShooterRifleHandBinder>();
        weaponRoot = FindFirstPart("KenneyWeapon", "Rifle", "Barrel", "Nozzle", "Muzzle");
        armBaseRotations = GetRotations(arms);
        legBaseRotations = GetRotations(legs);
        if (weaponRoot != null)
        {
            weaponBaseLocalRotation = weaponRoot.localRotation;
            weaponBaseLocalPosition = weaponRoot.localPosition;
        }
        lastPosition = transform.position;
    }

    public void RebindVisualRootBasePose()
    {
        if (VisualRoot == null)
            VisualRoot = transform.Find("Model");

        if (VisualRoot == null)
            return;

        baseRootLocalPosition = VisualRoot.localPosition;
        baseRootLocalRotation = VisualRoot.localRotation;
    }

    void UseGeneratedInfantryFallbackIfNeeded()
    {
        if (!HasMismatchedInfantryAnimator(gameObject, Style))
            return;

        Animator importedAnimator = GetComponentInChildren<Animator>(true);
        Transform modelRoot = VisualRoot != null ? VisualRoot : transform.Find("Model");
        if (modelRoot == null)
        {
            GameObject model = new GameObject("Model");
            model.transform.SetParent(transform, false);
            modelRoot = model.transform;
        }

        if (importedAnimator != null)
            importedAnimator.enabled = false;

        Transform generated = FindChildRecursive(modelRoot, "GeneratedInfantryUpright");
        if (generated == null)
            generated = BuildGeneratedInfantry(modelRoot);
        if (generated == null)
            return;

        VisualRoot = modelRoot;
        HideOtherVisualChildren(modelRoot, generated);
        ShowAttachmentChildren(modelRoot);
        AttachExternalWeaponToGeneratedInfantry(modelRoot, generated);
        HideGeneratedRifleIfExternalWeaponExists(modelRoot, generated);

        generated.gameObject.SetActive(true);
        generated.localPosition = Vector3.zero;
        generated.localRotation = Quaternion.identity;
        generated.localScale = Vector3.one;

        Transform staticInfantry = FindChildRecursive(modelRoot, "Infantry_Model_Instance");
        if (staticInfantry != null)
            staticInfantry.gameObject.SetActive(false);
    }

    static Transform FindChildNameContaining(Transform root, string token)
    {
        if (root == null || string.IsNullOrEmpty(token))
            return null;

        if (root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindChildNameContaining(child, token);
            if (found != null)
                return found;
        }

        return null;
    }

    static void HideOtherVisualChildren(Transform visualRoot, Transform keep)
    {
        if (visualRoot == null || keep == null)
            return;

        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            if (child != null && child != keep)
                child.gameObject.SetActive(false);
        }
    }

    static void ShowAttachmentChildren(Transform visualRoot)
    {
        if (visualRoot == null)
            return;

        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            if (child == null)
                continue;

            string name = child.name;
            if (name == "KenneyWeapon" || name == "FactionPlate" || name.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                child.gameObject.SetActive(true);
        }
    }

    static void HideGeneratedRifleIfExternalWeaponExists(Transform visualRoot, Transform generated)
    {
        if (visualRoot == null || generated == null)
            return;

        Transform externalWeapon = FindChildRecursive(visualRoot, "KenneyWeapon");
        if (externalWeapon == null || !externalWeapon.gameObject.activeSelf)
            return;

        Transform generatedRifle = FindChildRecursive(generated, "Rifle");
        if (generatedRifle != null)
            generatedRifle.gameObject.SetActive(false);
    }

    static void AttachExternalWeaponToGeneratedInfantry(Transform visualRoot, Transform generated)
    {
        if (visualRoot == null || generated == null)
            return;

        Transform externalWeapon = FindChildRecursive(visualRoot, "KenneyWeapon");
        if (externalWeapon == null)
            return;

        bool shoulderCannon = FindChildNameContaining(externalWeapon, "WW2Shoulder") != null;
        externalWeapon.SetParent(generated, false);
        externalWeapon.localPosition = shoulderCannon
            ? new Vector3(0.22f, 0.82f, 0.34f)
            : new Vector3(0.32f, 0.72f, 0.24f);
        externalWeapon.localRotation = shoulderCannon
            ? Quaternion.Euler(1f, 88f, -6f)
            : Quaternion.Euler(8f, 0f, 0f);
        externalWeapon.localScale = Vector3.one * 0.72f;
        externalWeapon.gameObject.SetActive(true);
    }

    static Transform BuildGeneratedInfantry(Transform parent)
    {
        if (parent == null) return null;

        GameObject root = new GameObject("GeneratedInfantryUpright");
        root.transform.SetParent(parent, false);

        Material uniform = MakeGeneratedMaterial(new Color(0.28f, 0.38f, 0.18f));
        Material skin = MakeGeneratedMaterial(new Color(0.86f, 0.62f, 0.42f));
        Material dark = MakeGeneratedMaterial(new Color(0.08f, 0.09f, 0.10f));
        Material wood = MakeGeneratedMaterial(new Color(0.42f, 0.24f, 0.12f));

        CreatePart(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.68f, 0f), new Vector3(0.32f, 0.56f, 0.20f), uniform);
        CreatePart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.08f, 0.01f), new Vector3(0.24f, 0.24f, 0.24f), skin);
        CreatePart(root.transform, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 1.18f, 0.01f), new Vector3(0.28f, 0.12f, 0.28f), dark);
        CreatePart(root.transform, "ArmLeft", PrimitiveType.Cube, new Vector3(-0.26f, 0.74f, 0f), new Vector3(0.10f, 0.42f, 0.10f), uniform);
        CreatePart(root.transform, "ArmRight", PrimitiveType.Cube, new Vector3(0.26f, 0.74f, 0f), new Vector3(0.10f, 0.42f, 0.10f), uniform);
        CreatePart(root.transform, "LegLeft", PrimitiveType.Cube, new Vector3(-0.10f, 0.25f, 0f), new Vector3(0.11f, 0.46f, 0.11f), dark);
        CreatePart(root.transform, "LegRight", PrimitiveType.Cube, new Vector3(0.10f, 0.25f, 0f), new Vector3(0.11f, 0.46f, 0.11f), dark);

        Transform rifle = CreatePart(root.transform, "Rifle", PrimitiveType.Cube, new Vector3(0.33f, 0.72f, 0.24f), new Vector3(0.07f, 0.07f, 0.55f), wood);
        rifle.localRotation = Quaternion.Euler(8f, 0f, 0f);

        return root.transform;
    }

    static Material MakeGeneratedMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        RendererColorUtil.TrySetColor(mat, color);
        return mat;
    }

    static Transform CreatePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return go.transform;
    }

    public static bool HasMismatchedInfantryAnimator(GameObject root, VisualStyle style)
    {
        if (style != VisualStyle.Infantry || root == null)
            return false;

        if (FindChildRecursive(root.transform, "Infantry_Model_Instance") != null)
            return true;

        Animator importedAnimator = root.GetComponentInChildren<Animator>(true);
        if (importedAnimator == null || importedAnimator.avatar != null)
            return false;

        string controllerName = importedAnimator.runtimeAnimatorController != null
            ? importedAnimator.runtimeAnimatorController.name
            : string.Empty;

        return controllerName.Contains("SurvivorLocomotion");
    }

    static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (root.name == targetName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = agent != null ? agent.velocity.magnitude : Vector3.Distance(transform.position, lastPosition) / dt;
        bool moving = speed > 0.06f;
        UpdateFireKick(dt);
        float hitKick = UpdateHitKick(dt);

        if (isDying)
        {
            UpdateDeathPose(dt);
            lastPosition = transform.position;
            return;
        }

        UpdateAnimator(speed, dt);

        clock += dt * Mathf.Lerp(1.4f, 5.5f, Mathf.Clamp01(speed / 8f));

        if (VisualRoot != null)
        {
            float bob = 0f;
            if (Style == VisualStyle.Infantry && moving && animator == null)
                bob = Mathf.Abs(Mathf.Sin(clock * 2f)) * BobAmplitude;
            else if (Style == VisualStyle.Aircraft && !disableAircraftBob)
                bob = Mathf.Sin(clock * 1.4f) * BobAmplitude * 1.6f;

            Vector3 hitOffset = hitKick > 0f
                ? Vector3.back * (hitKick * (hitKickHeavy ? 0.085f : 0.045f))
                : Vector3.zero;
            Quaternion hitRotation = Quaternion.identity;
            if (hitKick > 0f)
            {
                hitRotation = Style == VisualStyle.Aircraft
                    ? Quaternion.Euler(0f, 0f, hitKick * (hitKickHeavy ? 7f : 4f))
                    : Quaternion.Euler(-hitKick * (hitKickHeavy ? 7f : 4f), 0f, 0f);
            }

            VisualRoot.localPosition = baseRootLocalPosition + Vector3.up * bob + hitOffset;
            VisualRoot.localRotation = baseRootLocalRotation * hitRotation;
        }

        if (moving)
        {
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null)
                    wheels[i].Rotate(Vector3.right, speed * WheelSpinSpeed * dt, Space.Self);
        }

        for (int i = 0; i < propellers.Length; i++)
        {
            if (propellers[i] == null)
                continue;

            if (UseHelicopterRotorAxes)
            {
                bool tailRotor = LooksLikeTailRotorPart(propellers[i]);
                Vector3 axis = tailRotor
                    ? transform.TransformDirection(HelicopterTailRotorLocalAxis)
                    : ForcedPropellerWorldAxis;
                if (axis.sqrMagnitude < 0.0001f)
                    axis = tailRotor ? transform.right : Vector3.up;
                float speedMultiplier = tailRotor ? Mathf.Max(0.1f, HelicopterTailRotorSpeedMultiplier) : 1f;
                propellers[i].Rotate(axis.normalized, PropellerSpinSpeed * speedMultiplier * dt, Space.World);
            }
            else if (ForcePropellerWorldAxis)
            {
                Vector3 worldAxis = ForcedPropellerWorldAxis;
                if (worldAxis.sqrMagnitude < 0.0001f)
                    worldAxis = Vector3.up;
                propellers[i].Rotate(worldAxis.normalized, PropellerSpinSpeed * dt, Space.World);
            }
            else
            {
                propellers[i].Rotate(GetPropellerSpinAxis(i), PropellerSpinSpeed * dt, Space.Self);
            }
        }

        if (Style == VisualStyle.Infantry && animator == null)
            AnimateInfantry(moving);

        lastPosition = transform.position;
    }

    void CacheAnimatorParameters()
    {
        hasSpeedParam = false;
        hasFireParam = false;
        hasDieParam = false;
        if (animator == null) return;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == "Speed") hasSpeedParam = true;
            else if (parameters[i].name == "Fire") hasFireParam = true;
            else if (parameters[i].name == "Die") hasDieParam = true;
        }
    }

    void UpdateAnimator(float speed, float dt)
    {
        if (animator == null || !hasSpeedParam) return;
        float referenceSpeed = agent != null && agent.speed > 0.01f ? agent.speed : 5f;
        float normalized = Mathf.Clamp01(speed / referenceSpeed);
        animator.SetFloat(speedHash, normalized, 0.08f, dt);
    }

    public void TriggerFireAnimation()
    {
        fireKickTimer = 0.16f;
        if (rifleHandBinder != null)
            rifleHandBinder.TriggerRecoil();
        if (animator != null && hasFireParam)
            animator.SetTrigger(fireHash);
    }

    public void TriggerHitReaction(bool heavy)
    {
        hitKickHeavy = heavy;
        hitKickDuration = heavy ? 0.24f : 0.16f;
        hitKickTimer = hitKickDuration;
    }

    public void TriggerDeathAnimation()
    {
        if (isDying) return;
        isDying = true;
        deathTimer = 0f;
        if (animator != null && hasDieParam)
            animator.SetTrigger(dieHash);
    }

    void AnimateInfantry(bool moving)
    {
        float swing = moving ? Mathf.Sin(clock * 3.2f) * 24f : 0f;
        for (int i = 0; i < arms.Length; i++)
        {
            if (arms[i] == null) continue;
            float sign = i % 2 == 0 ? 1f : -1f;
            arms[i].localRotation = armBaseRotations[i] * Quaternion.Euler(sign * swing, 0f, 0f);
        }

        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == null) continue;
            float sign = i % 2 == 0 ? -1f : 1f;
            legs[i].localRotation = legBaseRotations[i] * Quaternion.Euler(sign * swing * 0.7f, 0f, 0f);
        }
    }

    void UpdateFireKick(float dt)
    {
        if (weaponRoot == null)
            return;

        fireKickTimer = Mathf.Max(0f, fireKickTimer - dt);
        float normalized = fireKickTimer / 0.16f;
        float kick = normalized > 0f ? Mathf.Sin(normalized * Mathf.PI) : 0f;
        weaponRoot.localRotation = weaponBaseLocalRotation * Quaternion.Euler(-18f * kick, 0f, 0f);
        weaponRoot.localPosition = weaponBaseLocalPosition + Vector3.back * (0.035f * kick);
    }

    float UpdateHitKick(float dt)
    {
        if (hitKickTimer <= 0f)
            return 0f;

        float duration = Mathf.Max(0.05f, hitKickDuration);
        float normalized = Mathf.Clamp01(hitKickTimer / duration);
        float kick = Mathf.Sin(normalized * Mathf.PI);
        hitKickTimer = Mathf.Max(0f, hitKickTimer - dt);
        return kick;
    }

    void UpdateDeathPose(float dt)
    {
        deathTimer += dt;
        float t = Mathf.Clamp01(deathTimer / 0.42f);
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        if (VisualRoot != null)
        {
            Vector3 settleOffset = Style == VisualStyle.Infantry
                ? new Vector3(0f, -0.20f, 0.10f)
                : new Vector3(0f, -0.08f, 0f);
            Vector3 fallAngles = Style == VisualStyle.Infantry
                ? new Vector3(88f, 0f, 14f)
                : new Vector3(22f, 0f, 0f);

            VisualRoot.localPosition = baseRootLocalPosition + Vector3.Lerp(Vector3.zero, settleOffset, eased);
            VisualRoot.localRotation = baseRootLocalRotation * Quaternion.Euler(Vector3.Lerp(Vector3.zero, fallAngles, eased));
        }

        if (weaponRoot != null)
        {
            weaponRoot.localRotation = weaponBaseLocalRotation * Quaternion.Euler(-28f, 0f, 0f);
            weaponRoot.localPosition = weaponBaseLocalPosition + new Vector3(0f, -0.03f, -0.04f);
        }
    }

    Transform[] FindParts(params string[] tokens)
    {
        if (VisualRoot == null) return new Transform[0];

        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in VisualRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == VisualRoot || !child.gameObject.activeInHierarchy) continue;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (child.name.IndexOf(tokens[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    list.Add(child);
                    break;
                }
            }
        }
        return list.ToArray();
    }

    Transform FindFirstPart(params string[] tokens)
    {
        var matches = FindParts(tokens);
        return matches.Length > 0 ? matches[0] : null;
    }

    Transform[] FindPropellerGroupParts()
    {
        if (VisualRoot == null)
            return new Transform[0];

        var exact = new System.Collections.Generic.List<Transform>();
        var named = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in VisualRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == VisualRoot || !child.gameObject.activeInHierarchy)
                continue;

            string name = child.name;
            if (name.Equals("Blades", System.StringComparison.OrdinalIgnoreCase)
                || name.Equals("Blades_end", System.StringComparison.OrdinalIgnoreCase)
                || name.Equals("Rotor", System.StringComparison.OrdinalIgnoreCase)
                || name.Equals("Propeller", System.StringComparison.OrdinalIgnoreCase))
            {
                exact.Add(child);
                continue;
            }

            if (name.IndexOf("Rotor", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Propeller", System.StringComparison.OrdinalIgnoreCase) >= 0)
                named.Add(child);
        }

        if (exact.Count > 0)
            return exact.ToArray();

        return RemoveNestedPropellerParts(named.ToArray());
    }

    static bool LooksLikeTailRotorPart(Transform part)
    {
        if (part == null)
            return false;

        string name = part.name;
        return name.IndexOf("Tail", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Rear", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.EndsWith("_end", System.StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("End", System.StringComparison.OrdinalIgnoreCase);
    }

    static Transform[] RemoveNestedPropellerParts(Transform[] parts)
    {
        if (parts == null || parts.Length <= 1)
            return parts ?? new Transform[0];

        var result = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < parts.Length; i++)
        {
            Transform candidate = parts[i];
            if (candidate == null)
                continue;

            bool nestedUnderOther = false;
            for (int j = 0; j < parts.Length; j++)
            {
                Transform other = parts[j];
                if (other == null || other == candidate)
                    continue;
                if (candidate.IsChildOf(other))
                {
                    nestedUnderOther = true;
                    break;
                }
            }

            if (!nestedUnderOther)
                result.Add(candidate);
        }

        return result.ToArray();
    }

    Transform[] FindAircraftRotorCandidates()
    {
        Transform aircraftRoot = FindFirstPart("KenneyAircraft", "Aircraft");
        if (aircraftRoot == null)
            return new Transform[0];

        Transform meshRoot = FindImportedMeshRoot(aircraftRoot);
        if (meshRoot == null)
        {
            if (!LooksLikeGenericImportedAircraft(aircraftRoot))
                return new Transform[0];
            meshRoot = aircraftRoot;
        }

        if (!TryGetBoundsInSpace(meshRoot, meshRoot, out Bounds aircraftBounds))
            return new Transform[0];

        var scored = new System.Collections.Generic.List<ScoredTransform>();
        int longAxis = GetLargestAxis(aircraftBounds.size);
        int upAxis = GetSmallestAxis(aircraftBounds.size);
        float overallLargestSpan = Mathf.Max(aircraftBounds.size[longAxis], 0.01f);
        float longitudinalCenter = aircraftBounds.center[longAxis];
        foreach (Transform child in EnumerateAircraftMeshParts(meshRoot))
        {
            if (child == meshRoot || !child.gameObject.activeInHierarchy)
                continue;
            if (!TryGetBoundsInSpace(child, meshRoot, out Bounds modelBounds))
                continue;
            if (!TryGetBoundsInSpace(child, child, out Bounds localBounds))
                continue;

            Vector3 localSize = localBounds.size;
            int vertexCount = CountMeshVertices(child);
            if (vertexCount > 4500)
                continue;

            IndexedSize[] sorted = GetSortedAxes(localSize);
            float small = sorted[0].Size;
            float middle = sorted[1].Size;
            float large = sorted[2].Size;
            float coverage = large / overallLargestSpan;
            if (coverage < 0.015f)
                continue;

            float thinness = large / Mathf.Max(small, 0.001f);
            float flatness = middle / Mathf.Max(small, 0.001f);
            float heightRatio = Mathf.InverseLerp(aircraftBounds.min[upAxis], aircraftBounds.max[upAxis], modelBounds.center[upAxis]);
            float edgeRatio = Mathf.Abs(modelBounds.center[longAxis] - longitudinalCenter) / Mathf.Max(aircraftBounds.extents[longAxis], 0.001f);
            float complexity = Mathf.Clamp01(1f - (vertexCount / 2500f));
            bool planarRotor = small <= middle * 0.45f && small <= large * 0.18f;
            bool compactTailRotor = coverage <= 0.08f && vertexCount > 0 && vertexCount <= 180 && edgeRatio >= 0.65f;
            if (!planarRotor && !compactTailRotor)
                continue;

            float score = thinness * 0.4f
                + flatness * 0.25f
                + coverage * 1.5f
                + heightRatio * 0.8f
                + edgeRatio * 0.6f
                + complexity * 1.8f;
            if (planarRotor)
                score += 2.5f;
            if (compactTailRotor)
                score += 1.5f;
            if (LooksLikeRotorByName(child.name))
                score += 6f;
            scored.Add(new ScoredTransform { Transform = child, Score = score });
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        var result = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < scored.Count; i++)
        {
            Transform candidate = scored[i].Transform;
            bool related = false;
            for (int j = 0; j < result.Count; j++)
            {
                if (candidate.IsChildOf(result[j]) || result[j].IsChildOf(candidate))
                {
                    related = true;
                    break;
                }
            }

            if (related)
                continue;

            result.Add(candidate);
            if (result.Count >= 3)
                break;
        }

        return result.ToArray();
    }

    Transform FindImportedMeshRoot(Transform aircraftRoot)
    {
        if (aircraftRoot == null)
            return null;

        Transform best = null;
        int bestChildCount = 0;
        foreach (Transform current in aircraftRoot.GetComponentsInChildren<Transform>(true))
        {
            int objectChildCount = 0;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child != null && child.name.StartsWith("Object_", System.StringComparison.OrdinalIgnoreCase))
                    objectChildCount++;
            }

            if (objectChildCount > bestChildCount)
            {
                bestChildCount = objectChildCount;
                best = current;
            }
        }

        return bestChildCount >= 3 ? best : null;
    }

    Transform[] EnumerateAircraftMeshParts(Transform meshRoot)
    {
        if (meshRoot == null)
            return new Transform[0];

        var directChildren = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < meshRoot.childCount; i++)
        {
            Transform child = meshRoot.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy && TryGetBoundsInSpace(child, child, out _))
                directChildren.Add(child);
        }

        if (directChildren.Count > 0)
            return directChildren.ToArray();

        var descendants = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in meshRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == meshRoot || !child.gameObject.activeInHierarchy)
                continue;
            if (TryGetBoundsInSpace(child, child, out _))
                descendants.Add(child);
        }
        return descendants.ToArray();
    }

    static bool LooksLikeGenericImportedAircraft(Transform aircraftRoot)
    {
        if (aircraftRoot == null)
            return false;

        int genericChildCount = 0;
        foreach (Transform child in aircraftRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == aircraftRoot)
                continue;
            if (!child.name.StartsWith("Object_", System.StringComparison.OrdinalIgnoreCase))
                continue;

            genericChildCount++;
            if (genericChildCount >= 3)
                return true;
        }

        return false;
    }

    Vector3[] GetPropellerSpinAxes(Transform[] parts)
    {
        var axes = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            axes[i] = GuessSpinAxis(parts[i]);
        return axes;
    }

    Vector3 GetPropellerSpinAxis(int index)
    {
        if (index < 0 || index >= propellerSpinAxes.Length)
            return Vector3.forward;

        Vector3 axis = propellerSpinAxes[index];
        return axis.sqrMagnitude > 0.0001f ? axis : Vector3.forward;
    }

    Vector3 GuessSpinAxis(Transform part)
    {
        if (ForcePropellerLocalAxis)
        {
            Vector3 forcedAxis = ForcedPropellerLocalAxis;
            return forcedAxis.sqrMagnitude > 0.0001f ? forcedAxis.normalized : Vector3.forward;
        }

        if (part == null || !TryGetBoundsInSpace(part, part, out Bounds localBounds))
            return Vector3.forward;

        int axis = GetSmallestAxis(localBounds.size);
        if (axis == 0) return Vector3.right;
        if (axis == 1) return Vector3.up;
        return Vector3.forward;
    }

    static bool LooksLikeRotorByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.IndexOf("propeller", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("rotor", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("blade", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static int CountMeshVertices(Transform root)
    {
        if (root == null)
            return 0;

        int total = 0;
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh sharedMesh = meshFilters[i] != null ? meshFilters[i].sharedMesh : null;
            if (sharedMesh != null)
                total += sharedMesh.vertexCount;
        }

        SkinnedMeshRenderer[] skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedMeshes.Length; i++)
        {
            Mesh sharedMesh = skinnedMeshes[i] != null ? skinnedMeshes[i].sharedMesh : null;
            if (sharedMesh != null)
                total += sharedMesh.vertexCount;
        }

        return total;
    }

    static IndexedSize[] GetSortedAxes(Vector3 size)
    {
        var axes = new[]
        {
            new IndexedSize(0, size.x),
            new IndexedSize(1, size.y),
            new IndexedSize(2, size.z),
        };
        System.Array.Sort(axes, (a, b) => a.Size.CompareTo(b.Size));
        return axes;
    }

    static int GetSmallestAxis(Vector3 size)
    {
        return GetSortedAxes(size)[0].Axis;
    }

    static int GetLargestAxis(Vector3 size)
    {
        return GetSortedAxes(size)[2].Axis;
    }

    static bool TryGetBoundsInSpace(Transform source, Transform space, out Bounds bounds)
    {
        bounds = new Bounds();
        if (source == null || space == null)
            return false;

        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
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
            {
                Vector3 local = space.InverseTransformPoint(corners[c]);
                if (!hasBounds)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return hasBounds;
    }

    struct ScoredTransform
    {
        public Transform Transform;
        public float Score;
    }

    struct IndexedSize
    {
        public readonly int Axis;
        public readonly float Size;

        public IndexedSize(int axis, float size)
        {
            Axis = axis;
            Size = size;
        }
    }

    static Quaternion[] GetRotations(Transform[] transforms)
    {
        var rotations = new Quaternion[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            rotations[i] = transforms[i] != null ? transforms[i].localRotation : Quaternion.identity;
        return rotations;
    }
}
