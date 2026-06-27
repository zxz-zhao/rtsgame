using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ModelHierarchyInspector
{
    const string InspectRootName = "__ModelInspect__";
    const string ArtilleryModelPath = "Assets/External/UserModels/Units/Artillery/30f22cfb80f34e4198d2aa3020a1dff6.fbx";

    [MenuItem("RTS/Model Tools/Inspect Selected FBX Hierarchy")]
    static void InspectSelectedFbxHierarchy()
    {
        if (!TryGetSelectedModelAsset(out GameObject modelAsset, out string assetPath))
        {
            EditorUtility.DisplayDialog("Inspect FBX", "Select an FBX asset in the Project window first.", "OK");
            return;
        }

        InspectModelHierarchy(modelAsset, assetPath);
    }

    [MenuItem("RTS/Model Tools/Inspect Selected FBX Hierarchy", true)]
    static bool ValidateInspectSelectedFbxHierarchy()
    {
        return TryGetSelectedModelAsset(out _, out _);
    }

    [MenuItem("RTS/Model Tools/Inspect Artillery FBX Hierarchy")]
    static void InspectArtilleryFbxHierarchy()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtilleryModelPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("Inspect FBX", $"Could not load model:\n{ArtilleryModelPath}", "OK");
            return;
        }

        InspectModelHierarchy(modelAsset, ArtilleryModelPath);
    }

    [MenuItem("RTS/Model Tools/Select Artillery FBX")]
    static void SelectArtilleryFbx()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(ArtilleryModelPath);
        if (asset == null)
        {
            EditorUtility.DisplayDialog("Select FBX", $"Could not find model:\n{ArtilleryModelPath}", "OK");
            return;
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        AssetDatabase.OpenAsset(asset);
    }

    [MenuItem("RTS/Model Tools/Clear Model Inspect Preview")]
    static void ClearInspectPreview()
    {
        GameObject existing = GameObject.Find(InspectRootName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }
    }

    static bool TryGetSelectedModelAsset(out GameObject modelAsset, out string assetPath)
    {
        modelAsset = null;
        assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return modelAsset != null;
    }

    static void InspectModelHierarchy(GameObject modelAsset, string assetPath)
    {
        ClearInspectPreview();

        GameObject previewRoot = new GameObject(InspectRootName);
        previewRoot.hideFlags = HideFlags.DontSaveInEditor;

        GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset, previewRoot.transform) as GameObject;
        if (instance == null)
        {
            UnityEngine.Object.DestroyImmediate(previewRoot);
            EditorUtility.DisplayDialog("Inspect FBX", "Could not instantiate the selected model.", "OK");
            return;
        }

        instance.name = modelAsset.name;
        MarkDontSave(instance.transform);

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(assetPath);
        AppendTransform(instance.transform, sb, 0);

        string hierarchyText = sb.ToString();
        EditorGUIUtility.systemCopyBuffer = hierarchyText;
        Debug.Log(hierarchyText, instance);

        Selection.activeGameObject = instance;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
            SceneView.lastActiveSceneView.Repaint();
        }

        EditorUtility.DisplayDialog(
            "Inspect FBX",
            "Temporary preview created in the current scene.\nHierarchy was copied to the clipboard and printed to the Console.",
            "OK");
    }

    static void MarkDontSave(Transform root)
    {
        root.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        for (int i = 0; i < root.childCount; i++)
        {
            MarkDontSave(root.GetChild(i));
        }
    }

    static void AppendTransform(Transform node, StringBuilder sb, int depth)
    {
        sb.Append(' ', depth * 2);
        sb.Append("- ");
        sb.Append(node.name);

        if (node.GetComponent<SkinnedMeshRenderer>() != null)
        {
            sb.Append(" [SkinnedMesh]");
        }
        else if (node.GetComponent<MeshRenderer>() != null)
        {
            sb.Append(" [Mesh]");
        }

        sb.AppendLine();

        for (int i = 0; i < node.childCount; i++)
        {
            AppendTransform(node.GetChild(i), sb, depth + 1);
        }
    }
}
