using Godot;

/// <summary>
/// Resource that defines how an item appears in the inventory.
/// Save as .tres files in your Items folder.
/// </summary>
[GlobalClass]
public partial class ItemDefinition : Resource
{
    /// <summary>
    /// Display name in inventory
    /// </summary>
    [Export] public string Name { get; set; } = "Item";
    /// <summary>
    /// Icon displayed in inventory UI
    /// </summary>
    [Export(PropertyHint.File, "*.png,*.jpg,*.jpeg,*.svg")] public string Icon { get; set; }
    
    /// <summary>
    /// Size in inventory grid (e.g., 1x1, 2x1, 2x2)
    /// </summary>
    [Export] public Vector2I Size { get; set; }
    
    /// <summary>
    /// Maximum stack size (1 = non-stackable)
    /// </summary>
    [Export] public int StackSize { get; set; } = 1;
    
    /// <summary>
    /// Path to the world scene that can be spawned (e.g., "res://Items/Scenes/rock.tscn")
    /// </summary>
    [Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "";

    /// <summary>
    /// Returns true if the item can be stacked (StackSize > 1)
    /// </summary>
    public bool Stackable => StackSize > 1;

    [Export] public PackedScene ToolScriptScene { get; set; }
    

}
    
