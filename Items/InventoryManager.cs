

using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    public int Id;
    public Vector2I Size { get; set; }
    public List<ItemInstance> Items { get; set; }
    // Hotbar mapping: slot index -> item instance ID
    public Dictionary<int, ItemInstance> HotbarItems;
    public Inventory(Vector2I size, int id)
    {
        Size = size;
        Items = new List<ItemInstance>();
        HotbarItems = new Dictionary<int, ItemInstance>();
        Id = id;
    }

    public int GetSpaceAt(ItemInstance item, Vector2I position, bool rotated)
    {
        Vector2I itemSize = rotated ? item.Size.Flip() : item.Size;


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
                otherSize = otherSize.Flip();

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

}

[GlobalClass]
public partial class InventoryManager : Node
{
    private Dictionary<int, Inventory> _Inventories = new Dictionary<int, Inventory>();
    
    // Cache for fast item lookup by instance ID
    private Dictionary<int, ItemInstance> _itemInstances = new Dictionary<int, ItemInstance>();

    int inventoryCount = 1;
    [Export]
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

    public bool ItemExists(int id)
    {
        return _itemInstances.ContainsKey(id);
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
            GD.Print("[IM] Server does not need to request state, skipping");
        }
        else 
        {
            GD.Print("[IM] Client requesting inventory state from server");
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
        GD.Print("[IM] Client received inventory state from server - Setting!");
        SetStateFromJson(state);
        GD.Print("[IM] Client received inventory state from server - Complete!");
        
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



    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MoveItem(int instanceId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {
        GD.Print($"MoveItem: {instanceId} to Inventory {targetInventoryId} at {targetPosition} rotated={rotated} count={count}");

        var item = GetItem(instanceId);
        var currentInventory = _Inventories[item.InventoryId];
        var targetInventory = _Inventories[targetInventoryId];

        int spaceAvailable = targetInventory.GetSpaceAt(item, targetPosition, rotated ^ item.IsRotated);
        ItemInstance? existingItem = targetInventory.GetItemAtPosition(targetPosition);

        
        // Item exists, try to stack it there.
        if (existingItem != null && existingItem.ItemData == item.ItemData && item.ItemData.Stackable)
        {
            if (existingItem.InstanceId == item.InstanceId)
            {
                GD.Print("[IM] Will not move item onto itself");
                return;
            }
            if (existingItem.CurrentStackSize + count > item.ItemData.StackSize)
            {
                GD.Print("[IM] Target stack full");
                throw new Exception("Target stack is already full");
            }

            if (count > item.CurrentStackSize)
            {
                GD.Print("[IM] Not enough items to transfer");
                throw new Exception("Not enough items to transfer");
            }

            if (count == item.CurrentStackSize)
            {
                GD.Print("[IM] Moving entire stack to existing stack");
                // Remove from current inventory
                currentInventory.Items.Remove(item);
                _itemInstances.Remove(item.InstanceId);
                existingItem.CurrentStackSize += count;
            }
            else
            {
                GD.Print("[IM] Moving partial stack to existing stack");
                item.CurrentStackSize -= count;
                existingItem.CurrentStackSize += count;
            }
            EmitSignal(nameof(InventoryUpdate));

        }
        // No existing item, check for space
        else
        {
            if (spaceAvailable >= item.CurrentStackSize)
            {
                if (count == item.CurrentStackSize)
                {
                    GD.Print("[IM] Moving existing itemdef to position");
                    currentInventory.Items.Remove(item);
                    item.InventoryId = targetInventoryId;
                    item.GridPosition = targetPosition;
                    item.IsRotated = rotated ^ item.IsRotated;
                    targetInventory.Items.Add(item);
                    EmitSignal(nameof(InventoryUpdate));
                    return;

                }
                else if (count < item.CurrentStackSize)
                {
                    GD.Print("[IM] Moving new itemdef to position");
                    ItemInstance newItem = new ItemInstance
                    {
                        InstanceId = ItemCount,
                        ItemData = item.ItemData,
                        InventoryId = targetInventoryId,
                        CurrentStackSize = count,
                        GridPosition = targetPosition,
                        IsRotated = rotated ^ item.IsRotated
                    };
                    item.CurrentStackSize -= count;
                    targetInventory.Items.Add(newItem);
                    _itemInstances[newItem.InstanceId] = newItem;
                    EmitSignal(nameof(InventoryUpdate));
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

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestItemMove(int itemId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {
        ItemInstance item = GetItem(itemId);
        if (GetInventory(targetInventoryId).GetSpaceAt(item, targetPosition, rotated) < count)
        {
            GD.Print("[IM] RequestItemMove: Not enough space in target inventory, aborting request.");
            return;
        }
        else {
            GD.Print("[IM] RequestItemMove: Validated locally!");
        }
        if (Multiplayer.IsServer())
        {
            Rpc(nameof(MoveItem), item.InstanceId, targetInventoryId, targetPosition, rotated, count);
        }
        else
        {
            RpcId(1, nameof(RequestItemMove), item.InstanceId, targetInventoryId, targetPosition, rotated, count);
        }
    }


    public void RequestItemRotate(ItemInstance item)
    {
        RequestItemMove(item.InstanceId, item.InventoryId, item.GridPosition, true, item.CurrentStackSize);
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

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public bool SpawnInstance(string itemResourcePath, int inventoryId, NodePath DeleteItemPath, bool check=false)
    {
        if (check)
        {
            GD.Print("[IM] Server checking spawn validity");
        }
        ItemDefinition itemDef = GD.Load<ItemDefinition>(itemResourcePath);
        var inventory = GetInventory(inventoryId);
        var newItem = new ItemInstance
        {
            InstanceId = ItemCount,
            ItemData = itemDef,
            InventoryId = inventoryId,
            CurrentStackSize = 1,
        };

        _itemInstances[newItem.InstanceId] = newItem;
        Vector2I? position = FindSpotToFitItem(newItem, inventoryId);
        GD.Print($"Space to fit item at {position}");


        // We find no valid position to spawn an item
        if (position == null)
        {
            _itemCount -= 1;
            _itemInstances.Remove(newItem.InstanceId);

            if (!check)
            {
                GD.Print("[IM] Spawning failed on non-check!");
                throw new Exception("Illegal spawn attempt");
            }
            else
            {
                return false;
            }
            
        } 
        else 
        {
        // valid spawn position found

            if (check)
            {
                _itemCount -= 1;
                _itemInstances.Remove(newItem.InstanceId);
                return true;
            }
            GD.Print("[IM] Spawning item at " + position.Value);
            MoveItem(newItem.InstanceId, inventoryId, position.Value, false, 1);
            if (!DeleteItemPath.IsEmpty)
            {
                GD.Print("[IM] Deleting spawned item from world");
                Node? node = GetNodeOrNull(DeleteItemPath);
                if (node != null)
                {
                    node.QueueFree();
                }
                else
                {
                    GD.Print("[IM] Warning: Could not find node to delete at path " + DeleteItemPath);
                }
            }
            return true;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSpawnInstance(string itemResourcePath, int inventoryId, NodePath? deleteNodePath = null)
    {
        if (deleteNodePath == null)
        {
            deleteNodePath = new NodePath();
        }
        if (Multiplayer.IsServer())
        {
            if (SpawnInstance(itemResourcePath, inventoryId, deleteNodePath, check: true))
            {
                GD.Print("[IM] Server accepts spawn instance, sending to clients");
                Rpc(nameof(SpawnInstance), itemResourcePath, inventoryId, deleteNodePath, false);
                SpawnInstance(itemResourcePath, inventoryId, deleteNodePath, false);
            }
            else
            {
                GD.Print("[IM] Server rejects spawn instance");
            }
        }
        else
        {
            GD.Print("[IM] Client requesting item spawn");
            RpcId(1, nameof(RequestSpawnInstance), itemResourcePath, inventoryId, deleteNodePath);
        }
    }


    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void CreateInventoryRpc(Vector2I size, int id)
    {
        
        GD.Print($"{Multiplayer.GetUniqueId()} Creating inventory" + size + " with specified ID " + id);
        _Inventories[id] = new Inventory(size, id);
    }

    public void CreateInventory(Vector2I size, int id)
    {
        GD.Print($"{Multiplayer.GetUniqueId()} Creating inventory" + size + " with specified ID " + id);
        _Inventories[id] = new Inventory(size, id);
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDeleteItem(ItemInstance item)
    {
        if (Multiplayer.IsServer())
        {
            GD.Print("[IM] Server deleting item instance " + item.InstanceId);
            Rpc(nameof(DeleteItem), item.InstanceId);
        }
        else
        {
            RpcId(1, nameof(RequestDeleteItem), item.InstanceId, Multiplayer.GetUniqueId());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DeleteItem(int instanceId)
    {
        var item = GetItem(instanceId);
        var inventory = GetInventory(item.InventoryId);
        inventory.Items.Remove(item);
        _itemInstances.Remove(item.InstanceId);
        EmitSignal(nameof(InventoryUpdate));
    }
    [Signal]
    public delegate void HotbarUpdateEventHandler();

    public void BindItemToSlot(int inventoryId, int slotIndex, int itemId)
    {
        Rpc(nameof(BindItemToSlotRpc), inventoryId, slotIndex, itemId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void BindItemToSlotRpc(int inventoryId, int slotIndex, int itemId)
    {
        Inventory inventory = GetInventory(inventoryId);
        ItemInstance item = GetItem(itemId);

        if (item.InventoryId != inventoryId)
        {
            GD.Print("[IM] Attempted to bind illegal item!");
            throw new Exception("Attempted to bind item that does not belong to inventory");
        }
        if (slotIndex < 0 || slotIndex > 5)
        {
            GD.Print("[IM] Attempted to bind to illegal slot!");
            throw new Exception("Attempted to bind item to invalid hotbar slot");
        }

        // If the item is already assigned to any hotbar slot, remove it from that slot first
        foreach (var kvp in inventory.HotbarItems.ToList())
        {
            if (kvp.Value.InstanceId == item.InstanceId)
            {
                inventory.HotbarItems.Remove(kvp.Key);
                break;
            }
        }

        inventory.HotbarItems[slotIndex] = item;
        EmitSignal(nameof(HotbarUpdate));
    }
}