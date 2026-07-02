extends Node3D

@export var pan_speed := 24.0
@export var zoom_speed := 5.0
@export var min_height := 14.0
@export var max_height := 48.0
@export var drag_pan_sensitivity := 0.04

@onready var camera: Camera3D = $Camera3D
var _dragging := false

func _process(delta: float) -> void:
	var dir := Vector3.ZERO
	if Input.is_action_pressed("camera_pan_left"):
		dir.x -= 1.0
	if Input.is_action_pressed("camera_pan_right"):
		dir.x += 1.0
	if Input.is_action_pressed("camera_pan_forward"):
		dir.z -= 1.0
	if Input.is_action_pressed("camera_pan_back"):
		dir.z += 1.0
	if dir != Vector3.ZERO:
		global_position += dir.normalized() * pan_speed * delta

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_MIDDLE:
			_dragging = event.pressed
		elif event.button_index == MOUSE_BUTTON_WHEEL_UP and event.pressed:
			_adjust_zoom(-zoom_speed)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN and event.pressed:
			_adjust_zoom(zoom_speed)
	elif event is InputEventMouseMotion and _dragging:
		global_position.x -= event.relative.x * drag_pan_sensitivity
		global_position.z -= event.relative.y * drag_pan_sensitivity

func _adjust_zoom(amount: float) -> void:
	var p := camera.position
	p.y = clamp(p.y + amount, min_height, max_height)
	p.z = clamp(p.z + amount, min_height, max_height)
	camera.position = p
