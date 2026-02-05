using Godot;


// Abstract class for any item that exists in the world and can be interacted with
public partial class GameItem : WorldItem
{
	public override string HintE { get; protected set; } = "";
	public override string HintF { get; protected set; } = "Pick up";
	
	public override bool CanInteract()
	{
		return InvItemData != null;
	}

	public override void InteractF(Character character)
	{
		pickup(character);
	}
}
