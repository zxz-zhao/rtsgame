using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Godot 4 3D RTS 现代化高精度战术小地图与态势感知雷达 (Tactical Minimap & Radar System)
/// 严格遵守 Camera3D 180° Y-yaw 逆转铁律：
/// 屏幕左 = +X，屏幕右 = -X，屏幕上 = +Z，屏幕下 = -Z
/// 小地图归一化映射：(0.5f - X/W, 0.5f - Z/H)
/// 支持：
/// 1. 地貌水文全域呈现：陆地、水系河道海峡、跨河战略大桥、补给道路、高地悬崖、战略要塞停机坪
/// 2. 四角地面视锥多边形投影与视口高光
/// 3. 动态全域雷达旋转扫描光束与余晖拖尾
/// 4. 实时敌我态势光点 (单位与建筑，敌军高亮发光警戒)
/// 5. 动态战术报警雷达脉冲环与警报蜂鸣 (敌军发现 / 基地交火)
/// 6. 毫秒级视口重定向点击与平滑拖拽
/// </summary>
public partial class BattleMinimap : Control
{
    public static BattleMinimap? Instance { get; private set; }

    [Export] private Vector2 mapWorldSize = new Vector2(400f, 400f);

    public enum MinimapPingType
    {
        EnemySpotted,   // 敌军侦测警报 (Orange-Red expanding radar shockwave)
        UnderAttack,    // 遭受攻击警报 (Crimson-Red pulsing hazard ring)
        TacticalBeacon  // 战术集结标记 (Cyan beacon)
    }

    public enum BlipKind
    {
        Unit,
        AirUnit,
        HeavyUnit,
        Building,
        MainBase
    }

    public struct MinimapBlip
    {
        public Vector2 NormPos;
        public bool IsPlayer;
        public BlipKind Kind;
        public bool IsGhost;
    }

