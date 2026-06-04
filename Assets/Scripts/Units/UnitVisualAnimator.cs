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
    Transform[] arms = new Transform[0];
    Transform[] legs = new Transform[0];
    Transform weaponRoot;
    BasicShooterRifleHandBinder rifleHandBinder;
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

        speedHash = Animator.StringToHash("Speed");
        fireHash = Animator.StringToHash("Fire");
        dieHash = Animator.StringToHash("Die");
        CacheAnimatorParameters();

        wheels = FindParts("Wheel");
        propellers = FindParts("Propeller", "Rotor");
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
            else if (Style == VisualStyle.Aircraft)
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
            if (propellers[i] != null)
                propellers[i].Rotate(Vector3.forward, PropellerSpinSpeed * dt, Space.Self);

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

    static Quaternion[] GetRotations(Transform[] transforms)
    {
        var rotations = new Quaternion[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            rotations[i] = transforms[i] != null ? transforms[i].localRotation : Quaternion.identity;
        return rotations;
    }
}
