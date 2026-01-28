# Character.cs - Complete Feature List

## Core Character Controller Features

### Movement & Physics
- **Basic Movement**: WASD movement with normalized direction
- **Sprint**: Shift key to sprint (increases speed and FOV)
  - Base speed: 5.0 units/s
  - Sprint speed: 10.0 units/s
- **Jumping**: Space bar, 8.0 units/s jump velocity
- **Gravity**: 20.0 units/s² when airborne
- **Floor Detection**: `IsOnFloor()` for jump enable
- **Mouse Look**: Mouse-based first-person camera rotation
  - Horizontal rotation on character body
  - Vertical rotation on camera (clamped -90° to 90°)
  - Mouse sensitivity: 0.01

### Camera System
- **Dual Camera Setup**: First-person and third-person cameras
- **Camera Toggle**: V key toggles between first/third person
- **FOV Adjustment**: Dynamic FOV changes during sprint
  - Base FOV: 70°
  - Sprint FOV: 80°
  - Smooth lerp transition
- **Mouse Capture**: Escape toggles mouse capture/visible

### Audio System
- **Footstep Sounds**: Alternating Step1.ogg and Step2.ogg
- **Speed-based Timing**:
  - Walk interval: 0.4s
  - Sprint interval: 0.3s
- **Floor Detection**: Only plays when on ground and moving

## Item & Pickup System

### Item Holding (_heldItem)
- **Dual Hold Modes**:
  1. **Tool Items** (ToolItem): Static hold in _holdPosition
     - Parented to camera's HoldPosition node
     - Uses tool-specific HoldPosition, HoldRotation, HoldScale
     - Physics completely disabled (Freeze = true, CollisionLayer/Mask = 0)
     - Calls OnEquip()/OnUnequip()
  2. **Regular Items** (GameItem): Floaty physics hold
     - Follows _holdPosition with spring physics
     - GravityScale = 0, uses force-based follow
     - Follow strength: 8.0, damping: 4.0
     - Angular velocity damping: 6.0

### Pickup System
- **E Key (interact)**: Pickup non-tool items only
  - 5m raycast range
  - Checks IPickupable interface
  - Skips tools (require F key)
  - Opens ContainerItems instead of picking up
- **F Key (pickup)**: Inventory pickup with progress bar
  - 0.5s hold required
  - Progress bar fills during hold
  - Adds to inventory system on completion
  - Calls GameItem.InventoryPickup() signal
  - Creates InvItem wrapper and adds to _inventory
  - Plays "BadWiggle" animation if inventory full

### Item Actions
- **Left Click (LMB)**:
  - Tools: Call ToolItem.OnPrimaryFire()
  - Regular items: ThrowHeldItem()
- **Right Click (RMB)**:
  - Tools: Call ToolItem.OnSecondaryFire()
  - Regular items: DropHeldItem()
- **Q Key (drop)**: Drop equipped tool item
  - Only works on tools
  - Calls DropToolItem()

### Drop/Throw Mechanics
- **DropHeldItem()**: Gentle drop
  - Re-enables physics (GravityScale = 1.0)
  - Removes from inventory
  - For tools: unparents from hold position, calls OnUnequip()
- **ThrowHeldItem()**: Forceful throw
  - Uses IPickupable.ThrowForce
  - Direction based on camera forward
  - Calls OnThrown()
- **DropToolItem()**: Q key tool drop
  - Positions 1.5m in front of player
  - Full physics re-enable
  - Removes from inventory

### Raycast System
- **PlayerObjectRay(range)**: Main interaction raycast
  - Default 5m range
  - Returns collider GodotObject
  - Excludes player character
  - Used for all interactions

## Inventory System (TO BE REMOVED)

### Hotbar Management
- **Hotbar Slots**: 7-element array (_hotbarItems[0-6]), slots 1-6 used
- **Current Slot**: _currentHotbarSlot tracks selected slot
- **Slot Selection**:
  - Number keys 1-6: Select hotbar slot
  - Mouse wheel: Scroll through slots
- **Slot Binding**: Number keys while inventory open + hovering item
  - Binds InvItem to hotbar slot
  - Updates hotbar display

### Hotbar Display
- **UpdateHotbarHighlight()**: Visual feedback for selected slot
  - Yellow border + tint for selected (3px border)
  - Transparent green for unselected
- **UpdateHotbarSlot(slot)**: Updates item icon in slot
  - Displays item texture from InvItem
  - 4px padding around icon
- **UpdateAllHotbarSlots()**: Refreshes all slot displays

