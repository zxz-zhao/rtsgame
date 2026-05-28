# UnityRTS 免费资源收集清单

这份文档用于回去后集中挑选、下载和整理免费资源。优先目标是地图场景相关资源：河流、水面、桥、建筑、基地、树石、废墟、地面材质、战斗音效和环境音。

如果项目确定走二战风格，优先看：`WW2_ASSET_COLLECTION.md`

## 使用原则

- 优先选 `CC0 / Public Domain`：商用风险低，通常可修改、可商用、无需署名。
- 其次选 `CC BY`：可商用，但要记录作者和来源，后续 Credits 里署名。
- 暂时避开 `CC BY-NC`、`仅个人使用`、`禁止商用`、`授权写不清楚` 的资源。
- Unity Asset Store 的免费资源也能用，但要看是不是 `Standard Unity Asset Store EULA`，不要把原始包单独转卖或裸分发。
- 每个下载包都保留 `License.txt`、来源链接、作者名和下载日期。

## 第一优先级：地图 3D 模型

| 来源 | 推荐资源 | 链接 | 授权/备注 |
| --- | --- | --- | --- |
| Kenney | Nature Kit：树、岩石、草、自然装饰 | https://kenney.nl/assets/nature-kit | CC0，适合低模地图基础装饰 |
| Kenney | City Kit Commercial：现代楼、商业建筑 | https://kenney.nl/assets/city-kit-commercial | CC0，适合城市废墟/大厅背景/地图建筑 |
| Kenney | Prototype Textures：占位/地面纹理 | https://kenney-assets.itch.io/prototype-textures | CC0，适合先统一地图块和测试关卡 |
| Quaternius | Buildings Pack：低模建筑 | https://quaternius.com/packs/buildings.html | CC0，FBX/OBJ/Blend，适合建筑原型 |
| Quaternius | 总资源页：建筑、街道、车辆、树石、坦克、船 | https://quaternius.com/ | 多数免费包标 CC0，下载前逐包确认 |
| OpenGameArt | LowPoly Buildings by Quaternius | https://opengameart.org/content/lowpoly-buildings | CC0，建筑包备选下载源 |
| OpenGameArt | CC0 3D Building 集合 | https://opengameart.org/content/cc0-3d-building | 混合集合，逐个资源确认授权 |
| itch.io | Low Poly Buildings by GualtierisGG | https://gualtierisgg.itch.io/low-poly-buildings | CC0，12 栋建筑，多颜色变体 |
| Screaming Brain Studios | 免费贴图/等距/老派游戏资源 | https://screamingbrainstudios.com/ | CC0/Public Domain，适合地面和旧 RTS 质感 |

## 第二优先级：河流、水面、地形、材质

| 来源 | 推荐用途 | 链接 | 授权/备注 |
| --- | --- | --- | --- |
| Poly Haven | 地形、岩石、混凝土、金属、HDRI | https://polyhaven.com/ | 官方标 CC0，可商用 |
| Poly Haven Textures | 地面、岩石、砖、混凝土、金属分类 | https://polyhaven.com/textures/ | 适合做地图地表和建筑表面 |
| ambientCG | PBR 材质、地形、岩石、道路、水泥 | https://ambientcg.com/ | 官方标 CC0，可商用 |
| ambientCG Docs | 了解材质/地形/Decal 类型 | https://docs.ambientcg.com/asset-types/ | 用于筛选 Unity 需要的贴图类型 |
| Unity Asset Store | 免费低模环境包、水体包、粒子包 | https://marketplace.unity.com/top-assets/top-free | 免费不等于 CC0，逐个看 EULA |
| Unity LowPoly Environment Pack | 低模环境备选 | https://marketplace.unity.com/packages/3d/environments/landscapes/lowpoly-environment-pack-99479 | 免费，Unity 标准 EULA，适合参考/测试 |

## 第三优先级：音效和音乐

| 来源 | 推荐用途 | 链接 | 授权/备注 |
| --- | --- | --- | --- |
| Freesound | UI 点击、爆炸、枪炮、环境音 | https://freesound.org/ | 每条音效授权不同，优先筛 CC0 |
| Freesound FAQ | 查看 Freesound 授权说明 | https://freesound.org/help/faq/ | CC0 基本最安全，CC BY 要署名，避开 BY-NC |
| GamesFXMaker Freesound Browser | 商用友好音效筛选 | https://gamesfxmaker.com/ | 帮助筛 CC0/Attribution 音效 |
| OpenGameArt Audio | 音效/音乐 | https://opengameart.org/ | 授权混合，优先 CC0；GPL/SA 类先别用 |
| Kenney | UI 和基础游戏音效 | https://kenney.nl/assets | Kenney 大量资源 CC0，可找 UI/提示音 |

## 建议优先下载的资源组合

第一批先别贪多，优先拿能让地图立刻变好看的组合：

1. `Kenney Nature Kit`：树、石头、草丛、自然地图装饰。
2. `Quaternius Buildings Pack`：基础建筑，用来替换素建筑。
3. `Kenney City Kit Commercial`：城市楼、废墟地图、建筑背景。
4. `Poly Haven / ambientCG`：混凝土、泥土、岩石、道路材质。
5. `Freesound CC0`：UI 点击、领取、爆炸、枪炮、环境风声。

## RTS 地图具体要找的关键词

用于 Kenney、Quaternius、itch.io、OpenGameArt、Unity Asset Store 搜索：

- `low poly buildings`
- `low poly military base`
- `low poly RTS buildings`
- `low poly bridge`
- `low poly river`
- `low poly nature`
- `low poly rocks trees`
- `low poly ruins`
- `sci-fi buildings low poly`
- `industrial props CC0`
- `concrete PBR CC0`
- `terrain PBR CC0`
- `river water shader free Unity`
- `explosion sound CC0`
- `gun shot sound CC0`
- `UI click sound CC0`

## 下载后目录建议

```text
Assets/
  External/
    Kenney/
      NatureKit/
      CityKitCommercial/
      PrototypeTextures/
    Quaternius/
      BuildingsPack/
      StreetsPack/
      NaturePack/
    PolyHaven/
      Materials/
      HDRI/
    ambientCG/
      Materials/
    Freesound/
      UI/
      Combat/
      Ambience/
```

## 授权记录模板

下载一个资源包后，在 `Assets/External/THIRD_PARTY_ASSETS.md` 里按这个格式记录：

```text
资源名：
作者：
来源链接：
下载日期：
授权：
用途：
是否需要署名：
License 文件位置：
备注：
```

## 筛选时的判断标准

- 能直接进 Unity：优先 `FBX`、`OBJ`、`GLB/GLTF`、`PNG`、`WAV`、`OGG`。
- 风格统一：优先低模、军事、科幻、硬表面，不要混入太卡通或太写实的包。
- 面数合理：移动端 RTS 场景里大量重复物件，单个小道具不要太高面。
- 材质简单：优先少材质、可换色、可合批的模型。
- 模块化强：墙、桥、道路、建筑块能拼接最好。
- 授权清楚：没有 License 文件或页面授权不清楚的先不要用。

## 后续可以让我做的整理工作

- 下载后我可以统一导入 Unity，整理目录和材质。
- 我可以批量检查模型尺寸、Pivot、碰撞、NavMesh 阻挡。
- 我可以把建筑/树石/废墟做成 Prefab。
- 我可以统一阵营色、描边、灯光、地图主题色。
- 我可以把河流、桥、道路、资源点重新布进地图。
- 我可以生成 `THIRD_PARTY_ASSETS.md`，把授权来源全部登记好。
