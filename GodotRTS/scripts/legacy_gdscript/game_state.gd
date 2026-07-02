extends Node

signal selection_changed(selection: Array)
signal session_changed()

var token := ""
var user_id := ""
var username := ""
var is_guest := true
var gold := 1000
var gems := 100
var selected: Array[Node] = []

const SESSION_PATH := "user://session.cfg"

func _ready() -> void:
	load_session()

func set_session(data: Dictionary) -> void:
	token = str(data.get("token", ""))
	user_id = str(data.get("userId", ""))
	username = str(data.get("username", ""))
	is_guest = bool(data.get("isGuest", true))
	gold = int(data.get("gold", gold))
	gems = int(data.get("gems", gems))
	_save_session()
	session_changed.emit()

func load_session() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(SESSION_PATH) != OK:
		return
	token = str(cfg.get_value("session", "token", ""))
	user_id = str(cfg.get_value("session", "user_id", ""))
	username = str(cfg.get_value("session", "username", ""))
	is_guest = bool(cfg.get_value("session", "is_guest", true))
	gold = int(cfg.get_value("session", "gold", gold))
	gems = int(cfg.get_value("session", "gems", gems))

func clear_session() -> void:
	token = ""
	user_id = ""
	username = ""
	is_guest = true
	_save_session()
	session_changed.emit()

func set_selection(nodes: Array) -> void:
	for old in selected:
		if is_instance_valid(old) and old.has_method("set_selected"):
			old.set_selected(false)
	selected = nodes.filter(func(n): return is_instance_valid(n))
	for item in selected:
		if item.has_method("set_selected"):
			item.set_selected(true)
	selection_changed.emit(selected)

func _save_session() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("session", "token", token)
	cfg.set_value("session", "user_id", user_id)
	cfg.set_value("session", "username", username)
	cfg.set_value("session", "is_guest", is_guest)
	cfg.set_value("session", "gold", gold)
	cfg.set_value("session", "gems", gems)
	cfg.save(SESSION_PATH)
