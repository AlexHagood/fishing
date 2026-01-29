using System.Dynamic;
using Godot;

public partial class Character : CharacterBody3D
{
    [Signal]
    public delegate void InventoryRequestedEventHandler(int inventoryId);
    
    [Signal]
    public delegate void RotateRequestedEventHandler();

    [Signal]
    public delegate void HotbarSlotSelectedEventHandler(int slotIndex);

    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 8.0f;
    [Export] public float Gravity = 20.0f;
    [Export] public float MouseSensitivity = 0.01f;
    
    // Camera system
    [ExportGroup("Camera Settings")]
    [Export] public float ThirdPersonDistance = 3.0f;
    [Export] public float CameraLerpSpeed = 10.0f;
    
    [ExportGroup("Animation Settings")]
    [Export] public float AnimationBlendTime = 0.2f; // Smooth transition time in seconds

    private bool isOnFloor = false;
    private Vector2 mouseDelta = Vector2.Zero;
    private float _cameraPitch = 0.0f; // Vertical camera angle

    private float _baseSpeed = 5.0f;
    private float _sprintSpeed = 10.0f;
    private bool _isSprinting = false;

    private Node3D _holdPosition;
    private PhysItem _heldPhysItem;
    private Node3D _cameraTarget; // Third-person camera pivot
    private SpringArm3D _springArm;  // For third-person collision
    private MeshInstance3D _playerBodyMesh;  // Reference to player's body mesh (not the whole armature!)
    private AnimationPlayer _animationPlayer;  // Character animations
    private BoneAttachment3D _rightHandAttachment;  // Attachment point for held tools
    private Node3D _toolPosition;  // Helper node for tool positioning
    private bool _isPlayingActionAnimation = false;  // Prevent movement animations from interrupting actions

    private Camera3D _activeCamera;
    private Camera3D _firstPersonCamera;
    private Camera3D _thirdPersonCamera;

    private InventoryManager _inventoryManager;
    private ToolScript _currentTool;

    public int inventoryId;
    private int _hotbarSlot = 0;
    public int HotbarSlot { 
        get => _hotbarSlot;
        private set 
        {
            _hotbarSlot = value;
            
            // Safety check: ensure inventory manager is initialized
            if (_inventoryManager == null)
            {
                GD.PushWarning("[Character] InventoryManager not initialized yet, deferring hotbar update");
                return;
            }
            
            ItemInstance? item = _inventoryManager.GetInventory(inventoryId).HotbarItems[value];

            if (item != null)
            {
                GD.Print($"[Character] Hotbar slot {value} has item: {item.ItemData.Name}");
                // Instantiate the tool script for the selected item
                if (_currentTool != null)
                {
                    _currentTool.QueueFree();
                    _currentTool = null;
                }
                if (item.ItemData.ToolScriptScene != null && _toolPosition != null)
                {
                    _currentTool = item.ItemData.ToolScriptScene.Instantiate<ToolScript>();
                    _currentTool.itemInstance = item;
                    // Add tool as child of hand attachment, not character root
                    _toolPosition.AddChild(_currentTool);
                }
            }
            else
            {
                GD.Print($"[Character] Hotbar slot {value} is empty");
                if (_currentTool != null)
                {
                    _currentTool.QueueFree();
                    _currentTool = null;
                }
            }
        }
    }

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Setup camera system
        SetupCameraSystem();
        
        // Get AnimationPlayer
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        
        // Connect to animation_finished signal to track action animations
        if (_animationPlayer != null)
        {
            _animationPlayer.AnimationFinished += OnAnimationFinished;
        }
        
        // Start with idle animation
        if (_animationPlayer != null && _animationPlayer.HasAnimation("Idle"))
        {
            _animationPlayer.Play("Idle");
        }
        
        // Get hand attachment for tools
        _rightHandAttachment = GetNodeOrNull<BoneAttachment3D>("CharacterArmature/Skeleton3D/RightHandAttachment");
        if (_rightHandAttachment == null)
        {
            GD.PrintErr("[Character] ERROR: RightHandAttachment not found! Create a BoneAttachment3D at CharacterArmature/Skeleton3D/RightHandAttachment with bone_name='Fist.R'");
        }
        
