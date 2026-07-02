extends CanvasLayer

var _label: Label

func _ready() -> void:
	_label = Label.new()
	_label.position = Vector2(18, 18)
	_label.text = "Xinghuo RTS Godot"
	add_child(_label)
	GameState.selection_changed.connect(_on_selection_changed)
	_on_selection_changed(GameState.selected)

func _on_selection_changed(selection: Array) -> void:
	if selection.is_empty():
		_label.text = "No selection"
		return
	var names := PackedStringArray()
	for n in selection:
		var label = n.get("display_name")
		names.append(str(label) if label != null and not str(label).is_empty() else n.name)
	_label.text = "Selected: " + ", ".join(names)
