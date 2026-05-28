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

    void Awake()
    {
        BindAttachments();
    }

    public void BindAttachments()
    {
        Transform visualRoot = ResolveVisualRoot();
        Transform animatedRoot = ResolveAnimatedRoot(visualRoot);
        if (visualRoot == null || animatedRoot == null || Bindings == null)
            return;

        VisualRoot = visualRoot;
        AnimatedRoot = animatedRoot;

        for (int i = 0; i < Bindings.Length; i++)
        {
            AttachmentBinding binding = Bindings[i];
            if (binding == null || string.IsNullOrEmpty(binding.AttachmentName))
                continue;

            Transform attachment = FindByName(visualRoot, binding.AttachmentName);
            if (attachment == null)
                continue;

            Transform bone = FindBestBone(animatedRoot, binding.BoneCandidates);
            if (bone == null || attachment == bone)
                continue;

            if (binding.PreserveWorldPose)
            {
                attachment.SetParent(bone, true);
                continue;
            }

            attachment.SetParent(bone, false);
            attachment.localPosition = binding.LocalPosition;
            attachment.localRotation = Quaternion.Euler(binding.LocalEulerAngles);
            attachment.localScale = binding.LocalScale;
        }
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

    static Transform FindBestBone(Transform root, string[] candidates)
    {
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

        return null;
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
