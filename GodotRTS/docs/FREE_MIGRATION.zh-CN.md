# 全量迁移到 Godot C# 的免费路线

目标：把现有 Unity/Tuanjie RTS 客户端迁到 Godot C#，尽量摆脱未来商业引擎收费变化，同时保留现有 C# 玩法经验、Node 服务端协议和美术资源。

## 当前结论

Godot 4.6.3 .NET 是目前最快的免费路线。原因是项目已有大量 C# 玩法代码，迁到 Godot C# 比改写成 GDScript 更接近原工程，也更容易复用战斗数值、网络协议、地图数据和资源流水线。

当前 Godot 客户端已经是可运行迁移切片，但还不是完整 1:1 Unity 替代品。

## 已完成

- 新 Godot 工程：`GodotRTS/`。
- C# SDK：`Godot.NET.Sdk/4.6.3`，`dotnet build` 通过。
- 主场景：`LoginScene.tscn`。
- 登录/注册/游客入口可以进入大厅。
- 大厅界面使用迁移后的 Unity UI 图，包括背景、头像、顶部栏、模式入口、任务/科技面板。
- 大厅支持快速匹配、自定义房间、全球争霸入口和地图选择。
- 地图目录已迁入 Godot C#，包含沙漠绿洲、冰雪要塞、丛林战场、城市废墟、海图群岛、全球争霸。
- 战斗场景可按所选地图生成地面、水域、道路、基地平台、树、石头、墙体和开场镜头。
- OBJ 静态道具已接入运行时地图。
- 真实模型已接入：
  - `assets/units/panzer_iv/pzIV.glb`
  - `assets/units/converted/light_tank.glb`
  - `assets/units/converted/heavy_tank.glb`
- `assets/units/converted/artillery.glb`
- `assets/units/converted/scout_helicopter.glb`
- `assets/units/infantry/character_medium.glb`
- `assets/units/aircraft/fighter_speeder.fbx`
- `assets/units/aircraft/bomber_cargo.fbx`
- 海军单位已接入 Kenney 军事 OBJ 船只模型。
- 兵工厂、船坞、炮塔和主基地外观已叠加 Kenney 军事 OBJ 建筑/炮台资源。
- 战斗玩法已具备：
  - 左键选择，框选，多选
  - 右键移动，右键攻击，Shift+右键攻击移动
  - 单位弹道、命中反馈、血条、选择圈
  - 资源、人口、生产队列
  - 建筑放置、建造菜单、施工进度和施工倒计时
  - 施工中建筑取消和 75% 金币返还
  - 电力供给/消耗、电力不足提示、断电暂停生产/收入/防御
  - 生产建筑集结点、集结旗标和出兵后自动移动
  - 己方建筑维修、金币扣费和实时 HP 刷新
  - 主基地、兵工厂、装甲工厂、坦克工厂、机场、飞机工厂、船坞、炮塔、电厂、金矿
  - 陆军、空军、海军单位目录和 Unity 原数值同步的第一版
  - AI 收入、防御和进攻波次
  - 胜利/失败结算面板
  - 战术小地图和单位/建筑标记

## 资源状态

完整 Unity 源资源已镜像到：

```text
GodotRTS/assets_migrated/
```

该目录带 `.gdignore`，避免 Godot 一次性导入 500MB 以上源资源。镜像验证结果：

- 文件数：3469
- 总大小：533134624 字节
- 缺失：0
- 大小不一致：0

已确认本机 Blender：

```powershell
E:\Blender 5.1\blender.exe
```

轻坦、重坦、火炮和侦察直升机 FBX 已通过 Blender 转出 GLB 并导入 Godot。侦察直升机已完成 Godot 侧比例、居中、血条和选择反馈校准；重坦和火炮仍需要继续校材质和细节朝向。

## 已验证命令

```powershell
cd E:\code\c++\UnityRTS
dotnet build GodotRTS\GodotRTS.csproj
& "G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe" --headless --path "E:\code\c++\UnityRTS\GodotRTS" --import
```

当前可查看截图在：

- `PreviewOutput/godot_login_preview.png`
- `PreviewOutput/godot_lobby_preview.png`
- `PreviewOutput/godot_battle_building_preview.png`
- `PreviewOutput/godot_battle_buildings_turret_preview.png`
- `PreviewOutput/godot_mixed_units_preview.png`
- `PreviewOutput/godot_light_tank_model_preview.png`
- `PreviewOutput/godot_battle_victory_preview.png`
- `PreviewOutput/godot_construction_selected.png`
- `PreviewOutput/godot_scout_helicopter_selected_v2.png`
- `PreviewOutput/godot_imported_ships_scaled_seq.png`
- `PreviewOutput/godot_imported_building_props.png`
- `PreviewOutput/godot_power_hud.png`
- `PreviewOutput/godot_power_shortage.png`
- `PreviewOutput/godot_cancel_construction_button.png`
- `PreviewOutput/godot_cancel_construction_refund.png`
- `PreviewOutput/godot_rally_point.png`
- `PreviewOutput/godot_rally_point_nohud.png`
- `PreviewOutput/godot_attack_move_v2.png`
- `PreviewOutput/godot_attack_move_combat.png`
- `PreviewOutput/godot_repair_button.png`
- `PreviewOutput/godot_repair_applied_v2.png`
- `PreviewOutput/godot_unit_repair_button.png`
- `PreviewOutput/godot_unit_repair_applied.png`
- `PreviewOutput/godot_imported_infantry_models.png`
- `PreviewOutput/godot_imported_aircraft_models.png`
- `PreviewOutput/godot_target_priority_artillery_hit.png`

## 还没达到 1:1 的部分

- Unity 所有 Prefab 还没有全部变成 Godot scene。
- FBX 没有全部转为 GLB，材质、缩放、朝向、动画也还未逐项校准。
- 当前战斗机/轰炸机、部分建筑和部分地面单位仍是 Godot 程序轮廓，不是最终模型。
- 科技升级、完整建造限制、单位技能、单位维修、阵型行为、雾中隐藏、完整战报和 Android 导出仍待迁移。
- 联机同步目前只有协议壳和命令入口，还没有完整回放/重连/状态校验。

“全部转完”的验收标准应以 `PARITY_CHECKLIST.md` 为准。
