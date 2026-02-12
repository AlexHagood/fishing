using Godot;
using System;
using System.Collections.Generic;

namespace InventorySystem
{
    public class Inventory
    {
    public int Id;
    public Vector2I Size { get; set; }
    public List<ItemInstance> Items { get; set; }
    // Hotbar mapping: slot index -> item instance ID
    public Dictionary<int, ItemInstance> HotbarItems;
    public List<long> subscribedPlayers = new List<long>();

    public bool IsShop = false;
    public Inventory(Vector2I size, int id, bool isShop = false)
    {
        Size = size;
        Items = new List<ItemInstance>();
        HotbarItems = new Dictionary<int, ItemInstance>();
        Id = id;
        IsShop = isShop;
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
            Log("GetSpaceAt: Position out of bounds -X");
            return 0; // Out of bounds
        } 
        else if (position.Y < 0)
        {
            Log("GetSpaceAt: Position out of bounds -Y");
            return 0; // Out of bounds
        }
        else if (position.X + itemSize.X > Size.X)
        {
            Log("GetSpaceAt: Position out of bounds +X");
            return 0; // Out of bounds
        }
        else if (position.Y + itemSize.Y > Size.Y)
        {
            Log("GetSpaceAt: Position out of bounds +Y");
            return 0; // Out of bounds
        }

        foreach (var otherItem in Items)
        {
            if (otherItem.Id == item.Id){
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
                    Log($"Item overlaps, but is stackable with space to fit" + (item.ItemData.StackSize - otherItem.Count));
                    // slot is occupied by another item of the same type
                    return item.ItemData.StackSize - otherItem.Count;
                }
                else
                {
                    Log($"Item {item.Name} ({item.Id}) illegally overlaps with {otherItem.Name} ({otherItem.Id})");
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

    public int CoinCount 
    {
        get
        {
            int count = 0;
            foreach (var item in Items)
            {
                if (item.ItemData.Name == "Coin")
                {
                    count += item.Count;
                }
            }
            return count;
        }
    }

}
}