using Godot;
using System;

public partial class Npc : StaticBody3D, IInteractable
{
    public virtual string HintE => "Talk";

    public virtual string HintF => "";

    public virtual float InteractRange => 5.0f;

	[Export(PropertyHint.ResourceType, "PackedScene")]
	public PackedScene HeldItem;


	[Export]
	public string idleAnimation;

	private AnimationPlayer _animationPlayer;

	private Node3D _heldItemNode;

    public override void _Ready()
    {

		_animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		if (!string.IsNullOrEmpty(idleAnimation))
		{
			_animationPlayer.Play(idleAnimation);
		}


		_heldItemNode = GetNode<Node3D>("CharacterArmature/Skeleton3D/RightHandAttachment/ToolPosition");
		if (HeldItem != null)
		{
			if (_heldItemNode.GetChildCount() != 0)
			{
				foreach (var child in _heldItemNode.GetChildren())
				{
					child.QueueFree();
				}
			}
			var toolScene = HeldItem.Instantiate() as Node3D;
			MeshInstance3D meshInstance = toolScene.GetNode<MeshInstance3D>("MeshInstance3D").Duplicate() as MeshInstance3D;
			_heldItemNode.AddChild(meshInstance);
		}

		

        
    }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

    public void InteractE(Character character)
    {
        GD.Print($"Talking to NPC: {Name}");
        // TODO: Implement dialogue system
    }
	
    public virtual void InteractF(Character character)
    {
        // No F interaction for NPCs
    }

    public bool CanInteract()
    {
        return true;
    }
}
