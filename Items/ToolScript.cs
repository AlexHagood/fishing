using Godot;

public partial class ToolScript: Node3D
{
    public ItemInstance itemInstance;
    public virtual void PrimaryFire(Character character)
    {
        GD.Print($"[ToolScript] PrimaryFire - by item {itemInstance.ItemData.Name}");
    }
    
    public virtual void SecondaryFire(Character character)
    {
        GD.Print($"[ToolScript] SecondaryFire - by item {itemInstance.ItemData.Name}");
    }
}