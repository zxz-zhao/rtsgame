# UnityRTS 地形地图修改说明

地图数据现在集中在：

```text
Assets/Scripts/Core/BattleMapCatalog.cs
```

也可以从 Unity 菜单打开辅助窗口：

```text
RTS > 地图 > 地图工具窗口
```

这个窗口可以快速查看地图元素数量、生成某张地图的场景预览、把某张地图设为本机运行测试地图。

## 改地图主题

每张地图由 `CreateSandOasis`、`CreateIceFortress`、`CreateJungle`、`CreateCityRuins`、`CreateGlobalConquest` 创建。常用颜色字段：

- `GroundColor`：主地面颜色
- `RoadColor` / `RoadEdgeColor`：道路和道路边缘
- `WaterColor`：水面
- `PatchAColor` / `PatchBColor`：地面色块
- `RockColor` / `FoliageColor` / `TrunkColor`：石头、树冠、树干
- `SkyColor` / `FogColor` / `FogStart` / `FogEnd`：天空和雾效

大厅选择不同地图时，`GameInitializer` 会读取同一份目录，并通过 `RuntimeBattleMapBuilder` 在运行时重新生成布局和调色。

## 改地形布局

默认布局在 `CreateBase` 里。单张地图如果要拥有独立布局，可以在对应的 `CreateIceFortress`、`CreateJungle`、`CreateCityRuins` 等方法里覆盖这些数组：

- `Roads`：地面道路，`Vector2(width, length)` 控制宽和长，`Angle` 控制旋转角度。
- `Waters`：水面装饰，参数同道路。
- `Patches`：基地地垫、中央据点、资源区等色块，最后一个 `paletteIndex` 对应 0/1/2/3 四套颜色。
- `RockClusters`：岩石障碍群坐标，会参与 NavMesh 烘焙，能阻挡寻路。
- `TreePositions`：树木装饰坐标，也会参与 NavMesh 烘焙。
- `RuinWalls`：废墟墙障碍，`segments` 控制长度。
- `SandbagRings`：基地沙袋防御圈。

改完后在 Unity 菜单执行：

```text
RTS > 生成场景 > ② 生成游戏场景（地形+HUD）
```

也可以预览某个主题：

```text
RTS > 地图 > 生成预览 > 沙漠绿洲 / 冰雪要塞 / 丛林战场 / 城市废墟 / 全球争霸
```

场景生成时会自动 Bake NavMesh。进入游戏时也会重新构建运行时 NavMesh，保证大厅选择的地图布局能影响单位绕路。真正影响寻路的是岩石、树、废墟墙、沙袋、边界墙和建筑；道路、水面、色块只是视觉层，不会挡住点击和寻路。

## 自动测试

现在每次执行 `SceneBuilder.BuildGameScene` 生成游戏场景后，会自动运行一次低风险冒烟测试。测试内容包括：

- 地图目录是否完整、坐标是否越界
- `GameScene` 必要对象是否存在
- HUD、小地图、核心管理器引用是否绑定
- 运行时需要的 Prefab 是否能从 `Resources/Prefabs` 加载
- `NavMesh.asset` 是否存在并能加载
- 场景里是否有 Missing Script

也可以手动执行：

```text
RTS > 测试 > 运行自动冒烟测试
```
