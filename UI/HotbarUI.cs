using Godot;
using System;
using System.Collections.Generic;

public partial class HotbarUI : Control
{

    public const int hotbarSize = 6;
    public int selectedSlot = 0;

    private int _lastSlot = 0;

    public int inventoryId;

    List<Panel> slots = new List<Panel>();

    private InventoryManager _inventoryManager;

    private StyleBoxFlat highlightStyle = new StyleBoxFlat()
    {
        BgColor = new Color(0, 0, 0, (float)0.25), // White with full transparency
        BorderColor = Colors.White,
        BorderWidthTop = 5,
        BorderWidthBottom = 5,
        BorderWidthLeft = 5,
        BorderWidthRight = 5
    };

    private StyleBoxFlat normalStyle = new StyleBoxFlat()
    {
        BgColor = new Color(0, 0, 0, (float)0.25), // White with full transparency
        BorderColor = Colors.White,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1
    };

    public override void _Ready()
    {
        for (int i = 0; i < hotbarSize; i++)
        {
            Panel slot = GetNode<Panel>($"Slot{i}");
            if (slot == null)
            {
                Error($"Failed to find Slot{i}!");
            }
            slot.AddThemeStyleboxOverride("panel", normalStyle);
            slots.Add(slot);
        }
        Log($"Ready - initialized {slots.Count} slots");
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");

        _inventoryManager.InventoryUpdate += (updatedInventoryId) => 
        {
            if (updatedInventoryId == inventoryId)
            {
                Refresh();
            }
        };
    }

    public void Refresh()
    {
        Log($"Refreshing hotbar UI from inventory {inventoryId}");
        

        
        Dictionary<int, ItemInstance> hotbarItems = _inventoryManager.GetInventory(inventoryId).HotbarItems;
        for (int i = 0; i < hotbarSize; i++)
        {
            Panel slotPanel = slots[i];
            ItemInstance? itemInstance = hotbarItems.ContainsKey(i) ? hotbarItems[i] : null;
            TextureRect slotTexture = slotPanel.GetNode<TextureRect>("TextureRect");
            if (itemInstance != null)
            {
                slotTexture.Texture = GD.Load<Texture2D>(itemInstance.ItemData.Icon);
            } else
            {
                slotTexture.Texture = null;
            }
        }

    }

    public void HighlightSlot(int slot)
    {
        Log($"Highlighting slot {slot}, {slots.Count} total");
        
        // Guard against invalid slot indices or uninitialized slots
        if (slot < 0 || slot >= slots.Count)
        {
            Error($"Invalid slot index: {slot} (max: {slots.Count - 1})");
            return;
        }
        
        if (_lastSlot >= 0 && _lastSlot < slots.Count && slots[_lastSlot] != null)
        {
            slots[_lastSlot].AddThemeStyleboxOverride("panel", normalStyle);
        }
        
        if (slots[slot] != null)
        {
            slots[slot].AddThemeStyleboxOverride("panel", highlightStyle);
            _lastSlot = slot;
        }
        else
        {
            Error($"Slot {slot} panel is null!");
        }
    }
}
