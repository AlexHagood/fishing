using System.Collections.Generic;
using Godot;

public partial class InventoryWindow : UIWindow
{
    InventoryManager inventoryManager => GetNode<InventoryManager>("/root/InventoryManager");

    public int inventoryId;

    
    private GridContainer _gridContainer;
    private Control _itemLayer; // Layer for displaying items above slots


    [Signal]
    public delegate void ItemGrabEventHandler(ItemTile item);

    private void OnItemClicked(ItemTile item)
    {
        EmitSignal(SignalName.ItemGrab, item);
    }

    public override void _Ready()
    {
        base._Ready();

        // Connect to inventory updates FIRST before any refresh attempts
        inventoryManager.InventoryUpdate += OnInventoryUpdate;

        // Subscribe to the inventory from the server
        Log($"Subscribing to inventory {inventoryId}");
        if (!Multiplayer.IsServer())
        {
            inventoryManager.SubscribeToInventory(inventoryId);
        }
        else
        {
            SetUpGridContainer();
            RefreshItems();
        }
    }

    private void OnInventoryUpdate(int updatedInventoryId)
    {
        // Only refresh if this is our inventory
        if (updatedInventoryId == inventoryId)
        {
            if (_gridContainer == null)
            {
                SetUpGridContainer();
            }
            
            Log($"Inventory {inventoryId} updated, refreshing");
            RefreshItems();
        }
    }

    public void SetUpGridContainer()
    {
        if (_gridContainer == null)
        {
            // Get the content container from the base UIWindow
            var contentContainer = GetNode<PanelContainer>("Panel/VBoxContainer/Content");
            Vector2I inventorySize = inventoryManager.GetInventorySize(inventoryId);
            // Create and add the GridContainer to the content area
            _gridContainer = new GridContainer();
            _gridContainer.AddThemeConstantOverride("v_separation", 0);
            _gridContainer.AddThemeConstantOverride("h_separation", 0);

            _gridContainer.Columns = inventorySize.X;
            contentContainer.AddChild(_gridContainer);
            
            // Create panels for each grid slot
            for (int y = 0; y < inventorySize.Y; y++)
            {
                for (int x = 0; x < inventorySize.X; x++)
                {
                    var slot = new InventorySlot();
                    slot.slotPosition = new Vector2I(x, y);
                    slot.Name = $"Slot_{x}_{y}";
                    slot.inventoryId = inventoryId;
                    slot.CustomMinimumSize = new Vector2(64, 64);
                    _gridContainer.AddChild(slot);
                }
            }
            
            // Create item layer on top of grid for displaying items
            _itemLayer = new Control();
            _itemLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
            _itemLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            contentContainer.AddChild(_itemLayer);

            // Set grid container's minimum size
            _gridContainer.CustomMinimumSize = new Vector2(inventorySize.X * 64, inventorySize.Y * 64);

            base.ResizeAndCenter();

        }
        else
        {
            GD.PrintErr("Tried to set up grid container for inventory window but it already exists");
            throw new System.Exception("Tried to set up grid container for inventory window but it already exists");
        }
    }
    
    public void RefreshItems()
    {
        // Safety check - don't try to refresh if inventory doesn't exist yet
        if (!inventoryManager.InventoryExists(inventoryId))
        {
            throw new System.Exception($"Tried to refresh inventory {inventoryId} but it doesn't exist in InventoryManager");
        }


        // Clear all existing item tiles from the item layer
        foreach (Node child in _itemLayer.GetChildren())
        {
            child.QueueFree();
        }
        
        
        // Get all items and recreate their tiles
        List<ItemInstance> items = inventoryManager.GetInventory(inventoryId).Items;
        foreach (var item in items)
        {
            
            // Create the item tile
            var itemTileScene = GD.Load<PackedScene>("res://UI/ItemTile.tscn");
            var itemTile = itemTileScene.Instantiate<ItemTile>(); 
            itemTile.ItemInstance = item;
            
            // Connect click handler
            itemTile.GuiInput += (InputEvent e) =>
            {
                if (e is InputEventMouseButton mb &&
                    mb.ButtonIndex == MouseButton.Left &&
                    mb.Pressed)
                {
                    OnItemClicked(itemTile);
                    // Mark the event as handled so Gui._Input() doesn't see it
                    itemTile.GetViewport().SetInputAsHandled();
                }
            };

            // Add to item layer
            _itemLayer.AddChild(itemTile);
        }
    }
}