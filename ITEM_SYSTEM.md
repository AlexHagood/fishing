# Item System - Resource-Based Architecture

## Overview
The item system now uses **ItemDefinition resources** as the single source of truth for all item properties. This eliminates duplication and enables features like stacking and easy item creation.

## Architecture

### ItemDefinition (Resource)
`.tres` files that define item properties:
- Basic info (name, icon, inventory size)
- Pickup settings (range, throw force)
- World representation (scene path)
- Tool settings (hold position, rotation, scale)
- **Stacking** (MaxStackSize: 1 = tool, >1 = stackable material)

### GameItem (RigidBody3D)
Physical representation in the 3D world:
- References an ItemDefinition
- Has physics, collision, visuals
- Can be picked up and placed in inventory

### ToolItem (extends GameItem)
Special items with actions (pickaxe, fishing rod, etc.):
- Inherits all GameItem/IPickupable functionality
- Adds OnPrimaryFire(), OnSecondaryFire(), OnEquip(), OnUnequip()

### InvItem (Control)
UI representation in inventory:
- References an ItemDefinition
- Has a stack count
- May or may not have a world GameItem instance

## Creating New Items

### 1. Create ItemDefinition Resource
Right-click in FileSystem → "New Resource" → Search for "ItemDefinition"

Set properties:
```
ItemName: "Wood Plank"
InvTexture: [assign texture]
InvSize: Vector2(1, 1)
MaxStackSize: 99  # Stackable material
WorldScenePath: "res://items/wood_plank.tscn"
```

Save as `Items/WoodPlankDef.tres`

### 2. Create 3D Scene (if needed)
For items that exist in the world:
- Create scene with GameItem or ToolItem root
- Assign the ItemDefinition resource
- Add mesh, collision shape, etc.

### 3. Use in Code

**Spawn item in world:**
```csharp
var itemDef = GD.Load<ItemDefinition>("res://Items/WoodPlankDef.tres");
var item = ItemUtils.SpawnItemInWorld(itemDef, position, parent);
```

**Add to inventory:**
```csharp
// From world item
var invItem = ItemUtils.CreateInvItemFromWorld(worldItem);

// From definition (for stacks)
var invItem = ItemUtils.CreateInvItemFromDefinition(itemDef, count: 10);
```

**Stack items:**
```csharp
bool stacked = ItemUtils.TryStackItems(targetInvItem, sourceInvItem);
```

## Benefits

✅ **Single source of truth** - Item properties defined once in .tres file
✅ **Easy to create** - New items don't require code changes  
✅ **Stacking support** - Materials can stack (wood, ore, etc.)
✅ **Memory efficient** - Inventory items don't need world nodes
✅ **Designer-friendly** - Artists/designers can create items in editor
✅ **Serialization** - Easy save/load system in the future

## Examples

See `Items/` folder for examples:
- **PickaxeDef.tres** - Tool (MaxStackSize=1)
- **FishingRodDef.tres** - Tool (MaxStackSize=1)

## Migration Notes

Existing GameItem/ToolItem instances should have ItemDefinition assigned in their scene properties. The system includes fallbacks but will warn if ItemDefinition is missing.
