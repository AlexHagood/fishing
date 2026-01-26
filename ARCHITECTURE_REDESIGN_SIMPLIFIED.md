# Simplified Inventory Architecture Redesign

**Goal**: Fix container bugs by separating data from UI. Keep it simple.

---

## 🎯 Core Problem (In Plain English)

**What's broken:**
- Your `Inventory` node gets moved around between windows/containers
- When you move a node, Godot can lose track of it
- You're mixing "what items you have" (data) with "how to display them" (UI)

**The fix:**
- Keep inventory **data** in one place (never moves)
- Create **views** that just display the data (can have many)
- Use **simple managers** to coordinate between them

---

## 🏗️ New Structure (Simple Version)

```
DATA (The Truth)
  InventoryData.cs        → What items you have
  ItemInstance.cs         → A specific item

MANAGERS (The Brain)  
  InventoryManager.cs     → Add/remove/move items
  ItemWorldBridge.cs      → Spawn/despawn in world

VIEWS (The Eyes)
  InventoryView.cs        → Shows the grid
  
WORLD (The Physics)
  GameItem.cs             → Item in 3D space (simplified)
```

That's it. Four concepts instead of dozens.

---

## 📦 Step-by-Step Implementation

### **STEP 1: Create Data Layer (2 hours)**

Create `/home/alex/glop-3/Inventory/Data/` folder with these files:

#### `ItemInstance.cs` - Represents ONE item
```csharp
using Godot;
using System;

/// <summary>
/// A specific instance of an item. Can be in inventory OR world, not both.
/// </summary>
[Serializable]
public class ItemInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ItemDefinition Definition { get; set; }
    public int StackCount { get; set; } = 1;
    public Vector2I Position { get; set; }  // Grid position
    
    // Where is this item? (null = in inventory only)
    public NodePath? WorldNodePath { get; set; }
}
```

#### `InventoryData.cs` - Holds items in a grid
```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure data for an inventory. No UI, no nodes.
/// </summary>
[Serializable]
public class InventoryData
{
    public string Id { get; set; }
    public Vector2I Size { get; set; }
    public List<ItemInstance> Items { get; set; } = new();
    
    public InventoryData(string id, int width, int height)
    {
        Id = id;
        Size = new Vector2I(width, height);
    }
    
    /// <summary>
    /// Check if area is empty (for placing items)
    /// </summary>
    public bool IsAreaFree(Vector2I pos, Vector2I itemSize)
    {
        // Out of bounds?
        if (pos.X < 0 || pos.Y < 0 || 
            pos.X + itemSize.X > Size.X || 
            pos.Y + itemSize.Y > Size.Y)
            return false;
        
        // Check all cells this item would occupy
        for (int y = 0; y < itemSize.Y; y++)
        {
            for (int x = 0; x < itemSize.X; x++)
            {
                var checkPos = new Vector2I(pos.X + x, pos.Y + y);
                
                // Is any item occupying this cell?
                foreach (var item in Items)
                {
                    if (IsPositionOccupied(checkPos, item))
                        return false;
                }
            }
        }
        
        return true;
    }
    
    private bool IsPositionOccupied(Vector2I pos, ItemInstance item)
    {
        var itemEnd = item.Position + item.Definition.InvSize;
        return pos.X >= item.Position.X && pos.X < itemEnd.X &&
               pos.Y >= item.Position.Y && pos.Y < itemEnd.Y;
    }
    
    /// <summary>
    /// Get item at specific position (if any)
    /// </summary>
    public ItemInstance GetItemAt(Vector2I pos)
    {
        return Items.FirstOrDefault(item => IsPositionOccupied(pos, item));
    }
}
```

**That's it for data!** No Godot nodes, no UI, just simple C# classes.

---

### **STEP 2: Create Manager (3 hours)**

Create `/home/alex/glop-3/Inventory/InventoryManager.cs` - an autoload singleton:

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central manager for all inventories. Autoload singleton.
/// Handles all inventory operations.
/// </summary>
public partial class InventoryManager : Node
{
    private Dictionary<string, InventoryData> _inventories = new();
    
