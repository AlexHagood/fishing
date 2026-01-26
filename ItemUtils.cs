using Godot;

/// <summary>
/// Utility methods for working with items and inventory
/// </summary>
public static class ItemUtils
{
    /// <summary>
    /// Spawns a GameItem in the world from an ItemDefinition
    /// </summary>
    public static GameItem SpawnItemInWorld(ItemDefinition itemDef, Vector3 position, Node parent)
    {
        if (itemDef == null)
        {
            GD.PrintErr("Cannot spawn item: ItemDefinition is null!");
            return null;
        }

        if (string.IsNullOrEmpty(itemDef.WorldScenePath))
        {
            GD.PrintErr($"Cannot spawn item '{itemDef.ItemName}': WorldScenePath is not set!");
            return null;
        }

        var scene = GD.Load<PackedScene>(itemDef.WorldScenePath);
        if (scene == null)
        {
            GD.PrintErr($"Failed to load scene at '{itemDef.WorldScenePath}'!");
            return null;
        }

        var item = scene.Instantiate<GameItem>();
        if (item == null)
        {
            GD.PrintErr($"Scene at '{itemDef.WorldScenePath}' did not instantiate as GameItem!");
            return null;
        }

        item.ItemDef = itemDef;
        item.GlobalPosition = position;
        parent.AddChild(item);

        return item;
    }

    /// <summary>
    /// Creates an inventory item from a GameItem that exists in the world
    /// </summary>
    public static InvItem CreateInvItemFromWorld(GameItem worldItem)
    {
        if (worldItem?.ItemDef == null)
        {
            GD.PrintErr("Cannot create InvItem: GameItem or ItemDefinition is null!");
            return null;
        }

        var invItem = new InvItem(worldItem.ItemDef);
        invItem.gameItem = worldItem;
        return invItem;
    }

    /// <summary>
    /// Creates an inventory item from an ItemDefinition (for stackable items or items without world presence)
    /// </summary>
    public static InvItem CreateInvItemFromDefinition(ItemDefinition itemDef, int stackCount = 1)
    {
        if (itemDef == null)
        {
            GD.PrintErr("Cannot create InvItem: ItemDefinition is null!");
            return null;
        }

        var invItem = new InvItem(itemDef, stackCount);
        return invItem;
    }

    /// <summary>
    /// Attempts to stack two inventory items if they're the same type
    /// </summary>
    public static bool TryStackItems(InvItem target, InvItem source)
    {
        if (target?.itemDef == null || source?.itemDef == null)
            return false;

        // Must be same item and stackable
        if (target.itemDef != source.itemDef || target.itemDef.MaxStackSize <= 1)
            return false;

        int spaceAvailable = target.itemDef.MaxStackSize - target.stackCount;
        if (spaceAvailable <= 0)
            return false;

        int amountToTransfer = Mathf.Min(spaceAvailable, source.stackCount);
        target.stackCount += amountToTransfer;
        source.stackCount -= amountToTransfer;

        return true;
    }
}
