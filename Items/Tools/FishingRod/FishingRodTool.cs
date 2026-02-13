using System.Collections.Generic;
using System.Threading.Tasks;
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
    private PackedScene _bobberScene; // Template stored in scene
    private Node3D _rodTip; // Position where the line attaches to the rod
    private float _currentLength = 3.0f;
    private const float MinLength = 0.5f;
    private const float MaxLength = 50.0f;
    private const float ReelSpeed = 3.0f; // units per second (continuous)
    private const float CastForceForward = 10.0f;
    private const float CastForceUp = 3f;
    
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
    

    MeshInstance3D _fishMesh;

    private FishManager _fishManager;

    private InventoryManager _inventoryManager;

    ItemDefinition hookedItem;

    private ReelStrength _reelStrength;
    
    public override void _Ready()
    {
        _fishManager = GetTree().Root.GetNode<FishManager>("FishManager");
        _inventoryManager = GetTree().Root.GetNode<InventoryManager>("InventoryManager");
        base._Ready();
        Log("Ready");
        
        // Position and rotation are now controlled by the hand bone and tool scene positioning
        // No need to set them here anymore
        
        // Get reference to bobber template (it should be a child in the scene)
        _bobberScene = ResourceLoader.Load<PackedScene>("res://Items/Tools/FishingRod/bobber.tscn");
        
        
        // Find rod tip marker
        _rodTip = GetNodeOrNull<Node3D>("RodTip");

        
        // Setup line mesh for drawing
        _lineMesh = new ImmediateMesh();
        _lineMeshInstance = GetNode<MeshInstance3D>("LineMesh");
        _lineMeshInstance.Mesh = _lineMesh;
        // Make the mesh instance top-level so it doesn't inherit our transform
        _fishMesh = GetNode<MeshInstance3D>("FishMesh");
        _fishMesh.Visible = false;
        
        
        // Add this tool to the FishingRodTool group so water can find it
        AddToGroup("FishingRodTool");
        
        // Position and rotation are now controlled by the hand bone and tool scene positioning
        // No need to set them here anymore

        _reelStrength = GetNode<ReelStrength>("Control");
    }
    
    public override void PrimaryFire(Character character)
    {
        holdingCharacter = character;

        if (hookedItem != null)
        {
            _inventoryManager.RequestSpawnInstance(hookedItem.ResourcePath, character.inventoryId, new NodePath(), 1, false);
            character.SendDialogMessage($"You caught a {hookedItem.Name}!");
            hookedItem = null;
            _fishMesh.Visible = false;
            return;
        }
        
        if (_state == FishingState.Idle)
        {
            // Cast the bobber
            character.animTree.Cast();
            Log("Casting bobber animation triggered");
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
        holdingCharacter = character;

        if (_state == FishingState.Cast)
        {
            // Mark that right button is held for continuous letting out
            _isRightButtonHeld = true;
        }
    }
    
    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_bobber != null && IsInstanceValid(_bobber) && _rodTip != null)
        {
            DrawFishingLine();
        }
        else
        {
            // Clear line if no bobber
            _lineMesh.ClearSurfaces();
        }
        

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
                    // Reeling in - set animation target to 1
                    if (holdingCharacter != null)
                    {
                        holdingCharacter.animTree.ReelTarget = 1.0f;
                    }
                    
                    // Reel in
                    _currentLength = Mathf.Max(MinLength, _currentLength - (ReelSpeed * (float)delta));
                    _reelStrength.UpdateReelStrength(1.2f);
                    
                    // Check if fully reeled in - reset to idle
                    if (_currentLength <= MinLength)
                    {
                        ResetRod();
                    }
                }
                else if (isRightHeld && !isLeftHeld)
                {
                    // Reeling out - set animation target to -1
                    if (holdingCharacter != null)
                    {
                        holdingCharacter.animTree.ReelTarget = -1.0f;
                    }
                    
                    // Let out
                    _reelStrength.UpdateReelStrength(.9f);
                    _currentLength = Mathf.Min(MaxLength, _currentLength + (ReelSpeed * (float)delta));
                }
                else
                {
                    // Not reeling - set animation target to 0 (idle)
                    if (holdingCharacter != null)
                    {
                        holdingCharacter.animTree.ReelTarget = 0.0f;
                    }
                }
            }
            else
            {
                // During cooldown, ensure reel animation is idle
                if (holdingCharacter != null)
                {
                    holdingCharacter.animTree.ReelTarget = 0.0f;
                }
            }
        }
        else
        {
            // Not in Cast state - ensure reel animation is idle
            if (holdingCharacter != null)
            {
                holdingCharacter.animTree.ReelTarget = 0.0f;
            }
        }
    
    }
    
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        // Apply spring physics to bobber when cast
        if (_state == FishingState.Cast && _bobber != null && IsInstanceValid(_bobber) && _rodTip != null)
        {
            if (_hasLanded) 
            {
            ApplySpringForce();
            } else
            {
                _currentLength = _rodTip.GlobalPosition.DistanceTo(_bobber.GlobalPosition);
            }
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
        
        Log("Starting cast animation - bobber will spawn in 0.9 seconds");
        
        // Delay the actual bobber spawn/launch by 0.9 seconds to sync with animation
        GetTree().CreateTimer(0.9).Timeout += () => {
            // Make sure we're still in a valid state
            if (holdingCharacter == null || _rodTip == null || _bobberScene == null)
            {
                GD.PushWarning("Cast cancelled - character or rod no longer valid");
                return;
            }
            SpawnAndLaunchBobber();
            
        };
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SpawnNetworkBobbers()
    {
        _bobber = _bobberScene.Instantiate() as RigidBody3D;
        _bobber.SetMultiplayerAuthority(GetMultiplayerAuthority());
        GetNode("/root/Main").AddChild(_bobber);
        Log($"SpawnNetworkBobbers called - bobber added to scene on player {Multiplayer.GetUniqueId()} with authority {_bobber.GetMultiplayerAuthority()}");
        
    }


    
    private void SpawnAndLaunchBobber()
    {
        _fishMesh.Visible = false;
        
        Log("Spawning and launching bobber!");
        
        // Duplicate the bobber template and add it to the world
        Rpc(nameof(SpawnNetworkBobbers));

        Log($"Bobber instantiated and added to world with authority {_bobber.GetMultiplayerAuthority()}");
        
        // Get the camera's forward direction (where the character is looking)
        Camera3D camera = holdingCharacter.GetNode<Camera3D>("Camera3D");
        Vector3 castDirection = -camera.GlobalTransform.Basis.Z;
        
        // Position bobber at the rod tip position
        _bobber.GlobalPosition = _rodTip.GlobalPosition;
        
        Log($"Rod tip position: {_rodTip.GlobalPosition}");
        Log($"Cast direction: {castDirection}");
        Log($"Bobber spawned at: {_bobber.GlobalPosition}");
        
        
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
        
        Log($"Launch velocity: {launchVelocity}");
        
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
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClearNetworkBobbers()
    {
        _bobber.QueueFree();
        _bobber = null;
    }

    
    private void ResetRod()
    {
        Log("Bobber fully reeled in - resetting rod");

        hookedItem = _fishManager.GetFishingLoot();
        if (hookedItem != null && hookedItem.Icon != null)
        {
            // Load the texture first, then get the image from it
            Texture2D originalTexture = GD.Load<Texture2D>(hookedItem.Icon);
            Image fishImage = originalTexture.GetImage();
                    // Rotate the image
            fishImage.Rotate90(ClockDirection.Clockwise);
                // Create a new texture from the rotated image
            Texture2D fishTexture = ImageTexture.CreateFromImage(fishImage);
                
            _fishMesh.Visible = true;
                
            var mat = _fishMesh.GetActiveMaterial(0) as StandardMaterial3D;
            if (mat == null)
            {
                mat = new StandardMaterial3D();
                _fishMesh.SetMaterialOverride(mat);
            }
            mat.AlbedoTexture = fishTexture;
        }

        
        
        // Reset reel animation to idle
        if (holdingCharacter != null)
        {
            holdingCharacter.animTree.ReelTarget = 0.0f;
        }
        
        // Clean up bobber
        Rpc(nameof(ClearNetworkBobbers));
        
        // Reset to idle state so we can cast again
        _state = FishingState.Idle;
        _hasLanded = false;
        _currentLength = 3.0f;
    }
    
    public void OnBobberLanded(Node body)
    {
        // Check if bobber hit water (Area3D named WaterVolume)
        if (body is Area3D waterArea)
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
                    
                    Log($"Bobber landed in water! Line length: {_currentLength:F2}m");
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
            
            Log($"Bobber landed! Line length: {_currentLength:F2}m");
        }
    }
    
    private void DrawFishingLine()
    {
        if (_lineMesh == null)
        {
            Log("Cannot draw line: _lineMesh is null");
            return;
        }
        if (_rodTip == null)
        {
            Log("Cannot draw line: _rodTip is null");
            return;
        }
        if (_bobber == null)
        {
            Log("Cannot draw line: _bobber is null");
            return;
        }
        if (!IsInstanceValid(_bobber))
        {
            Log("Cannot draw line: _bobber is not a valid instance");
            return;
        }

        _lineMesh.ClearSurfaces();
        _lineMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

        
        // Get global positions (mesh is top-level, so we use global directly)
        Vector3 tipPos = _rodTip.GlobalPosition;
        Vector3 bobberPos = _bobber.GlobalPosition + Vector3.Up * 0.1f; // Slightly above bobber center

        Vector3 dir = (bobberPos - tipPos).Normalized();
        Vector3 right = dir.Cross(Vector3.Up).Normalized();
        Vector3 up = -right.Cross(dir).Normalized();

        float distance = tipPos.DistanceTo(bobberPos);
        float slack = Mathf.Max(0f, _currentLength - distance);

        List<Vector3> linePoints = new List<Vector3>();

        int segments = 9;
        var space = GetWorld3D().DirectSpaceState;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            Vector3 p = tipPos.Lerp(bobberPos, t);

            float sagFactor = 4f * t * (1f - t);
            p += up * slack * sagFactor;
            

            _lineMesh.SurfaceAddVertex(p);
        }
                    
        
        // Set white color for the line
        _lineMesh.SurfaceSetColor(new Color(1, 1, 1));
        
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
