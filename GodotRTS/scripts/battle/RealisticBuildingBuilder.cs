using Godot;
using System;

/// <summary>
/// 真实军工仿真 3D 建筑构建引擎 (AAA Realistic RTS Military Structure Builder)
/// 彻底废弃简陋方块与卡通玩具模型，采用具有硬核军事要塞与红警科技风格的高精层次化 3D 结构，
/// 结合 PBR 装甲钢板、法线贴图、工业花纹钢板、高反光偏光玻璃、动态排烟/蒸汽粒子与 360° 旋转雷达，
/// 打造极具视觉冲击力与现代重工业军事美学的基地建筑群。
/// </summary>
public static class RealisticBuildingBuilder
{
    const string MetalPlatesColorPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/MetalPlates015A/MetalPlates015A_1K-JPG_Color.jpg";
    const string MetalPlatesNormalPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/MetalPlates015A/MetalPlates015A_1K-JPG_NormalGL.jpg";
    const string MetalPlatesRoughPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/MetalPlates015A/MetalPlates015A_1K-JPG_Roughness.jpg";
    const string MetalPlatesMetalPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/MetalPlates015A/MetalPlates015A_1K-JPG_Metalness.jpg";

    const string DiamondColorPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/DiamondPlate008C/DiamondPlate008C_1K-JPG_Color.jpg";
    const string DiamondNormalPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/DiamondPlate008C/DiamondPlate008C_1K-JPG_NormalGL.jpg";
    const string DiamondRoughPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/DiamondPlate008C/DiamondPlate008C_1K-JPG_Roughness.jpg";
    const string DiamondMetalPath = "res://assets/unity_migrated/Assets/External/ambientCG/SciFiVehicleMaterials/DiamondPlate008C/DiamondPlate008C_1K-JPG_Metalness.jpg";

    static Texture2D? metalColorTex;
    static Texture2D? metalNormalTex;
    static Texture2D? metalRoughTex;
    static Texture2D? metalMetalTex;

    static Texture2D? diamondColorTex;
    static Texture2D? diamondNormalTex;
    static Texture2D? diamondRoughTex;
    static Texture2D? diamondMetalTex;

    static bool texturesLoaded;

    static void EnsureTextures()
    {
        if (texturesLoaded) return;
        texturesLoaded = true;

        if (ResourceLoader.Exists(MetalPlatesColorPath))
            metalColorTex = ResourceLoader.Load<Texture2D>(MetalPlatesColorPath);
        if (ResourceLoader.Exists(MetalPlatesNormalPath))
            metalNormalTex = ResourceLoader.Load<Texture2D>(MetalPlatesNormalPath);
        if (ResourceLoader.Exists(MetalPlatesRoughPath))
            metalRoughTex = ResourceLoader.Load<Texture2D>(MetalPlatesRoughPath);
        if (ResourceLoader.Exists(MetalPlatesMetalPath))
            metalMetalTex = ResourceLoader.Load<Texture2D>(MetalPlatesMetalPath);

        if (ResourceLoader.Exists(DiamondColorPath))
            diamondColorTex = ResourceLoader.Load<Texture2D>(DiamondColorPath);
        if (ResourceLoader.Exists(DiamondNormalPath))
            diamondNormalTex = ResourceLoader.Load<Texture2D>(DiamondNormalPath);
        if (ResourceLoader.Exists(DiamondRoughPath))
            diamondRoughTex = ResourceLoader.Load<Texture2D>(DiamondRoughPath);
        if (ResourceLoader.Exists(DiamondMetalPath))
            diamondMetalTex = ResourceLoader.Load<Texture2D>(DiamondMetalPath);
    }

