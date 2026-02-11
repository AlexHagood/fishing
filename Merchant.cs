using Godot;

public partial class Merchant : Npc
{
    public override string HintF => "Shop";
    
    [Export]
	public int ShopId { get; set; } = -1;

    [Export]
    public Godot.Collections.Array<ItemDefinition> ShopInventory;

	private InventoryManager _inventoryManager => GetNode<InventoryManager>("/root/InventoryManager");

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        _inventoryManager.CreateInventory(new Vector2I(6, 12), ShopId);
		if (IsMultiplayerAuthority())
		{
            if (ShopInventory != null)
            {
                foreach (ItemDefinition itemdef in ShopInventory)
                {
                    _inventoryManager.RequestSpawnInstance(itemdef.ResourcePath, ShopId, infinite: true);
                }
            }
		}

        base._Ready();
	}

    public override void InteractF(Character character)
    {
        character.OpenInventory(ShopId);
    }
}