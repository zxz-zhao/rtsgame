using Godot;
using System;

[Tool]
public partial class BattleMapRenderer : Node3D
{
	const float GroundHeight = -0.05f;
	const float PatchHeight = 0.02f;
	const float WaterModelHeight = 0.06f;
	const float WaterSurfaceHeight = 0.10f;
	const float RoadHeight = 0.18f;
	const string WaterFlowShaderPath = "res://assets/shaders/water_flow.gdshader";
	const string NatureKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/NatureKit/Models/FBX format/";
	const string MilitaryExtractedRoot = "res://assets/unity_migrated/Assets/External/MilitaryModels/Kenney/Extracted/Models/FBX format/";
	const string SurvivalKitRoot = "res://assets/unity_migrated/Assets/External/MilitaryModels/Kenney/Extracted/SurvivalKit/Models/FBX format/";
	const string CityIndustrialRoot = "res://assets/unity_migrated/Assets/External/Downloads/city-industrial/Models/FBX format/";
	const string RetroUrbanRoot = "res://assets/unity_migrated/Assets/External/Kenney/RetroUrbanKit/Models/FBX format/";
	const string BlasterKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/BlasterKit/Models/FBX format/";
	const string CarKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/CarKit/Models/FBX format/";
	const string CastleKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/CastleKit/Models/FBX format/";
	const string TrainKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/TrainKit/Models/FBX format/";
	const string RiverScenePath = "res://assets/unity_migrated/Assets/External/Kenney/NatureKit/Models/FBX format/ground_riverStraight.fbx";
	const string SnowRiverScenePath = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/snow-tile-river-straight.fbx";
	const string RuinWallScenePath = MilitaryExtractedRoot + "castle-wall.fbx";
	const string KenneyMilitaryRoot = "res://assets/props/kenney/military/";
	const string KenneyNatureRoot = "res://assets/props/kenney/nature/";

	[Export] public string PreviewMapName { get; set; } = BattleMapCatalog.DefaultMapName;
	[Export(PropertyHint.File, "*.tres,*.res")] public string AuthoredMapResourcePath { get; set; } = string.Empty;
	[Export] public bool ShowSpawnMarkers { get; set; } = false;
	[Export] public bool TintImportedAssets { get; set; } = true;
	[Export] public bool LivePreviewInEditor { get; set; } = true;
	[Export] public NodePath PlayerBasePath { get; set; } = new("");
	[Export] public NodePath EnemyBasePath { get; set; } = new("");
	[Export] public NodePath PlayerUnitPath { get; set; } = new("");
	[Export] public NodePath EnemyUnitPath { get; set; } = new("");
	[Export] public NodePath SunPath { get; set; } = new("");
	[Export] public NodePath CameraPath { get; set; } = new("");

	public static BattleMapRenderer? Instance { get; private set; }
	BattleMapDefinition? currentBuiltMap;
	public BattleMapDefinition? CurrentBuiltMap => currentBuiltMap ?? ResolveMap();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	Node3D? generatedRoot;
	BattleMapResource? authoredMapResourceCache;
	string authoredMapResourceCachePath = string.Empty;

	struct PolyHavenAssetMaterials
	{
		public Material Trunk;
		public Material Foliage;
		public Material? Branch;
	}

	static readonly System.Collections.Generic.Dictionary<string, Texture2D> combinedTextureCache = new();
	static readonly System.Collections.Generic.Dictionary<string, PolyHavenAssetMaterials> polyHavenMaterialCache = new();
	string lastPreviewSignature = string.Empty;
	double livePreviewCooldown;
	/// <summary>所有已放置的树木节点，用于战斗时的淡化效果。</summary>
	readonly System.Collections.Generic.List<Node3D> treeNodes = new();
	/// <summary>所有树木的地面位置（XZ），用于判断单位是否在树林内。</summary>
	readonly System.Collections.Generic.List<Vector3> treePositions = new();

	readonly string[] temperateTreeScenes =
	{
		NatureKitRoot + "tree_detailed.fbx",
		NatureKitRoot + "tree_oak.fbx",
		NatureKitRoot + "tree_default.fbx",
		NatureKitRoot + "tree_tall.fbx",
		NatureKitRoot + "tree_pineDefaultA.fbx"
	};
	readonly string[] jungleTreeScenes =
	{
		NatureKitRoot + "tree_detailed_dark.fbx",
		NatureKitRoot + "tree_tall_dark.fbx",
		NatureKitRoot + "tree_default_dark.fbx",
		NatureKitRoot + "tree_palmDetailedTall.fbx",
		NatureKitRoot + "tree_palmBend.fbx",
		NatureKitRoot + "tree_palmTall.fbx"
	};
	readonly string[] snowTreeScenes =
	{
		NatureKitRoot + "tree_pineTallA_detailed.fbx",
		NatureKitRoot + "tree_pineTallB_detailed.fbx",
		NatureKitRoot + "tree_pineTallC_detailed.fbx",
		NatureKitRoot + "tree_pineTallD_detailed.fbx",
		NatureKitRoot + "tree_pineRoundA.fbx",
		NatureKitRoot + "tree_pineRoundC.fbx",
		NatureKitRoot + "tree_pineSmallA.fbx",
		NatureKitRoot + "tree_pineSmallC.fbx"
	};
	readonly string[] islandTreeScenes =
	{
		NatureKitRoot + "tree_palmDetailedTall.fbx",
		NatureKitRoot + "tree_palmTall.fbx",
		NatureKitRoot + "tree_palmBend.fbx",
		NatureKitRoot + "tree_palmDetailedShort.fbx",
		NatureKitRoot + "tree_palmShort.fbx"
	};
	readonly string[] rockScenes =
	{
		NatureKitRoot + "rock_largeA.fbx",
		NatureKitRoot + "rock_largeB.fbx",
		NatureKitRoot + "rock_largeC.fbx",
		NatureKitRoot + "rock_tallA.fbx",
		NatureKitRoot + "rock_tallB.fbx",
		NatureKitRoot + "rock_smallFlatA.fbx",
		SurvivalKitRoot + "rock-a.fbx",
		SurvivalKitRoot + "rock-b.fbx",
		SurvivalKitRoot + "rock-flat-grass.fbx"
	};
	readonly string[] bushScenes =
	{
		NatureKitRoot + "plant_bush.fbx",
		NatureKitRoot + "plant_bushDetailed.fbx",
		NatureKitRoot + "plant_bushLarge.fbx"
	};
	readonly string[] grassScenes =
	{
		NatureKitRoot + "grass.fbx",
		NatureKitRoot + "grass_leafs.fbx",
		NatureKitRoot + "grass_large.fbx"
	};
	readonly (string Path, Vector3 Position, float Yaw, float Height, float Span)[] cityRuinPlacements =
	{
		(CityIndustrialRoot + "building-a.fbx", new Vector3(-104f, 0f, 26f), 8f, 22f, 20f),
		(CityIndustrialRoot + "building-d.fbx", new Vector3(-62f, 0f, 86f), -4f, 18f, 16f),
		(CityIndustrialRoot + "building-h.fbx", new Vector3(-18f, 0f, 122f), 12f, 14f, 14f),
		(CityIndustrialRoot + "building-j.fbx", new Vector3(52f, 0f, -82f), -18f, 18f, 18f),
		(CityIndustrialRoot + "building-m.fbx", new Vector3(98f, 0f, -22f), 14f, 21f, 18f),
		(CityIndustrialRoot + "building-q.fbx", new Vector3(18f, 0f, -118f), -6f, 15f, 14f),
		(CityIndustrialRoot + "chimney-medium.fbx", new Vector3(-86f, 0f, -18f), 0f, 17f, 10f),
		(CityIndustrialRoot + "chimney-large.fbx", new Vector3(86f, 0f, 18f), 0f, 20f, 12f)
	};

	public override void _Ready()
	{
		RefreshMap();
	}

	public override void _Process(double delta)
	{
		var scene = GetTree()?.CurrentScene;
		if (scene != null && Engine.IsEditorHint())
		{
			foreach (var prop in scene.FindChildren("*SubmarinePropeller*", "Node3D", true, false).OfType<Node3D>())
			{
				prop.RotateObjectLocal(Vector3.Forward, (float)(24.0 * delta));
			}
		}

		if (!Engine.IsEditorHint() || !LivePreviewInEditor)
			return;

		livePreviewCooldown -= delta;
		if (livePreviewCooldown > 0d)
			return;

		livePreviewCooldown = 0.6d;
		var map = ResolveMap();
		var signature = BuildPreviewSignature(map);
		if (signature == lastPreviewSignature)
			return;

		Build(map);
	}

	public void RefreshMap()
	{
		Build(ResolveMap());
	}

	public void ReloadAuthoredMapResource()
	{
		authoredMapResourceCache = null;
		authoredMapResourceCachePath = string.Empty;
	}

	public void Build(BattleMapDefinition map)
	{
		currentBuiltMap = map;
		generatedRoot?.QueueFree();
		generatedRoot = new Node3D { Name = "GeneratedMap" };
		AddChild(generatedRoot);
		treeNodes.Clear();
		treePositions.Clear();

		ApplyWorldEnvironment(map);
		AddGround(map);
		AddElevatedPlateaus(map);
		AddBridges(map);
		AddWater(map);
		AddUnderwaterFish(map);
		AddStrips(map, map.Roads, map.RoadColor, RoadHeight, "Road");
		AddWaterColliders(map);
		AddPatches(map);
		AddRocks(map);
		AddTrees(map);
		AddGroundCover(map);
		AddRiverDecorations(map);
		AddPatchDecorations(map);
		AddCityStructures(map);
		AddRuinWalls(map);
		AddSandbags(map);
		AddBattlefieldProps(map);
		AddSpawnMarkers(map);
		ApplySpawnPoints(map);
		FrameOpeningCamera(map);
		lastPreviewSignature = BuildPreviewSignature(map);
	}

	void AddShowcaseSubmarine(BattleMapDefinition map)
	{
	}

	void ApplyWorldEnvironment(BattleMapDefinition map)
	{
		var world = new WorldEnvironment { Name = "WorldEnvironment" };

		var skyTop = map.SkyColor.Lightened(0.1f);
		var skyHorizon = map.SkyColor.Lightened(0.35f);
		var groundBottom = map.AmbientGroundColor.Darkened(0.35f);
		var groundHorizon = map.AmbientEquatorColor;

		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Sky,
			Sky = new Sky
			{
				SkyMaterial = new ProceduralSkyMaterial
				{
					SkyTopColor = skyTop,
					SkyHorizonColor = skyHorizon,
					GroundBottomColor = groundBottom,
					GroundHorizonColor = groundHorizon,
					SunAngleMax = 30.0f
				}
			},
			AmbientLightSource = Godot.Environment.AmbientSource.Sky,
			AmbientLightColor = map.AmbientSkyColor,
			AmbientLightEnergy = 0.95f,
			ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,

			// 🌟 开启高清屏幕空间 3D 舰艇水面倒影 (Screen-Space Reflections - SSR) 🌟
			SsrEnabled = true,
			SsrMaxSteps = 128,
			SsrFadeIn = 0.15f,
			SsrFadeOut = 2.0f,
			SsrDepthTolerance = 0.45f,

			// 屏幕空间环境光遮蔽 (SSAO)
			SsaoEnabled = true,
			SsaoRadius = 1.2f,
			SsaoIntensity = 1.8f,

			// 🌟 开启全场景战斗辉光 (Glow / Bloom) 🌟
			// 让高能粒子（枪口火光、穿甲弹自发光、火箭马赫尾焰、爆炸火球、火星破片）产生惊艳的高光溢出外晕
			GlowEnabled = true,
			GlowIntensity = 0.45f,
			GlowStrength = 0.85f,
			GlowBloom = 0.15f,
			GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
			GlowHdrThreshold = 1.35f,
			GlowHdrScale = 1.4f,
			TonemapMode = Godot.Environment.ToneMapper.Aces,

			FogEnabled = map.MapHalfSize <= 250f,
			FogLightColor = map.FogColor,
			FogDensity = 0.00003f
		};
		world.Environment = env;
		generatedRoot!.AddChild(world);

