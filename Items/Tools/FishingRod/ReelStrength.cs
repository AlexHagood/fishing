using Godot;
using System;

public partial class ReelStrength : Control
{
	// Called when the node enters the scene tree for the first time.

	TextureProgressBar _progressBar;
	public override void _Ready()
	{
		_progressBar = GetNode<TextureProgressBar>("CanvasLayer/TextureProgressBar");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	public void UpdateReelStrength(float strength)
	{
		_progressBar.Value = strength * 100.0f; // Assuming strength is between 0 and 1
	}
}
