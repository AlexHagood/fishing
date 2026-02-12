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
        ClickablePrint.Log($"[InventoryManager] Hellooo world!");
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
            _itemInstances[item.InstanceId] = item;
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
        GD.Print($"MoveItem: {instanceId} to Inventory {targetInventoryId} at {targetPosition} rotated={rotated} count={count}");

        ItemInstance item = GetItem(instanceId);
        Inventory currentInventory = GetInventory(item.InventoryId);
        Inventory targetInventory = GetInventory(targetInventoryId);

        if (currentInventory.IsShop)
        {
            GD.Print("Purchasing item from shop");

        }
        else if (targetInventory.IsShop)
        {
            GD.Print("Selling item to shop");
        }

        int spaceAvailable = targetInventory.GetSpaceAt(item, targetPosition, rotated);
        ItemInstance? existingItem = targetInventory.GetItemAtPosition(targetPosition);

        var dto = InventoryDTO.FromInventory(targetInventory);
        GD.Print($"Target Inventory: {JsonSerializer.Serialize(dto)}");

        
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
                if (!item.Infinite)
                {
                    currentInventory.Items.Remove(item);
                    _itemInstances.Remove(item.InstanceId);
                }
                existingItem.CurrentStackSize += count;
            }
            else
            {
                GD.Print("[IM] Moving partial stack to existing stack");
                if (!item.Infinite)
                {
                    item.CurrentStackSize -= count;
                }
                existingItem.CurrentStackSize += count;
            }

        }
        // No existing item, check for space
        else
        {
            if (spaceAvailable >= item.CurrentStackSize)
            {
                if (count == item.CurrentStackSize && !item.Infinite)
                {
                    GD.Print("[IM] Moving existing itemdef to position");
                    currentInventory.Items.Remove(item);
                    item.InventoryId = targetInventoryId;
                    item.GridPosition = targetPosition;
                    item.IsRotated = rotated ^ item.IsRotated;
                    targetInventory.Items.Add(item);
                }
                else if (count < item.CurrentStackSize || item.Infinite)
                {
                    GD.Print("[IM] Moving new itemdef to position");
                    ItemInstance newItem = new ItemInstance
                    {
                        InstanceId = GenerateItemInstanceId(),
                        ItemData = item.ItemData,
                        InventoryId = targetInventoryId,
                        CurrentStackSize = count,
                        GridPosition = targetPosition,
                        IsRotated = rotated ^ item.IsRotated
                    };
                    if (!item.Infinite)
                    {
                        item.CurrentStackSize -= count;
                    }
                    targetInventory.Items.Add(newItem);
                    _itemInstances[newItem.InstanceId] = newItem;
                }
                else
                {
                    throw new Exception("Not enough items to transfer or invalid count? Infinite broken?");
                }
            }
            else
            {
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
        ClickablePrint.Log($"[InventoryManager] Hellooo world!");
        ItemInstance item = GetItem(itemId);
        Inventory sourceInventory = GetInventory(item.InventoryId);
        Inventory targetInventory = GetInventory(targetInventoryId);
        if (targetInventory.GetSpaceAt(item, targetPosition, rotated) < count)
        {
            GD.Print("[IM] RequestItemMove: Not enough space in target inventory, aborting request.");
            return;
        }
        else {
            GD.Print("[IM] RequestItemMove: Validated locally!");
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

            GD.Print($"Notifying subscribed clients of move {targetInventory.subscribedPlayers.ToString()}");

            targetInventory.Notify(peerId => {
                {
                    RpcId(peerId, nameof(MoveItem), item.InstanceId, targetInventoryId, targetPosition, rotated, count);
                }

            });
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
                GD.Print("-> Space to fit item at " + pos + ": " + space);
                if (space >= item.CurrentStackSize)
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
        GD.Print($"[IM] SpawnInstance called: check={check}, infinite={infinite}, InstanceId will be {newInstanceId}");
        if (check)
        {
            GD.Print("[IM] Server checking spawn validity");
        }
        ItemDefinition itemDef = GD.Load<ItemDefinition>(itemResourcePath);
        var inventory = GetInventory(inventoryId);
        var newItem = new ItemInstance
        {
            InstanceId = newInstanceId,
            ItemData = itemDef,
            InventoryId = inventoryId,
            CurrentStackSize = infinite ? itemDef.StackSize : 1,
            Infinite = infinite
        };

        _itemInstances[newItem.InstanceId] = newItem;
        inventory.Items.Add(newItem);
        GD.Print($"[IM] Created item {newItem.InstanceId}, inventory now has {inventory.Items.Count} items");
        Vector2I? position = FindSpotToFitItem(newItem, inventoryId);
        GD.Print($"[IM] Space to fit new item {newItem.InstanceId} at {position}, infinite = {infinite}");


        // We find no valid position to spawn an item
        if (position == null)
        {
            _itemInstances.Remove(newItem.InstanceId);
            inventory.Items.Remove(newItem);
            GD.Print($"[IM] No position found, cleaned up. Inventory now has {inventory.Items.Count} items");


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
                _itemInstances.Remove(newItem.InstanceId);
                inventory.Items.Remove(newItem);
                GD.Print($"[IM] Check successful, cleaned up. Inventory now has {inventory.Items.Count} items");
                return true;
            }
            // Set position directly instead of using MoveItem to avoid creating duplicates
            GD.Print("[IM] Spawning item at " + position.Value);
            newItem.GridPosition = position.Value;
            // Item already added to inventory.Items at line ~425, just set position here
            EmitSignal(nameof(InventoryUpdate), inventoryId);
            if (!DeleteItemPath.IsEmpty)
            {
                GD.Print("[IM] Deleting spawned item from world");
                Node? node = GetNodeOrNull(DeleteItemPath);
                if (node != null)
                {
                    // Call RPC on the WorldItem to delete it on all clients
                    if (node is WorldItem worldItem)
                    {
                        if (Multiplayer.IsServer())
                        {
                            GD.Print("[IM] Calling Destroy RPC on WorldItem");
                            worldItem.Rpc(nameof(worldItem.Destroy));
                        }
                    }
                }
                else
                {
                    GD.Print("[IM] Warning: Could not find node to delete at path " + DeleteItemPath);
                }
            }
            else
            {
                GD.Print("[IM] No delete path provided, not deleting any world item");
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
                GD.Print($"[IM] Server accepts spawn instance of {itemResourcePath}, sending to clients");
                inventory.Notify(peerId =>
                {
                    RpcId(peerId, nameof(SpawnInstance), itemResourcePath, inventoryId, deleteNodePath, false, infinite);
                });
            }
            else
            {
                GD.Print("[IM] Server rejects spawn instance");
            }
        }
        else
        {
            if (infinite == true)
            {
                GD.Print("[IM] Client requesting infinite item spawn");
                throw new Exception("Clients cannot spawn infinite items!");
            }
            GD.Print("[IM] Client requesting item spawn");
            RpcId(1, nameof(RequestSpawnInstance), itemResourcePath, inventoryId, deleteNodePath, false);
        }
    }

    public void CreateInventory(Vector2I size, int id, bool isShop = false)
    {
        GD.Print($"{Multiplayer.GetUniqueId()} Creating {(isShop ? "shop" : "regular")} inventory" + size + " with specified ID " + id);
        _Inventories[id] = new Inventory(size, id, isShop);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SpawnWorldItem(int itemid, long peerId)
    {
        Character requestingPlayer = _networkManager.IdToPlayer[peerId];
        ItemInstance itemInstance = GetItem(itemid);
        string scenePath = itemInstance.ItemData.ScenePath;
        
        GD.Print($"[IM] Attempting to spawn world item: {itemInstance.ItemData.Name} with scene path: '{scenePath}'");
        
        if (string.IsNullOrEmpty(scenePath) || scenePath == "res://")
        {
            GD.PrintErr($"[IM] Item '{itemInstance.ItemData.Name}' does not have a valid scene path assigned, cannot spawn in world. Path: '{scenePath}'");
            return;
        }
        
        PackedScene item = GD.Load<PackedScene>(scenePath);
        if (item == null)
        {
            GD.PrintErr($"[IM] Failed to load scene at path: {scenePath}");
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
        GD.Print("[IM] Deleting item instance " + instanceId);
        var item = GetItem(instanceId);
        if (item.Infinite)
        {
            throw new Exception("[IM] Item instance " + instanceId + " is infinite, not deleting");
        }
        var inventory = GetInventory(item.InventoryId);
        inventory.Items.Remove(item);
        inventory.HotbarItems = inventory.HotbarItems.Where(kvp => kvp.Value.InstanceId != instanceId).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        _itemInstances.Remove(item.InstanceId);
        EmitSignal(nameof(InventoryUpdate), inventory.Id);
    }




    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDeleteItem(int itemId)
    {
        if (Multiplayer.IsServer())
        {

            ItemInstance item = GetItem(itemId);
            Inventory inventory = GetInventory(item.InventoryId);
            GD.Print("[IM] Server deleting item instance " + itemId);
            Rpc(nameof(SpawnWorldItem), itemId, Multiplayer.GetRemoteSenderId());

            inventory.Notify(peerId => {
                RpcId(peerId, nameof(DeleteItem), itemId);
            });
        }
        else
        {
            GD.Print("[IM] Client requesting item deletion " + itemId);
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
        EmitSignal(nameof(InventoryUpdate), inventoryId);
        GD.Print($"[IM] Bound item {item.ItemData.Name} to hotbar slot {slotIndex}");
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
            GD.Print($"[IM] Player {requestingPeer} subscribed to inventory {inventoryId}, sending...");
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
        GD.Print($"[IM] InventorySubscribeCallback called, receiving inventory state on peer {Multiplayer.GetUniqueId()}");
        SetInventoryFromJson(jsonState);
    }


}
}