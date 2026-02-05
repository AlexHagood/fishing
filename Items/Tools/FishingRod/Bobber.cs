using Godot;
using System;

public partial class Bobber : RigidBody3D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
        GD.Print($"[Bobber] Hi everyone I'm bobber. I am owned by {GetMultiplayerAuthority()}");
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
