// using Godot;

// public partial class FishingRod : ToolItem
// {
//     // State management
//     private enum FishingState
//     {
//         Idle,       // Not cast yet
//         Cast        // Bobber has been cast
//     }
    
//     private FishingState _state = FishingState.Idle;
    
//     private RigidBody3D _bobber;
//     private RigidBody3D _bobberTemplate; // Template stored in scene
//     private Node3D _rodTip; // Position where the line attaches to the rod
//     private float _currentLength = 3.0f;
//     private const float MinLength = 0.5f;
//     private const float MaxLength = 50.0f; // Increased for dynamic length
//     private const float ReelSpeed = 3.0f; // units per second (continuous)
//     // Reduced cast forces to prevent tunneling through terrain
//     private const float CastForceForward = 3.0f; // Forward force (reduced from 5.0)
//     private const float CastForceUp = 1.5f; // Upward force for arc (reduced from 2.0)
    
//     // Spring physics for fishing line
//     private const float SpringStiffness = 50.0f;
//     private const float SpringDamping = 5.0f;
    
//     // For drawing the fishing line
//     private ImmediateMesh _lineMesh;
//     private MeshInstance3D _lineMeshInstance;
    
//     // Track if bobber has landed
//     private bool _hasLanded = false;
    
//     // Prevent immediate reeling after cast
//     private double _castTime = 0;
//     private const double CastCooldown = 1; // 300ms delay before allowing reeling
    
//     public override void _Ready()
//     {
//         base._Ready();
        
//         // Get reference to bobber template (it's a child in the scene)
//         _bobberTemplate = GetNodeOrNull<RigidBody3D>("Bobber");
//         if (_bobberTemplate != null)
//         {
//             // Keep it frozen and hidden initially
//             _bobberTemplate.Freeze = true;
//             _bobberTemplate.Visible = false;
//         }
        
//         // Find or create rod tip marker
//         _rodTip = GetNodeOrNull<Node3D>("RodTip");
//         if (_rodTip == null)
//         {
//             // Create rod tip at end of the rod if not found
//             _rodTip = new Node3D();
//             _rodTip.Name = "RodTip";
//             AddChild(_rodTip);
//             // Position it at the tip of the rod (adjust as needed)
//             _rodTip.Position = new Vector3(0, 0, -1.5f);
//         }
        
//         // Setup line mesh for drawing
//         _lineMesh = new ImmediateMesh();
//         _lineMeshInstance = new MeshInstance3D();
//         _lineMeshInstance.Mesh = _lineMesh;
//         _lineMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
//         // Make the mesh instance top-level so it doesn't inherit our transform
//         _lineMeshInstance.TopLevel = true;
//         AddChild(_lineMeshInstance);
//     }
    
//     public override void OnEquip()
//     {
//         base.OnEquip();
//         GD.Print("Fishing Rod equipped");
        
//         // Reset to idle state when equipped
//         _state = FishingState.Idle;
//         _hasLanded = false;
        
//         // Clean up any existing bobber
//         if (_bobber != null && IsInstanceValid(_bobber))
//         {
//             _bobber.QueueFree();
//             _bobber = null;
//         }
//     }
    
//     public override void OnUnequip()
//     {
//         base.OnUnequip();
//         GD.Print("Fishing Rod unequipped");
        
//         // Clean up bobber when unequipped
//         if (_bobber != null && IsInstanceValid(_bobber))
//         {
//             // Disconnect signal if connected
//             if (_bobber.IsConnected("body_entered", new Callable(this, nameof(OnBobberLanded))))
//             {
//                 _bobber.BodyEntered -= OnBobberLanded;
//             }
//             _bobber.QueueFree();
//             _bobber = null;
//         }
        
//         _state = FishingState.Idle;
//         _hasLanded = false;
//     }

//     public override void OnPrimaryFire()
//     {
//         if (_state == FishingState.Idle)
//         {
//             // Cast the bobber
//             CastBobber();
//         }
//         // After casting, primary fire is handled in _Process() for continuous reeling
//     }
    
//     public override void OnSecondaryFire()
//     {
//         // Secondary fire is handled in _Process() for continuous letting out
//     }
    
//     public override void _Process(double delta)
//     {
//         // Handle continuous reeling in/out while buttons are held
//         if (_state == FishingState.Cast)
//         {
//             // Only allow reeling after cast cooldown has passed
//             double timeSinceCast = Time.GetTicksMsec() / 1000.0 - _castTime;
            
