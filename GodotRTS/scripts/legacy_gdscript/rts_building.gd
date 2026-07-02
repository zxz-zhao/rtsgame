extends StaticBody3D
class_name RtsBuilding

signal died(building: RtsBuilding)
signal production_finished(unit_key: String, spawn_position: Vector3)

@export var display_name := "Building"
@export var player_owned := true
@export var max_health := 500.0
@export var rally_offset := Vector3(4, 0, 0)

var net_id := 0
var health := 500.0
var selected := false
var queue: Array[String] = []
var _production_time_left := 0.0

func _ready() -> void:
	health = max_health

func _process(delta: float) -> void:
	if queue.is_empty():
		return
	_production_time_left -= delta
	if _production_time_left <= 0.0:
		var unit_key := queue.pop_front()
		production_finished.emit(unit_key, global_position + rally_offset)
		if not queue.is_empty():
			_production_time_left = _production_duration(queue[0])

func enqueue_unit(unit_key: String) -> void:
	queue.append(unit_key)
	if queue.size() == 1:
		_production_time_left = _production_duration(unit_key)

func apply_damage(amount: float) -> void:
	health = max(0.0, health - amount)
	if health <= 0.0:
		died.emit(self)
		queue_free()

func set_selected(value: bool) -> void:
	selected = value

func _production_duration(unit_key: String) -> float:
	match unit_key:
		"infantry":
			return 4.0
		"tank":
			return 8.0
		"artillery":
			return 10.0
		_:
			return 6.0
