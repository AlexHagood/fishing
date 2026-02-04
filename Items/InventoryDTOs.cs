using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

/// <summary>
/// Serializable representation of an ItemInstance for network transmission
/// </summary>
public class ItemInstanceDTO
{
    [JsonPropertyName("instance_id")]
    public int InstanceId { get; set; }
    
    [JsonPropertyName("inventory_id")]
    public int InventoryId { get; set; }
    
    [JsonPropertyName("item_resource_path")]
    public string ItemResourcePath { get; set; } = "";
    
    [JsonPropertyName("stack_size")]
    public int StackSize { get; set; }
    
    [JsonPropertyName("grid_x")]
    public int GridX { get; set; }
    
    [JsonPropertyName("grid_y")]
    public int GridY { get; set; }
    
    [JsonPropertyName("is_rotated")]
    public bool IsRotated { get; set; }

    /// <summary>
    /// Convert ItemInstance to DTO
    /// </summary>
    public static ItemInstanceDTO FromItemInstance(ItemInstance item)
    {
        return new ItemInstanceDTO
        {
            InstanceId = item.InstanceId,
            InventoryId = item.InventoryId,
            ItemResourcePath = item.ItemData.ResourcePath,
            StackSize = item.CurrentStackSize,
            GridX = item.GridPosition.X,
            GridY = item.GridPosition.Y,
            IsRotated = item.IsRotated
        };
    }

    /// <summary>
    /// Convert DTO back to ItemInstance
    /// </summary>
    public ItemInstance ToItemInstance()
    {
        var itemDef = GD.Load<ItemDefinition>(ItemResourcePath);
        if (itemDef == null)
            throw new InvalidOperationException($"Failed to load ItemDefinition from path: {ItemResourcePath}");

        return new ItemInstance
        {
            InstanceId = InstanceId,
            InventoryId = InventoryId,
            ItemData = itemDef,
            CurrentStackSize = StackSize,
            GridPosition = new Vector2I(GridX, GridY),
            IsRotated = IsRotated
        };
    }

    /// <summary>
    /// Serialize to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// Deserialize from JSON string
    /// </summary>
    public static ItemInstanceDTO? FromJson(string json)
    {
        return JsonSerializer.Deserialize<ItemInstanceDTO>(json);
    }
}

/// <summary>
/// Serializable representation of an Inventory for network transmission
/// </summary>
public class InventoryDTO
{
    [JsonPropertyName("inventory_id")]
    public int InventoryId { get; set; }
    
    [JsonPropertyName("size_x")]
    public int SizeX { get; set; }
    
    [JsonPropertyName("size_y")]
    public int SizeY { get; set; }
    
    [JsonPropertyName("items")]
    public List<ItemInstanceDTO> Items { get; set; } = new();
    
    [JsonPropertyName("hotbar_instance_ids")]
    public List<int?> HotbarInstanceIds { get; set; } = new();

    /// <summary>
    /// Convert Inventory to DTO
    /// </summary>
    public static InventoryDTO FromInventory(Inventory inventory)
    {
        return new InventoryDTO
        {
            InventoryId = inventory.Id,
            SizeX = inventory.Size.X,
            SizeY = inventory.Size.Y,
            Items = inventory.GetAllItems()
                .Select(ItemInstanceDTO.FromItemInstance)
                .ToList(),
            HotbarInstanceIds = inventory.HotbarItems
                .Select(item => item?.InstanceId)
                .ToList()
        };
    }

    /// <summary>
    /// Reconstruct Inventory from DTO (does not add to grid - use InventoryManager methods)
    /// </summary>
    public Inventory ToInventory()
    {
        var inventory = new Inventory(new Vector2I(SizeX, SizeY), InventoryId);
        
        // Reconstruct items and place in grid
        var itemInstances = new Dictionary<int, ItemInstance>();
        
        foreach (var itemDTO in Items)
        {
            var item = itemDTO.ToItemInstance();
            itemInstances[item.InstanceId] = item;

            // Fill grid cells for this item
            for (int x = 0; x < item.Size.X; x++)
            {
                for (int y = 0; y < item.Size.Y; y++)
                {
                    var pos = new Vector2I(item.GridPosition.X + x, item.GridPosition.Y + y);
                    inventory.Grid[pos] = item;
                }
            }
        }

        // Reconstruct hotbar references
        inventory.HotbarItems = HotbarInstanceIds
            .Select(id => id.HasValue && itemInstances.ContainsKey(id.Value) 
                ? itemInstances[id.Value] 
                : null)
            .ToList();

        return inventory;
    }

    /// <summary>
    /// Serialize to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = false 
        });
    }

    /// <summary>
    /// Deserialize from JSON string
    /// </summary>
    public static InventoryDTO? FromJson(string json)
    {
        return JsonSerializer.Deserialize<InventoryDTO>(json);
    }
}

/// <summary>
/// Serializable representation of all inventories in InventoryManager
/// </summary>
public class InventoryManagerStateDTO
{
    [JsonPropertyName("inventories")]
    public List<InventoryDTO> Inventories { get; set; } = new();
    
    [JsonPropertyName("next_item_id")]
    public int NextItemId { get; set; }

    /// <summary>
    /// Serialize to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = false 
        });
    }

    /// <summary>
    /// Deserialize from JSON string
    /// </summary>
    public static InventoryManagerStateDTO? FromJson(string json)
    {
        return JsonSerializer.Deserialize<InventoryManagerStateDTO>(json);
    }
}
