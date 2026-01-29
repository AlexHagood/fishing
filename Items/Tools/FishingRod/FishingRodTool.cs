using Godot;

public partial class FishingRodTool : ToolScript
{
        
    // State management
    private enum FishingState
    {
        Idle,       // Not cast yet
        Cast        // Bobber has been cast
    }
    
    private FishingState _state = FishingState.Idle;
    
    private RigidBody3D _bobber;
    private RigidBody3D _bobberTemplate; // Template stored in scene
    private Node3D _rodTip; // Position where the line attaches to the rod
    private float _currentLength = 3.0f;
    private const float MinLength = 0.5f;
    private const float MaxLength = 50.0f;
    private const float ReelSpeed = 3.0f; // units per second (continuous)
    private const float CastForceForward = 3.0f;
    private const float CastForceUp = 1.5f;
    
    // Spring physics for fishing line
    private const float SpringStiffness = 50.0f;
    private const float SpringDamping = 5.0f;
    
    // For drawing the fishing line
    private ImmediateMesh _lineMesh;
    private MeshInstance3D _lineMeshInstance;
    
    // Track if bobber has landed
    private bool _hasLanded = false;
    
    // Prevent immediate reeling after cast
    private double _castTime = 0;
    private const double CastCooldown = 1.0; // 1 second delay before allowing reeling
    
    // Track button states for continuous input
    private bool _isLeftButtonHeld = false;
    private bool _isRightButtonHeld = false;
    
    private Character _character; // Reference to the character using this tool
    
    public override void _Ready()
    {
        base._Ready();
        GD.Print("[FishingRodTool] Ready");
        
        // Position and rotation are now controlled by the hand bone and tool scene positioning
        // No need to set them here anymore
        
        // Get reference to bobber template (it should be a child in the scene)
        _bobberTemplate = GetNodeOrNull<RigidBody3D>("Bobber");
        if (_bobberTemplate != null)
        {
            // Keep it frozen and hidden initially
            _bobberTemplate.Freeze = true;
            _bobberTemplate.Visible = false;
        }
        else
        {
            GD.PushWarning("[FishingRodTool] No Bobber child found in scene!");
        }
        
        // Find rod tip marker
        _rodTip = GetNodeOrNull<Node3D>("RodTip");

        
        // Setup line mesh for drawing
        _lineMesh = new ImmediateMesh();
        _lineMeshInstance = new MeshInstance3D();
        _lineMeshInstance.Mesh = _lineMesh;
        _lineMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        // Make the mesh instance top-level so it doesn't inherit our transform
        _lineMeshInstance.TopLevel = true;
        AddChild(_lineMeshInstance);
        
        // Position and rotation are now controlled by the hand bone and tool scene positioning
        // No need to set them here anymore
    }
    
    public override void PrimaryFire(Character character)
    {
        _character = character;
        
        if (_state == FishingState.Idle)
        {
            // Cast the bobber
            CastBobber();
        }
        else if (_state == FishingState.Cast)
        {
            // Mark that left button is held for continuous reeling
            _isLeftButtonHeld = true;
        }
    }
    
    public override void SecondaryFire(Character character)
    {
        _character = character;
        
        if (_state == FishingState.Cast)
        {
            // Mark that right button is held for continuous letting out
            _isRightButtonHeld = true;
        }
    }
    
