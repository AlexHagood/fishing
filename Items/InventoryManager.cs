

using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

#nullable enable
public class ItemInstance
{
    public int InventoryId { get; set; }
    public int InstanceId { get; set; }
    public ItemDefinition ItemData { get; set; }
    public int CurrentStackSize { get; set; }
    public Vector2I GridPosition { get; set; }
    public bool IsRotated { get; set; } = false;

    public Vector2I Size
    {
        get
        {
            if (IsRotated)
                return new Vector2I(ItemData.Size.Y, ItemData.Size.X);
            return ItemData.Size;
        }
    }
}

public class Inventory
{
    public Vector2I Size { get; set; }
    public List<ItemInstance> Items { get; set; }

    public Inventory(Vector2I size, int id)
    {
        Size = size;
        Items = new List<ItemInstance>();
        HotbarItems = new List<ItemInstance?>() {null, null, null, null, null, null};
        Id = id;
    }

    public int GetSpaceAt(ItemInstance item, Vector2I position, bool rotated)
    {
        Vector2I itemSize = rotated ? new Vector2I(item.Size.Y, item.Size.X) : item.Size;


        if (position.X < 0 || position.Y < 0 ||
            position.X + itemSize.X > Size.X ||
            position.Y + itemSize.Y > Size.Y)
        {
            return 0; // Out of bounds
        }
        foreach (var otherItem in Items)
        {
            if (otherItem.InstanceId == item.InstanceId)
                continue; // Skip self

            Vector2I otherPos = otherItem.GridPosition;
            Vector2I otherSize = otherItem.Size;
            if (otherItem.IsRotated)
                otherSize = new Vector2I(otherItem.Size.Y, otherItem.Size.X);

            // Check for overlap
            bool overlapX = position.X < otherPos.X + otherSize.X && position.X + itemSize.X > otherPos.X;
            bool overlapY = position.Y < otherPos.Y + otherSize.Y && position.Y + itemSize.Y > otherPos.Y;

            if (overlapX && overlapY)
            {
                if (otherItem.ItemData == item.ItemData && item.ItemData.Stackable)
                {
                    // slot is occupied by another item of the same type
                    return item.ItemData.StackSize - otherItem.CurrentStackSize;
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
            if (item.IsRotated)
                itemSize = new Vector2I(item.Size.Y, item.Size.X);

            if (position.X >= itemPos.X && position.X < itemPos.X + itemSize.X &&
                position.Y >= itemPos.Y && position.Y < itemPos.Y + itemSize.Y)
            {
                return item;
            }
        }
        return null;
    }


    public List<ItemInstance?> HotbarItems;

    public int Id;
}

[GlobalClass]
public partial class InventoryManager : Node
{
    private Dictionary<int, Inventory> _Inventories = new Dictionary<int, Inventory>();
    
    // Cache for fast item lookup by instance ID
    private Dictionary<int, ItemInstance> _itemInstances = new Dictionary<int, ItemInstance>();

    int inventoryCount = 1;
    
    private int _itemCount = 0;
    private int ItemCount => _itemCount++;

    [Signal]
    public delegate void InventoryUpdateEventHandler();

    public ItemInstance GetItem(int id)
    {
        if (_itemInstances.ContainsKey(id))
            return _itemInstances[id];
        throw new Exception($"Item instance not found {id}");
    }

    public Inventory GetInventory(int id)
    {
        if (_Inventories.ContainsKey(id))
            return _Inventories[id];
        throw new Exception($"Inventory not found {id}");
    }


    public void GetInventoryState()
    {
        if (Multiplayer.IsServer())
        {
            GD.Print("Server does not need to request state, skipping");
        }
        else 
        {
            GD.Print("Client requesting inventory state from server");
            RpcId(1, nameof(RequestServerState), Multiplayer.GetUniqueId());
        }
    }

    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestServerState()
    {
        string state = GetStateAsJson();
        RpcId(Multiplayer.GetRemoteSenderId(), nameof(InventoryStateCallback), state);
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void InventoryStateCallback(string state)
    {
        GD.Print("Client received inventory state from server - Setting!");
        SetStateFromJson(state);
        GD.Print("Client received inventory state from server - Complete!");
        
    }

    /// <summary>
    /// Converts the entire inventory manager state to JSON
    /// </summary>
    public string GetStateAsJson()
    {
        return this.GetStateAsJson(_Inventories, inventoryCount, _itemCount);
    }

    /// <summary>
    /// Loads inventory manager state from JSON
    /// </summary>
    public void SetStateFromJson(string json)
    {
        var (inventories, itemInstances, invCount, itmCount) = InventoryManagerExtensions.LoadStateFromJson(json);
        
        _Inventories = inventories;
        _itemInstances = itemInstances;
        inventoryCount = invCount;
        _itemCount = itmCount;
        
        EmitSignal(nameof(InventoryUpdate));
    }

    public Vector2I GetInventorySize(int inventoryId)
    {
        var inventory = GetInventory(inventoryId);
        return inventory.Size;
    }




    private void MoveItem(int instanceId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {

        var item = GetItem(instanceId);
        var currentInventory = _Inventories[item.InventoryId];
        var targetInventory = _Inventories[targetInventoryId];

        int spaceAvailable = targetInventory.GetSpaceAt(item, targetPosition, rotated);
        ItemInstance? existingItem = targetInventory.GetItemAtPosition(targetPosition);

        
        // Item exists, try to stack it there.
        if (existingItem != null && existingItem.ItemData == item.ItemData && item.ItemData.Stackable)
        {
            if (existingItem.CurrentStackSize + count > item.ItemData.StackSize)
            {
                throw new Exception("Target stack is already full");
            }

            if (count > item.CurrentStackSize)
            {
                throw new Exception("Not enough items to transfer");
            }

            if (count == item.CurrentStackSize)
            {
                // Remove from current inventory
                currentInventory.Items.Remove(item);
                _itemInstances.Remove(item.InstanceId);
                existingItem.CurrentStackSize += count;
            }
            else
            {
                item.CurrentStackSize -= count;
                existingItem.CurrentStackSize += count;
            }
        }
        // No existing item, check for space
        else
        {
            if (spaceAvailable >= item.CurrentStackSize)
            {
                if (count == item.CurrentStackSize)
                {
                    currentInventory.Items.Remove(item);
                    item.InventoryId = targetInventoryId;
                    item.GridPosition = targetPosition;
                    item.IsRotated = rotated ? !item.IsRotated : item.IsRotated;
                    targetInventory.Items.Add(item);
                    return;

                }
                else if (count < item.CurrentStackSize)
                {
                    ItemInstance newItem = new ItemInstance
                    {
                        InstanceId = count,
                        ItemData = item.ItemData,
                        InventoryId = targetInventoryId,
                        CurrentStackSize = count,
                        GridPosition = targetPosition,
                        IsRotated = rotated ? !item.IsRotated : item.IsRotated
                    };
                    item.CurrentStackSize -= count;
                    targetInventory.Items.Add(newItem);
                    _itemInstances[newItem.InstanceId] = newItem;
                    return;
                }
                else
                {
                    throw new Exception("Not enough items to transfer");
                }
            }
            else
            {
                throw new Exception("Not enough space in target inventory");
            }
        }
    }


    public void RequestItemMove(ItemInstance item, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {
        if (Multiplayer.IsServer())
        {
            MoveItem(item.InstanceId, targetInventoryId, targetPosition, rotated, count);
            EmitSignal(nameof(InventoryUpdate));
        }
        else
        {
            RpcId(1, nameof(RequestServerItemMove), item.InstanceId, targetInventoryId, targetPosition, rotated, count, Multiplayer.GetUniqueId());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestServerItemMove(int instanceId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count, long peerId)
    {
        Inventory inv = GetInventory(targetInventoryId);
        ItemInstance item = GetItem(instanceId);

        if (inv.GetSpaceAt(item, targetPosition, rotated) <= 0)
        {
            GD.Print("Character requested illegal item move!");
            RpcId(peerId, nameof(ItemMoveCallback), false, instanceId, targetInventoryId, targetPosition, rotated, count);
            return;
        } else
        {
            try
            {
                MoveItem(instanceId, targetInventoryId, targetPosition, rotated, count);
                RpcId(peerId, nameof(ItemMoveCallback), true, instanceId, targetInventoryId, targetPosition, rotated, count);
            }
            catch (Exception e)
            {
                GD.Print("Character requested super illegal item move!: " + e.Message);
                RpcId(peerId, nameof(ItemMoveCallback), false, instanceId, targetInventoryId, targetPosition, rotated, count);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ItemMoveCallback(bool status, int instanceId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {
        if (!status)
        {
            GD.Print("Server rejected item move request.");
        } else
        {
            MoveItem(instanceId, targetInventoryId, targetPosition, rotated, count);
            EmitSignal(nameof(InventoryUpdate));
        }
    }


    public void RequestItemRotate(ItemInstance item)
    {
        RequestItemMove(item, item.InventoryId, item.GridPosition, !item.IsRotated, item.CurrentStackSize);
    }

    public bool CanRotateItem(ItemInstance item)
    {
        Inventory inv = GetInventory(item.InventoryId);
        int space = inv.GetSpaceAt(item, item.GridPosition, true);
        return space >= item.CurrentStackSize;
    }

    public Vector2I? FindSpotToFitItem(ItemInstance item, int inventoryId)
    {
        Inventory inv = GetInventory(inventoryId);
        for (int x = 0; x <= inv.Size.X - item.Size.X; x++)
        {
            for (int y = 0; y <= inv.Size.Y - item.Size.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);
                int space = inv.GetSpaceAt(item, pos, item.IsRotated);
                if (space >= item.CurrentStackSize)
                {
                    return pos;
                }
            }
        }
        return null;
    }

    public bool SpawnInstance(ItemDefinition itemDef, int inventoryId)
    {
        var inventory = GetInventory(inventoryId);
        var newItem = new ItemInstance
        {
            InstanceId = ItemCount,
            ItemData = itemDef,
            InventoryId = inventoryId,
            CurrentStackSize = 1,
        };
        Vector2I? position = FindSpotToFitItem(newItem, inventoryId);
        if (position == null)
        {
            GD.Print("No space to spawn item in inventory");;
            return false;
        } else 
        {
            newItem.GridPosition = position.Value;
            inventory.Items.Add(newItem);
            _itemInstances[newItem.InstanceId] = newItem;
            return true;
        }
    }

    public void RequestSpawnInstance(ItemDefinition itemDef, int inventoryId)
    {
        if (Multiplayer.IsServer())
        {
            SpawnInstance(itemDef, inventoryId);
            EmitSignal(nameof(InventoryUpdate));
        }
        else
        {
            RpcId(1, nameof(RequestServerSpawnInstance), itemDef.ResourcePath, inventoryId, Multiplayer.GetUniqueId());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestServerSpawnInstance(string itemResourcePath, int inventoryId, long peerId)
    {
        bool res = SpawnInstance(GD.Load<ItemDefinition>(itemResourcePath), inventoryId);
        if (!res)
        {
            RpcId(peerId, nameof(SpawnInstanceCallback), false, itemResourcePath, inventoryId);
            throw new Exception("Player requested illegal item spawn! No space!");
        }
        else {
            RpcId(peerId, nameof(SpawnInstanceCallback), true, itemResourcePath, inventoryId);
        }
    }


    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnInstanceCallback(bool status, string itemResourcePath, int inventoryId)
    {
        if (!status)
        {
            GD.Print("Server rejected item spawn request.");
        } else
        {
            SpawnInstance(GD.Load<ItemDefinition>(itemResourcePath), inventoryId);
            EmitSignal(nameof(InventoryUpdate));
        }
    }

    
    public int CreateInventory(Vector2I size, int? id)
    {
        int newId;
        if (id != null)
        {
            newId = id.Value;
        }
        else
        {
            newId = inventoryCount++;
        }

        GD.Print($"{Multiplayer.GetUniqueId()} Creating inventory" + size + " with specified ID " + newId);
        _Inventories[newId] = new Inventory(size, newId);
        return newId;
    }

    public void RequestDeleteItem(ItemInstance item)
    {
        if (Multiplayer.IsServer())
        {
            var inventory = GetInventory(item.InventoryId);
            inventory.Items.Remove(item);
            _itemInstances.Remove(item.InstanceId);
            EmitSignal(nameof(InventoryUpdate));
        }
        else
        {
            RpcId(1, nameof(DeleteItemCallback), item.InstanceId, Multiplayer.GetUniqueId());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DeleteItemCallback(int instanceId)
    {
        var item = GetItem(instanceId);
        var inventory = GetInventory(item.InventoryId);
        inventory.Items.Remove(item);
        _itemInstances.Remove(item.InstanceId);
        EmitSignal(nameof(InventoryUpdate));

    }


}