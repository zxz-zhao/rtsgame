using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PubgLikeMapPreviewBuilder
{
    const string ScenePath = "Assets/Scenes/PubgLikeMapPreview.unity";
    const int HeightmapResolution = 513;
    const int AlphamapResolution = 512;
    const float TerrainSize = 720f;
    const float TerrainHeight = 92f;
    const float RiverHalfWidth = 18f;
    const float RiverBankWidth = 42f;
    const float WaterLevel01 = 0.105f;

    static readonly Vector2[] MainRoad =
    {
        new Vector2(-280f, -240f),
        new Vector2(-205f, -150f),
        new Vector2(-104f, -54f),
        new Vector2(0f, 0f),
        new Vector2(112f, 72f),
        new Vector2(214f, 164f),
        new Vector2(285f, 245f),
    };

    static readonly Vector3[] HousePositions =
    {
        new Vector3(-246f, 0f, -224f),
        new Vector3(-220f, 0f, -266f),
        new Vector3(-285f, 0f, -182f),
        new Vector3(-122f, 0f, 92f),
        new Vector3(-82f, 0f, 128f),
        new Vector3(246f, 0f, 224f),
        new Vector3(220f, 0f, 266f),
        new Vector3(286f, 0f, 182f),
        new Vector3(74f, 0f, -148f),
        new Vector3(118f, 0f, -174f),
    };

    static readonly string[] BuildingAssetPaths =
    {
        "Assets/External/Downloads/city-industrial/Models/FBX format/building-a.fbx",
        "Assets/External/Downloads/city-industrial/Models/FBX format/building-c.fbx",
        "Assets/External/Downloads/city-industrial/Models/FBX format/building-f.fbx",
        "Assets/External/Kenney/ModularBuildings/Models/FBX format/building-sample-house-a.fbx",
        "Assets/External/Kenney/ModularBuildings/Models/FBX format/building-sample-house-b.fbx",
        "Assets/External/Kenney/ModularBuildings/Models/FBX format/building-sample-house-c.fbx",
    };

    [MenuItem("RTS/Map/Build PUBG-Like Realistic Map Preview")]
    public static void Build()
    {
        UnityEngine.Random.InitState(1907);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Terrain terrain = CreateTerrain();
        CreateRiver();
        CreateRoads();
        CreateBridge();
        PlaceBuildings(terrain);
        ScatterNature(terrain);
        CreateSpawnMarkers(terrain);
        SetupLightingAndCamera(terrain);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[PubgLikeMapPreviewBuilder] Built scene: " + ScenePath);
    }

    static Terrain CreateTerrain()
    {
        TerrainData data = new TerrainData
        {
            heightmapResolution = HeightmapResolution,
            alphamapResolution = AlphamapResolution,
            size = new Vector3(TerrainSize, TerrainHeight, TerrainSize)
        };

        float[,] heights = new float[HeightmapResolution, HeightmapResolution];
        for (int z = 0; z < HeightmapResolution; z++)
        {
            float wz = IndexToWorld(z, HeightmapResolution);
            for (int x = 0; x < HeightmapResolution; x++)
            {
                float wx = IndexToWorld(x, HeightmapResolution);
                heights[z, x] = Mathf.Clamp01(Height01(wx, wz));
            }
        }
        data.SetHeights(0, 0, heights);
        data.terrainLayers = CreateTerrainLayers();
        PaintTerrain(data);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "PUBGStyle_Terrain";
        terrainObject.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.detailObjectDensity = 0.7f;
        terrain.treeDistance = 620f;
        terrain.detailObjectDistance = 110f;
        terrain.heightmapPixelError = 3f;

        TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
        if (collider != null)
            collider.terrainData = data;

        return terrain;
    }

    static TerrainLayer[] CreateTerrainLayers()
    {
        return new[]
        {
            CreateTerrainLayer("Assets/External/PolyHaven/EnvironmentTextures/forrest_ground_01/forrest_ground_01_diff_1k.jpg",
                "Assets/External/PolyHaven/EnvironmentTextures/forrest_ground_01/forrest_ground_01_nor_gl_1k.png",
                new Color(0.18f, 0.34f, 0.13f), new Vector2(16f, 16f)),
            CreateTerrainLayer("Assets/External/PolyHaven/EnvironmentTextures/rocky_trail/rocky_trail_diff_1k.jpg",
                "Assets/External/PolyHaven/EnvironmentTextures/rocky_trail/rocky_trail_nor_gl_1k.png",
                new Color(0.42f, 0.40f, 0.34f), new Vector2(18f, 18f)),
            CreateTerrainLayer("Assets/External/PolyHaven/EnvironmentTextures/forest_ground_04/forest_ground_04_diff_1k.jpg",
                "Assets/External/PolyHaven/EnvironmentTextures/forest_ground_04/forest_ground_04_nor_gl_1k.png",
                new Color(0.28f, 0.24f, 0.18f), new Vector2(14f, 14f)),
        };
    }

    static TerrainLayer CreateTerrainLayer(string diffusePath, string normalPath, Color fallbackColor, Vector2 tileSize)
    {
        TerrainLayer layer = new TerrainLayer
        {
            diffuseTexture = LoadTexture(diffusePath),
            normalMapTexture = LoadTexture(normalPath),
            tileSize = tileSize,
            smoothness = 0.18f,
            metallic = 0f
        };

        if (layer.diffuseTexture == null)
            layer.diffuseTexture = CreateSolidTexture(fallbackColor);

        return layer;
    }

    static void PaintTerrain(TerrainData data)
    {
        float[,,] alpha = new float[AlphamapResolution, AlphamapResolution, 3];
        for (int z = 0; z < AlphamapResolution; z++)
        {
            float wz = IndexToWorld(z, AlphamapResolution);
            for (int x = 0; x < AlphamapResolution; x++)
            {
                float wx = IndexToWorld(x, AlphamapResolution);
                float h = Height01(wx, wz);
                float slope = ApproxSlope(wx, wz);
                float river = RiverDistance(wx, wz);
                float road = DistanceToPolyline(new Vector2(wx, wz), MainRoad);

                float grass = 1f;
                float rock = Mathf.InverseLerp(0.20f, 0.46f, h) * 0.65f + Mathf.InverseLerp(0.14f, 0.35f, slope) * 0.55f;
                float dirt = Mathf.InverseLerp(RiverBankWidth + 18f, RiverBankWidth * 0.35f, river) + Mathf.InverseLerp(18f, 0f, road) * 1.4f;

                grass = Mathf.Max(0.03f, grass - rock * 0.65f - dirt * 0.85f);
                rock = Mathf.Clamp01(rock);
                dirt = Mathf.Clamp01(dirt);
                float sum = grass + rock + dirt;
                alpha[z, x, 0] = grass / sum;
                alpha[z, x, 1] = rock / sum;
                alpha[z, x, 2] = dirt / sum;
            }
        }

        data.SetAlphamaps(0, 0, alpha);
    }

    static void CreateRiver()
    {
        GameObject river = new GameObject("River_Main_Wide");
        MeshFilter filter = river.AddComponent<MeshFilter>();
        MeshRenderer renderer = river.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateWaterMaterial();

        const int samples = 120;
        Vector3[] vertices = new Vector3[samples * 2];
        int[] triangles = new int[(samples - 1) * 6];
        float half = TerrainSize * 0.5f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (samples - 1f);
            float z = Mathf.Lerp(-half, half, t);
            float center = RiverCenterX(z);
            float width = RiverHalfWidth + Mathf.Sin(t * Mathf.PI * 5f) * 3.5f;
            float y = WaterLevel01 * TerrainHeight + 0.15f;
            vertices[i * 2] = new Vector3(center - width, y, z);
            vertices[i * 2 + 1] = new Vector3(center + width, y, z);
            if (i >= samples - 1)
                continue;

            int vi = i * 2;
            int ti = i * 6;
            triangles[ti] = vi;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh { name = "River_Main_Wide_Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        filter.sharedMesh = mesh;
    }

    static void CreateRoads()
    {
        CreateRoad("Road_Main_Valley", MainRoad, 12f, new Color(0.25f, 0.20f, 0.13f));
        CreateRoad("Road_North_Service", new[]
        {
            new Vector2(-280f, 120f), new Vector2(-164f, 82f), new Vector2(-66f, 92f), new Vector2(40f, 118f), new Vector2(178f, 146f)
        }, 8f, new Color(0.22f, 0.20f, 0.16f));
    }

    static void CreateRoad(string name, Vector2[] points, float width, Color color)
    {
        GameObject road = new GameObject(name);
        MeshFilter filter = road.AddComponent<MeshFilter>();
        MeshRenderer renderer = road.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = MakeMaterial(name + "_Material", color, 0.28f);

        Vector3[] vertices = new Vector3[points.Length * 2];
        int[] triangles = new int[(points.Length - 1) * 6];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 dir = i == 0 ? points[i + 1] - points[i] : points[i] - points[i - 1];
            Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
            Vector2 a = points[i] + normal * width * 0.5f;
            Vector2 b = points[i] - normal * width * 0.5f;
            vertices[i * 2] = new Vector3(a.x, WorldHeight(a.x, a.y) + 0.22f, a.y);
            vertices[i * 2 + 1] = new Vector3(b.x, WorldHeight(b.x, b.y) + 0.22f, b.y);

            if (i >= points.Length - 1)
                continue;

            int vi = i * 2;
            int ti = i * 6;
            triangles[ti] = vi;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh { name = name + "_Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        filter.sharedMesh = mesh;
    }

    static void CreateBridge()
    {
        float z = 0f;
        float x = RiverCenterX(z);
        GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "Bridge_Concrete_RiverCrossing";
        bridge.transform.position = new Vector3(x, WaterLevel01 * TerrainHeight + 2.3f, z);
        bridge.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
        bridge.transform.localScale = new Vector3(48f, 3.2f, 16f);
        bridge.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Bridge_Concrete_Material", new Color(0.33f, 0.32f, 0.29f), 0.22f);
    }

    static void PlaceBuildings(Terrain terrain)
    {
        GameObject root = new GameObject("PUBGStyle_Villages_And_Compounds");
        for (int i = 0; i < HousePositions.Length; i++)
        {
            Vector3 pos = HousePositions[i];
            float yaw = i * 37f + UnityEngine.Random.Range(-12f, 12f);
            GameObject building = InstantiateBuilding(i, pos, yaw);
            building.transform.SetParent(root.transform, true);
        }

        PlaceIndustrialProps(root.transform);
    }

    static GameObject InstantiateBuilding(int index, Vector3 flatPos, float yaw)
    {
        string path = BuildingAssetPaths[index % BuildingAssetPaths.Length];
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Vector3 position = new Vector3(flatPos.x, WorldHeight(flatPos.x, flatPos.z), flatPos.z);

        if (asset != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance != null)
            {
                instance.name = "Compound_Building_" + index.ToString("00");
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                instance.transform.localScale = Vector3.one * PickBuildingScale(path);
                EnsureCollider(instance);
                return instance;
            }
        }

        return CreateFallbackBuilding("Compound_Building_" + index.ToString("00"), position, yaw);
    }

    static void PlaceIndustrialProps(Transform root)
    {
        string[] paths =
        {
            "Assets/External/Downloads/city-industrial/Models/FBX format/detail-tank.fbx",
            "Assets/External/Downloads/city-industrial/Models/FBX format/chimney-small.fbx",
            "Assets/External/Downloads/city-industrial/Models/FBX format/chimney-medium.fbx",
        };

        Vector3[] positions =
        {
            new Vector3(64f, 0f, -120f),
            new Vector3(94f, 0f, -138f),
            new Vector3(126f, 0f, -112f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i % paths.Length]);
            if (asset == null)
                continue;

            Vector3 p = positions[i];
            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
                continue;

            instance.name = "Industrial_Detail_" + i.ToString("00");
            instance.transform.position = new Vector3(p.x, WorldHeight(p.x, p.z), p.z);
            instance.transform.rotation = Quaternion.Euler(0f, 35f + i * 44f, 0f);
            instance.transform.localScale = Vector3.one * 2.4f;
            instance.transform.SetParent(root, true);
            EnsureCollider(instance);
        }
    }

    static void ScatterNature(Terrain terrain)
    {
        GameObject root = new GameObject("PUBGStyle_Forest_Rocks");
        Material trunk = MakeMaterial("Realistic_Trunk_Mat", new Color(0.18f, 0.10f, 0.045f), 0.42f);
        Material leaves = MakeMaterial("Realistic_Leaves_Mat", new Color(0.055f, 0.27f, 0.075f), 0.58f);
        Material rock = MakeMaterial("Realistic_Rock_Mat", new Color(0.30f, 0.31f, 0.29f), 0.36f);

        int trees = 0;
        int attempts = 0;
        while (trees < 460 && attempts < 9000)
        {
            attempts++;
            Vector2 p = new Vector2(UnityEngine.Random.Range(-330f, 330f), UnityEngine.Random.Range(-330f, 330f));
            float h = Height01(p.x, p.y);
            if (RiverDistance(p.x, p.y) < 36f || h < 0.05f || h > 0.46f || ApproxSlope(p.x, p.y) > 0.20f)
                continue;

            CreateTree("Forest_Tree_" + trees.ToString("000"), p, h > 0.20f, trunk, leaves).transform.SetParent(root.transform, true);
            trees++;
        }

        for (int i = 0; i < 140; i++)
        {
            float z = UnityEngine.Random.Range(-340f, 340f);
            float x = RiverCenterX(z) + (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(50f, 84f);
            CreateRock("River_Boulder_" + i.ToString("000"), new Vector2(x, z), UnityEngine.Random.Range(1.0f, 3.6f), rock).transform.SetParent(root.transform, true);
        }
    }

    static GameObject CreateTree(string name, Vector2 flatPos, bool pine, Material trunkMat, Material leafMat)
    {
        GameObject root = new GameObject(name);
        float y = WorldHeight(flatPos.x, flatPos.y);
        root.transform.position = new Vector3(flatPos.x, y, flatPos.y);
        float scale = UnityEngine.Random.Range(1.2f, 2.4f);

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 2.1f * scale, 0f);
        trunk.transform.localScale = new Vector3(0.45f * scale, 2.1f * scale, 0.45f * scale);
        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

        int canopyCount = pine ? 3 : 2;
        for (int i = 0; i < canopyCount; i++)
        {
            GameObject canopy = GameObject.CreatePrimitive(pine ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            canopy.name = "Canopy_" + i;
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localPosition = new Vector3(0f, (4.3f + i * 1.15f) * scale, 0f);
            float radius = (pine ? 1.65f - i * 0.25f : 1.8f - i * 0.22f) * scale;
            canopy.transform.localScale = new Vector3(radius, radius * (pine ? 1.25f : 0.78f), radius);
            canopy.GetComponent<Renderer>().sharedMaterial = leafMat;
            UnityEngine.Object.DestroyImmediate(canopy.GetComponent<Collider>());
        }

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.radius = 0.65f * scale;
        collider.height = 4.5f * scale;
        collider.center = new Vector3(0f, 2.25f * scale, 0f);
        return root;
    }

    static GameObject CreateRock(string name, Vector2 flatPos, float scale, Material material)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = name;
        rock.transform.position = new Vector3(flatPos.x, WorldHeight(flatPos.x, flatPos.y) + 0.7f * scale, flatPos.y);
        rock.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-8f, 8f));
        rock.transform.localScale = new Vector3(scale * 1.6f, scale * 0.7f, scale * 1.2f);
        rock.GetComponent<Renderer>().sharedMaterial = material;
        return rock;
    }

    static void CreateSpawnMarkers(Terrain terrain)
    {
        CreateMarker("Spawn_Player_BlueZone", new Vector2(-300f, -286f), new Color(0.05f, 0.45f, 1f));
        CreateMarker("Spawn_Enemy_RedZone", new Vector2(300f, 286f), new Color(1f, 0.16f, 0.06f));
    }

    static void CreateMarker(string name, Vector2 pos, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = name;
        marker.transform.position = new Vector3(pos.x, WorldHeight(pos.x, pos.y) + 0.35f, pos.y);
        marker.transform.localScale = new Vector3(8f, 0.18f, 8f);
        marker.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name + "_Mat", color, 0.18f);
    }

    static void SetupLightingAndCamera(Terrain terrain)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.62f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.39f, 0.32f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.13f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 260f;
        RenderSettings.fogEndDistance = 900f;
        RenderSettings.fogColor = new Color(0.50f, 0.58f, 0.61f);

        Light sun = UnityEngine.Object.FindObjectOfType<Light>();
        if (sun == null)
            sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.name = "Sun_LateAfternoon";
        sun.type = LightType.Directional;
        sun.intensity = 1.18f;
        sun.color = new Color(1f, 0.91f, 0.74f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;
        sun.transform.rotation = Quaternion.Euler(47f, -33f, 0f);

        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.gameObject.tag = "MainCamera";
        }
        camera.name = "Camera_PUBGStyle_MapPreview";
        camera.transform.position = new Vector3(-250f, 210f, -310f);
        camera.transform.rotation = Quaternion.Euler(58f, 38f, 0f);
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1400f;
        camera.backgroundColor = new Color(0.55f, 0.66f, 0.72f);
    }

    static GameObject CreateFallbackBuilding(string name, Vector3 position, float yaw)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 4f, 0f);
        body.transform.localScale = new Vector3(18f, 8f, 14f);
        body.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name + "_Wall", new Color(0.48f, 0.43f, 0.35f), 0.35f);

        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(root.transform, false);
        roof.transform.localPosition = new Vector3(0f, 8.8f, 0f);
        roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        roof.transform.localScale = new Vector3(16f, 2.0f, 16f);
        roof.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name + "_Roof", new Color(0.38f, 0.13f, 0.09f), 0.32f);
        return root;
    }

    static void EnsureCollider(GameObject instance)
    {
        if (instance.GetComponentInChildren<Collider>() != null)
            return;

        Bounds bounds = new Bounds(instance.transform.position, Vector3.one);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        BoxCollider collider = instance.AddComponent<BoxCollider>();
        collider.center = instance.transform.InverseTransformPoint(bounds.center);
        collider.size = instance.transform.InverseTransformVector(bounds.size);
    }

    static float PickBuildingScale(string path)
    {
        if (path.Contains("city-industrial"))
            return 4.8f;
        return 5.8f;
    }

    static Texture2D LoadTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            return null;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return texture;
    }

    static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, true);
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
            texture.SetPixel(x, y, color);
        texture.Apply(true, true);
        return texture;
    }

    static Material CreateWaterMaterial()
    {
        Material material = MakeMaterial("River_Water_Material", new Color(0.04f, 0.31f, 0.48f, 0.72f), 0.65f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.72f);
        material.SetFloat("_Mode", 3f);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;
        return material;
    }

    static Material MakeMaterial(string name, Color color, float smoothness)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", smoothness);
        return material;
    }

    static float IndexToWorld(int index, int resolution)
    {
        return index / (resolution - 1f) * TerrainSize - TerrainSize * 0.5f;
    }

    static float Height01(float x, float z)
    {
        float h = 0.055f;
        h += Mathf.PerlinNoise(x * 0.008f + 20.1f, z * 0.008f + 4.7f) * 0.060f;
        h += Mathf.PerlinNoise(x * 0.019f + 7.4f, z * 0.019f + 32.2f) * 0.030f;
        h += Gaussian(x, z, -260f, 190f, 150f, 120f, 0.30f);
        h += Gaussian(x, z, 230f, 160f, 135f, 160f, 0.25f);
        h += Gaussian(x, z, 240f, -230f, 160f, 130f, 0.31f);
        h += Gaussian(x, z, -245f, -170f, 180f, 150f, 0.19f);
        h += Gaussian(x, z, 20f, 285f, 245f, 92f, 0.14f);

        float river = RiverDistance(x, z);
        float valley = Mathf.Clamp01(1f - river / RiverBankWidth);
        h -= valley * valley * 0.17f;
        if (river < RiverHalfWidth)
            h = Mathf.Min(h, WaterLevel01 - 0.015f - (1f - river / RiverHalfWidth) * 0.018f);

        float road = DistanceToPolyline(new Vector2(x, z), MainRoad);
        if (road < 20f)
            h = Mathf.Lerp(h, SmoothRoadHeight(x, z), Mathf.InverseLerp(20f, 0f, road) * 0.78f);

        return Mathf.Clamp01(h);
    }

    static float SmoothRoadHeight(float x, float z)
    {
        float sum = 0f;
        int count = 0;
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            sum += RawHeight01(x + dx * 18f, z + dz * 18f);
            count++;
        }
        return sum / count;
    }

    static float RawHeight01(float x, float z)
    {
        float h = 0.055f;
        h += Mathf.PerlinNoise(x * 0.008f + 20.1f, z * 0.008f + 4.7f) * 0.060f;
        h += Mathf.PerlinNoise(x * 0.019f + 7.4f, z * 0.019f + 32.2f) * 0.030f;
        h += Gaussian(x, z, -260f, 190f, 150f, 120f, 0.30f);
        h += Gaussian(x, z, 230f, 160f, 135f, 160f, 0.25f);
        h += Gaussian(x, z, 240f, -230f, 160f, 130f, 0.31f);
        h += Gaussian(x, z, -245f, -170f, 180f, 150f, 0.19f);
        h += Gaussian(x, z, 20f, 285f, 245f, 92f, 0.14f);
        return h;
    }

    static float Gaussian(float x, float z, float cx, float cz, float sx, float sz, float height)
    {
        float dx = (x - cx) / sx;
        float dz = (z - cz) / sz;
        return height * Mathf.Exp(-(dx * dx + dz * dz));
    }

    static float WorldHeight(float x, float z)
    {
        return Height01(x, z) * TerrainHeight;
    }

    static float RiverCenterX(float z)
    {
        return -18f + Mathf.Sin(z * 0.020f) * 66f + Mathf.Sin(z * 0.047f + 1.25f) * 15f;
    }

    static float RiverDistance(float x, float z)
    {
        return Mathf.Abs(x - RiverCenterX(z));
    }

    static float ApproxSlope(float x, float z)
    {
        float step = 4f;
        float hx = Height01(x + step, z) - Height01(x - step, z);
        float hz = Height01(x, z + step) - Height01(x, z - step);
        return Mathf.Sqrt(hx * hx + hz * hz) / (step * 2f);
    }

    static float DistanceToPolyline(Vector2 point, Vector2[] line)
    {
        float best = float.MaxValue;
        for (int i = 0; i < line.Length - 1; i++)
            best = Mathf.Min(best, DistanceToSegment(point, line[i], line[i + 1]));
        return best;
    }

    static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
        return Vector2.Distance(point, a + ab * t);
    }
}
