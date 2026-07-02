extends Node

@export var camera_path: NodePath
@export var selection_radius := 0.75

@onready var camera: Camera3D = get_node(camera_path)

var _drag_start := Vector2.ZERO
var _dragging := false

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_drag_start = event.position
			_dragging = true
		else:
			_dragging = false
			_select_at(event.position)
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
		_issue_order(event.position)

func _select_at(screen_pos: Vector2) -> void:
	var hit := _raycast(screen_pos)
	if hit.is_empty():
		GameState.set_selection([])
		return
	var target: Node = hit.get("collider")
	while target and not target.is_in_group("rts_selectable"):
		target = target.get_parent()
	if target and target.is_in_group("rts_selectable"):
		GameState.set_selection([target])
	else:
		GameState.set_selection([])

func _issue_order(screen_pos: Vector2) -> void:
	var hit := _raycast(screen_pos)
	if hit.is_empty():
		return
	var target: Node = hit.get("collider")
	while target and not target.is_in_group("rts_selectable"):
		target = target.get_parent()
	var selected := GameState.selected.filter(func(n): return is_instance_valid(n) and n.is_in_group("rts_units"))
	if selected.is_empty():
		return
	if target and target.is_in_group("enemy_owned"):
		for unit in selected:
			if unit.has_method("attack"):
				unit.attack(target)
				GameRelay.send_attack(int(unit.get("net_id")), int(target.get("net_id")), target.is_in_group("rts_buildings"))
	else:
		var p: Vector3 = hit.get("position")
		for i in range(selected.size()):
			var unit = selected[i]
			var offset := Vector3((i % 4) * 1.4, 0, int(i / 4) * 1.4)
			if unit.has_method("move_to"):
				unit.move_to(p + offset)
				GameRelay.send_move(int(unit.get("net_id")), p + offset)

func _raycast(screen_pos: Vector2) -> Dictionary:
	var origin := camera.project_ray_origin(screen_pos)
	var end := origin + camera.project_ray_normal(screen_pos) * 2000.0
	var query := PhysicsRayQueryParameters3D.create(origin, end)
	return get_viewport().world_3d.direct_space_state.intersect_ray(query)
