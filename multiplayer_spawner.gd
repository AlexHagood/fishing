extends MultiplayerSpawner


func _enter_tree() -> void:
	spawn_function = custom_spawn_function


func custom_spawn_function(data: Variant) -> Node:
	var player_scene = preload("res://Player/character.tscn")
	var player = player_scene.instantiate()
	player.name = str(data["peer_id"])
	player.set_multiplayer_authority(data["peer_id"])

	return player