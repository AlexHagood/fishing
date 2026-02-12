using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Metadata;
using Godot;

#nullable enable

public partial class Gui : CanvasLayer
{
    public List<UIWindow> windows = new List<UIWindow>();

    private int openWindows => windows.Count(w => w.Visible);
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

    private ContextMenu _contextMenu;

    private Dialog _dialog;



    private InputHandler _inputHandler = null!;



    public override void _Ready()
    {

    }

    public void init(Character character)
    {
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
        _inputHandler = GetNode<InputHandler>("/root/InputHandler");

        _dropAudio = GetNode<AudioStreamPlayer>("DropSound");
        _pickupAudio = GetNode<AudioStreamPlayer>("PickupSound");

        // Connect to Character's signals
        character.InventoryRequested += OnInventoryKeyPress;
        character.HintEUpdated += SetHintE;
        character.HintFUpdated += SetHintF;

        _hotbarUI = GetNode<HotbarUI>("Hotbar");
        _hotbarUI.inventoryId = character.inventoryId;

        _HintF = GetNode<HBoxContainer>("ButtonHints/F");
        _HintE = GetNode<HBoxContainer>("ButtonHints/E");
        _HintFLabel = _HintF.GetNode<Label>("Label");
        _HintELabel = _HintE.GetNode<Label>("Label");

        _contextMenu = GetNode<ContextMenu>("PopupMenu");
        _contextMenu.AddItem("Drop", 0);
        _contextMenu.AddItem("Rotate", 1);


        // Connect to InputHandler signals for UI-specific input
        _inputHandler.NumkeyPressed += OnInputHandlerHotbarSlotSelected;
        _inputHandler.ItemRotateRequested += HandleRotateAction;
        _inputHandler.EscPressed += CloseAllWindows;

        _dialog = GetNode<Dialog>("Dialog");

        character.DialogMessage += ShowDialog;

    }

    public void ShowDialog(string message)
    {
        _dialog.SendMessage(message);
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

    // InputHandler signal handlers
    private void OnInputHandlerHotbarSlotSelected(int slotIndex)
    {
        // Only handle this in UI context (when inventory is open)
        if (_inputHandler.CurrentContext == InputHandler.InputContext.Gameplay)
        {
            GD.Print($"[GUI] Hotbar slot selected: {slotIndex}");
            _hotbarUI.HighlightSlot(slotIndex);
            _hotbarUI.Refresh(); // Update thumbnails when slot changes
            
        } 
        else if (_inputHandler.CurrentContext == InputHandler.InputContext.UI)
        {
        GD.Print($"[GUI] InputHandler hotbar key pressed: {slotIndex}");
        Vector2 mousePos = GetViewport().GetMousePosition();
        var slotUnder = GetSlotAtPosition(mousePos);

        if (slotUnder != null)
        {
            Vector2I slotPos = slotUnder.slotPosition;
            var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
            ItemInstance? item = inv.GetItemAtPosition(slotPos);

            if (item != null)
            {
                GD.Print($"[GUI] Hotbar key pressed over item at position: {item.ItemData.Name}");
                _inventoryManager.BindItemToSlot(inv.Id, slotIndex, item.InstanceId);
            }
        }
        }
    }



    public override void _Input(InputEvent @event)
    {
        // Handle click to drop item (click-to-pick, click-to-drop behavior)
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed)
        {
            // Opens the context menu
            if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed && _dragGhost == null)
            {
                // Show context menu at mouse position
                Vector2 mousePos = GetViewport().GetMousePosition();
                InventorySlot? slotUnder = GetSlotAtPosition(mousePos);
                if (slotUnder != null)
                {
                    // Check if this slot actually has an item in it
                    var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
                    ItemInstance? item = inv.GetItemAtPosition(slotUnder.slotPosition);
                    if (item != null)
                    {
                        GD.Print($"[GUI] Right-clicked on item - {item.ItemData.Name}");
                        // There's an item here - show context menu
                        _contextMenu.Position = (Vector2I)mousePos;
                        _contextMenu.item = item;
                        _contextMenu.Popup();
                        GetViewport().SetInputAsHandled();
                    }
                }
            }

            if (_dragGhost != null)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    GD.Print("[GUI] Left click detected for dropping item");
                    OnDraggingItemClick(false);
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right)
                {
                    GD.Print("[GUI] Right click detected for dropping item");
                    OnDraggingItemClick(true);
                }
                // Only drop if we're clicking on empty space or a slot, not on another item
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Process(double delta)
    {
        // Update drag ghost position to follow mouse
        HighlightSlots();
    }

