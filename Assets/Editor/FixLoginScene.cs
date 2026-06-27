using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class FixLoginScene
{
    static UnityEngine.SceneManagement.Scene scene;

    [MenuItem("RTS/Fix Login Scene (Bindings + Layout)")]
    public static void Fix()
    {
        const string path = "Assets/Scenes/LoginScene.unity";
        scene = EditorSceneManager.OpenScene(path);
        EditorSceneManager.SetActiveScene(scene);

        LoginManager manager = FindOrCreateLoginManager();

        manager.LoginPanel = FindGO("LoginPanel");
        manager.RegisterPanel = FindGO("RegisterPanel");
        manager.GuestNamePopup = FindGO("GuestNamePopup");

        manager.LoginUsernameInput = FindComp<InputField>("LoginUsernameInput");
        manager.LoginPasswordInput = FindComp<InputField>("LoginPasswordInput");
        manager.LoginButton = FindComp<Button>("LoginButton");
        manager.GuestLoginButton = FindComp<Button>("GuestLoginButton");
        manager.SwitchToRegisterButton = FindComp<Button>("SwitchToRegisterButton");
        manager.LoginStatusText = FindComp<Text>("LoginStatusText");

        manager.RegUsernameInput = FindComp<InputField>("RegUsernameInput");
        manager.RegPasswordInput = FindComp<InputField>("RegPasswordInput");
        manager.RegConfirmInput = FindComp<InputField>("RegConfirmInput");
        manager.RegisterButton = FindComp<Button>("RegisterButton");
        manager.SwitchToLoginButton = FindComp<Button>("SwitchToLoginButton");
        manager.RegisterStatusText = FindComp<Text>("RegisterStatusText");

        manager.GuestNameInput = FindComp<InputField>("GuestNameInput");
        manager.GuestConfirmButton = FindComp<Button>("GuestConfirmButton");
        manager.GuestCancelButton = FindComp<Button>("GuestCancelButton");
        manager.GuestStatusText = FindComp<Text>("GuestStatusText");

        NormalizeButtonLayout(manager.GuestLoginButton, new Vector2(0.32f, 0.165f));
        NormalizeButtonLayout(manager.SwitchToRegisterButton, new Vector2(0.72f, 0.165f));

        EditorUtility.SetDirty(manager);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LoginScene bindings and button layout fixed.");
    }

    static LoginManager FindOrCreateLoginManager()
    {
        Transform existing = FindInScene("LoginManager");
        if (existing != null)
        {
            LoginManager manager = existing.GetComponent<LoginManager>();
            return manager != null ? manager : existing.gameObject.AddComponent<LoginManager>();
        }

        GameObject go = new GameObject("LoginManager");
        return go.AddComponent<LoginManager>();
    }

    static void NormalizeButtonLayout(Button button, Vector2 anchor)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rt = button.GetComponent<RectTransform>();
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    static Transform FindInScene(string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindInChildren(root.transform, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform found = FindInChildren(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static GameObject FindGO(string name)
    {
        Transform found = FindInScene(name);
        if (found == null)
        {
            Debug.LogWarning("Missing GameObject: " + name);
            return null;
        }

        return found.gameObject;
    }

    static T FindComp<T>(string name) where T : Component
    {
        Transform found = FindInScene(name);
        if (found == null)
        {
            Debug.LogWarning("Missing GameObject: " + name);
            return null;
        }

        T component = found.GetComponent<T>();
        if (component == null)
        {
            Debug.LogWarning(name + " is missing component " + typeof(T).Name);
        }

        return component;
    }
}
