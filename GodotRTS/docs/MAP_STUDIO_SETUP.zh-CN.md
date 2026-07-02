# Godot 3D 仿真地图搭建路线

这个工程里的 `MapStudio` 是你后面自己搭 3D 地图的入口。

场景路径：
- `res://scenes/map/MapStudio.tscn`

地图资源路径：
- `res://data/maps/simulation_sandbox.tres`

## 现在这版已经有的东西

- 一份可编辑地图资源 `BattleMapResource`
- 一个地图工作场景 `MapStudio.tscn`
- 一个编辑器插件入口 `RTS Map Studio`
- `BattleMapRenderer` 可以直接预览：
  - 地表色块
  - 道路
  - 水域
  - 树木点位
  - 岩石点位
  - 出生点标记

## 你接下来怎么装 Terrain3D

截至 `2026-06-29`，`Terrain3D` 是 Godot Asset Library 上的第三方插件，不是 Godot 内置功能。

参考：
- [Terrain3D Asset Library](https://godotengine.org/asset-library/asset/3892)
- [Terrain3D 文档](https://terrain3d.readthedocs.io/en/stable/)

建议流程：

1. 下载并安装 `Terrain3D`
2. 在 Godot 里启用插件
3. 打开 `MapStudio.tscn`
4. 在 `TerrainRoot` 下创建 Terrain3D 地形节点
5. 用 `simulation_sandbox.tres` 里的水域、道路、出生点做布局参考

## 推荐素材来源

你要“仿真”而不是低模，素材来源要换：

- 地表材质：`Poly Haven`
  - [Poly Haven](https://polyhaven.com/)
- PBR 材质：`ambientCG`
  - [ambientCG](https://ambientcg.com/)
- 树木 / 植被模型：
  - `Poly Haven`
  - `Quixel Megascans`（如果你后面接受 Epic 生态）
- 模块化道具：
  - `Kenney` 只适合占位，不适合最后成品写实风格

## 推荐搭建方式

### 1. 地形
- 用 `Terrain3D` 负责起伏、坡地、河岸、海岸
- 刷 4 到 8 层真实材质：
  - 草地
  - 森林地表
  - 泥地
  - 石地
  - 沙地
  - 碎石路
  - 混凝土
  - 沿海湿地

### 2. 功能块
- 用 `GridMap` 摆：
  - 道路模块
  - 掩体
  - 基地平台
  - 墙体
  - 桥

### 3. 植被
- 树木不要再用低模占位树做最终版
- 用真实树模型
- 建议做：
  - 大树层
  - 小树层
  - 灌木层
  - 地被层

### 4. 游戏规则层
- `simulation_sandbox.tres` 保存：
  - 出生点
  - 道路带
  - 水域带
  - 地表补丁
  - 树木点位
  - 岩石点位

## 第一阶段你该做什么

先不要急着做完整游戏编辑器，先把下面这套打通：

1. 安装 Terrain3D
2. 打开 `MapStudio.tscn`
3. 让 Terrain3D 地形和 `simulation_sandbox.tres` 的布局对齐
4. 导入一套真实草地材质
5. 导入两到三种真实树模型
6. 把 `BattleMapRenderer` 里的树替换成真实树路径

## 第二阶段

等你地形和树真实了，再做：

1. 自定义刷树工具
2. 出生点编辑器
3. 敌我颜色和编号标记
4. 海域 / 禁行区编辑
5. 导出成正式运行地图

## 现在的定位

现在这版还不是完整地图编辑器。

它是：
- 一个可继续扩展的 Godot 工程基础
- 一个可保存地图数据的资源结构
- 一个适合接 Terrain3D 和真实素材的入口