        // Get tool position helper (optional, but recommended)
        _toolPosition = GetNodeOrNull<Node3D>("CharacterArmature/Skeleton3D/RightHandAttachment/ToolPosition");
        if (_toolPosition == null)
        {
            GD.Print("[Character] ToolPosition helper not found, tools will attach directly to hand bone");
            // Use the hand attachment itself as fallback
            _toolPosition = _rightHandAttachment;
        }
        
        // Create hold position for physics items (attached to active camera)
        _holdPosition = new Node3D();
        _holdPosition.Name = "HoldPosition";
        _activeCamera.AddChild(_holdPosition);
        _holdPosition.Position = new Vector3(0, -0.5f, -2.0f);
        
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");

        inventoryId = _inventoryManager.CreateInventory(new Vector2I(5, 5));
        

    }
    
    private void SetupCameraSystem()
    {
        // Get camera target from scene (created in editor)
        _cameraTarget = GetNodeOrNull<Node3D>("CameraTarget");
        if (_cameraTarget == null)
        {
            GD.PrintErr("[Character] ERROR: CameraTarget node not found! Create a Node3D called 'CameraTarget' as a child of Character.");
        }
        
        // Get spring arm
        _springArm = GetNodeOrNull<SpringArm3D>("CameraTarget/SpringArm3D");
        if (_springArm == null)
        {
            GD.PrintErr("[Character] ERROR: SpringArm3D not found! Create a SpringArm3D as a child of CameraTarget.");
        }
        else
        {
            _springArm.SpringLength = ThirdPersonDistance;
            _springArm.CollisionMask = 1;
        }

        // Get both cameras
        _firstPersonCamera = GetNodeOrNull<Camera3D>("Camera3D");
        
        _thirdPersonCamera = GetNodeOrNull<Camera3D>("CameraTarget/SpringArm3D/Camera3D");
        
        // Start in first person if camera exists
        if (_firstPersonCamera != null && _thirdPersonCamera != null)
        {
            _activeCamera = _firstPersonCamera;
            _firstPersonCamera.Current = true;
            _thirdPersonCamera.Current = false;
            GD.Print("[Character] Camera system initialized successfully");
        }
        else
        {
            GD.PrintErr("[Character] ERROR: Camera system failed to initialize - missing camera nodes!");
        }
        
        // Initialize camera angles
        _cameraPitch = 0.0f;
        
        // Try to find player body mesh (not the armature, just the visible mesh!)
        _playerBodyMesh = GetNodeOrNull<MeshInstance3D>("CharacterArmature/Skeleton3D/Body");
        
        if (_playerBodyMesh != null)
        {
            setMeshVisibility(false); // Hide body in first person by default (tool stays visible!)
            GD.Print("[Character] Found player body mesh: " + _playerBodyMesh.Name);
        }
        else
        {
            GD.Print("[Character] No player body mesh found - will not hide in first person");
        }
    }

    public void OpenInventory(int id)
    {
        GD.Print("Opening inventory with Id " + id);
        EmitSignal(SignalName.InventoryRequested, id);
    }

    public override void _Process(double delta)
    {
        // Apply floaty physics to held physics item
        if (_heldPhysItem != null && _holdPosition != null)
        {
            _heldPhysItem.ApplyFloatyPhysics(_holdPosition.GlobalPosition, (float)delta);
        }
    }

    public override void _Input(InputEvent @event)
    {        
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            mouseDelta = mouseMotion.Relative;
        }

        // Handle mouse clicks for throw/drop physics items
        if (@event is InputEventMouseButton mouseButton)
        {
            // Mouse wheel scrolling for hotbar (only when not in menu)
            if (Input.MouseMode != Input.MouseModeEnum.Visible)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
                {
                    HotbarSlot = (HotbarSlot + 1 + 6) % 6; // Wrap around: 0 -> 5
                    EmitSignal(SignalName.HotbarSlotSelected, HotbarSlot);
                    GD.Print($"Hotbar scrolled up to slot {HotbarSlot}");
                    return; // Don't process other mouse actions
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
                {
                    HotbarSlot = (HotbarSlot - 1 + 6) % 6; // Wrap around: 5 -> 0
                    EmitSignal(SignalName.HotbarSlotSelected, HotbarSlot);
                    GD.Print($"Hotbar scrolled down to slot {HotbarSlot}");
                    return; // Don't process other mouse actions
                }
            }
            
            // Don't process item actions in menu mode
            
            
            // Left mouse button - throw
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                if (_heldPhysItem != null)
                {
                    ThrowPhysItem();
                }
                if (_currentTool != null)
                {
                    _currentTool.PrimaryFire(this);
                    // Play character animation for tool use if specified
                    if (!string.IsNullOrEmpty(_currentTool.primaryAnimation) && _animationPlayer != null)
                    {
                        if (_animationPlayer.HasAnimation(_currentTool.primaryAnimation))
                        {
                            _animationPlayer.Play(_currentTool.primaryAnimation);
                        }
                    }
                }
            }
            // Right mouse button - drop
            else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                if (_heldPhysItem != null)
                {
                    DropPhysItem();
                }
                if (_currentTool != null)
                {
                    _currentTool.SecondaryFire(this);
                    // Play character animation for tool secondary use if specified
                    if (!string.IsNullOrEmpty(_currentTool.secondaryAnimation) && _animationPlayer != null)
                    {
                        if (_animationPlayer.HasAnimation(_currentTool.secondaryAnimation))
                        {
                            _animationPlayer.Play(_currentTool.secondaryAnimation);
                        }
                    }
                }
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key6)
            {
                GD.Print("Hotbar key pressed: " + keyEvent.Keycode);
                int slotIndex = (int)keyEvent.Keycode - (int)Key.Key1;
                HotbarSlot = slotIndex;
                EmitSignal(SignalName.HotbarSlotSelected, slotIndex);
            }
        }

        // Tab or I key - toggle inventory
        if (Input.IsActionJustPressed("inventory"))
        {
            GD.Print($"Trying to open inventory {inventoryId}");
            OpenInventory(inventoryId);
            return;
        }

        // R key - rotate item in inventory
        if (Input.IsActionJustPressed("rotate"))
        {
            EmitSignal(SignalName.RotateRequested);
        }
        
        // C key - toggle camera mode
        if (Input.IsActionJustPressed("camera"))
        {
            ToggleCameraMode();
        }

        // E key - interact with WorldItem (don't allow in menu mode)
        if (Input.IsActionJustPressed("interact"))
        {
            TryInteractE();
        }

        // F key - interact with WorldItem (don't allow in menu mode)
        if (Input.IsActionJustPressed("pickup"))
        {
            TryInteractF();
        }
        
        // Sprint input handling (don't allow in menu mode)
        if (Input.IsActionPressed("sprint"))
        {
            _isSprinting = true;
        }
        else
        {
            _isSprinting = false;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("fwd"))
        {
            direction -= Transform.Basis.Z;
        }
        if (Input.IsActionPressed("back"))
        {
            direction += Transform.Basis.Z;
        }
        if (Input.IsActionPressed("left"))
        {
            direction -= Transform.Basis.X;
        }
        if (Input.IsActionPressed("right"))
        {
            direction += Transform.Basis.X;
        }

        direction = direction.Normalized();

        // Apply movement speed
        float currentSpeed = _isSprinting ? _sprintSpeed : _baseSpeed;
        Velocity = new Vector3(
            direction.X * currentSpeed,
            Velocity.Y, // preserve vertical component for gravity/jump
            direction.Z * currentSpeed
        );

        // Apply gravity
        if (!isOnFloor)
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);

        // Handle jump
        if (isOnFloor && Input.IsActionJustPressed("jump"))
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);

        // Move the character
        MoveAndSlide();
        isOnFloor = IsOnFloor();

        // Push RigidBody objects when colliding
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            var collider = collision.GetCollider();
            
            if (collider is PhysItem rigidBody)
            {
                // Get the collision normal (direction away from the collider)
                Vector3 pushDirection = -collision.GetNormal();
                
                // Calculate push force based on velocity
                Vector3 pushVelocity = pushDirection * Velocity.Length();
                
                // Apply force (not impulse) - this is continuous pushing
                float pushPower = 5.0f; // Adjust this value for push strength
                rigidBody.ApplyCentralForce(pushVelocity * pushPower);
            }
        }

        // Handle mouse look
        HandleCameraRotation(delta);

        // Update camera position
        UpdateCameraPosition(delta);
        
        // Update animations based on movement state
        UpdateAnimations(direction);
    }
    
    private void UpdateAnimations(Vector3 direction)
    {
        if (_animationPlayer == null) return;
        
        // Don't interrupt action animations (like attacks, tool use, etc.)
        if (_isPlayingActionAnimation) return;
        
        // Determine which animation to play based on state
        string targetAnimation = "Idle";
        
        // Check if in air (jumping/falling)
        if (!isOnFloor)
        {
            targetAnimation = "Jump";
        }
        // Check if moving on ground
        else if (direction.Length() > 0.1f)
        {
            if (_isSprinting)
            {
                targetAnimation = "Run";
            }
            else
            {
                targetAnimation = "Walk";
            }
        }
        
        // Only switch animation if it's different from current and animation exists
        if (_animationPlayer.HasAnimation(targetAnimation) && 
            _animationPlayer.CurrentAnimation != targetAnimation)
        {
            // Use smooth crossfade transition
            // Play(name, customBlend, customSpeed, fromEnd)
            _animationPlayer.Play(targetAnimation, AnimationBlendTime);
        }
    }
    
    private void HandleCameraRotation(double delta)
    {
        // Always rotate the player character with mouse X
        RotateY(-mouseDelta.X * MouseSensitivity);
        
        // Handle vertical camera rotation (pitch)
        _cameraPitch -= mouseDelta.Y * MouseSensitivity * 100.0f;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -80, 80);
        
        // Check which camera is active
        if (_activeCamera == _thirdPersonCamera)
        {
            // Third-person: apply pitch to camera target
            if (_cameraTarget != null)
            {
                _cameraTarget.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
            }
        }
        else
        {
            // First-person: apply pitch directly to first person camera
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
            }
        }
        
        mouseDelta = Vector2.Zero;
    }
    
    private void UpdateCameraPosition(double delta)
    {
        // No need to move cameras - they're already positioned correctly in the scene!
        // The SpringArm automatically handles third-person camera collision
    }
    
    private void ToggleCameraMode()
    {
        // Check if cameras are initialized
        if (_firstPersonCamera == null || _thirdPersonCamera == null)
        {
            GD.PrintErr("[Character] Cannot toggle camera - cameras not initialized!");
            return;
        }
        
        // Check which camera is currently active and switch
        if (_activeCamera == _thirdPersonCamera)
        {
            // Switch to first-person camera
            GD.Print("[Character] Switched to first-person camera");
            
            _firstPersonCamera.Current = true;
            _thirdPersonCamera.Current = false;
            _activeCamera = _firstPersonCamera;
            
            // Reset camera pitch
            _cameraPitch = 0.0f;
            _firstPersonCamera.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
            
            // Hide player mesh
            setMeshVisibility(false);
        }
        else
        {
            // Switch to third-person camera
            GD.Print("[Character] Switched to third-person camera");
            
            _thirdPersonCamera.Current = true;
            _firstPersonCamera.Current = false;
            _activeCamera = _thirdPersonCamera;
            
            // Initialize camera pitch for third person
            _cameraPitch = -10.0f;
            if (_cameraTarget != null)
            {
                _cameraTarget.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
            }
            
            // Show player mesh
            setMeshVisibility(true);
        }
    }

    private void setMeshVisibility(bool isVisible)
    {
        // Only hide/show the body mesh, not the entire armature
        // This keeps the skeleton and bone attachments (like tools) working
        if (_playerBodyMesh != null)
        {
            _playerBodyMesh.Visible = isVisible;
        }
        
        // Tool stays visible regardless (it's attached to the skeleton, not the mesh)
        // No need to explicitly set tool visibility - it's always visible
    }

    public GodotObject RaycastFromCamera(float range = 5.0f)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = _activeCamera.GlobalTransform.Origin;
        var to = from + _activeCamera.GlobalTransform.Basis.Z * -range;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0 && result.ContainsKey("collider"))
        {
            return result["collider"].AsGodotObject();
        }
        return null;
    }

    private void TryInteractE()
    {
        var collider = RaycastFromCamera(5.0f);
        if (collider == null)
            return;

        // Look for WorldItem in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is WorldItem worldItem && worldItem.CanInteract())
            {
                float distance = GlobalPosition.DistanceTo(worldItem.GlobalPosition);
                
                if (distance <= worldItem.InteractRange)
                {
                    worldItem.InteractE(this);
                }
                else
                {
                    GD.Print($"[Character] Too far away for E: {distance:F1}m");
                }
                return;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
    }

    private void TryInteractF()
    {
        var collider = RaycastFromCamera(5.0f);
        if (collider == null)
            return;

        // Look for WorldItem in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is WorldItem worldItem && worldItem.CanInteract())
            {
                float distance = GlobalPosition.DistanceTo(worldItem.GlobalPosition);
                
        
                worldItem.InteractF(this);
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
    }

    public void PickupPhysItem(PhysItem physItem)
    {
        if (_heldPhysItem != null)
        {
            GD.Print("[Character] Already holding an item!");
            return;
        }

        _heldPhysItem = physItem;
        _heldPhysItem.OnPickedUp();
        GD.Print($"[Character] Picked up: {physItem.Name}");
    }

    private void DropPhysItem()
    {
        if (_heldPhysItem == null) return;

        var itemToDrop = _heldPhysItem;
        _heldPhysItem = null;    
        itemToDrop.OnDropped();
        GD.Print($"[Character] Dropped: {itemToDrop.Name}");
    }

    private void ThrowPhysItem()
    {
        if (_heldPhysItem == null) return;

        var throwDirection = -_activeCamera.GlobalTransform.Basis.Z;
        var itemToThrow = _heldPhysItem;
        var throwForce = _heldPhysItem.ThrowForce;
        
        // Clear the reference FIRST so floaty physics stops applying
        _heldPhysItem = null;
        
        // Then throw the item
        itemToThrow.OnThrown(throwDirection, throwForce);
        GD.Print($"[Character] Threw: {itemToThrow.Name}");
    }

    /// <summary>
    /// Play a character animation. Tools can call this to trigger animations.
    /// </summary>
    public void PlayAnimation(string animationName, float blendTime = -1, bool isActionAnimation = true)
    {
        if (_animationPlayer == null)
        {
            GD.PushWarning($"[Character] Cannot play animation '{animationName}' - AnimationPlayer not found");
            return;
        }

        if (!_animationPlayer.HasAnimation(animationName))
        {
            GD.PushWarning($"[Character] Animation '{animationName}' not found");
            return;
        }

        // Use provided blend time, or default to AnimationBlendTime
        float actualBlendTime = blendTime >= 0 ? blendTime : AnimationBlendTime;
        _animationPlayer.Play(animationName, actualBlendTime);
        
        // Mark as action animation to prevent movement animations from interrupting
        if (isActionAnimation)
        {
            _isPlayingActionAnimation = true;
        }
        
        GD.Print($"[Character] Playing animation: {animationName}");
    }
    
    /// <summary>
    /// Called when any animation finishes
    /// </summary>
    private void OnAnimationFinished(StringName animName)
    {
        GD.Print($"[Character] Animation finished: {animName}");
        
        // Reset action animation flag
        _isPlayingActionAnimation = false;
        
        // Return to appropriate idle/movement animation
        // The next _PhysicsProcess will handle this via UpdateAnimations
    }
}
