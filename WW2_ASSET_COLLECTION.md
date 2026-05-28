# UnityRTS 二战风格资源收集清单

目标风格：二战 RTS / 军事低模 / 可在移动端运行。  
核心画面关键词：泥土战场、壕沟、河流桥梁、废墟村镇、沙袋、木箱、油桶、碉堡、坦克、炮塔、螺旋桨飞机、烟尘爆炸。

## 风格边界

- 优先做“二战低模 RTS”，不要混入太科幻、太现代、太魔法的资源。
- 可接受适度风格化，但颜色要压低：军绿、土黄、灰砖、铁锈、深棕、烟黑。
- 地图要像战场：道路、河流、桥、壕沟、废墟、补给点、炮兵阵地。
- UI 可以科技感，但地图和模型要军事年代感，避免未来机甲/霓虹风。

## 第一批优先找的免费模型

| 分类 | 需要资源 | 推荐来源 | 链接 | 授权/备注 |
| --- | --- | --- | --- | --- |
| 坦克 | Sherman/Tiger 风格低模坦克、炮塔、履带车 | itch.io WW2 Low-poly 搜索 | https://itch.io/game-assets/free/tag-low-poly/tag-world-war-ii | 每个包单独看授权，适合先淘免费 WW2 包 |
| 坦克 | 低模坦克包 | CraftUz Free Low Poly Tanks Pack | https://craftuz.itch.io/free-low-poly-tanks-pack | 标注 Royalty Free，下载前保存页面授权截图 |
| 坦克 | Low Poly WW2 Sherman Tank | CGTrader | https://www.cgtrader.com/free-3d-models/military/military-vehicle/low-poly-ww2-sherman-tank | Royalty Free，不是 CC0，使用前看 CGTrader 许可 |
| 军事基地 | 军营、炮台、军用道具、车辆、武器 | Unity Asset Store Military FREE | https://assetstore.unity.com/packages/3d/environments/military-free-low-poly-3d-models-pack-260358 | 免费，Unity 标准 EULA，不要从盗版站下载 |
| 通用低模 | 建筑、树石、车辆、船、道具 | Quaternius | https://quaternius.com/ | Quaternius 大量资产为 CC0，适合补通用军事地图物件 |
| 建筑废墟 | 废墟建筑、破墙、城市残骸 | OpenGameArt / Sketchfab CC0 | https://opengameart.org/ | 授权混合，只拿 CC0 或明确可商用 |
| 二战建筑 | 城市建筑、砖房、工厂、仓库 | Kenney City Kit Commercial | https://kenney.nl/assets/city-kit-commercial | CC0，可改成二战村镇/城市废墟 |
| 自然战场 | 树、岩石、灌木、地形装饰 | Kenney Nature Kit | https://kenney.nl/assets/nature-kit | CC0，适合战场边缘和河岸 |
| 公共 3D | 砖房、工业件、旧道具、木箱 | PolyScan / Artaley3D / Numinia | https://polyscann.com/assets/category/1 | 优先挑 CC0、GLB/FBX/OBJ |

## 地图场景资源

| 场景模块 | 需要资源 | 可用做法 | 状态 |
| --- | --- | --- | --- |
| 壕沟 | 木板、泥墙、沙袋、刺网、土坡 | 可用低模方块/墙体程序化搭，贴泥土材质 | `[我能做]` |
| 河流 | 河道、水面、浅滩、岸边石头、桥 | Unity 水面平面 + 滚动法线/贴图 + 石头岸线 | `[我能做原型]` |
| 桥梁 | 木桥、石桥、铁桥、被炸断桥 | 可用 Quaternius/自建低模桥，或模块化拼接 | `[我能做原型]` |
| 村镇 | 砖房、仓库、教堂/钟楼、围墙 | 用 Kenney/Quaternius/CC0 建筑改色做战区村镇 | `[我能做原型]` |
| 城市废墟 | 破楼、断墙、瓦砾、弹坑 | OpenGameArt/CC0 废墟资源 + 地面贴花 | `[我能做原型]` |
| 军事基地 | 帐篷、哨塔、碉堡、仓库、油桶、木箱 | Unity 免费军事包 + 自建低模军事件 | `[我能做原型]` |
| 炮兵阵地 | 沙袋圈、火炮底座、弹药箱、炮弹堆 | 可程序化组合现有模型 | `[我能做]` |
| 地图边界 | 丛林、山坡、废墟墙、铁丝网 | 可做阻挡和视觉边界，参与 NavMesh | `[我能做]` |

## 材质与贴图

| 类型 | 推荐来源 | 链接 | 用途 |
| --- | --- | --- | --- |
| 泥土/湿土/碎石 | ambientCG | https://ambientcg.com/ | 战场地面、壕沟、河岸 |
| 砖墙/混凝土/石墙 | Poly Haven | https://polyhaven.com/textures/ | 城市废墟、碉堡、桥梁 |
| 金属/铁锈 | ambientCG / Poly Haven | https://ambientcg.com/ | 坦克、炮塔、油桶、军工件 |
| 木板/木箱 | Poly Haven / Kenney | https://polyhaven.com/ | 壕沟木板、木桥、弹药箱 |
| 烧焦/弹坑/污渍 | OpenGameArt / 自制贴花 | https://opengameart.org/ | 地面战损、爆炸痕迹 |

