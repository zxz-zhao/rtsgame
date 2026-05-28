using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class AndroidBuildSetup
{
    private const string PackageName = "com.mystudio.unityRTS";
    private const string ProductName = "Xinghuo RTS";
    private const string CompanyName = "MyStudio";
    private const string ApkPath = "Build/Android/UnityRTS.apk";

    private static readonly string[] BuildScenes =
    {
        "Assets/Scenes/LoginScene.unity",
        "Assets/Scenes/LobbyScene.unity",
        "Assets/Scenes/GameScene.unity"
    };

    [MenuItem("RTS/Configure Android Build Settings")]
    public static void SetupAndroidMenu()
    {
        SetupAndroid(false);
    }

    public static void SetupAndroid(bool silent)
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.resizableWindow = false;

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSpeed);

        // Local test servers still use HTTP. Production should move to HTTPS and set this to NotAllowed.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        QualitySettings.SetQualityLevel(2, true);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BuildScenes[0], true),
            new EditorBuildSettingsScene(BuildScenes[1], true),
            new EditorBuildSettingsScene(BuildScenes[2], true)
        };

        ConfigureBundledAndroidTools();

        AssetDatabase.SaveAssets();

        Debug.Log($"Android build settings configured. Package: {PackageName}");
        if (!silent)
        {
            EditorUtility.DisplayDialog(
                "Android Build Settings",
                "Android build settings configured:\n" +
                $"- Package: {PackageName}\n" +
                "- Min API: 26\n" +
                "- Target API: 35\n" +
                "- Orientation: Landscape Left\n" +
                "- Architecture: ARM64\n" +
                "- Graphics API: OpenGL ES3\n" +
                "- Scripting backend: IL2CPP",
                "OK");
        }
    }

    [MenuItem("RTS/Build Android APK")]
    public static void BuildAndroid()
    {
        SetupAndroid(true);
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

        var options = new BuildPlayerOptions
        {
            scenes = BuildScenes,
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"APK build succeeded: {report.summary.outputPath}");
        }
        else
        {
            Debug.LogError($"APK build failed: {report.summary.totalErrors} errors");
        }
    }

    private static void ConfigureBundledAndroidTools()
    {
        string editorRoot = EditorApplication.applicationContentsPath;
        string playerRoot = Path.Combine(editorRoot, "PlaybackEngines", "AndroidPlayer");
        string jdkPath = Path.Combine(playerRoot, "OpenJDK");
        string ndkPath = Path.Combine(playerRoot, "NDK");

        if (Directory.Exists(jdkPath))
        {
            EditorPrefs.SetString("JdkPath", jdkPath);
        }

        if (Directory.Exists(ndkPath))
        {
            EditorPrefs.SetString("AndroidNdkRootR21D", ndkPath);
        }
    }
}
