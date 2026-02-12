using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TriangleNet;

#nullable enable

namespace InventorySystem
{
    [GlobalClass]
    public partial class InventoryManager : Node
    {
    private Dictionary<int, Inventory> _Inventories = new Dictionary<int, Inventory>();
    
    // Cache for fast item lookup by instance ID
    private Dictionary<int, ItemInstance> _itemInstances = new Dictionary<int, ItemInstance>();

    int inventoryCount = 1;

    [Signal]
    public delegate void InventoryUpdateEventHandler(int inventoryId);

    private NetworkManager _networkManager;

    public override void _Ready()
    {
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
    }


    /// <summary>
    /// Generates a new unique item instance ID by finding the first available ID not in use
    /// </summary>
    private int GenerateItemInstanceId()
    {
        int id = 0;
        while (_itemInstances.ContainsKey(id))
        {
            id++;
        }
        return id;
    }

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
        throw new Exception($"Inventory not found {id}, is {Multiplayer.GetUniqueId()} subscribed?");
    }
    
    public bool InventoryExists(int id)
    {
        return _Inventories.ContainsKey(id);
    }


    /// <summary>
    /// Serialize a single inventory to JSON
    /// </summary>
    public string GetInventoryAsJson(int inventoryId)
    {
        if (!InventoryExists(inventoryId))
        {
            throw new Exception($"Cannot serialize non-existent inventory {inventoryId}");
        }
        
        var inventory = GetInventory(inventoryId);
        var dto = InventoryDTO.FromInventory(inventory);
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Create/update an inventory from JSON and register all items in the item cache
    /// </summary>
    public int SetInventoryFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<InventoryDTO>(json);
        if (dto == null)
        {
            throw new Exception("Failed to deserialize inventory");
        }
        
        var inventory = dto.ToInventory();
        
        // Register all items in the global item cache
        foreach (var item in inventory.Items)
        {
            _itemInstances[item.Id] = item;
        }
        
        // Store or update the inventory
        _Inventories[inventory.Id] = inventory;
        
        EmitSignal(nameof(InventoryUpdate), inventory.Id);
        return inventory.Id;
    }