    // Signals for UI to react to
    [Signal] public delegate void InventoryChangedEventHandler(string inventoryId);
    [Signal] public delegate void ItemAddedEventHandler(string inventoryId, Guid itemId);
    [Signal] public delegate void ItemRemovedEventHandler(string inventoryId, Guid itemId);
    
    /// <summary>
    /// Create a new inventory
    /// </summary>
    public InventoryData CreateInventory(string id, int width, int height)
    {
        var inventory = new InventoryData(id, width, height);
        _inventories[id] = inventory;
        return inventory;
    }
    
    /// <summary>
    /// Get existing inventory (or null)
    /// </summary>
    public InventoryData GetInventory(string id)
    {
        return _inventories.GetValueOrDefault(id);
    }
    
    /// <summary>
    /// Try to add item to inventory (auto-finds space)
    /// Returns the item instance if successful, null if failed
    /// </summary>
    public ItemInstance TryAddItem(string inventoryId, ItemDefinition itemDef, int count = 1)
    {
        var inventory = GetInventory(inventoryId);
        if (inventory == null)
        {
            GD.PrintErr($"Inventory '{inventoryId}' not found");
            return null;
        }
        
        // Try to stack first (if stackable)
        if (itemDef.MaxStackSize > 1)
        {
            var existingStack = inventory.Items.FirstOrDefault(i => 
                i.Definition == itemDef && 
                i.StackCount < itemDef.MaxStackSize
            );
            
            if (existingStack != null)
            {
                int spaceInStack = itemDef.MaxStackSize - existingStack.StackCount;
                int amountToAdd = Mathf.Min(spaceInStack, count);
                existingStack.StackCount += amountToAdd;
                
                EmitSignal(SignalName.InventoryChanged, inventoryId);
                return existingStack;
            }
        }
        
        // Find empty space
        var position = FindEmptySpace(inventory, itemDef.InvSize);
        if (position == null)
        {
            GD.Print($"No space in inventory '{inventoryId}' for {itemDef.ItemName}");
            return null;
        }
        
        // Create and add item
        var item = new ItemInstance
        {
            Definition = itemDef,
            StackCount = count,
            Position = position.Value
        };
        
        inventory.Items.Add(item);
        
        EmitSignal(SignalName.ItemAdded, inventoryId, item.Id);
        EmitSignal(SignalName.InventoryChanged, inventoryId);
        
        GD.Print($"Added {itemDef.ItemName} to '{inventoryId}' at {position.Value}");
        return item;
    }
    
    /// <summary>
    /// Remove item from inventory
    /// </summary>
    public bool RemoveItem(string inventoryId, Guid itemId)
    {
        var inventory = GetInventory(inventoryId);
        if (inventory == null) return false;
        
        var item = inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;
        
        inventory.Items.Remove(item);
        
        EmitSignal(SignalName.ItemRemoved, inventoryId, itemId);
        EmitSignal(SignalName.InventoryChanged, inventoryId);
        
        return true;
    }
    
    /// <summary>
    /// Move item within same inventory
    /// </summary>
    public bool TryMoveItem(string inventoryId, Guid itemId, Vector2I newPosition)
    {
        var inventory = GetInventory(inventoryId);
        if (inventory == null) return false;
        
        var item = inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;
        
        // Check if new position is valid
        if (!inventory.IsAreaFree(newPosition, item.Definition.InvSize))
            return false;
        
        item.Position = newPosition;
        EmitSignal(SignalName.InventoryChanged, inventoryId);
        return true;
    }
    
    /// <summary>
    /// Transfer item between inventories
    /// </summary>
    public bool TryTransferItem(string fromInventoryId, string toInventoryId, Guid itemId)
    {
        var fromInv = GetInventory(fromInventoryId);
        var toInv = GetInventory(toInventoryId);
        
        if (fromInv == null || toInv == null) return false;
        
        var item = fromInv.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;
        
        // Find space in target inventory
        var newPos = FindEmptySpace(toInv, item.Definition.InvSize);
        if (newPos == null) return false;
        
        // Move item
        fromInv.Items.Remove(item);
        item.Position = newPos.Value;
        toInv.Items.Add(item);
        
        EmitSignal(SignalName.InventoryChanged, fromInventoryId);
        EmitSignal(SignalName.InventoryChanged, toInventoryId);
        
        return true;
    }
    
