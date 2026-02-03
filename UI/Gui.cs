using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Godot;

#nullable enable

public partial class Gui : CanvasLayer
{
    public List<UIWindow> openWindows = new List<UIWindow>();
    protected ItemTile? _draggedItem = null;

    private ItemGhost _dragGhost = null!;
    private InventoryManager _inventoryManager = null!;
    private InventorySlot? _lastHoveredSlot = null;

    private List<InventorySlot> _highlightedSlots = new List<InventorySlot>();

    private AudioStreamPlayer _dropAudio = null!;
    private AudioStreamPlayer _pickupAudio = null!;

    HotbarUI _hotbarUI = null!;

    private bool _rotateHeld = false;


    private string HintTextF { get; set; } = "";

    private string HintTextE { get; set; } = "";

    private HBoxContainer _HintF;
    private HBoxContainer _HintE;
    private Label _HintFLabel;
    private Label _HintELabel;

    private PopupMenu _contextMenu;

    private LineEdit chatEntry;

    private TextEdit chatLog;

    private ChatManager _chatManager;


    public override void _Ready()
    {
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
        _chatManager = GetNode<ChatManager>("/root/ChatManager");

        // Connect to ChatManager's MessageAdded signal
        _chatManager.MessageAdded += OnChatMessageReceived;

        _dropAudio = GetNode<AudioStreamPlayer>("DropSound");
        _pickupAudio = GetNode<AudioStreamPlayer>("PickupSound");
        
        // Connect to Character's signals
        var character = GetNode<Character>("/root/Main/Character");
        character.InventoryRequested += OnInventoryRequested;
        character.RotateRequested += OnRotateRequested;
        character.HotbarSlotSelected += OnHotbarSlotSelected;
        character.HintEUpdated += SetHintE;
        character.HintFUpdated += SetHintF;

        _hotbarUI = GetNode<HotbarUI>("Hotbar");
        _hotbarUI.inventoryId = character.inventoryId;

        _HintF = GetNode<HBoxContainer>("ButtonHints/F");
        _HintE = GetNode<HBoxContainer>("ButtonHints/E");
        _HintFLabel = _HintF.GetNode<Label>("Label");
        _HintELabel = _HintE.GetNode<Label>("Label");

        _contextMenu = GetNode<PopupMenu>("PopupMenu");
        _contextMenu.AddItem("Drop", 0);
        _contextMenu.AddItem("Rotate", 1);
        _contextMenu.IdPressed += OnContextMenuItemSelected;

        chatEntry = GetNode<LineEdit>("ChatContainer/ChatEntry");
        chatLog = GetNode<TextEdit>("ChatContainer/ChatLog");

        // Connect text submitted event for chat entry
        chatEntry.TextSubmitted += OnChatMessageSubmitted;
    }

    // Called when ChatManager emits MessageAdded signal
    private void OnChatMessageReceived(string sender, string message)
    {
        string formattedMessage = $"[{System.DateTime.Now:HH:mm}] {sender}: {message}\n";
        chatLog.Text += formattedMessage;
        
        // Scroll to bottom to show latest message
        chatLog.ScrollVertical = (int)chatLog.GetVScrollBar().MaxValue;
    }

    // Called when user submits text in chat entry
    private void OnChatMessageSubmitted(string text)
    {
        // Don't send empty messages
        if (string.IsNullOrWhiteSpace(text))
        {
            chatEntry.Text = "";
            return;
        }

        // Add the message to ChatManager (which will emit signal to all GUIs)
        _chatManager.AddMessage("Player", text);
        
        // Clear the input field
        chatEntry.Text = "";
    }
    
    public void SetHintF(string hint)
    {
        // Null check in case signal fires before _Ready()
        if (_HintF == null || _HintFLabel == null)
            return;
            
        if (string.IsNullOrEmpty(hint))
        {
            _HintF.Visible = false;
        }
        else
        {
            _HintF.Visible = true;
            _HintFLabel.Text = hint;
        }
    }

