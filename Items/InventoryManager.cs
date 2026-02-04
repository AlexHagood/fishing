

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
    public Dictionary<Vector2I, ItemInstance> Grid { get; set; }

    public Inventory(Vector2I size, int id)
    {
        Size = size;
        Grid = new Dictionary<Vector2I, ItemInstance>();
        HotbarItems = new List<ItemInstance?>() {null, null, null, null, null, null};
        Id = id;
    }

    public List<ItemInstance> GetAllItems()
    {
        return Grid.Values
            .GroupBy(item => item.InstanceId)
            .Select(group => group.First())
            .ToList();
    }

    public List<ItemInstance?> HotbarItems;

    public int Id;

    public string Serialize()
    {
        var dto = InventoryDTO.FromInventory(this);
        return dto.ToJson();
    }
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

    public override void _Ready()
    {
        if (Multiplayer.IsServer())
        {
            Multiplayer.PeerConnected += SendInventoryState;
            // Create default inventory for server/authority
            GD.Print("Creating inventory state for server/authority");
            CreateInventory(new Vector2I(5,5));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void LoadInventoryState(string serializedInventories)
    {
        GD.Print($"{Multiplayer.GetUniqueId()} loading inventory state from authority");
        var state = InventoryManagerStateDTO.FromJson(serializedInventories);
        if (state == null)
            throw new InvalidOperationException("Failed to deserialize inventory state");

        // Clear existing state
        _Inventories.Clear();
        _itemInstances.Clear();
        
        // Restore item counter
        _itemCount = state.NextItemId;
        
        // Restore all inventories
        foreach (var invDTO in state.Inventories)
        {
            var inventory = invDTO.ToInventory();
            _Inventories[inventory.Id] = inventory;
            
            // Register all items in cache
            foreach (var item in inventory.GetAllItems())
            {
                _itemInstances[item.InstanceId] = item;
            }
        }
        
        GD.Print($"Loaded {state.Inventories.Count} inventories with {_itemInstances.Count} total items");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void MoveItemRpc(int toInventoryId, int instanceId, Vector2I position, bool rotation)
    {
        ItemInstance item = IdToInstance(instanceId);
        TryTransferItemPosition(toInventoryId, item, position, rotation);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void DropItemRpc(int instanceId)
    {
        ItemInstance item = IdToInstance(instanceId);
        DropItem(item.InventoryId, item);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RotateItemRpc(int instanceId)
    {
        ItemInstance item = IdToInstance(instanceId);
        RotateItem(item);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void CreateInventoryRpc(Vector2I size)
    {
        CreateInventory(size);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void TryPushItemRpc(int inventoryId,string itemDefPath)
    {
        ItemDefinition itemDef = PathToDef(itemDefPath);
        TryPushItem(inventoryId, itemDef);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void MoveCountRpc(int toInventoryId, int instanceId, Vector2I position, bool rotateAgain, int count)
    {
        ItemInstance item = IdToInstance(instanceId);
        TrySplitStack(toInventoryId, item, count, position, rotateAgain);
    }


    

    private void SendInventoryState(long peerId)
    {

        CreateInventory(new Vector2I(5, 5), (int)peerId); // Inventory for new player

        var state = new InventoryManagerStateDTO
        {
            Inventories = _Inventories.Values
                .Select(inv => InventoryDTO.FromInventory(inv))
                .ToList(),
            NextItemId = _itemCount
        };
        string serializedState = state.ToJson();
        RpcId(peerId, nameof(LoadInventoryState), serializedState);
    }


    private ItemDefinition PathToDef(string path)
    {
        ItemDefinition? item = GD.Load<ItemDefinition>(path);
        if (item == null)
            throw new InvalidOperationException($"Failed to load ItemInstance from path: {path}");
        return item;
    }
    
    private string DefToPath(ItemDefinition item)
    {
        return item.ResourcePath;
    }

    // Fast O(1) lookup using cache
    private ItemInstance IdToInstance(int id)
    {
        if (!_itemInstances.TryGetValue(id, out ItemInstance? item))
            throw new InvalidOperationException($"No ItemInstance with ID {id} found");
        return item;
    }
    
    // Register item in cache when created
    private void RegisterItem(ItemInstance item)
    {
        _itemInstances[item.InstanceId] = item;
    }
    
    // Remove item from cache when destroyed
    private void UnregisterItem(int instanceId)
    {
        _itemInstances.Remove(instanceId);
    }


    public int CreateInventory(Vector2I size)
    {
        _Inventories[inventoryCount] = new Inventory(size, inventoryCount);
        inventoryCount++;
        return inventoryCount - 1;
    }

    public int CreateInventory(Vector2I size, int inventoryId)
    {
        _Inventories[inventoryId] = new Inventory(size, inventoryId);
        return inventoryId;
    }

    public Inventory GetInventory(int inventoryId)
    {
        if (!_Inventories.ContainsKey(inventoryId))
            throw new ArgumentException($"Invalid inventory ID: {inventoryId}");

        return _Inventories[inventoryId];
    }

    public Vector2I GetInventorySize(int inventoryId)
    {
        if (!_Inventories.ContainsKey(inventoryId))
            throw new ArgumentException($"Invalid inventory ID: {inventoryId}");

        return _Inventories[inventoryId].Size;
    }


    public string DropItem(int inventoryId, ItemInstance item)
    {
        if (!_Inventories.ContainsKey(inventoryId))
            throw new ArgumentException($"Invalid inventory ID: {inventoryId}");

        Inventory inv = _Inventories[inventoryId];

        // Remove item from grid
        for (int x = 0; x < item.Size.X; x++)
        {
            for (int y = 0; y < item.Size.Y; y++)
            {
                GD.Print($"Dropping item {item.InstanceId} from inventory {inventoryId} at position {item.GridPosition.X + x}, {item.GridPosition.Y + y}");
                Vector2I pos = new Vector2I(item.GridPosition.X + x, item.GridPosition.Y + y);
                if (!inv.Grid.Remove(pos))
                {
                    throw new InvalidOperationException($"Massive Error: Position {pos} was not occupied in inventory {inventoryId} when trying to drop item {item.InstanceId}");
                }
            }
        }

        // Unregister from cache
        UnregisterItem(item.InstanceId);

        return item.ItemData.ScenePath;
    }

    /// Check if an item fits in the inventory at any position and place it there
    public bool TryPushItem(int inventoryId, ItemDefinition item, bool rotated = false)
    {
        Inventory inv = _Inventories[inventoryId];
        for (int x = 0; x <= _Inventories[inventoryId].Size.X - item.Size.X; x++)
        {
            for (int y = 0; y <= _Inventories[inventoryId].Size.Y - item.Size.Y; y++)
            {
                Vector2I position = new Vector2I(x, y);
                if (CheckItemFits(inventoryId, item, position, rotated) > 0)
                {
                    if (inv.Grid.ContainsKey(position) &&
                    inv.Grid[position] is ItemInstance existingItem &&
                    existingItem.ItemData == item && item.Stackable)
                    {
                        // Stack onto existing item
                        AddItemToStack(inventoryId, existingItem, 1);
                        return true; // Successfully stacked
                        
                    }
                    else
                    {
                        ItemInstance newItemInstance = new ItemInstance
                        {
                            InventoryId = inventoryId,
                            InstanceId = ItemCount,
                            ItemData = item,
                            CurrentStackSize = 1,
                            GridPosition = position,
                        };
                        RegisterItem(newItemInstance);
                        AddInstanceToInventory(newItemInstance);
                        return true; // Successfully placed
                    }
                }
            }
        }
        return false; // No suitable position found
    }

    public void AddItemToStack(int inventoryId, ItemInstance item, int count)
    {
        Inventory inv = _Inventories[inventoryId];
        if (item.CurrentStackSize + count <= item.ItemData.StackSize)
        {
            item.CurrentStackSize += count;
        } else
        {
            throw new InvalidOperationException($"Cannot add {count} to stack of item {item.InstanceId}, exceeds max stack size.");
        }
        
    }

    // Move item from one inventory to another, any position in target inventory
    public bool TryTransferItem(int toInventoryId, ItemInstance item)
    {
        if (TryPushItem(toInventoryId, item.ItemData, item.IsRotated))
        {
            // Remove from source inventory
            DropItem(item.InventoryId, item);
            return true; // Successfully transferred
        }
        return false;
    }

    public int TryTransferItemPosition(int toInventoryId, ItemInstance item, Vector2I position, bool rotateAgain)
    {
        Inventory targetInv = _Inventories[toInventoryId];
        int spaceAvailable = CheckItemFits(toInventoryId, item.ItemData, position, item.IsRotated ^ rotateAgain, item.InstanceId);
        
        if (spaceAvailable > 0)
        {
            // Check if there's an existing stack at this position
            if (targetInv.Grid.ContainsKey(position) &&
                targetInv.Grid[position] is ItemInstance existingItem &&
                existingItem.ItemData == item.ItemData &&
                item.ItemData.Stackable)
            {
                // Merge stacks
                int amountToTransfer = Math.Min(item.CurrentStackSize, spaceAvailable);
                existingItem.CurrentStackSize += amountToTransfer;
                item.CurrentStackSize -= amountToTransfer;
                
                if (item.CurrentStackSize <= 0)
                {
                    // Remove original stack if empty
                    DropItem(item.InventoryId, item);
                }
                
                return amountToTransfer; // Successfully merged
            }
            else
            {
                // No existing stack, place item at position
                DropItem(item.InventoryId, item);
                item.InventoryId = toInventoryId;
                item.GridPosition = position;
                item.IsRotated = item.IsRotated ^ rotateAgain;
                AddInstanceToInventory(item);
                
                return item.CurrentStackSize; // Successfully transferred
            }
        }
        return 0;
    }

    public bool CanRotateItem(ItemInstance item)
    {
        Vector2I newSize = new Vector2I(item.Size.Y, item.Size.X);
        // Check if item fits in current position with new size
        if (item.GridPosition.X + newSize.X > _Inventories[item.InventoryId].Size.X ||
            item.GridPosition.Y + newSize.Y > _Inventories[item.InventoryId].Size.Y)
        {
            return false; // Out of bounds
        }

        for (int x = 0; x < newSize.X; x++)
        {
            for (int y = 0; y < newSize.Y; y++)
            {
                Vector2I checkPos = new Vector2I(item.GridPosition.X + x, item.GridPosition.Y + y);
                if (_Inventories[item.InventoryId].Grid.ContainsKey(checkPos))
                {
                    if (_Inventories[item.InventoryId].Grid[checkPos].InstanceId == item.InstanceId)
                    {
                        // Allow checking against itself
                        continue;
                    }
                    return false; // Space occupied 
                }
            }
        }
        return true; // Can rotate
    }

    public bool RotateItem(ItemInstance item)
    {
        if (!CanRotateItem(item))
            return false;

        // Remove item from current grid positions
        DropItem(item.InventoryId, item);

        // Toggle rotation
        item.IsRotated = !item.IsRotated;

        // Add item back to inventory at new size
        AddInstanceToInventory(item);

        return true;
    }

    private void AddInstanceToInventory(ItemInstance item)
    {
        Inventory inv = _Inventories[item.InventoryId];
        for (int ix = 0; ix < item.Size.X; ix++)
            {
                for (int iy = 0; iy < item.Size.Y; iy++)
                    {
                        Vector2I pos = new Vector2I(item.GridPosition.X + ix, item.GridPosition.Y + iy);
                        if (inv.Grid.ContainsKey(pos))
                            throw new InvalidOperationException($"Massive Error: Position {pos} is already occupied in inventory {item.InventoryId}");
                        inv.Grid[pos] = item;
                    }
            }
    }

    public int TrySplitStack(int targetInventoryId, ItemInstance item, int splitCount, Vector2I targetPosition, bool rotateAgain)
    {
        if (item.CurrentStackSize < splitCount)
            throw new InvalidOperationException($"Cannot split {splitCount} from stack of size {item.CurrentStackSize}");

        Inventory targetInv = _Inventories[targetInventoryId];
        int spaceAvailable = CheckItemFits(targetInventoryId, item.ItemData, targetPosition, item.IsRotated ^ rotateAgain);
        
        if (spaceAvailable >= splitCount)
        {
            // Check if there's an existing stack at this position
            if (targetInv.Grid.ContainsKey(targetPosition) &&
                targetInv.Grid[targetPosition] is ItemInstance existingItem &&
                existingItem.ItemData == item.ItemData &&
                item.ItemData.Stackable)
            {
                // Merge into existing stack
                int amountToTransfer = Math.Min(splitCount, spaceAvailable);
                existingItem.CurrentStackSize += amountToTransfer;
                item.CurrentStackSize -= amountToTransfer;
                
                if (item.CurrentStackSize <= 0)
                {
                    // Remove original stack if empty
                    DropItem(item.InventoryId, item);
                }
                
                return amountToTransfer; // Successfully merged
            }
            else
            {
                // No existing stack, create new item instance for split stack
                ItemInstance newItemInstance = new ItemInstance
                {
                    InventoryId = targetInventoryId,
                    InstanceId = ItemCount,
                    ItemData = item.ItemData,
                    CurrentStackSize = splitCount,
                    IsRotated = item.IsRotated ^ rotateAgain,
                    GridPosition = targetPosition,
                };
                RegisterItem(newItemInstance);
                AddInstanceToInventory(newItemInstance);
                item.CurrentStackSize -= splitCount;
                
                if (item.CurrentStackSize == 0)
                {
                    // Remove original item if stack is now empty
                    DropItem(item.InventoryId, item);
                }
                
                return splitCount; // Successfully split
            }
        }
        return 0;
    }



    public bool CheckItemFits(int inventoryId, ItemInstance item, Vector2I position)
    {
        return CheckCountFit(inventoryId, item.ItemData, position, item.CurrentStackSize, item.IsRotated, item.InstanceId);
    }

    public bool CheckCountFit(int inventoryId, ItemDefinition item, Vector2I position, int count, bool rotated, int instanceId = -1)
    {
        int fits = CheckItemFits(inventoryId, item, position, rotated, instanceId);
        return fits >= count;
    }
    
    /// <summary>
    ///  Returns how many of an item can fit at that slot
    /// </summary>
    /// <param name="inventoryId"></param>
    /// <param name="item"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public int CheckItemFits(int inventoryId, ItemDefinition item, Vector2I position, bool rotated, int instanceId = -1)
    {
        Vector2I size = rotated ? new Vector2I(item.Size.Y, item.Size.X) : item.Size;

        if (!_Inventories.ContainsKey(inventoryId))
            throw new ArgumentException($"Invalid inventory ID: {inventoryId}");

        Inventory inv = _Inventories[inventoryId];

        if (item.Stackable)
        {
            if (inv.Grid.ContainsKey(position) &&
               inv.Grid[position] is ItemInstance existingItem &&
               existingItem.ItemData == item
               && existingItem.InstanceId != instanceId)
            {
                GD.Print("ItemDef Fits " + (item.StackSize - existingItem.CurrentStackSize).ToString() + " more in stack");
                return item.StackSize - existingItem.CurrentStackSize; // How many more can be stacked
            }
        }

        if (position.X < 0 || position.Y < 0 ||
            position.X + size.X > _Inventories[inventoryId].Size.X ||
            position.Y + size.Y > _Inventories[inventoryId].Size.Y)
        {
            GD.Print("ItemDef Doesn't fit, OOB");
            return 0; // Out of bounds
        }
        
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                Vector2I checkPos = new Vector2I(position.X + x, position.Y + y);
                if (inv.Grid.ContainsKey(checkPos))
                {
                    if (inv.Grid[checkPos].InstanceId == instanceId)
                    {
                        // Allow checking against itself
                        continue;
                    }
                    GD.Print("ItemDef Doesn't fit, Occupied");
                    return 0; // Space occupied
                }
            }
        }
        GD.Print("ItemDef Fits");
        return item.StackSize; // Item fits
    }

    /// <summary>
    /// Find an item instance by its unique instance ID (uses cache for O(1) lookup)
    /// </summary>
    public ItemInstance? FindItemByInstanceId(int instanceId)
    {
        _itemInstances.TryGetValue(instanceId, out ItemInstance? item);
        return item;
    }

    /// <summary>
    /// Find an item instance by its instance ID in a specific inventory
    /// </summary>
    public ItemInstance? FindItemByInstanceId(int inventoryId, int instanceId)
    {
        if (!_itemInstances.TryGetValue(instanceId, out ItemInstance? item))
            return null;
        
        // Verify it's in the specified inventory
        return item.InventoryId == inventoryId ? item : null;
    }
    
    /// <summary>
    /// Get all items currently tracked in the system
    /// </summary>
    public IEnumerable<ItemInstance> GetAllItems()
    {
        return _itemInstances.Values;
    }
}