    /// <summary>
    /// Find empty space for item (simple left-to-right, top-to-bottom scan)
    /// </summary>
    private Vector2I? FindEmptySpace(InventoryData inventory, Vector2I itemSize)
    {
        for (int y = 0; y <= inventory.Size.Y - itemSize.Y; y++)
        {
            for (int x = 0; x <= inventory.Size.X - itemSize.X; x++)
            {
                var pos = new Vector2I(x, y);
                if (inventory.IsAreaFree(pos, itemSize))
                    return pos;
            }
        }
        return null;
    }
}
```

**Add to project.godot autoload:**
```ini
[autoload]
InventoryManager="*res://Inventory/InventoryManager.cs"
```

---

### **STEP 3: Simplify GameItem (1 hour)**

Update `GameItem.cs` to just link to data:

```csharp
using Godot;

[GlobalClass]
public partial class GameItem : RigidBody3D, IPickupable
{
    [Export] public ItemDefinition ItemDef { get; set; }
    
    // NEW: Link to data layer (instead of invItem)
    public Guid? InstanceId { get; set; }
    
    // Keep all your existing IPickupable implementation
    // Just REMOVE the invItem property
    
    public const string HintF = "Pickup";
    public const string HintE = "Grab";
    
    // ... rest of existing code stays the same ...
}
```

---

### **STEP 4: Simplify ContainerItem (30 minutes)**

Update `ContainerItem.cs` to just store an inventory ID:

```csharp
using Godot;

public partial class ContainerItem : GameItem
{
    [Export] public Vector2I ContainerSize = new Vector2(5, 3);
    
    public new const string HintE = "Open";
    public new const string HintF = "Pickup";
    
    // NEW: Just store the inventory ID
    public string ContainerInventoryId { get; private set; }
    
    public override void _Ready()
    {
        base._Ready();
        
        // Create inventory in manager (no child nodes!)
        ContainerInventoryId = $"container_{GetInstanceId()}";
        
        var manager = GetNode<InventoryManager>("/root/InventoryManager");
        manager.CreateInventory(ContainerInventoryId, (int)ContainerSize.X, (int)ContainerSize.Y);
        
        GD.Print($"Container created with inventory ID: {ContainerInventoryId}");
    }
    
    [Signal]
    public delegate void ContainerOpenedEventHandler(ContainerItem container);
    
    public void OpenContainer()
    {
        EmitSignal(SignalName.ContainerOpened, this);
    }
}
```

---

### **STEP 5: Update Character Pickup (2 hours)**

Simplify the pickup logic in `Character.cs`:

```csharp
// In Character._Process(), replace the pickup logic:
if (Input.IsActionPressed("pickup"))
{
    var progressBar = _gui.progressBar;
    var rayHit = PlayerObjectRay(5.0f);
    
    IPickupable pickupableItem = null;
    Node nodeToCheck = rayHit as Node;
    while (nodeToCheck != null && pickupableItem == null)
    {
        if (nodeToCheck is IPickupable p && p.CanBePickedUp())
        {
            pickupableItem = p;
            break;
        }
        nodeToCheck = nodeToCheck.GetParent();
    }
    
    if (pickupableItem == null)
    {
        var anim = (AnimationPlayer)_gui.progressBar.GetNode("AnimationPlayer");
        if (!anim.IsPlaying() || anim.CurrentAnimation != "BadWiggle")
        {
            _gui.progressBar.Visible = false;
            _gui.progressBar.Value = 0;
        }
    }
    else
    {
        progressBar.Visible = true;
        progressBar.Value += (float)GetProcessDeltaTime() * (progressBar.MaxValue / 0.5f);
        
        if (progressBar.Value >= progressBar.MaxValue)
        {
            progressBar.Value = 0;
            progressBar.Visible = false;
            
            if (pickupableItem is GameItem gameItem)
            {
                // NEW: Use manager instead of manual InvItem creation
                var manager = GetNode<InventoryManager>("/root/InventoryManager");
                var itemInstance = manager.TryAddItem("player_inventory", gameItem.ItemDef);
                
                if (itemInstance != null)
                {
                    // Success! Link item to world object
                    itemInstance.WorldNodePath = gameItem.GetPath();
                    gameItem.InstanceId = itemInstance.Id;
                    
                    // Hide item in world
                    pickupableItem.DisablePhys();
                }
                else
                {
                    // Failed - inventory full
                    wiggleBar();
                }
            }
        }
    }
}
```

---

### **STEP 6: Create Simple InventoryView (3 hours)**

Create `/home/alex/glop-3/Inventory/InventoryView.cs`:

```csharp
using Godot;
using System.Collections.Generic;

