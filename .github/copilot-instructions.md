📝 Important Note for Future Reference:
DO NOT create UI nodes or scene nodes via code.

If a node can be created in the editor, it should be created there
Only reference nodes by name using GetNode()
Provide clear instructions to the user about what to create in the editor and what to name it
Work collaboratively - let the user build the scene structure

## Project Context

**Godot Version**: 4.5.1.stable
**Language**: C# (.NET)
**Project Type**: 3D multiplayer game with terrain manipulation and item system

### Key Systems
1. **Terrain System** (`Terrain.cs`, `TerrainBrush.cs`, `TerrainManipulationTools.cs`)
   - Procedural terrain generation with Delaunay triangulation
   - Real-time terrain editing capabilities
   - Custom terrain editor plugin in `addons/TerrainTools/`

2. **Item System** (`ITEM_SYSTEM.md` has detailed architecture)
   - `ItemDefinition.cs`: Data-driven item definitions (stored as .tres resources)
   - `PickupableItem.cs`: World items implementing `IPickupable`
   - `Inventory.cs`: Player inventory management
   - `ToolItem.cs`: Base class for usable tools
   - Current items: Pickaxe, FishingRod

3. **Character System** (`Character.cs`)
   - Player controller with tool management
   - `PlayerTools.cs`: Handles tool switching and usage

4. **Networking** (`NetworkManager.cs`)
   - Multiplayer support built-in

### Scene Structure
- `main.tscn`: Main game scene
- `character.tscn`: Player character
- `terrain.tscn`: Terrain system
- `inventory.tscn`: Inventory UI
- Individual item scenes: `pickaxe.tscn`, `FishingRod.tscn`, etc.

### Coding Conventions
- Use C# with Godot 4.5+ conventions
- Scripts should extend appropriate Godot base classes
- Use `GetNode<T>()` for type-safe node references
- Follow the existing pattern: data-driven design with .tres resources
- Tool items should extend `ToolItem` base class
- Pickupable items should implement `IPickupable` interface

### When Adding New Features
1. Check existing systems in `ITEM_SYSTEM.md` before creating new patterns
2. Use .tres resource files for item definitions (see `Items/` folder)
3. Create scene files for visual items, attach scripts for behavior
4. Update relevant manager classes (Inventory, PlayerTools, etc.)
5. Consider multiplayer implications (NetworkManager)

### MCP Tools Available
- Use `mcp_godot_*` tools to query scenes, modify nodes, and manage project
- Prefer `query_node` before modifying to understand structure
- Use `list_assets` to find existing resources
- Use `get_class_info` for Godot API documentation