using UnityEditor;
using UnityEngine;
using System.IO;

[CustomEditor(typeof(LobbyModelPreview))]
public sealed class LobbyModelPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        var preview = (LobbyModelPreview)target;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload Preview"))
            {
                preview.ReloadModel();
                preview.RenderPreviewOnce();
                EditorUtility.SetDirty(preview);
            }

            if (GUILayout.Button("Clear Cache"))
                LobbyModelPreviewTools.ClearLobbyModelLoadCache();
        }

        if (GUILayout.Button("Render Once"))
            preview.RenderPreviewOnce();

        if (GUILayout.Button("Reset View"))
        {
            preview.ResetPreviewView();
            preview.RenderPreviewOnce();
        }

        if (GUILayout.Button("Save Preview PNG"))
            SavePreviewPng(preview);

        if (GUILayout.Button("Run Load Diagnostics"))
            LobbyModelPreviewTools.DiagnoseLobbyModelLoadPaths();
    }

    static void SavePreviewPng(LobbyModelPreview preview)
    {
        string path = EditorUtility.SaveFilePanel(
            "保存大厅模型预览",
            Application.dataPath,
            "LobbyModelPreview.png",
            "png");
        if (string.IsNullOrEmpty(path))
            return;

        var texture = preview.CapturePreviewTexture();
        if (texture == null)
            return;

        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(path);
    }
}
