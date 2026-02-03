using Godot;
using System;

public partial class MultiplayerSpawner : Godot.MultiplayerSpawner
{
	[Export(PropertyHint.File, "*.tscn")] 
	public String NetworkPlayer = "res://Player/character.tscn";
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
		// Only the server listens to peer connections
		if (Multiplayer.IsServer())
		{
			Multiplayer.PeerConnected += SpawnPlayer;
			
			// Spawn the server/host's own player immediately
			CallDeferred(nameof(SpawnLocalPlayer));
		}
    }

	private void SpawnLocalPlayer()
	{
		// Spawn the server/host's own player
		int localPeerId = Multiplayer.GetUniqueId();
		GD.Print($"[MultiplayerSpawner] Spawning local server player with ID: {localPeerId}");
		SpawnPlayer(localPeerId);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void SpawnPlayer(long peerId)
	{
		GD.Print($"[MultiplayerSpawner] Spawning player with peer ID: {peerId}");
		
		var scene = ResourceLoader.Load<PackedScene>(NetworkPlayer);
		if (scene == null)
		{
			GD.PrintErr($"[MultiplayerSpawner] Failed to load scene at path: {NetworkPlayer}");
			return;
		}
		var newPlayer = scene.Instantiate<Character>();
		newPlayer.Name = peerId.ToString();

		// Add to Players node (sibling of MultiplayerSpawner in main scene)
		var playersNode = GetNode("../Players");
		playersNode.CallDeferred("add_child", newPlayer);
		
		GD.Print($"[MultiplayerSpawner] Successfully spawned player {peerId}");
	}
}
