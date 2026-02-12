using Godot;
using System;

public partial class BobberSpawner : MultiplayerSpawner
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
        Spawned += OnSpawn;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void OnSpawn(Node spawnedNode)
	{
		Log($"[BobberSpawner {Multiplayer.GetUniqueId()}] OnSpawn called for node: " + spawnedNode.Name);
	}
}
