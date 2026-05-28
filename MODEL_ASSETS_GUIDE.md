# 外部模型资源说明

本项目已导入 Kenney 官方 CC0 资源包，路径在：

- `Assets/External/Kenney/BlockyCharacters`
- `Assets/External/Kenney/BlasterKit`
- `Assets/External/Kenney/CarKit`

来源页面：

- Blocky Characters: https://www.kenney.nl/assets/blocky-characters
- Blaster Kit: https://www.kenney.nl/assets/blaster-kit
- Car Kit: https://www.kenney.nl/assets/car-kit

每个资源包目录内都保留了原始 `License.txt`。授权为 Creative Commons Zero (CC0)，可用于个人、教学和商业项目；署名 Kenney 不是强制要求，但建议保留来源说明。

## 当前应用方式

`Assets/Editor/PrefabBuilder.cs` 会在生成单位 prefab 时自动加载这些 FBX：

- 步兵：`character-a.fbx` + `blaster-d.fbx`
- 喷火兵：`character-f.fbx` + `blaster-r.fbx`
- 坦克：`tractor-shovel.fbx` 作为车辆底盘，保留项目内程序化炮塔/炮管
- 火炮：`truck-flat.fbx` 作为车辆底盘，保留项目内程序化火炮组件
- 战场道具：`crate-*`、`target-large`、`debris-*`、`wheel-*` 组合成补给箱、弹药箱、靶标和载具残骸
- 战斗特效：`bullet-foam-tip`、`grenade-a/b`、`smoke` 组合成步枪弹、炮弹、航弹和火焰弹道

旧的程序化模型部件仍保留在 prefab 里作为回退和测试锚点；当外部 FBX 缺失或导入失败时，单位仍能生成基础模型。

`BattlefieldPropSpawner` 会在地图生成时自动把这些道具摆到双方基地和中央区域。编辑器场景构建与运行时地图重建都会调用它，因此切换地形地图后道具也会跟着刷新。

`CombatProjectile` 负责运行时弹道表现。单位攻击仍保持原有瞬时伤害结算，弹道、拖尾和落点爆闪只作为视觉层，避免影响 AI、联机同步和数值平衡。

## 重新生成

在 Unity/Tuanjie 编辑器菜单运行：

`RTS/生成场景/生成所有Prefab`

或使用批处理入口：

`PrefabBuilder.BuildAllPrefabs`

生成后，`SceneBuilder.BuildGameScene` 会继续触发自动冒烟测试，检查外部模型文件、Resources prefab、地图、HUD、小地图和 NavMesh。
