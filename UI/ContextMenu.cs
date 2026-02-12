using Godot;
using System;
#nullable enable
public partial class ContextMenu : Godot.PopupMenu
{
	// Called when the node enters the scene tree for the first time.

	private InventoryManager _inventoryManager;
	
	public ItemInstance? item;
	public override void _Ready()
    {
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
		IdPressed += HandleItemSelected;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void HandleItemSelected(long id)
    {
        if (item == null)
        {
            Log("Context menu action attempted on empty slot");
            return;
        }

        switch (id)
        {
            case 0: // Drop
                Log($"Context menu: Drop item {item.ItemData.Name}");
                _inventoryManager.RequestDeleteItem(item.InstanceId);
                break;

            case 1: // Rotate
                Log($"Context menu: Rotate item {item.ItemData.Name}");
                _inventoryManager.RequestItemRotate(item);
                break;
        }

		item = null;
    }
}