		if (!string.IsNullOrEmpty(SunPath.ToString()) && GetNodeOrNull<DirectionalLight3D>(SunPath) is { } sun)
		{
			sun.LightColor = new Color(1.0f, 0.97f, 0.92f);
			sun.LightEnergy = map.Name == BattleMapCatalog.JungleName ? 1.55f : 1.75f;
			sun.ShadowEnabled = true;
		}
	}

	public float GetTerrainHeightAt(Vector3 pos)
	{
		var map = currentBuiltMap ?? ResolveMap();
		return BattleMapCatalog.GetTerrainHeightAt(map, pos.X, pos.Z, includeBridges: true);
	}

	void AddGround(BattleMapDefinition map)
	{
		var mapSize = ResolveMapSize(map);
		var body = new StaticBody3D { Name = "MapCollision" };
		generatedRoot!.AddChild(body);

		if (map.Waters != null && map.Waters.Length > 0)
		{
			// 生成真正 3D 凹陷开凿的连续地形网格（河道与海槽凹陷下沉，地貌自然衔接！）
			var carvedMesh = BuildCarvedTerrainMesh(map, mapSize);
			generatedRoot.AddChild(carvedMesh);

			// 为 3D 开凿地形生成精确的物理碰撞体
			if (carvedMesh.Mesh is ArrayMesh am)
			{
				try
				{
					var trimeshShape = am.CreateTrimeshShape();
					if (trimeshShape != null)
					{
						body.AddChild(new CollisionShape3D { Shape = trimeshShape });
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[BattleMapRenderer] Fallback to box collision: {ex.Message}");
					body.AddChild(new CollisionShape3D
					{
						Position = new Vector3(0, -0.5f, 0),
						Shape = new BoxShape3D { Size = new Vector3(mapSize, 1f, mapSize) }
					});
				}
			}
			return;
		}

		var shape = new CollisionShape3D
		{
			Position = new Vector3(0, -0.5f, 0),
			Shape = new BoxShape3D { Size = new Vector3(mapSize, 1f, mapSize) }
		};
		body.AddChild(shape);

		var mesh = new MeshInstance3D
		{
			Name = "Ground",
			Position = new Vector3(0f, GroundHeight, 0f),
			Mesh = new PlaneMesh { Size = new Vector2(mapSize, mapSize) },
			MaterialOverride = CreateGroundMaterial(map.GroundColor)
		};
		generatedRoot.AddChild(mesh);
	}

	MeshInstance3D BuildCarvedTerrainMesh(BattleMapDefinition map, float mapSize)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		int gridRes = map.MapHalfSize >= 280f ? 80 : 60;
		float halfSize = mapSize * 0.60f;
		float step = (halfSize * 2.0f) / gridRes;

		float GetHeightAt(float gx, float gz)
		{
			return BattleMapCatalog.GetTerrainHeightAt(map, gx, gz, includeBridges: false);
		}

		Color GetVertexColor(float y)
		{
			if (y <= -1.80f) return new Color(0.68f, 0.62f, 0.48f); // 坑底浅滩细砂
			if (y < -0.30f)
			{
				float t = (-0.30f - y) / 1.50f;
				return map.GroundColor.Lerp(new Color(0.50f, 0.46f, 0.38f), t); // 水岸斜坡砂石
			}
			if (y <= 2.0f) return map.GroundColor; // 平原地表草坪
			if (y <= 7.0f)
			{
				float t = (y - 2.0f) / 5.0f;
				return map.GroundColor.Lerp(map.RockColor.Darkened(0.12f), t); // 高地山腰岩壁
			}
			return map.RockColor.Lightened(0.08f); // 高台绝壁岩峰
		}

		float uvSpan = halfSize * 2.0f;
		for (int z = 0; z < gridRes; z++)
		{
			for (int x = 0; x < gridRes; x++)
			{
				float x0 = -halfSize + x * step;
				float z0 = -halfSize + z * step;
				float x1 = x0 + step;
				float z1 = z0 + step;

				float y00 = GetHeightAt(x0, z0);
				float y10 = GetHeightAt(x1, z0);
				float y01 = GetHeightAt(x0, z1);
				float y11 = GetHeightAt(x1, z1);

				Vector3 v00 = new Vector3(x0, y00, z0);
				Vector3 v10 = new Vector3(x1, y10, z0);
				Vector3 v01 = new Vector3(x0, y01, z1);
				Vector3 v11 = new Vector3(x1, y11, z1);

				Vector2 uv00 = new Vector2((x0 + halfSize) / uvSpan, (z0 + halfSize) / uvSpan);
				Vector2 uv10 = new Vector2((x1 + halfSize) / uvSpan, (z0 + halfSize) / uvSpan);
				Vector2 uv01 = new Vector2((x0 + halfSize) / uvSpan, (z1 + halfSize) / uvSpan);
				Vector2 uv11 = new Vector2((x1 + halfSize) / uvSpan, (z1 + halfSize) / uvSpan);

				// Quad 1 (CCW 正面朝上): v00 -> v01 -> v10
				st.SetColor(GetVertexColor(y00)); st.SetUV(uv00); st.AddVertex(v00);
				st.SetColor(GetVertexColor(y01)); st.SetUV(uv01); st.AddVertex(v01);
				st.SetColor(GetVertexColor(y10)); st.SetUV(uv10); st.AddVertex(v10);

				// Quad 2 (CCW 正面朝上): v01 -> v11 -> v10
				st.SetColor(GetVertexColor(y01)); st.SetUV(uv01); st.AddVertex(v01);
				st.SetColor(GetVertexColor(y11)); st.SetUV(uv11); st.AddVertex(v11);
				st.SetColor(GetVertexColor(y10)); st.SetUV(uv10); st.AddVertex(v10);
			}
		}

		st.GenerateNormals();
		st.GenerateTangents();
		st.Index();
		var arrayMesh = st.Commit();

		var carvedMat = CreateGroundMaterial(map.GroundColor);
		carvedMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

		return new MeshInstance3D
		{
			Name = "CarvedTerrainMesh",
			Mesh = arrayMesh,
			MaterialOverride = carvedMat
		};
	}

	void AddElevatedPlateaus(BattleMapDefinition map)
	{
		if (map.ElevatedPlateaus == null || map.ElevatedPlateaus.Length == 0)
			return;

		bool isCarvedMap = map.Waters != null && map.Waters.Length > 0;

		for (var i = 0; i < map.ElevatedPlateaus.Length; i++)
		{
			var plateau = map.ElevatedPlateaus[i];
			var root = new Node3D
			{
				Name = $"Plateau_{i:00}_{plateau.Name}",
				Position = plateau.Center,
				Rotation = new Vector3(0f, Mathf.DegToRad(plateau.Angle), 0f)
			};
			generatedRoot!.AddChild(root);

			if (isCarvedMap)
			{
				// 3D 开凿连续网格：高台山体与斜坡已直接由地形网格生成且拥有 Trimesh 碰撞，只添加要塞顶层地表铺装
				if (plateau.StyleIndex > 0)
				{
					var capMat = plateau.StyleIndex switch
					{
						1 => CreateRockCliffMaterial(map.RockColor.Darkened(0.10f)),
						2 => CreateGroundMaterial(map.RoadEdgeColor),
						3 => CreateGroundMaterial(map.GroundColor.Darkened(0.25f)),
						4 => CreateRoadMaterial(map.RoadColor, plateau.Size),
						_ => CreateGroundMaterial(map.PatchAColor)
					};
					var carvedCapMesh = new MeshInstance3D
					{
						Name = "PlateauTopDeck",
						Position = new Vector3(0f, plateau.Height + 0.02f, 0f),
						Mesh = new PlaneMesh { Size = plateau.Size * 0.88f },
						MaterialOverride = capMat
					};
					root.AddChild(carvedCapMesh);
				}
				continue;
			}

			// 1. 实体静态物理碰撞体 (StaticBody3D on Layer 1)
			// 让地面单位、坦克与射线检测 (GetTerrainHeightAt) 精确感知高台表面
			var body = new StaticBody3D
			{
				Name = "PlateauCollision",
				CollisionLayer = 1u,
				CollisionMask = 0u
			};
			root.AddChild(body);
			body.AddChild(new CollisionShape3D
			{
				Position = new Vector3(0f, plateau.Height * 0.5f, 0f),
				Shape = new BoxShape3D
				{
					Size = new Vector3(plateau.Size.X, plateau.Height, plateau.Size.Y)
				}
			});

			// 2. 高台四周陡峭岩壁 Base Box
			var cliffMat = CreateRockCliffMaterial(map.RockColor);
			var baseMesh = new MeshInstance3D
			{
				Name = "PlateauCliffBody",
				Position = new Vector3(0f, plateau.Height * 0.5f, 0f),
				Mesh = new BoxMesh { Size = new Vector3(plateau.Size.X, plateau.Height, plateau.Size.Y) },
				MaterialOverride = cliffMat
			};
			root.AddChild(baseMesh);

			// 3. 高台顶部平坦甲板/草地 (Top Deck Surface)
			var topMat = plateau.StyleIndex switch
			{
				1 => CreateRockCliffMaterial(map.RockColor.Darkened(0.10f)),
				2 => CreateGroundMaterial(map.RoadEdgeColor),
				3 => CreateGroundMaterial(map.GroundColor.Darkened(0.25f)),
				4 => CreateRoadMaterial(map.RoadColor, plateau.Size),
				_ => CreateGroundMaterial(map.PatchAColor)
			};
			var topMesh = new MeshInstance3D
			{
				Name = "PlateauTopDeck",
				Position = new Vector3(0f, plateau.Height + 0.02f, 0f),
				Mesh = new PlaneMesh { Size = plateau.Size },
				MaterialOverride = topMat
			};
			root.AddChild(topMesh);

			// 4. 高台边缘倒角边框 (Beveled Top Edge Border)
			var edgeMat = Material(map.RockColor.Darkened(0.2f), 0.9f);
			var rimNorth = new MeshInstance3D
			{
				Name = "RimNorth",
				Position = new Vector3(0f, plateau.Height + 0.15f, plateau.Size.Y * 0.5f),
				Mesh = new BoxMesh { Size = new Vector3(plateau.Size.X, 0.3f, 0.8f) },
				MaterialOverride = edgeMat
			};
			var rimSouth = new MeshInstance3D
			{
				Name = "RimSouth",
				Position = new Vector3(0f, plateau.Height + 0.15f, -plateau.Size.Y * 0.5f),
				Mesh = new BoxMesh { Size = new Vector3(plateau.Size.X, 0.3f, 0.8f) },
				MaterialOverride = edgeMat
			};
			root.AddChild(rimNorth);
			root.AddChild(rimSouth);

			// 5. 战术上下坡道 (Sloped Access Ramp)，便于战车沿缓坡登上高台
			var rampWidth = Mathf.Min(plateau.Size.X * 0.35f, 22f);
			var rampLength = plateau.Height * 2.8f;
			var rampAngle = Mathf.Atan2(plateau.Height, rampLength);
			var rampHypot = Mathf.Sqrt(plateau.Height * plateau.Height + rampLength * rampLength);

			var rampMesh = new MeshInstance3D
			{
				Name = "AccessRampNorth",
				Position = new Vector3(0f, plateau.Height * 0.5f, plateau.Size.Y * 0.5f + rampLength * 0.5f),
				Rotation = new Vector3(-rampAngle, 0f, 0f),
				Mesh = new BoxMesh { Size = new Vector3(rampWidth, 0.4f, rampHypot) },
				MaterialOverride = CreateRoadMaterial(map.RoadColor, new Vector2(rampWidth, rampLength))
			};
			root.AddChild(rampMesh);

			// 坡道物理碰撞体
			var rampBody = new StaticBody3D
			{
				Name = "RampCollision",
				CollisionLayer = 1u,
				CollisionMask = 0u,
				Position = rampMesh.Position,
				Rotation = rampMesh.Rotation
			};
			root.AddChild(rampBody);
			rampBody.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(rampWidth, 0.4f, rampHypot) }
			});
		}
	}

	void AddBridges(BattleMapDefinition map)
	{
		if (map.Bridges == null || map.Bridges.Length == 0)
			return;

		for (var i = 0; i < map.Bridges.Length; i++)
		{
			var bridge = map.Bridges[i];
			var root = new Node3D
			{
				Name = $"Bridge_{i:00}_{bridge.Name}",
				Position = bridge.Center,
				Rotation = new Vector3(0f, Mathf.DegToRad(bridge.Angle), 0f)
			};
			generatedRoot!.AddChild(root);

			// 桥面基准高度 Y = 0.35m，高于常规陆地 (-0.05m) 与水面 (-0.25m)
			const float DeckY = 0.35f;
			const float DeckThickness = 0.45f;

			// 1. 桥面物理碰撞体 (StaticBody3D on Layer 1)
			// 使重型坦克与步兵单位能无阻碍横穿大河与峡谷
			var body = new StaticBody3D
			{
				Name = "BridgeCollision",
				Position = new Vector3(0f, DeckY, 0f),
				CollisionLayer = 1u,
				CollisionMask = 0u
			};
			root.AddChild(body);
			body.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D
				{
					Size = new Vector3(bridge.Width, DeckThickness + 0.1f, bridge.Length)
				}
			});

			// 2. 桥梁材质定制 (根据 bridge.BridgeType)
			Material deckMat;
			Material railMat;
			Material pierMat;
			switch (bridge.BridgeType)
			{
				case "steel":
					deckMat = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.28f, 0.32f), Metallic = 0.92f, Roughness = 0.28f };
					railMat = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.72f, 0.18f), Metallic = 0.75f, Roughness = 0.35f };
					pierMat = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.35f, 0.38f), Metallic = 0.65f, Roughness = 0.45f };
					break;
				case "wood":
					deckMat = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.32f, 0.18f), Roughness = 0.82f };
					railMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.24f, 0.14f), Roughness = 0.85f };
					pierMat = new StandardMaterial3D { AlbedoColor = new Color(0.30f, 0.20f, 0.12f), Roughness = 0.90f };
					break;
				case "basalt":
					deckMat = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.18f, 0.18f), Roughness = 0.75f, Metallic = 0.25f };
					railMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.20f, 0.12f), Roughness = 0.70f };
					pierMat = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.15f, 0.15f), Roughness = 0.85f };
					break;
				case "ice":
					deckMat = new StandardMaterial3D { AlbedoColor = new Color(0.80f, 0.92f, 0.98f), Roughness = 0.18f, Metallic = 0.15f };
					railMat = new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.96f, 1.0f), Roughness = 0.25f };
					pierMat = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.80f, 0.92f), Roughness = 0.35f };
					break;
				default: // "stone"
					deckMat = new StandardMaterial3D { AlbedoColor = map.RockColor.Lightened(0.1f), Roughness = 0.85f };
					railMat = new StandardMaterial3D { AlbedoColor = map.RockColor.Darkened(0.15f), Roughness = 0.88f };
					pierMat = new StandardMaterial3D { AlbedoColor = map.RockColor.Darkened(0.25f), Roughness = 0.90f };
					break;
			}

			// 3. 桥面主梁 (Deck Slab)
			var deck = new MeshInstance3D
			{
				Name = "BridgeDeck",
				Position = new Vector3(0f, DeckY, 0f),
				Mesh = new BoxMesh { Size = new Vector3(bridge.Width, DeckThickness, bridge.Length) },
				MaterialOverride = deckMat
			};
			root.AddChild(deck);

			// 4. 桥面行车沥青/石板车道 (Road Overlay on Deck)
			var roadway = new MeshInstance3D
			{
				Name = "BridgeRoadway",
				Position = new Vector3(0f, DeckY + DeckThickness * 0.5f + 0.02f, 0f),
				Mesh = new PlaneMesh { Size = new Vector2(bridge.Width * 0.82f, bridge.Length) },
				MaterialOverride = CreateRoadMaterial(map.RoadColor, new Vector2(bridge.Width, bridge.Length))
			};
			root.AddChild(roadway);

			// 5. 左右防护栏杆 (Left & Right Safety Railings)
			var halfW = bridge.Width * 0.48f;
			var railHeight = 0.85f;
			var railLeft = new MeshInstance3D
			{
				Name = "RailLeft",
				Position = new Vector3(-halfW, DeckY + DeckThickness * 0.5f + railHeight * 0.5f, 0f),
				Mesh = new BoxMesh { Size = new Vector3(0.24f, railHeight, bridge.Length) },
				MaterialOverride = railMat
			};
			var railRight = new MeshInstance3D
			{
				Name = "RailRight",
				Position = new Vector3(halfW, DeckY + DeckThickness * 0.5f + railHeight * 0.5f, 0f),
				Mesh = new BoxMesh { Size = new Vector3(0.24f, railHeight, bridge.Length) },
				MaterialOverride = railMat
			};
			root.AddChild(railLeft);
			root.AddChild(railRight);

			// 6. 下沉式重型桥墩 (Bridge Piers sinking into deep riverbed Y = -6.5m)
			var pierCount = Mathf.Max(2, Mathf.RoundToInt(bridge.Length / 22f));
			var pierStep = bridge.Length / (pierCount + 1);
			for (var p = 1; p <= pierCount; p++)
			{
				var pierZ = -bridge.Length * 0.5f + p * pierStep;
				var pier = new MeshInstance3D
				{
					Name = $"Pier_{p}",
					Position = new Vector3(0f, -3.2f, pierZ),
					Mesh = new BoxMesh { Size = new Vector3(bridge.Width * 0.72f, 7.5f, 2.2f) },
					MaterialOverride = pierMat
				};
				root.AddChild(pier);
			}

			// 7. 两端平滑衔接引桥坡 (Transition Approach Ramps)
			var rampLen = 8.0f;
			var rampAngle = Mathf.Atan2(DeckY - GroundHeight, rampLen);
			var rampHypot = Mathf.Sqrt((DeckY - GroundHeight) * (DeckY - GroundHeight) + rampLen * rampLen);

			var rampSouth = new MeshInstance3D
			{
				Name = "ApproachRampSouth",
				Position = new Vector3(0f, (DeckY + GroundHeight) * 0.5f, -bridge.Length * 0.5f - rampLen * 0.5f),
				Rotation = new Vector3(-rampAngle, 0f, 0f),
				Mesh = new BoxMesh { Size = new Vector3(bridge.Width * 0.95f, 0.3f, rampHypot) },
				MaterialOverride = CreateRoadMaterial(map.RoadColor, new Vector2(bridge.Width, rampLen))
			};
			var rampNorth = new MeshInstance3D
			{
				Name = "ApproachRampNorth",
				Position = new Vector3(0f, (DeckY + GroundHeight) * 0.5f, bridge.Length * 0.5f + rampLen * 0.5f),
				Rotation = new Vector3(rampAngle, 0f, 0f),
				Mesh = new BoxMesh { Size = new Vector3(bridge.Width * 0.95f, 0.3f, rampHypot) },
				MaterialOverride = CreateRoadMaterial(map.RoadColor, new Vector2(bridge.Width, rampLen))
			};
			root.AddChild(rampSouth);
			root.AddChild(rampNorth);
		}
	}

	void AddWaterColliders(BattleMapDefinition map)
	{
		foreach (var strip in map.Waters)
		{
			var body = new StaticBody3D
			{
				Name = $"WaterCollision_{strip.Name}",
				Position = new Vector3(strip.Center.X, 0.9f, strip.Center.Z),
				Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f),
				CollisionLayer = 1u << 1,
				CollisionMask = 0u
			};
			generatedRoot!.AddChild(body);
			body.AddChild(new CollisionShape3D
			{
				Position = Vector3.Zero,
				Shape = new BoxShape3D
				{
					Size = new Vector3(strip.Size.X, 10f, strip.Size.Y)
				}
			});
		}
	}

	MeshInstance3D BuildConformingRoadMesh(BattleMapDefinition map, TerrainStripSpec strip, Color color)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		float halfW = strip.Size.X * 0.5f;
		float halfL = strip.Size.Y * 0.5f;

		// 沿道路长度方向每 3.0m 进行网格细分，确保与起伏地形完全贴合
		int segs = Mathf.Max(6, Mathf.RoundToInt(strip.Size.Y / 3.0f));
		float stepZ = strip.Size.Y / segs;

		var rad = Mathf.DegToRad(strip.Angle);
		float cosA = Mathf.Cos(rad);
		float sinA = Mathf.Sin(rad);

		Vector3 ToWorld(float lx, float lz)
		{
			float wx = strip.Center.X + lx * cosA + lz * sinA;
			float wz = strip.Center.Z - lx * sinA + lz * cosA;
			float wy = BattleMapCatalog.GetTerrainHeightAt(map, wx, wz, includeBridges: true) + 0.035f;
			return new Vector3(wx, wy, wz);
		}

		for (int i = 0; i < segs; i++)
		{
			float lz0 = -halfL + i * stepZ;
			float lz1 = -halfL + (i + 1) * stepZ;

			Vector3 v00 = ToWorld(-halfW, lz0);
			Vector3 v10 = ToWorld(halfW, lz0);
			Vector3 v01 = ToWorld(-halfW, lz1);
			Vector3 v11 = ToWorld(halfW, lz1);

			float vU0 = (float)i / segs * (strip.Size.Y / 8f);
			float vU1 = (float)(i + 1) / segs * (strip.Size.Y / 8f);

			Vector2 uv00 = new Vector2(0f, vU0);
			Vector2 uv10 = new Vector2(1f, vU0);
			Vector2 uv01 = new Vector2(0f, vU1);
			Vector2 uv11 = new Vector2(1f, vU1);

			// Quad 1 (CCW 朝上): v00 -> v01 -> v10
			st.SetUV(uv00); st.AddVertex(v00);
			st.SetUV(uv01); st.AddVertex(v01);
			st.SetUV(uv10); st.AddVertex(v10);

			// Quad 2 (CCW 朝上): v01 -> v11 -> v10
			st.SetUV(uv01); st.AddVertex(v01);
			st.SetUV(uv11); st.AddVertex(v11);
			st.SetUV(uv10); st.AddVertex(v10);
		}

		st.GenerateNormals();
		st.GenerateTangents();
		var arrayMesh = st.Commit();

		var mat = CreateRoadMaterial(color, strip.Size);
		return new MeshInstance3D
		{
			Name = $"Road_{strip.Name}",
			Position = Vector3.Zero,
			Mesh = arrayMesh,
			MaterialOverride = mat
		};
	}

	void AddStrips(BattleMapDefinition map, TerrainStripSpec[] strips, Color color, float height, string prefix)
	{
		for (var i = 0; i < strips.Length; i++)
		{
			var strip = strips[i];

			if (prefix == "Road")
			{
				if (BattleMapCatalog.IsPointInWater(map, strip.Center, 0f))
					continue;

				var roadMesh = BuildConformingRoadMesh(map, strip, color);
				generatedRoot!.AddChild(roadMesh);
				continue;
			}

			Material matOverride = prefix == "Water"
				? CreateWaterMaterial(color, false)
				: Material(color, 0.82f);

			var mesh = new MeshInstance3D
			{
				Name = $"{prefix}_{strip.Name}",
				Position = new Vector3(strip.Center.X, height, strip.Center.Z),
				Rotation = new Vector3(0f, Mathf.DegToRad(strip.Angle), 0f),
				Mesh = new PlaneMesh { Size = strip.Size },
				MaterialOverride = matOverride
			};
			generatedRoot!.AddChild(mesh);
		}
	}

	static Material CreateWaterMaterial(Color color, bool riverLike = false)
	{
		var targetColor = color.A > 0.05f ? color : new Color(0.0f, 0.58f, 0.85f, 0.38f);
		if (targetColor.A > 0.45f)
			targetColor.A = 0.38f;

		var mat = new StandardMaterial3D
		{
			AlbedoColor = targetColor,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.05f,
			Metallic = 0.10f,
			MetallicSpecular = 0.95f,
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
		};

		const string norPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/forest_ground_04/forest_ground_04_nor_gl_1k.png";
		if (ResourceLoader.Exists(norPath))
		{
			mat.NormalEnabled = true;
			mat.NormalTexture = GD.Load<Texture2D>(norPath);
			mat.Uv1Triplanar = true;
			mat.Uv1Scale = new Vector3(0.12f, 0.12f, 0.12f);
		}
		return mat;
	}

	void AddWater(BattleMapDefinition map)
	{
		// 渲染开凿在陆地深坑里的 3D 水面（水面沉在深坑内，绝不上浮覆盖陆地）
		foreach (var strip in map.Waters)
			AddWaterStrip(map, strip);
	}

	void AddWaterStrip(BattleMapDefinition map, TerrainStripSpec strip)
	{
		var riverLike = IsRiverLike(strip);
		var rad = Mathf.DegToRad(strip.Angle);
		var cosA = Mathf.Cos(rad);
		var sinA = Mathf.Sin(rad);
		float waterBankWidth = riverLike ? 12.0f : 18.0f;

		// 1. 3D 广阔深海/海底地壳底面 (Deep Seabed Base at Y = -6.0m)
		var seabed = new MeshInstance3D
		{
			Name = $"Waterbed_{strip.Name}",
			Position = new Vector3(strip.Center.X, -6.0f, strip.Center.Z),
			Rotation = new Vector3(0f, rad, 0f),
			Mesh = new PlaneMesh { Size = new Vector2(strip.Size.X + waterBankWidth * 2.5f, strip.Size.Y + waterBankWidth * 2.5f) },
			MaterialOverride = CreateGroundMaterial(map.GroundColor.Darkened(0.85f))
		};
		generatedRoot!.AddChild(seabed);

		// 2. 在 3D 海槽上融入 3D 湛蓝半透明水面 (Water Surface Y = -0.75m resting naturally inside carved channel!)
		float waterPlaneWidth = strip.Size.X + waterBankWidth * 2.2f;
		float waterPlaneLength = strip.Size.Y + waterBankWidth * 2.2f;
		var surface = new MeshInstance3D
		{
			Name = $"WaterSurface_{strip.Name}",
			Position = new Vector3(strip.Center.X, -0.75f, strip.Center.Z),
			Rotation = new Vector3(0f, rad, 0f),
			Mesh = new PlaneMesh { Size = new Vector2(waterPlaneWidth, waterPlaneLength), SubdivideWidth = 64, SubdivideDepth = 64 },
			MaterialOverride = CreateWaterMaterial(map, riverLike)
		};
		generatedRoot!.AddChild(surface);

		// 4. 两岸散布 3D 河石散件
		float tileLength = 4.0f;
		int count = Mathf.Max(2, Mathf.RoundToInt(strip.Size.Y / tileLength));
		float step = strip.Size.Y / count;
		float startZ = -strip.Size.Y * 0.5f + step * 0.5f;
		AddRiverDecorations(map, strip, count, step, startZ, rad, cosA, sinA);
	}


	void AddUnderwaterFish(BattleMapDefinition map)
	{
		if (map.Waters == null || map.Waters.Length == 0) return;

		var fishRoot = new Node3D { Name = "UnderwaterFishSchool" };
		generatedRoot!.AddChild(fishRoot);

		var fishMat1 = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.55f, 0.08f), // 鲜艳热带小丑鱼橙黄色
			Metallic = 0.40f,
			Roughness = 0.20f
		};

		var fishMat2 = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.98f, 0.92f, 0.15f), // 炫彩热带金黄鱼
			Metallic = 0.50f,
			Roughness = 0.15f
		};

		var fishMat3 = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.12f, 0.90f, 0.98f), // 荧光霓虹青蓝彩鱼
			Metallic = 0.60f,
			Roughness = 0.15f
		};

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(map.RandomSeed + 999);

		// 在深海水域下散布 36 条 3D 热带鱼群，游弋在水面下方 Y = -1.5m ~ -8.5m
		for (int i = 0; i < 36; i++)
		{
			float posX = rng.RandfRange(-32f, 32f);
			float posZ = rng.RandfRange(-65f, 65f);
			float posY = rng.RandfRange(-1.50f, -8.50f);
			float yaw = rng.RandfRange(0f, Mathf.Tau);
			float scale = rng.RandfRange(0.85f, 1.45f);
			var mat = (i % 3) switch
			{
				0 => fishMat1,
				1 => fishMat2,
				_ => fishMat3
			};

			var fishNode = new Node3D
			{
				Name = $"TropicalFish_{i}",
				Position = new Vector3(posX, posY, posZ),
				Rotation = new Vector3(0f, yaw, 0f),
				Scale = new Vector3(scale, scale, scale * 1.35f)
			};
			fishRoot.AddChild(fishNode);

			// 鱼身 3D 梭形 Mesh
			var bodyMesh = new MeshInstance3D
			{
				Name = "FishBody",
				Mesh = new CapsuleMesh { Radius = 0.22f, Height = 0.85f, RadialSegments = 16 },
				Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
				MaterialOverride = mat
			};
			fishNode.AddChild(bodyMesh);

			// 鱼尾 3D 尾鳍 Mesh
			var tailMesh = new MeshInstance3D
			{
				Name = "FishTail",
				Mesh = new PrismMesh { Size = new Vector3(0.08f, 0.42f, 0.45f) },
				Position = new Vector3(0f, 0.0f, 0.48f),
				Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
				MaterialOverride = mat
			};
			fishNode.AddChild(tailMesh);
		}
	}

	void BuildSubmarineModelNode(Node3D subRoot, Color playerColor)
	{
		// 100% 实体不透明重工硬质海军钢板装甲 (Opaque Rugged Navy Steel Armor)
		var hullUpperMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
			AlbedoColor = new Color(0.28f, 0.58f, 0.78f, 1.0f), // 100% 不透明海军蓝灰冷钢
			Metallic = 0.95f,
			Roughness = 0.28f, // 粗糙质感军用钢板，绝非滑溜塑料
			RimEnabled = true,
			Rim = 0.85f
		};

		// 100% 实体不透明水下鲜艳防污红底舱 (Opaque Vibrant Anti-Fouling Red Keel)
		var hullRedKeelMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
			AlbedoColor = new Color(0.88f, 0.20f, 0.16f, 1.0f), // 100% 不透明防污红
			Metallic = 0.82f,
			Roughness = 0.32f,
			RimEnabled = true,
			Rim = 0.75f
		};

		// 100% 实体不透明指挥塔围壳亮蓝灰钢材 (Opaque Navy Sail Tower)
		var sailMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
			AlbedoColor = new Color(0.42f, 0.72f, 0.90f, 1.0f),
			Metallic = 0.98f,
			Roughness = 0.20f
		};

		var brassMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.95f, 0.82f, 0.35f, 1.0f),
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
			Metallic = 0.98f,
			Roughness = 0.10f
		};

		// 100% 实体不透明水下发光声呐指示灯 (100% Opaque Emerald Green LED)
		var sonarLightMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
			AlbedoColor = new Color(0.10f, 1.0f, 0.50f, 1.0f),
			EmissionEnabled = true,
			Emission = new Color(0.15f, 1.0f, 0.55f),
			EmissionEnergyMultiplier = 4.0f
		};

		// 100% 实体不透明不锈钢栏杆材质 (Opaque Railing Metal)
		var railMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
			AlbedoColor = new Color(0.85f, 0.90f, 0.95f, 1.0f),
			Metallic = 0.98f,
			Roughness = 0.10f
		};

		// 1a. 放大 1.8 倍经典海军潜艇主艇体上层蓝灰 (Upper Bright Navy Blue Hull)
		var hullUpperMesh = new MeshInstance3D
		{
			Name = "UpperSubmarineHull",
			Mesh = new CapsuleMesh { Radius = 1.35f, Height = 12.0f, RadialSegments = 32, Rings = 8 },
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
			Position = new Vector3(0f, 0.25f, 0f),
			MaterialOverride = hullUpperMat
		};
		subRoot.AddChild(hullUpperMesh);

		// 1b. 3D 甲板防滑脊条 (3D Deck Panel Ridge Stripe)
		var deckStripe = new MeshInstance3D
		{
			Name = "DeckPanelStripe",
			Mesh = new BoxMesh { Size = new Vector3(0.32f, 0.18f, 9.6f) },
			Position = new Vector3(0f, 1.55f, 0f),
			MaterialOverride = sailMat
		};
		subRoot.AddChild(deckStripe);

		// 1c. 放大 1.8 倍水下鲜艳防污红底舱 (Lower Vibrant Anti-Fouling Red Keel)
		var hullRedKeelMesh = new MeshInstance3D
		{
			Name = "LowerRedKeel",
			Mesh = new CapsuleMesh { Radius = 1.30f, Height = 11.6f, RadialSegments = 32, Rings = 8 },
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
			Position = new Vector3(0f, -0.25f, 0f),
			MaterialOverride = hullRedKeelMat
		};
		subRoot.AddChild(hullRedKeelMesh);

		// 2. 指挥塔围壳主台座 (Navy Conning Tower Sail Base)
		var sailMesh = new MeshInstance3D
		{
			Name = "SailTowerBase",
			Mesh = new BoxMesh { Size = new Vector3(1.10f, 0.95f, 2.80f) },
			Position = new Vector3(0f, 0.85f, -1.0f),
			MaterialOverride = sailMat
		};
		subRoot.AddChild(sailMesh);

		// 2b. ★ 3D 顶层观望台/瞭望观景甲板 (Recessed Conning Tower Observation Platform Deck) ★
		var sailDeck = new MeshInstance3D
		{
			Name = "SailObservationDeck",
			Mesh = new BoxMesh { Size = new Vector3(0.92f, 0.45f, 2.20f) },
			Position = new Vector3(0f, 1.25f, -1.0f),
			MaterialOverride = hullUpperMat
		};
		subRoot.AddChild(sailDeck);

		// 2c. ★ 观望台四周 3D 不锈钢防护栏杆与扶手 (Observation Deck Safety Railings) ★
		var portRail = new MeshInstance3D
		{
			Name = "PortRailing",
			Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.48f, 2.30f) },
			Position = new Vector3(-0.52f, 1.55f, -1.0f),
			MaterialOverride = railMat
		};
		subRoot.AddChild(portRail);

		var stbdRail = new MeshInstance3D
		{
			Name = "StarboardRailing",
			Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.48f, 2.30f) },
			Position = new Vector3(0.52f, 1.55f, -1.0f),
			MaterialOverride = railMat
		};
		subRoot.AddChild(stbdRail);

		var frontRail = new MeshInstance3D
		{
			Name = "FrontRailing",
			Mesh = new BoxMesh { Size = new Vector3(1.10f, 0.48f, 0.08f) },
			Position = new Vector3(0f, 1.55f, -2.10f),
			MaterialOverride = railMat
		};
		subRoot.AddChild(frontRail);

		// 2d. ★ 观望台上双筒高倍大望远镜 (Lookout High-Power Naval Binocular Telescope) ★
		var teleStand = new MeshInstance3D
		{
			Name = "LookoutTelescopeStand",
			Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 0.50f },
			Position = new Vector3(0f, 1.60f, -1.8f),
			MaterialOverride = railMat
		};
		subRoot.AddChild(teleStand);

		var teleLens = new MeshInstance3D
		{
			Name = "LookoutTelescopeLens",
			Mesh = new BoxMesh { Size = new Vector3(0.38f, 0.16f, 0.35f) },
			Position = new Vector3(0f, 1.88f, -1.8f),
			MaterialOverride = sonarLightMat
		};
		subRoot.AddChild(teleLens);

		// 2e. 艇顶声呐示宽指示灯 (Sail Platform Sonar LED Light)
		var towerLight = new MeshInstance3D
		{
			Name = "TowerSonarLight",
			Mesh = new SphereMesh { Radius = 0.28f, Height = 0.45f },
			Position = new Vector3(0f, 1.75f, -0.1f),
			MaterialOverride = sonarLightMat
		};
		subRoot.AddChild(towerLight);

		// 4a. 围壳 3D 潜浮舵机械转轴 (Sail Hydroplane Hinge Axle)
		var planeHinge = new MeshInstance3D
		{
			Name = "SailPlaneHinge",
			Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 3.65f },
			Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f),
			Position = new Vector3(0f, 0.75f, -1.0f),
			MaterialOverride = brassMat
		};
		subRoot.AddChild(planeHinge);

		// 4b. 围壳 3D 水平潜浮舵翼 (Sail Hydroplanes)
		var planesMesh = new MeshInstance3D
		{
			Name = "SailPlanes",
			Mesh = new BoxMesh { Size = new Vector3(3.60f, 0.16f, 0.85f) },
			Position = new Vector3(0f, 0.75f, -1.0f),
			MaterialOverride = hullUpperMat
		};
		subRoot.AddChild(planesMesh);

		// 5. ★ 观望台高耸光电潜望镜塔与镜头 (Optronic Periscope Stem & Lens) ★
		var periStem = new MeshInstance3D
		{
			Name = "PeriscopeStem",
			Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.10f, Height = 0.85f },
			Position = new Vector3(0f, 1.85f, -0.6f),
			MaterialOverride = hullUpperMat
		};
		subRoot.AddChild(periStem);

		var periLens = new MeshInstance3D
		{
			Name = "PeriscopeLens",
			Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.22f, 0.35f) },
			Position = new Vector3(0f, 2.32f, -0.62f),
			MaterialOverride = sonarLightMat
		};
		subRoot.AddChild(periLens);

		// 6. 艇尾十字尾舵 (Stern Rudders - 部分沉于水下)
		var rudderV = new MeshInstance3D
		{
			Name = "RudderV",
			Mesh = new BoxMesh { Size = new Vector3(0.12f, 1.55f, 0.90f) },
			Position = new Vector3(0f, 0.05f, 3.35f),
			MaterialOverride = hullUpperMat
		};
		subRoot.AddChild(rudderV);

		var rudderH = new MeshInstance3D
		{
			Name = "RudderH",
			Mesh = new BoxMesh { Size = new Vector3(1.55f, 0.12f, 0.90f) },
			Position = new Vector3(0f, 0.05f, 3.35f),
			MaterialOverride = hullUpperMat
		};
		subRoot.AddChild(rudderH);

		// 7. 艇尾黄铜推进螺旋桨与 5 叶桨片 (Brass Propeller Hub & 5 Skewed Blades)
		var propRoot = new Node3D
		{
			Name = "SubmarinePropeller",
			Position = new Vector3(0f, 0.05f, 3.75f)
		};
		subRoot.AddChild(propRoot);

		var propHub = new MeshInstance3D
		{
			Name = "PropellerHub",
			Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 0.35f, RadialSegments = 16 },
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
			MaterialOverride = brassMat
		};
		propRoot.AddChild(propHub);

		// 5 叶黄铜大斜角螺旋桨片 (5 Skewed Brass Blades)
		for (int b = 0; b < 5; b++)
		{
			float angle = b * (Mathf.Tau / 5f);
			var blade = new MeshInstance3D
			{
				Name = $"PropellerBlade_{b}",
				Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.92f, 0.06f) },
				Rotation = new Vector3(0f, 0f, angle + 0.35f),
				Position = new Vector3(Mathf.Cos(angle) * 0.32f, Mathf.Sin(angle) * 0.32f, 0f),
				MaterialOverride = brassMat
			};
			propRoot.AddChild(blade);
		}
	}

	void AddRiverDecorations(BattleMapDefinition map, TerrainStripSpec strip, int count, float step, float startZ, float rad, float cosA, float sinA)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(map.RandomSeed + 99);
		var halfW = strip.Size.X * 0.5f;

		for (int i = 0; i < count; i += 3)
		{
			float localZ = startZ + i * step;
			var rockScenePath = PickRockScene(map, i);
			if (!ResourceLoader.Exists(rockScenePath))
				rockScenePath = NatureKitRoot + "rock_smallFlatA.fbx";
			if (!ResourceLoader.Exists(rockScenePath))
				continue;

			var rockScene = GD.Load<PackedScene>(rockScenePath);
			if (rockScene == null)
				continue;

			// 左岸 3D 自然河石（无平铺地面底座）
			var leftLocal = new Vector3(-halfW * rng.RandfRange(0.85f, 1.15f), 0f, localZ);
			var leftPos = strip.Center + new Vector3(leftLocal.X * cosA + leftLocal.Z * sinA, 0f, -leftLocal.X * sinA + leftLocal.Z * cosA);
			leftPos.Y = GetTerrainHeightAt(leftPos) + 0.02f;
			var leftRock = rockScene.Instantiate<Node3D>();
			leftRock.Position = leftPos;
			leftRock.Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f);
			leftRock.Scale = Vector3.One * rng.RandfRange(1.2f, 2.4f);
			generatedRoot!.AddChild(leftRock);

			// 右岸 3D 自然河石（无平铺地面底座）
			var rightLocal = new Vector3(halfW * rng.RandfRange(0.85f, 1.15f), 0f, localZ);
			var rightPos = strip.Center + new Vector3(rightLocal.X * cosA + rightLocal.Z * sinA, 0f, -rightLocal.X * sinA + rightLocal.Z * cosA);
			rightPos.Y = GetTerrainHeightAt(rightPos) + 0.02f;
			var rightRock = rockScene.Instantiate<Node3D>();
			rightRock.Position = rightPos;
			rightRock.Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f);
			rightRock.Scale = Vector3.One * rng.RandfRange(1.2f, 2.4f);
			generatedRoot!.AddChild(rightRock);
		}
	}

	private static void ApplyRiverMaterial(Node node, Material material)
	{
		if (node is MeshInstance3D mesh)
		{
			var name = mesh.Name.ToString().ToLowerInvariant();
			if (name.Contains("water") || name.Contains("liquid"))
			{
				mesh.MaterialOverride = material;
			}
		}
		foreach (var child in node.GetChildren())
		{
			ApplyRiverMaterial(child, material);
		}
	}

	private static void ApplyGroundMaterialToRiverModel(Node node, Color groundColor)
	{
		if (node is MeshInstance3D mesh)
		{
			var name = mesh.Name.ToString().ToLowerInvariant();
			if (!name.Contains("water") && !name.Contains("liquid"))
			{
				mesh.MaterialOverride = CreateGroundMaterial(groundColor);
			}
		}
		foreach (var child in node.GetChildren())
		{
			ApplyGroundMaterialToRiverModel(child, groundColor);
		}
	}

	MeshInstance3D BuildConformingPatchMesh(BattleMapDefinition map, TerrainPatchSpec patch)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		float halfW = patch.Size.X * 0.5f;
		float halfL = patch.Size.Y * 0.5f;

		// 将基地地皮/色块细分为网格，使大面积地皮完全平滑贴合地面起伏
		int resX = Mathf.Clamp(Mathf.RoundToInt(patch.Size.X / 7.0f), 4, 16);
		int resZ = Mathf.Clamp(Mathf.RoundToInt(patch.Size.Y / 7.0f), 4, 16);
		float stepX = patch.Size.X / resX;
		float stepZ = patch.Size.Y / resZ;

		var rad = Mathf.DegToRad(patch.Angle);
		float cosA = Mathf.Cos(rad);
		float sinA = Mathf.Sin(rad);

		Vector3 ToWorld(float lx, float lz)
		{
			float wx = patch.Center.X + lx * cosA + lz * sinA;
			float wz = patch.Center.Z - lx * sinA + lz * cosA;
			float wy = BattleMapCatalog.GetTerrainHeightAt(map, wx, wz, includeBridges: false) + 0.025f;
			return new Vector3(wx, wy, wz);
		}

		for (int z = 0; z < resZ; z++)
		{
			float lz0 = -halfL + z * stepZ;
			float lz1 = -halfL + (z + 1) * stepZ;

			for (int x = 0; x < resX; x++)
			{
				float lx0 = -halfW + x * stepX;
				float lx1 = -halfW + (x + 1) * stepX;

				Vector3 v00 = ToWorld(lx0, lz0);
				Vector3 v10 = ToWorld(lx1, lz0);
				Vector3 v01 = ToWorld(lx0, lz1);
				Vector3 v11 = ToWorld(lx1, lz1);

				Vector2 uv00 = new Vector2((float)x / resX, (float)z / resZ);
				Vector2 uv10 = new Vector2((float)(x + 1) / resX, (float)z / resZ);
				Vector2 uv01 = new Vector2((float)x / resX, (float)(z + 1) / resZ);
				Vector2 uv11 = new Vector2((float)(x + 1) / resX, (float)(z + 1) / resZ);

				// Quad 1: v00 -> v01 -> v10
				st.SetUV(uv00); st.AddVertex(v00);
				st.SetUV(uv01); st.AddVertex(v01);
				st.SetUV(uv10); st.AddVertex(v10);

				// Quad 2: v01 -> v11 -> v10
				st.SetUV(uv01); st.AddVertex(v01);
				st.SetUV(uv11); st.AddVertex(v11);
				st.SetUV(uv10); st.AddVertex(v10);
			}
		}

		st.GenerateNormals();
		st.GenerateTangents();
		var arrayMesh = st.Commit();

		var mat = CreateGroundMaterial(map.GetPatchColor(patch.PaletteIndex));
		return new MeshInstance3D
		{
			Name = $"TerrainPatch_{patch.Name}",
			Position = Vector3.Zero,
			Mesh = arrayMesh,
			MaterialOverride = mat
		};
	}

	void AddPatches(BattleMapDefinition map)
	{
		foreach (var patch in map.Patches)
		{
			var mesh = BuildConformingPatchMesh(map, patch);
			generatedRoot!.AddChild(mesh);
		}
	}

	void AddRocks(BattleMapDefinition map)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(1, map.RandomSeed);
		for (var i = 0; i < map.RockClusters.Length; i++)
		{
			var pos = map.RockClusters[i];
			if (TryAddImportedScenery(
				PickRockScene(map, i),
				$"Rock_{i:00}",
				new Vector3(pos.X, GetTerrainHeightAt(pos) + 0.03f, pos.Z),
				new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
				rng.RandfRange(2.6f, 4.8f),
				rng.RandfRange(3.8f, 7.0f),
				map.RockColor,
				preserveMaterials: true))
			{
				continue;
			}

			var rock = new MeshInstance3D
			{
				Name = $"Rock_{i:00}",
				Position = new Vector3(pos.X, GetTerrainHeightAt(pos) + 0.35f, pos.Z),
				Rotation = new Vector3(rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.1f, 0.1f)),
				Scale = Vector3.One * rng.RandfRange(2.5f, 5.8f),
				Mesh = new SphereMesh { Radius = 1f, Height = 1.1f, RadialSegments = 7, Rings = 4 },
				MaterialOverride = Material(map.RockColor, 0.94f)
			};
			generatedRoot!.AddChild(rock);
		}
	}

	void AddTrees(BattleMapDefinition map)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(2, map.RandomSeed + 31);
		for (var i = 0; i < map.TreePositions.Length; i++)
		{
			var pos = map.TreePositions[i];
			var targetHeight = map.Name switch
			{
				BattleMapCatalog.IceFortressName => rng.RandfRange(6.8f, 9.8f),
				BattleMapCatalog.SeaChartName => rng.RandfRange(6.0f, 8.4f),
				BattleMapCatalog.JungleName => rng.RandfRange(7.2f, 10.6f),
				_ => rng.RandfRange(5.8f, 8.6f)
			};
			var treePos3D = new Vector3(pos.X, GetTerrainHeightAt(new Vector3(pos.X, 0f, pos.Z)), pos.Z);

			// ─── 容器节点（视觉 + 碰撞 + 元数据） ───────────────────────
			var container = new Node3D
			{
				Name = $"TreeGroup_{i:00}",
				Position = treePos3D
			};
			generatedRoot!.AddChild(container);
			treeNodes.Add(container);
			treePositions.Add(treePos3D);

			// ─── 视觉部分：优先 FBX，失败则用程序化几何 ────────────────
			var visualYaw = rng.RandfRange(0f, Mathf.Tau);
			var imported = TryInstanceScene(PickTreeScene(map, i), $"TreeVisual_{i:00}");
			if (imported is not null)
			{
				FitImportedNode(imported, targetHeight, targetHeight * 0.75f);
				imported.Rotation = new Vector3(0f, visualYaw, 0f);
				// FBX 保留原材质（不覆盖），使树木呈现真实纹理
				container.AddChild(imported);
			}
			else
			{
				// ── fallback：程序化几何 ─────────────────────────────────
				var treeVisual = new Node3D
				{
					Name = $"TreeVisual_{i:00}",
					Rotation = new Vector3(0f, visualYaw, 0f),
					Scale = Vector3.One * rng.RandfRange(0.9f, 1.3f)
				};
				container.AddChild(treeVisual);

				var trunkHeight = rng.RandfRange(2.4f, 3.6f);
				var trunkBottomR = rng.RandfRange(0.22f, 0.32f);
				var trunkTopR = trunkBottomR * 0.55f;
				var trunkMat = Material(map.TrunkColor, 0.72f);

				treeVisual.AddChild(new MeshInstance3D
				{
					Name = "TrunkLow",
					Position = new Vector3(rng.RandfRange(-0.04f, 0.04f), trunkHeight * 0.30f, rng.RandfRange(-0.04f, 0.04f)),
					Rotation = new Vector3(rng.RandfRange(-0.04f, 0.04f), 0f, rng.RandfRange(-0.04f, 0.04f)),
					Mesh = new CylinderMesh { TopRadius = trunkBottomR * 0.78f, BottomRadius = trunkBottomR, Height = trunkHeight * 0.60f, RadialSegments = 7 },
					MaterialOverride = trunkMat
				});
				treeVisual.AddChild(new MeshInstance3D
				{
					Name = "TrunkHigh",
					Position = new Vector3(rng.RandfRange(-0.05f, 0.05f), trunkHeight * 0.80f, rng.RandfRange(-0.05f, 0.05f)),
					Rotation = new Vector3(rng.RandfRange(-0.06f, 0.06f), 0f, rng.RandfRange(-0.06f, 0.06f)),
					Mesh = new CylinderMesh { TopRadius = trunkTopR * 0.5f, BottomRadius = trunkBottomR * 0.78f, Height = trunkHeight * 0.44f, RadialSegments = 6 },
					MaterialOverride = trunkMat
				});
				treeVisual.AddChild(new MeshInstance3D
				{
					Name = "RootBulge",
					Position = new Vector3(0f, 0.10f, 0f),
					Mesh = new SphereMesh { Radius = trunkBottomR * 1.55f, Height = 0.30f, RadialSegments = 8, Rings = 3 },
					MaterialOverride = trunkMat
				});

				var crownBaseY = trunkHeight * 0.88f;
				var crownBaseR = rng.RandfRange(1.05f, 1.45f);
				var foliageMat  = Material(map.FoliageColor, 0.90f);
				var foliageDark = Material(map.FoliageColor.Darkened(0.18f), 0.88f);
				var foliageMid  = Material(map.FoliageColor.Darkened(0.08f), 0.89f);

				treeVisual.AddChild(new MeshInstance3D
				{
					Name = "CrownBottom",
					Position = new Vector3(rng.RandfRange(-0.1f, 0.1f), crownBaseY + crownBaseR * 0.30f, rng.RandfRange(-0.1f, 0.1f)),
					Rotation = new Vector3(rng.RandfRange(-0.05f, 0.05f), rng.RandfRange(0f, Mathf.Pi), rng.RandfRange(-0.05f, 0.05f)),
					Mesh = new SphereMesh { Radius = crownBaseR, Height = crownBaseR * 1.15f, RadialSegments = 10, Rings = 5 },
					MaterialOverride = foliageDark
				});
				treeVisual.AddChild(new MeshInstance3D
				{
					Name = "CrownMid",
					Position = new Vector3(rng.RandfRange(-0.08f, 0.08f), crownBaseY + crownBaseR * 0.95f, rng.RandfRange(-0.08f, 0.08f)),
					Rotation = new Vector3(rng.RandfRange(-0.05f, 0.05f), rng.RandfRange(0f, Mathf.Pi), rng.RandfRange(-0.05f, 0.05f)),
					Mesh = new SphereMesh { Radius = crownBaseR * 0.82f, Height = crownBaseR * 1.05f, RadialSegments = 9, Rings = 5 },
					MaterialOverride = foliageMid
				});
				treeVisual.AddChild(new MeshInstance3D
				{
					Name = "CrownTop",
					Position = new Vector3(rng.RandfRange(-0.06f, 0.06f), crownBaseY + crownBaseR * 1.52f, rng.RandfRange(-0.06f, 0.06f)),
					Rotation = new Vector3(rng.RandfRange(-0.04f, 0.04f), rng.RandfRange(0f, Mathf.Pi), rng.RandfRange(-0.04f, 0.04f)),
					Mesh = new SphereMesh { Radius = crownBaseR * 0.55f, Height = crownBaseR * 0.80f, RadialSegments = 8, Rings = 4 },
					MaterialOverride = foliageMat
				});
			}

			// ─── 碰撞体：Layer 3 = forest_zone ─────────────────────────
			// 仅阻挡联盟军地面单位（含此 mask 的单位），反抗军和空军不受影响。
			// CollisionMask = 0 表示此 StaticBody3D 不主动检测任何物体，
			// 只被其他单位的 CollisionMask 包含 Layer 3 时才产生阻挡。
			var forestBody = new StaticBody3D
			{
				Name = "ForestCollider",
				CollisionLayer = RtsUnit.ForestCollisionLayer,
				CollisionMask  = 0u
			};
			forestBody.AddChild(new CollisionShape3D
			{
				Name = "ForestShape",
				Position = new Vector3(0f, 1.5f, 0f),
				Shape = new CylinderShape3D { Radius = 2.5f, Height = 3.0f }
			});
			container.AddChild(forestBody);
		}
	}


	void AddGroundCover(BattleMapDefinition map)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(3, map.RandomSeed + 53);
		var mapHalfSize = BattleMapCatalog.GetMapHalfSize(map);
		var coverCount = map.Name switch
		{
			BattleMapCatalog.JungleName => 54,
			BattleMapCatalog.CityRuinsName => 24,
			BattleMapCatalog.SeaChartName => 42,
			_ => 40
		};
		for (var i = 0; i < coverCount; i++)
		{
			var x = rng.RandfRange(-mapHalfSize + 18f, mapHalfSize - 18f);
			var z = rng.RandfRange(-mapHalfSize + 18f, mapHalfSize - 18f);
			var pos = new Vector3(x, GetTerrainHeightAt(new Vector3(x, 0f, z)), z);

			if (BattleMapCatalog.IsPointInWater(map, pos, 4f))
				continue;
			if (BattleMapCatalog.DistanceToWater(map, pos) < 7f && rng.Randf() < 0.45f)
				continue;

			var useGrass = map.Name != BattleMapCatalog.CityRuinsName && (map.Name == BattleMapCatalog.JungleName || rng.Randf() > 0.35f);
			var scenePath = useGrass ? PickGrassScene(map, i) : PickBushScene(map, i);
			var targetHeight = useGrass
				? rng.RandfRange(0.55f, map.Name == BattleMapCatalog.JungleName ? 1.55f : 1.15f)
				: rng.RandfRange(0.90f, 1.80f);
			var targetSpan = useGrass
				? rng.RandfRange(0.90f, 2.10f)
				: rng.RandfRange(1.20f, 2.60f);

			if (TryAddImportedScenery(
				scenePath,
				$"{(useGrass ? "Grass" : "Bush")}_{i:00}",
				new Vector3(pos.X, pos.Y + 0.02f, pos.Z),
				new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
				targetHeight,
				targetSpan,
				map.FoliageColor,
				preserveMaterials: true))
			{
				continue;
			}

			generatedRoot!.AddChild(new MeshInstance3D
			{
				Name = $"Bush_{i:00}",
				Position = new Vector3(pos.X, pos.Y + 0.05f, pos.Z),
				Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
				Scale = Vector3.One * rng.RandfRange(0.9f, 1.4f),
				Mesh = new SphereMesh { Radius = 0.45f, Height = 0.7f, RadialSegments = 7, Rings = 4 },
				MaterialOverride = Material(map.FoliageColor, 0.92f)
			});
		}
	}

	void AddRuinWalls(BattleMapDefinition map)
	{
		for (var i = 0; i < map.RuinWalls.Length; i++)
		{
			var wall = map.RuinWalls[i];
			for (var s = 0; s < wall.Segments; s++)
			{
				var offset = (s - (wall.Segments - 1) * 0.5f) * 5.5f;
				var local = new Vector3(offset, 1.1f, 0f).Rotated(Vector3.Up, Mathf.DegToRad(wall.Angle));
				var horizontalPos = new Vector3(wall.Center.X, 0f, wall.Center.Z) + new Vector3(local.X, 0f, local.Z);
				var terrainY = GetTerrainHeightAt(horizontalPos);
				var finalPos = new Vector3(horizontalPos.X, terrainY + local.Y, horizontalPos.Z);

				if (TryAddImportedScenery(
					RuinWallScenePath,
					$"RuinWall_{i:00}_{s:00}",
					finalPos,
					new Vector3(0f, Mathf.DegToRad(wall.Angle), 0f),
					2.8f,
					5.4f,
					map.RuinColor,
					preserveMaterials: true))
				{
					continue;
				}

				var segment = new MeshInstance3D
				{
					Name = $"RuinWall_{i:00}_{s:00}",
					Position = finalPos,
					Rotation = new Vector3(0f, Mathf.DegToRad(wall.Angle), 0f),
					Mesh = new BoxMesh { Size = new Vector3(4.8f, 2.2f, 0.8f) },
					MaterialOverride = Material(map.RuinColor, 0.86f)
				};
				generatedRoot!.AddChild(segment);
			}
		}
	}

	void AddCityStructures(BattleMapDefinition map)
	{
		if (map.Name != BattleMapCatalog.CityRuinsName)
			return;

		foreach (var placement in cityRuinPlacements)
		{
			_ = TryAddImportedScenery(
				placement.Path,
				$"CityRuin_{placement.Position.X:0}_{placement.Position.Z:0}",
				new Vector3(placement.Position.X, GetTerrainHeightAt(placement.Position), placement.Position.Z),
				new Vector3(0f, Mathf.DegToRad(placement.Yaw), 0f),
				placement.Height,
				placement.Span,
				map.RuinColor,
				preserveMaterials: true);
		}
	}

	void AddBattlefieldProps(BattleMapDefinition map)
	{
		var playerBase = FindPatchCenter(map, 2, new Vector3(-map.BaseSpawnOffset, 0f, -map.BaseSpawnOffset));
		var enemyBase = FindPatchCenter(map, 3, new Vector3(map.BaseSpawnOffset, 0f, map.BaseSpawnOffset));
		var center = FindPatchCenter(map, 1, Vector3.Zero);

		if (map.Name == BattleMapCatalog.TriLaneWarfareName)
		{
			AddTriLaneBattlefieldProps(map);
			return;
		}

		AddBaseProps(playerBase, false);
		AddBaseProps(enemyBase, true);
		AddBaseStagingProps(playerBase, false);
		AddBaseStagingProps(enemyBase, true);
		AddCentralConflictProps(center);

		if (map.Name == BattleMapCatalog.SeaChartName)
		{
			AddSeaBattlefieldProps(map, center, playerBase, enemyBase);
			return;
		}

		AddAmbientUrbanProps(map, center, playerBase, enemyBase);
	}

	void AddBaseProps(Vector3 baseCenter, bool enemy)
	{
		var side = enemy ? -1f : 1f;
		AddSupplyCrateProp(
			$"SupplyCrate_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(side * 24f, 0f, -side * 12f),
			enemy ? 45f : 225f);
		AddAmmoCrateProp(
			$"AmmoCrate_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(side * 15f, 0f, -side * 24f),
			enemy ? 18f : 198f);
		AddVehicleWreckProp(
			$"VehicleWreck_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(-side * 28f, 0f, side * 18f),
			enemy ? 140f : -40f);
	}

	void AddBaseStagingProps(Vector3 baseCenter, bool enemy)
	{
		var side = enemy ? -1f : 1f;
		var facing = enemy ? 45f : 225f;
		var counterFacing = facing + 180f;

		AddBarrierProp(
			$"BaseBarrierA_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(side * 26f, 0f, side * 8f),
			facing,
			1.18f);
		AddBarrierProp(
			$"BaseBarrierB_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(side * 26f, 0f, -side * 8f),
			counterFacing,
			1.18f);
		AddSandbagWallProp(
			$"BaseSandbagA_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(side * 12f, 0f, side * 26f),
			facing - 90f,
			1.08f);
		AddSandbagWallProp(
			$"BaseSandbagB_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(-side * 12f, 0f, -side * 26f),
			facing + 90f,
			1.08f);
		AddStreetLightProp(
			$"BaseLightA_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(-side * 24f, 0f, side * 22f),
			facing);
		AddStreetLightProp(
			$"BaseLightB_{(enemy ? "Enemy" : "Player")}",
			baseCenter + new Vector3(side * 22f, 0f, -side * 24f),
			counterFacing);
	}

	void AddCentralConflictProps(Vector3 center)
	{
		AddTargetMarkerProp("TargetMarker_West", center + new Vector3(-18f, 0f, 12f), 35f);
		AddTargetMarkerProp("TargetMarker_East", center + new Vector3(18f, 0f, -12f), 215f);
		AddVehicleWreckProp("VehicleWreck_Center", center + new Vector3(0f, 0f, 28f), -18f, 1.25f);
	}

	void AddAmbientUrbanProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
	{
		var axis = enemyBase - playerBase;
		axis.Y = 0f;
		if (axis.LengthSquared() < 0.01f)
			axis = new Vector3(1f, 0f, 1f);

		var axisN = axis.Normalized();
		var perp = new Vector3(-axisN.Z, 0f, axisN.X);
		var halfLen = axis.Length() * 0.35f;
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(1, map.RandomSeed * 31 + 17);

		for (var i = 0; i < 10; i++)
		{
			var t = rng.RandfRange(-1f, 1f);
			var side = rng.RandfRange(-42f, 42f);
			var pos = center + axisN * (t * halfLen) + perp * side;
			var yaw = rng.RandfRange(0f, 360f);
			switch (i % 6)
			{
				case 0:
					AddBarrierProp($"BarrierStrong_{i:00}", pos, yaw, 1.12f);
					break;
				case 1:
					AddSandbagWallProp($"SandbagWall_{i:00}", pos, yaw, 1.08f);
					break;
				case 2:
					AddBrickRubbleProp($"BrickRubble_{i:00}", pos, yaw, 1.08f);
					break;
				case 3:
					AddStreetLightProp($"StreetLight_{i:00}", pos, yaw);
					break;
				case 4:
					AddRuinedTowerProp($"RuinedTower_{i:00}", pos, yaw, 1.02f);
					break;
				default:
					AddDamagedRailProp($"DamagedRail_{i:00}", pos, yaw, 1.06f);
					break;
			}
		}

		if (map.Name != BattleMapCatalog.CityRuinsName)
			return;

		AddBrickRubbleProp("CityRubble_North", center + new Vector3(-42f, 0f, 86f), 28f, 1.22f);
		AddBrickRubbleProp("CityRubble_South", center + new Vector3(46f, 0f, -92f), -18f, 1.18f);
		AddVehicleWreckProp("CityTruckWreck", center + new Vector3(18f, 0f, -48f), 164f, 1.18f);
		AddStreetLightProp("CityLight_North", center + new Vector3(-58f, 0f, 58f), 92f);
		AddStreetLightProp("CityLight_South", center + new Vector3(64f, 0f, -60f), -88f);
	}

	void AddSeaBattlefieldProps(BattleMapDefinition map, Vector3 center, Vector3 playerBase, Vector3 enemyBase)
	{
		var mainShipPos = BattleMapCatalog.ClosestWaterPoint(map, center + new Vector3(-15f, 0f, 65f));
		var westWarship = BattleMapCatalog.ClosestWaterPoint(map, center + new Vector3(-65f, 0f, 25f));
		var eastWarship = BattleMapCatalog.ClosestWaterPoint(map, center + new Vector3(75f, 0f, -30f));
		var playerDock = BattleMapCatalog.ClosestWaterPoint(map, playerBase + new Vector3(-48f, 0f, 62f), 6f);
		var enemyDock = BattleMapCatalog.ClosestWaterPoint(map, enemyBase + new Vector3(48f, 0f, -62f), 6f);

		// Use ONLY real 3D imported artist FBX models from asset kit (No procedurally generated code meshes!)
		TryAddImportedScenery(MilitaryExtractedRoot + "ship-medium.fbx", "SeaShip_North", mainShipPos, new Vector3(0f, Mathf.DegToRad(110f), 0f), 5.2f, 18.0f, new Color(0.38f, 0.44f, 0.50f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "ship-small.fbx", "SeaShip_West", westWarship, new Vector3(0f, Mathf.DegToRad(210f), 0f), 4.2f, 12.0f, new Color(0.35f, 0.40f, 0.46f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "ship-small.fbx", "SeaShip_East", eastWarship, new Vector3(0f, Mathf.DegToRad(25f), 0f), 3.8f, 11.0f, new Color(0.35f, 0.40f, 0.46f), true);

		TryAddImportedScenery(MilitaryExtractedRoot + "structure-platform-dock.fbx", "SeaDock_Player", playerDock + new Vector3(0f, 0f, -3f), new Vector3(0f, Mathf.DegToRad(142f), 0f), 4.2f, 11f, new Color(0.38f, 0.42f, 0.45f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "structure-platform-dock-small.fbx", "SeaDock_Enemy", enemyDock + new Vector3(0f, 0f, 3f), new Vector3(0f, Mathf.DegToRad(-38f), 0f), 3.4f, 8f, new Color(0.38f, 0.42f, 0.45f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "flag-pennant.fbx", "SeaFlag_Player", playerDock + new Vector3(6f, 0f, 10f), new Vector3(0f, Mathf.DegToRad(40f), 0f), 7.5f, 3f, new Color(0.22f, 0.55f, 0.92f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "flag-pennant.fbx", "SeaFlag_Enemy", enemyDock + new Vector3(-6f, 0f, -10f), new Vector3(0f, Mathf.DegToRad(220f), 0f), 7.5f, 3f, new Color(0.85f, 0.22f, 0.22f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "palm-detailed-straight.fbx", "SeaPalm_West", center + new Vector3(-28f, 0f, 118f), Vector3.Zero, 8f, 5f, new Color(0.20f, 0.60f, 0.28f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "palm-detailed-bend.fbx", "SeaPalm_East", center + new Vector3(34f, 0f, -122f), new Vector3(0f, Mathf.Pi, 0f), 7.4f, 5f, new Color(0.20f, 0.60f, 0.28f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "rocks-sand-a.fbx", "SeaRock_West", center + new Vector3(-52f, 0f, 132f), new Vector3(0f, Mathf.DegToRad(32f), 0f), 2.4f, 6f, new Color(0.66f, 0.56f, 0.38f), true);
		TryAddImportedScenery(MilitaryExtractedRoot + "rocks-sand-b.fbx", "SeaRock_East", center + new Vector3(56f, 0f, -136f), new Vector3(0f, Mathf.DegToRad(-26f), 0f), 2.4f, 6f, new Color(0.62f, 0.52f, 0.35f), true);
	}

	void AddSupplyCrateProp(string name, Vector3 position, float yaw)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(BlasterKitRoot + "crate-wide.fbx", Vector3.Zero, 0f, 1.6f, 2.8f, new Color(0.46f, 0.36f, 0.22f), true),
			(BlasterKitRoot + "crate-small.fbx", new Vector3(0.9f, 0.55f, -0.2f), 18f, 0.75f, 1.1f, new Color(0.40f, 0.30f, 0.18f), true));
	}

	void AddAmmoCrateProp(string name, Vector3 position, float yaw)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(BlasterKitRoot + "crate-medium.fbx", Vector3.Zero, 0f, 1.3f, 2.1f, new Color(0.28f, 0.34f, 0.24f), true),
			(BlasterKitRoot + "clip-large.fbx", new Vector3(-0.55f, 0.42f, 0.18f), 35f, 0.45f, 0.55f, new Color(0.28f, 0.30f, 0.32f), true),
			(BlasterKitRoot + "grenade-a.fbx", new Vector3(0.45f, 0.40f, -0.16f), -20f, 0.40f, 0.40f, new Color(0.34f, 0.38f, 0.28f), true));
	}

	void AddVehicleWreckProp(string name, Vector3 position, float yaw, float scale = 1f)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(CarKitRoot + "debris-drivetrain.fbx", Vector3.Zero, 20f, 1.2f * scale, 2.7f * scale, new Color(0.22f, 0.22f, 0.22f), true),
			(CarKitRoot + "debris-bumper.fbx", new Vector3(0.95f * scale, 0f, 0.26f * scale), -28f, 0.55f * scale, 1.0f * scale, new Color(0.38f, 0.40f, 0.42f), true),
			(CarKitRoot + "wheel-truck.fbx", new Vector3(-0.70f * scale, 0.08f * scale, -0.52f * scale), 50f, 0.75f * scale, 0.75f * scale, new Color(0.05f, 0.05f, 0.05f), true),
			(CarKitRoot + "wheel-dark.fbx", new Vector3(0.60f * scale, 0.08f * scale, -0.66f * scale), -20f, 0.55f * scale, 0.55f * scale, new Color(0.05f, 0.05f, 0.05f), true));
	}

	void AddBarrierProp(string name, Vector3 position, float yaw, float scale = 1f)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(RetroUrbanRoot + "detail-barrier-strong-type-a.fbx", Vector3.Zero, 0f, 1.6f * scale, 3.6f * scale, new Color(0.34f, 0.34f, 0.34f), true),
			(RetroUrbanRoot + "detail-barrier-strong-damaged.fbx", new Vector3(2.8f * scale, 0f, 0.22f * scale), 180f, 1.45f * scale, 3.0f * scale, new Color(0.36f, 0.34f, 0.32f), true));
	}

	void AddSandbagWallProp(string name, Vector3 position, float yaw, float scale = 1f)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(RetroUrbanRoot + "detail-barrier-type-a.fbx", Vector3.Zero, 0f, 1.25f * scale, 3.0f * scale, new Color(0.55f, 0.46f, 0.28f), true),
			(RetroUrbanRoot + "detail-barrier-type-b.fbx", new Vector3(2.3f * scale, 0f, 0f), 0f, 1.2f * scale, 2.8f * scale, new Color(0.50f, 0.42f, 0.25f), true));
	}

	void AddBrickRubbleProp(string name, Vector3 position, float yaw, float scale = 1f)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(RetroUrbanRoot + "detail-bricks-type-a.fbx", Vector3.Zero, 15f, 1.15f * scale, 2.5f * scale, new Color(0.45f, 0.30f, 0.22f), true),
			(RetroUrbanRoot + "detail-bricks-type-b.fbx", new Vector3(1.5f * scale, 0f, 0.5f * scale), -25f, 0.95f * scale, 2.0f * scale, new Color(0.42f, 0.28f, 0.20f), true),
			(RetroUrbanRoot + "detail-block.fbx", new Vector3(-0.8f * scale, 0f, 0.9f * scale), 40f, 0.7f * scale, 1.0f * scale, new Color(0.38f, 0.33f, 0.27f), true));
	}

	void AddStreetLightProp(string name, Vector3 position, float yaw)
	{
		AddSimpleProp(RetroUrbanRoot + "detail-light-double.fbx", name, position, yaw, 6.2f, 2.0f, new Color(0.40f, 0.42f, 0.44f));
	}

	void AddRuinedTowerProp(string name, Vector3 position, float yaw, float scale = 1f)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(CastleKitRoot + "siege-tower-demolished.fbx", Vector3.Zero, 15f, 5.8f * scale, 4.2f * scale, new Color(0.42f, 0.32f, 0.24f), true),
			(RetroUrbanRoot + "detail-bricks-type-a.fbx", new Vector3(1.4f * scale, 0f, 0.4f * scale), 40f, 0.95f * scale, 1.9f * scale, new Color(0.45f, 0.30f, 0.22f), true));
	}

	void AddDamagedRailProp(string name, Vector3 position, float yaw, float scale = 1f)
	{
		AddCompoundProp(
			name,
			position,
			yaw,
			(TrainKitRoot + "railroad-damaged-straight.fbx", new Vector3(-2.0f * scale, 0f, 0f), 0f, 0.8f * scale, 4.6f * scale, new Color(0.30f, 0.28f, 0.26f), true),
			(TrainKitRoot + "railroad-damaged-straight-skew-left.fbx", new Vector3(2.0f * scale, 0f, 0f), 0f, 0.8f * scale, 4.6f * scale, new Color(0.30f, 0.28f, 0.26f), true),
			(MilitaryExtractedRoot + "barrel.fbx", new Vector3(0.4f * scale, 0.1f * scale, 1.2f * scale), 90f, 0.75f * scale, 0.9f * scale, new Color(0.34f, 0.30f, 0.28f), true));
	}

	void AddTargetMarkerProp(string name, Vector3 position, float yaw)
	{
		AddSimpleProp(BlasterKitRoot + "target-large.fbx", name, position, yaw, 2.1f, 1.4f, new Color(0.86f, 0.62f, 0.12f));
	}

	void AddSimpleProp(string path, string name, Vector3 position, float yaw, float height, float span, Color tint)
	{
		var groundY = GetTerrainHeightAt(position);
		var snappedPos = new Vector3(position.X, groundY, position.Z);
		_ = TryAddImportedScenery(
			path,
			name,
			snappedPos,
			new Vector3(0f, Mathf.DegToRad(yaw), 0f),
			height,
			span,
			tint,
			preserveMaterials: tint == Colors.White);
	}

	void AddCompoundProp(
		string name,
		Vector3 position,
		float yaw,
		params (string Path, Vector3 LocalPosition, float LocalYaw, float Height, float Span, Color Tint, bool PreserveMaterials)[] parts)
	{
		var groundY = GetTerrainHeightAt(position);
		var snappedPos = new Vector3(position.X, groundY, position.Z);
		var root = new Node3D
		{
			Name = name,
			Position = snappedPos,
			Rotation = new Vector3(0f, Mathf.DegToRad(yaw), 0f)
		};

		var anyAdded = false;
		for (var i = 0; i < parts.Length; i++)
		{
			var part = parts[i];
			var imported = TryInstanceScene(part.Path, $"Part_{i:00}");
			if (imported is null)
				continue;

			FitImportedNode(imported, part.Height, part.Span);
			imported.Position += part.LocalPosition;
			imported.Rotation = new Vector3(0f, Mathf.DegToRad(part.LocalYaw), 0f);
			if (TintImportedAssets && !part.PreserveMaterials)
				TintImported(imported, part.Tint);
			root.AddChild(imported);
			anyAdded = true;
		}

		if (anyAdded)
			generatedRoot!.AddChild(root);
		else
			root.QueueFree();
	}

	void AddSandbags(BattleMapDefinition map)
	{
		foreach (var ring in map.SandbagRings)
		{
			for (var i = 0; i < ring.Count; i++)
			{
				var angle = Mathf.Tau * i / Mathf.Max(1, ring.Count);
				var horizontalPos = ring.Center + new Vector3(Mathf.Cos(angle) * ring.Radius, 0f, Mathf.Sin(angle) * ring.Radius);
				var terrainY = GetTerrainHeightAt(horizontalPos);
				var pos = new Vector3(horizontalPos.X, terrainY + 0.35f, horizontalPos.Z);
				var bag = new MeshInstance3D
				{
					Name = $"Sandbag_{(ring.IsEnemy ? "Enemy" : "Player")}_{i:00}",
					Position = pos,
					Rotation = new Vector3(0f, -angle, 0f),
					Mesh = new BoxMesh { Size = new Vector3(3.8f, 0.7f, 1.2f) },
					MaterialOverride = Material(new Color(0.42f, 0.34f, 0.22f), 0.9f)
				};
				generatedRoot!.AddChild(bag);
			}
		}
	}

	void AddSpawnMarkers(BattleMapDefinition map)
	{
		if (!ShowSpawnMarkers)
			return;

		for (var i = 0; i < map.SpawnPoints.Length; i++)
		{
			var spawn = map.SpawnPoints[i];
			var color = spawn.IsPlayer ? new Color(0.22f, 0.72f, 0.95f) : new Color(0.95f, 0.34f, 0.18f);
			var marker = new Node3D
			{
				Name = $"SpawnMarker_{i:00}_{spawn.Name}",
				Position = spawn.Position,
				Rotation = new Vector3(0f, Mathf.DegToRad(spawn.Yaw), 0f),
			};
			generatedRoot!.AddChild(marker);

			marker.AddChild(new MeshInstance3D
			{
				Name = "BaseDisc",
				Position = new Vector3(0f, 0.08f, 0f),
				Mesh = new CylinderMesh { TopRadius = 1.35f, BottomRadius = 1.35f, Height = 0.16f, RadialSegments = 18 },
				MaterialOverride = Material(color, 0.32f)
			});

			marker.AddChild(new MeshInstance3D
			{
				Name = "ArrowPole",
				Position = new Vector3(0f, 0.80f, 0f),
				Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.45f, RadialSegments = 12 },
				MaterialOverride = Material(color.Lerp(Colors.White, 0.18f), 0.28f)
			});

			marker.AddChild(new MeshInstance3D
			{
				Name = "ArrowHead",
				Position = new Vector3(0f, 1.70f, 1.10f),
				Rotation = new Vector3(Mathf.DegToRad(90f), 0f, 0f),
				Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.40f, Height = 0.95f, RadialSegments = 14 },
				MaterialOverride = Material(color, 0.20f)
			});

			marker.AddChild(new Label3D
			{
				Name = "MarkerLabel",
				Text = spawn.IsPlayer ? $"P{spawn.TeamIndex} {spawn.Name}" : $"E{spawn.TeamIndex} {spawn.Name}",
				Position = new Vector3(0f, 2.40f, 0f),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				Modulate = color.Lerp(Colors.White, 0.10f),
				FontSize = 36,
				OutlineSize = 8,
			});
		}
	}

	void ApplySpawnPoints(BattleMapDefinition map)
	{
		foreach (var spawn in map.SpawnPoints)
		{
			var target = spawn.Name switch
			{
				"PlayerBase" => GetNodeOrNull<Node3D>(PlayerBasePath),
				"PlayerForward" => GetNodeOrNull<Node3D>(PlayerUnitPath),
				"EnemyBase" => GetNodeOrNull<Node3D>(EnemyBasePath),
				"EnemyForward" => GetNodeOrNull<Node3D>(EnemyUnitPath),
				_ => null
			};

			if (target is null) continue;

			var terrainY = GetTerrainHeightAt(spawn.Position);
			target.GlobalPosition = new Vector3(spawn.Position.X, terrainY, spawn.Position.Z);
			target.GlobalRotation = new Vector3(0f, Mathf.DegToRad(spawn.Yaw), 0f);
		}

		// 检查场景中的所有预置单位：若是舰艇/潜艇，自动定位到最近的水域航道中
		var scene = GetTree()?.CurrentScene ?? GetParent();
		if (scene != null && map.Waters != null && map.Waters.Length > 0)
		{
			var navalUnits = scene.FindChildren("*", "CharacterBody3D", true, false)
				.OfType<CharacterBody3D>()
				.Where(u => u is RtsUnit rts && BattleUnitCatalog.IsNavalUnit(rts.UnitKey))
				.ToList();

			for (int i = 0; i < navalUnits.Count; i++)
			{
				var unit = navalUnits[i];
				if (!BattleMapCatalog.IsPointInWater(map, unit.GlobalPosition, 0f))
				{
					var offset = new Vector3((i - (navalUnits.Count - 1) * 0.5f) * 12f, 0f, 0f);
					var waterPos = BattleMapCatalog.ClosestWaterPoint(map, unit.GlobalPosition + offset, 2f);
					var spawnH = unit is RtsUnit rtsUnit ? BattleUnitCatalog.SpawnHeight(rtsUnit.UnitKey) : 0f;
					unit.GlobalPosition = new Vector3(waterPos.X, spawnH, waterPos.Z);
				}
			}
		}
	}

	void FrameOpeningCamera(BattleMapDefinition map)
	{
		if (string.IsNullOrEmpty(CameraPath.ToString()) || GetNodeOrNull<Camera3D>(CameraPath) is not { } camera)
			return;

		camera.Near = 1f;
		camera.Far = 800f; // Prevent ground culling/clipping on larger maps

		var playerBase = FindSpawn(map, "PlayerBase", new Vector3(-map.BaseSpawnOffset, 0f, -map.BaseSpawnOffset));
		if (camera.GetParent() is RtsCamera rtsCam)
		{
			rtsCam.EnsureCameraConfigured();
			rtsCam.JumpTo(playerBase);
		}
		else
		{
			var cameraScale = Mathf.Max(1f, ResolveMapScale(map));
			var focus = playerBase + new Vector3(8f, 1.2f, 8f);
			camera.GlobalPosition = focus + new Vector3(0f, 34f * cameraScale, -44f * cameraScale);
			camera.LookAt(focus, Vector3.Up);
			camera.Fov = Mathf.Lerp(42f, 46f, Mathf.Clamp(cameraScale - 1f, 0f, 1f));
		}
	}

	static float ResolveMapSize(BattleMapDefinition map)
		=> BattleMapCatalog.GetMapHalfSize(map) * 2f;

	static float ResolveMapScale(BattleMapDefinition map)
		=> BattleMapCatalog.GetMapHalfSize(map) / BattleMapCatalog.DefaultMapHalfSize;

	static Vector3 FindSpawn(BattleMapDefinition map, string name, Vector3 fallback)
	{
		foreach (var spawn in map.SpawnPoints)
		{
			if (spawn.Name == name)
				return spawn.Position;
		}
		return fallback;
	}

	static Vector3 FindPatchCenter(BattleMapDefinition map, int paletteIndex, Vector3 fallback)
	{
		foreach (var patch in map.Patches)
		{
			if (patch.PaletteIndex == paletteIndex)
				return patch.Center;
		}
		return fallback;
	}

	string PickTreeScene(BattleMapDefinition map, int index)
	{
		if (map.Name == BattleMapCatalog.SeaChartName)
			return islandTreeScenes[index % islandTreeScenes.Length];
		if (map.Name == BattleMapCatalog.IceFortressName)
			return snowTreeScenes[index % snowTreeScenes.Length];
		if (map.Name == BattleMapCatalog.JungleName)
			return jungleTreeScenes[index % jungleTreeScenes.Length];
		return temperateTreeScenes[index % temperateTreeScenes.Length];
	}

	string PickRockScene(BattleMapDefinition map, int index)
	{
		if (map.Name == BattleMapCatalog.SeaChartName)
		{
			var seaRocks = new[]
			{
				NatureKitRoot + "rock_largeC.fbx",
				NatureKitRoot + "rock_smallFlatA.fbx",
				SurvivalKitRoot + "rock-sand-a.fbx",
				SurvivalKitRoot + "rock-sand-b.fbx",
				SurvivalKitRoot + "rock-sand-c.fbx"
			};
			return seaRocks[index % seaRocks.Length];
		}

		return rockScenes[index % rockScenes.Length];
	}

	string PickBushScene(BattleMapDefinition map, int index)
	{
		if (map.Name == BattleMapCatalog.SeaChartName)
		{
			var islandBushes = new[]
			{
				NatureKitRoot + "plant_bushSmall.fbx",
				NatureKitRoot + "plant_bushTriangle.fbx",
				NatureKitRoot + "plant_bushLargeTriangle.fbx"
			};
			return islandBushes[index % islandBushes.Length];
		}

		return bushScenes[index % bushScenes.Length];
	}

	string PickGrassScene(BattleMapDefinition map, int index)
	{
		if (map.Name == BattleMapCatalog.IceFortressName)
			return index % 2 == 0
				? NatureKitRoot + "grass_leafs.fbx"
				: NatureKitRoot + "grass.fbx";
		if (map.Name == BattleMapCatalog.SeaChartName)
			return index % 2 == 0
				? NatureKitRoot + "grass_leafsLarge.fbx"
				: NatureKitRoot + "grass_large.fbx";
		return grassScenes[index % grassScenes.Length];
	}

	bool TryAddImportedScenery(
		string path,
		string name,
		Vector3 position,
		Vector3 rotation,
		float targetHeight,
		float targetSpan,
		Color tint,
		bool preserveMaterials)
	{
		var imported = TryInstanceScene(path, name);
		if (imported is null)
			return false;

		FitImportedNode(imported, targetHeight, targetSpan);
		imported.Position = position;
		imported.Rotation = rotation;
		if (TintImportedAssets && !preserveMaterials)
			TintImported(imported, tint);

		if (path.Contains("polyhaven"))
		{
			if (path.Contains("tree_small_02"))
				ApplyPolyHavenMaterials(imported, "tree_small_02");
			else if (path.Contains("fir_tree_01"))
				ApplyPolyHavenMaterials(imported, "fir_tree_01");
			else if (path.Contains("shrub_01"))
				ApplyPolyHavenMaterials(imported, "shrub_01");
			else if (path.Contains("fern_02"))
				ApplyPolyHavenMaterials(imported, "fern_02");
			else if (path.Contains("grass_medium_01"))
				ApplyPolyHavenMaterials(imported, "grass_medium_01");
			else if (path.Contains("grass_bermuda_01"))
				ApplyPolyHavenMaterials(imported, "grass_bermuda_01");
		}

		generatedRoot!.AddChild(imported);
		return true;
	}

	static void ApplyPolyHavenMaterials(Node3D node, string assetId)
	{
		if (polyHavenMaterialCache.TryGetValue(assetId, out var cachedMats))
		{
			ApplyMeshMaterials(node, cachedMats.Trunk, cachedMats.Foliage, cachedMats.Branch);
			return;
		}

		string baseDir = $"res://assets/third_party/polyhaven/EnvironmentModels/{assetId}/";
		var trunkMat = new StandardMaterial3D();
		var foliageMat = new StandardMaterial3D();
		Material? branchMat = null;

		if (assetId == "tree_small_02")
		{
			SetMaterialTexture(trunkMat, baseDir + "tree_small_02_diff_1k.jpg", baseDir + "tree_small_02_nor_gl_1k.exr", baseDir + "tree_small_02_rough_1k.exr");
			SetMaterialTexture(foliageMat, baseDir + "tree_small_02_leaves_diff_1k.png", baseDir + "tree_small_02_leaves_nor_gl_1k.png", baseDir + "tree_small_02_leaves_rough_1k.png");
			foliageMat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
			foliageMat.AlphaScissorThreshold = 0.5f;
			foliageMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

			var bMat = new StandardMaterial3D();
			SetMaterialTexture(bMat, baseDir + "tree_small_02_branch_diff_1k.png", baseDir + "tree_small_02_branch_nor_gl_1k.png", baseDir + "tree_small_02_branch_rough_1k.png");
			branchMat = bMat;
		}
		else if (assetId == "fir_tree_01")
		{
			SetMaterialTexture(trunkMat, baseDir + "fir_tree_01_bark_diff_1k.png", baseDir + "fir_tree_01_bark_nor_gl_1k.png", baseDir + "fir_tree_01_bark_rough_1k.png");
			SetMaterialTexture(foliageMat, baseDir + "fir_tree_01_twig_diff_1k.png", baseDir + "fir_tree_01_twig_nor_gl_1k.png", baseDir + "fir_tree_01_twig_rough_1k.png");
			foliageMat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
			foliageMat.AlphaScissorThreshold = 0.5f;
			foliageMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		}
		else if (assetId == "shrub_01")
		{
			if (ResourceLoader.Exists(baseDir + "shrub_01_alpha_1k.png"))
			{
				foliageMat.AlbedoTexture = LoadCombinedAlphaTexture(baseDir + "shrub_01_diff_1k.jpg", baseDir + "shrub_01_alpha_1k.png");
				foliageMat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
				foliageMat.AlphaScissorThreshold = 0.5f;
				foliageMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			}
			else
			{
				SetMaterialTexture(foliageMat, baseDir + "shrub_01_diff_1k.jpg", baseDir + "shrub_01_nor_gl_1k.exr", baseDir + "shrub_01_rough_1k.exr");
			}
			trunkMat = foliageMat;
		}
		else if (assetId == "fern_02")
		{
			if (ResourceLoader.Exists(baseDir + "fern_02_alpha_1k.png"))
			{
				foliageMat.AlbedoTexture = LoadCombinedAlphaTexture(baseDir + "fern_02_diff_1k.jpg", baseDir + "fern_02_alpha_1k.png");
				foliageMat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
				foliageMat.AlphaScissorThreshold = 0.5f;
				foliageMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			}
			else
			{
				SetMaterialTexture(foliageMat, baseDir + "fern_02_diff_1k.jpg", baseDir + "fern_02_nor_gl_1k.exr", baseDir + "fern_02_rough_1k.exr");
			}
			trunkMat = foliageMat;
		}
		else if (assetId == "grass_medium_01")
		{
			if (ResourceLoader.Exists(baseDir + "grass_medium_01_alpha_1k.png"))
			{
				foliageMat.AlbedoTexture = LoadCombinedAlphaTexture(baseDir + "grass_medium_01_diff_1k.jpg", baseDir + "grass_medium_01_alpha_1k.png");
				foliageMat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
				foliageMat.AlphaScissorThreshold = 0.5f;
				foliageMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			}
			else
			{
				SetMaterialTexture(foliageMat, baseDir + "grass_medium_01_diff_1k.jpg", baseDir + "grass_medium_01_nor_gl_1k.exr", baseDir + "grass_medium_01_rough_1k.exr");
			}
			trunkMat = foliageMat;
		}
		else if (assetId == "grass_bermuda_01")
		{
			SetMaterialTexture(foliageMat, baseDir + "grass_bermuda_01_diff_1k.jpg", baseDir + "grass_bermuda_01_nor_gl_1k.exr", baseDir + "grass_bermuda_01_rough_1k.exr");
			foliageMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			trunkMat = foliageMat;
		}

		var newMats = new PolyHavenAssetMaterials
		{
			Trunk = trunkMat,
			Foliage = foliageMat,
			Branch = branchMat
		};
		polyHavenMaterialCache[assetId] = newMats;

		ApplyMeshMaterials(node, trunkMat, foliageMat, branchMat);
	}

	static void SetMaterialTexture(StandardMaterial3D mat, string diff, string nor, string rough)
	{
		if (ResourceLoader.Exists(diff))
			mat.AlbedoTexture = GD.Load<Texture2D>(diff);
		if (ResourceLoader.Exists(nor))
		{
			mat.NormalEnabled = true;
			mat.NormalTexture = GD.Load<Texture2D>(nor);
		}
		if (ResourceLoader.Exists(rough))
			mat.RoughnessTexture = GD.Load<Texture2D>(rough);
	}

	static Texture2D LoadCombinedAlphaTexture(string diffPath, string alphaPath)
	{
		string cacheKey = diffPath + "|" + alphaPath;
		if (combinedTextureCache.TryGetValue(cacheKey, out var cachedTex))
			return cachedTex;

		var diffTex = ResourceLoader.Exists(diffPath) ? GD.Load<Texture2D>(diffPath) : null;
		var alphaTex = ResourceLoader.Exists(alphaPath) ? GD.Load<Texture2D>(alphaPath) : null;
		var diffImg = diffTex?.GetImage();
		var alphaImg = alphaTex?.GetImage();

		if (diffImg is null && ResourceLoader.Exists(diffPath))
		{
			try { diffImg = Image.LoadFromFile(ProjectSettings.GlobalizePath(diffPath)); } catch { }
		}
		if (alphaImg is null && ResourceLoader.Exists(alphaPath))
		{
			try { alphaImg = Image.LoadFromFile(ProjectSettings.GlobalizePath(alphaPath)); } catch { }
		}

		if (diffImg is not null && alphaImg is not null)
		{
			if (diffImg.IsCompressed()) diffImg.Decompress();
			if (alphaImg.IsCompressed()) alphaImg.Decompress();
			diffImg.Convert(Image.Format.Rgba8);
			var size = diffImg.GetSize();
			if (alphaImg.GetSize() != size)
			{
				alphaImg.Resize(size.X, size.Y);
			}
			
			for (var y = 0; y < size.Y; y++)
			{
				for (var x = 0; x < size.X; x++)
				{
					var color = diffImg.GetPixel(x, y);
					var alphaColor = alphaImg.GetPixel(x, y);
					color.A = alphaColor.R;
					diffImg.SetPixel(x, y, color);
				}
			}
			var tex = ImageTexture.CreateFromImage(diffImg);
			combinedTextureCache[cacheKey] = tex;
			return tex;
		}
		var fallbackTex = diffTex ?? (ResourceLoader.Exists(diffPath) ? GD.Load<Texture2D>(diffPath) : null);
		if (fallbackTex is not null)
			combinedTextureCache[cacheKey] = fallbackTex;
		return fallbackTex!;
	}

	static void ApplyMeshMaterials(Node node, Material trunk, Material foliage, Material? branch)
	{
		if (node is MeshInstance3D mesh)
		{
			string nameLower = mesh.Name.ToString().ToLower();
			if (nameLower.Contains("leave") || nameLower.Contains("leaf") || nameLower.Contains("twig") || nameLower.Contains("foliage"))
			{
				mesh.MaterialOverride = foliage;
			}
			else if (nameLower.Contains("branch") && branch is not null)
			{
				mesh.MaterialOverride = branch;
			}
			else
			{
				mesh.MaterialOverride = trunk;
			}
		}

		foreach (var child in node.GetChildren())
		{
			ApplyMeshMaterials(child, trunk, foliage, branch);
		}
	}

	static readonly System.Collections.Generic.Dictionary<string, Resource> resourceCache = new();

	static Node3D? TryInstanceScene(string path, string name)
	{
		if (!resourceCache.TryGetValue(path, out var resource))
		{
			resource = GD.Load<Resource>(path);
			if (resource != null)
				resourceCache[path] = resource;
		}
		Node3D? node = resource switch
		{
			PackedScene packed => packed.Instantiate<Node3D>(),
			Mesh mesh => new MeshInstance3D { Mesh = mesh },
			_ => null
		};
		if (node is not null)
			node.Name = name;
		return node;
	}

	static void FitImportedNode(Node3D node, float targetHeight, float targetSpan)
	{
		if (!TryGetLocalBounds(node, Transform3D.Identity, out var bounds))
			return;

		var size = bounds.Size;
		var heightScale = targetHeight > 0.01f && size.Y > 0.001f
			? targetHeight / size.Y
			: 1f;
		var span = Mathf.Max(size.X, size.Z);
		var spanScale = targetSpan > 0.01f && span > 0.001f
			? targetSpan / span
			: heightScale;
		var scale = targetHeight > 0.01f && targetSpan > 0.01f
			? Mathf.Min(heightScale, spanScale)
			: targetHeight > 0.01f
				? heightScale
				: spanScale;

		var center = bounds.Position + bounds.Size * 0.5f;
		node.Position = new Vector3(-center.X * scale, -bounds.Position.Y * scale, -center.Z * scale);
		node.Scale = Vector3.One * scale;
	}

	static bool TryGetLocalBounds(Node node, Transform3D transform, out Aabb bounds)
	{
		bounds = default;
		var hasBounds = false;

		if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
		{
			bounds = TransformAabb(meshInstance.Mesh.GetAabb(), transform);
			hasBounds = true;
		}

		foreach (var child in node.GetChildren())
		{
			var childTransform = transform;
			if (child is Node3D child3D)
				childTransform = transform * child3D.Transform;

			if (!TryGetLocalBounds(child, childTransform, out var childBounds))
				continue;

			bounds = hasBounds ? bounds.Merge(childBounds) : childBounds;
			hasBounds = true;
		}

		return hasBounds;
	}

	static Aabb TransformAabb(Aabb aabb, Transform3D transform)
	{
		var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
		var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
		var pos = aabb.Position;
		var size = aabb.Size;
		for (var x = 0; x <= 1; x++)
		for (var y = 0; y <= 1; y++)
		for (var z = 0; z <= 1; z++)
		{
			var corner = pos + new Vector3(size.X * x, size.Y * y, size.Z * z);
			var p = transform * corner;
			min = new Vector3(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y), Mathf.Min(min.Z, p.Z));
			max = new Vector3(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y), Mathf.Max(max.Z, p.Z));
		}

		return new Aabb(min, max - min);
	}

	static void TintImported(Node node, Color color)
	{
		foreach (var child in node.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (child is MeshInstance3D mesh)
				mesh.MaterialOverride = Material(color, 0.88f);
		}
		if (node is MeshInstance3D self)
			self.MaterialOverride = Material(color, 0.88f);
	}

	static StandardMaterial3D Material(Color color, float roughness)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
			Metallic = 0f
		};
	}

	static StandardMaterial3D CreateGroundMaterial(Color color)
	{
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = color;
		mat.VertexColorUseAsAlbedo = true;
		mat.Roughness = 0.9f;

		const string diffPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/forest_ground_04/forest_ground_04_diff_1k.jpg";
		const string norPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/forest_ground_04/forest_ground_04_nor_gl_1k.png";
		const string roughPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/forest_ground_04/forest_ground_04_rough_1k.jpg";

		if (ResourceLoader.Exists(diffPath))
		{
			mat.AlbedoTexture = GD.Load<Texture2D>(diffPath);
			mat.Uv1Triplanar = true;
			mat.Uv1Scale = new Vector3(0.08f, 0.08f, 0.08f);
		}
		if (ResourceLoader.Exists(norPath))
		{
			mat.NormalEnabled = true;
			mat.NormalTexture = GD.Load<Texture2D>(norPath);
			mat.Uv1Triplanar = true;
		}
		if (ResourceLoader.Exists(roughPath))
		{
			mat.RoughnessTexture = GD.Load<Texture2D>(roughPath);
			mat.Uv1Triplanar = true;
		}

		return mat;
	}

	static StandardMaterial3D CreateRockCliffMaterial(Color color)
	{
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = color;
		mat.Roughness = 0.92f;

		const string diffPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/rocky_trail/rocky_trail_diff_1k.jpg";
		const string norPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/rocky_trail/rocky_trail_nor_gl_1k.png";
		const string roughPath = "res://assets/unity_migrated/Assets/External/PolyHaven/EnvironmentTextures/rocky_trail/rocky_trail_rough_1k.jpg";

		if (ResourceLoader.Exists(diffPath))
		{
			mat.AlbedoTexture = GD.Load<Texture2D>(diffPath);
			mat.Uv1Triplanar = true;
			mat.Uv1Scale = new Vector3(0.06f, 0.06f, 0.06f);
		}
		if (ResourceLoader.Exists(norPath))
		{
			mat.NormalEnabled = true;
			mat.NormalTexture = GD.Load<Texture2D>(norPath);
			mat.Uv1Triplanar = true;
		}
		if (ResourceLoader.Exists(roughPath))
		{
			mat.RoughnessTexture = GD.Load<Texture2D>(roughPath);
			mat.Uv1Triplanar = true;
		}

		return mat;
	}

	static StandardMaterial3D CreateRoadMaterial(Color color, Vector2 size)
	{
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = color;
		mat.Roughness = 0.85f;

		const string diffPath = "res://assets/unity_migrated/Assets/External/UserModels/Props/WarScene/Tex_Road_D.png";
		if (ResourceLoader.Exists(diffPath))
		{
			mat.AlbedoTexture = GD.Load<Texture2D>(diffPath);
			mat.Uv1Scale = new Vector3(1f, size.Y / 8f, 1f);
		}

		return mat;
	}

	static bool IsRiverLike(TerrainStripSpec strip)
	{
		return Mathf.Min(strip.Size.X, strip.Size.Y) <= 28f;
	}

	static string PickWaterScene(BattleMapDefinition map)
	{
		return map.Name == BattleMapCatalog.IceFortressName
			? SnowRiverScenePath
			: RiverScenePath;
	}

	static Material CreateWaterMaterial(BattleMapDefinition map, bool riverLike)
	{
		var shader = GD.Load<Shader>(WaterFlowShaderPath);
		if (shader is null)
			return Material(map.WaterColor, riverLike ? 0.45f : 0.3f);

		var material = new ShaderMaterial
		{
			Shader = shader
		};

		Color waterColor;
		Color shallowColor;
		Color foamColor;

		if (map.Name == BattleMapCatalog.SeaChartName)
		{
			// 海图群岛：清澈热带碧玉翠绿海洋 (Tropical Emerald Ocean)
			waterColor = new Color(0.03f, 0.42f, 0.24f, 0.95f);       // 深海碧绿/深竹青
			shallowColor = new Color(0.08f, 0.70f, 0.46f, 0.85f);     // 近岸浅海清透翡翠绿
			foamColor = new Color(0.88f, 0.98f, 0.94f, 0.90f);        // 纯白浪花带微翠
		}
		else if (map.Name == BattleMapCatalog.IceFortressName)
		{
			// 冰雪要塞：冰川寒蓝水体
			waterColor = new Color(0.12f, 0.35f, 0.58f, 0.92f);
			shallowColor = new Color(0.45f, 0.78f, 0.92f, 0.85f);
			foamColor = new Color(0.92f, 0.96f, 1.0f, 0.92f);
		}
		else if (map.Name == BattleMapCatalog.VolcanoFortressName)
		{
			// 火山要塞：赤红流淌熔岩
			waterColor = new Color(0.85f, 0.18f, 0.04f, 0.98f);
			shallowColor = new Color(1.0f, 0.45f, 0.10f, 0.92f);
			foamColor = new Color(1.0f, 0.85f, 0.35f, 0.95f);
		}
		else if (map.Name == BattleMapCatalog.TriLaneWarfareName)
		{
			// 三路对抗：深蓝大河
			waterColor = new Color(0.05f, 0.28f, 0.48f, 0.92f);
			shallowColor = new Color(0.18f, 0.55f, 0.75f, 0.85f);
			foamColor = new Color(0.88f, 0.95f, 1.0f, 0.88f);
		}
		else
		{
			// 通用/沙漠绿洲等：以地图配置的 WaterColor 为基准计算深浅梯度
			var baseC = map.WaterColor;
			waterColor = new Color(baseC.R * 0.75f, baseC.G * 0.75f, baseC.B * 0.75f, 0.92f);
			shallowColor = new Color(
				Mathf.Clamp(baseC.R * 1.35f + 0.05f, 0f, 1f),
				Mathf.Clamp(baseC.G * 1.35f + 0.08f, 0f, 1f),
				Mathf.Clamp(baseC.B * 1.35f + 0.10f, 0f, 1f),
				0.85f
			);
			foamColor = new Color(0.90f, 0.96f, 1.0f, 0.90f);
		}

		material.SetShaderParameter("water_color", waterColor);
		material.SetShaderParameter("shallow_color", shallowColor);
		material.SetShaderParameter("foam_color", foamColor);
		material.SetShaderParameter("flow_speed", riverLike ? 0.85f : 0.38f);
		material.SetShaderParameter("wave_scale", riverLike ? 0.48f : 0.28f);
		material.SetShaderParameter("wave_strength", riverLike ? 0.42f : 0.22f);
		material.SetShaderParameter("wave_height", riverLike ? 0.22f : 0.32f);
		material.SetShaderParameter("ripple_scale", riverLike ? 14f : 8.5f);
		material.SetShaderParameter("ripple_strength", 0.14f);
		material.SetShaderParameter("edge_fade", 0.16f);
		material.SetShaderParameter("shine_strength", 0.20f);
		material.SetShaderParameter("foam_scale", 0.35f);
		material.SetShaderParameter("roughness", 0.12f);
		material.SetShaderParameter("metallic", 0.0f);
		material.SetShaderParameter("specular", 0.5f);
		return material;
	}

	void AddTriLaneBattlefieldProps(BattleMapDefinition map)
	{
		// 1. 蓝方 (盟军) 5 大基地群前沿军备设施
		AddBaseProps(new Vector3(-200f, 0f, -200f), false); // P1: 中央总指挥主基地
		AddBaseStagingProps(new Vector3(-200f, 0f, -200f), false);

		AddBaseProps(new Vector3(-270f, 0f, -90f), false);  // P2: 上路装甲突破阵地
		AddSandbagWallProp("PlayerTopSandbag", new Vector3(-255f, 0f, -75f), 45f, 1.25f);
		AddBarrierProp("PlayerTopBarrier", new Vector3(-250f, 0f, -70f), 45f, 1.2f);
		AddSupplyCrateProp("PlayerTopCrate", new Vector3(-285f, 0f, -105f), 30f);

		AddBaseProps(new Vector3(-90f, 0f, -270f), false);  // P3: 下路深水军港
		AddNavalHarborProps(new Vector3(-90f, 0f, -270f), false);

		AddBaseProps(new Vector3(-235f, 0f, -145f), false); // P4: 中左战术空军基地
		AddAmmoCrateProp("PlayerAirfieldAmmo", new Vector3(-225f, 0f, -135f), 30f);
		AddSupplyCrateProp("PlayerAirfieldSupply", new Vector3(-245f, 0f, -155f), 120f);

		AddBaseProps(new Vector3(-145f, 0f, -235f), false); // P5: 中右后勤防空基地
		AddSandbagWallProp("PlayerAaSandbag", new Vector3(-135f, 0f, -225f), -45f, 1.2f);
		AddAmmoCrateProp("PlayerAaAmmo", new Vector3(-155f, 0f, -245f), 70f);

		// 2. 红方 (反抗军) 5 大基地群前沿军备设施
		AddBaseProps(new Vector3(200f, 0f, 200f), true);   // E1: 中央总指挥主基地
		AddBaseStagingProps(new Vector3(200f, 0f, 200f), true);

		AddBaseProps(new Vector3(90f, 0f, 270f), true);    // E2: 上路装甲突破阵地
		AddSandbagWallProp("EnemyTopSandbag", new Vector3(75f, 0f, 255f), -135f, 1.25f);
		AddBarrierProp("EnemyTopBarrier", new Vector3(70f, 0f, 250f), -135f, 1.2f);
		AddSupplyCrateProp("EnemyTopCrate", new Vector3(105f, 0f, 285f), -150f);

		AddBaseProps(new Vector3(270f, 0f, 90f), true);    // E3: 下路深水军港
		AddNavalHarborProps(new Vector3(270f, 0f, 90f), true);

		AddBaseProps(new Vector3(145f, 0f, 235f), true);   // E4: 中左战术空军基地
		AddAmmoCrateProp("EnemyAirfieldAmmo", new Vector3(135f, 0f, 225f), -150f);
		AddSupplyCrateProp("EnemyAirfieldSupply", new Vector3(155f, 0f, 245f), -60f);

		AddBaseProps(new Vector3(235f, 0f, 145f), true);   // E5: 中右后勤防空基地
		AddSandbagWallProp("EnemyAaSandbag", new Vector3(225f, 0f, 135f), 135f, 1.2f);
		AddAmmoCrateProp("EnemyAaAmmo", new Vector3(245f, 0f, 155f), -110f);

		// 3. 中路大干道双侧对称路灯与桥头堡关卡
		for (float d = 45f; d <= 185f; d += 35f)
		{
			var pSw = new Vector3(-d, 0f, -d);
			AddStreetLightProp($"AvenueLight_SW_L_{d:0}", pSw + new Vector3(-14f, 0f, 14f), 45f);
			AddStreetLightProp($"AvenueLight_SW_R_{d:0}", pSw + new Vector3(14f, 0f, -14f), -135f);

			var pNe = new Vector3(d, 0f, d);
			AddStreetLightProp($"AvenueLight_NE_L_{d:0}", pNe + new Vector3(14f, 0f, -14f), -135f);
			AddStreetLightProp($"AvenueLight_NE_R_{d:0}", pNe + new Vector3(-14f, 0f, 14f), 45f);
		}

		// 4. 中央十字要塞岛核心争夺战区 (Central Fortress Island)
		AddCentralFortressProps(Vector3.Zero);

		// 5. 上路高峡险隘与西峡大钢桥前线 (Top Canyon Armored Corridor)
		AddTopCanyonBattlefieldProps();

		// 6. 下路深水三角洲航道海战场与滨海群岛 (Bottom Naval Channel & Delta)
		AddBottomDeltaBattlefieldProps();
	}

	void AddNavalHarborProps(Vector3 baseCenter, bool enemy)
	{
		var side = enemy ? -1f : 1f;
		var dockPos = baseCenter + new Vector3(side * 25f, 0f, -side * 25f);
		AddSimpleProp(KenneyMilitaryRoot + "structure-platform-dock.obj", $"NavalDock_{(enemy ? "Enemy" : "Player")}", dockPos, enemy ? -135f : 45f, 1.8f, 12f, new Color(0.36f, 0.32f, 0.28f));
		AddSimpleProp(KenneyMilitaryRoot + "tower-watch.obj", $"NavalWatchtower_{(enemy ? "Enemy" : "Player")}", baseCenter + new Vector3(side * 18f, 0f, -side * 36f), enemy ? -135f : 45f, 7.5f, 4f, new Color(0.30f, 0.34f, 0.30f));
		AddSimpleProp(KenneyMilitaryRoot + "barrel.obj", $"NavalFuelA_{(enemy ? "Enemy" : "Player")}", dockPos + new Vector3(3f, 0f, 0f), 0f, 1.2f, 0.9f, new Color(0.24f, 0.36f, 0.22f));
		AddSimpleProp(KenneyMilitaryRoot + "barrel.obj", $"NavalFuelB_{(enemy ? "Enemy" : "Player")}", dockPos + new Vector3(4f, 0f, 1.5f), 45f, 1.2f, 0.9f, new Color(0.55f, 0.22f, 0.18f));
		AddSimpleProp(KenneyMilitaryRoot + "crate.obj", $"NavalCrate_{(enemy ? "Enemy" : "Player")}", dockPos + new Vector3(2f, 0f, 3f), 20f, 1.4f, 1.4f, new Color(0.48f, 0.40f, 0.28f));
	}

	void AddCentralFortressProps(Vector3 center)
	{
		const float IslandH = 4.8f;
		var islandCenter = center + new Vector3(0f, IslandH, 0f);

		// 中央核心雷达指挥塔与空投补给
		AddSimpleProp(KenneyMilitaryRoot + "tower-watch.obj", "FortressRadarTower", islandCenter + new Vector3(0f, 0f, 0f), 45f, 10.5f, 5f, new Color(0.28f, 0.32f, 0.34f));
		AddSupplyCrateProp("FortressSupplyA", islandCenter + new Vector3(-8f, 0f, 6f), 30f);
		AddAmmoCrateProp("FortressAmmoA", islandCenter + new Vector3(8f, 0f, -6f), -45f);

		// 桥头堡防线：双桥入口处反坦克石阵与重机枪沙袋环
		AddBarrierProp("FortressNorthBarrierA", islandCenter + new Vector3(-24f, 0f, -24f), 135f, 1.35f);
		AddBarrierProp("FortressNorthBarrierB", islandCenter + new Vector3(-28f, 0f, -18f), 135f, 1.35f);
		AddSandbagWallProp("FortressNorthSandbag", islandCenter + new Vector3(-20f, 0f, -26f), 135f, 1.25f);

		AddBarrierProp("FortressSouthBarrierA", islandCenter + new Vector3(24f, 0f, 24f), -45f, 1.35f);
		AddBarrierProp("FortressSouthBarrierB", islandCenter + new Vector3(28f, 0f, 18f), -45f, 1.35f);
		AddSandbagWallProp("FortressSouthSandbag", islandCenter + new Vector3(20f, 0f, 26f), -45f, 1.25f);

		// 四角防御瞭望台
		AddSimpleProp(KenneyMilitaryRoot + "tower-watch.obj", "FortressCornerNW", islandCenter + new Vector3(-32f, 0f, 32f), -45f, 6.5f, 3.5f, new Color(0.30f, 0.34f, 0.30f));
		AddSimpleProp(KenneyMilitaryRoot + "tower-watch.obj", "FortressCornerSE", islandCenter + new Vector3(32f, 0f, -32f), 135f, 6.5f, 3.5f, new Color(0.30f, 0.34f, 0.30f));
	}

	void AddTopCanyonBattlefieldProps()
	{
		// 上路高峡钢桥 (-115, 0, 115) 附近装甲残骸与反坦克拒马
		var bridgePos = new Vector3(-115f, 0f, 115f);
		AddVehicleWreckProp("CanyonTankWreckA", bridgePos + new Vector3(-26f, 0f, -12f), 65f, 1.4f);
		AddVehicleWreckProp("CanyonTankWreckB", bridgePos + new Vector3(24f, 0f, 14f), -115f, 1.3f);
		AddBarrierProp("CanyonChokeBarrierA", bridgePos + new Vector3(-35f, 0f, 18f), 45f, 1.3f);
		AddBarrierProp("CanyonChokeBarrierB", bridgePos + new Vector3(35f, 0f, -18f), -135f, 1.3f);
		AddSimpleProp(KenneyMilitaryRoot + "tower-watch.obj", "CanyonOverlookPost", new Vector3(-160f, 8.5f, 85f), 15f, 7.5f, 4f, new Color(0.30f, 0.34f, 0.30f));
	}

	void AddBottomDeltaBattlefieldProps()
	{
		// 下路深水三角洲航道 (115, 0, -115) 搁浅战舰残骸与水运浮标
		var deltaCenter = new Vector3(155f, -0.6f, -155f);
		AddSimpleProp(KenneyMilitaryRoot + "ship-wreck.obj", "DeltaShipwreckHero", deltaCenter, 35f, 8.5f, 22f, new Color(0.38f, 0.28f, 0.24f));
		AddSimpleProp(KenneyMilitaryRoot + "tower-watch.obj", "DeltaLighthouse", new Vector3(70f, 5.5f, -170f), -45f, 9.0f, 4.5f, new Color(0.32f, 0.36f, 0.32f));
		AddSimpleProp(NatureKitRoot + "tree_palmDetailedTall.fbx", "DeltaPalm_01", new Vector3(145f, 0f, -95f), 0f, 7.5f, 5f, new Color(0.22f, 0.55f, 0.25f));
		AddSimpleProp(NatureKitRoot + "tree_palmDetailedTall.fbx", "DeltaPalm_02", new Vector3(175f, 0f, -125f), 45f, 8.2f, 5.5f, new Color(0.22f, 0.55f, 0.25f));
		AddSimpleProp(NatureKitRoot + "tree_palmBend.fbx", "DeltaPalm_03", new Vector3(135f, 0f, -135f), -30f, 7.0f, 4.8f, new Color(0.22f, 0.55f, 0.25f));
	}

	BattleMapDefinition ResolveMap()
	{
		if (TryResolveAuthoredMapResource(out var authoredMap))
			return authoredMap.ToDefinition();

		var preferredMap = string.IsNullOrWhiteSpace(PreviewMapName)
			? null
			: PreviewMapName;
		var selected = GameState.Instance?.SelectedMapName;
		var fallback = !string.IsNullOrWhiteSpace(selected)
			? selected
			: preferredMap;
		var mapName = BattleMapCatalog.RequestedMapName(fallback);
		return BattleMapCatalog.Get(mapName);
	}

	public bool TryResolveAuthoredMapResource(out BattleMapResource resource)
	{
		resource = null!;
		var resourcePath = AuthoredMapResourcePath?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(resourcePath))
			return false;

		if (authoredMapResourceCache is not null
			&& string.Equals(authoredMapResourceCachePath, resourcePath, StringComparison.Ordinal))
		{
			resource = authoredMapResourceCache;
			return true;
		}

		if (!ResourceLoader.Exists(resourcePath))
			return false;

		var loaded = GD.Load<Resource>(resourcePath);
		if (loaded is BattleMapResource typed)
		{
			authoredMapResourceCache = typed;
			authoredMapResourceCachePath = resourcePath;
			resource = typed;
			return true;
		}

		if (loaded is not null && TryRehydrateBattleMapResource(loaded, resourcePath, out var hydrated))
		{
			authoredMapResourceCache = hydrated;
			authoredMapResourceCachePath = resourcePath;
			resource = hydrated;
			return true;
		}

		return false;
	}

	static bool TryRehydrateBattleMapResource(Resource source, string resourcePath, out BattleMapResource resource)
	{
		resource = null!;
		if (!HasMapResourceData(source))
			return false;

		var hydrated = new BattleMapResource();
		hydrated.MapName = ReadString(source, nameof(BattleMapResource.MapName), hydrated.MapName);
		hydrated.Description = ReadString(source, nameof(BattleMapResource.Description), hydrated.Description);
		hydrated.RandomSeed = ReadInt(source, nameof(BattleMapResource.RandomSeed), hydrated.RandomSeed);

		hydrated.GroundColor = ReadColor(source, nameof(BattleMapResource.GroundColor), hydrated.GroundColor);
		hydrated.RoadColor = ReadColor(source, nameof(BattleMapResource.RoadColor), hydrated.RoadColor);
		hydrated.RoadEdgeColor = ReadColor(source, nameof(BattleMapResource.RoadEdgeColor), hydrated.RoadEdgeColor);
		hydrated.WaterColor = ReadColor(source, nameof(BattleMapResource.WaterColor), hydrated.WaterColor);
		hydrated.PatchAColor = ReadColor(source, nameof(BattleMapResource.PatchAColor), hydrated.PatchAColor);
		hydrated.PatchBColor = ReadColor(source, nameof(BattleMapResource.PatchBColor), hydrated.PatchBColor);
		hydrated.PlayerBasePadColor = ReadColor(source, nameof(BattleMapResource.PlayerBasePadColor), hydrated.PlayerBasePadColor);
		hydrated.EnemyBasePadColor = ReadColor(source, nameof(BattleMapResource.EnemyBasePadColor), hydrated.EnemyBasePadColor);
		hydrated.RockColor = ReadColor(source, nameof(BattleMapResource.RockColor), hydrated.RockColor);
		hydrated.FoliageColor = ReadColor(source, nameof(BattleMapResource.FoliageColor), hydrated.FoliageColor);
		hydrated.TrunkColor = ReadColor(source, nameof(BattleMapResource.TrunkColor), hydrated.TrunkColor);
		hydrated.WallColor = ReadColor(source, nameof(BattleMapResource.WallColor), hydrated.WallColor);
		hydrated.RuinColor = ReadColor(source, nameof(BattleMapResource.RuinColor), hydrated.RuinColor);
		hydrated.SkyColor = ReadColor(source, nameof(BattleMapResource.SkyColor), hydrated.SkyColor);
		hydrated.FogColor = ReadColor(source, nameof(BattleMapResource.FogColor), hydrated.FogColor);
		hydrated.AmbientSkyColor = ReadColor(source, nameof(BattleMapResource.AmbientSkyColor), hydrated.AmbientSkyColor);
		hydrated.AmbientEquatorColor = ReadColor(source, nameof(BattleMapResource.AmbientEquatorColor), hydrated.AmbientEquatorColor);
		hydrated.AmbientGroundColor = ReadColor(source, nameof(BattleMapResource.AmbientGroundColor), hydrated.AmbientGroundColor);
		hydrated.FogStart = ReadFloat(source, nameof(BattleMapResource.FogStart), hydrated.FogStart);
		hydrated.FogEnd = ReadFloat(source, nameof(BattleMapResource.FogEnd), hydrated.FogEnd);

		hydrated.RockClusters = ReadVector3Array(source, nameof(BattleMapResource.RockClusters), hydrated.RockClusters);
		hydrated.TreePositions = ReadVector3Array(source, nameof(BattleMapResource.TreePositions), hydrated.TreePositions);

		foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.SpawnPoints)))
			hydrated.SpawnPoints.Add(RehydrateSpawnPoint(child));
		foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.Roads)))
			hydrated.Roads.Add(RehydrateStrip(child));
		foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.Waters)))
			hydrated.Waters.Add(RehydrateStrip(child));
		foreach (var child in ReadResourceArray(source, nameof(BattleMapResource.Patches)))
			hydrated.Patches.Add(RehydratePatch(child));

		hydrated.TakeOverPath(resourcePath);
		resource = hydrated;
		return true;
	}

	static bool HasMapResourceData(Resource source)
	{
		return source.Get(nameof(BattleMapResource.MapName)).VariantType != Variant.Type.Nil
			|| source.Get(nameof(BattleMapResource.SpawnPoints)).VariantType != Variant.Type.Nil
			|| source.Get(nameof(BattleMapResource.Patches)).VariantType != Variant.Type.Nil;
	}

	static MapSpawnPointResource RehydrateSpawnPoint(Resource source)
	{
		var hydrated = new MapSpawnPointResource();
		hydrated.Label = ReadString(source, nameof(MapSpawnPointResource.Label), hydrated.Label);
		hydrated.Position = ReadVector3(source, nameof(MapSpawnPointResource.Position), hydrated.Position);
		hydrated.Yaw = ReadFloat(source, nameof(MapSpawnPointResource.Yaw), hydrated.Yaw);
		hydrated.IsPlayer = ReadBool(source, nameof(MapSpawnPointResource.IsPlayer), hydrated.IsPlayer);
		hydrated.TeamIndex = ReadInt(source, nameof(MapSpawnPointResource.TeamIndex), hydrated.TeamIndex);
		return hydrated;
	}

	static MapStripResource RehydrateStrip(Resource source)
	{
		var hydrated = new MapStripResource();
		hydrated.Label = ReadString(source, nameof(MapStripResource.Label), hydrated.Label);
		hydrated.Center = ReadVector3(source, nameof(MapStripResource.Center), hydrated.Center);
		hydrated.Size = ReadVector2(source, nameof(MapStripResource.Size), hydrated.Size);
		hydrated.Angle = ReadFloat(source, nameof(MapStripResource.Angle), hydrated.Angle);
		return hydrated;
	}

	static MapPatchResource RehydratePatch(Resource source)
	{
		var hydrated = new MapPatchResource();
		hydrated.Label = ReadString(source, nameof(MapPatchResource.Label), hydrated.Label);
		hydrated.Center = ReadVector3(source, nameof(MapPatchResource.Center), hydrated.Center);
		hydrated.Size = ReadVector2(source, nameof(MapPatchResource.Size), hydrated.Size);
		hydrated.Angle = ReadFloat(source, nameof(MapPatchResource.Angle), hydrated.Angle);
		hydrated.PaletteIndex = ReadInt(source, nameof(MapPatchResource.PaletteIndex), hydrated.PaletteIndex);
		return hydrated;
	}

	static string ReadString(GodotObject source, string propertyName, string fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
	}

	static int ReadInt(GodotObject source, string propertyName, int fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
	}

	static bool ReadBool(GodotObject source, string propertyName, bool fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
	}

	static float ReadFloat(GodotObject source, string propertyName, float fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsSingle();
	}

	static Color ReadColor(GodotObject source, string propertyName, Color fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsColor();
	}

	static Vector2 ReadVector2(GodotObject source, string propertyName, Vector2 fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector2();
	}

	static Vector3 ReadVector3(GodotObject source, string propertyName, Vector3 fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector3();
	}

	static Vector3[] ReadVector3Array(GodotObject source, string propertyName, Vector3[] fallback)
	{
		var value = source.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector3Array();
	}

	static Godot.Collections.Array<Resource> ReadResourceArray(GodotObject source, string propertyName)
	{
		var resources = new Godot.Collections.Array<Resource>();
		var value = source.Get(propertyName);
		if (value.VariantType != Variant.Type.Array)
			return resources;

		var items = value.AsGodotArray();
		for (var i = 0; i < items.Count; i++)
		{
			var item = items[i];
			if (item.VariantType != Variant.Type.Object)
				continue;

			if (item.AsGodotObject() is Resource resource)
				resources.Add(resource);
		}

		return resources;
	}

	static string BuildPreviewSignature(BattleMapDefinition map)
	{
		var signature = $"{map.Name}|{map.RandomSeed}|{map.TreePositions.Length}|{map.RockClusters.Length}|{map.SpawnPoints.Length}|{map.Roads.Length}|{map.Waters.Length}|{map.Patches.Length}|{map.Bridges.Length}|{map.ElevatedPlateaus.Length}";
		for (var i = 0; i < map.SpawnPoints.Length; i++)
		{
			var spawn = map.SpawnPoints[i];
			signature += $"|{spawn.Name}|{spawn.Position.X:F1}|{spawn.Position.Z:F1}|{spawn.Yaw:F1}";
		}
		for (var i = 0; i < map.Roads.Length; i++)
		{
			var road = map.Roads[i];
			signature += $"|{road.Name}|{road.Center.X:F1}|{road.Center.Z:F1}|{road.Size.X:F1}|{road.Size.Y:F1}|{road.Angle:F1}";
		}
		for (var i = 0; i < map.Waters.Length; i++)
		{
			var water = map.Waters[i];
			signature += $"|{water.Center.X:F1}|{water.Center.Z:F1}|{water.Size.X:F1}|{water.Size.Y:F1}|{water.Angle:F1}";
		}
		for (var i = 0; i < map.Patches.Length; i++)
		{
			var patch = map.Patches[i];
			signature += $"|{patch.Name}|{patch.Center.X:F1}|{patch.Center.Z:F1}|{patch.Size.X:F1}|{patch.Size.Y:F1}|{patch.Angle:F1}|{patch.PaletteIndex}";
		}
		return signature;
	}

	// ── 树林查询与视觉效果 ─────────────────────────────────────────────

	/// <summary>
	/// 检查给定世界坐标（XZ 平面）是否在某棵树的覆盖范围（2.5m 半径）内。
	/// </summary>
	public bool IsPositionInForest(Vector3 worldPos)
	{
		const float forestRadius = 2.5f;
		const float forestRadiusSq = forestRadius * forestRadius;
		foreach (var treePos in treePositions)
		{
			var dx = worldPos.X - treePos.X;
			var dz = worldPos.Z - treePos.Z;
			if (dx * dx + dz * dz <= forestRadiusSq)
				return true;
		}
		return false;
	}

	/// <summary>
	/// 淡化/恢复 combatPositions 附近的树木视觉效果。
	/// 当反抗军在树林中作战时，将附近树木变透明，突出战斗单位。
	/// 调用者每帧提供当前处于战斗状态的反抗军位置列表。
	/// </summary>
	public void UpdateForestFade(System.Collections.Generic.IReadOnlyList<Vector3> combatPositions)
	{
		const float fadeRadius = 8f;
		const float fadeRadiusSq = fadeRadius * fadeRadius;

		for (var t = 0; t < treeNodes.Count; t++)
		{
			var treeNode = treeNodes[t];
			if (!GodotObject.IsInstanceValid(treeNode))
				continue;

			var treePos = treePositions[t];
			var shouldFade = false;
			foreach (var combatPos in combatPositions)
			{
				var dx = treePos.X - combatPos.X;
				var dz = treePos.Z - combatPos.Z;
				if (dx * dx + dz * dz <= fadeRadiusSq)
				{
					shouldFade = true;
					break;
				}
			}

			ApplyTreeFade(treeNode, shouldFade);
		}
	}

	static void ApplyTreeFade(Node3D container, bool fade)
	{
		// 遍历容器下所有视觉节点（跳过 ForestCollider StaticBody3D）
		foreach (var child in container.GetChildren())
		{
			if (child is StaticBody3D)
				continue;
			if (child is Node3D visual3D)
				SetForestFadeRecursive(visual3D, fade);
		}
	}

	static void SetForestFadeRecursive(Node3D node, bool fade)
	{
		if (node is GeometryInstance3D geom)
		{
			if (fade)
			{
				// 淡化：叠加半透明覆盖层，突出树林中的战斗单位
				if (geom.MaterialOverlay is not StandardMaterial3D overlay
					|| overlay.ResourceName != "_forest_fade_overlay")
				{
					overlay = new StandardMaterial3D
					{
						ResourceName = "_forest_fade_overlay",
						Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
						ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
						BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
						NoDepthTest = false,
						AlbedoColor = new Color(0.1f, 0.12f, 0.08f, 0.55f)
					};
					geom.MaterialOverlay = overlay;
				}
			}
			else
			{
				// 恢复：只移除树林淡化覆盖层，保留其他覆盖
				if (geom.MaterialOverlay is StandardMaterial3D mat
					&& mat.ResourceName == "_forest_fade_overlay")
					geom.MaterialOverlay = null;
			}
		}

		foreach (var child in node.GetChildren())
		{
			if (child is Node3D child3D)
				SetForestFadeRecursive(child3D, fade);
		}
	}

	void AddRiverDecorations(BattleMapDefinition map)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(5, map.RandomSeed + 127);

		var index = 0;
		foreach (var strip in map.Waters)
		{
			var riverLike = IsRiverLike(strip);
			if (!riverLike)
				continue; // Only decorate river edges, not large lakes/seas

			// Transform to convert local coordinates to world coordinates
			var transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(strip.Angle)), new Vector3(strip.Center.X, 0f, strip.Center.Z));

			var length = strip.Size.Y;
			var width = strip.Size.X;

			// Step size along Z (lengthwise) – tighter spacing for denser bank vegetation
			var stepZ = 2.4f;
			for (float z = -length * 0.46f; z <= length * 0.46f; z += stepZ)
			{
				// Kenney's straight river model: water fills ~42% of strip width.
				// Banks begin at ~X = 0.21*width; place items from ~0.22 to 0.38*width.
				var baseLeftX  = -width * rng.RandfRange(0.22f, 0.36f);
				var baseRightX =  width * rng.RandfRange(0.22f, 0.36f);

				// Primary bank positions
				var leftPos  = transform * new Vector3(baseLeftX,  0.03f, z + rng.RandfRange(-1.0f, 1.0f));
				var rightPos = transform * new Vector3(baseRightX, 0.03f, z + rng.RandfRange(-1.0f, 1.0f));
				SpawnRiverEdgeAsset(map, leftPos,  rng, ref index);
				SpawnRiverEdgeAsset(map, rightPos, rng, ref index);

				// Second row further from bank for layered depth (~50% chance each)
				if (rng.Randf() < 0.55f)
				{
					var leftPos2 = transform * new Vector3(-width * rng.RandfRange(0.36f, 0.46f), 0.03f, z + rng.RandfRange(-1.4f, 1.4f));
					SpawnRiverEdgeAsset(map, leftPos2, rng, ref index);
				}
				if (rng.Randf() < 0.55f)
				{
					var rightPos2 = transform * new Vector3(width * rng.RandfRange(0.36f, 0.46f), 0.03f, z + rng.RandfRange(-1.4f, 1.4f));
					SpawnRiverEdgeAsset(map, rightPos2, rng, ref index);
				}
			}
		}
	}

	void SpawnRiverEdgeAsset(BattleMapDefinition map, Vector3 worldPos, RandomNumberGenerator rng, ref int index)
	{
		index++;
		var roll = rng.Randf();

		if (roll < 0.22f)
		{
			// Spawn a tree!
			var scenePath = PickTreeScene(map, index);
			var targetHeight = rng.RandfRange(4.5f, 6.5f); // Slightly smaller than normal trees to fit banks nicely!
			var targetSpan = targetHeight * 0.70f;
			
			var container = new Node3D
			{
				Name = $"RiverTreeGroup_{index:00}",
				Position = worldPos
			};
			generatedRoot!.AddChild(container);
			treeNodes.Add(container);
			treePositions.Add(worldPos);

			var imported = TryInstanceScene(scenePath, $"RiverTree_{index:00}");
			if (imported is not null)
			{
				FitImportedNode(imported, targetHeight, targetSpan);
				imported.Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f);
				container.AddChild(imported);
			}
			else
			{
				// Fallback to procedural trunk and canopy if fbx fails
				var trunkMat = Material(map.TrunkColor, 0.82f);
				var foliageMat = Material(map.FoliageColor.Lerp(new Color(0.18f, 0.55f, 0.22f), 0.45f), 0.90f);
				
				var trunk = new MeshInstance3D
				{
					Name = "ProceduralTrunk",
					Position = new Vector3(0f, targetHeight * 0.3f, 0f),
					Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = targetHeight * 0.6f, RadialSegments = 6 },
					MaterialOverride = trunkMat
				};
				container.AddChild(trunk);

				var canopy = new MeshInstance3D
				{
					Name = "ProceduralCanopy",
					Position = new Vector3(0f, targetHeight * 0.75f, 0f),
					Mesh = new SphereMesh { Radius = targetSpan * 0.6f, Height = targetSpan * 1.1f, RadialSegments = 7, Rings = 4 },
					MaterialOverride = foliageMat
				};
				container.AddChild(canopy);
			}
			
			// If tree is placed successfully, also spawn a little grass clump at its foot to look nice!
			if (rng.Randf() < 0.70f)
			{
				var grassPos = worldPos + new Vector3(rng.RandfRange(-0.4f, 0.4f), 0f, rng.RandfRange(-0.4f, 0.4f));
				var grassScene = PickGrassScene(map, index + 1);
				TryAddImportedScenery(
					grassScene,
					$"RiverTreeGrass_{index:00}",
					grassPos,
					new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
					rng.RandfRange(0.40f, 0.75f),
					rng.RandfRange(0.60f, 1.00f),
					map.FoliageColor,
					preserveMaterials: true);
			}
		}
		else if (roll < 0.78f) // 0.22f to 0.78f = 56% standard foliage/grass
		{
			var useGrass = rng.Randf() > 0.30f;
			var scenePath = useGrass ? PickGrassScene(map, index) : PickBushScene(map, index);
			var targetHeight = useGrass ? rng.RandfRange(0.40f, 0.85f) : rng.RandfRange(0.60f, 1.10f);
			var targetSpan   = useGrass ? rng.RandfRange(0.60f, 1.20f) : rng.RandfRange(0.80f, 1.40f);

			// Try to load the imported polyhaven/kenney model first
			bool placed = TryAddImportedScenery(
				scenePath,
				$"RiverFoliage_{index:00}",
				worldPos,
				new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
				targetHeight,
				targetSpan,
				map.FoliageColor,
				preserveMaterials: true);

			// ── Fallback: procedural clump so banks are never bare ────────
			if (!placed)
			{
				var foliageMat = Material(map.FoliageColor.Lerp(new Color(0.18f, 0.55f, 0.22f), 0.45f), 0.90f);
				var stemMat    = Material(map.TrunkColor, 0.82f);
				var clumpRoot  = new Node3D { Name = $"RiverFoliageFB_{index:00}", Position = worldPos };
				var bladeCount = rng.RandiRange(3, 5);
				for (var b = 0; b < bladeCount; b++)
				{
					var bladePhi = rng.RandfRange(0f, Mathf.Tau);
					var bladeR   = rng.RandfRange(0.05f, 0.22f);
					var bladeH   = rng.RandfRange(targetHeight * 0.70f, targetHeight);
					var bladeW   = rng.RandfRange(0.06f, 0.14f);
					clumpRoot.AddChild(new MeshInstance3D
					{
						Name = $"Blade_{b}",
						Position = new Vector3(Mathf.Cos(bladePhi) * bladeR, bladeH * 0.5f, Mathf.Sin(bladePhi) * bladeR),
						Rotation = new Vector3(rng.RandfRange(-0.18f, 0.18f), bladePhi, rng.RandfRange(-0.12f, 0.12f)),
						Mesh = new CylinderMesh { TopRadius = bladeW * 0.15f, BottomRadius = bladeW, Height = bladeH, RadialSegments = 5 },
						MaterialOverride = foliageMat
					});
				}
				// Small bulge at base
				clumpRoot.AddChild(new MeshInstance3D
				{
					Name = "BaseClump",
					Position = new Vector3(0f, 0.05f, 0f),
					Mesh = new SphereMesh { Radius = targetSpan * 0.22f, Height = 0.18f, RadialSegments = 7, Rings = 3 },
					MaterialOverride = stemMat
				});
				generatedRoot!.AddChild(clumpRoot);
			}
		}
		else if (roll < 0.92f)
		{
			var rockPath    = PickRockScene(map, index);
			var targetHeight = rng.RandfRange(0.25f, 0.65f);
			var targetSpan   = rng.RandfRange(0.45f, 0.95f);

			bool placed = TryAddImportedScenery(
				rockPath,
				$"RiverRock_{index:00}",
				new Vector3(worldPos.X, worldPos.Y - 0.05f, worldPos.Z),
				new Vector3(rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.1f, 0.1f)),
				targetHeight,
				targetSpan,
				map.RockColor,
				preserveMaterials: true);

			// Fallback: procedural pebble
			if (!placed)
			{
				generatedRoot!.AddChild(new MeshInstance3D
				{
					Name = $"RiverRockFB_{index:00}",
					Position = new Vector3(worldPos.X, worldPos.Y - 0.04f, worldPos.Z),
					Rotation = new Vector3(rng.RandfRange(-0.15f, 0.15f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.15f, 0.15f)),
					Scale = new Vector3(rng.RandfRange(0.7f, 1.4f), rng.RandfRange(0.4f, 0.7f), rng.RandfRange(0.7f, 1.4f)) * targetHeight,
					Mesh = new SphereMesh { Radius = 1f, Height = 1.1f, RadialSegments = 7, Rings = 4 },
					MaterialOverride = Material(map.RockColor, 0.94f)
				});
			}
		}
	}

	void AddPatchDecorations(BattleMapDefinition map)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(7, map.RandomSeed + 233);

		var index = 0;
		foreach (var patch in map.Patches)
		{
			var transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(patch.Angle)), new Vector3(patch.Center.X, 0f, patch.Center.Z));

			var width = patch.Size.X;
			var length = patch.Size.Y;

			bool nearWater = BattleMapCatalog.IsPointInWater(map, patch.Center, 2f) || BattleMapCatalog.DistanceToWater(map, patch.Center) < 8f;

			// We step along the boundary of the patch to place decorations
			// For width (horizontal edges)
			var stepX = 4.0f;
			for (float x = -width * 0.5f; x <= width * 0.5f; x += stepX)
			{
				var topPos = transform * new Vector3(x + rng.RandfRange(-1f, 1f), PatchHeight, -length * 0.5f + rng.RandfRange(-0.5f, 0.5f));
				SpawnPatchEdgeAsset(map, topPos, nearWater, rng, ref index);

				var bottomPos = transform * new Vector3(x + rng.RandfRange(-1f, 1f), PatchHeight, length * 0.5f + rng.RandfRange(-0.5f, 0.5f));
				SpawnPatchEdgeAsset(map, bottomPos, nearWater, rng, ref index);
			}

			// For length (vertical edges)
			var stepZ = 4.0f;
			for (float z = -length * 0.5f; z <= length * 0.5f; z += stepZ)
			{
				var leftPos = transform * new Vector3(-width * 0.5f + rng.RandfRange(-0.5f, 0.5f), PatchHeight, z + rng.RandfRange(-1f, 1f));
				SpawnPatchEdgeAsset(map, leftPos, nearWater, rng, ref index);

				var rightPos = transform * new Vector3(width * 0.5f + rng.RandfRange(-0.5f, 0.5f), PatchHeight, z + rng.RandfRange(-1f, 1f));
				SpawnPatchEdgeAsset(map, rightPos, nearWater, rng, ref index);
			}
		}
	}

	void SpawnPatchEdgeAsset(BattleMapDefinition map, Vector3 worldPos, bool nearWater, RandomNumberGenerator rng, ref int index)
	{
		index++;
		var roll = rng.Randf();

		if (nearWater)
		{
			if (roll < 0.50f)
			{
				var scenePath = PickGrassScene(map, index);
				var targetHeight = rng.RandfRange(0.40f, 0.90f);
				var targetSpan = rng.RandfRange(0.60f, 1.20f);
				TryAddImportedScenery(scenePath, $"PatchWaterGrass_{index:00}", worldPos, new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f), targetHeight, targetSpan, map.FoliageColor, true);
			}
			else if (roll < 0.85f)
			{
				var rockPath = PickRockScene(map, index);
				var targetHeight = rng.RandfRange(0.20f, 0.70f);
				var targetSpan = rng.RandfRange(0.40f, 1.00f);
				TryAddImportedScenery(rockPath, $"PatchWaterRock_{index:00}", new Vector3(worldPos.X, worldPos.Y - 0.04f, worldPos.Z), new Vector3(rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.1f, 0.1f)), targetHeight, targetSpan, map.RockColor, true);
			}
		}
		else
		{
			if (roll < 0.25f)
			{
				var scenePath = PickGrassScene(map, index);
				var targetHeight = rng.RandfRange(0.50f, 1.00f);
				var targetSpan = rng.RandfRange(0.70f, 1.30f);
				TryAddImportedScenery(scenePath, $"PatchGrass_{index:00}", worldPos, new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f), targetHeight, targetSpan, map.FoliageColor, true);
			}
		}
	}
}
