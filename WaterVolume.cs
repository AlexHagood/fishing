using Godot;
using System.Collections.Generic;

/// <summary>
/// Water volume that applies buoyancy and drag forces to RigidBody3D objects.
/// Designed for realistic fishing game physics.
/// </summary>
[GlobalClass]
public partial class WaterVolume : Area3D
{
    #region Exports
    
    [Export] public float WaterDrag { get; set; } = 2.0f; // Slows down movement in water
    
    #endregion
    
    #region Private Fields
    
    private HashSet<RigidBody3D> _bodiesInWater = new HashSet<RigidBody3D>();
    private AudioStreamPlayer3D _splashPlayer;
    private Node3D _waterRoot; // Parent node to get water surface position
    
    #endregion
    
    #region Lifecycle Methods
    
    public override void _Ready()
    {
        // Get parent node for water surface position
        _waterRoot = GetParent<Node3D>();
        if (_waterRoot == null)
        {
            GD.PushWarning("WaterVolume: Parent is not a Node3D. Water surface position may be incorrect.");
        }
        
        // Set up Area3D for collision detection
        Monitoring = true;
        Monitorable = true;
        
        // Make sure we can detect bodies on all layers
        CollisionLayer = 2; // Water is on layer 2
        CollisionMask = 1;  // Detect bodies on layer 1 (default physics layer)
        
        // Get audio player
        _splashPlayer = GetNodeOrNull<AudioStreamPlayer3D>("SplashPlayer");
        if (_splashPlayer == null)
        {
            GD.PushWarning("WaterVolume: No AudioStreamPlayer3D child named 'SplashPlayer' found. Splash sounds disabled.");
        }
        
        // Connect signals
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        
        GD.Print($"WaterVolume initialized, Monitoring={Monitoring}, Mask={CollisionMask}");
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Apply forces to all bodies currently in water
        foreach (var body in _bodiesInWater)
        {
            if (!IsInstanceValid(body))
                continue;
            
            // Skip if body is frozen
            if (body.Freeze)
                continue;
            
            ApplyWaterForces(body, (float)delta);
        }
    }
    
    #endregion
    
    #region Signal Handlers
    
    private void OnBodyEntered(Node3D body)
    {
        // Only interact with RigidBody3D objects (not CharacterBody3D)
        if (body is RigidBody3D rigidBody)
        {
            _bodiesInWater.Add(rigidBody);
            PlaySplash(rigidBody);
            
            GD.Print($"WaterVolume: {rigidBody.Name} entered water");
        }
    }
    
    private void OnBodyExited(Node3D body)
    {
        if (body is RigidBody3D rigidBody)
        {
            _bodiesInWater.Remove(rigidBody);
            GD.Print($"WaterVolume: {rigidBody.Name} exited water");
        }
    }
    
    #endregion
    
    #region Water Physics
    
    private void ApplyWaterForces(RigidBody3D body, float delta)
    {
        // Skip if body is frozen
        if (body.Freeze)
            return;
        
        // Get buoyancy from the body (default to 1.0 if not set)
        float buoyancy = 1.0f;
        
        // Check if this is a GameItem with an ItemDefinition
        if (body is GameItem item && item.ItemDef != null)
        {
            buoyancy = item.ItemDef.Buoyancy;
        }
        
        // Simple physics:
        // - Buoyancy > 1.0 = floats (wood, cork, bobber)
        // - Buoyancy = 1.0 = neutral (suspended in water)
        // - Buoyancy < 1.0 = sinks (metal, stone)
        
        float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);
        Vector3 upwardForce = Vector3.Up * gravity * body.Mass * buoyancy;
        
        body.ApplyCentralForce(upwardForce);
        
        // Simple drag - slow down movement in water
        body.LinearVelocity *= (1.0f - WaterDrag * delta);
        body.AngularVelocity *= (1.0f - WaterDrag * delta);
    }
    
    #endregion
    
    #region Audio
    
    private void PlaySplash(RigidBody3D body)
    {
        if (_splashPlayer != null && _splashPlayer.Stream != null)
        {
            // Position splash sound at entry point
            _splashPlayer.GlobalPosition = body.GlobalPosition;
            _splashPlayer.Play();
        }
    }
    
    #endregion
}
