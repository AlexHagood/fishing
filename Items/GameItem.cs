using Godot;


// Abstract class for any item that exists in the world and can be interacted with
public partial class GameItem : WorldItem
{
	
	public override bool CanInteract()
	{
		return InvItemData != null;
	}

	public override void InteractF(Character character)
	{
		if (!CanInteract())
		{
			GD.Print("[GameItem] No ItemData set, cannot pick up");
			return;
		}

		// Get the inventory manager
		var inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
		
		// Try to add item to character's inventory
		
		bool added = inventoryManager.TryPushItem(character.inventoryId, InvItemData, false);
		
		if (added)
		{
			GD.Print($"[GameItem] Picked up {InvItemData.Name} into inventory");
			QueueFree(); // Remove the world item
		}
		else
		{
			GD.Print($"[GameItem] Inventory full, couldn't pick up {InvItemData.Name}");
		}
	}
}