//             if (timeSinceCast > CastCooldown)
//             {
//                 // Check if left mouse button is held down for reeling in
//                 bool isLeftHeld = Input.IsMouseButtonPressed(MouseButton.Left);
//                 // Check if right mouse button is held down for letting out
//                 bool isRightHeld = Input.IsMouseButtonPressed(MouseButton.Right);
                
//                 if (isLeftHeld && !isRightHeld)
//                 {
//                     // Reel in
//                     _currentLength = Mathf.Max(MinLength, _currentLength - (ReelSpeed * (float)delta));
//                     GD.Print($"Reeling in - Line length: {_currentLength:F2}m");
                    
//                     // Check if fully reeled in - reset to idle
//                     if (_currentLength <= MinLength)
//                     {
//                         ResetRod();
//                     }
//                 }
//                 else if (isRightHeld && !isLeftHeld)
//                 {
//                     // Let out
//                     _currentLength = Mathf.Min(MaxLength, _currentLength + (ReelSpeed * (float)delta));
//                     GD.Print($"Letting out - Line length: {_currentLength:F2}m");
//                 }
//             }
//         }
        
//         // Draw the fishing line from rod tip to bobber
//         if (_state == FishingState.Cast && _rodTip != null && _bobber != null && _lineMesh != null)
//         {
//             DrawFishingLine();
//         }
//         else
//         {
//             // Clear the line when not cast
//             _lineMesh?.ClearSurfaces();
//         }
//     }
    
//     public override void _PhysicsProcess(double delta)
//     {
//         base._PhysicsProcess(delta);
        
//         // Apply spring physics to bobber when cast
//         if (_state == FishingState.Cast && _bobber != null && IsInstanceValid(_bobber) && _rodTip != null)
//         {
//             ApplySpringForce();
//         }
//     }
    
//     private void ApplySpringForce()
//     {
//         Vector3 rodTipPos = _rodTip.GlobalPosition;
//         Vector3 bobberPos = _bobber.GlobalPosition;
//         Vector3 toRod = rodTipPos - bobberPos;
//         float distance = toRod.Length();
        
//         // Only apply force if bobber is beyond the max length
//         if (distance > _currentLength)
//         {
//             Vector3 direction = toRod.Normalized();
//             float extension = distance - _currentLength;
            
//             // Separate horizontal and vertical components for different handling
//             Vector3 horizontalDirection = new Vector3(direction.X, 0, direction.Z).Normalized();
//             float verticalComponent = direction.Y;
            
//             // Emphasize horizontal force, reduce vertical (prevents jumping out of water)
//             float horizontalMultiplier = 1.5f; // Pull harder horizontally
//             float verticalMultiplier = 0.3f;   // Pull gently upward (let buoyancy do the work)
            
//             // Reconstruct the direction with adjusted components
//             Vector3 adjustedDirection = (horizontalDirection * horizontalMultiplier + Vector3.Up * verticalComponent * verticalMultiplier).Normalized();
            
//             // Spring force (Hooke's law)
//             Vector3 springForce = adjustedDirection * extension * SpringStiffness;
            
//             // Damping force (opposite to velocity)
//             Vector3 dampingForce = -_bobber.LinearVelocity * SpringDamping;
            
//             // Apply combined force
//             _bobber.ApplyCentralForce(springForce + dampingForce);
//         }
//     }
    
//     private void CastBobber()
//     {
//         if (_bobberTemplate == null || _rodTip == null) return;
        
//         GD.Print("Casting bobber!");
        
//         // Duplicate the bobber template and add it to the world
//         _bobber = _bobberTemplate.Duplicate() as RigidBody3D;
//         if (_bobber == null) return;
        
//         // Add bobber to the world (root node) so it's independent
//         GetTree().Root.AddChild(_bobber);
        
//         // Get the forward direction for positioning
//         Vector3 castDirection = -GlobalTransform.Basis.Z;
        
//         // Position bobber slightly in front of rod tip to avoid immediate collision
//         _bobber.GlobalPosition = _rodTip.GlobalPosition + (castDirection * 0.5f);
//         _bobber.Visible = true;
//         _bobber.Freeze = false;
//         _bobber.CollisionLayer = 1;
//         _bobber.CollisionMask = 1;
        
//         // Set physics properties for better collision detection
//         _bobber.Mass = 1; // Increased from 0.2 for more stable collisions
//         _bobber.LinearDamp = 0.1f; // Increased air resistance to slow down
//         _bobber.AngularDamp = 3.0f; // Rotation damping
//         _bobber.LockRotation = true; // Prevent spinning
        
