using Godot;
using System;
using System.Collections.Generic;

public partial class Gui : CanvasLayer
{
    
    private bool _inventoryOpen = false;
    private TextureRect _crosshair;
    private Texture _crosshairTexture;
    private Inventory _inventory;

    public ProgressBar progressBar;

    

    public bool InventoryOpen
    {
        get => _inventoryOpen;
        set
        {
            if (_inventoryOpen != value)
            {
                _inventoryOpen = value;
                UpdateInventoryState();
            }
        }
    }

    public void ToggleInventoryOpen()
    {
        InventoryOpen = !InventoryOpen;
    }

    public override void _Ready()
    {
        _crosshair = GetNode<TextureRect>("Crosshair");
        _crosshairTexture = _crosshair.Texture;
        _crosshair.Visible = true;
        InventoryOpen = false;
        _inventory = GetNode<Inventory>("Inventory");
        _inventory.Visible = false;
        progressBar = GetNode<ProgressBar>("PickupBar");
    }


    private void UpdateInventoryState()
    {
        if (_inventoryOpen)
        {
            GD.Print("Inventory opened");
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (_crosshairTexture != null)
                Input.SetCustomMouseCursor(_crosshairTexture);

            _inventory.Visible = true;
            _crosshair.Visible = false;
        }
        else
        {
            GD.Print("Inventory closed");
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Input.SetCustomMouseCursor(null);
            _inventory.Visible = false;
            _crosshair.Visible = true;
        }
    }

    public void ToggleInventory()
    {
        ToggleInventoryOpen();
    }
}
