using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance { get; private set; }
    public static readonly bool GuestLoginUsesNicknamePopup = false;

    [Header("Login Panel")]
    public GameObject LoginPanel;
    public InputField LoginUsernameInput;
    public InputField LoginPasswordInput;
    public Button LoginButton;
    public Button SwitchToRegisterButton;
    public Button GuestLoginButton;
    public Text LoginStatusText;

    [Header("Register Panel")]
    public GameObject RegisterPanel;
    public InputField RegUsernameInput;
    public InputField RegPasswordInput;
    public InputField RegConfirmInput;
    public Button RegisterButton;
    public Button SwitchToLoginButton;
    public Text RegisterStatusText;

    [Header("Guest Nickname Popup")]
    public GameObject GuestNamePopup;
    public InputField GuestNameInput;
    public Button GuestConfirmButton;
    public Button GuestCancelButton;
    public Text GuestStatusText;

    [Header("Server")]
    public InputField ServerIpInput;

    const string SaveKeyServerIp = "saved_server_ip";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        EnsureNetworkClient();
        StartCoroutine(LoadLoginBG());
        RestoreServerIp();
        BindButtons();

        ShowPanel(true);
        GuestNamePopup?.SetActive(false);

#if UNITY_EDITOR
        if (LoginUsernameInput != null && string.IsNullOrEmpty(LoginUsernameInput.text))
            LoginUsernameInput.text = "test";
        if (LoginPasswordInput != null && string.IsNullOrEmpty(LoginPasswordInput.text))
            LoginPasswordInput.text = "123";