    public void HighlightSlots(bool force = false)
    {

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

                    bool targetRotation = _dragGhost.ItemInstance.IsRotated ^ _rotateHeld;
                    Vector2I targetSize = _dragGhost.ItemInstance.ItemData.Size;
                    // Get target size based on rotation
                    targetSize = targetRotation ? targetSize.Flip() : targetSize;

                    _highlightedSlots = slotUnder.GetSlotsForSize(targetSize);

                    // Check if it fits with target rotation and ignore self
                    int spaceAvailable = _inventoryManager.GetInventory(slotUnder.inventoryId).GetSpaceAt(
                        _dragGhost.ItemInstance,
                        slotUnder.slotPosition,
                        _rotateHeld);

                    bool validSlot = spaceAvailable > 0;
                    foreach (var slot in _highlightedSlots)
                    {
                        slot.SetHighlight(validSlot ? SlotState.Valid : SlotState.Invalid);
                    }
                }

                _lastHoveredSlot = slotUnder;
            }
        }
    }


    private InventorySlot? GetSlotAtPosition(Vector2 globalPosition)
    {
        // Check all open inventory windows
        foreach (var window in windows.OfType<InventoryWindow>())
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

    public void CloseAllWindows()
    {
        foreach (var window in windows)
        {
            CloseWindow(window);
        }
    }


    public void OnInventoryKeyPress(int id)
    {
        // Check if a window for this specific inventory is already open
        var existing = windows.OfType<InventoryWindow>().FirstOrDefault(w => w.inventoryId == id);
        if (existing != null)
        {
            if (existing.Visible)
            {
                CloseAllWindows();
            }
            else
            {
                OpenWindow(existing);
            }
        }
        else
        {
            CreateInventoryWindow(id);
        }
    }

    public void CloseWindow(UIWindow window)
    {
        window.Visible = false;
        // If no windows are open, recapture the mouse for 3D view
        if (openWindows == 0)
        {
            _inputHandler.CurrentContext = InputHandler.InputContext.Gameplay;

        }

        GD.Print($"[GUI] Window closed. Windows open: {openWindows}");
    }

    // Call this when any UIWindow is opened
    public void OpenWindow(UIWindow window)
    {
        window.Visible = true;
        if (window is InventoryWindow invWindow)
        {
            invWindow.RefreshItems();
        }
        // If this is the first window, release mouse capture for UI interaction
        if (openWindows > 0)
        {
            _inputHandler.CurrentContext = InputHandler.InputContext.UI;
        }

        GD.Print($"[GUI] On Window opened. Windows open: {openWindows}");
    }



    private void CreateInventoryWindow(int id)
    {
        // Load and instantiate the inventory window directly
        var inventoryScene = GD.Load<PackedScene>("res://UI/inventory.tscn");
        var inventoryWindow = inventoryScene.Instantiate<InventoryWindow>();
        inventoryWindow.inventoryId = id;


        // Connect the ItemGrab signal using += syntax
        inventoryWindow.ItemGrab += OnStartDraggingItem;

        // Connect the WindowClosed signal so GUI knows when window is closed
        inventoryWindow.WindowClosed += CloseWindow;

        // Set the window title
        var titleLabel = inventoryWindow.GetNode<Label>("Panel/VBoxContainer/StatusBar/HBoxContainer/Label");
        titleLabel.Text = "Inventory";

        // Add to scene
        AddChild(inventoryWindow);
        windows.Add(inventoryWindow);

        OpenWindow(inventoryWindow);


        GD.Print($"[GUI] Inventory opened. Total windows: {windows.Count}");


    }

    private void HandleRotateAction()
    {
        // Case 1: If we're dragging an item, rotate the drag ghost
        if (_dragGhost != null)
        {
            // XOR: if IsRotated and _rotateHeld match, rotate left; if they differ, rotate right
            if (_dragGhost.ItemInstance.IsRotated == _rotateHeld)
            {
                _dragGhost.RotateLeft();
            }
            else
            {
                _dragGhost.RotateRight();
            }

            _rotateHeld = !_rotateHeld;


            GD.Print($"[GUI] Rotate toggle. _rotateHeld: {_rotateHeld}");
            HighlightSlots(true);
            return; // Done - fit checking happens in _Process
        }
        // Case 2: If we're hovering over an item (not dragging), rotate it in place
        var mousePos = GetViewport().GetMousePosition();
        var slotUnder = GetSlotAtPosition(mousePos);

        if (slotUnder != null)
        {
            var inv = _inventoryManager.GetInventory(slotUnder.inventoryId);
            if (inv.GetItemAtPosition(slotUnder.slotPosition) != null)
            {
                var itemAtSlot = inv.GetItemAtPosition(slotUnder.slotPosition);

                if (itemAtSlot == null)
                {
                    GD.Print("Attempted to rotate an empty slot.");
                    return;
                }

                // Check if we can rotate it in place
                if (_inventoryManager.CanRotateItem(itemAtSlot))
                {
                    _inventoryManager.RequestItemRotate(itemAtSlot);
                    GD.Print($"[GUI] Requested rotated item {itemAtSlot.InstanceId} in place. IsRotated: {itemAtSlot.IsRotated}");
                }
                else
                {
                    GD.Print("[GUI] Cannot rotate item - not enough space");
                }
            }
        }
    }


    private void OnStartDraggingItem(ItemTile itemTile)
    {
        GD.Print($"[GUI] Item grabbed: {itemTile.ItemInstance.InstanceId}");
        _dragGhost = new ItemGhost();
        _dragGhost.MouseDefaultCursorShape = Control.CursorShape.Move;
        _dragGhost.Count = itemTile.ItemInstance.CurrentStackSize;
        _pickupAudio.Play();
        AddChild(_dragGhost);
        _dragGhost.setup(itemTile);
        _dragGhost.ItemInstance = itemTile.ItemInstance;
        _rotateHeld = false;
    }

    private void OnDraggingItemClick(bool rightClick)
    {
        bool leftClick = !rightClick;

        if (_dragGhost == null)
            return;

        GD.Print("[GUI] Attempting to place dragged item");

        // Get the slot under the detection point
        Vector2 detectionPoint = _dragGhost.Position + new Vector2(32, 32);
        InventorySlot? targetSlot = GetSlotAtPosition(detectionPoint);

        if (targetSlot != null)
        {
            Inventory targetInv = _inventoryManager.GetInventory(targetSlot.inventoryId);
            Vector2I targetPos = targetSlot.slotPosition;

            ItemInstance itemInstance = _dragGhost.ItemInstance;
            if (targetSlot != null)
            {
                if (targetInv.GetItemAtPosition(targetSlot.slotPosition)?.InstanceId == itemInstance.InstanceId && leftClick && !_rotateHeld && targetSlot.slotPosition == itemInstance.GridPosition)
                {
                    GD.Print("[GUI] Placed item onto self, exitting dragging");
                    StopDragging();
                    return;
                }
                // Check if the item fits in the target position
                int canPlace = targetInv.GetSpaceAt(
                    itemInstance,
                    targetPos,
                    _rotateHeld);
                if (canPlace > 0)
                {
                    if (leftClick)
                    {
                        GD.Print($"[GUI] Left click, drop all, rotating? {_rotateHeld}");

                        _inventoryManager.RequestItemMove(
                            itemInstance.InstanceId,
                            targetSlot.inventoryId,
                            targetPos,
                            _rotateHeld,
                            Math.Min(canPlace, _dragGhost.Count));
                        _dragGhost.Count -= Math.Min(canPlace, _dragGhost.Count);
                        GD.Print($"[GUI] Placed {Math.Min(canPlace, _dragGhost.Count)} items, {_dragGhost.Count} remaining");

                    }
                    else if (rightClick)
                    {

                        GD.Print("[GUI] Trying to place 1");
                        _inventoryManager.RequestItemMove(
                            itemInstance.InstanceId,
                            targetSlot.inventoryId,
                            targetPos,
                            _rotateHeld,
                            1);
                        _dragGhost.Count -= 1;
                        GD.Print("[GUI] Placed 1, remaining in ghost: " + _dragGhost.Count);

                    }
                    else
                    {
                        GD.Print("[GUI] Not a click I know");
                        throw new System.Exception("Unknown click type in OnDraggingItemClick");
                    }
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
            if (_dragGhost.Count < 0)
            {
                GD.Print("[GUI] Error: Drag ghost count below zero");
                StopDragging();
                throw new System.Exception("Drag ghost count below zero");
            }
            if (_dragGhost.Count == 0)
            {
                StopDragging();
            }


        }
    }

    private void StopDragging()
    {

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
        RefreshInventoryWindows();
        _rotateHeld = false;

    }


    private void RefreshInventoryWindows()
    {

        if (_dragGhost != null)
        {
            int itemid = _dragGhost.ItemInstance.InstanceId;
            if (_inventoryManager.ItemExists(itemid))
            {
                ItemInstance item = _inventoryManager.GetItem(itemid);
                _dragGhost.Count = item.CurrentStackSize;
            }
            else
            {
                StopDragging();
            }
            _dragGhost.QueueFree();
            _dragGhost = null!;
        }
        // Refresh all open inventory windows to show updated item positions
        foreach (var window in windows.OfType<InventoryWindow>())
        {
            window.RefreshItems();
        }


    }


    public override void _ExitTree()
    {
        var character = GetNode<Character>("/root/Main/Character/CharacterBody3D");
        character.InventoryRequested += OnInventoryKeyPress;

        base._ExitTree();
    }
}
