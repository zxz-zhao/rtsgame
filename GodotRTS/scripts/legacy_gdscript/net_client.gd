extends Node

signal request_finished(path: String, data: Dictionary)
signal request_failed(path: String, error: String)

const NETWORK_UNAVAILABLE := "NETWORK_UNAVAILABLE"

@export var server_url := "http://127.0.0.1:8080"

func _ready() -> void:
	if OS.has_feature("android"):
		server_url = "http://10.0.2.2:8080"

func guest_login(display_name := "") -> void:
	var body := {}
	if not display_name.strip_edges().is_empty():
		body["displayName"] = display_name.strip_edges()
	await post_json("/api/guest", body)

func login(username: String, password: String) -> void:
	await post_json("/api/login", {"username": username, "password": password})

func register(username: String, password: String) -> void:
	await post_json("/api/register", {"username": username, "password": password})

func get_lobby() -> void:
	await get_json("/api/lobby")

func join_match(map_name: String) -> void:
	await post_json("/api/match/join", {"mapName": map_name})

func cancel_match() -> void:
	await post_json("/api/match/cancel", {})

func create_room(map_name: String, room_name := "") -> void:
	await post_json("/api/rooms/create", {"mapName": map_name, "roomName": room_name})

func join_room(room_id: String) -> void:
	await post_json("/api/rooms/join", {"roomId": room_id})

func report_match_result(win: bool, kills: int, duration: int) -> void:
	await post_json("/api/result", {"win": int(win), "kills": kills, "duration": duration})

func get_json(path: String) -> Dictionary:
	return await _request(path, HTTPClient.METHOD_GET, {})

func post_json(path: String, body: Dictionary) -> Dictionary:
	return await _request(path, HTTPClient.METHOD_POST, body)

func _request(path: String, method: int, body: Dictionary) -> Dictionary:
	var req := HTTPRequest.new()
	add_child(req)
	var headers := ["Content-Type: application/json"]
	if not GameState.token.is_empty():
		headers.append("Authorization: Bearer %s" % GameState.token)
	var payload := "" if method == HTTPClient.METHOD_GET else JSON.stringify(body)
	var err := req.request(server_url + path, headers, method, payload)
	if err != OK:
		req.queue_free()
		request_failed.emit(path, NETWORK_UNAVAILABLE)
		return {"success": false, "error": NETWORK_UNAVAILABLE}
	var result: Array = await req.request_completed
	req.queue_free()
	var response_code: int = result[1]
	var bytes: PackedByteArray = result[3]
	if response_code <= 0 or response_code >= 500:
		request_failed.emit(path, NETWORK_UNAVAILABLE)
		return {"success": false, "error": NETWORK_UNAVAILABLE}
	var text := bytes.get_string_from_utf8()
	var parsed := JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		request_failed.emit(path, "Invalid server JSON")
		return {"success": false, "error": "Invalid server JSON"}
	request_finished.emit(path, parsed)
	if response_code == 401:
		GameState.clear_session()
	if bool(parsed.get("success", false)) and parsed.has("token"):
		GameState.set_session(parsed)
	return parsed
