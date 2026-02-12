using Godot;
using System;
using System.Collections.Generic;

public class Inventory
{
    public int Id;
    public Vector2I Size { get; set; }
    public List<ItemInstance> Items { get; set; }
    // Hotbar mapping: slot index -> item instance ID
    public Dictionary<int, ItemInstance> HotbarItems;
    public List<long> subscribedPlayers = new List<long>();
    public Inventory(Vector2I size, int id)
    {
        Size = size;
        Items = new List<ItemInstance>();
        HotbarItems = new Dictionary<int, ItemInstance>();
        Id = id;
        subscribedPlayers = new List<long>();
        subscribedPlayers.Add(1);
    }

    /// <summary>
    /// Notify all subscribed players by calling the provided action for each player
    /// </summary>
    /// <param name="notifyAction">Action to perform for each subscribed player (receives peerId)</param>
    public void Notify(Action<long> notifyAction)
    {
        foreach (long peerId in subscribedPlayers)
        {
            notifyAction(peerId);
        }
    }

    /// <param name="rotated">If true, checks validity for the item rotated 90 degrees relative to its current state</param>
    public int GetSpaceAt(ItemInstance item, Vector2I position, bool rotated)
    {
        Vector2I itemSize = item.Size;
        if (rotated) 
        {
            itemSize = new Vector2I(item.Size.Y, item.Size.X);
        }


        if (position.X < 0)
        {
            GD.Print("[Inventory] GetSpaceAt: Position out of bounds -X");
            return 0; // Out of bounds
        } 
        else if (position.Y < 0)
        {
            GD.Print("[Inventory] GetSpaceAt: Position out of bounds -Y");
            return 0; // Out of bounds
        }
        else if (position.X + itemSize.X > Size.X)
        {
            GD.Print("[Inventory] GetSpaceAt: Position out of bounds +X");
            return 0; // Out of bounds
        }
        else if (position.Y + itemSize.Y > Size.Y)
        {
            GD.Print("[Inventory] GetSpaceAt: Position out of bounds +Y");
            return 0; // Out of bounds
        }

        foreach (var otherItem in Items)
        {
            if (otherItem.InstanceId == item.InstanceId){
                continue; // Skip self
            }

            Vector2I otherPos = otherItem.GridPosition;
            Vector2I otherSize = otherItem.Size;

            // Check for overlap
            bool overlapX = position.X < otherPos.X + otherSize.X && position.X + itemSize.X > otherPos.X;
            bool overlapY = position.Y < otherPos.Y + otherSize.Y && position.Y + itemSize.Y > otherPos.Y;

            if (overlapX && overlapY)
            {
                if (otherItem.ItemData == item.ItemData && item.ItemData.Stackable)
                {
                    GD.Print($"[Inventory] Item overlaps, but is stackable. How many will fit here?" + (item.ItemData.StackSize - otherItem.CurrentStackSize));
                    // slot is occupied by another item of the same type
                    return item.ItemData.StackSize - otherItem.CurrentStackSize;
                }
                else
                {
                    GD.Print("[Inventory] Item overlaps with non-stackable item or different type");
                    // slot is occupied by another item
                    return 0;
                }
            }
        }
        // Empty slot - could fit a whole stack!
        return item.ItemData.StackSize;
    }

    public ItemInstance? GetItemAtPosition(Vector2I position)
    {
        foreach (var item in Items)
        {
            Vector2I itemPos = item.GridPosition;
            Vector2I itemSize = item.Size;

            if (position.X >= itemPos.X && position.X < itemPos.X + itemSize.X &&
                position.Y >= itemPos.Y && position.Y < itemPos.Y + itemSize.Y)
            {
                return item;
            }
        }
        return null;
    }

}