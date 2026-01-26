using Godot;

/// <summary>
/// Resource that defines an item's properties and metadata.
/// Create .tres files in the editor to define new items.
/// </summary>
[GlobalClass]
public partial class ItemDefinition : Resource
{
    [Export] public string ItemName { get; set; } = "Item";
    [Export] public Texture2D InvTexture { get; set; }
    [Export] public Vector2 InvSize { get; set; } = new Vector2(2, 2);
    [Export] public int MaxStackSize { get; set; } = 1; // 1 = not stackable (tools), >1 = stackable (materials)
    
    [ExportGroup("Pickup Settings")]
    [Export] public bool IsPickupable { get; set; } = true;
    [Export] public float PickupRange { get; set; } = 3.0f;
    [Export] public float ThrowForce { get; set; } = 10.0f;
    
    [ExportGroup("Physics")]
    [Export] public float Buoyancy { get; set; } = 1.0f; // >1.0 floats, <1.0 sinks, 1.0 neutral
    
    [ExportGroup("World Representation")]
    [Export] public string WorldScenePath { get; set; } = ""; // Path to the 3D scene for this item
    
    [ExportGroup("Tool Settings (if applicable)")]
    [Export] public bool IsTool { get; set; } = false;
    [Export] public Vector3 HoldPosition { get; set; } = Vector3.Zero;
    [Export] public Vector3 HoldRotation { get; set; } = Vector3.Zero;
    [Export] public Vector3 HoldScale { get; set; } = Vector3.One;
}