//         // Enable Continuous Collision Detection for fast-moving small object
//         _bobber.ContinuousCd = true; // Critical for preventing tunneling!
        
//         // Delay contact monitoring to prevent immediate collision detection
//         _bobber.ContactMonitor = false;
//         _bobber.MaxContactsReported = 1;
        
//         // Get the forward direction and launch in an arc (forward + up)
//         Vector3 upDirection = Vector3.Up;
        
//         // Combine forward and upward force for an arc
//         Vector3 launchVelocity = (castDirection * CastForceForward) + (upDirection * CastForceUp);
        
//         // Apply impulse to launch the bobber in an arc
//         _bobber.LinearVelocity = Vector3.Zero;
//         _bobber.ApplyCentralImpulse(launchVelocity);
        
//         // Reset state
//         _hasLanded = false;
//         _currentLength = MaxLength; // Start with max length until it lands
//         _castTime = Time.GetTicksMsec() / 1000.0; // Record cast time
        
//         // Change state to Cast
//         _state = FishingState.Cast;
        
//         // Enable contact monitoring after a short delay to avoid immediate collision
//         GetTree().CreateTimer(0.1).Timeout += () => {
//             if (_bobber != null && IsInstanceValid(_bobber))
//             {
//                 _bobber.ContactMonitor = true;
//                 _bobber.BodyEntered += OnBobberLanded;
//             }
//         };
//     }
    
//     private void ResetRod()
//     {
//         GD.Print("Bobber fully reeled in - resetting rod");
        
//         // Clean up bobber
//         if (_bobber != null && IsInstanceValid(_bobber))
//         {
//             // Disconnect signal if connected
//             if (_bobber.IsConnected("body_entered", new Callable(this, nameof(OnBobberLanded))))
//             {
//                 _bobber.BodyEntered -= OnBobberLanded;
//             }
//             _bobber.QueueFree();
//             _bobber = null;
//         }
        
//         // Reset to idle state so we can cast again
//         _state = FishingState.Idle;
//         _hasLanded = false;
//         _currentLength = 3.0f;
//     }
    
//     private void OnBobberLanded(Node body)
//     {
//         // Check if bobber hit water (Area3D named WaterVolume)
//         if (body is Area3D waterArea && waterArea.Name == "WaterVolume")
//         {
//             // Bobber hit water - set as landed and allow reeling immediately
//             if (!_hasLanded)
//             {
//                 _hasLanded = true;
                
//                 // Set line length to the distance when it first lands in water
//                 if (_rodTip != null && _bobber != null)
//                 {
//                     float landingDistance = _rodTip.GlobalPosition.DistanceTo(_bobber.GlobalPosition);
//                     _currentLength = Mathf.Clamp(landingDistance, MinLength, MaxLength);
                    
//                     GD.Print($"Bobber landed in water! Line length set to: {_currentLength:F2}m");
//                 }
                
//                 // Reset cast cooldown so player can immediately start reeling
//                 _castTime = 0;
//             }
//             return;
//         }
        
//         // Only set the line length on the first collision with solid objects (terrain)
//         if (!_hasLanded && _rodTip != null && _bobber != null)
//         {
//             _hasLanded = true;
            
//             // Set line length to the distance when it first lands
//             float landingDistance = _rodTip.GlobalPosition.DistanceTo(_bobber.GlobalPosition);
//             _currentLength = Mathf.Clamp(landingDistance, MinLength, MaxLength);
            
//             GD.Print($"Bobber landed! Line length set to: {_currentLength:F2}m");
//         }
//     }
    
//     private void DrawFishingLine()
//     {
//         if (_lineMesh == null || _rodTip == null || _bobber == null || !IsInstanceValid(_bobber)) return;
        
//         _lineMesh.ClearSurfaces();
//         _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        
//         // Get global positions (mesh is now top-level, so we use global directly)
//         Vector3 tipPos = _rodTip.GlobalPosition;
//         Vector3 bobberPos = _bobber.GlobalPosition;
        
//         // Debug print to verify coordinates
//         // GD.Print($"Line: RodTip {tipPos} -> Bobber {bobberPos}");
        
//         // Set white color for the line
//         _lineMesh.SurfaceSetColor(new Color(1, 1, 1));
//         _lineMesh.SurfaceAddVertex(tipPos);
//         _lineMesh.SurfaceAddVertex(bobberPos);
        
//         _lineMesh.SurfaceEnd();
//     }
// }