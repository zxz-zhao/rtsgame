# Visual and Gameplay Parity Checklist

The Godot project runs, but it is not yet visually identical to Unity. This checklist defines what "same effect" means.

## Completed In Godot

- C# project builds with 0 errors.
- Godot import completes.
- Full Unity source asset mirror exists in `assets/unity_migrated/Assets/`.
- Login scene exists and enters lobby.
- Lobby scene exists with migrated Unity lobby art.
- Map selection feeds the battle scene.
- Runtime maps render themed terrain, water, roads, props, spawn points, and opening camera.
- Real Panzer IV GLB is used.
- Converted light tank GLB is imported and used for light tanks.
- Converted heavy tank, artillery, and scout helicopter GLBs are imported and wired to unit spawning for smoke testing.
- Scout helicopter scale, centering, health bar, and selection feedback have been calibrated in Godot.
- Kenney character GLB is imported and used for infantry, artillery infantry, and flamethrower infantry smoke tests.
- Kenney SpaceKit FBX models are imported and used for fighter and bomber smoke tests.
- OBJ props are used for map rocks, trees, walls, and environment pieces.
- Imported military OBJ ship models are used for patrol boat, destroyer, and transport ship visuals.
- Imported military OBJ props are layered into barracks, naval yard, turret, and command-base visuals.
- Unit selection, box selection, movement, attack targeting, attack-move, projectiles, health bars, and selection rings exist.
- Attack-ground exists for splash-capable ground/naval units: Ctrl+right-clicking ground orders sustained fire on that point, projectiles arc to the ground, and enemy ground units/buildings in the splash radius take falloff damage.
- Multi-unit ground orders use centered formation offsets instead of piling every unit into one corner of the destination.
- Selected player units expose a Stop command in the command panel.
- Patrol and guard commands exist: Alt+right-click patrols between current position and target, Ctrl+right-click on an allied unit assigns guard/follow behavior, and HUD hints expose the shortcuts.
- Godot command relay now understands Unity-style `move`, `attack`, `amove`, `aground`, `patrol`, `guard`, and `stop` actions for the migrated tactical orders.
- Idle guard acquisition and target-priority scoring exist: anti-air favors air targets, flamethrowers favor infantry, artillery/bombers/naval firepower favors buildings, and naval units favor naval targets.
- Real fog-of-war hiding/reveal exists for enemy units and buildings, including selection blocking, attack blocking, and minimap filtering outside player vision.
- Air units keep their flight layer when receiving move, attack-move, production rally, and debug orders.
- Naval units normalize movement, attack-move, production spawn, and rally targets to the nearest authored water strip.
- Resources, population, production queue, and build menu exist.
- Tank factory production has been corrected back to land armor/artillery; naval production remains on naval yard.
- First-pass battle research exists: command doctrine, infantry medicine, vehicle armor, ballistics, air superiority, and naval gunnery can be purchased from matching buildings and apply live unit stat/vision bonuses.
- First-pass building level upgrades exist for player buildings up to Lv.3, affecting HP, production speed, income, population, power, and turret combat stats where relevant.
- Building construction progress exists with delayed activation, HUD countdown, health/progress feedback, and production/income/defense locked until complete.
- Building placement blocks water placement, and naval yards require nearby water.
- Construction cancellation exists for player buildings under construction with 75% gold refund and selection/economy refresh.
- Power rules exist with building supply/usage, HUD power status, build-menu power hints, and low-power pause for production, income, and defense.
- Production building rally points exist: right-click ground sets a rally flag, and produced units move to the rally target.
- Player building repair exists with gold cost, HP restoration, command-panel button, and live selection HP refresh.
- Player unit repair exists with gold cost, HP restoration, command-panel button, and live selection HP refresh.
- Bottom command-bar buttons now drive real tactical commands in Godot: patrol mode, attack-ground mode, bomber bombing-run targeting, and aircraft return-to-airfield parking.
- Bombers now support first-pass Unity-style area bombing runs with start/end targeting, lane offsets across multiple selected bombers, and four-bomb splash volleys.
- Aircraft parking now supports first-pass manual return-to-airfield behavior for fighters and bombers, including parking-slot routing and parked-state visuals.
- Aircraft fuel rules now support first-pass Unity-style burn, low-fuel auto return, parked refuel recovery, no-airfield warning, and fuel-loss crashes for airfield-bound aircraft.
- Buildings exist for main base, barracks, armor factory, tank factory, airfield, air factory, naval yard, turret, power plant, and gold mine.
- Land, infantry, air, and naval unit catalog exists with first-pass Unity stats.
- AI income, defense, waves, and win/loss state exist.
- Battle HUD has economy, selection, command panel, minimap, return lobby, retry, and game-over panel.

## Asset Parity Still Needed