/// <summary>
/// Displays an inventory. Just a view - doesn't own data.
/// </summary>
public partial class InventoryView : Control
{
    [Export] public int TileSize { get; set; } = 64;
    [Export] public string InventoryId { get; set; } = "player_inventory";
    
    private GridContainer _gridContainer;
    private InventoryManager _manager;
    private InventoryData _currentInventory;
    
    // Keep track of UI elements
    private Panel[,] _tiles;
    private Dictionary<Guid, Control> _itemViews = new();
    
    public override void _Ready()
    {
        _manager = GetNode<InventoryManager>("/root/InventoryManager");
        
        // Get or create grid container
        _gridContainer = GetNodeOrNull<GridContainer>("GridContainer");
        if (_gridContainer == null)
        {
            _gridContainer = new GridContainer();
            _gridContainer.Name = "GridContainer";
            AddChild(_gridContainer);
        }
        
        // Connect to manager signals
        _manager.InventoryChanged += OnInventoryChanged;
        
        // Initial display
        RefreshDisplay();
    }
    
    /// <summary>
    /// Rebuild entire display to match current inventory data
    /// </summary>
    public void RefreshDisplay()
    {
        _currentInventory = _manager.GetInventory(InventoryId);
        if (_currentInventory == null)
        {
            GD.PrintErr($"Inventory '{InventoryId}' not found!");
            return;
        }
        
        // Rebuild grid
        RebuildGrid();
        
        // Display all items
        DisplayAllItems();
    }
    
    private void RebuildGrid()
    {
        // Clear existing
        foreach (var child in _gridContainer.GetChildren())
        {
            child.QueueFree();
        }
        _itemViews.Clear();
        
        // Create grid
        var size = _currentInventory.Size;
        _tiles = new Panel[size.X, size.Y];
        _gridContainer.Columns = size.X;
        
        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                var panel = new Panel();
                var style = new StyleBoxFlat();
                style.BgColor = new Color(0, 0, 0);
                panel.AddThemeStyleboxOverride("panel", style);
                panel.CustomMinimumSize = new Vector2(TileSize, TileSize);
                
                _tiles[x, y] = panel;
                _gridContainer.AddChild(panel);
            }
        }
        
        // Set size
        var totalSize = new Vector2(size.X * TileSize, size.Y * TileSize);
        CustomMinimumSize = totalSize;
        Size = totalSize;
    }
    
    private void DisplayAllItems()
    {
        foreach (var item in _currentInventory.Items)
        {
            DisplayItem(item);
        }
    }
    
    private void DisplayItem(ItemInstance item)
    {
        // Create item visual
        var itemControl = new Control();
        itemControl.Position = new Vector2(
            item.Position.X * TileSize, 
            item.Position.Y * TileSize
        );
        itemControl.Size = new Vector2(
            item.Definition.InvSize.X * TileSize,
            item.Definition.InvSize.Y * TileSize
        );
        
        // Add icon
        var icon = new TextureRect();
        icon.Texture = item.Definition.InvTexture;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.AnchorRight = 1;
        icon.AnchorBottom = 1;
        itemControl.AddChild(icon);
        
        // Add stack count if > 1
        if (item.StackCount > 1)
        {
            var label = new Label();
            label.Text = item.StackCount.ToString();
            label.AddThemeColorOverride("font_color", Colors.White);
            label.Position = new Vector2(4, 4);
            itemControl.AddChild(label);
        }
        
        _gridContainer.AddChild(itemControl);
        _itemViews[item.Id] = itemControl;
    }
    
    private void OnInventoryChanged(string inventoryId)
    {
        if (inventoryId == InventoryId)
        {
            RefreshDisplay();
        }
    }
    
    public override void _ExitTree()
    {
        if (_manager != null)
            _manager.InventoryChanged -= OnInventoryChanged;
    }
}
```

---

### **STEP 7: Update Gui.cs (1 hour)**

Simplify `Gui.cs` - NO MORE REPARENTING:

```csharp
// In Gui._Ready(), replace inventory setup:

