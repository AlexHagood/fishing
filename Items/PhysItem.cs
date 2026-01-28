using Godot;

/// <summary>
/// Physics-based item that can be picked up and thrown by the player.
/// Uses floaty physics when held. Pickup happens on InteractE.
/// </summary>
[GlobalClass]
public partial class PhysItem : WorldItem
{
    [Export] public float ThrowForce { get; set; } = 15.0f;
    
    // Floaty physics parameters
    private float _followStrength = 8.0f;
    private float _damping = 4.0f;
    private float _angularDamping = 6.0f;
    
    public override string HintE { get; protected set; } = "Grab";
    public override string HintF { get; protected set; } = "Pick up";

    public override void _Ready()
    {
        // Ensure default collision settings
        CollisionLayer = 1;
        CollisionMask = 1;
        GravityScale = 1.0f;
    }

    /// <summary>
    /// E key - Pick up the item
    /// </summary>
    public override void InteractE(Character character)
    {
        character.PickupPhysItem(this);
    }

    /// <summary>
    /// F key - Not used for PhysItem
    /// </summary>
    public override void InteractF(Character character)
    {
        // PhysItem doesn't use F key interaction
    }

    /// <summary>
    /// Setup floaty physics hold - called when player picks up the item
    /// </summary>
    public void OnPickedUp()
    {
        Freeze = false;
        FreezeMode = FreezeModeEnum.Static;
        GravityScale = 0;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GD.Print($"[PhysItem] Picked up: {Name}");
    }

    /// <summary>
    /// Apply floaty physics to follow target position - called every frame while held
    /// </summary>
    public void ApplyFloatyPhysics(Vector3 targetPosition, float delta)
    {
        Vector3 toTarget = targetPosition - GlobalPosition;
        Vector3 velocity = LinearVelocity;
        Vector3 desiredVelocity = toTarget * _followStrength;
        Vector3 force = (desiredVelocity - velocity) * _damping;
        ApplyCentralForce(force);
        
        // Angular velocity damping
        AngularVelocity *= Mathf.Exp(-_angularDamping * delta);
    }

    /// <summary>
    /// Drop the item - re-enable normal physics
    /// </summary>
    public void OnDropped()
    {
        Freeze = false;
        FreezeMode = FreezeModeEnum.Kinematic;
        GravityScale = 1.0f;
        CollisionLayer = 1;
        CollisionMask = 1;
        GD.Print($"[PhysItem] Dropped: {Name}");
    }

    /// <summary>
    /// Throw the item in a direction
    /// </summary>
    public void OnThrown(Vector3 throwDirection, float force)
    {
        // Re-enable physics
        Freeze = false;
        FreezeMode = FreezeModeEnum.Kinematic;
        GravityScale = 1.0f;
        CollisionLayer = 1;
        CollisionMask = 1;
        
        // Apply throw impulse
        ApplyImpulse(throwDirection * force);
    }
}