- Convert all priority FBX files to GLB.
- Rebuild materials: base color, normal, metallic, roughness, emission, transparency.
- Compare model scale, rotation, pivot, and origin against Unity prefabs.
- Converted heavy tank and artillery runtime models are imported and wired, with scale/orientation corrected for the current Godot slice. Final material fidelity against Unity references is still pending.
- Import animations and assign them to infantry, aircraft, and other animated units. Infantry currently uses the imported character model, but idle/run animation retargeting is still pending.
- Replace remaining procedural building stand-ins and tune temporary aircraft models against final Unity references.
- Produce Unity/Godot side-by-side screenshots for each promoted model.

## Scene Parity Still Needed

- Login scene needs pixel-level comparison against Unity.
- Lobby scene needs all secondary panels/popups, room states, rank/shop/friend views where present in Unity.
- Battle terrain needs navigation/obstacle parity with Unity `RuntimeBattleMapBuilder`.
- HUD needs Android and desktop layout parity.
- Victory/defeat/settlement screens need final art parity.

## Gameplay Parity Still Needed

- Full Unity-authored building upgrade requirements, art/VFX/audio, and per-building balance curves.
- Complete remaining unit abilities such as infantry grenade timing/VFX, collision-aware formation movement, and pathfinding polish.
- Fog-of-war polish: explored-terrain memory/shroud, sensor types, and shared/team vision.
- Naval/air polish: full water pathfinding, land/water collision separation, docking rules, transport behavior, and stricter air targeting rules.
- Multiplayer state sync, reconnection, and reconciliation.
- Android export and real-device performance verification.

## Current Verification Screenshots

- `../PreviewOutput/godot_login_preview.png`
- `../PreviewOutput/godot_lobby_preview.png`
- `../PreviewOutput/godot_battle_building_preview.png`
- `../PreviewOutput/godot_battle_buildings_turret_preview.png`
- `../PreviewOutput/godot_mixed_units_preview.png`
- `../PreviewOutput/godot_light_tank_model_preview.png`
- `../PreviewOutput/godot_battle_victory_preview.png`
- `../PreviewOutput/godot_construction_selected.png`
- `../PreviewOutput/godot_scout_helicopter_selected_v2.png`
- `../PreviewOutput/godot_imported_ships_scaled_seq.png`
- `../PreviewOutput/godot_imported_building_props.png`
- `../PreviewOutput/godot_power_hud.png`
- `../PreviewOutput/godot_power_shortage.png`
- `../PreviewOutput/godot_cancel_construction_button.png`
- `../PreviewOutput/godot_cancel_construction_refund.png`
- `../PreviewOutput/godot_rally_point.png`
- `../PreviewOutput/godot_rally_point_nohud.png`
- `../PreviewOutput/godot_attack_move_v2.png`
- `../PreviewOutput/godot_attack_move_combat.png`
- `../PreviewOutput/godot_repair_button.png`
- `../PreviewOutput/godot_repair_applied_v2.png`
- `../PreviewOutput/godot_unit_repair_button.png`
- `../PreviewOutput/godot_unit_repair_applied.png`
- `../PreviewOutput/godot_imported_infantry_models.png`
- `../PreviewOutput/godot_imported_aircraft_models.png`
- `../PreviewOutput/godot_target_priority_artillery_hit.png`
- `../PreviewOutput/godot_fog_enemy_hidden.png`
- `../PreviewOutput/godot_fog_enemy_revealed.png`
- `../PreviewOutput/godot_naval_water_constraint.png`
- `../PreviewOutput/godot_air_flight_layer_clear.png`
- `../PreviewOutput/godot_research_command_doctrine.png`
- `../PreviewOutput/godot_research_armor_bonus.png`
- `../PreviewOutput/godot_building_upgrade_mainbase.png`
- `../PreviewOutput/godot_centered_formation_move.png`
- `../PreviewOutput/godot_patrol_command.png`
- `../PreviewOutput/godot_guard_command.png`
- `../PreviewOutput/godot_unit_tactical_orders_hud.png`
- `../PreviewOutput/godot_attack_ground_artillery.png`
- `../PreviewOutput/godot_command_bar_smoke.png`
- `../PreviewOutput/godot_bomber_command_bar.png`
- `../PreviewOutput/godot_aircraft_refuel_state.png`
- `../PreviewOutput/godot_aircraft_refuel_state_v2.png`

## Mirror Verification

- Expected files: 3469.
- Missing files: 0.
- Size mismatches: 0.
- Total bytes: 533134624.

## Definition Of Done

The migration can be called fully complete only when every Unity gameplay scene has a Godot equivalent, all required runtime models/materials/animations are promoted from the mirror, single-player can complete a full match, multiplayer matches the existing server protocol, Android export runs on device, and visual parity has been checked with reference screenshots.
