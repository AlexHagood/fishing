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
namespace InventorySystem
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
        public bool Infinite { get; set; }

        public static ItemInstanceDTO FromItemInstance(ItemInstance item)
        {
            return new ItemInstanceDTO
            {
                InventoryId = item.InventoryId,
                InstanceId = item.Id,
                ItemDataPath = item.ItemData.ResourcePath,
                CurrentStackSize = item.Count,
                GridPositionX = item.GridPosition.X,
                GridPositionY = item.GridPosition.Y,
                IsRotated = item.IsRotated,
                Infinite = item.Infinite
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
                Id = InstanceId,
                ItemData = itemData,
                Count = CurrentStackSize,
                GridPosition = new Vector2I(GridPositionX, GridPositionY),
                IsRotated = IsRotated,
                Infinite = Infinite
            };
        }
    }

    public class InventoryDTO
    {
        public int SizeX { get; set; }
        public int SizeY { get; set; }
        public List<ItemInstanceDTO> Items { get; set; } = new();
        public int Id { get; set; }

        public static InventoryDTO FromInventory(Inventory inventory)
        {
            return new InventoryDTO
            {
                SizeX = inventory.Size.X,
                SizeY = inventory.Size.Y,
                Items = inventory.Items.Select(ItemInstanceDTO.FromItemInstance).ToList(),
                Id = inventory.Id
            };
        }

        public Inventory ToInventory()
        {
            var inventory = new Inventory(new Vector2I(SizeX, SizeY), Id);
            inventory.Items = Items.Select(dto => dto.ToItemInstance()).ToList();
            
            // Hotbar will be managed separately
            inventory.HotbarItems = new Dictionary<int, ItemInstance>();
            
            return inventory;
        }
    }
}
