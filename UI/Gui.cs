using Godot;
using System;
using System.Collections.Generic;

public partial class Gui : CanvasLayer
{
    
    private bool _inventoryOpen = false;
    private TextureRect _crosshair;
    private Texture _crosshairTexture;
    private Inventory _inventory;
    private UIWindow _inventoryWindow;
    
    // Container management
    private UIWindow _containerWindow;
    private ContainerItem _currentContainer;

    public ProgressBar progressBar;
    public ButtonHints buttonHints;

    

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
        
        // Get inventory
        _inventory = GetNode<Inventory>("Inventory");
        
        // Check if InventoryWindow exists, if not create it
        _inventoryWindow = GetNodeOrNull<UIWindow>("InventoryWindow");
        if (_inventoryWindow == null)
        {
            GD.Print("[Gui] InventoryWindow not found in scene, creating dynamically...");
            
            // Load the UIWindow scene
            var windowScene = GD.Load<PackedScene>("res://UI/UIWindow.tscn");
            if (windowScene != null)
            {
                _inventoryWindow = windowScene.Instantiate<UIWindow>();
                _inventoryWindow.Name = "InventoryWindow";
                _inventoryWindow.WindowTitle = "Inventory";
                AddChild(_inventoryWindow);
                
                GD.Print("[Gui] InventoryWindow created successfully");
            }
            else
            {
                GD.PrintErr("[Gui] Failed to load UIWindow.tscn!");
                return;
            }
        }
        
        // Set title
        _inventoryWindow.WindowTitle = "Inventory";
        
        // Move inventory into the window using SetContent (which handles sizing)
        if (_inventory.GetParent() != null)
        {
            _inventory.GetParent().RemoveChild(_inventory);
        }
        _inventoryWindow.SetContent(_inventory);
        
        // Hide the window initially
        _inventoryWindow.Hide();
        
        // Connect window closed signal
        _inventoryWindow.WindowClosed += OnInventoryWindowClosed;
        
        progressBar = GetNode<ProgressBar>("PickupBar");
        buttonHints = GetNodeOrNull<ButtonHints>("ButtonHints");
        
        if (buttonHints == null)
        {
            GD.PrintErr("[Gui] ButtonHints node not found! Please add ButtonHints scene to GUI.");
        }
        
        // Create container window (hidden initially)
        CreateContainerWindow();
    }
    
    private void CreateContainerWindow()
    {
        var windowScene = GD.Load<PackedScene>("res://UI/UIWindow.tscn");
        if (windowScene != null)
        {
            _containerWindow = windowScene.Instantiate<UIWindow>();
            _containerWindow.Name = "ContainerWindow";
            _containerWindow.WindowTitle = "Container";
            _containerWindow.Position = new Vector2(100, 100); // Offset from inventory
            AddChild(_containerWindow);
            _containerWindow.Hide();
            _containerWindow.WindowClosed += OnContainerWindowClosed;
            
            GD.Print("[Gui] ContainerWindow created successfully");
        }
        else
        {
            GD.PrintErr("[Gui] Failed to load UIWindow.tscn for container!");
        }
    }
    
    public void OpenContainer(ContainerItem container)
    {
        if (_containerWindow == null || container == null)
        {
            GD.PrintErr("[Gui] Cannot open container - window or container is null");
            return;
        }
        
        _currentContainer = container;
        var containerInventory = container.GetContainerInventory();
        
        if (containerInventory == null)
        {
            GD.PrintErr("[Gui] Container has no inventory!");
            return;
        }
        
        // Set window title to container name
        _containerWindow.WindowTitle = container.ItemName;
        
        // Move container inventory into window
        if (containerInventory.GetParent() != null && containerInventory.GetParent() != _containerWindow)
        {
            containerInventory.GetParent().RemoveChild(containerInventory);
        }
        _containerWindow.SetContent(containerInventory);
        
        // Show the window
        _containerWindow.Show();
        
        // Ensure mouse is visible
        Input.MouseMode = Input.MouseModeEnum.Visible;
        
        GD.Print($"[Gui] Opened container: {container.ItemName}");
    }
    
    private void OnContainerWindowClosed()
    {
        if (_currentContainer != null && _containerWindow != null)
        {
            // Move inventory back to container
            var containerInventory = _currentContainer.GetContainerInventory();
            if (containerInventory != null && containerInventory.GetParent() == _containerWindow)
            {
                _containerWindow.RemoveChild(containerInventory);
                _currentContainer.AddChild(containerInventory);
            }
            _currentContainer = null;
        }
        
        // If no windows are open, capture mouse
        if (!_inventoryOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private void OnInventoryWindowClosed()
    {
        // Close inventory properly when X button is clicked
        InventoryOpen = false;
    }


    private void UpdateInventoryState()
    {
        if (_inventoryOpen)
        {
            GD.Print("Inventory opened");
            _inventoryWindow.Show();
            _crosshair.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            GD.Print("Inventory closed");
            _inventoryWindow.Hide();
            _crosshair.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public void ToggleInventory()
    {
        ToggleInventoryOpen();
    }
}
