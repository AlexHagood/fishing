extends MultiplayerSpawner


func _enter_tree() -> void:
	spawn_function = custom_spawn_function
	print("Custom spawn function assigned.")
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	print("Custom spawn ready")

	pass # Replace with function body.

func custom_spawn_function(data: Variant) -> Node:
	var player_scene = preload("res://Player/character.tscn")
	var player = player_scene.instantiate()
	player.name = str(data["peer_id"])
	player.set_multiplayer_authority(data["peer_id"])

	return player