    public static StandardMaterial3D CreateArmoredSteelMaterial(Color factionTint, float uvScale = 2.0f)
    {
        EnsureTextures();
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(
                Mathf.Clamp(Mathf.Lerp(0.62f, factionTint.R * 1.4f, 0.50f), 0.45f, 0.95f),
                Mathf.Clamp(Mathf.Lerp(0.65f, factionTint.G * 1.4f, 0.50f), 0.45f, 0.95f),
                Mathf.Clamp(Mathf.Lerp(0.70f, factionTint.B * 1.4f, 0.50f), 0.45f, 0.95f),
                1.0f
            ),
            Metallic = 0.85f,
            Roughness = 0.32f,
            Uv1Scale = new Vector3(uvScale, uvScale, uvScale)
        };
        if (metalColorTex != null) mat.AlbedoTexture = metalColorTex;
        if (metalNormalTex != null)
        {
            mat.NormalEnabled = true;
            mat.NormalTexture = metalNormalTex;
            mat.NormalScale = 1.0f;
        }
        if (metalRoughTex != null) mat.RoughnessTexture = metalRoughTex;
        if (metalMetalTex != null) mat.MetallicTexture = metalMetalTex;
        return mat;
    }

    public static StandardMaterial3D CreateDiamondPlatingMaterial(float uvScale = 3.0f)
    {
        EnsureTextures();
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.52f, 0.56f, 0.60f),
            Metallic = 0.88f,
            Roughness = 0.28f,
            Uv1Scale = new Vector3(uvScale, uvScale, uvScale)
        };
        if (diamondColorTex != null) mat.AlbedoTexture = diamondColorTex;
        if (diamondNormalTex != null)
        {
            mat.NormalEnabled = true;
            mat.NormalTexture = diamondNormalTex;
            mat.NormalScale = 1.25f;
        }
        if (diamondRoughTex != null) mat.RoughnessTexture = diamondRoughTex;
        if (diamondMetalTex != null) mat.MetallicTexture = diamondMetalTex;
        return mat;
    }

    public static StandardMaterial3D CreateConcreteFoundationMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.38f, 0.40f, 0.42f),
            Roughness = 0.92f,
            Metallic = 0.08f
        };
    }

    public static StandardMaterial3D CreateGunmetalMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.14f, 0.15f, 0.17f),
            Metallic = 0.92f,
            Roughness = 0.22f
        };
    }

    public static StandardMaterial3D CreateHazardStripeMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.96f, 0.78f, 0.12f),
            Metallic = 0.35f,
            Roughness = 0.38f
        };
    }

    public static StandardMaterial3D CreateEnergyGlowMaterial(Color glowColor, float energy = 3.0f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = energy,
            Roughness = 0.15f
        };
    }

    public static StandardMaterial3D CreateGlassMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.38f, 0.48f, 0.82f),
            Metallic = 0.95f,
            Roughness = 0.06f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }

    public static void BuildRealisticBuilding(Node3D root, string buildKey, bool playerOwned, Color tint)
    {
        switch (buildKey)
        {
            case "barracks":
                BuildBarracks(root, playerOwned, tint);
                break;
            case "tank_factory":
                BuildTankFactory(root, playerOwned, tint);
                break;
            case "armor_factory":
                BuildArmorFactory(root, playerOwned, tint);
                break;
            case "airfield":
                BuildAirfield(root, playerOwned, tint);
                break;
            case "air_factory":
                BuildAirFactory(root, playerOwned, tint);
                break;
            case "naval_yard":
                BuildNavalYard(root, playerOwned, tint);
                break;
            case "turret":
                BuildTurret(root, playerOwned, tint);
                break;
            case "power_plant":
                BuildPowerPlant(root, playerOwned, tint);
                break;
            case "gold_mine":
                BuildGoldMine(root, playerOwned, tint);
                break;
            default:
                BuildMainBase(root, playerOwned, tint);
                break;
        }
    }

    // ==========================================
    // 1. 主基地指挥要塞 (Main Base Command Citadel)
    // ==========================================
    static void BuildMainBase(Node3D root, bool playerOwned, Color tint)
    {
        var armorMat = CreateArmoredSteelMaterial(tint, 2.5f);
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var concreteMat = CreateConcreteFoundationMaterial();
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();
        var glassMat = CreateGlassMaterial();
        var glowMat = CreateEnergyGlowMaterial(playerOwned ? new Color(0.2f, 0.85f, 1.0f) : new Color(1.0f, 0.28f, 0.22f), 3.2f);
        var redBeacon = CreateEnergyGlowMaterial(new Color(1.0f, 0.15f, 0.12f), 4.5f);

        // 1. 复合双层抗震防爆混凝土底台 (Tiered Bastion Plinth)
        AddBox(root, "BasePlinthLower", new Vector3(8.8f, 0.45f, 8.8f), new Vector3(0f, 0.22f, 0f), concreteMat);
        AddBox(root, "BasePlinthUpper", new Vector3(7.8f, 0.35f, 7.8f), new Vector3(0f, 0.62f, 0f), concreteMat);

        // 2. 4 角斜面防爆倒角护壁 (4-Corner Chamfered Blast Berms)
        float[] cPos = { -3.8f, 3.8f };
        for (int i = 0; i < 4; i++)
        {
            float px = cPos[i % 2];
            float pz = cPos[i / 2];
            AddPrism(root, $"BermSlope_{i}", new Vector3(1.6f, 0.7f, 1.6f), new Vector3(px, 0.55f, pz), concreteMat, new Vector3(0f, Mathf.Pi * 0.25f * (i * 2 + 1), 0f));
        }

        // 3. 前方重型花纹钢板出入引桥 (Diamond-plate Rollout Ramp)
        AddBox(root, "ApproachRamp", new Vector3(3.8f, 0.22f, 2.2f), new Vector3(0f, 0.38f, -4.6f), floorMat);
        AddBox(root, "RampHazardL", new Vector3(0.25f, 0.25f, 2.2f), new Vector3(-1.9f, 0.40f, -4.6f), hazardMat);
        AddBox(root, "RampHazardR", new Vector3(0.25f, 0.25f, 2.2f), new Vector3(1.9f, 0.40f, -4.6f), hazardMat);

        // 4. 下层斜面防弹装甲主堡核心 (Lower Sloped Armor Citadel)
        AddBox(root, "CitadelLower", new Vector3(6.6f, 2.2f, 6.6f), new Vector3(0f, 1.88f, 0f), armorMat);
        
        // 5. 正面深凹式双扇液压防核爆装甲闸门 (Recessed Hydraulic Blast Vault)
        AddBox(root, "VaultFrame", new Vector3(3.4f, 2.1f, 0.5f), new Vector3(0f, 1.82f, -3.32f), gunmetalMat);
        AddBox(root, "VaultDoorLeft", new Vector3(1.4f, 1.8f, 0.35f), new Vector3(-0.75f, 1.70f, -3.28f), floorMat);
        AddBox(root, "VaultDoorRight", new Vector3(1.4f, 1.8f, 0.35f), new Vector3(0.75f, 1.70f, -3.28f), floorMat);
        AddBox(root, "VaultHazardTop", new Vector3(3.4f, 0.25f, 0.55f), new Vector3(0f, 2.85f, -3.32f), hazardMat);

        // 6. 中层战术参谋指挥甲板 (Tactical Operations Tier)
        AddBox(root, "CitadelUpper", new Vector3(4.8f, 1.6f, 4.8f), new Vector3(0f, 3.75f, 0f), armorMat);
        
        // 四面环带式防弹偏光玻璃狭缝舷窗 (Observation Glass Slits)
        AddBox(root, "ObsGlassFront", new Vector3(4.2f, 0.45f, 0.15f), new Vector3(0f, 4.15f, -2.42f), glassMat);
        AddBox(root, "ObsGlassBack", new Vector3(4.2f, 0.45f, 0.15f), new Vector3(0f, 4.15f, 2.42f), glassMat);
        AddBox(root, "ObsGlassLeft", new Vector3(0.15f, 0.45f, 4.2f), new Vector3(-2.42f, 4.15f, 0f), glassMat);
        AddBox(root, "ObsGlassRight", new Vector3(0.15f, 0.45f, 4.2f), new Vector3(2.42f, 4.15f, 0f), glassMat);

        // 7. 顶部战术指挥上盖与停机坪 (Roof Deck & Helipad)
        AddBox(root, "HelipadDeck", new Vector3(5.2f, 0.35f, 5.2f), new Vector3(0f, 4.70f, 0f), floorMat);
        AddBox(root, "HelipadMarkH", new Vector3(1.6f, 0.38f, 0.35f), new Vector3(0f, 4.72f, 0f), hazardMat);
        AddBox(root, "HelipadMarkBarL", new Vector3(0.35f, 0.38f, 1.4f), new Vector3(-0.7f, 4.72f, 0f), hazardMat);
        AddBox(root, "HelipadMarkBarR", new Vector3(0.35f, 0.38f, 1.4f), new Vector3(0.7f, 4.72f, 0f), hazardMat);

        // 8. 四角防御哨戒机枪掩体柱 (4 Corner Security Posts)
        foreach (var ox in cPos)
        {
            foreach (var oz in cPos)
            {
                AddCylinder(root, $"SentryPost_{ox}_{oz}", 0.65f, 0.75f, 2.2f, new Vector3(ox * 0.85f, 1.70f, oz * 0.85f), gunmetalMat);
                AddBox(root, $"SentryArmor_{ox}_{oz}", new Vector3(0.9f, 0.6f, 0.9f), new Vector3(ox * 0.85f, 2.65f, oz * 0.85f), armorMat);
                // 枪管
                AddCylinder(root, $"SentryGunA_{ox}_{oz}", 0.06f, 0.06f, 0.8f, new Vector3(ox * 0.85f - 0.15f, 2.65f, oz * 0.85f + (oz > 0 ? 0.6f : -0.6f)), gunmetalMat, new Vector3(Mathf.Pi * 0.5f, 0f, 0f));
                AddCylinder(root, $"SentryGunB_{ox}_{oz}", 0.06f, 0.06f, 0.8f, new Vector3(ox * 0.85f + 0.15f, 2.65f, oz * 0.85f + (oz > 0 ? 0.6f : -0.6f)), gunmetalMat, new Vector3(Mathf.Pi * 0.5f, 0f, 0f));
            }
        }

        // 9. 顶部中央通讯塔与 360° 动态旋转相控阵雷达 (Active Rotating Phased Array Radar)
        var radarNode = new Node3D { Name = "RadarRotator", Position = new Vector3(0f, 4.9f, 0f) };
        root.AddChild(radarNode);
        radarNode.AddChild(new BuildingRadarRotator { RotateSpeed = 1.8f });

        AddCylinder(radarNode, "RadarPylon", 0.22f, 0.32f, 1.8f, new Vector3(0f, 0.9f, 0f), gunmetalMat);
        AddBox(radarNode, "RadarDishBack", new Vector3(3.2f, 1.0f, 0.25f), new Vector3(0f, 1.8f, 0f), armorMat);
        AddBox(radarNode, "RadarSensorBar", new Vector3(2.8f, 0.3f, 0.18f), new Vector3(0f, 1.8f, -0.22f), glowMat);
        AddCylinder(radarNode, "RadarFeedHorn", 0.08f, 0.12f, 0.8f, new Vector3(0f, 1.8f, -0.65f), gunmetalMat, new Vector3(Mathf.Pi * 0.5f, 0f, 0f));

        // 10. 卫星通信球罩与战术天线 (Satellite Radomes & Whip Antennas)
        AddSphere(root, "CommsDomeMain", 0.85f, new Vector3(-1.7f, 5.35f, 1.4f), gunmetalMat);
        AddSphere(root, "CommsDomeSec", 0.60f, new Vector3(1.7f, 5.25f, 1.4f), gunmetalMat);
        AddCylinder(root, "WhipAntennaA", 0.04f, 0.08f, 2.6f, new Vector3(-2.1f, 6.0f, -1.8f), gunmetalMat);
        AddCylinder(root, "WhipAntennaB", 0.04f, 0.08f, 2.6f, new Vector3(2.1f, 6.0f, -1.8f), gunmetalMat);

        // 11. 避雷针顶部红色夜航警示灯 (Aviation Hazard Beacon)
        AddSphere(root, "TopBeacon", 0.15f, new Vector3(0f, 7.0f, 0f), redBeacon);
    }

    // ==========================================
    // 2. 步兵野战兵营 (Barracks Forward Garrison)
    // ==========================================
    static void BuildBarracks(Node3D root, bool playerOwned, Color tint)
    {
        var armorMat = CreateArmoredSteelMaterial(tint, 2.0f);
        var floorMat = CreateDiamondPlatingMaterial(2.5f);
        var concreteMat = CreateConcreteFoundationMaterial();
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();
        var sandbagMat = new StandardMaterial3D { AlbedoColor = new Color(0.48f, 0.42f, 0.28f), Roughness = 0.95f };

        // 1. 加固抗震地台 (Fortified Concrete Pad)
        AddBox(root, "Plinth", new Vector3(6.8f, 0.35f, 7.6f), new Vector3(0f, 0.17f, 0f), concreteMat);

        // 2. 双跨并列半拱双坡斜顶营房主体 (Twin Hardened Quonset Garrison Barracks)
        AddBox(root, "BarracksHallL", new Vector3(2.7f, 2.4f, 5.8f), new Vector3(-1.45f, 1.50f, 0.4f), armorMat);
        AddBox(root, "BarracksHallR", new Vector3(2.7f, 2.4f, 5.8f), new Vector3(1.45f, 1.50f, 0.4f), armorMat);
        
        // 营房双斜坡防爆顶棚 (Gabled Blast Roofs)
        AddPrism(root, "RoofPeakL", new Vector3(2.9f, 0.65f, 6.0f), new Vector3(-1.45f, 3.0f, 0.4f), floorMat);
        AddPrism(root, "RoofPeakR", new Vector3(2.9f, 0.65f, 6.0f), new Vector3(1.45f, 3.0f, 0.4f), floorMat);

        // 3. 前方防爆进出气闸门斗 (Airlock Security Port)
        AddBox(root, "AirlockPort", new Vector3(2.8f, 1.9f, 1.6f), new Vector3(0f, 1.15f, -2.7f), armorMat);
        AddBox(root, "AirlockDoorL", new Vector3(0.9f, 1.6f, 0.25f), new Vector3(-0.55f, 1.05f, -3.45f), floorMat);
        AddBox(root, "AirlockDoorR", new Vector3(0.9f, 1.6f, 0.25f), new Vector3(0.55f, 1.05f, -3.45f), floorMat);
        AddBox(root, "DoorHazard", new Vector3(2.4f, 0.22f, 0.35f), new Vector3(0f, 2.0f, -3.45f), hazardMat);
        AddBox(root, "EntryRamp", new Vector3(2.8f, 0.15f, 1.4f), new Vector3(0f, 0.22f, -4.1f), floorMat);

        // 4. 周边战术沙包掩体防御工事 (Sandbag Blast Berms)
        AddBox(root, "SandbagFrontL", new Vector3(1.8f, 0.55f, 0.55f), new Vector3(-2.2f, 0.45f, -3.4f), sandbagMat);
        AddBox(root, "SandbagFrontR", new Vector3(1.8f, 0.55f, 0.55f), new Vector3(2.2f, 0.45f, -3.4f), sandbagMat);

        // 5. 高架哨戒岗亭塔 (Sentry Watchtower)
        AddCylinder(root, "TowerLegA", 0.08f, 0.08f, 3.6f, new Vector3(2.8f, 1.95f, -1.8f), gunmetalMat);
        AddCylinder(root, "TowerLegB", 0.08f, 0.08f, 3.6f, new Vector3(2.8f, 1.95f, -0.6f), gunmetalMat);
        AddBox(root, "WatchCab", new Vector3(1.4f, 1.2f, 1.4f), new Vector3(2.8f, 3.8f, -1.2f), armorMat);
        AddSphere(root, "Searchlight", 0.25f, new Vector3(2.8f, 4.5f, -1.2f), CreateEnergyGlowMaterial(new Color(1f, 0.95f, 0.7f), 3.5f));

        // 6. 营区通风机组与通信桅杆
        AddBox(root, "HvacUnitA", new Vector3(1.0f, 0.6f, 0.8f), new Vector3(-1.45f, 3.4f, 1.8f), gunmetalMat);
        AddBox(root, "HvacUnitB", new Vector3(1.0f, 0.6f, 0.8f), new Vector3(1.45f, 3.4f, 1.8f), gunmetalMat);
        AddCylinder(root, "RadioMast", 0.06f, 0.12f, 4.2f, new Vector3(-2.6f, 3.6f, 2.6f), gunmetalMat);
    }

    // ==========================================
    // 3. 特勤工厂车间 (Tank Factory / Heavy Works)
    // ==========================================
    static void BuildTankFactory(Node3D root, bool playerOwned, Color tint)
    {
        var armorMat = CreateArmoredSteelMaterial(tint, 2.5f);
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var concreteMat = CreateConcreteFoundationMaterial();
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();

        // 1. 高抗压重工业地坪 (Industrial Slab)
        AddBox(root, "Plinth", new Vector3(8.8f, 0.45f, 9.6f), new Vector3(0f, 0.22f, 0f), concreteMat);

        // 2. 重装甲总装流水线厂房 (Main Assembly Hall with Sawtooth Roof)
        AddBox(root, "AssemblyHall", new Vector3(7.2f, 3.6f, 7.6f), new Vector3(0f, 2.2f, 0.6f), armorMat);
        AddPrism(root, "SawtoothRoofA", new Vector3(7.4f, 0.8f, 3.6f), new Vector3(0f, 4.35f, -1.1f), floorMat);
        AddPrism(root, "SawtoothRoofB", new Vector3(7.4f, 0.8f, 3.6f), new Vector3(0f, 4.35f, 2.3f), floorMat);

        // 3. 巨型装甲出厂卷帘闸门 (Rollout Blast Door)
        AddBox(root, "DoorFrame", new Vector3(4.4f, 3.0f, 0.45f), new Vector3(0f, 1.8f, -3.2f), gunmetalMat);
        AddBox(root, "RolloutDoor", new Vector3(3.8f, 2.6f, 0.35f), new Vector3(0f, 1.6f, -3.15f), floorMat);
        AddBox(root, "HazardLintel", new Vector3(4.4f, 0.35f, 0.5f), new Vector3(0f, 3.35f, -3.2f), hazardMat);

        // 4. 重型花纹钢板测试引坡 (Extended Rollout Traction Ramp)
        AddBox(root, "TractionRamp", new Vector3(4.2f, 0.22f, 3.2f), new Vector3(0f, 0.32f, -4.8f), floorMat);
        AddBox(root, "RampStripeL", new Vector3(0.25f, 0.24f, 3.2f), new Vector3(-2.0f, 0.34f, -4.8f), hazardMat);
        AddBox(root, "RampStripeR", new Vector3(0.25f, 0.24f, 3.2f), new Vector3(2.0f, 0.34f, -4.8f), hazardMat);

        // 5. 双联重工业排气烟囱与动态排烟粒子 (Twin Chimneys with Dynamic Particle Smoke)
        AddCylinder(root, "ChimneyA", 0.45f, 0.65f, 4.6f, new Vector3(2.7f, 4.5f, 2.6f), gunmetalMat);
        AddCylinder(root, "ChimneyB", 0.45f, 0.65f, 4.6f, new Vector3(2.7f, 4.5f, 1.0f), gunmetalMat);
        AddSmokeParticles(root, "ChimneySmokeA", new Vector3(2.7f, 6.8f, 2.6f), new Color(0.22f, 0.22f, 0.24f, 0.45f), 3.0f);
        AddSmokeParticles(root, "ChimneySmokeB", new Vector3(2.7f, 6.8f, 1.0f), new Color(0.22f, 0.22f, 0.24f, 0.45f), 3.0f);

        // 6. 侧翼悬臂重型龙门起重机 (Heavy Cantilever Gantry Crane)
        AddBox(root, "GantryBeam", new Vector3(0.45f, 0.45f, 7.8f), new Vector3(-3.4f, 3.8f, 0.6f), gunmetalMat);
        AddCylinder(root, "GantryLegA", 0.16f, 0.16f, 3.6f, new Vector3(-3.4f, 2.0f, -2.4f), gunmetalMat);
        AddCylinder(root, "GantryLegB", 0.16f, 0.16f, 3.6f, new Vector3(-3.4f, 2.0f, 3.6f), gunmetalMat);
        // 吊具
        AddBox(root, "CraneTrolley", new Vector3(0.8f, 0.3f, 0.8f), new Vector3(-3.4f, 3.5f, -0.6f), hazardMat);
        AddCylinder(root, "CraneCable", 0.03f, 0.03f, 1.8f, new Vector3(-3.4f, 2.5f, -0.6f), gunmetalMat);
        AddBox(root, "MagnetHook", new Vector3(0.7f, 0.25f, 0.7f), new Vector3(-3.4f, 1.5f, -0.6f), gunmetalMat);

        // 7. 外部高压液力储罐与输送管道 (Storage Cylinder & Pipes)
        AddCylinder(root, "PressureTank", 0.75f, 0.75f, 2.4f, new Vector3(-2.8f, 1.6f, 3.2f), gunmetalMat);
        AddCylinder(root, "ManifoldPipe", 0.12f, 0.12f, 2.2f, new Vector3(-2.8f, 1.6f, 1.6f), gunmetalMat, new Vector3(Mathf.Pi * 0.5f, 0f, 0f));
    }

    // ==========================================
    // 4. 坦克工厂重工流水线 (Armor Factory Heavy Foundry)
    // ==========================================
    static void BuildArmorFactory(Node3D root, bool playerOwned, Color tint)
    {
        var armorMat = CreateArmoredSteelMaterial(tint, 2.5f);
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var concreteMat = CreateConcreteFoundationMaterial();
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();

        // 1. 重工业基台
        AddBox(root, "Plinth", new Vector3(9.2f, 0.45f, 9.2f), new Vector3(0f, 0.22f, 0f), concreteMat);

        // 2. 双联跨装甲锻压冲压总装流水线车间 (Twin-Bay Foundry)
        AddBox(root, "FoundryBayL", new Vector3(3.6f, 3.8f, 7.6f), new Vector3(-2.1f, 2.3f, 0.4f), armorMat);
        AddBox(root, "FoundryBayR", new Vector3(3.6f, 3.8f, 7.6f), new Vector3(2.1f, 2.3f, 0.4f), armorMat);
        AddPrism(root, "FoundryRoofL", new Vector3(3.8f, 0.7f, 7.8f), new Vector3(-2.1f, 4.45f, 0.4f), floorMat);
        AddPrism(root, "FoundryRoofR", new Vector3(3.8f, 0.7f, 7.8f), new Vector3(2.1f, 4.45f, 0.4f), floorMat);

        // 3. 中央高架行车过桥天街 (Connecting Gantry Skybridge)
        AddBox(root, "Skybridge", new Vector3(2.2f, 0.8f, 2.6f), new Vector3(0f, 3.6f, 0.4f), gunmetalMat);

        // 4. 双出车防弹闸门与引坡 (Dual Rollout Ports)
        float[] bX = { -2.1f, 2.1f };
        foreach (var bx in bX)
        {
            AddBox(root, $"Gate_{bx}", new Vector3(2.6f, 2.4f, 0.35f), new Vector3(bx, 1.6f, -3.45f), floorMat);
            AddBox(root, $"GateHazard_{bx}", new Vector3(2.8f, 0.3f, 0.4f), new Vector3(bx, 2.95f, -3.45f), hazardMat);
            AddBox(root, $"Ramp_{bx}", new Vector3(2.8f, 0.2f, 2.4f), new Vector3(bx, 0.32f, -4.6f), floorMat);
        }

        // 5. 3 联高压球形与圆柱形液力储罐群 (Trio of Storage Vessels)
        AddCylinder(root, "VesselA", 0.85f, 0.85f, 2.6f, new Vector3(-3.6f, 1.7f, 2.8f), gunmetalMat);
        AddCylinder(root, "VesselB", 0.70f, 0.70f, 2.2f, new Vector3(-3.6f, 1.5f, 1.0f), gunmetalMat);
        AddSphere(root, "SphereTank", 1.1f, new Vector3(3.6f, 2.1f, 2.8f), gunmetalMat);

        // 6. 工业重型换热通风百叶组 (Heavy HVAC Banks)
        AddBox(root, "HvacBankL", new Vector3(0.5f, 1.2f, 2.4f), new Vector3(-3.9f, 2.8f, -1.2f), gunmetalMat);
        AddBox(root, "HvacBankR", new Vector3(0.5f, 1.2f, 2.4f), new Vector3(3.9f, 2.8f, -1.2f), gunmetalMat);
    }

    // ==========================================
    // 5. 飞机制造机库 (Air Factory Aircraft Hangar)
    // ==========================================
    static void BuildAirFactory(Node3D root, bool playerOwned, Color tint)
    {
        var armorMat = CreateArmoredSteelMaterial(tint, 2.6f);
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var concreteMat = CreateConcreteFoundationMaterial();
        var gunmetalMat = CreateGunmetalMaterial();
        var glassMat = CreateGlassMaterial();
        var hazardMat = CreateHazardStripeMaterial();

        // 1. 宽跨度重载地基
        AddBox(root, "Plinth", new Vector3(9.2f, 0.4f, 9.2f), new Vector3(0f, 0.2f, 0f), concreteMat);

        // 2. 超宽跨度拱顶航空制造大机库 (Vaulted Aircraft Assembly Hangar)
        AddBox(root, "HangarMain", new Vector3(7.8f, 3.8f, 7.2f), new Vector3(0f, 2.3f, 0.5f), armorMat);
        AddPrism(root, "HangarArchRoof", new Vector3(8.0f, 0.9f, 7.4f), new Vector3(0f, 4.55f, 0.5f), floorMat);

        // 3. 四折式巨型航空推拉大门 (4-Section Folding Hangar Doors)
        AddBox(root, "Door1", new Vector3(1.6f, 3.2f, 0.35f), new Vector3(-2.55f, 1.95f, -3.15f), floorMat);
        AddBox(root, "Door2", new Vector3(1.6f, 3.2f, 0.35f), new Vector3(-0.85f, 1.95f, -3.15f), floorMat);
        AddBox(root, "Door3", new Vector3(1.6f, 3.2f, 0.35f), new Vector3(0.85f, 1.95f, -3.15f), floorMat);
        AddBox(root, "Door4", new Vector3(1.6f, 3.2f, 0.35f), new Vector3(2.55f, 1.95f, -3.15f), floorMat);
        AddBox(root, "HangarLintelHazard", new Vector3(7.2f, 0.35f, 0.45f), new Vector3(0f, 3.65f, -3.15f), hazardMat);

        // 4. 侧翼 3 层航电控制塔楼 (Avionics Engineering Wing)
        AddBox(root, "ControlTower", new Vector3(2.6f, 4.4f, 2.6f), new Vector3(3.2f, 2.6f, 2.6f), armorMat);
        AddBox(root, "GlassObservatory", new Vector3(2.8f, 0.85f, 2.8f), new Vector3(3.2f, 4.7f, 2.6f), glassMat);
        AddCylinder(root, "TowerMast", 0.08f, 0.12f, 2.2f, new Vector3(3.2f, 6.0f, 2.6f), gunmetalMat);

        // 5. 机库停机坪滑行引导黄色中线 (Taxiway Guide Markings)
        AddBox(root, "TaxiwayStripe", new Vector3(0.25f, 0.02f, 3.0f), new Vector3(0f, 0.42f, -4.5f), hazardMat);
    }

    // ==========================================
    // 6. 战术军用停机坪 (Airfield Runway Complex)
    // ==========================================
    static void BuildAirfield(Node3D root, bool playerOwned, Color tint)
    {
        var floorMat = CreateDiamondPlatingMaterial(3.5f);
        var concreteMat = CreateConcreteFoundationMaterial();
        var gunmetalMat = CreateGunmetalMaterial();
        var armorMat = CreateArmoredSteelMaterial(tint, 2.0f);
        var glassMat = CreateGlassMaterial();
        var markerMat = new StandardMaterial3D { AlbedoColor = new Color(0.96f, 0.96f, 0.94f), Roughness = 0.6f };
        var edgeGreen = CreateEnergyGlowMaterial(new Color(0.15f, 0.98f, 0.25f), 3.5f);
        var edgeAmber = CreateEnergyGlowMaterial(new Color(0.98f, 0.75f, 0.12f), 3.5f);

        // 1. 宽幅重型战术沥青混凝土跑道 (Heavy Tactical Runway Pad)
        AddBox(root, "RunwayPad", new Vector3(10.2f, 0.22f, 12.0f), new Vector3(0f, 0.11f, 0f), concreteMat);

        // 2. 跑道两端落地斑马阈值线 (Threshold Markings)
        AddBox(root, "ThresholdStart", new Vector3(4.8f, 0.24f, 0.45f), new Vector3(0f, 0.13f, -5.2f), markerMat);
        AddBox(root, "ThresholdEnd", new Vector3(4.8f, 0.24f, 0.45f), new Vector3(0f, 0.13f, 5.2f), markerMat);

        // 3. 跑道白色中心虚线 (Dashed Centerline)
        float[] cZ = { -3.6f, -1.2f, 1.2f, 3.6f };
        foreach (var cz in cZ)
        {
            AddBox(root, $"Centerline_{cz}", new Vector3(0.35f, 0.24f, 1.6f), new Vector3(0f, 0.13f, cz), markerMat);
        }

        // 4. 跑道边缘序列式夜航指示灯 (Runway Edge Lights)
        float[] lightZ = { -5.0f, -2.5f, 0f, 2.5f, 5.0f };
        foreach (var lz in lightZ)
        {
            AddCylinder(root, $"RunwayLightL_{lz}", 0.08f, 0.08f, 0.25f, new Vector3(-3.2f, 0.24f, lz), lz > 3f ? edgeAmber : edgeGreen);
            AddCylinder(root, $"RunwayLightR_{lz}", 0.08f, 0.08f, 0.25f, new Vector3(3.2f, 0.24f, lz), lz > 3f ? edgeAmber : edgeGreen);
        }

        // 5. 侧翼半地下加固机堡掩体 (Hardened Aircraft Shelter - HAS)
        AddBox(root, "HangarShelter", new Vector3(3.6f, 2.6f, 5.4f), new Vector3(3.2f, 1.45f, 0f), armorMat);
        AddBox(root, "ShelterFloor", new Vector3(3.4f, 0.1f, 5.2f), new Vector3(3.2f, 0.24f, 0f), floorMat);
        AddPrism(root, "ShelterRoof", new Vector3(3.8f, 0.6f, 5.6f), new Vector3(3.2f, 2.95f, 0f), floorMat);

        // 6. 空管指挥塔台与 360° 旋转雷达 (ATC Tower & Rotating Airport Radar)
        AddBox(root, "TowerShaft", new Vector3(1.8f, 4.2f, 1.8f), new Vector3(-3.6f, 2.2f, 3.8f), armorMat);
        AddBox(root, "ControlCabGlass", new Vector3(2.4f, 1.1f, 2.4f), new Vector3(-3.6f, 4.7f, 3.8f), glassMat);
        AddBox(root, "TowerRoof", new Vector3(2.6f, 0.25f, 2.6f), new Vector3(-3.6f, 5.35f, 3.8f), gunmetalMat);

        var radarNode = new Node3D { Name = "AirportRadarRotator", Position = new Vector3(-3.6f, 5.5f, 3.8f) };
        root.AddChild(radarNode);
        radarNode.AddChild(new BuildingRadarRotator { RotateSpeed = 2.4f });
        AddCylinder(radarNode, "RadarMast", 0.06f, 0.10f, 0.9f, new Vector3(0f, 0.45f, 0f), gunmetalMat);
        AddBox(radarNode, "RadarDish", new Vector3(1.8f, 0.6f, 0.15f), new Vector3(0f, 0.9f, 0f), armorMat);
    }

    // ==========================================
    // 7. 海军造船干船坞 (Naval Yard Drydock Complex)
    // ==========================================
    static void BuildNavalYard(Node3D root, bool playerOwned, Color tint)
    {
        var concreteMat = CreateConcreteFoundationMaterial();
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();
        var armorMat = CreateArmoredSteelMaterial(tint, 2.0f);
        var waterMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.04f, 0.22f, 0.35f),
            Roughness = 0.10f,
            Metallic = 0.90f
        };

        // 1. 左右重型干船坞混凝土栈桥防波堤 (Port & Starboard Caisson Piers)
        AddBox(root, "PierLeft", new Vector3(2.6f, 1.6f, 9.2f), new Vector3(-3.4f, 0.8f, 0f), concreteMat);
        AddBox(root, "PierRight", new Vector3(2.6f, 1.6f, 9.2f), new Vector3(3.4f, 0.8f, 0f), concreteMat);
        AddBox(root, "DeckLeft", new Vector3(2.4f, 0.12f, 9.0f), new Vector3(-3.4f, 1.65f, 0f), floorMat);
        AddBox(root, "DeckRight", new Vector3(2.4f, 0.12f, 9.0f), new Vector3(3.4f, 1.65f, 0f), floorMat);

        // 2. 栈桥边缘警示条纹与防撞护圈
        AddBox(root, "CurbHazardL", new Vector3(0.25f, 0.25f, 9.0f), new Vector3(-2.1f, 1.75f, 0f), hazardMat);
        AddBox(root, "CurbHazardR", new Vector3(0.25f, 0.25f, 9.0f), new Vector3(2.1f, 1.75f, 0f), hazardMat);

        // 3. 干船坞注水盆地 (Drydock Deep Basin)
        AddBox(root, "BasinWater", new Vector3(4.2f, 0.2f, 8.4f), new Vector3(0f, 0.35f, -0.4f), waterMat);

        // 4. 后方加固防水船台大闸与舾装车间 (Rear Gate & Workshop)
        AddBox(root, "RearFloodGate", new Vector3(9.4f, 1.6f, 2.2f), new Vector3(0f, 0.8f, 3.6f), concreteMat);
        AddBox(root, "FittingWorkshop", new Vector3(5.2f, 2.8f, 2.4f), new Vector3(0f, 2.7f, 3.6f), armorMat);
        AddPrism(root, "WorkshopRoof", new Vector3(5.4f, 0.7f, 2.6f), new Vector3(0f, 4.3f, 3.6f), floorMat);

        // 5. 左栈桥门座式高耸造船轨道龙门起重机 (Portal Shipyard Gantry Crane)
        var craneBase = new Vector3(-3.4f, 1.7f, -1.8f);
        AddBox(root, "CraneLegs", new Vector3(1.8f, 3.2f, 1.8f), craneBase + new Vector3(0f, 1.6f, 0f), gunmetalMat);
        AddBox(root, "CraneJib", new Vector3(1.2f, 0.7f, 5.4f), craneBase + new Vector3(0.9f, 3.5f, -0.8f), hazardMat);
        AddCylinder(root, "HoistCable", 0.04f, 0.04f, 2.6f, craneBase + new Vector3(1.4f, 2.0f, -2.6f), gunmetalMat);
        AddBox(root, "CargoHook", new Vector3(0.5f, 0.3f, 0.5f), craneBase + new Vector3(1.4f, 0.8f, -2.6f), gunmetalMat);
    }

    // ==========================================
    // 8. 要塞防御重型炮塔 (Fortified Defense Turret)
    // ==========================================
    static void BuildTurret(Node3D root, bool playerOwned, Color tint)
    {
        var concreteMat = CreateConcreteFoundationMaterial();
        var armorMat = CreateArmoredSteelMaterial(tint, 2.0f);
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();
        var glowMat = CreateEnergyGlowMaterial(playerOwned ? new Color(0.2f, 0.85f, 1.0f) : new Color(1.0f, 0.28f, 0.22f), 3.0f);

        // 1. 八角形/双层圆台防爆混凝土掩体碉堡座 (Multi-Tier Bunker Bastion)
        AddCylinder(root, "BunkerPlinth", 2.6f, 3.0f, 0.9f, new Vector3(0f, 0.45f, 0f), concreteMat);
        AddCylinder(root, "ArmorApron", 2.2f, 2.5f, 0.45f, new Vector3(0f, 1.10f, 0f), gunmetalMat);
        AddCylinder(root, "HazardRing", 2.25f, 2.25f, 0.15f, new Vector3(0f, 1.35f, 0f), hazardMat);

        // 2. 炮塔底盘旋转座 (Traversing Turret Turntable)
        var turretHead = new Node3D { Name = "TurretHead", Position = new Vector3(0f, 1.4f, 0f) };
        root.AddChild(turretHead);

        // 流线型八角斜面重装甲主炮座 (Armored Turret House)
        AddBox(turretHead, "TurretHouse", new Vector3(2.6f, 1.2f, 3.0f), new Vector3(0f, 0.65f, 0.1f), armorMat);
        AddPrism(turretHead, "TurretCheekL", new Vector3(0.8f, 1.1f, 2.8f), new Vector3(-1.4f, 0.65f, 0.1f), armorMat, new Vector3(0f, Mathf.Pi, 0f));
        AddPrism(turretHead, "TurretCheekR", new Vector3(0.8f, 1.1f, 2.8f), new Vector3(1.4f, 0.65f, 0.1f), armorMat, new Vector3(0f, Mathf.Pi, 0f));

        // 3. 双联装重型滑膛加农主炮管与炮口制退器 (Twin Heavy Rifled Cannon Barrels)
        float[] gunX = { -0.55f, 0.55f };
        foreach (var gx in gunX)
        {
            AddCylinder(turretHead, $"GunRecoilBoot_{gx}", 0.28f, 0.32f, 1.0f, new Vector3(gx, 0.70f, -1.6f), gunmetalMat, new Vector3(Mathf.Pi * 0.5f, 0f, 0f));
            AddCylinder(turretHead, $"GunBarrel_{gx}", 0.16f, 0.18f, 3.8f, new Vector3(gx, 0.70f, -3.8f), gunmetalMat, new Vector3(Mathf.Pi * 0.5f, 0f, 0f));
            AddBox(turretHead, $"MuzzleBrake_{gx}", new Vector3(0.48f, 0.35f, 0.65f), new Vector3(gx, 0.70f, -5.6f), gunmetalMat);
        }

        // 4. 炮塔顶部测距仪雷达天线罩与瞄准仪 (Optical Rangefinder & Sensor Hood)
        AddBox(turretHead, "RangefinderHood", new Vector3(2.8f, 0.45f, 0.7f), new Vector3(0f, 1.35f, 0.2f), gunmetalMat);
        AddSphere(turretHead, "OpticSensorL", 0.25f, new Vector3(-1.45f, 1.35f, 0.2f), glowMat);
        AddSphere(turretHead, "OpticSensorR", 0.25f, new Vector3(1.45f, 1.35f, 0.2f), glowMat);
    }

    // ==========================================
    // 9. 军用聚变核能电厂 (Power Plant Nuclear / Fusion Station)
    // ==========================================
    static void BuildPowerPlant(Node3D root, bool playerOwned, Color tint)
    {
        var concreteMat = CreateConcreteFoundationMaterial();
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var armorMat = CreateArmoredSteelMaterial(tint, 2.0f);
        var gunmetalMat = CreateGunmetalMaterial();
        var reactorGlow = CreateEnergyGlowMaterial(playerOwned ? new Color(0.15f, 0.85f, 1.0f) : new Color(1.0f, 0.45f, 0.15f), 4.5f);

        // 1. 基台
        AddBox(root, "Plinth", new Vector3(6.6f, 0.4f, 6.6f), new Vector3(0f, 0.2f, 0f), concreteMat);

        // 2. 双联装双曲面重工业冷却塔与真实上升蒸汽水雾粒子 (Twin Hyperbolic Cooling Towers)
        float[] cX = { -1.8f, 1.8f };
        foreach (var cx in cX)
        {
            AddCylinder(root, $"CoolingBase_{cx}", 1.4f, 1.7f, 1.6f, new Vector3(cx, 1.1f, -1.2f), concreteMat, null, 28);
            AddCylinder(root, $"CoolingNeck_{cx}", 1.15f, 1.4f, 1.6f, new Vector3(cx, 2.6f, -1.2f), concreteMat, null, 28);
            AddCylinder(root, $"CoolingFlare_{cx}", 1.35f, 1.15f, 1.6f, new Vector3(cx, 4.1f, -1.2f), concreteMat, null, 28);
            // 塔顶环箍
            AddCylinder(root, $"CoolingRim_{cx}", 1.4f, 1.4f, 0.25f, new Vector3(cx, 4.95f, -1.2f), gunmetalMat, null, 28);
            // 动态上升冷凝水蒸汽粒子
            AddSmokeParticles(root, $"SteamParticles_{cx}", new Vector3(cx, 5.2f, -1.2f), new Color(0.92f, 0.94f, 0.98f, 0.55f), 3.2f, 2.8f, 1.3f);
        }

        // 3. 中央聚变反应堆核心厂房 (Fusion Reactor Core Hall)
        AddBox(root, "GeneratorHall", new Vector3(5.6f, 2.0f, 2.8f), new Vector3(0f, 1.25f, 1.6f), armorMat);
        AddSphere(root, "ReactorCoreSphere", 1.4f, new Vector3(0f, 2.6f, 1.6f), reactorGlow);
        AddCylinder(root, "ReactorRingA", 1.55f, 1.55f, 0.25f, new Vector3(0f, 2.6f, 1.6f), gunmetalMat);

        // 4. 变电站高压绝缘子架与重型输电母线 (Transformer Insulator Stacks)
        float[] insX = { -2.0f, -0.7f, 0.7f, 2.0f };
        foreach (var ix in insX)
        {
            AddCylinder(root, $"Insulator_{ix}", 0.12f, 0.18f, 1.2f, new Vector3(ix, 2.8f, 2.6f), gunmetalMat);
        }
    }

    // ==========================================
    // 10. 战略矿物精炼厂 (Gold Mine Mineral Refinery)
    // ==========================================
    static void BuildGoldMine(Node3D root, bool playerOwned, Color tint)
    {
        var concreteMat = CreateConcreteFoundationMaterial();
        var floorMat = CreateDiamondPlatingMaterial(3.0f);
        var armorMat = CreateArmoredSteelMaterial(tint, 2.0f);
        var gunmetalMat = CreateGunmetalMaterial();
        var hazardMat = CreateHazardStripeMaterial();
        var goldOreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.75f, 0.18f),
            Metallic = 0.95f,
            Roughness = 0.28f
        };

        // 1. 基础
        AddBox(root, "Plinth", new Vector3(6.6f, 0.4f, 6.6f), new Vector3(0f, 0.2f, 0f), concreteMat);

        // 2. 矿石受料漏斗与自卸引坡 (Receiving Dump Hopper & Ramp)
        AddBox(root, "DumpHopper", new Vector3(2.6f, 1.8f, 2.6f), new Vector3(-1.8f, 1.2f, -1.6f), gunmetalMat);
        AddBox(root, "DumpRamp", new Vector3(2.8f, 0.22f, 2.0f), new Vector3(-1.8f, 0.35f, -3.4f), floorMat);
        AddBox(root, "RampHazard", new Vector3(2.8f, 0.15f, 0.25f), new Vector3(-1.8f, 0.45f, -2.4f), hazardMat);

        // 3. 倾斜重型桁架输送机 (Inclined Truss Conveyor)
        AddBox(root, "ConveyorTruss", new Vector3(0.85f, 0.45f, 4.2f), new Vector3(-0.4f, 2.4f, 0.2f), gunmetalMat, new Vector3(Mathf.DegToRad(-26f), 0f, 0f));
        AddCylinder(root, "ConveyorLeg", 0.12f, 0.12f, 2.2f, new Vector3(-0.4f, 1.3f, 0.8f), gunmetalMat);

        // 4. 精炼分离旋流塔与浸出反应罐 (Refining Cyclones & Leaching Silos)
        AddBox(root, "RefiningTower", new Vector3(2.6f, 3.8f, 2.6f), new Vector3(1.8f, 2.2f, 1.6f), armorMat);
        AddCylinder(root, "LeachSiloA", 0.75f, 0.75f, 2.6f, new Vector3(1.8f, 1.6f, -1.4f), gunmetalMat);
        AddCylinder(root, "LeachSiloB", 0.65f, 0.65f, 2.2f, new Vector3(0.2f, 1.4f, -1.4f), gunmetalMat);

        // 5. 高纯度战略精金/合金锭堆 (Refined High-Purity Metal Ingot Mound)
        AddBox(root, "IngotPlatform", new Vector3(1.8f, 0.25f, 1.8f), new Vector3(-1.8f, 0.45f, 1.8f), floorMat);
        AddBox(root, "IngotTier1", new Vector3(1.4f, 0.4f, 1.4f), new Vector3(-1.8f, 0.75f, 1.8f), goldOreMat);
        AddBox(root, "IngotTier2", new Vector3(0.9f, 0.35f, 0.9f), new Vector3(-1.8f, 1.10f, 1.8f), goldOreMat);
    }

    // ==========================================
    // 基础三维几何部件快速生成助手 (Mesh Construction Helpers)
    // ==========================================
    static void AddBox(Node3D parent, string name, Vector3 size, Vector3 pos, Material mat, Vector3? rot = null)
    {
        var box = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = pos,
            MaterialOverride = mat
        };
        if (rot.HasValue) box.Rotation = rot.Value;
        parent.AddChild(box);
    }

    static void AddCylinder(Node3D parent, string name, float topR, float bottomR, float height, Vector3 pos, Material mat, Vector3? rot = null, int segments = 24)
    {
        var cyl = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = topR,
                BottomRadius = bottomR,
                Height = height,
                RadialSegments = segments
            },
            Position = pos,
            MaterialOverride = mat
        };
        if (rot.HasValue) cyl.Rotation = rot.Value;
        parent.AddChild(cyl);
    }

    static void AddPrism(Node3D parent, string name, Vector3 size, Vector3 pos, Material mat, Vector3? rot = null)
    {
        var prism = new MeshInstance3D
        {
            Name = name,
            Mesh = new PrismMesh { Size = size },
            Position = pos,
            MaterialOverride = mat
        };
        if (rot.HasValue) prism.Rotation = rot.Value;
        parent.AddChild(prism);
    }

    static void AddSphere(Node3D parent, string name, float radius, Vector3 pos, Material mat)
    {
        var sphere = new MeshInstance3D
        {
            Name = name,
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 2f,
                RadialSegments = 20,
                Rings = 12
            },
            Position = pos,
            MaterialOverride = mat
        };
        parent.AddChild(sphere);
    }

    static void AddSmokeParticles(Node3D parent, string name, Vector3 pos, Color smokeColor, float lifetime = 2.5f, float speed = 3.5f, float scale = 1.0f)
    {
        var particles = new CpuParticles3D
        {
            Name = name,
            Position = pos,
            Amount = 18,
            Lifetime = lifetime,
            SpeedScale = 1.0f,
            Explosiveness = 0.0f,
            Randomness = 0.4f,
            Direction = new Vector3(0.2f, 1.0f, 0.1f),
            Spread = 16.0f,
            Gravity = new Vector3(0.2f, 1.2f, 0.1f),
            InitialVelocityMin = speed * 0.7f,
            InitialVelocityMax = speed * 1.2f,
            ScaleAmountMin = 0.4f * scale,
            ScaleAmountMax = 1.3f * scale,
            Color = smokeColor,
            DrawOrder = CpuParticles3D.DrawOrderEnum.ViewDepth
        };

        var sphereMesh = new SphereMesh
        {
            Radius = 0.35f * scale,
            Height = 0.7f * scale,
            RadialSegments = 8,
            Rings = 4
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = smokeColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        sphereMesh.Material = mat;
        particles.Mesh = sphereMesh;

        parent.AddChild(particles);
    }
}

/// <summary>
/// 建筑相控阵雷达动态 3D 旋转器 (Smooth 360° Military Radar Rotator)
/// </summary>
public partial class BuildingRadarRotator : Node
{
    [Export] public float RotateSpeed { get; set; } = 1.8f;

    public override void _Process(double delta)
    {
        if (GetParent() is Node3D parent3D)
        {
            parent3D.RotateY((float)delta * RotateSpeed);
        }
    }
}