### Inventory Integration
- **_inventory**: Reference to Inventory UI node
- **InvItem Management**: Creates/destroys InvItem wrappers
- **ForceFitItem()**: Attempts to fit item in inventory grid
- **RemoveHeldItemFromInventory()**: Cleanup when dropping
  - Clears tile references
  - Removes from InventoryPanel
  - Updates hotbar display
- **BindItemToHotbarSlot()**: Links InvItem to hotbar slot

### Inventory UI
- **I Key**: Toggle inventory open/closed
- **Mouse Cursor**: Shows when inventory open

## Button Hints System

### UpdateButtonHints()
- **Dynamic Hints**: Shows E/F key prompts based on raycast target
- **Hint Priority**:
  1. ContainerItem: Shows both E and F hints
  2. ToolItem: Shows F hint only
  3. GameItem: Shows both E and F hints
  4. Metadata: Checks for "HintE" and "HintF" meta keys
- **Parent Chain Search**: Walks up node tree to find hints
- **Integration**: Updates ButtonHints UI component

## Tool-Specific Features

## GUI Integration

### Inventory Window
- **Toggle**: I key opens/closes
- **Mouse Mode**: Visible when open, captured when closed
- **Crosshair**: Hidden when inventory open

### Progress Bar (PickupBar)
- **F Key Pickup**: Shows during hold
- **Value**: 0-100, fills over 0.5s
- **BadWiggle**: Animation plays when inventory full

### Container System
- **Container Detection**: E key on ContainerItem
- **Opens Container Window**: Calls _gui.OpenContainer()

## Dependencies & References

### Required Nodes
- `Camera3D` (first person)
- `CameraBack` (third person)
- `HoldPosition` (child of Camera3D, created in _Ready)
- `GUI/Inventory` (Inventory UI node)
- `GUI/Hotbar` with Slot1-Slot6 panels
- `GUI/PickupBar` with AnimationPlayer
- `GUI/ButtonHints` (optional)

### Required Classes
- `Gui` - Main UI controller
- `GameItem` - Base item class (RigidBody3D)
- `ToolItem` - Tool items (extends GameItem)
- `ContainerItem` - Container items
- `IPickupable` - Interface for pickupable objects
- `InvItem` - Inventory item wrapper (TO BE REMOVED)
- `Inventory` - Inventory grid system (TO BE REMOVED)
- `ButtonHints` - E/F key hint display
- `GraphNode` - Terrain nodes for shovel interaction

### Signals & Events
- `GameItem.InventoryPickup()` - Called when F key pickup succeeds
- `IPickupable` interface methods:
  - `CanBePickedUp()`
  - `OnPickedUp()`
  - `OnDropped()`
  - `OnThrown(direction, force)`
- `ToolItem` methods:
  - `OnEquip()`
  - `OnUnequip()`
  - `OnPrimaryFire()`
  - `OnSecondaryFire()`

## Input Actions Required

### Movement
- `fwd` - Forward (W)
- `back` - Backward (S)
- `left` - Left (A)
- `right` - Right (D)
- `jump` - Jump (Space)
- `sprint` - Sprint (Shift)

### Interaction
- `interact` - Pickup non-tools (E)
- `pickup` - Inventory pickup with hold (F)
- `drop` - Drop tool item (Q)
- `inventory` - Toggle inventory (I)
- `camera` - Toggle camera view (V)

### Mouse
- Left Click - Primary action
- Right Click - Secondary action
- Mouse Wheel - Hotbar scroll
- Escape - Toggle mouse capture

## Features to Preserve When Removing Inventory

### KEEP:
- All movement & physics
- Camera system
- Audio system
- Item holding (_heldItem) with dual modes
- Pickup raycasting (E and F keys)
- Drop/throw mechanics
- Tool interaction (OnEquip/OnUnequip/OnPrimaryFire/OnSecondaryFire)
- Button hints system
- RaiseNodeWithShovel()
- Container opening (E key)
- GameItem/ToolItem/IPickupable interaction

### REMOVE:
- _inventory field
- _currentHotbarSlot field
- _hotbarItems array
- UpdateHotbarHighlight()
- UpdateHotbarSlot()
- UpdateAllHotbarSlots()
- BindItemToHotbarSlot()
- SelectHotbarSlot() - entire method
- InvItem creation in F key pickup
- _inventory.ForceFitItem() calls
- RemoveHeldItemFromInventory() - entire method
- Number key hotbar selection
- Mouse wheel hotbar scrolling
- Inventory panel checks in _Input
- All InvItem/Inventory references

### STUB OUT (for new InventoryManager):
- F key pickup: Add placeholder for "request add to inventory"
- Drop items: Add placeholder for "request remove from inventory"
- Hotbar selection: Add placeholder for "request equip from slot"
