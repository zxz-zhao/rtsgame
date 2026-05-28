using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

// 修复登录场景：绑定LoginManager引用 + 修正游客登录按钮居中
public class FixLoginScene
{
    // 当前正在修复的场景（供 FindGO/FindComp 使用）
    static UnityEngine.SceneManagement.Scene _scene;

    [MenuItem("RTS/修复登录场景（绑定引用+居中）")]
    public static void Fix()
    {
        // 打开 LoginScene 并设为 active scene
        string path = "Assets/Scenes/LoginScene.unity";
        _scene = EditorSceneManager.OpenScene(path);
        EditorSceneManager.SetActiveScene(_scene);

        // 找 LoginManager（从场景根对象向下搜索，兼容批量构建模式）
        var lmTransform = FindInScene("LoginManager");
        LoginManager lm;
        if (lmTransform != null)
        {
            lm = lmTransform.GetComponent<LoginManager>();
            if (lm == null)
                lm = lmTransform.gameObject.AddComponent<LoginManager>();
        }
        else
        {
            // 如果不存在就创建
            var go = new GameObject("LoginManager");
            lm = go.AddComponent<LoginManager>();
        }

        // 按名字找所有 UI 元素并赋值
        lm.LoginPanel           = FindGO("LoginPanel");
        lm.RegisterPanel        = FindGO("RegisterPanel");
        lm.GuestNamePopup       = FindGO("GuestNamePopup");

        lm.LoginUsernameInput   = FindComp<InputField>("LoginUsernameInput");
        lm.LoginPasswordInput   = FindComp<InputField>("LoginPasswordInput");
        lm.LoginButton          = FindComp<Button>("LoginButton");
        lm.GuestLoginButton     = FindComp<Button>("GuestLoginButton");
        lm.SwitchToRegisterButton = FindComp<Button>("SwitchToRegisterButton");
        lm.LoginStatusText      = FindComp<Text>("LoginStatusText");

        lm.RegUsernameInput     = FindComp<InputField>("RegUsernameInput");
        lm.RegPasswordInput     = FindComp<InputField>("RegPasswordInput");
        lm.RegConfirmInput      = FindComp<InputField>("RegConfirmInput");
        lm.RegisterButton       = FindComp<Button>("RegisterButton");
        lm.SwitchToLoginButton  = FindComp<Button>("SwitchToLoginButton");
        lm.RegisterStatusText   = FindComp<Text>("RegisterStatusText");

        lm.GuestNameInput       = FindComp<InputField>("GuestNameInput");
        lm.GuestConfirmButton   = FindComp<Button>("GuestConfirmButton");
        lm.GuestCancelButton    = FindComp<Button>("GuestCancelButton");
        lm.GuestStatusText      = FindComp<Text>("GuestStatusText");

        EditorUtility.SetDirty(lm);
        EditorSceneManager.SaveScene(_scene);
        Debug.Log("✅ LoginScene 修复完毕，引用已绑定，布局已修正");
    }

    // 在场景所有根对象中递归查找指定名称（含非激活对象）
    static Transform FindInScene(string name)
    {
        foreach (var root in _scene.GetRootGameObjects())
        {
            var found = FindInChildren(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject FindGO(string name)
    {
        var t = FindInScene(name);
        if (t == null) Debug.LogWarning($"未找到: {name}");
        return t != null ? t.gameObject : null;
    }

    static T FindComp<T>(string name) where T : Component
    {
        var t = FindInScene(name);
        if (t == null) { Debug.LogWarning($"未找到: {name}"); return null; }
        var c = t.GetComponent<T>();
        if (c == null) Debug.LogWarning($"{name} 上没有 {typeof(T).Name}");
        return c;
    }
}
