using Godot;

public partial class ToolScript: Node3D
{
    public ItemInstance itemInstance;
    
    [Export]
    public NodePath MeshPath;
    
    public virtual void PrimaryFire(Character character)
    {
        GD.Print($"[ToolScript] PrimaryFire - by item {itemInstance.ItemData.Name}");
    }
    
    public virtual void SecondaryFire(Character character)
    {
        GD.Print($"[ToolScript] SecondaryFire - by item {itemInstance.ItemData.Name}");
    }
    
    // Override these in subclasses if you need per-frame updates
    public override void _Process(double delta)
    {
        base._Process(delta);
    }
    
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }
}