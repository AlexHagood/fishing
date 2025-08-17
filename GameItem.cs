using Godot;

[GlobalClass]
public partial class GameItem : RigidBody3D
{
    [Export] public bool IsPickupable { get; set; } = true;
    [Export] public float PickupRange { get; set; } = 3.0f;
    [Export] public float ThrowForce { get; set; } = 10.0f;
    [Export] public string ItemName { get; set; } = "Item";

    private RigidBody3D _rigidBody;

    public override void _Ready()
    {
    }
    
    public bool CanBePickedUp()
    {
        return IsPickupable && _rigidBody != null;
    }

    public void OnPickedUp()
    {
        // Disable physics when picked up
        _rigidBody.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        _rigidBody.Freeze = true;
        GD.Print($"Picked up {ItemName}");
    }

    public void OnDropped()
    {
        // Re-enable physics when dropped
        _rigidBody.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        _rigidBody.Freeze = false;
        GD.Print($"Dropped {ItemName}");
    }

    public void OnThrown(Vector3 throwDirection, float force)
    {
        // Re-enable physics and apply force
        _rigidBody.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        _rigidBody.Freeze = false;
        _rigidBody.ApplyImpulse(throwDirection * force);
        GD.Print($"Threw {ItemName} with force {force}");
    }

    public void InventoryPickup()
    {
        GD.Print("PICKED UP!");
    }
}
