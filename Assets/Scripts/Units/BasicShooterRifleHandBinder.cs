using UnityEngine;

public class BasicShooterRifleHandBinder : MonoBehaviour
{
    public Transform WeaponRoot;
    public Transform AnimatedRoot;
    public Vector3 ForwardOffset = new Vector3(0.10f, -0.02f, 0.16f);
    public Vector3 EulerOffset = new Vector3(0f, 0f, 0f);
    public float RecoilDuration = 0.16f;
    public float RecoilDistance = 0.055f;
    public float RecoilPitch = 7f;

    Animator animator;
    Transform rightHand;
    Transform leftHand;
    Transform chest;
    float recoilTimer;

    void Awake()
    {
        ResolveReferences();
    }

    void LateUpdate()
    {
        if (WeaponRoot == null || animator == null)
            ResolveReferences();
        if (WeaponRoot == null || animator == null)
            return;

        rightHand = rightHand != null ? rightHand : animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftHand = leftHand != null ? leftHand : animator.GetBoneTransform(HumanBodyBones.LeftHand);
        chest = chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Chest);
        if (rightHand == null)
            return;

        Vector3 forward = ResolveForward();
        Vector3 up = ResolveUp();
        Vector3 right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = transform.right;

        Vector3 handAnchor = rightHand.position;
        if (leftHand != null)
            handAnchor = Vector3.Lerp(rightHand.position, leftHand.position, 0.38f);

        recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
        float normalized = RecoilDuration > 0.001f ? recoilTimer / RecoilDuration : 0f;
        float recoil = normalized > 0f ? Mathf.Sin(normalized * Mathf.PI) : 0f;

        WeaponRoot.position = handAnchor
            + right * ForwardOffset.x
            + up * ForwardOffset.y
            + forward * (ForwardOffset.z - RecoilDistance * recoil);
        WeaponRoot.rotation = Quaternion.LookRotation(forward, up)
            * Quaternion.Euler(EulerOffset + new Vector3(-RecoilPitch * recoil, 0f, 0f));
        WeaponRoot.gameObject.SetActive(true);
    }

    public void TriggerRecoil()
    {
        recoilTimer = Mathf.Max(recoilTimer, Mathf.Max(0.02f, RecoilDuration));
    }

    void ResolveReferences()
    {
        if (AnimatedRoot == null)
        {
            Animator found = GetComponentInChildren<Animator>(true);
            if (found != null)
                AnimatedRoot = found.transform;
        }

        animator = AnimatedRoot != null
            ? AnimatedRoot.GetComponentInChildren<Animator>(true)
            : GetComponentInChildren<Animator>(true);

        if (WeaponRoot == null)
            WeaponRoot = FindByName(transform, "KenneyWeapon");

        if (animator != null && animator.isHuman)
        {
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }
    }

    Vector3 ResolveForward()
    {
        if (rightHand != null && leftHand != null)
        {
            Vector3 handForward = leftHand.position - rightHand.position;
            if (handForward.sqrMagnitude > 0.0025f)
                return handForward.normalized;
        }

        Vector3 forward = transform.forward;
        if (AnimatedRoot != null && AnimatedRoot.forward.sqrMagnitude > 0.001f)
            forward = AnimatedRoot.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f && chest != null)
        {
            forward = chest.forward;
            forward.y = 0f;
        }

        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    Vector3 ResolveUp()
    {
        if (chest != null && chest.up.sqrMagnitude > 0.001f)
            return chest.up.normalized;
        if (AnimatedRoot != null && AnimatedRoot.up.sqrMagnitude > 0.001f)
            return AnimatedRoot.up.normalized;
        return Vector3.up;
    }

    static Transform FindByName(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindByName(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
