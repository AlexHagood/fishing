using Godot;

public partial class FishingRod : ToolItem
{
    private Generic6DofJoint3D _joint;
    private RigidBody3D _bobber;
    private float _currentLength = 3.0f;
    private const float MinLength = 0.5f;
    private const float MaxLength = 10.0f;
    private const float ReelSpeed = 2.0f; // units per second

    
    public override void _Ready()
    {
        base._Ready();
        
        // Get references to the joint and bobber
        _joint = GetNodeOrNull<Generic6DofJoint3D>("Generic6DOFJoint3D");
        _bobber = GetNodeOrNull<RigidBody3D>("Bobber");
        
        if (_joint != null)
        {
            // Get the initial length from one of the linear limits
            _currentLength = _joint.Get("linear_limit_x/upper_distance").AsSingle();
        }
    }
    
    public override void OnEquip()
    {
        GD.Print("Fishing Rod equipped");
        
        // Enable bobber physics when equipped
        if (_bobber != null)
        {
            _bobber.Freeze = false;
            _bobber.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            _bobber.CollisionLayer = 1;
            _bobber.CollisionMask = 1;
        }
        
        // Ensure joint is active
        if (_joint != null && _physicsBody != null)
        {
            _joint.NodeA = _physicsBody.GetPath();
            _joint.NodeB = _bobber.GetPath();
        }
    }
    
    public override void OnUnequip()
    {
        GD.Print("Fishing Rod unequipped");
        
        // Freeze bobber when unequipped
        if (_bobber != null)
        {
            _bobber.Freeze = true;
            _bobber.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        }
    }

    public override void OnPrimaryFire()
    {
        // Reel in (decrease length)
        ReelIn();
    }
    
    public override void OnSecondaryFire()
    {
        // Let out (increase length)
        LetOut();
    }
    
    public override void _Process(double delta)
    {
        // Could add continuous reeling if holding the button
        // For now, discrete actions work fine
    }
    
    private void ReelIn()
    {
        if (_joint == null) return;
        
        _currentLength = Mathf.Max(MinLength, _currentLength - (ReelSpeed * 0.1f));
        UpdateJointLength();
        GD.Print($"Reeling in: {_currentLength:F2}m");
    }
    
    private void LetOut()
    {
        if (_joint == null) return;
        
        _currentLength = Mathf.Min(MaxLength, _currentLength + (ReelSpeed * 0.1f));
        UpdateJointLength();
        GD.Print($"Letting out: {_currentLength:F2}m");
    }
    
    private void UpdateJointLength()
    {
        if (_joint == null) return;
        
        // Update all three linear limits to the current length
        _joint.Set("linear_limit_x/upper_distance", _currentLength);
        _joint.Set("linear_limit_y/upper_distance", _currentLength);
        _joint.Set("linear_limit_z/upper_distance", _currentLength);
    }
}