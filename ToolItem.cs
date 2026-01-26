using Godot;

public partial class ToolItem : Node3D
{
    [Export] public Vector3 HoldPosition { get; set; } = Vector3.Zero;
    [Export] public Vector3 HoldRotation { get; set; } = Vector3.Zero;
    [Export] public Vector3 HoldScale { get; set; } = Vector3.One;
    
    [Export] public bool IsPickupable { get; set; } = true;
    [Export] public float PickupRange { get; set; } = 3.0f;
    [Export] public float ThrowForce { get; set; } = 10.0f;
    [Export] public string ItemName { get; set; } = "Tool";
    [Export] public Vector2 InvSize { get; set; } = new Vector2(2, 2);
    [Export] public Texture2D InvTexture { get; set; }
    
    public InvItem invItem { get; set; }
    
    // Reference to the main RigidBody3D child if it exists (for physics interactions)
    public RigidBody3D _physicsBody;

    public override void _Ready()
    {
        if (InvTexture == null)
        {
            InvTexture = GD.Load<Texture2D>("res://icon.png");
        }
        
        // Find the physics body child if it exists
        foreach (var child in GetChildren())
        {
            if (child is RigidBody3D rb)
            {
                _physicsBody = rb;
                break;
            }
        }
    }

    public virtual void OnPrimaryFire()
    {
        GD.Print($"{ItemName} Primary Fire");
    }

    public virtual void OnSecondaryFire()
    {
        GD.Print($"{ItemName} Secondary Fire");
    }

    public virtual void OnEquip() { }
    public virtual void OnUnequip() { }

    public bool CanBePickedUp()
    {
        return IsPickupable && this != null;
    }

    public virtual void OnPickedUp()
    {
        // For ToolItem, we handle physics in OnEquip
        GD.Print($"ToolItem {ItemName} picked up");
        
        // Freeze physics body if it exists
        if (_physicsBody != null)
        {
            _physicsBody.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
            _physicsBody.Freeze = true;
            _physicsBody.CollisionLayer = 0;
            _physicsBody.CollisionMask = 0;
        }
    }

    public virtual void OnDropped()
    {
        // Re-enable physics when dropped
        if (_physicsBody != null)
        {
            _physicsBody.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            _physicsBody.Freeze = false;
            _physicsBody.CollisionLayer = 1;
            _physicsBody.CollisionMask = 1;
        }
        GD.Print($"Dropped {ItemName}");
    }

    public virtual void OnThrown(Vector3 throwDirection, float force)
    {
        if (_physicsBody != null)
        {
            _physicsBody.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            _physicsBody.Freeze = false;
            _physicsBody.ApplyImpulse(throwDirection * force);
        }
        GD.Print($"Threw {ItemName} with force {force}");
    }

    public void DisablePhys()
    {
        if (_physicsBody != null)
        {
            _physicsBody.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
            _physicsBody.Freeze = true;
            _physicsBody.CollisionLayer = 0;
            _physicsBody.CollisionMask = 0;
        }
        this.Visible = false;
    }

    public void EnablePhys()
    {
        if (_physicsBody != null)
        {
            _physicsBody.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            _physicsBody.Freeze = false;
            _physicsBody.CollisionLayer = 1;
            _physicsBody.CollisionMask = 1;
        }
        this.Visible = true;
    }
}
