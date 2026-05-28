using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyModelPreview : MonoBehaviour, IDragHandler, IScrollHandler, ICanvasRaycastFilter
{
    [Header("UI")]
    public RawImage TargetImage;
    public int TextureSize = 768;
    public bool AutoTextureSize = true;
    public int MinTextureSize = 256;
    public int MaxTextureSize = 1024;

    [Header("Model Source")]
    public GameObject ModelPrefab;
    public bool PreferStreamingAssetBundle = true;
    public string AssetBundlePath = "LobbyModels/lobbyhero";
    public string AssetBundleAssetName = "LobbyHero";
    public string ResourcesPath = "Models/LobbyHero";
    public bool EnableRuntimeObjFallback = true;
    public string RuntimeObjPath = "LobbyModels/LobbyHero.obj";
    public float MaxRuntimeObjFileSizeMB = 24f;
    public int MaxRuntimeObjOutputVertices = 120000;
    public int MaxRuntimeObjTriangles = 180000;
    public string[] AlternativeResourcePaths =
    {
        "Models/Tank",
        "Lobby/LobbyHero",
        "Units/Tank",
        "Prefabs/Tank_P",
        "Prefabs/Tank"
    };

    [Header("Presentation")]
    public float TargetHeight = 2.45f;
    public Vector3 ModelEulerAngles = new Vector3(0f, 145f, 0f);
    public bool AutoRotate = true;
    public float RotateSpeed = 10f;
    public float CameraDistance = 5.2f;
    public bool AutoFrameCamera = true;
    public float FramePadding = 1.25f;
    public float MinCameraDistance = 2.6f;
    public float MaxCameraDistance = 10f;
    public float CameraLookHeightOffset = 0.05f;
    public bool AllowPointerInteraction = true;
    public bool RequireAltForPointerInteraction = true;
    public bool DragToRotate = true;
    public float DragRotateSpeed = 0.35f;
    public bool ScrollToZoom = true;
    public float ScrollZoomSpeed = 0.35f;
    public bool ShowStagePlatform = true;
    public float StagePlatformRadius = 1.15f;

    [Header("Optimization")]
    public bool ShowFallbackWhileLoading = false;
    public bool PauseWhenHidden = true;
    public bool RenderOnlyWhenAnimating = true;
    public bool DisablePreviewColliders = true;
    public bool DisablePreviewShadows = true;
    public bool FillMissingPreviewMaterials = true;

    [Header("Diagnostics")]
    public string LastLoadSource = "None";
    public string LastLoadPath = "";
    public string LastLoadWarning = "";

    RenderTexture previewTexture;
    GameObject stageRoot;
    Transform modelRoot;
    GameObject stageDecorRoot;
    GameObject currentModel;
    bool currentModelIsFallback;
    Camera previewCamera;
    Coroutine loadRoutine;
    int stageLayer;
    bool previewDirty = true;
    string pendingLoadSource = "";
    readonly List<Material> generatedMaterials = new List<Material>();
    readonly List<Material> stageMaterials = new List<Material>();
    readonly List<Mesh> generatedMeshes = new List<Mesh>();
    readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    static readonly Dictionary<string, AssetBundle> LoadedBundles = new Dictionary<string, AssetBundle>();

    void OnValidate()
    {
        MinTextureSize = Mathf.Clamp(MinTextureSize, 128, 2048);
        MaxTextureSize = Mathf.Clamp(MaxTextureSize, MinTextureSize, 2048);
        TextureSize = Mathf.Clamp(TextureSize, 128, 2048);
        TargetHeight = Mathf.Max(0.1f, TargetHeight);
        CameraDistance = Mathf.Max(0.5f, CameraDistance);
        FramePadding = Mathf.Max(1f, FramePadding);
        MinCameraDistance = Mathf.Max(0.5f, MinCameraDistance);
        MaxCameraDistance = Mathf.Max(MinCameraDistance, MaxCameraDistance);
        DragRotateSpeed = Mathf.Max(0f, DragRotateSpeed);
        ScrollZoomSpeed = Mathf.Max(0f, ScrollZoomSpeed);
        MaxRuntimeObjFileSizeMB = Mathf.Max(1f, MaxRuntimeObjFileSizeMB);
        MaxRuntimeObjOutputVertices = Mathf.Max(1000, MaxRuntimeObjOutputVertices);
        MaxRuntimeObjTriangles = Mathf.Max(1000, MaxRuntimeObjTriangles);
        StagePlatformRadius = Mathf.Clamp(StagePlatformRadius, 0.35f, 4f);
    }

    void OnEnable()
    {
        EnsureStage();
        BeginLoadModel();
    }

    void OnDisable()
    {
        ReleaseStage();
    }

    void OnApplicationQuit()
    {
        UnloadCachedAssetBundles(false);
    }

    void Update()
    {
        RefreshTextureSizeIfNeeded();

        bool shouldRender = ShouldRenderPreview();
        UpdatePreviewCameraState(shouldRender);

        if (!shouldRender || !AutoRotate || modelRoot == null)
            return;

        modelRoot.Rotate(Vector3.up, RotateSpeed * Time.unscaledDeltaTime, Space.World);
    }

    void RefreshTextureSizeIfNeeded()
    {
        if (!AutoTextureSize || previewTexture == null)
            return;

        int desiredSize = GetDesiredTextureSize();
        if (previewTexture.width != desiredSize || previewTexture.height != desiredSize)
            EnsureStage();
    }

    public void ReloadModel()
    {
        ClearModel();
        BeginLoadModel();
    }

    public void RenderPreviewOnce()
    {
        EnsureStage();
        if (currentModel == null && loadRoutine == null)
            BeginLoadModel();
        if (previewCamera == null || previewTexture == null)
            return;

        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
        previewDirty = false;
    }

    public void ResetPreviewView()
    {
        if (modelRoot != null)
            modelRoot.localRotation = Quaternion.Euler(ModelEulerAngles);
        if (currentModel != null)
            FrameCameraForModel(currentModel);

        previewDirty = true;
    }

    public Texture2D CapturePreviewTexture()
    {
        RenderPreviewOnce();
        if (previewTexture == null)
            return null;

        var previous = RenderTexture.active;
        RenderTexture.active = previewTexture;

        var capture = new Texture2D(previewTexture.width, previewTexture.height, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0f, 0f, previewTexture.width, previewTexture.height), 0, 0);
        capture.Apply(false, false);

        RenderTexture.active = previous;
        return capture;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsPointerInteractionActive() || !DragToRotate || modelRoot == null || eventData == null)
            return;

        modelRoot.Rotate(Vector3.up, -eventData.delta.x * DragRotateSpeed, Space.World);
        previewDirty = true;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!IsPointerInteractionActive() || !ScrollToZoom || previewCamera == null || eventData == null)
            return;

        CameraDistance = Mathf.Clamp(
            CameraDistance - eventData.scrollDelta.y * ScrollZoomSpeed,
            MinCameraDistance,
            MaxCameraDistance);
        MoveCameraToDistance(CameraDistance);
        previewDirty = true;
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        return IsPointerInteractionActive();
    }

    bool IsPointerInteractionActive()
    {
        if (!AllowPointerInteraction || (!DragToRotate && !ScrollToZoom))
            return false;
        if (!RequireAltForPointerInteraction)
            return true;

        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }

    public static void UnloadCachedAssetBundles(bool unloadAllLoadedObjects)
    {
        foreach (var pair in LoadedBundles)
        {
            if (pair.Value != null)
                pair.Value.Unload(unloadAllLoadedObjects);
        }

        LoadedBundles.Clear();
    }

    void EnsureStage()
    {
        if (TargetImage == null)
            TargetImage = GetComponent<RawImage>();

        stageLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (stageLayer < 0)
            stageLayer = gameObject.layer;

        int size = GetDesiredTextureSize();
        TextureSize = size;

        if (previewTexture == null || previewTexture.width != size || previewTexture.height != size)
        {
            ReleaseTexture();
            previewTexture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
            previewTexture.name = name + "_RT";
            previewTexture.hideFlags = HideFlags.HideAndDontSave;
            previewTexture.antiAliasing = 2;
            previewTexture.useMipMap = false;
            previewTexture.Create();
            previewDirty = true;
        }

        if (TargetImage != null)
        {
            TargetImage.texture = previewTexture;
            TargetImage.raycastTarget = AllowPointerInteraction && (DragToRotate || ScrollToZoom);
        }

        if (previewCamera != null)
            previewCamera.targetTexture = previewTexture;

        if (stageRoot != null)
            return;

        stageRoot = new GameObject(name + "_Stage");
        stageRoot.hideFlags = HideFlags.HideAndDontSave;
        stageRoot.transform.position = GetStagePosition();
        SetLayerRecursive(stageRoot, stageLayer);

        modelRoot = new GameObject("ModelRoot").transform;
        modelRoot.gameObject.hideFlags = HideFlags.HideAndDontSave;
        modelRoot.SetParent(stageRoot.transform, false);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.Euler(ModelEulerAngles);
        SetLayerRecursive(modelRoot.gameObject, stageLayer);

        CreatePreviewCamera();
        CreatePreviewLights();
        CreateStageDecor();
    }

    void CreatePreviewCamera()
    {
        var camGO = new GameObject("PreviewCamera");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        camGO.transform.SetParent(stageRoot.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 1.28f, -CameraDistance);
        camGO.transform.LookAt(stageRoot.transform.position + new Vector3(0f, 1.05f, 0f), Vector3.up);
        SetLayerRecursive(camGO, stageLayer);

        previewCamera = camGO.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << stageLayer;
        previewCamera.fieldOfView = 27f;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = 25f;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = true;
        previewCamera.targetTexture = previewTexture;
        previewCamera.enabled = ShouldRenderPreview();
        previewDirty = true;
    }

    void CreatePreviewLights()
    {
        CreateLight("KeyLight", LightType.Directional, new Vector3(36f, -30f, 0f), 1.42f, new Color(1f, 0.94f, 0.80f));
        CreateLight("FillLight", LightType.Directional, new Vector3(10f, 132f, 0f), 0.50f, new Color(0.58f, 0.74f, 1f));
        CreateLight("RimLight", LightType.Directional, new Vector3(-10f, 218f, 0f), 0.92f, new Color(1f, 0.70f, 0.40f));
    }

    void CreateLight(string lightName, LightType type, Vector3 euler, float intensity, Color color)
    {
        var lightGO = new GameObject(lightName);
        lightGO.hideFlags = HideFlags.HideAndDontSave;
        lightGO.transform.SetParent(stageRoot.transform, false);
        lightGO.transform.localRotation = Quaternion.Euler(euler);
        SetLayerRecursive(lightGO, stageLayer);

        var light = lightGO.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
    }

    void CreateStageDecor()
    {
        if (!ShowStagePlatform || stageRoot == null || stageDecorRoot != null)
            return;

        stageDecorRoot = new GameObject("StageDecor");
        stageDecorRoot.hideFlags = HideFlags.HideAndDontSave;
        stageDecorRoot.transform.SetParent(stageRoot.transform, false);
        stageDecorRoot.transform.localPosition = Vector3.zero;
        SetLayerRecursive(stageDecorRoot, stageLayer);

        var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        platform.name = "PreviewPlatform";
        platform.hideFlags = HideFlags.HideAndDontSave;
        platform.transform.SetParent(stageDecorRoot.transform, false);
        platform.transform.localPosition = new Vector3(0f, -0.045f, 0f);
        platform.transform.localScale = new Vector3(StagePlatformRadius, 0.055f, StagePlatformRadius * 0.72f);
        SetLayerRecursive(platform, stageLayer);
        DestroyPreviewCollider(platform);
        ApplySharedMaterial(platform, CreateStageMaterial("PreviewPlatformMat", new Color(0.18f, 0.155f, 0.085f, 1f)));

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "PreviewPlatformRim";
        rim.hideFlags = HideFlags.HideAndDontSave;
        rim.transform.SetParent(stageDecorRoot.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0.005f, 0f);
        rim.transform.localScale = new Vector3(StagePlatformRadius * 1.04f, 0.018f, StagePlatformRadius * 0.76f);
        SetLayerRecursive(rim, stageLayer);
        DestroyPreviewCollider(rim);
        ApplySharedMaterial(rim, CreateStageMaterial("PreviewPlatformRimMat", new Color(0.72f, 0.57f, 0.25f, 1f)));

        var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "PreviewSoftShadow";
        shadow.hideFlags = HideFlags.HideAndDontSave;
        shadow.transform.SetParent(stageDecorRoot.transform, false);
        shadow.transform.localPosition = new Vector3(0.10f, -0.02f, -0.03f);
        shadow.transform.localScale = new Vector3(StagePlatformRadius * 0.90f, 0.006f, StagePlatformRadius * 0.52f);
        SetLayerRecursive(shadow, stageLayer);
        DestroyPreviewCollider(shadow);
        ApplySharedMaterial(shadow, CreateStageMaterial("PreviewSoftShadowMat", new Color(0.025f, 0.022f, 0.014f, 1f)));
    }

    static void DestroyPreviewCollider(GameObject go)
    {
        var collider = go != null ? go.GetComponent<Collider>() : null;
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
    }

    static void ApplySharedMaterial(GameObject go, Material material)
    {
        if (go == null || material == null)
            return;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    Material CreateStageMaterial(string materialName, Color color)
    {
        var material = CreateMaterial(materialName, color);
        if (material != null)
        {
            generatedMaterials.Remove(material);
            stageMaterials.Add(material);
        }
        return material;
    }

    void BeginLoadModel()
    {
        EnsureStage();

        if (currentModel != null || loadRoutine != null)
            return;

        LastLoadSource = "Loading";
        LastLoadPath = "";
        LastLoadWarning = "";
        pendingLoadSource = "";

        if (ModelPrefab != null || !Application.isPlaying)
        {
            CreateModelInstance(FindModelPrefabSync());
            return;
        }

        if (ShowFallbackWhileLoading && currentModel == null)
            CreateModelInstance(null);

        loadRoutine = StartCoroutine(LoadModelRoutine());
    }

    IEnumerator LoadModelRoutine()
    {
        GameObject prefab = null;
        if (PreferStreamingAssetBundle)
            yield return LoadAssetBundlePrefabAsync(value => prefab = value);

        if (prefab == null)
            yield return LoadResourcePrefabAsync(ResourcesPath, value => prefab = value);

        if (prefab == null && AlternativeResourcePaths != null)
        {
            for (int i = 0; i < AlternativeResourcePaths.Length && prefab == null; i++)
                yield return LoadResourcePrefabAsync(AlternativeResourcePaths[i], value => prefab = value);
        }

        loadRoutine = null;
        if (prefab != null)
            CreateModelInstance(prefab);
        else
            CreateRuntimeObjOrFallback();
    }

    void CreateModelInstance(GameObject prefab)
    {
        if (modelRoot == null)
            return;

        if (currentModel != null)
        {
            if (!currentModelIsFallback || prefab == null)
                return;

            DestroyCurrentModelObject();
            ReleaseGeneratedMaterials();
        }

        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("Prefabs/Tank_P")
                ?? Resources.Load<GameObject>("Prefabs/Tank")
                ?? Resources.Load<GameObject>("Prefabs/Tank_E");
            pendingLoadSource = prefab != null ? "Downloaded prefab fallback" : pendingLoadSource;
        }

        if (prefab == null)
        {
            currentModel = null;
            currentModelIsFallback = false;
            LastLoadSource = "No downloaded preview model";
            LastLoadPath = "";
            pendingLoadSource = "";
            return;
        }

        bool wasModelRootActive = modelRoot.gameObject.activeSelf;
        modelRoot.gameObject.SetActive(false);
        currentModel = Instantiate(prefab, modelRoot);
        currentModelIsFallback = false;
        LastLoadSource = (string.IsNullOrEmpty(pendingLoadSource) ? "Loaded model" : pendingLoadSource) + ": " + prefab.name;
        pendingLoadSource = "";

        currentModel.name = prefab.name + "_Preview";
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.identity;
        currentModel.transform.localScale = Vector3.one;

        SetLayerRecursive(currentModel, stageLayer);
        OptimizePreviewInstance(currentModel);
        FitToStage(currentModel);
        FrameCameraForModel(currentModel);
        modelRoot.gameObject.SetActive(wasModelRootActive);
        previewDirty = true;
    }

    GameObject FindModelPrefabSync()
    {
        if (ModelPrefab != null)
        {
            pendingLoadSource = "Direct prefab";
            LastLoadPath = ModelPrefab.name;
            return ModelPrefab;
        }

        if (PreferStreamingAssetBundle && Application.isPlaying)
        {
            var bundledPrefab = LoadAssetBundlePrefabSync();
            if (bundledPrefab != null)
                return bundledPrefab;
        }

        var prefab = LoadResourcePrefab(ResourcesPath);
        if (prefab != null || AlternativeResourcePaths == null)
            return prefab;

        for (int i = 0; i < AlternativeResourcePaths.Length; i++)
        {
            prefab = LoadResourcePrefab(AlternativeResourcePaths[i]);
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    void CreateRuntimeObjOrFallback()
    {
        if (EnableRuntimeObjFallback)
        {
            if (currentModel != null && currentModelIsFallback)
            {
                DestroyCurrentModelObject();
                ReleaseGeneratedMaterials();
                ReleaseGeneratedMeshes();
                ReleaseGeneratedTextures();
            }

            var objModel = LoadRuntimeObjModel();
            if (objModel != null)
            {
                CreateLoadedModelInstance(objModel);
                return;
            }
        }

        CreateModelInstance(null);
    }

    GameObject LoadResourcePrefab(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Trim().Length == 0)
            return null;

        string cleanPath = path.Trim();
        var prefab = Resources.Load<GameObject>(cleanPath);
        if (prefab != null)
        {
            pendingLoadSource = "Resources";
            LastLoadPath = cleanPath;
        }

        return prefab;
    }

    IEnumerator LoadResourcePrefabAsync(string path, System.Action<GameObject> onLoaded)
    {
        if (string.IsNullOrEmpty(path) || path.Trim().Length == 0)
        {
            onLoaded(null);
            yield break;
        }

        string cleanPath = path.Trim();
        var request = Resources.LoadAsync<GameObject>(cleanPath);
        yield return request;
        var prefab = request.asset as GameObject;
        if (prefab != null)
        {
            pendingLoadSource = "Resources";
            LastLoadPath = cleanPath;
        }

        onLoaded(prefab);
    }

    GameObject LoadRuntimeObjModel()
    {
        string fullPath = FindExistingObjFullPath();
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            return null;

        var fileInfo = new FileInfo(fullPath);
        float maxBytes = Mathf.Max(1f, MaxRuntimeObjFileSizeMB) * 1024f * 1024f;
        if (fileInfo.Length > maxBytes)
        {
            LastLoadWarning = "Runtime OBJ skipped because the file is larger than MaxRuntimeObjFileSizeMB.";
            LastLoadPath = fullPath;
            return null;
        }

        var material = CreateMaterial("RuntimeObjPreview", new Color(0.48f, 0.56f, 0.42f, 1f));
        ApplyRuntimeObjTexture(material, fullPath);
        var model = SimpleObjModelLoader.LoadFromFile(
            fullPath,
            material,
            MaxRuntimeObjOutputVertices,
            MaxRuntimeObjTriangles);
        if (model == null)
        {
            LastLoadWarning = "Runtime OBJ parse failed.";
            ReleaseGeneratedMaterials();
            ReleaseGeneratedTextures();
            return null;
        }

        TrackGeneratedMeshes(model);
        LastLoadPath = fullPath;
        return model;
    }

    void ApplyRuntimeObjTexture(Material material, string objPath)
    {
        if (material == null || string.IsNullOrEmpty(objPath))
            return;

        bool textureApplied = ApplyRuntimeObjMtl(material, objPath);
        if (textureApplied)
            return;

        string texturePath = FindRuntimeObjTexturePath(objPath);
        if (string.IsNullOrEmpty(texturePath))
            return;

        LoadTextureIntoMaterial(material, texturePath);
    }

    bool ApplyRuntimeObjMtl(Material material, string objPath)
    {
        string mtlPath = FindRuntimeObjMtlPath(objPath);
        if (string.IsNullOrEmpty(mtlPath) || !File.Exists(mtlPath))
            return false;

        string texturePath = null;
        var lines = File.ReadAllLines(mtlPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (line.StartsWith("Kd ", System.StringComparison.OrdinalIgnoreCase))
                ApplyMtlDiffuseColor(material, line);
            else if (line.StartsWith("map_Kd ", System.StringComparison.OrdinalIgnoreCase)
                     || line.StartsWith("map_BaseColor ", System.StringComparison.OrdinalIgnoreCase)
                     || line.StartsWith("map_diffuse ", System.StringComparison.OrdinalIgnoreCase))
                texturePath = ResolveMtlTexturePath(mtlPath, line);
        }

        if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath))
            return false;

        LoadTextureIntoMaterial(material, texturePath);
        return true;
    }

    string FindRuntimeObjMtlPath(string objPath)
    {
        string sameNameMtl = Path.Combine(Path.GetDirectoryName(objPath), Path.GetFileNameWithoutExtension(objPath) + ".mtl");
        if (File.Exists(sameNameMtl))
            return sameNameMtl;

        var lines = File.ReadAllLines(objPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.StartsWith("mtllib ", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string relativeMtl = line.Substring(7).Trim().Trim('"');
            string fullPath = Path.IsPathRooted(relativeMtl)
                ? relativeMtl
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(objPath), relativeMtl.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    void ApplyMtlDiffuseColor(Material material, string line)
    {
        string[] parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return;

        float r = ParseInvariantFloat(parts[1], 0.48f);
        float g = ParseInvariantFloat(parts[2], 0.56f);
        float b = ParseInvariantFloat(parts[3], 0.42f);
        SetMaterialColor(material, new Color(r, g, b, 1f));
    }

    string ResolveMtlTexturePath(string mtlPath, string line)
    {
        string[] parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        string relativeTexture = parts[parts.Length - 1].Trim('"');
        return Path.IsPathRooted(relativeTexture)
            ? relativeTexture
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mtlPath), relativeTexture.Replace('/', Path.DirectorySeparatorChar)));
    }

    void LoadTextureIntoMaterial(Material material, string texturePath)
    {
        byte[] bytes = File.ReadAllBytes(texturePath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = Path.GetFileNameWithoutExtension(texturePath) + "_PreviewTexture";
        texture.hideFlags = HideFlags.DontSave;
        if (!texture.LoadImage(bytes))
        {
            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
            return;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        generatedTextures.Add(texture);

        SetMaterialTexture(material, texture);
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    void SetMaterialTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    float ParseInvariantFloat(string value, float fallback)
    {
        float result;
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return result;

        return fallback;
    }

    string FindRuntimeObjTexturePath(string objPath)
    {
        string dir = Path.GetDirectoryName(objPath);
        string stem = Path.Combine(dir, Path.GetFileNameWithoutExtension(objPath));
        string[] candidates =
        {
            stem + ".png",
            stem + ".jpg",
            stem + ".jpeg",
            Path.Combine(dir, "LobbyHero.png"),
            Path.Combine(dir, "LobbyHero.jpg"),
            Path.Combine(dir, "LobbyHero.jpeg")
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
                return candidates[i];
        }

        return null;
    }

    string FindExistingObjFullPath()
    {
        if (string.IsNullOrEmpty(RuntimeObjPath) || RuntimeObjPath.Trim().Length == 0)
            return null;

        string relativePath = RuntimeObjPath.Trim().Replace('\\', '/').TrimStart('/');
        var candidates = new List<string>();
        AddObjPathCandidate(candidates, relativePath);

        string fileName = Path.GetFileName(relativePath);
        string directory = Path.GetDirectoryName(relativePath);
        if (!string.IsNullOrEmpty(fileName) && !fileName.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
            AddObjPathCandidate(candidates, CombineAssetBundleRelativePath(directory, fileName + ".obj"));

        for (int i = 0; i < candidates.Count; i++)
        {
            if (File.Exists(candidates[i]))
                return candidates[i];
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    void AddObjPathCandidate(List<string> candidates, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return;

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!candidates.Contains(fullPath))
            candidates.Add(fullPath);
    }

    void CreateLoadedModelInstance(GameObject model)
    {
        if (modelRoot == null)
        {
            if (Application.isPlaying)
                Destroy(model);
            else
                DestroyImmediate(model);
            return;
        }

        if (currentModel != null)
        {
            if (!currentModelIsFallback)
            {
                if (Application.isPlaying)
                    Destroy(model);
                else
                    DestroyImmediate(model);
                return;
            }

            DestroyCurrentModelObject();
        }

        currentModel = model;
        currentModelIsFallback = false;
        LastLoadSource = "Runtime OBJ: " + model.name;
        currentModel.name = model.name + "_Preview";
        currentModel.transform.SetParent(modelRoot, false);
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.identity;
        currentModel.transform.localScale = Vector3.one;

        SetLayerRecursive(currentModel, stageLayer);
        OptimizePreviewInstance(currentModel);
        FitToStage(currentModel);
        FrameCameraForModel(currentModel);
        previewDirty = true;
    }

    GameObject LoadAssetBundlePrefabSync()
    {
        string bundleFullPath = FindExistingAssetBundleFullPath();
        if (string.IsNullOrEmpty(bundleFullPath) || !File.Exists(bundleFullPath))
            return null;

        var bundle = GetLoadedBundle(bundleFullPath);
        if (bundle == null)
            bundle = AssetBundle.LoadFromFile(bundleFullPath);
        if (bundle == null)
            return null;

        LoadedBundles[bundleFullPath] = bundle;
        var prefab = LoadPrefabFromBundle(bundle);
        if (prefab != null)
        {
            pendingLoadSource = "AssetBundle";
            LastLoadPath = bundleFullPath;
        }

        return prefab;
    }

    IEnumerator LoadAssetBundlePrefabAsync(System.Action<GameObject> onLoaded)
    {
        string bundleFullPath = FindExistingAssetBundleFullPath();
        if (string.IsNullOrEmpty(bundleFullPath) || !File.Exists(bundleFullPath))
        {
            onLoaded(null);
            yield break;
        }

        var bundle = GetLoadedBundle(bundleFullPath);
        if (bundle == null)
        {
            var bundleRequest = AssetBundle.LoadFromFileAsync(bundleFullPath);
            yield return bundleRequest;
            bundle = bundleRequest.assetBundle;
            if (bundle != null)
                LoadedBundles[bundleFullPath] = bundle;
        }

        if (bundle == null)
        {
            onLoaded(null);
            yield break;
        }

        string assetName = ResolveBundleAssetName(bundle);
        var assetRequest = bundle.LoadAssetAsync<GameObject>(assetName);
        yield return assetRequest;
        var prefab = assetRequest.asset as GameObject;
        if (prefab != null)
        {
            pendingLoadSource = "AssetBundle";
            LastLoadPath = bundleFullPath + "::" + assetName;
        }

        onLoaded(prefab);
    }

    AssetBundle GetLoadedBundle(string bundleFullPath)
    {
        AssetBundle bundle;
        if (LoadedBundles.TryGetValue(bundleFullPath, out bundle) && bundle != null)
            return bundle;

        LoadedBundles.Remove(bundleFullPath);
        return null;
    }

    GameObject LoadPrefabFromBundle(AssetBundle bundle)
    {
        if (bundle == null)
            return null;

        return bundle.LoadAsset<GameObject>(ResolveBundleAssetName(bundle));
    }

    string FindExistingAssetBundleFullPath()
    {
        string[] candidates = GetAssetBundleFullPathCandidates();
        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
                return candidates[i];
        }

        return candidates.Length > 0 ? candidates[0] : null;
    }

    string[] GetAssetBundleFullPathCandidates()
    {
        if (string.IsNullOrEmpty(AssetBundlePath) || AssetBundlePath.Trim().Length == 0)
            return new string[0];

        string relativePath = AssetBundlePath.Trim().Replace('\\', '/').TrimStart('/');
        var candidates = new List<string>();
        AddBundlePathCandidate(candidates, relativePath);

        string fileName = Path.GetFileName(relativePath);
        string directory = Path.GetDirectoryName(relativePath);
        if (!string.IsNullOrEmpty(fileName))
        {
            AddBundlePathCandidate(candidates, CombineAssetBundleRelativePath(directory, fileName.ToLowerInvariant()));
            AddBundlePathCandidate(candidates, CombineAssetBundleRelativePath(directory, fileName + ".bundle"));
            AddBundlePathCandidate(candidates, CombineAssetBundleRelativePath(directory, fileName + ".unity3d"));
            AddBundlePathCandidate(candidates, CombineAssetBundleRelativePath(directory, fileName.ToLowerInvariant() + ".bundle"));
            AddBundlePathCandidate(candidates, CombineAssetBundleRelativePath(directory, fileName.ToLowerInvariant() + ".unity3d"));
        }

        return candidates.ToArray();
    }

    void AddBundlePathCandidate(List<string> candidates, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return;

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!candidates.Contains(fullPath))
            candidates.Add(fullPath);
    }

    string CombineAssetBundleRelativePath(string directory, string fileName)
    {
        if (string.IsNullOrEmpty(directory))
            return fileName;

        return directory.Replace('\\', '/') + "/" + fileName;
    }

    string GetAssetBundleAssetName()
    {
        if (!string.IsNullOrEmpty(AssetBundleAssetName) && AssetBundleAssetName.Trim().Length > 0)
            return AssetBundleAssetName.Trim();

        return "LobbyHero";
    }

    string ResolveBundleAssetName(AssetBundle bundle)
    {
        string preferredName = GetAssetBundleAssetName();
        if (bundle == null)
            return preferredName;

        string[] assetNames = bundle.GetAllAssetNames();
        for (int i = 0; i < assetNames.Length; i++)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(assetNames[i]);
            if (string.Equals(nameWithoutExtension, preferredName, System.StringComparison.OrdinalIgnoreCase))
                return assetNames[i];
        }

        for (int i = 0; i < assetNames.Length; i++)
        {
            if (assetNames[i].EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                return assetNames[i];
        }

        return preferredName;
    }

    void OptimizePreviewInstance(GameObject root)
    {
        var agents = root.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
            agents[i].enabled = false;

        var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        if (DisablePreviewColliders)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        var animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (FillMissingPreviewMaterials)
                EnsurePreviewRendererMaterial(renderers[i]);

            if (DisablePreviewShadows)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
        }
    }

    void EnsurePreviewRendererMaterial(Renderer renderer)
    {
        if (renderer == null)
            return;

        var materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            renderer.sharedMaterial = CreateMaterial("MissingPreviewMaterial", new Color(0.48f, 0.56f, 0.42f, 1f));
            return;
        }

        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                continue;

            materials[i] = CreateMaterial("MissingPreviewMaterial", new Color(0.48f, 0.56f, 0.42f, 1f));
            changed = true;
        }

        if (changed)
            renderer.sharedMaterials = materials;
    }

    void FitToStage(GameObject root)
    {
        var bounds = GetRendererBounds(root);
        if (bounds.size == Vector3.zero)
            return;

        float height = Mathf.Max(0.01f, bounds.size.y);
        float scale = TargetHeight / height;
        root.transform.localScale *= scale;

        bounds = GetRendererBounds(root);
        Vector3 correction = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        root.transform.position += correction;
    }

    void FrameCameraForModel(GameObject root)
    {
        if (!AutoFrameCamera || previewCamera == null || root == null)
            return;

        var bounds = GetRendererBounds(root);
        if (bounds.size == Vector3.zero)
            return;

        float aspect = previewTexture != null && previewTexture.height > 0
            ? previewTexture.width / (float)previewTexture.height
            : 1f;
        aspect = Mathf.Max(0.1f, aspect);

        float verticalFov = Mathf.Max(5f, previewCamera.fieldOfView) * Mathf.Deg2Rad;
        float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * aspect);
        float halfHeight = Mathf.Max(0.1f, bounds.extents.y);
        float halfWidth = Mathf.Max(0.1f, Mathf.Max(bounds.extents.x, bounds.extents.z * 0.65f));
        float distanceForHeight = halfHeight / Mathf.Tan(verticalFov * 0.5f);
        float distanceForWidth = halfWidth / Mathf.Tan(horizontalFov * 0.5f);
        float distance = Mathf.Max(distanceForHeight, distanceForWidth) * FramePadding + bounds.extents.z;
        distance = Mathf.Clamp(distance, MinCameraDistance, MaxCameraDistance);

        Vector3 localTarget = stageRoot.transform.InverseTransformPoint(bounds.center);
        localTarget.y += CameraLookHeightOffset;
        previewCamera.transform.localPosition = new Vector3(localTarget.x, localTarget.y, localTarget.z - distance);
        previewCamera.transform.LookAt(stageRoot.transform.TransformPoint(localTarget), Vector3.up);
        CameraDistance = distance;
    }

    void MoveCameraToDistance(float distance)
    {
        if (previewCamera == null)
            return;

        Vector3 target = stageRoot != null ? stageRoot.transform.position + Vector3.up : Vector3.up;
        if (currentModel != null)
        {
            var bounds = GetRendererBounds(currentModel);
            if (bounds.size != Vector3.zero)
                target = bounds.center + Vector3.up * CameraLookHeightOffset;
        }

        Vector3 direction = previewCamera.transform.position - target;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.back;
        direction.Normalize();

        previewCamera.transform.position = target + direction * distance;
        previewCamera.transform.LookAt(target, Vector3.up);
    }

    Bounds GetRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    Material CreateMaterial(string materialName, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.DontSave;
        generatedMaterials.Add(material);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);

        return material;
    }

    Vector3 GetStagePosition()
    {
        int offset = Mathf.Abs(GetInstanceID() % 64);
        return new Vector3(1000f + offset * 8f, -1000f, 1000f);
    }

    void ClearModel()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        if (currentModel != null)
        {
            DestroyCurrentModelObject();
        }

        ReleaseGeneratedMaterials();
        ReleaseGeneratedMeshes();
        ReleaseGeneratedTextures();
    }

    void DestroyCurrentModelObject()
    {
        if (currentModel == null)
            return;

        if (Application.isPlaying)
            Destroy(currentModel);
        else
            DestroyImmediate(currentModel);

        currentModel = null;
        currentModelIsFallback = false;
    }

    void ReleaseStage()
    {
        ClearModel();

        if (stageRoot != null)
        {
            if (Application.isPlaying)
                Destroy(stageRoot);
            else
                DestroyImmediate(stageRoot);

            stageRoot = null;
            stageDecorRoot = null;
            modelRoot = null;
            previewCamera = null;
        }

        ReleaseStageMaterials();

        if (TargetImage != null)
            TargetImage.texture = null;

        ReleaseTexture();
    }

    void ReleaseTexture()
    {
        if (previewTexture == null)
            return;

        if (previewCamera != null)
            previewCamera.targetTexture = null;

        previewTexture.Release();

        if (Application.isPlaying)
            Destroy(previewTexture);
        else
            DestroyImmediate(previewTexture);

        previewTexture = null;
    }

    void TrackGeneratedMeshes(GameObject root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            var mesh = filters[i].sharedMesh;
            if (mesh != null && !generatedMeshes.Contains(mesh))
                generatedMeshes.Add(mesh);
        }
    }

    void ReleaseGeneratedMaterials()
    {
        for (int i = 0; i < generatedMaterials.Count; i++)
        {
            var material = generatedMaterials[i];
            if (material == null)
                continue;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        generatedMaterials.Clear();
    }

    void ReleaseStageMaterials()
    {
        for (int i = 0; i < stageMaterials.Count; i++)
        {
            var material = stageMaterials[i];
            if (material == null)
                continue;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        stageMaterials.Clear();
    }

    void ReleaseGeneratedMeshes()
    {
        for (int i = 0; i < generatedMeshes.Count; i++)
        {
            var mesh = generatedMeshes[i];
            if (mesh == null)
                continue;

            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        generatedMeshes.Clear();
    }

    void ReleaseGeneratedTextures()
    {
        for (int i = 0; i < generatedTextures.Count; i++)
        {
            var texture = generatedTextures[i];
            if (texture == null)
                continue;

            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
        }

        generatedTextures.Clear();
    }

    int GetDesiredTextureSize()
    {
        int minSize = Mathf.Clamp(MinTextureSize, 128, 2048);
        int maxSize = Mathf.Clamp(MaxTextureSize, minSize, 2048);
        if (!AutoTextureSize || TargetImage == null)
            return Mathf.Clamp(TextureSize, minSize, maxSize);

        var rectTransform = TargetImage.rectTransform;
        Rect rect = rectTransform != null ? rectTransform.rect : Rect.zero;
        float maxDimension = Mathf.Max(rect.width, rect.height);
        if (maxDimension <= 1f)
            return Mathf.Clamp(TextureSize, minSize, maxSize);

        int desired = Mathf.NextPowerOfTwo(Mathf.CeilToInt(maxDimension));
        return Mathf.Clamp(desired, minSize, maxSize);
    }

    void UpdatePreviewCameraState(bool shouldRender)
    {
        if (previewCamera == null)
            return;

        bool continuous = !RenderOnlyWhenAnimating || (AutoRotate && modelRoot != null);
        if (!shouldRender)
        {
            previewCamera.enabled = false;
            return;
        }

        if (continuous)
        {
            previewCamera.enabled = true;
            return;
        }

        previewCamera.enabled = false;
        if (previewDirty)
        {
            previewCamera.Render();
            previewDirty = false;
        }
    }

    void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
    }

    bool ShouldRenderPreview()
    {
        if (!PauseWhenHidden)
            return true;
        if (!isActiveAndEnabled)
            return false;
        if (TargetImage == null || !TargetImage.isActiveAndEnabled)
            return false;

        var canvasRenderer = TargetImage.canvasRenderer;
        return canvasRenderer == null || !canvasRenderer.cull;
    }
}
