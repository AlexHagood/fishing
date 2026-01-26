using Godot;

[GlobalClass]
public partial class GameItem : RigidBody3D, IPickupable
{
    [Export] public ItemDefinition ItemDef { get; set; }
    
    public const string HintF = "Pickup";
    public const string HintE = "Grab";
    // Properties exposed via IPickupable - now reference ItemDef
    public bool IsPickupable 
    { 
        get => ItemDef?.IsPickupable ?? true;
        set { if (ItemDef != null) ItemDef.IsPickupable = value; }
    }
    
    public float PickupRange 
    { 
        get => ItemDef?.PickupRange ?? 3.0f;
        set { if (ItemDef != null) ItemDef.PickupRange = value; }
    }
    
    public float ThrowForce 
    { 
        get => ItemDef?.ThrowForce ?? 10.0f;
        set { if (ItemDef != null) ItemDef.ThrowForce = value; }
    }
    
    public string ItemName 
    { 
        get => ItemDef?.ItemName ?? "Item";
        set { if (ItemDef != null) ItemDef.ItemName = value; }
    }
    
    public Vector2 InvSize 
    { 
        get => ItemDef?.InvSize ?? new Vector2(2, 2);
        set { if (ItemDef != null) ItemDef.InvSize = value; }
    }
    
    public Texture2D InvTexture 
    { 
        get => ItemDef?.InvTexture;
        set { if (ItemDef != null) ItemDef.InvTexture = value; }
    }

    public InvItem invItem { get; set; }

    public override void _Ready()
    {
        // Fallback if no ItemDef is assigned
        if (ItemDef == null)
        {
            GD.PrintErr($"GameItem '{Name}' has no ItemDefinition assigned! Creating default.");
            ItemDef = new ItemDefinition();
        }
        
        if (ItemDef.InvTexture == null)
        {
            ItemDef.InvTexture = GD.Load<Texture2D>("res://icon.png");
        }
    }
    
    public bool CanBePickedUp()
    {
        return IsPickupable && this != null;
    }

    public virtual void OnPickedUp()
    {
        // Disable physics when picked up
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        this.Freeze = true;
        this.CollisionLayer = 1;
        this.CollisionMask = 1;
        GD.Print($"Picked up {ItemName}");
    }

    public virtual void OnDropped()
    {
        // Re-enable physics when dropped
        this.Freeze = false;
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        this.GravityScale = 1.0f;
        this.CollisionLayer = 1;
        this.CollisionMask = 1;
        GD.Print($"Dropped {ItemName}");
    }

    public virtual void OnThrown(Vector3 throwDirection, float force)
    {
        GD.Print("Throwing");
        // Re-enable physics and apply force
        this.Freeze = false;
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        this.GravityScale = 1.0f;
        this.CollisionLayer = 1;
        this.CollisionMask = 1;
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
        this.Freeze = false;
        this.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        this.GravityScale = 1.0f;
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
