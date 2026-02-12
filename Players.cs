using Godot;
using System;

public partial class Players : Node3D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
        ChildEnteredTree += ChildAdded;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ChildAdded(Node child)
    {
        Log($"Child entered tree: {child.Name}");
    }
}