    public Vector2I GetInventorySize(int inventoryId)
    {
        var inventory = GetInventory(inventoryId);
        return inventory.Size;
    }



    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MoveItem(int instanceId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {

        ItemInstance item = GetItem(instanceId);
        Inventory currentInventory = GetInventory(item.InventoryId);
        Inventory targetInventory = GetInventory(targetInventoryId);

        if (currentInventory.IsShop)
        {
            Log("Purchasing item from shop");

        }
        else if (targetInventory.IsShop)
        {
            Log("Selling item to shop");
        }

        int spaceAvailable = targetInventory.GetSpaceAt(item, targetPosition, rotated);
        ItemInstance? existingItem = targetInventory.GetItemAtPosition(targetPosition);

        var dto = InventoryDTO.FromInventory(targetInventory);

        
        // Item exists, try to stack it there.
        if (existingItem != null && existingItem.ItemData == item.ItemData && item.ItemData.Stackable)
        {
            if (existingItem.Id == item.Id)
            {
                Log("Will not move item onto itself");
                return;
            }
            if (existingItem.Count + count > item.ItemData.StackSize)
            {
                Error($"Target stack full: {existingItem.Count} + {count} > {item.ItemData.StackSize}");
                throw new Exception("Target stack is already full");
            }

            if (count > item.Count)
            {
                Error($"Not enough items to transfer: requested {count}, available {item.Count}");
                throw new Exception("Not enough items to transfer");
            }

            if (count == item.Count)
            {
                Log($"Moving entire stack of {count} {item.Name} ({item.Id}) to existing stack of {existingItem.Count} ({existingItem.Id})");
                // Remove from current inventory
                if (!item.Infinite)
                {
                    currentInventory.Items.Remove(item);
                    _itemInstances.Remove(item.Id);
                }
                existingItem.Count += count;
            }
            else
            {
                Log($"Moving {count} to existing stack");
                if (!item.Infinite)
                {
                    item.Count -= count;
                }
                existingItem.Count += count;
            }

        }
        // No existing item, check for space
        else
        {
            if (spaceAvailable >= item.Count)
            {
                if (count == item.Count && !item.Infinite)
                {
                    Log($"Moving existing itemdef {item.Name} ({item.Id}) to position {targetPosition} in inventory {targetInventoryId} {(targetInventory.Id != currentInventory.Id ? "from {currentInventory.Id}" : "")}");
                    currentInventory.Items.Remove(item);
                    item.InventoryId = targetInventoryId;
                    item.GridPosition = targetPosition;
                    item.IsRotated = rotated ^ item.IsRotated;
                    targetInventory.Items.Add(item);
                }
                else if (count < item.Count || item.Infinite)
                {
                    ItemInstance newItem = new ItemInstance
                    {
                        Id = GenerateItemInstanceId(),
                        ItemData = item.ItemData,
                        InventoryId = targetInventoryId,
                        Count = count,
                        GridPosition = targetPosition,
                        IsRotated = rotated ^ item.IsRotated
                    };
                    Log($"Moving new itemdef {item.Name} ({newItem.Id} from {item.Id})to position {targetPosition} in inventory {targetInventoryId} {(targetInventory.Id != currentInventory.Id ? "from {currentInventory.Id}" : "")}");
                    if (!item.Infinite)
                    {
                        item.Count -= count;
                    }
                    targetInventory.Items.Add(newItem);
                    _itemInstances[newItem.Id] = newItem;
                }
                else
                {
                    Error($"Invalid move count {count} from stacksize {item.Count}");
                    throw new Exception("Not enough items to transfer or invalid count? Infinite broken?");
                }
            }
            else
            {
                Error($"Not enough space in target inventory {targetInventoryId}");
                throw new Exception("Not enough space in target inventory");
            }
        }

        EmitSignal(nameof(InventoryUpdate), currentInventory.Id);
        if (targetInventoryId != currentInventory.Id)
        {
            EmitSignal(nameof(InventoryUpdate), targetInventoryId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestItemMove(int itemId, int targetInventoryId, Vector2I targetPosition, bool rotated, int count)
    {
        ItemInstance item = GetItem(itemId);
        Inventory sourceInventory = GetInventory(item.InventoryId);
        Inventory targetInventory = GetInventory(targetInventoryId);
        if (targetInventory.GetSpaceAt(item, targetPosition, rotated) < count)
        {
            Log($"RequestItemMove: Not enough space in target inventory {targetInventoryId}, aborting request.");
            return;
        }
        if (Multiplayer.IsServer())
        {
            string sourceInventoryJson = GetInventoryAsJson(sourceInventory.Id);
            targetInventory.Notify(peerId => {
                if (!sourceInventory.subscribedPlayers.Contains(peerId))
                {
                    sourceInventory.subscribedPlayers.Add(peerId);
                    RpcId(peerId, nameof(InventorySubscribeCallback), sourceInventoryJson);
                }
            });
            string targetInventoryJson = GetInventoryAsJson(targetInventory.Id);
            sourceInventory.Notify(peerId => {
                if (!targetInventory.subscribedPlayers.Contains(peerId))
                {
                    targetInventory.subscribedPlayers.Add(peerId);
                    RpcId(peerId, nameof(InventorySubscribeCallback), targetInventoryJson);
                }
            });

            targetInventory.Notify(peerId => {
                {
                    RpcId(peerId, nameof(MoveItem), item.Id, targetInventoryId, targetPosition, rotated, count);
                }

            });
        }
        else
        {
            RpcId(1, nameof(RequestItemMove), item.Id, targetInventoryId, targetPosition, rotated, count);
        }
    }


    public void RequestItemRotate(ItemInstance item)
    {
        RequestItemMove(item.Id, item.InventoryId, item.GridPosition, true, item.Count);
    }

    public bool CanRotateItem(ItemInstance item)
    {
        Inventory inv = GetInventory(item.InventoryId);
        int space = inv.GetSpaceAt(item, item.GridPosition, true);
        return space >= item.Count;
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
                if (space >= item.Count)
                {
                    return pos;
                }
            }
        }
        return null;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public bool SpawnInstance(string itemResourcePath, int inventoryId, NodePath DeleteItemPath, bool check=false, bool infinite = false)
    {
        int newInstanceId = GenerateItemInstanceId();
        ItemDefinition itemDef = GD.Load<ItemDefinition>(itemResourcePath);
        var inventory = GetInventory(inventoryId);
        var newItem = new ItemInstance
        {
            Id = newInstanceId,
            ItemData = itemDef,
            InventoryId = inventoryId,
            Count = infinite ? itemDef.StackSize : 1,
            Infinite = infinite
        };

        _itemInstances[newItem.Id] = newItem;
        inventory.Items.Add(newItem);
        Vector2I? position = FindSpotToFitItem(newItem, inventoryId);
        



        // We find no valid position to spawn an item
        if (position == null)
        {
            _itemInstances.Remove(newItem.Id);
            inventory.Items.Remove(newItem);
            Log($"No position found, cleaned up. Inventory now has {inventory.Items.Count} items");


            if (!check)
            {
                Log("Spawning failed on non-check!");
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
                _itemInstances.Remove(newItem.Id);
                inventory.Items.Remove(newItem);
                return true;
            }
            // Set position directly instead of using MoveItem to avoid creating duplicates
            Log($"Created item {newItem.Id} ({newItem.Name}) in inventory {inventoryId} at {position.Value} {(infinite ? "infinite" : "")}");
            newItem.GridPosition = position.Value;
            // Item already added to inventory.Items at line ~425, just set position here
            EmitSignal(nameof(InventoryUpdate), inventoryId);
            if (!DeleteItemPath.IsEmpty)
            {
                Log($"Deleting spawned item from world {DeleteItemPath}");
                Node? node = GetNodeOrNull(DeleteItemPath);
                if (node != null)
                {
                    // Call RPC on the WorldItem to delete it on all clients
                    if (node is WorldItem worldItem)
                    {
                        if (Multiplayer.IsServer())
                        {
                            Log("Calling Destroy RPC on WorldItem");
                            worldItem.Rpc(nameof(worldItem.Destroy));
                        }
                    }
                }
                else
                {
                    Log("Warning: Could not find node to delete at path " + DeleteItemPath);
                }
            }
            else
            {
                Log("No delete path provided, not deleting any world item");
            }
            return true;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSpawnInstance(string itemResourcePath, int inventoryId, NodePath deleteNodePath, bool infinite)
    {
        if (Multiplayer.IsServer())
        {
            if (SpawnInstance(itemResourcePath, inventoryId, deleteNodePath, check: true, infinite: infinite))
            {
                Inventory inventory = GetInventory(inventoryId);
                Log($"Server accepts spawn instance of {itemResourcePath} in inventory {inventoryId}, sending to clients");
                inventory.Notify(peerId =>
                {
                    RpcId(peerId, nameof(SpawnInstance), itemResourcePath, inventoryId, deleteNodePath, false, infinite);
                });
            }
            else
            {
                Log($"Server rejects spawn instance of {itemResourcePath} in inventory {inventoryId}");
            }
        }
        else
        {
            if (infinite == true)
            {
                Error("Client requesting infinite item spawn");
                throw new Exception("Clients cannot spawn infinite items!");
            }
            Log("Client requesting item spawn");
            RpcId(1, nameof(RequestSpawnInstance), itemResourcePath, inventoryId, deleteNodePath, false);
        }
    }

    public void CreateInventory(Vector2I size, int id, bool isShop = false)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }
        Log($"Creating {(isShop ? "shop" : "regular")} inventory" + size + " with specified ID " + id);
        _Inventories[id] = new Inventory(size, id, isShop);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SpawnWorldItem(int itemid, long peerId)
    {
        Character requestingPlayer = _networkManager.IdToPlayer[peerId];
        ItemInstance itemInstance = GetItem(itemid);
        string scenePath = itemInstance.ItemData.ScenePath;
        
        Log($"Attempting to spawn world item: {itemInstance.ItemData.Name} with scene path: '{scenePath}' for player {peerId}");
        
        if (string.IsNullOrEmpty(scenePath) || scenePath == "res://")
        {
            Error($"Item '{itemInstance.ItemData.Name}' does not have a valid scene path assigned, cannot spawn in world. Path: '{scenePath}'");
            return;
        }
        
        PackedScene item = GD.Load<PackedScene>(scenePath);
        if (item == null)
        {
            Error($"Failed to load scene at path: {scenePath}");
            return;
        }
        
        Node instance = item.Instantiate();
        if (instance == null)
        {
            throw new Exception("Failed to instantiate item scene for " + itemInstance.ItemData.Name);
        }

        if (requestingPlayer.holdPosition != null && instance is Node3D node3D)
        {
            node3D.Position = requestingPlayer.holdPosition.GlobalPosition;
        }
        else
        {
            throw new Exception("Requesting player does not have a hold position to spawn item at or instance is invalid type");
        }
        GetNode("/root/Main").AddChild(instance);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DeleteItem(int instanceId)
    {
        Log("Deleting item instance " + instanceId);
        var item = GetItem(instanceId);
        if (item.Infinite)
        {
            throw new Exception("Item instance " + instanceId + " is infinite, not deleting");
        }
        var inventory = GetInventory(item.InventoryId);
        inventory.Items.Remove(item);
        inventory.HotbarItems = inventory.HotbarItems.Where(kvp => kvp.Value.Id != instanceId).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        _itemInstances.Remove(item.Id);
        EmitSignal(nameof(InventoryUpdate), inventory.Id);
    }




    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDeleteItem(int itemId)
    {
        ItemInstance item = GetItem(itemId);
        if (Multiplayer.IsServer())
        {

            
            Inventory inventory = GetInventory(item.InventoryId);
            Log($"Server deleting item {item.Name} instance {itemId}");
            Rpc(nameof(SpawnWorldItem), itemId, Multiplayer.GetRemoteSenderId());

            inventory.Notify(peerId => {
                RpcId(peerId, nameof(DeleteItem), itemId);
            });
        }
        else
        {
            Log($"Client requesting item {item.Name} deletion " + itemId);
            RpcId(1, nameof(RequestDeleteItem), itemId);
        }
    }


    /// <summary>
    /// Binds an item to a hotbar slot. This is local-only since hotbar state
    /// is communicated through UpdateEquippedTool RPC when the slot is actually selected.
    /// </summary>
    public void BindItemToSlot(int inventoryId, int slotIndex, int itemId)
    {
        Inventory inventory = GetInventory(inventoryId);
        ItemInstance item = GetItem(itemId);

        if (item.InventoryId != inventoryId)
        {
            Log($"Attempted to bind illegal item! item in {item.InventoryId} but inventoryId is {inventoryId}");
            throw new Exception("Attempted to bind item that does not belong to inventory");
        }
        if (slotIndex < 0 || slotIndex > 5)
        {
            Log("Attempted to bind to slot out of range!");
            throw new Exception("Attempted to bind item to invalid hotbar slot");
        }

        // If the item is already assigned to any hotbar slot, remove it from that slot first
        foreach (var kvp in inventory.HotbarItems.ToList())
        {
            if (kvp.Value.Id == item.Id)
            {
                inventory.HotbarItems.Remove(kvp.Key);
                break;
            }
        }

        inventory.HotbarItems[slotIndex] = item;
        EmitSignal(nameof(InventoryUpdate), inventoryId);
        Log($"Bound item {item.Name} to hotbar slot {slotIndex}");
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubscribeToInventory(int inventoryId)
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(1, nameof(SubscribeToInventory), inventoryId);
            return;
        }
        Inventory inventory = GetInventory(inventoryId);
        long requestingPeer = Multiplayer.GetRemoteSenderId();
        if (!inventory.subscribedPlayers.Contains(requestingPeer))
        {
            inventory.subscribedPlayers.Add(requestingPeer);
            Log($"Player {requestingPeer} subscribed to inventory {inventoryId}, sending...");
            RpcId(requestingPeer, nameof(InventorySubscribeCallback), GetInventoryAsJson(inventoryId));
        } 
        else
        {
            throw new Exception($"Player {requestingPeer} is already subscribed to inventory {inventoryId}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void InventorySubscribeCallback(string jsonState)
    {
        Log($"InventorySubscribeCallback called, receiving inventory on peer {Multiplayer.GetUniqueId()}");
        SetInventoryFromJson(jsonState);
    }


}
}