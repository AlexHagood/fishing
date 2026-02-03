using Godot;
using System;

public partial class CharAnimations : AnimationTree
{
	// Called when the node enters the scene tree for the first time.

	public float ReelTarget = -1;
	public float _reelValue = -1;

	public float WalkTarget = -1;
	public float _walkValue = -1;


	public override void _Ready()
    {
        
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
    {

	_reelValue = Mathf.MoveToward(_reelValue, ReelTarget, 3f * (float)delta);
	_walkValue = Mathf.MoveToward(_walkValue, WalkTarget, 6f * (float)delta);
	

    Set("parameters/Reeling/blend_position", _reelValue);
	Set("parameters/Walking/blend_position", _walkValue);
    }

	public void Jump()
    {
        Set("parameters/Jump/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
    }

	public void Swing()
    {
        Set("parameters/Swing/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
    }

	public void Cast()
	{
		Set("parameters/Cast/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}
}