#endif
    }

    void EnsureNetworkClient()
    {
        if (NetworkClient.Instance == null)
            new GameObject("NetworkClient").AddComponent<NetworkClient>();
    }

    void RestoreServerIp()
    {
        string savedIp = PlayerPrefs.GetString(SaveKeyServerIp, "");
        if (ServerIpInput != null)
        {
            ServerIpInput.text = savedIp;
            ServerIpInput.onEndEdit.AddListener(ip =>
            {
                ip = (ip ?? "").Trim();
                if (string.IsNullOrEmpty(ip)) return;

                PlayerPrefs.SetString(SaveKeyServerIp, ip);
                PlayerPrefs.Save();
                ApplyServerIp(ip);
            });
        }

        if (!string.IsNullOrEmpty(savedIp))
            ApplyServerIp(savedIp);
    }

    void BindButtons()
    {
        LoginButton?.onClick.AddListener(OnLoginClick);
        SwitchToRegisterButton?.onClick.AddListener(() => ShowPanel(false));
        GuestLoginButton?.onClick.AddListener(OnGuestLoginClick);
        RegisterButton?.onClick.AddListener(OnRegisterClick);
        SwitchToLoginButton?.onClick.AddListener(() => ShowPanel(true));
        GuestConfirmButton?.onClick.AddListener(OnGuestConfirm);
        GuestCancelButton?.onClick.AddListener(OnGuestCancel);
    }

    IEnumerator LoadLoginBG()
    {
        string uri = Application.streamingAssetsPath + "/LoginBG.jpg";
        using (var req = UnityWebRequest.Get(uri))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(req.downloadHandler.data);
            var bgGO = GameObject.Find("Background");
            var bgImg = bgGO != null ? bgGO.GetComponent<Image>() : null;
            if (bgImg == null) yield break;

            bgImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
        }
    }

    void ApplyServerIp(string ip)
    {
        var net = NetworkClient.Instance;
        if (net == null) return;

        ip = (ip ?? "").Trim();
        if (string.IsNullOrEmpty(ip)) return;
        if (!ip.Contains(":")) ip += ":8080";

        net.ServerUrl = "http://" + ip;
        Debug.Log("[Login] Server URL set to " + net.ServerUrl);
    }

    void ShowPanel(bool showLogin)
    {
        LoginPanel?.SetActive(showLogin);
        RegisterPanel?.SetActive(!showLogin);
        GuestNamePopup?.SetActive(false);
        SetLoginStatus("");
        SetRegStatus("");
    }

    void OnLoginClick()
    {
        string user = LoginUsernameInput != null ? LoginUsernameInput.text.Trim() : "";
        string pass = LoginPasswordInput != null ? LoginPasswordInput.text : "";
        if (string.IsNullOrEmpty(user)) { SetLoginStatus("请输入用户名"); return; }
        if (string.IsNullOrEmpty(pass)) { SetLoginStatus("请输入密码"); return; }

        var net = NetworkClient.Instance;
        if (net == null) { SetLoginStatus("网络模块未初始化"); return; }

        SetLoginStatus("登录中...");
        SetButtonInteractable(LoginButton, false);
        net.Login(user, pass, (ok, msg) =>
        {
            SetButtonInteractable(LoginButton, true);
            if (ok)
            {
                SetLoginStatus(string.IsNullOrEmpty(msg) ? "登录成功" : msg);
                Invoke(nameof(EnterLobby), 0.35f);
                return;
            }

            SetLoginStatus(string.IsNullOrEmpty(msg) ? "登录失败" : msg);
        });
    }

    void OnRegisterClick()
    {
        string user = RegUsernameInput != null ? RegUsernameInput.text.Trim() : "";
        string pass = RegPasswordInput != null ? RegPasswordInput.text : "";
        string confirm = RegConfirmInput != null ? RegConfirmInput.text : "";
        if (string.IsNullOrEmpty(user)) { SetRegStatus("请输入用户名"); return; }
        if (user.Length < 3) { SetRegStatus("用户名至少 3 个字符"); return; }
        if (string.IsNullOrEmpty(pass)) { SetRegStatus("请输入密码"); return; }
        if (pass.Length < 6) { SetRegStatus("密码至少 6 个字符"); return; }
        if (pass != confirm) { SetRegStatus("两次密码不一致"); return; }

        var net = NetworkClient.Instance;
        if (net == null) { SetRegStatus("网络模块未初始化"); return; }

        SetRegStatus("注册中...");
        SetButtonInteractable(RegisterButton, false);
        net.Register(user, pass, (ok, msg) =>
        {
            SetButtonInteractable(RegisterButton, true);
            if (ok)
            {
                SetRegStatus(string.IsNullOrEmpty(msg) ? "注册成功" : msg);
                Invoke(nameof(EnterLobby), 0.35f);
                return;
            }

            SetRegStatus(string.IsNullOrEmpty(msg) ? "注册失败" : msg);
        });
    }

    void OnGuestLoginClick()
    {
        GuestNamePopup?.SetActive(false);
        StartGuestLogin(CreateAutoGuestName());
    }

    void OnGuestConfirm()
    {
        string name = GuestNameInput != null ? GuestNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name)) name = CreateAutoGuestName();
        if (name.Length < 2) name += "1";
        if (name.Length > 12)
        {
            SetGuestStatus("昵称最多 12 个字符");
            return;
        }

        GuestNamePopup?.SetActive(false);
        StartGuestLogin(name);
    }

    static string CreateAutoGuestName()
    {
        return "Guest" + Random.Range(1000, 10000);
    }

    void StartGuestLogin(string displayName)
    {
        var net = NetworkClient.Instance;
        if (net == null)
        {
            SetGuestStatus("网络模块未初始化");
            return;
        }

        SetGuestStatus("游客登录中...");
        SetButtonInteractable(GuestLoginButton, false);
        SetButtonInteractable(GuestConfirmButton, false);
        net.GuestLoginLocal((ok, msg) =>
        {
            SetButtonInteractable(GuestLoginButton, true);
            SetButtonInteractable(GuestConfirmButton, true);
            if (!ok)
            {
                SetGuestStatus(string.IsNullOrEmpty(msg) ? "游客登录失败" : msg);
                return;
            }

            PlayerPrefs.SetString("current_user", NetworkClient.Instance.UserName);
            PlayerPrefs.SetInt("is_guest", 1);
            PlayerPrefs.Save();
            EnterLobby();
        }, displayName);
    }

    void OnGuestCancel()
    {
        GuestNamePopup?.SetActive(false);
    }

    void EnterLobby()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    void SetLoginStatus(string msg)
    {
        if (LoginStatusText != null)
            LoginStatusText.text = msg;
    }

    void SetRegStatus(string msg)
    {
        if (RegisterStatusText != null)
            RegisterStatusText.text = msg;
    }

    void SetGuestStatus(string msg)
    {
        if (!GuestLoginUsesNicknamePopup)
        {
            SetLoginStatus(msg);
            return;
        }

        if (GuestStatusText != null)
            GuestStatusText.text = msg;
        else
            SetLoginStatus(msg);
    }
}