    public override void _Process(double delta)
    {
        base._Process(delta);
        

        // Handle continuous reeling in/out while buttons are held
        if (_state == FishingState.Cast)
        {
            // Only allow reeling after cast cooldown has passed
            double timeSinceCast = Time.GetTicksMsec() / 1000.0 - _castTime;
            
            if (timeSinceCast > CastCooldown)
            {
                // Check if buttons are currently held
                bool isLeftHeld = Input.IsMouseButtonPressed(MouseButton.Left);
                bool isRightHeld = Input.IsMouseButtonPressed(MouseButton.Right);
                
                if (isLeftHeld && !isRightHeld)
                {
                    // Reel in
                    _currentLength = Mathf.Max(MinLength, _currentLength - (ReelSpeed * (float)delta));
                    
                    // Check if fully reeled in - reset to idle
                    if (_currentLength <= MinLength)
                    {
                        ResetRod();
                    }
                }
                else if (isRightHeld && !isLeftHeld)
                {
                    // Let out
                    _currentLength = Mathf.Min(MaxLength, _currentLength + (ReelSpeed * (float)delta));
                }
            }
        }
        
        // Draw the fishing line from rod tip to bobber
        if (_state == FishingState.Cast && _rodTip != null && _bobber != null && IsInstanceValid(_bobber))
        {
            DrawFishingLine();
        }
        else
        {
            // Clear the line when not cast
            _lineMesh?.ClearSurfaces();
        }
    }
    
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        // Apply spring physics to bobber when cast
        if (_state == FishingState.Cast && _bobber != null && IsInstanceValid(_bobber) && _rodTip != null)
        {
            ApplySpringForce();
        }
    }
    
    private void ApplySpringForce()
    {
        Vector3 rodTipPos = _rodTip.GlobalPosition;
        Vector3 bobberPos = _bobber.GlobalPosition;
        Vector3 toRod = rodTipPos - bobberPos;
        float distance = toRod.Length();
        
        // Only apply force if bobber is beyond the max length
        if (distance > _currentLength)
        {
            Vector3 direction = toRod.Normalized();
            float extension = distance - _currentLength;
            
            // Separate horizontal and vertical components for different handling
            Vector3 horizontalDirection = new Vector3(direction.X, 0, direction.Z).Normalized();
            float verticalComponent = direction.Y;
            
            // Emphasize horizontal force, reduce vertical (prevents jumping out of water)
            float horizontalMultiplier = 1.5f;
            float verticalMultiplier = 0.3f;
            
            // Reconstruct the direction with adjusted components
            Vector3 adjustedDirection = (horizontalDirection * horizontalMultiplier + Vector3.Up * verticalComponent * verticalMultiplier).Normalized();
            
            // Spring force (Hooke's law)
            Vector3 springForce = adjustedDirection * extension * SpringStiffness;
            
            // Damping force (opposite to velocity)
            Vector3 dampingForce = -_bobber.LinearVelocity * SpringDamping;
            
            // Apply combined force
            _bobber.ApplyCentralForce(springForce + dampingForce);
        }
    }
    
    private void CastBobber()
    {
        if (_bobberTemplate == null || _rodTip == null || _character == null)
        {
            GD.PushWarning("[FishingRodTool] Cannot cast - missing bobber template, rod tip, or character!");
            return;
        }
        
        GD.Print("[FishingRodTool] Casting bobber!");
        
        // Duplicate the bobber template and add it to the world
        _bobber = _bobberTemplate.Duplicate() as RigidBody3D;
        if (_bobber == null) return;
        
        // Add bobber to the world (root node) so it's independent
        GetTree().Root.AddChild(_bobber);
        
        // Get the camera's forward direction (where the character is looking)
        Camera3D camera = _character.GetNode<Camera3D>("Camera3D");
        Vector3 castDirection = -camera.GlobalTransform.Basis.Z;
        
        // Position bobber at the rod tip position
        _bobber.GlobalPosition = _rodTip.GlobalPosition;
        
        GD.Print($"[FishingRodTool] Rod tip position: {_rodTip.GlobalPosition}");
        GD.Print($"[FishingRodTool] Cast direction: {castDirection}");
        GD.Print($"[FishingRodTool] Bobber spawned at: {_bobber.GlobalPosition}");
        
        _bobber.Visible = true;
        _bobber.Freeze = false;
        _bobber.CollisionLayer = 1;
        _bobber.CollisionMask = 1;
        
        // Set physics properties
        _bobber.Mass = 1.0f;
        _bobber.LinearDamp = 0.1f;
        _bobber.AngularDamp = 3.0f;
        _bobber.LockRotation = true;
        
        // Enable Continuous Collision Detection
        _bobber.ContinuousCd = true;
        
        // Delay contact monitoring to prevent immediate collision detection
        _bobber.ContactMonitor = false;
        _bobber.MaxContactsReported = 1;
        
        // Get the forward direction and launch in an arc
        Vector3 upDirection = Vector3.Up;
        
        // Combine forward and upward force for an arc
        Vector3 launchVelocity = (castDirection * CastForceForward) + (upDirection * CastForceUp);
        
        GD.Print($"[FishingRodTool] Launch velocity: {launchVelocity}");
        
        // Apply impulse to launch the bobber
        _bobber.LinearVelocity = Vector3.Zero;
        _bobber.ApplyCentralImpulse(launchVelocity);
        
        // Reset state
        _hasLanded = false;
        _currentLength = MaxLength; // Start with max length until it lands
        _castTime = Time.GetTicksMsec() / 1000.0; // Record cast time
        
        // Change state to Cast
        _state = FishingState.Cast;
        
        // Enable contact monitoring after a short delay
        GetTree().CreateTimer(0.1).Timeout += () => {
            if (_bobber != null && IsInstanceValid(_bobber))
            {
                _bobber.ContactMonitor = true;
                _bobber.BodyEntered += OnBobberLanded;
            }
        };
    }
    
    private void ResetRod()
    {
        GD.Print("[FishingRodTool] Bobber fully reeled in - resetting rod");
        
        // Clean up bobber
        if (_bobber != null && IsInstanceValid(_bobber))
        {
            // Disconnect signal if connected
            if (_bobber.IsConnected("body_entered", new Callable(this, nameof(OnBobberLanded))))
            {
                _bobber.BodyEntered -= OnBobberLanded;
            }
            _bobber.QueueFree();
            _bobber = null;
        }
        
        // Reset to idle state so we can cast again
        _state = FishingState.Idle;
        _hasLanded = false;
        _currentLength = 3.0f;
    }
    
    private void OnBobberLanded(Node body)
    {
        // Check if bobber hit water (Area3D named WaterVolume)
        if (body is Area3D waterArea && waterArea.Name == "WaterVolume")
        {
            // Bobber hit water - set as landed and allow reeling immediately
            if (!_hasLanded)
            {
                _hasLanded = true;
                
                // Set line length to the distance when it first lands in water
                if (_rodTip != null && _bobber != null)
                {
                    float landingDistance = _rodTip.GlobalPosition.DistanceTo(_bobber.GlobalPosition);
                    _currentLength = Mathf.Clamp(landingDistance, MinLength, MaxLength);
                    
                    GD.Print($"[FishingRodTool] Bobber landed in water! Line length: {_currentLength:F2}m");
                }
                
                // Reset cast cooldown so player can immediately start reeling
                _castTime = 0;
            }
            return;
        }
        
        // Only set the line length on the first collision with solid objects (terrain)
        if (!_hasLanded && _rodTip != null && _bobber != null)
        {
            _hasLanded = true;
            
            // Set line length to the distance when it first lands
            float landingDistance = _rodTip.GlobalPosition.DistanceTo(_bobber.GlobalPosition);
            _currentLength = Mathf.Clamp(landingDistance, MinLength, MaxLength);
            
            GD.Print($"[FishingRodTool] Bobber landed! Line length: {_currentLength:F2}m");
        }
    }
    
    private void DrawFishingLine()
    {
        if (_lineMesh == null || _rodTip == null || _bobber == null || !IsInstanceValid(_bobber)) return;
        
        _lineMesh.ClearSurfaces();
        _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        
        // Get global positions (mesh is top-level, so we use global directly)
        Vector3 tipPos = _rodTip.GlobalPosition;
        Vector3 bobberPos = _bobber.GlobalPosition;
        
        // Set white color for the line
        _lineMesh.SurfaceSetColor(new Color(1, 1, 1));
        _lineMesh.SurfaceAddVertex(tipPos);
        _lineMesh.SurfaceAddVertex(bobberPos);
        
        _lineMesh.SurfaceEnd();
    }
    
    // Clean up when tool is removed/switched
    public override void _ExitTree()
    {
        base._ExitTree();
        
        // Clean up bobber when tool is removed
        if (_bobber != null && IsInstanceValid(_bobber))
        {
            if (_bobber.IsConnected("body_entered", new Callable(this, nameof(OnBobberLanded))))
            {
                _bobber.BodyEntered -= OnBobberLanded;
            }
            _bobber.QueueFree();
            _bobber = null;
        }
        
        _state = FishingState.Idle;
        _hasLanded = false;
    }
}
