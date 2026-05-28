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
    Quaternion[] armBaseRotations = new Quaternion[0];
    Quaternion[] legBaseRotations = new Quaternion[0];
    Quaternion weaponBaseLocalRotation;
    Vector3 weaponBaseLocalPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
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

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = agent != null ? agent.velocity.magnitude : Vector3.Distance(transform.position, lastPosition) / dt;
        bool moving = speed > 0.06f;
        UpdateFireKick(dt);

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
            VisualRoot.localPosition = baseRootLocalPosition + Vector3.up * bob;
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
        if (animator != null && hasFireParam)
            animator.SetTrigger(fireHash);
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
