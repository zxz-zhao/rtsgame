extends Node

signal role_assigned(is_host: bool, seed: int)
signal peer_joined(peer_id: String)
signal peer_left()
signal command_received(data: Dictionary)

var is_network_game := false
var is_host := false
var peer_connected := false
var game_seed := 12345

var _socket := WebSocketPeer.new()
var _room_id := ""
var _connected := false

func start_network_game(room_id: String) -> void:
	_room_id = room_id
	var host := NetClient.server_url.replace("http://", "").replace("https://", "").split(":")[0]
	var err := _socket.connect_to_url("ws://%s:8081" % host)
	if err != OK:
		is_network_game = false
		return
	is_network_game = true

func _process(_delta: float) -> void:
	_socket.poll()
	var state := _socket.get_ready_state()
	if state == WebSocketPeer.STATE_OPEN and not _connected:
		_connected = true
		_send({"type": "join", "token": GameState.token, "roomId": _room_id})
	elif state == WebSocketPeer.STATE_CLOSED:
		_connected = false
		is_network_game = false
	while _socket.get_available_packet_count() > 0:
		var parsed = JSON.parse_string(_socket.get_packet().get_string_from_utf8())
		if typeof(parsed) == TYPE_DICTIONARY:
			_handle_message(parsed)

func send_command(data: Dictionary) -> void:
	if not is_network_game or not _connected:
		return
	_send({"type": "cmd", "data": data})

func send_move(net_id: int, dest: Vector3) -> void:
	send_command({"action": "move", "id": net_id, "x": dest.x, "z": dest.z})

func send_attack(attacker_id: int, target_id: int, target_is_building := false) -> void:
	send_command({"action": "attack", "id": attacker_id, "tid": target_id, "bldg": int(target_is_building)})

func _send(data: Dictionary) -> void:
	_socket.send_text(JSON.stringify(data))

func _handle_message(msg: Dictionary) -> void:
	match str(msg.get("type", "")):
		"role":
			is_host = str(msg.get("role", "")) == "host"
			game_seed = int(msg.get("seed", 12345))
			role_assigned.emit(is_host, game_seed)
		"peer_joined":
			peer_connected = true
			peer_joined.emit(str(msg.get("peerId", "")))
		"peer_left":
			peer_connected = false
			peer_left.emit()
		"cmd":
			var data = msg.get("data", {})
			if typeof(data) == TYPE_DICTIONARY:
				command_received.emit(data)
