using System;
using UnityEngine;

public class AnimatedUnitAttachmentBinder : MonoBehaviour
{
    [Serializable]
    public class AttachmentBinding
    {
        public string AttachmentName;
        public string[] BoneCandidates = new string[0];
        public bool PreserveWorldPose = true;
        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
        public Vector3 LocalScale = Vector3.one;
    }

    public Transform VisualRoot;
    public Transform AnimatedRoot;
    public AttachmentBinding[] Bindings = new AttachmentBinding[0];
    Animator cachedAnimator;

    void Awake()
    {
        if (ShouldUseStaticInfantryFallback())
            return;

        BindAttachments();
    }

    bool ShouldUseStaticInfantryFallback()
    {
        UnitVisualAnimator visualAnimator = GetComponentInParent<UnitVisualAnimator>();
        return visualAnimator != null
            && UnitVisualAnimator.HasMismatchedInfantryAnimator(visualAnimator.gameObject, visualAnimator.Style);
    }

    public void BindAttachments()
    {
        Transform visualRoot = ResolveVisualRoot();
        Transform animatedRoot = ResolveAnimatedRoot(visualRoot);
        if (visualRoot == null || animatedRoot == null || Bindings == null)
            return;

        VisualRoot = visualRoot;
        AnimatedRoot = animatedRoot;
        cachedAnimator = GetComponentInChildren<Animator>(true);

        for (int i = 0; i < Bindings.Length; i++)
        {
            AttachmentBinding binding = Bindings[i];
            if (binding == null || string.IsNullOrEmpty(binding.AttachmentName))
                continue;

            Transform attachment = FindByName(visualRoot, binding.AttachmentName);
            if (attachment == null)
                continue;

            Transform bone = FindBestBone(animatedRoot, binding.BoneCandidates, cachedAnimator);
            if (bone == null || attachment == bone)
                continue;

            if (ShouldUseSocketLocalPose(binding))
            {
                ApplyLocalBinding(attachment, bone, Vector3.zero, Vector3.zero, Vector3.one);
                continue;
            }

            if (binding.PreserveWorldPose)
            {
                attachment.SetParent(bone, true);
                attachment.gameObject.SetActive(true);
                continue;
            }

            ApplyLocalBinding(attachment, bone, binding.LocalPosition, binding.LocalEulerAngles, binding.LocalScale);
        }
    }

    bool ShouldUseSocketLocalPose(AttachmentBinding binding)
    {
        if (binding == null || !binding.PreserveWorldPose)
            return false;

        if (!string.Equals(binding.AttachmentName, "KenneyWeapon", StringComparison.OrdinalIgnoreCase))
            return false;

        if (cachedAnimator != null && cachedAnimator.isHuman)
            return false;

        return IsDefaultLocalBinding(binding);
    }

    static bool IsDefaultLocalBinding(AttachmentBinding binding)
    {
        return binding.LocalPosition == Vector3.zero
            && binding.LocalEulerAngles == Vector3.zero
            && binding.LocalScale == Vector3.one;
    }

    static void ApplyLocalBinding(Transform attachment, Transform bone, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        attachment.SetParent(bone, false);
        attachment.localPosition = localPosition;
        attachment.localRotation = Quaternion.Euler(localEulerAngles);
        attachment.localScale = localScale;
        attachment.gameObject.SetActive(true);
    }

    Transform ResolveVisualRoot()
    {
        if (VisualRoot != null)
            return VisualRoot;

        Transform model = transform.Find("Model");
        return model != null ? model : transform;
    }

    Transform ResolveAnimatedRoot(Transform visualRoot)
    {
        if (AnimatedRoot != null)
            return AnimatedRoot;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null)
            return animator.transform;

        if (visualRoot == null)
            return null;

        Transform importedCharacter = FindByName(visualRoot, "KenneyCharacter");
        return importedCharacter != null ? importedCharacter : visualRoot;
    }

    static Transform FindBestBone(Transform root, string[] candidates, Animator animator)
    {
        Transform humanoidBone = FindHumanoidBone(candidates, animator);
        if (humanoidBone != null)
            return humanoidBone;

        if (root == null || candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.IsNullOrEmpty(candidates[i]))
                continue;

            Transform exact = FindByName(root, candidates[i]);
            if (exact != null)
                return exact;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.IsNullOrEmpty(candidates[i]))
                continue;

            Transform compatible = FindCompatibleBoneName(root, candidates[i]);
            if (compatible != null)
                return compatible;
        }

        return null;
    }

    static Transform FindHumanoidBone(string[] candidates, Animator animator)
    {
        if (animator == null || !animator.isHuman || candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
                continue;

            if (NameMatches(candidate, "RightHand"))
                return animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (NameMatches(candidate, "LeftHand"))
                return animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (NameMatches(candidate, "Head"))
                return animator.GetBoneTransform(HumanBodyBones.Head);
            if (NameMatches(candidate, "RightForeArm"))
                return animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (NameMatches(candidate, "RightArm") || NameMatches(candidate, "RightUpperArm"))
                return animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (NameMatches(candidate, "UpperChest"))
                return animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (NameMatches(candidate, "Chest"))
                return animator.GetBoneTransform(HumanBodyBones.Chest);
            if (NameMatches(candidate, "Spine"))
                return animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        return null;
    }

    static Transform FindCompatibleBoneName(Transform root, string candidate)
    {
        if (root == null)
            return null;

        if (NameMatches(root.name, candidate))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindCompatibleBoneName(child, candidate);
            if (found != null)
                return found;
        }

        return null;
    }

    static bool NameMatches(string actualName, string candidate)
    {
        if (string.IsNullOrEmpty(actualName) || string.IsNullOrEmpty(candidate))
            return false;

        if (string.Equals(actualName, candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        string normalizedActual = NormalizeBoneName(actualName);
        string normalizedCandidate = NormalizeBoneName(candidate);
        return string.Equals(normalizedActual, normalizedCandidate, StringComparison.OrdinalIgnoreCase)
            || normalizedActual.EndsWith(normalizedCandidate, StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeBoneName(string name)
    {
        return name.Replace("mixamorig:", string.Empty)
            .Replace("mixamorig", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }

    static Transform FindByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
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
