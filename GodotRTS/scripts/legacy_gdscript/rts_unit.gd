extends CharacterBody3D
class_name RtsUnit

signal died(unit: RtsUnit)

@export var display_name := "Unit"
@export var player_owned := true
@export var max_health := 100.0
@export var move_speed := 6.0
@export var turn_speed := 9.0
@export var attack_damage := 10.0
@export var attack_range := 6.0
@export var attack_cooldown := 1.0

var net_id := 0
var health := 100.0
var target_position: Vector3
var attack_target: Node3D
var selected := false
var _cooldown_left := 0.0

func _ready() -> void:
	health = max_health
	target_position = global_position

func _physics_process(delta: float) -> void:
	if _cooldown_left > 0.0:
		_cooldown_left -= delta
	if is_instance_valid(attack_target):
		_process_attack(delta)
	else:
		_process_move(delta)

func move_to(world_pos: Vector3) -> void:
	attack_target = null
	target_position = world_pos

func attack(node: Node3D) -> void:
	attack_target = node

func stop() -> void:
	attack_target = null
	target_position = global_position
	velocity = Vector3.ZERO

func apply_damage(amount: float) -> void:
	health = max(0.0, health - amount)
	if health <= 0.0:
		died.emit(self)
		queue_free()

func set_selected(value: bool) -> void:
	selected = value

func _process_move(delta: float) -> void:
	var flat_delta := target_position - global_position
	flat_delta.y = 0.0
	if flat_delta.length() <= 0.15:
		velocity = Vector3.ZERO
		move_and_slide()
		return
	var direction := flat_delta.normalized()
	velocity = direction * move_speed
	look_at(global_position + direction, Vector3.UP, true)
	move_and_slide()

func _process_attack(delta: float) -> void:
	var to_target := attack_target.global_position - global_position
	to_target.y = 0.0
	if to_target.length() > attack_range:
		target_position = attack_target.global_position
		_process_move(delta)
		return
	velocity = Vector3.ZERO
	move_and_slide()
	if to_target.length() > 0.01:
		look_at(global_position + to_target.normalized(), Vector3.UP, true)
	if _cooldown_left <= 0.0:
		_cooldown_left = attack_cooldown
		if attack_target.has_method("apply_damage"):
			attack_target.apply_damage(attack_damage)
