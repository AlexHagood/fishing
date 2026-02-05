using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

/// <summary>
/// Data Transfer Objects for serializing/deserializing inventory state to JSON
/// </summary>
namespace InventoryState
{
    public class ItemInstanceDTO
    {
        public int InventoryId { get; set; }
        public int InstanceId { get; set; }
        public string ItemDataPath { get; set; } = "";
        public int CurrentStackSize { get; set; }
        public int GridPositionX { get; set; }
        public int GridPositionY { get; set; }
        public bool IsRotated { get; set; }

        public static ItemInstanceDTO FromItemInstance(ItemInstance item)
        {
            return new ItemInstanceDTO
            {
                InventoryId = item.InventoryId,
                InstanceId = item.InstanceId,
                ItemDataPath = item.ItemData.ResourcePath,
                CurrentStackSize = item.CurrentStackSize,
                GridPositionX = item.GridPosition.X,
                GridPositionY = item.GridPosition.Y,
                IsRotated = item.IsRotated
            };
        }

        public ItemInstance ToItemInstance()
        {
            var itemData = GD.Load<ItemDefinition>(ItemDataPath);
            if (itemData == null)
            {
                throw new Exception($"Failed to load ItemDefinition from path: {ItemDataPath}");
            }

            return new ItemInstance
            {
                InventoryId = InventoryId,
                InstanceId = InstanceId,
                ItemData = itemData,
                CurrentStackSize = CurrentStackSize,
                GridPosition = new Vector2I(GridPositionX, GridPositionY),
                IsRotated = IsRotated
            };
        }
    }

    public class InventoryDTO
    {
        public int SizeX { get; set; }
        public int SizeY { get; set; }
        public List<ItemInstanceDTO> Items { get; set; } = new();
        public Dictionary<int, int> HotbarItemIds { get; set; } = new();
        public int Id { get; set; }

        public static InventoryDTO FromInventory(Inventory inventory)
        {
            return new InventoryDTO
            {
                SizeX = inventory.Size.X,
                SizeY = inventory.Size.Y,
                Items = inventory.Items.Select(ItemInstanceDTO.FromItemInstance).ToList(),
                HotbarItemIds = inventory.HotbarItems.ToDictionary(k => k.Key, v => v.Value.InstanceId),
                Id = inventory.Id
            };
        }

        public Inventory ToInventory()
        {
            var inventory = new Inventory(new Vector2I(SizeX, SizeY), Id);
            inventory.Items = Items.Select(dto => dto.ToItemInstance()).ToList();
            
            // Reconstruct hotbar references (will be populated after all items are loaded)
            inventory.HotbarItems = new Dictionary<int, ItemInstance>();
            
            return inventory;
        }
    }

    public class InventoryManagerStateDTO
    {
        public Dictionary<int, InventoryDTO> Inventories { get; set; } = new();
        public int InventoryCount { get; set; }
        public int ItemCount { get; set; }

        public static InventoryManagerStateDTO FromInventoryManager(
            Dictionary<int, Inventory> inventories, 
            int inventoryCount, 
            int itemCount)
        {
            return new InventoryManagerStateDTO
            {
                Inventories = inventories.ToDictionary(
                    kvp => kvp.Key,
                    kvp => InventoryDTO.FromInventory(kvp.Value)
                ),
                InventoryCount = inventoryCount,
                ItemCount = itemCount
            };
        }
    }
}

/// <summary>
/// Extension methods for InventoryManager to serialize/deserialize state
/// </summary>
public static class InventoryManagerExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Converts the entire inventory manager state to a JSON string
    /// </summary>
    public static string GetStateAsJson(
        this InventoryManager manager,
        Dictionary<int, Inventory> inventories,
        int inventoryCount,
        int itemCount)
    {
        var state = InventoryState.InventoryManagerStateDTO.FromInventoryManager(
            inventories, 
            inventoryCount, 
            itemCount
        );
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    /// <summary>
    /// Loads inventory manager state from a JSON string and returns the reconstructed data
    /// </summary>
    public static (Dictionary<int, Inventory> inventories, Dictionary<int, ItemInstance> itemInstances, int inventoryCount, int itemCount) 
        LoadStateFromJson(string json)
    {
        var state = JsonSerializer.Deserialize<InventoryState.InventoryManagerStateDTO>(json);
        if (state == null)
        {
            throw new Exception("Failed to deserialize inventory state");
        }

        var inventories = new Dictionary<int, Inventory>();
        var itemInstances = new Dictionary<int, ItemInstance>();

        // First pass: reconstruct inventories and items
        foreach (var kvp in state.Inventories)
        {
            var inventory = kvp.Value.ToInventory();
            inventories[kvp.Key] = inventory;

            // Add all items to the cache
            foreach (var item in inventory.Items)
            {
                itemInstances[item.InstanceId] = item;
            }
        }

        // Second pass: reconstruct hotbar references
        foreach (var kvp in state.Inventories)
        {
            var inventory = inventories[kvp.Key];
            var hotbarIds = kvp.Value.HotbarItemIds;

            foreach (var hotbarKvp in hotbarIds)
            {
                int slot = hotbarKvp.Key;
                int instanceId = hotbarKvp.Value;
                
                if (itemInstances.ContainsKey(instanceId))
                {
                    inventory.HotbarItems[slot] = itemInstances[instanceId];
                }
            }
        }

        return (inventories, itemInstances, state.InventoryCount, state.ItemCount);
    }
}