    public sealed class MinimapPing
    {
        public Vector3 WorldPos;
        public Vector2 NormPos;
        public MinimapPingType Type;
        public float Lifetime;
        public float MaxLifetime;
        public string Label = string.Empty;
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Instance == this)
            Instance = null;
    }

    private double lastEnemySpottedAlertTime = -999.0;
    private double lastUnderAttackAlertTime = -999.0;

    public void NotifyEnemySpotted(Vector3 pos, string displayName, bool isBuilding)
    {
        AddPing(pos, MinimapPingType.EnemySpotted, isBuilding ? $"敌据点:{displayName}" : $"敌军:{displayName}");

        var now = Godot.Time.GetTicksMsec() / 1000.0;
        if (now - lastEnemySpottedAlertTime >= 7.0)
        {
            lastEnemySpottedAlertTime = now;
            BattleFeedback.PlayEnemySpottedAlert();

            var hud = FindHud();
            if (hud is not null)
            {
                string msg = isBuilding 
                    ? $"【雷达预警】侦测到敌军据点建筑 [{displayName}]！方位已在小地图标红！" 
                    : $"【战况预警】雷达侦测到敌军部队 [{displayName}]！方位已在小地图高亮！";
                hud.ShowAlert(msg);
                hud.AppendBattleCommunicationMessage("战术雷达", msg, new Color(1f, 0.55f, 0.15f));
            }
        }
    }

    public void NotifyUnderAttack(Vector3 pos, bool isMainBase)
    {
        AddPing(pos, MinimapPingType.UnderAttack, isMainBase ? "基地遇袭!" : "战火交锋!");

        var now = Godot.Time.GetTicksMsec() / 1000.0;
        if (now - lastUnderAttackAlertTime >= 6.0)
        {
            lastUnderAttackAlertTime = now;
            BattleFeedback.PlayUnderAttackAlert();
        }
    }

    public void AddPing(Vector3 worldPos, MinimapPingType type, string label = "")
    {
        if (mapWorldSize.X <= 0.1f || mapWorldSize.Y <= 0.1f) return;
        float nx = 0.5f - (worldPos.X / mapWorldSize.X);
        float ny = 0.5f - (worldPos.Z / mapWorldSize.Y);

        for (int i = 0; i < activePings.Count; i++)
        {
            var existing = activePings[i];
            if (existing.Type == type && existing.NormPos.DistanceTo(new Vector2(nx, ny)) < 0.05f)
            {
                existing.Lifetime = 4.0f;
                return;
            }
        }

        activePings.Add(new MinimapPing
        {
            WorldPos = worldPos,
            NormPos = new Vector2(nx, ny),
            Type = type,
            Lifetime = 4.0f,
            MaxLifetime = 4.0f,
            Label = label
        });
    }

    private BattleHud? FindHud()
    {
        return GetTree()?.Root.GetNodeOrNull<BattleHud>("HUD")
            ?? GetTree()?.CurrentScene?.GetNodeOrNull<BattleHud>("HUD")
            ?? GetParent()?.GetParent() as BattleHud
            ?? GetTree()?.GetFirstNodeInGroup("battle_hud") as BattleHud;
    }

    private float sweepAngle = 0f;
    private double refreshTimer = 0.0;
    private bool isDragging = false;
    private string loadedMapSignature = string.Empty;
    private Vector2 lastDrawnSize = Vector2.Zero;

    private Color landColor;
    private Color waterColor;
    private Color shoreColor;
    private Color foliageColor;
    private Color rockColor;

    private readonly List<TerrainFeature> plateauFeatures = new();
    private readonly List<TerrainFeature> patchFeatures = new();
    private readonly List<TerrainFeature> waterFeatures = new();
    private readonly List<TerrainFeature> roadFeatures = new();
    private readonly List<TerrainFeature> bridgeFeatures = new();
    private readonly List<Vector2> normTreePositions = new();
    private readonly List<Vector2> normRockPositions = new();
    private readonly List<Vector2> screenTreePositions = new();
    private readonly List<Vector2> screenRockPositions = new();

    private readonly List<MinimapBlip> blips = new(128);
    private readonly List<MinimapPing> activePings = new(16);
    private readonly Vector2[] frustumPoints = new Vector2[5];
    private readonly Vector2[] frustumPoly4 = new Vector2[4];
    private bool hasValidFrustum = false;

    private sealed class TerrainFeature
    {
        public Vector2[] NormQuad = new Vector2[4];
        public Vector2[] ScreenQuad = new Vector2[4];
        public Vector2[] ScreenClosed = new Vector2[5];
        public Vector2[] ScreenEdgeA = new Vector2[2];
        public Vector2[] ScreenEdgeB = new Vector2[2];
        public Vector2[] ScreenCenter = new Vector2[2];

        public Color FillColor;
        public Color BorderColor;
        public float BorderWidth = 1.0f;
        public bool HasBorder = true;
        public bool HasCenterLine = false;
        public Color CenterLineColor = Colors.Transparent;

        public void UpdateScreen(Vector2 sz)
        {
            for (int i = 0; i < 4; i++)
            {
                ScreenQuad[i] = new Vector2(NormQuad[i].X * sz.X, NormQuad[i].Y * sz.Y);
                ScreenClosed[i] = ScreenQuad[i];
            }
            ScreenClosed[4] = ScreenQuad[0];

            ScreenEdgeA[0] = ScreenQuad[0];
            ScreenEdgeA[1] = ScreenQuad[3];

            ScreenEdgeB[0] = ScreenQuad[1];
            ScreenEdgeB[1] = ScreenQuad[2];

            ScreenCenter[0] = (ScreenQuad[0] + ScreenQuad[1]) * 0.5f;
            ScreenCenter[1] = (ScreenQuad[3] + ScreenQuad[2]) * 0.5f;
        }
    }

    public override void _Ready()
    {
        Instance = this;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        CustomMinimumSize = new Vector2(180, 140);
        UpdateWorldMapSize();
    }

    private void UpdateWorldMapSize()
    {
        try
        {
            var map = ResolveActiveMap();
            var halfSize = BattleMapCatalog.GetMapHalfSize(map);
            if (halfSize > 10f)
            {
                mapWorldSize = new Vector2(halfSize * 2f, halfSize * 2f);
            }
            else
            {
                mapWorldSize = new Vector2(400f, 400f);
            }

            BuildTerrainVisuals(map);
            lastDrawnSize = Vector2.Zero; // 触发屏幕坐标更新
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[BattleMinimap] UpdateWorldMapSize error: {ex.Message}");
            mapWorldSize = new Vector2(400f, 400f);
        }
    }

    private BattleMapDefinition ResolveActiveMap()
    {
        if (BattleMapRenderer.Instance?.CurrentBuiltMap is { } builtMap)
            return builtMap;

        var mapName = BattleMapCatalog.RequestedMapName(GameState.Instance?.SelectedMapName);
        return BattleMapCatalog.Get(mapName);
    }

    private void BuildTerrainVisuals(BattleMapDefinition map)
    {
        loadedMapSignature = $"{map.Name}_{map.MapHalfSize}_{map.Waters.Length}_{map.Bridges.Length}_{map.Roads.Length}";

        // 1. 战术地貌基色计算 (Tactical Topographic Land Palette)
        var gc = map.GroundColor;
        landColor = new Color(
            Mathf.Clamp(gc.R * 0.36f + 0.06f, 0.10f, 0.40f),
            Mathf.Clamp(gc.G * 0.36f + 0.06f, 0.10f, 0.40f),
            Mathf.Clamp(gc.B * 0.36f + 0.07f, 0.10f, 0.42f),
            0.96f
        );

        // 2. 战术水系与熔岩色系计算 (Naval Tactical Waters / Volcanic Magma)
        if (map.Name == BattleMapCatalog.VolcanoFortressName)
        {
            waterColor = new Color(0.80f, 0.25f, 0.05f, 0.94f); // 炽热岩浆
            shoreColor = new Color(1.0f, 0.65f, 0.20f, 0.92f); // 亮橙熔岩岸边缘
        }
        else
        {
            var wc = map.WaterColor;
            waterColor = new Color(
                Mathf.Clamp(wc.R * 0.42f + 0.04f, 0.04f, 0.22f),
                Mathf.Clamp(wc.G * 0.55f + 0.18f, 0.18f, 0.54f),
                Mathf.Clamp(wc.B * 0.68f + 0.32f, 0.42f, 0.85f),
                0.94f
            );
            shoreColor = new Color(
                Mathf.Clamp(waterColor.R * 1.4f + 0.1f, 0.15f, 0.85f),
                Mathf.Clamp(waterColor.G * 1.4f + 0.15f, 0.35f, 0.95f),
                Mathf.Clamp(waterColor.B * 1.3f + 0.18f, 0.60f, 1.0f),
                0.85f
            );
        }

        // 3. 战略悬崖与高台 (Elevated Plateaus)
        plateauFeatures.Clear();
        foreach (var plateau in map.ElevatedPlateaus)
        {
            var normQuad = ComputeStripNormCorners(plateau.Center, plateau.Size, plateau.Angle, mapWorldSize);
            plateauFeatures.Add(new TerrainFeature
            {
                NormQuad = normQuad,
                FillColor = landColor.Lightened(0.18f),
                BorderColor = new Color(0.72f, 0.84f, 0.96f, 0.50f),
                BorderWidth = 1.2f,
                HasBorder = true
            });
        }

        // 4. 基地停机坪、广场与森林地块 (Patches)
        patchFeatures.Clear();
        foreach (var patch in map.Patches)
        {
            var normQuad = ComputeStripNormCorners(patch.Center, patch.Size, patch.Angle, mapWorldSize);
            Color pFill;
            Color pBorder;
            bool hasBorder = true;

            if (patch.PaletteIndex == 2) // 友方基地停机坪
            {
                pFill = new Color(0.10f, 0.36f, 0.52f, 0.72f);
                pBorder = new Color(0.20f, 0.90f, 1.0f, 0.80f);
            }
            else if (patch.PaletteIndex == 3) // 敌方基地停机坪
            {
                pFill = new Color(0.50f, 0.16f, 0.14f, 0.72f);
                pBorder = new Color(1.0f, 0.35f, 0.22f, 0.80f);
            }
            else // 森林/绿洲/中立地块
            {
                var pc = map.GetPatchColor(patch.PaletteIndex);
                pFill = new Color(
                    Mathf.Clamp(pc.R * 0.35f + 0.05f, 0.08f, 0.36f),
                    Mathf.Clamp(pc.G * 0.35f + 0.06f, 0.08f, 0.38f),
                    Mathf.Clamp(pc.B * 0.35f + 0.06f, 0.08f, 0.38f),
                    0.70f
                );
                pBorder = pFill.Lightened(0.25f);
                pBorder.A = 0.50f;
            }

            patchFeatures.Add(new TerrainFeature
            {
                NormQuad = normQuad,
                FillColor = pFill,
                BorderColor = pBorder,
                BorderWidth = 1.0f,
                HasBorder = hasBorder
            });
        }

        // 5. 战术水系与河道 (Waters)
        waterFeatures.Clear();
        foreach (var water in map.Waters)
        {
            var normQuad = ComputeStripNormCorners(water.Center, water.Size, water.Angle, mapWorldSize);
            waterFeatures.Add(new TerrainFeature
            {
                NormQuad = normQuad,
                FillColor = waterColor,
                BorderColor = shoreColor,
                BorderWidth = 1.4f,
                HasBorder = true,
                HasCenterLine = true,
                CenterLineColor = new Color(shoreColor.R, shoreColor.G, shoreColor.B, 0.35f)
            });
        }

        // 6. 陆军补给道路 (Roads)
        roadFeatures.Clear();
        foreach (var road in map.Roads)
        {
            var normQuad = ComputeStripNormCorners(road.Center, road.Size, road.Angle, mapWorldSize);
            roadFeatures.Add(new TerrainFeature
            {
                NormQuad = normQuad,
                FillColor = new Color(0.20f, 0.20f, 0.21f, 0.80f),
                BorderColor = new Color(0.48f, 0.46f, 0.38f, 0.55f),
                BorderWidth = 1.0f,
                HasBorder = true,
                HasCenterLine = true,
                CenterLineColor = new Color(0.72f, 0.68f, 0.45f, 0.40f)
            });
        }

        // 7. 跨河战略大桥 (Bridges)
        bridgeFeatures.Clear();
        foreach (var bridge in map.Bridges)
        {
            var normQuad = ComputeStripNormCorners(bridge.Center, new Vector2(bridge.Width, bridge.Length), bridge.Angle, mapWorldSize);
            Color deckColor = bridge.BridgeType switch
            {
                "steel" => new Color(0.35f, 0.42f, 0.52f, 1.0f),
                "wood" => new Color(0.45f, 0.32f, 0.18f, 1.0f),
                _ => new Color(0.50f, 0.48f, 0.44f, 1.0f) // stone/concrete
            };

            bridgeFeatures.Add(new TerrainFeature
            {
                NormQuad = normQuad,
                FillColor = deckColor,
                BorderColor = new Color(0.96f, 0.84f, 0.22f, 0.95f), // 高警示黄色防撞护栏
                BorderWidth = 1.6f,
                HasBorder = true,
                HasCenterLine = true,
                CenterLineColor = new Color(1.0f, 1.0f, 1.0f, 0.35f)
            });
        }

        // 8. 自然有机地貌：岩石群与林木簇 (Rocks & Trees)
        var fc = map.FoliageColor;
        foliageColor = new Color(
            Mathf.Clamp(fc.R * 0.45f + 0.05f, 0.08f, 0.35f),
            Mathf.Clamp(fc.G * 0.55f + 0.15f, 0.20f, 0.55f),
            Mathf.Clamp(fc.B * 0.45f + 0.05f, 0.08f, 0.35f),
            0.85f
        );

        var rc = map.RockColor;
        rockColor = new Color(
            Mathf.Clamp(rc.R * 0.40f + 0.15f, 0.20f, 0.55f),
            Mathf.Clamp(rc.G * 0.40f + 0.15f, 0.20f, 0.55f),
            Mathf.Clamp(rc.B * 0.40f + 0.15f, 0.20f, 0.55f),
            0.85f
        );

        normTreePositions.Clear();
        if (map.TreePositions != null)
        {
            foreach (var tree in map.TreePositions)
            {
                normTreePositions.Add(new Vector2(
                    0.5f - (tree.X / mapWorldSize.X),
                    0.5f - (tree.Z / mapWorldSize.Y)
                ));
            }
        }

        normRockPositions.Clear();
        if (map.RockClusters != null)
        {
            foreach (var rock in map.RockClusters)
            {
                normRockPositions.Add(new Vector2(
                    0.5f - (rock.X / mapWorldSize.X),
                    0.5f - (rock.Z / mapWorldSize.Y)
                ));
            }
        }
    }

    private static Vector2[] ComputeStripNormCorners(Vector3 center, Vector2 size, float angleDeg, Vector2 worldSize)
    {
        float halfW = size.X * 0.5f;
        float halfL = size.Y * 0.5f;

        var basis = new Basis(Vector3.Up, Mathf.DegToRad(angleDeg));

        // 局部坐标系 (Y=0, X为宽度, Z为长度)
        Vector3 p0 = center + basis * new Vector3(-halfW, 0f, -halfL);
        Vector3 p1 = center + basis * new Vector3( halfW, 0f, -halfL);
        Vector3 p2 = center + basis * new Vector3( halfW, 0f,  halfL);
        Vector3 p3 = center + basis * new Vector3(-halfW, 0f,  halfL);

        // 严格遵守 180° Y-yaw 逆转铁律：
        // normX = 0.5f - X / W, normY = 0.5f - Z / H
        return new Vector2[]
        {
            new Vector2(0.5f - (p0.X / worldSize.X), 0.5f - (p0.Z / worldSize.Y)),
            new Vector2(0.5f - (p1.X / worldSize.X), 0.5f - (p1.Z / worldSize.Y)),
            new Vector2(0.5f - (p2.X / worldSize.X), 0.5f - (p2.Z / worldSize.Y)),
            new Vector2(0.5f - (p3.X / worldSize.X), 0.5f - (p3.Z / worldSize.Y)),
        };
    }

    private void UpdateAllScreenFeatures(Vector2 sz)
    {
        foreach (var f in plateauFeatures) f.UpdateScreen(sz);
        foreach (var f in patchFeatures) f.UpdateScreen(sz);
        foreach (var f in waterFeatures) f.UpdateScreen(sz);
        foreach (var f in roadFeatures) f.UpdateScreen(sz);
        foreach (var f in bridgeFeatures) f.UpdateScreen(sz);

        screenTreePositions.Clear();
        for (int i = 0; i < normTreePositions.Count; i++)
        {
            var p = normTreePositions[i];
            screenTreePositions.Add(new Vector2(p.X * sz.X, p.Y * sz.Y));
        }

        screenRockPositions.Clear();
        for (int i = 0; i < normRockPositions.Count; i++)
        {
            var p = normRockPositions[i];
            screenRockPositions.Add(new Vector2(p.X * sz.X, p.Y * sz.Y));
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        sweepAngle += dt * 2.2f;
        if (sweepAngle > Mathf.Tau)
        {
            sweepAngle -= Mathf.Tau;
        }

        // 更新活动战术警报脉冲
        for (int i = activePings.Count - 1; i >= 0; i--)
        {
            var p = activePings[i];
            p.Lifetime -= dt;
            if (p.Lifetime <= 0f)
            {
                activePings.RemoveAt(i);
            }
        }

        refreshTimer += delta;
        if (refreshTimer >= 0.08) // 每 80ms 刷新一次战场态势，保持零 GC 与低开销
        {
            refreshTimer = 0.0;
            CheckMapRefresh();
            CollectBattleTelemetry();
        }

        QueueRedraw();
    }

    private void CheckMapRefresh()
    {
        var currentMap = ResolveActiveMap();
        var sig = $"{currentMap.Name}_{currentMap.MapHalfSize}_{currentMap.Waters.Length}_{currentMap.Bridges.Length}_{currentMap.Roads.Length}";
        if (sig != loadedMapSignature)
        {
            UpdateWorldMapSize();
        }
    }

    private void CollectBattleTelemetry()
    {
        blips.Clear();
        var tree = GetTree();
        if (tree is null) return;

        // 1. 采集全场单位 (零 GC 高性能遍历)
        var activeUnits = RtsUnit.AllActiveUnits;
        for (int i = 0; i < activeUnits.Count; i++)
        {
            var u = activeUnits[i];
            if (!GodotObject.IsInstanceValid(u) || u.IsDead)
                continue;

            // 仅在迷雾揭晓或关闭迷雾时显示敌军
            bool isRevealed = u.PlayerOwned 
                || u.FogRevealed 
                || BattleGameManager.DisableFogOfWar;

            if (!isRevealed)
                continue;

            // 遵守 180° Y-yaw 逆转：normX = 0.5f - X / W, normY = 0.5f - Z / H
            float nx = 0.5f - (u.GlobalPosition.X / mapWorldSize.X);
            float ny = 0.5f - (u.GlobalPosition.Z / mapWorldSize.Y);

            bool isAir = BattleUnitCatalog.IsAirUnit(u.UnitKey);
            bool isHeavy = BattleUnitCatalog.IsCarrier(u.UnitKey) || u.MaxHealth >= 700f;
            blips.Add(new MinimapBlip
            {
                NormPos = new Vector2(nx, ny),
                IsPlayer = u.PlayerOwned,
                Kind = isAir ? BlipKind.AirUnit : (isHeavy ? BlipKind.HeavyUnit : BlipKind.Unit),
                IsGhost = false
            });
        }

        // 2. 采集全场建筑 (零 GC 高性能遍历)
        var activeBuildings = RtsBuilding.AllActiveBuildings;
        for (int i = 0; i < activeBuildings.Count; i++)
        {
            var b = activeBuildings[i];
            if (!GodotObject.IsInstanceValid(b) || b.IsRuined)
                continue;

            bool isCurrentlyVisible = b.PlayerOwned 
                || b.FogRevealed 
                || BattleGameManager.DisableFogOfWar;

            bool isExplored = b.FogExplored;

            if (!isCurrentlyVisible && !isExplored)
                continue;

            float nx = 0.5f - (b.GlobalPosition.X / mapWorldSize.X);
            float ny = 0.5f - (b.GlobalPosition.Z / mapWorldSize.Y);

            blips.Add(new MinimapBlip
            {
                NormPos = new Vector2(nx, ny),
                IsPlayer = b.PlayerOwned,
                Kind = b.IsMainBase ? BlipKind.MainBase : BlipKind.Building,
                IsGhost = !isCurrentlyVisible && isExplored
            });
        }

        // 3. 采集当前 3D 摄像机地面视锥投影多边形
        hasValidFrustum = false;
        var rtsCam = FindRtsCamera();
        if (rtsCam is not null && rtsCam.TryGetViewportGroundPolygon(out var groundPolygon) && groundPolygon.Length == 4)
        {
            var sz = Size;
            for (int i = 0; i < 4; i++)
            {
                float nx = 0.5f - (groundPolygon[i].X / mapWorldSize.X);
                float ny = 0.5f - (groundPolygon[i].Z / mapWorldSize.Y);
                frustumPoints[i] = new Vector2(nx * sz.X, ny * sz.Y);
                frustumPoly4[i] = frustumPoints[i];
            }
            frustumPoints[4] = frustumPoints[0]; // 闭合多边形
            hasValidFrustum = true;
        }
    }

    private RtsCamera? FindRtsCamera()
    {
        if (RtsCamera.Instance is not null && GodotObject.IsInstanceValid(RtsCamera.Instance))
            return RtsCamera.Instance;

        var tree = GetTree();
        if (tree is not null)
        {
            var cams = tree.GetNodesInGroup("rts_cameras");
            if (cams.Count > 0 && cams[0] is RtsCamera cam && GodotObject.IsInstanceValid(cam))
                return cam;
        }

        var viewportCam = GetViewport()?.GetCamera3D();
        return viewportCam?.GetParent() as RtsCamera;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                isDragging = true;
                HandleMinimapNavigation(mb.Position);
            }
            else
            {
                isDragging = false;
            }
            GetViewport()?.SetInputAsHandled();
        }
        else if (@event is InputEventMouseMotion mm && isDragging)
        {
            HandleMinimapNavigation(mm.Position);
            GetViewport()?.SetInputAsHandled();
        }
        else if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                isDragging = true;
                HandleMinimapNavigation(touch.Position);
            }
            else
            {
                isDragging = false;
            }
            GetViewport()?.SetInputAsHandled();
        }
        else if (@event is InputEventScreenDrag drag && isDragging)
        {
            HandleMinimapNavigation(drag.Position);
            GetViewport()?.SetInputAsHandled();
        }
    }

    private void HandleMinimapNavigation(Vector2 localPos)
    {
        var sz = Size;
        if (sz.X <= 1f || sz.Y <= 1f) return;

        // 若直接点击了活动警报光标附近，优先精确定位到警报中心
        for (int i = 0; i < activePings.Count; i++)
        {
            var ping = activePings[i];
            var pingScreen = new Vector2(ping.NormPos.X * sz.X, ping.NormPos.Y * sz.Y);
            if (pingScreen.DistanceTo(localPos) <= 16f)
            {
                var cam = FindRtsCamera();
                if (cam is not null && GodotObject.IsInstanceValid(cam))
                {
                    cam.JumpTo(new Vector3(ping.WorldPos.X, 0f, ping.WorldPos.Z));
                }
                return;
            }
        }

        float normX = Mathf.Clamp(localPos.X / sz.X, 0f, 1f);
        float normY = Mathf.Clamp(localPos.Y / sz.Y, 0f, 1f);

        // 严格遵循 180° Y-yaw 逆转反推世界坐标
        float worldX = (0.5f - normX) * mapWorldSize.X;
        float worldZ = (0.5f - normY) * mapWorldSize.Y;

        var rtsCam = FindRtsCamera();
        if (rtsCam is not null && GodotObject.IsInstanceValid(rtsCam))
        {
            rtsCam.JumpTo(new Vector3(worldX, 0f, worldZ));
        }
    }

    public override void _Draw()
    {
        var sz = Size;
        if (sz.X < 20f || sz.Y < 20f) return;

        // 屏幕坐标尺寸更新 (仅在尺寸变化时执行，保持零 GC)
        if (sz != lastDrawnSize)
        {
            lastDrawnSize = sz;
            UpdateAllScreenFeatures(sz);
        }

        // 1. 深色工业金属战术底板 (Dark Slate Military Frame Plate)
        DrawRect(new Rect2(Vector2.Zero, sz), new Color(0.03f, 0.04f, 0.06f, 0.98f), true);

        // 2. 战术全域陆地底板 (Tactical Land Surface Base)
        DrawRect(new Rect2(Vector2.Zero, sz), landColor, true);

        // 3. 战略悬崖与高台 (Elevated Plateaus)
        foreach (var plateau in plateauFeatures)
        {
            DrawColoredPolygon(plateau.ScreenQuad, plateau.FillColor);
            if (plateau.HasBorder)
            {
                DrawPolyline(plateau.ScreenClosed, plateau.BorderColor, plateau.BorderWidth);
            }
        }

        // 4. 基地停机坪、广场与森林地块 (Terrain Patches)
        foreach (var patch in patchFeatures)
        {
            DrawColoredPolygon(patch.ScreenQuad, patch.FillColor);
            if (patch.HasBorder)
            {
                DrawPolyline(patch.ScreenClosed, patch.BorderColor, patch.BorderWidth);
            }
        }

        // 5. 战术水系与大河水道 (Rivers & Waterways)
        foreach (var water in waterFeatures)
        {
            DrawColoredPolygon(water.ScreenQuad, water.FillColor);
            // 绘制两侧发光河岸线 (Glowing Shorelines)
            DrawLine(water.ScreenEdgeA[0], water.ScreenEdgeA[1], water.BorderColor, water.BorderWidth);
            DrawLine(water.ScreenEdgeB[0], water.ScreenEdgeB[1], water.BorderColor, water.BorderWidth);
            // 水流航道中心虚光线
            if (water.HasCenterLine)
            {
                DrawLine(water.ScreenCenter[0], water.ScreenCenter[1], water.CenterLineColor, 1.0f);
            }
        }

        // 6. 陆军行军与补给道路 (Roads)
        foreach (var road in roadFeatures)
        {
            DrawColoredPolygon(road.ScreenQuad, road.FillColor);
            DrawLine(road.ScreenEdgeA[0], road.ScreenEdgeA[1], road.BorderColor, road.BorderWidth);
            DrawLine(road.ScreenEdgeB[0], road.ScreenEdgeB[1], road.BorderColor, road.BorderWidth);
        }

        // 7. 跨河战略大桥 (Bridges - 覆盖在河流之上以清晰展示通行路线)
        foreach (var bridge in bridgeFeatures)
        {
            DrawColoredPolygon(bridge.ScreenQuad, bridge.FillColor);
            // 两侧醒目黄色警戒防护栏 (High-Vis Safety Railings)
            DrawLine(bridge.ScreenEdgeA[0], bridge.ScreenEdgeA[1], bridge.BorderColor, bridge.BorderWidth);
            DrawLine(bridge.ScreenEdgeB[0], bridge.ScreenEdgeB[1], bridge.BorderColor, bridge.BorderWidth);
            // 桥梁中轴线
            if (bridge.HasCenterLine)
            {
                DrawLine(bridge.ScreenCenter[0], bridge.ScreenCenter[1], bridge.CenterLineColor, 1.0f);
            }
        }

        // 7.5 自然地貌要素：岩石群与林木簇 (Topographic Rocks & Foliage Clusters)
        for (int i = 0; i < screenRockPositions.Count; i++)
        {
            var pt = screenRockPositions[i];
            if (pt.X >= 2f && pt.X <= sz.X - 2f && pt.Y >= 2f && pt.Y <= sz.Y - 2f)
            {
                DrawCircle(pt, 2.2f, rockColor);
                DrawArc(pt, 2.8f, 0, Mathf.Tau, 4, rockColor.Lightened(0.2f), 0.8f);
            }
        }

        for (int i = 0; i < screenTreePositions.Count; i++)
        {
            var pt = screenTreePositions[i];
            if (pt.X >= 2f && pt.X <= sz.X - 2f && pt.Y >= 2f && pt.Y <= sz.Y - 2f)
            {
                DrawCircle(pt, 2.4f, foliageColor);
                DrawCircle(pt, 1.2f, foliageColor.Lightened(0.25f));
            }
        }

        // 8. 战术坐标网格 (25% / 50% / 75%) 与中心十字准星
        var gridColor = new Color(0.12f, 0.38f, 0.48f, 0.22f);
        var axisColor = new Color(0.20f, 0.65f, 0.78f, 0.42f);

        for (int i = 1; i <= 3; i++)
        {
            float fx = sz.X * (i * 0.25f);
            float fy = sz.Y * (i * 0.25f);
            DrawLine(new Vector2(fx, 0), new Vector2(fx, sz.Y), i == 2 ? axisColor : gridColor, 1.0f);
            DrawLine(new Vector2(0, fy), new Vector2(sz.X, fy), i == 2 ? axisColor : gridColor, 1.0f);
        }

        // 9. 动态全域雷达旋转扫描光束与余晖拖尾 (Radar Sweep Line & Phosphor Glow Trail)
        var center = sz * 0.5f;

        // 余晖拖尾线 (Phosphor Fade Trails) - 严格几何射线内部截断，永不出框
        for (int trail = 3; trail >= 1; trail--)
        {
            float trailAngle = sweepAngle - trail * 0.05f;
            var trailDir = new Vector2(Mathf.Cos(trailAngle), Mathf.Sin(trailAngle));
            var trailEnd = GetRayRectIntersection(center, trailDir, sz, 2.0f);
            float alpha = 0.12f * (4 - trail);
            DrawLine(center, trailEnd, new Color(0.12f, 0.88f, 0.68f, alpha), 1.0f);
        }

        // 主扫描光束 (Main Sweep Beam) - 严格几何射线内部截断，永不出框
        var sweepDir = new Vector2(Mathf.Cos(sweepAngle), Mathf.Sin(sweepAngle));
        var sweepEnd = GetRayRectIntersection(center, sweepDir, sz, 2.0f);
        DrawLine(center, sweepEnd, new Color(0.18f, 1.0f, 0.80f, 0.85f), 1.6f);

        // 10. 实时作战单位与建筑态势光点 (Unit & Building Blips)
        float pulse = (Mathf.Sin(sweepAngle * 3f) + 1f) * 0.5f;

        foreach (var blip in blips)
        {
            var pt = new Vector2(blip.NormPos.X * sz.X, blip.NormPos.Y * sz.Y);
            if (pt.X < 2f || pt.X > sz.X - 2f || pt.Y < 2f || pt.Y > sz.Y - 2f)
                continue;

            if (blip.IsPlayer)
            {
                switch (blip.Kind)
                {
                    case BlipKind.MainBase:
                        // 友军主基地：大号金色战术菱盾
                        var mbCol = new Color(1.0f, 0.88f, 0.20f, 1.0f);
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -6f),
                            pt + new Vector2(6f, 0),
                            pt + new Vector2(0, 6f),
                            pt + new Vector2(-6f, 0)
                        }, mbCol);
                        DrawArc(pt, 7.5f, 0, Mathf.Tau, 8, new Color(0.2f, 0.9f, 1.0f, 0.8f), 1.2f);
                        break;

                    case BlipKind.Building:
                        // 友军建筑：实心金色菱形
                        var bCol = new Color(1.0f, 0.84f, 0.0f, 0.95f);
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -4.5f),
                            pt + new Vector2(4.5f, 0),
                            pt + new Vector2(0, 4.5f),
                            pt + new Vector2(-4.5f, 0)
                        }, bCol);
                        break;

                    case BlipKind.AirUnit:
                        // 友军战机：青蓝发光倒三角/尖头
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -4.5f),
                            pt + new Vector2(3.8f, 3.5f),
                            pt + new Vector2(-3.8f, 3.5f)
                        }, new Color(0.2f, 0.95f, 1.0f, 1.0f));
                        break;

                    case BlipKind.HeavyUnit:
                        // 友军重型/航母/采矿车：稳重菱形
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -3.8f),
                            pt + new Vector2(3.8f, 0),
                            pt + new Vector2(0, 3.8f),
                            pt + new Vector2(-3.8f, 0)
                        }, new Color(0.25f, 0.95f, 0.85f, 1.0f));
                        DrawArc(pt, 4.8f, 0, Mathf.Tau, 6, new Color(0.2f, 0.9f, 1.0f, 0.6f), 1.0f);
                        break;

                    default:
                        // 友军普通地面单位：青蓝微点与发光圆环
                        DrawCircle(pt, 3.0f, new Color(0.18f, 0.88f, 1.0f, 1.0f));
                        DrawArc(pt, 4.2f, 0, Mathf.Tau, 6, new Color(0.18f, 0.88f, 1.0f, 0.45f), 1.0f);
                        break;
                }
            }
            else
            {
                // 敌军：醒目炽烈红与动态呼吸警戒晕光
                switch (blip.Kind)
                {
                    case BlipKind.MainBase:
                        // 敌军主基地：大号猩红菱形与闪烁警告环
                        var embCol = new Color(1.0f, 0.18f, 0.14f, 1.0f);
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -6.5f),
                            pt + new Vector2(6.5f, 0),
                            pt + new Vector2(0, 6.5f),
                            pt + new Vector2(-6.5f, 0)
                        }, embCol);
                        DrawArc(pt, 8.5f, 0, Mathf.Tau, 8, new Color(1.0f, 0.45f, 0.1f, 0.6f + 0.4f * pulse), 1.5f);
                        break;

                    case BlipKind.Building:
                        if (blip.IsGhost)
                        {
                            // 曾探索过的敌军建筑幽灵印记：半透明暗红框
                            DrawPolyline(new Vector2[]
                            {
                                pt + new Vector2(0, -4f),
                                pt + new Vector2(4f, 0),
                                pt + new Vector2(0, 4f),
                                pt + new Vector2(-4f, 0),
                                pt + new Vector2(0, -4f)
                            }, new Color(0.85f, 0.35f, 0.30f, 0.45f), 1.0f);
                        }
                        else
                        {
                            // 视野内敌军建筑：炽红实心菱形与亮橙外框
                            var ebCol = new Color(1.0f, 0.22f, 0.18f, 0.98f);
                            DrawColoredPolygon(new Vector2[]
                            {
                                pt + new Vector2(0, -4.8f),
                                pt + new Vector2(4.8f, 0),
                                pt + new Vector2(0, 4.8f),
                                pt + new Vector2(-4.8f, 0)
                            }, ebCol);
                            DrawArc(pt, 6.0f, 0, Mathf.Tau, 6, new Color(1.0f, 0.55f, 0.10f, 0.75f), 1.0f);
                        }
                        break;

                    case BlipKind.AirUnit:
                        // 敌军战机：深红倒三角
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -4.5f),
                            pt + new Vector2(4.0f, 3.5f),
                            pt + new Vector2(-4.0f, 3.5f)
                        }, new Color(1.0f, 0.20f, 0.20f, 1.0f));
                        DrawArc(pt, 5.5f, 0, Mathf.Tau, 6, new Color(1.0f, 0.4f, 0.2f, 0.4f + 0.3f * pulse), 1.0f);
                        break;

                    case BlipKind.HeavyUnit:
                        // 敌方重型单位/采矿车：红底橙框方钻
                        DrawColoredPolygon(new Vector2[]
                        {
                            pt + new Vector2(0, -4.0f),
                            pt + new Vector2(4.0f, 0),
                            pt + new Vector2(0, 4.0f),
                            pt + new Vector2(-4.0f, 0)
                        }, new Color(1.0f, 0.15f, 0.15f, 1.0f));
                        DrawArc(pt, 5.2f, 0, Mathf.Tau, 8, new Color(1.0f, 0.5f, 0.1f, 0.5f + 0.3f * pulse), 1.2f);
                        break;

                    default:
                        // 敌军普通作战单位：高亮赤红圆点与鲜艳呼吸警戒外光圈
                        DrawCircle(pt, 3.2f, new Color(1.0f, 0.22f, 0.20f, 1.0f));
                        DrawArc(pt, 4.6f + pulse * 1.0f, 0, Mathf.Tau, 8, new Color(1.0f, 0.35f, 0.15f, 0.55f + 0.35f * pulse), 1.2f);
                        break;
                }
            }
        }

        // 10.5 动态战术警报雷达脉冲环与蜂鸣光标 (Tactical Alert Pings & Alarm Shockwaves)
        for (int i = 0; i < activePings.Count; i++)
        {
            var ping = activePings[i];
            var pt = new Vector2(ping.NormPos.X * sz.X, ping.NormPos.Y * sz.Y);
            pt.X = Mathf.Clamp(pt.X, 6f, sz.X - 6f);
            pt.Y = Mathf.Clamp(pt.Y, 6f, sz.Y - 6f);

            float progress = 1.0f - Mathf.Clamp(ping.Lifetime / ping.MaxLifetime, 0f, 1f);
            Color pingColor = ping.Type switch
            {
                MinimapPingType.UnderAttack => new Color(1.0f, 0.15f, 0.12f),
                MinimapPingType.EnemySpotted => new Color(1.0f, 0.50f, 0.05f),
                _ => new Color(0.20f, 0.90f, 1.0f)
            };

            // 3 层连续外扩雷达冲击波 (Expanding Shockwave Rings)
            for (int wave = 0; wave < 3; wave++)
            {
                float wavePhase = Mathf.PosMod(progress * 3.5f + wave * 0.33f, 1.0f);
                float radius = Mathf.Lerp(4f, 22f, wavePhase);
                float alpha = Mathf.Clamp((1f - wavePhase) * (ping.Lifetime / ping.MaxLifetime), 0f, 1f);
                if (alpha > 0.02f)
                {
                    var waveCol = new Color(pingColor.R, pingColor.G, pingColor.B, alpha * 0.85f);
                    DrawArc(pt, radius, 0, Mathf.Tau, 16, waveCol, 1.8f);
                }
            }

            // 中心高频闪烁警报光标 (Flashing Center Beacon / Icon)
            float flash = (Mathf.Sin((float)Godot.Time.GetTicksMsec() * 0.012f) + 1f) * 0.5f;
            var centerCol = new Color(pingColor.R, pingColor.G, pingColor.B, 0.6f + 0.4f * flash);
            DrawCircle(pt, 4.0f, centerCol);
            DrawArc(pt, 5.5f, 0, Mathf.Tau, 8, Colors.White * (0.6f + 0.4f * flash), 1.2f);
        }

        // 11. 3D 摄像机地面视锥梯形边界投影 (Camera Viewport Frustum Box)
        if (hasValidFrustum)
        {
            // 视口半透明微光填充 (Zero Allocation)
            DrawColoredPolygon(frustumPoly4, new Color(0.1f, 0.85f, 1.0f, 0.10f));
            // 视口霓虹青绿高光边框
            DrawPolyline(frustumPoints, new Color(0.15f, 0.95f, 1.0f, 0.85f), 1.6f);
        }

        // 12. 工业金属风格装饰外边框 (MetalUiStyle Outer Frame) - 缩进 1px 避免与边缘裁剪重叠
        DrawRect(new Rect2(1f, 1f, sz.X - 2f, sz.Y - 2f), new Color(0.42f, 0.52f, 0.62f, 0.85f), false, 1.5f);
        
        // 四角军事战术加固切角 (向内缩进 1px，避免边缘溢出或裁剪锯齿)
        float cLen = 7f;
        float pad = 1f;
        var cornerCol = new Color(1.0f, 0.84f, 0.0f, 0.95f);
        DrawLine(new Vector2(pad, cLen), new Vector2(pad, pad), cornerCol, 2.0f);
        DrawLine(new Vector2(pad, pad), new Vector2(cLen, pad), cornerCol, 2.0f);
        DrawLine(new Vector2(sz.X - cLen, pad), new Vector2(sz.X - pad, pad), cornerCol, 2.0f);
        DrawLine(new Vector2(sz.X - pad, pad), new Vector2(sz.X - pad, cLen), cornerCol, 2.0f);
        DrawLine(new Vector2(sz.X - pad, sz.Y - cLen), new Vector2(sz.X - pad, sz.Y - pad), cornerCol, 2.0f);
        DrawLine(new Vector2(sz.X - cLen, sz.Y - pad), new Vector2(sz.X - pad, sz.Y - pad), cornerCol, 2.0f);
        DrawLine(new Vector2(pad, sz.Y - cLen), new Vector2(pad, sz.Y - pad), cornerCol, 2.0f);
        DrawLine(new Vector2(pad, sz.Y - pad), new Vector2(cLen, sz.Y - pad), cornerCol, 2.0f);
    }

    /// <summary>
    /// 计算从中心点向指定方向发射的射线与雷达矩形边界内部的交点，确保扫描线与拖尾永不出框
    /// </summary>
    private static Vector2 GetRayRectIntersection(Vector2 center, Vector2 dir, Vector2 sz, float margin = 2.0f)
    {
        float minX = margin;
        float maxX = sz.X - margin;
        float minY = margin;
        float maxY = sz.Y - margin;

        float tX = float.MaxValue;
        if (dir.X > 1e-5f)
        {
            tX = (maxX - center.X) / dir.X;
        }
        else if (dir.X < -1e-5f)
        {
            tX = (minX - center.X) / dir.X;
        }

        float tY = float.MaxValue;
        if (dir.Y > 1e-5f)
        {
            tY = (maxY - center.Y) / dir.Y;
        }
        else if (dir.Y < -1e-5f)
        {
            tY = (minY - center.Y) / dir.Y;
        }

        float t = Mathf.Min(tX, tY);
        if (float.IsInfinity(t) || float.IsNaN(t) || t < 0f)
        {
            return center;
        }

        var end = center + dir * t;
        end.X = Mathf.Clamp(end.X, minX, maxX);
        end.Y = Mathf.Clamp(end.Y, minY, maxY);
        return end;
    }
}