## 二战特效清单

| 特效 | 需要表现 | 推荐来源/做法 | 状态 |
| --- | --- | --- | --- |
| 枪口火焰 | 短闪光、火星、轻烟 | Unity ParticleSystem 自制 | `[我能做]` |
| 炮口火焰 | 大闪光、冲击烟、炮口烟柱 | 粒子 + Kenney Smoke Particles | `[我能做]` |
| 爆炸 | 火球、黑烟、碎片、冲击波 | OpenGameArt CC0 爆炸/烟雾素材 + 粒子 | `[我能做]` |
| 坦克中弹 | 火花、黑烟、火苗、震动 | 粒子 + 灯光闪烁 + 屏幕震动 | `[我能做]` |
| 建筑受击 | 灰尘、砖块碎片、烟尘 | 粒子 + 小碎片 Mesh | `[我能做]` |
| 河流水面 | 轻微流动、岸边泡沫、炮弹水花 | 滚动纹理 + 粒子水花 | `[我能做原型]` |
| 战场氛围 | 远处烟柱、灰雾、火点、警戒灯 | Fog + 粒子 + 低强度点光 | `[我能做]` |

## 可用特效资源

| 来源 | 资源 | 链接 | 授权/备注 |
| --- | --- | --- | --- |
| Kenney | Smoke Particles | https://kenney.nl/assets/smoke-particles | CC0，适合炮烟、爆炸烟、受击烟 |
| OpenGameArt | Smoke particle assets | https://opengameart.org/content/smoke-particle-assets | CC0，Kenney 发布，包含黑烟/爆炸/白烟 |
| OpenGameArt | More Explosions | https://opengameart.org/content/more-explosions | CC0，可做爆炸序列贴图 |
| OpenGameArt | CC0 Special Effects 集合 | https://opengameart.org/content/cc0-special-effects | 集合页，逐个确认资源授权 |

## 二战音效方向

| 音效 | 搜索关键词 | 推荐来源 | 授权注意 |
| --- | --- | --- | --- |
| 步枪/机枪 | `rifle shot CC0`、`machine gun CC0` | Freesound / OpenGameArt | Freesound 只优先 CC0，CC BY 要署名 |
| 坦克炮 | `tank cannon shot CC0`、`artillery fire CC0` | Freesound | 避免 BY-NC |
| 爆炸 | `explosion distant CC0`、`mortar explosion CC0` | Freesound / OpenGameArt | 注意是否需署名 |
| 车辆 | `tank engine loop`、`diesel engine loop` | Freesound | 循环音要检查噪点 |
| UI 军事提示 | `radio beep`、`military radio click` | Freesound / Kenney | 可做二战无线电反馈 |
| 环境 | `battle ambience`、`wind field`、`distant artillery` | Freesound | 背景音版权要更谨慎 |

## 二战 RTS 第一阶段下载组合

1. `Military FREE - Low Poly 3D Models Pack`：军营/车辆/军事道具，先搭基地氛围。
2. `Kenney Nature Kit`：树、岩石、草丛，做战场边界和河岸。
3. `Kenney City Kit Commercial`：砖房/城市件，改成二战城镇和废墟。
4. `Quaternius` 免费包：补桥、车、船、道具、通用建筑。
5. `Kenney Smoke Particles` + OpenGameArt 爆炸：做枪炮烟尘和爆炸。
6. `ambientCG / Poly Haven`：泥土、砖墙、混凝土、铁锈材质。
7. `Freesound CC0`：枪炮、爆炸、坦克引擎、无线电提示。

## 搜索关键词

```text
low poly WW2 tank
low poly World War II assets
low poly military base free
WW2 bunker 3D model free
WW2 ruins 3D model CC0
low poly trenches
low poly sandbags
low poly artillery
low poly military bridge
WW2 village buildings low poly
war ruins low poly CC0
smoke particle CC0
explosion sprite CC0
tank cannon sound CC0
rifle shot CC0
```

## 下载后目录建议

```text
Assets/
  External/
    WW2/
      Tanks/
      Buildings/
      Bunkers/
      Trenches/
      Props/
      TerrainMaterials/
      VFX/
      SFX/
```

## 我能负责的落地工作

- `[我能做]` 把下载的 WW2 模型导入 Unity，统一比例、Pivot、材质和命名。
- `[我能做]` 把坦克/建筑/沙袋/废墟做成 Prefab。
- `[我能做]` 改地图布局，让河流、桥、壕沟、基地、资源点有战术意义。
- `[我能做]` 用粒子做炮火、烟尘、爆炸、建筑受击、坦克冒烟。
- `[我能做]` 给地图做二战色调：低饱和军绿、泥土、灰砖、烟雾、火光。
- `[我能做]` 做授权登记文档，避免后期版权不清。
- `[需你挑选/确认]` 最终想偏“写实二战”还是“低模二战手游风”。
