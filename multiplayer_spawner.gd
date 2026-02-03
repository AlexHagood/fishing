extends MultiplayerSpawner


func _enter_tree() -> void:
	spawn_function = custom_spawn_function
	print("[MultiplayerSpawner] Custom spawn function assigned.")
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	print("[MultiplayerSpawner] Custom spawn ready")

	pass # Replace with function body.

func custom_spawn_function(data: Variant) -> Node:
	print("[MultiplayerSpawner] LOOK LOOK LOOK: %s" % data["peer_id"])
	
	var player_scene = preload("res://Player/character.tscn")
	var player = player_scene.instantiate()
	player.name = str(data["peer_id"])
	player.set_multiplayer_authority(data["peer_id"])

	return player