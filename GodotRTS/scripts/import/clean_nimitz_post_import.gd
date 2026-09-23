@tool
extends EditorScenePostImport

func _post_import(scene: Node) -> Object:
	if scene:
		var wake = scene.find_child("Wake", true, false)
		if wake:
			wake.free()
		for child in scene.find_children("*Wake*", "Node3D", true, false):
			child.free()
	return scene