    public void SetHintE(string hint)
    {
        // Null check in case signal fires before _Ready()
        if (_HintE == null || _HintELabel == null)
            return;
            
        if (string.IsNullOrEmpty(hint))
        {
            _HintE.Visible = false;
        }
        else
        {
            _HintE.Visible = true;
            _HintELabel.Text = hint;
        }
    }

    
    public override void _Input(InputEvent @event)
    {
        // Handle click to drop item (click-to-pick, click-to-drop behavior)
        if (@event is InputEventMouseButton mouseButton &&  
            mouseButton.Pressed)
        {

            if (_draggedItem != null) {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    GD.Print("[GUI] Left click detected for dropping item");
                    OnItemDropped(false);
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right)
                {
                    GD.Print("[GUI] Right click detected for dropping item");
                    OnItemDropped(true);
                }
                // Only drop if we're clicking on empty space or a slot, not on another item
                GetViewport().SetInputAsHandled();
            }

            if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                // Show context menu at mouse position
                Vector2 mousePos = GetViewport().GetMousePosition();
                InventorySlot? slotUnder = GetSlotAtPosition(mousePos);
                if (slotUnder != null)
                {
                    // Check if this slot actually has an item in it
                    var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
                    if (inv.Grid.ContainsKey(slotUnder.slotPosition))
                    {
                        GD.Print($"[GUI] Right-clicked on item - {inv.Grid[slotUnder.slotPosition].ItemData.Name}");
                        // There's an item here - show context menu
                        _contextMenu.Position = (Vector2I)mousePos;
                        _contextMenu.Popup();
                        GetViewport().SetInputAsHandled();
                    }
                }
            }
            
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key6)
            {
                int slotIndex = (int)keyEvent.Keycode - (int)Key.Key1;
                GD.Print("Hotbar key pressed: " + slotIndex);
                Vector2 mousePos = GetViewport().GetMousePosition();
                var slotUnder = GetSlotAtPosition(mousePos);
                if (slotUnder != null)
                {
                    Vector2I slotPos = slotUnder.slotPosition;
                    var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
                    if (inv.Grid.ContainsKey(slotPos))
                    {
                        GD.Print($"[GUI] Hotbar key pressed over item at position: {inv.Grid[slotPos].ItemData.Name}");
                        ItemInstance item = inv.Grid[slotPos];

                        int i = inv.HotbarItems.IndexOf(item);
                        GD.Print($"[GUI] Item is currently in hotbar at index: {i}");
                        if (i != -1)
                        {
                            
                            // Item is already in hotbar, swap positions
                            inv.HotbarItems[i] = null;
                        }
                        inv.HotbarItems[slotIndex] = item;
                        _hotbarUI.Refresh();
                    }
                }
            }
        }
    }
    
    public override void _Process(double delta)
    {
        // Update drag ghost position to follow mouse
        if (_dragGhost != null)
        {
            var mousePos = GetViewport().GetMousePosition();
            // Center the ghost on the cursor
            _dragGhost.Position = mousePos - (_dragGhost.Size / 2);
            
            // Get the detection point (32px in, 32px down from ghost's top-left)
            Vector2 detectionPoint = _dragGhost.Position + new Vector2(32, 32);
            
            // Find which slot is under the detection point
            var slotUnder = GetSlotAtPosition(detectionPoint);
            
            // Update highlighting
            if (slotUnder != _lastHoveredSlot)
            {
                // Unhighlight previous slot
                if (_lastHoveredSlot != null)
                {
                    foreach (var slot in _highlightedSlots)
                    {
                        slot.SetHighlight(SlotState.Default);
                    }
                }
                
                // Highlight new slot
                if (slotUnder != null)
                {
                    // Calculate target rotation
                    bool targetRotation = _draggedItem.ItemInstance.IsRotated ^ _rotateHeld;
                    
                    // Get target size based on rotation
                    Vector2I targetSize = targetRotation ? 
                        new Vector2I(_draggedItem.ItemInstance.ItemData.Size.Y, 
                                     _draggedItem.ItemInstance.ItemData.Size.X) : 
                        _draggedItem.ItemInstance.ItemData.Size;
                    
                    _highlightedSlots = slotUnder.GetSlotsForSize(targetSize);
                    
                    // Check if it fits with target rotation and ignore self
                    int spaceAvailable = _inventoryManager.CheckItemFits(
                        slotUnder.inventoryId,
                        _draggedItem.ItemInstance.ItemData,
                        slotUnder.slotPosition,
                        _draggedItem.ItemInstance.IsRotated ^ _rotateHeld,
                        _draggedItem.ItemInstance.InstanceId);
                    
                    bool validSlot = spaceAvailable > 0;
                    foreach (var slot in _highlightedSlots)
                    {
                        slot.SetHighlight(validSlot ? SlotState.Valid : SlotState.Invalid);
                    }
                    GD.Print($"[GUI] Hovering slot at position: {slotUnder.Position}");
                }
                
                _lastHoveredSlot = slotUnder;
            }
        }
    }

    public void OnHotbarSlotSelected(int slotIndex)
    {
        GD.Print($"[GUI] Hotbar slot selected: {slotIndex}");
        _hotbarUI.HighlightSlot(slotIndex);
    }
    
    private InventorySlot? GetSlotAtPosition(Vector2 globalPosition)
    {
        // Check all open inventory windows
        foreach (var window in openWindows.OfType<InventoryWindow>())
        {
            // Access the grid container directly (it's a public field)
            var contentContainer = window.GetNode<PanelContainer>("Panel/VBoxContainer/Content");
            
            // The grid is the first child of content container
            GridContainer? gridContainer = null;
            foreach (Node child in contentContainer.GetChildren())
            {
                if (child is GridContainer grid)
                {
                    gridContainer = grid;
                    break;
                }
            }
            
            if (gridContainer == null) continue;
            
            // Check each slot in the grid
            foreach (Node child in gridContainer.GetChildren())
            {
                if (child is InventorySlot slot)
                {
                    // Get the slot's global rect
                    var slotRect = slot.GetGlobalRect();
                    
                    // Check if the detection point is inside this slot
                    if (slotRect.HasPoint(globalPosition))
                    {
                        return slot;
                    }
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the detection point for the dragged item (32px offset from top-left corner)
    /// </summary>

    private void OnContextMenuItemSelected(long id)
    {
        Vector2 mousePos = GetViewport().GetMousePosition();
        var slotUnder = GetSlotAtPosition(mousePos);
        
        if (slotUnder == null)
            return;
            
        var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
        if (!inv.Grid.ContainsKey(slotUnder.slotPosition))
            return;
            
        var itemAtSlot = inv.Grid[slotUnder.slotPosition];
        
        switch (id)
        {
            case 0: // Drop
                GD.Print($"[GUI] Context menu: Drop item {itemAtSlot.ItemData.Name}");
                _inventoryManager.DropItem(slotUnder.inventoryId, itemAtSlot);
                RefreshInventoryWindows();
                break;
                
            case 1: // Rotate
                GD.Print($"[GUI] Context menu: Rotate item {itemAtSlot.ItemData.Name}");
                _inventoryManager.RotateItem(itemAtSlot);
                RefreshInventoryWindows();
                break;
        }
    }

    public void OnInventoryRequested(int id)
    {
        // Check if a window for this specific inventory is already open
        var existing = openWindows.OfType<InventoryWindow>().FirstOrDefault(w => w.inventoryId == id);
        if (existing != null)
        {
            existing.QueueFree();
        }
        else
        {
            OpenInventory(id);
        }
    }

    private void OnRotateRequested()
    {
        // Case 1: If we're dragging an item, rotate the drag ghost and update the dragged item
        if (_draggedItem != null && _dragGhost != null)
        {
            // XOR: if IsRotated and _rotateHeld match, rotate left; if they differ, rotate right
            if (_draggedItem.ItemInstance.IsRotated == _rotateHeld)
            {
                _dragGhost.RotateLeft();
            }
            else
            {
                _dragGhost.RotateRight();
            }

            _rotateHeld = !_rotateHeld;

            
            GD.Print($"[GUI] Rotate toggle. _rotateHeld: {_rotateHeld}");
            return; // Done - fit checking happens in _Process
        }
        // Case 2: If we're hovering over an item (not dragging), rotate it in place
        var mousePos = GetViewport().GetMousePosition();
        var slotUnder = GetSlotAtPosition(mousePos);
        
        if (slotUnder != null)
        {
            var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
            if (inv.Grid.ContainsKey(slotUnder.slotPosition))
            {
                var itemAtSlot = inv.Grid[slotUnder.slotPosition];
                
                // Check if we can rotate it in place
                if (_inventoryManager.CanRotateItem(itemAtSlot))
                {
                    _inventoryManager.RotateItem(itemAtSlot);
                    RefreshInventoryWindows();
                    GD.Print($"[GUI] Rotated item {itemAtSlot.InstanceId} in place. IsRotated: {itemAtSlot.IsRotated}");
                }
                else
                {
                    GD.Print("[GUI] Cannot rotate item - not enough space");
                }
            }
        }
    }

    private void OpenInventory(int id)
    {
        // Load and instantiate the inventory window directly
        var inventoryScene = GD.Load<PackedScene>("res://UI/inventory.tscn");
        var inventoryWindow = inventoryScene.Instantiate<InventoryWindow>();
        inventoryWindow.inventoryId = id;
        
        
        // Connect the ItemGrab signal using += syntax
        inventoryWindow.ItemGrab += OnItemGrabbed;
        
        // Set the window title
        var titleLabel = inventoryWindow.GetNode<Label>("Panel/VBoxContainer/StatusBar/HBoxContainer/Label");
        titleLabel.Text = "Inventory";
        
        // Add to scene
        AddChild(inventoryWindow);

        OnWindowOpened(inventoryWindow);

        GD.Print($"[GUI] Inventory opened. Total windows: {openWindows.Count}");
    }
    
    private void OnItemGrabbed(ItemTile itemTile)
    {
        GD.Print($"[GUI] Item grabbed: {itemTile.ItemInstance.InstanceId}");
        _draggedItem = itemTile;
        _dragGhost = new ItemGhost();
        _dragGhost.MouseDefaultCursorShape = Control.CursorShape.Move;
        _pickupAudio.Play();
        AddChild(_dragGhost);
        _dragGhost.setup(itemTile);
    }
    
    private void OnItemDropped(bool split)
    {
        if (_draggedItem == null || _dragGhost == null)
            return;
        
        // Get the slot under the detection point
        Vector2 detectionPoint = _dragGhost.Position + new Vector2(32, 32);
        var targetSlot = GetSlotAtPosition(detectionPoint);
        
        if (targetSlot != null)
        {
            // Check if the item fits in the target position
            bool canPlace = _inventoryManager.CheckItemFits(
                targetSlot.inventoryId,
                _draggedItem.ItemInstance,
                targetSlot.slotPosition);
            
            if (canPlace)
            {
                int res;
                if (!split)
                {
                GD.Print($"[GUI] Dropping item at position: {targetSlot.slotPosition}");

                // Move the item in the inventory manager
                res = _inventoryManager.TryTransferItemPosition(
                    targetSlot.inventoryId,
                    _draggedItem.ItemInstance,
                    targetSlot.slotPosition,
                    _rotateHeld);

                } 
                else 
                {
                    res = _inventoryManager.TrySplitStack(
                        targetSlot.inventoryId,
                        _draggedItem.ItemInstance,
                        1,
                        targetSlot.slotPosition,
                        _rotateHeld);
                }
                if (res > 0)
                {
                    _dragGhost.Count -= res;
                    _dropAudio.Play();
                }
                
                // Refresh both inventory windows
                RefreshInventoryWindows();
            }
            else
            {
                GD.Print("[GUI] Cannot place item here - doesn't fit");
            }
        }
        else
        {
            GD.Print("[GUI] Dropped outside inventory - returning item");
        }
        if (_dragGhost.Count <= 0)
        {
            GD.Print("[GUI] All items dropped from stack");
            CleanupDragState();
        }
        
        // Clean up drag state
    }
    
    private void CleanupDragState()
    {
        _dragGhost.MouseDefaultCursorShape = Control.CursorShape.Arrow;
        // Remove ghost
        if (_dragGhost != null)
        {
            _dragGhost.QueueFree();
            _dragGhost = null!;
        }
        
        // Clear highlights
        foreach (var slot in _highlightedSlots)
        {
            slot.SetHighlight(SlotState.Default);
        }
        _highlightedSlots.Clear();
        
        // Clear references
        _draggedItem = null;
        _lastHoveredSlot = null;
        _rotateHeld = false; // Reset rotation flag ⭐
    }
    
    private void RefreshInventoryWindows()
    {
        // Refresh all open inventory windows to show updated item positions
        foreach (var window in openWindows.OfType<InventoryWindow>())
        {
            window.RefreshItems();
        }
    }

    private void CloseAll()
    {
        foreach (var window in openWindows)
        {
            window.QueueFree();
        }   
        
        GD.Print($"[GUI] Windows closed. Total windows: {openWindows.Count}");
    }

    // Call this when any UIWindow is closed
    public void OnWindowClosed(UIWindow window)
    {
        openWindows.Remove(window);        
        // If no windows are open, recapture the mouse for 3D view
        if (openWindows.Count == 0)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        
        GD.Print($"[GUI] Window closed. Total windows: {openWindows.Count}");
    }

    // Call this when any UIWindow is opened
    public void OnWindowOpened(UIWindow window)
    {
        openWindows.Add(window);
        
        // If this is the first window, release mouse capture for UI interaction
        if (openWindows.Count > 0)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        
        GD.Print($"[GUI] On Window opened. Total windows: {openWindows.Count}");
    }

    public override void _ExitTree()
    {
        var character = GetNode<Character>("/root/Main/Character/CharacterBody3D");
        character.InventoryRequested += OnInventoryRequested;
        character.RotateRequested += OnRotateRequested;
        character.HotbarSlotSelected += OnHotbarSlotSelected;
        
        base._ExitTree();
    }
}