// Create player inventory data (once, never moves!)
var manager = GetNode<InventoryManager>("/root/InventoryManager");
manager.CreateInventory("player_inventory", 8, 4);

// Get or create inventory view
var inventoryView = GetNodeOrNull<InventoryView>("InventoryView");
if (inventoryView == null)
{
    inventoryView = new InventoryView();
    inventoryView.Name = "InventoryView";
    inventoryView.InventoryId = "player_inventory";
    AddChild(inventoryView);
}

// Setup window to contain the view (NO REPARENTING)
_inventoryWindow = GetNodeOrNull<UIWindow>("InventoryWindow");
if (_inventoryWindow == null)
{
    var windowScene = GD.Load<PackedScene>("res://UI/UIWindow.tscn");
    _inventoryWindow = windowScene.Instantiate<UIWindow>();
    _inventoryWindow.Name = "InventoryWindow";
    AddChild(_inventoryWindow);
}

_inventoryWindow.WindowTitle = "Inventory";
// Just position the view inside the window (no reparenting!)
inventoryView.Position = new Vector2(10, 40); // Inside window
_inventoryWindow.Hide();
```

For containers:
```csharp
public void OpenContainer(ContainerItem container)
{
    // Create or get container view
    var containerView = _containerWindow.GetNodeOrNull<InventoryView>("ContainerView");
    if (containerView == null)
    {
        containerView = new InventoryView();
        containerView.Name = "ContainerView";
        _containerWindow.AddChild(containerView);
    }
    
    // Just change what inventory it displays (NO REPARENTING!)
    containerView.InventoryId = container.ContainerInventoryId;
    containerView.RefreshDisplay();
    
    _containerWindow.WindowTitle = container.ItemName;
    _containerWindow.Show();
    
    Input.MouseMode = Input.MouseModeEnum.Visible;
}
```

---

## ✅ Testing Checklist

After implementing, test these:

1. **Basic Inventory**
   - [ ] Can pick up items (F key)
   - [ ] Items appear in inventory grid
   - [ ] Can open/close inventory (Tab)

2. **Containers**
   - [ ] Can open container (E key)
   - [ ] Container shows its items
   - [ ] Can close container without crash
   - [ ] Can open same container multiple times

3. **Stress Test**
   - [ ] Open container, close, open inventory, close, open container again
   - [ ] Pick up items while container is open
   - [ ] Open multiple containers in sequence

---

## 🎯 What This Fixes

### Before:
```
Container opened → Inventory node reparented to window → 
Close window → Reparent back to container → 
❌ BUG: References lost, signals broken, crashes
```

### After:
```
Container opened → View displays container's data →
Close window → View just changes what it displays →
✅ No bugs, no reparenting, data never moves
```

---

## 📈 Migration Path

1. **Phase 1** (Day 1-2): Implement data layer and manager
2. **Phase 2** (Day 3): Update GameItem and ContainerItem
3. **Phase 3** (Day 4-5): Create InventoryView and update Gui
4. **Phase 4** (Day 6): Update Character pickup logic
5. **Phase 5** (Day 7): Test everything
6. **Phase 6** (Day 8+): Remove old code gradually

**Old code stays working during migration!** Use compiler flags:
```csharp
#if USE_NEW_INVENTORY
    // New code
#else
    // Old code
#endif
```

---

## 💡 Key Principles

1. **Data never moves** - It lives in InventoryManager
2. **Views are dumb** - They just display data
3. **One source of truth** - InventoryManager is the boss
4. **Simple > Complex** - Less abstraction = fewer bugs

---

## 🚀 Quick Start

Copy-paste the code in order:
1. ItemInstance.cs
2. InventoryData.cs  
3. InventoryManager.cs (add to autoload)
4. Update GameItem.cs
5. Update ContainerItem.cs
6. Create InventoryView.cs
7. Update Gui.cs

**Estimated time: 1-2 days of focused work**

---

**Questions? Start with Step 1 and I'll help with each step!**
