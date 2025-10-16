using Godot;

[GlobalClass]
public partial class GameItem : RigidBody3D
{
    [Export] public bool IsPickupable { get; set; } = true;
    [Export] public float PickupRange { get; set; } = 3.0f;
    [Export] public float ThrowForce { get; set; } = 10.0f;
    [Export] public string ItemName { get; set; } = "Item";

    [Export] public Vector2 InvSize { get; set; } = new Vector2(2, 2);

    [Export] public Texture2D InvTexture { get; set; }

    public InvItem invItem { get; set; }

    public override void _Ready()
    {
        if (InvTexture == null)
        {
            InvTexture = GD.Load<Texture2D>("res://icon.png"); // Godot's default pink/black placeholder
        }
    }
    
    public bool CanBePickedUp()
    {
        return IsPickupable && this != null;
    }

    public void OnPickedUp()
    {
        // Disable physics when picked up
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        this.Freeze = true;
        this.CollisionLayer = 1;
        this.CollisionMask = 1;
        GD.Print($"Picked up {ItemName}");
    }

    public void OnDropped()
    {
        // Re-enable physics when dropped
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        this.Freeze = false;
        GD.Print($"Dropped {ItemName}");
    }

    public void OnThrown(Vector3 throwDirection, float force)
    {
        GD.Print("Throwing");
        // Re-enable physics and apply force
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        this.Freeze = false;
        this.ApplyImpulse(throwDirection * force);
        GD.Print($"Threw {ItemName} with force {force}");
    }

    public void DisablePhys()
    {
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        this.Freeze = true;
        this.Visible = false;
        this.CollisionLayer = 0;
        this.CollisionMask = 0;
    }

    public void EnablePhys()
    {
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        this.Freeze = false;
        this.Visible = true;
        this.CollisionLayer = 1; // Set to appropriate layer
        this.CollisionMask = 1; // Set to appropriate mask
    }

    public void InventoryPickup()
    {
        GD.Print("Picked up"!);
        EmitSignal("inventory_pickup", this);
    }
}
