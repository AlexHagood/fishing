using Godot;

/// <summary>
/// Physics-based item that can be picked up and thrown by the player.
/// Uses floaty physics when held. Pickup happens on InteractE.
/// </summary>
[GlobalClass]
public partial class ContainerItem : PhysItem
{
	public override string HintE { get; protected set; } = "Grab";
	public override string HintF { get; protected set; } = "Open";

	private InventoryManager _inventoryManager;

	private int _containerInventoryId;
	public override void _Ready()
	{
		_inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
		_containerInventoryId = _inventoryManager.CreateInventory(new Vector2I(4, 3));
		GD.Print("Created tacklebox with Id " + _containerInventoryId);
		base._Ready();
	}

	/// <summary>
	/// E key - Open the container inventory
	/// </summary>
	public override void InteractF(Character character)
	{
		character.OpenInventory(_containerInventoryId);
	}
}
