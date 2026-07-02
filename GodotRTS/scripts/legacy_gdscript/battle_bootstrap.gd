extends Node3D

var _next_net_id := 1

func _ready() -> void:
	for node in get_tree().get_nodes_in_group("rts_selectable"):
		if node.get("net_id") != null:
			node.set("net_id", _next_net_id)
			_next_net_id += 1
	GameRelay.command_received.connect(_on_remote_command)

func _on_remote_command(data: Dictionary) -> void:
	match str(data.get("action", "")):
		"move":
			var unit = _find_by_net_id(int(data.get("id", 0)))
			if unit and unit.has_method("move_to"):
				unit.move_to(Vector3(float(data.get("x", 0.0)), 0.0, float(data.get("z", 0.0))))
		"attack":
			var attacker = _find_by_net_id(int(data.get("id", 0)))
			var target = _find_by_net_id(int(data.get("tid", 0)))
			if attacker and target and attacker.has_method("attack"):
				attacker.attack(target)

func _find_by_net_id(net_id: int) -> Node:
	for node in get_tree().get_nodes_in_group("rts_selectable"):
		if node.get("net_id") != null and int(node.get("net_id")) == net_id:
			return node
	return null
