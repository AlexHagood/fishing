using System.Dynamic;
using Godot;
using TriangleNet.Tools;

public partial class Character : CharacterBody3D
{
    [Signal]
    public delegate void InventoryRequestedEventHandler(int inventoryId);
    
    [Signal]
    public delegate void RotateRequestedEventHandler();

    [Signal]
    public delegate void HotbarSlotSelectedEventHandler(int slotIndex);

    [Signal]
    public delegate void HintEUpdatedEventHandler(string hint);

    [Signal]
    public delegate void HintFUpdatedEventHandler(string hint);

    [Signal]
    public delegate void ToolAnimationEventHandler();

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

    public CharAnimations animTree;

    public AudioStreamPlayer3D footstepsAudioPlayer;

    public string Username = "Player";

    private int _playerId;
    public int PlayerId
    {
        get => _playerId;
        set
        {
            _playerId = value;
            // Authority is set by NetworkManager after node is added to tree
        }
    }



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
        // Configure MultiplayerSynchronizer to only sync from authority
        var synchronizer = GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer");
        if (synchronizer != null)
        {
            // Set the synchronizer to use this node's authority
            synchronizer.SetMultiplayerAuthority(GetMultiplayerAuthority());
            GD.Print($"[Character {Name}] MultiplayerSynchronizer authority set to {synchronizer.GetMultiplayerAuthority()}");
        }
        
        // Initialize animation tree for ALL characters (local and remote)
        animTree = GetNode<CharAnimations>("AnimationTree");
        footstepsAudioPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");
        
        // Get mesh for visibility control (ALL characters need this)
        _playerBodyMesh = GetNodeOrNull<MeshInstance3D>("CharacterArmature/Skeleton3D/Body");
        
        // Early return for non-authority characters (remote players on your screen)
        GD.Print($"[Character {Multiplayer.GetUniqueId()}] _Ready called for character {Name} with authority {IsMultiplayerAuthority()}");
        if (!IsMultiplayerAuthority())
        {
            GD.Print($"[Character {Multiplayer.GetUniqueId()}] {Name} is remote player, skipping initialization");
            // Make sure remote characters are visible
            if (_playerBodyMesh != null)
            {
                _playerBodyMesh.Visible = true;
                GD.Print($"[Character] Remote player mesh set to visible");
            }
            return;
        }
        
        GD.Print($"[Character {Multiplayer.GetUniqueId()}] {Name} is local authority player, initializing...");
        
        // Only the local authority character controls mouse mode
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Setup camera system
        SetupCameraSystem();
        
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

        if (IsMultiplayerAuthority())
        {
            _holdPosition = new Node3D();
            _holdPosition.Name = "HoldPosition";
            _activeCamera.AddChild(_holdPosition);
            _holdPosition.Position = new Vector3(0, -0.5f, -2.0f);

        }
        
        // Create hold position for physics items (attached to active camera)

        
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");

        inventoryId = _inventoryManager.CreateInventory(new Vector2I(5, 5));

        

    }
    
    private void SetupCameraSystem()
    {
        GD.Print($"[Character] Setting up camera system...");
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

        GD.Print($"{_firstPersonCamera}");
        
        // Initialize camera angles
        _cameraPitch = 0.0f;
        
        // Hide local player's body in first person
        if (_playerBodyMesh != null)
        {
            setMeshVisibility(false); // Hide body in first person by default (tool stays visible!)
            GD.Print("[Character] Local player body hidden for first person view");
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
        if (!IsMultiplayerAuthority())
            return;
        
        // Debug: Log first input event to verify input is working
        if (Engine.GetFramesDrawn() % 300 == 0 && @event is InputEventKey)
        {
            GD.Print($"[Character {Name}] Received input event. MouseMode: {Input.MouseMode}");
        }

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
                }
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key6)
            {
                GD.Print("Char - Hotbar key pressed: " + keyEvent.Keycode);
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


        if (Engine.GetPhysicsFrames() % 60 == 0)
        {
            GD.Print($"[Character {Name}] Physics processing - Peer ID: {Multiplayer.GetUniqueId()}, Authority: {GetMultiplayerAuthority()}, {IsMultiplayerAuthority()}");
        }

        if (!IsMultiplayerAuthority())
            return;


        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("fwd"))
        {
            direction -= Transform.Basis.Z;
            if (Engine.GetPhysicsFrames() % 60 == 0)
                GD.Print($"[Character {Name}] FWD pressed!");
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

        // Update animation blend based on movement state
        if (animTree != null)
        {
            if (direction.Length() > 0.1f) // Check if player is actually moving
            {
                animTree.WalkTarget = _isSprinting ? 1.0f : 0.5f;
            }
            else
            {
                animTree.WalkTarget = -1.0f; // Idle
            }
        }

        Velocity = new Vector3(
            direction.X * currentSpeed,
            Velocity.Y, // preserve vertical component for gravity/jump
            direction.Z * currentSpeed
        );

        if (Engine.GetPhysicsFrames() % 60 == 0 && direction.Length() > 0)
        {
            GD.Print($"[Character {Name}] Direction: {direction}, Sprint: {_isSprinting}, Velocity: {Velocity} ");
        }



        // Apply gravity
        if (!isOnFloor)
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);

        // Handle jump
        if (isOnFloor && Input.IsActionJustPressed("jump"))
        {
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
            if (animTree != null)
            {
                animTree.Jump();
            }
        }
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
        
        
        // Update interaction hints based on what player is looking at
        UpdateInteractionHints();
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

    /// <summary>
    /// Switch to using an external camera (e.g., vehicle camera)
    /// </summary>
    public void SetExternalCamera(Camera3D externalCamera)
    {
        if (externalCamera == null)
        {
            GD.PrintErr("[Character] Cannot set null external camera!");
            return;
        }

        // Deactivate character cameras
        if (_firstPersonCamera != null)
            _firstPersonCamera.Current = false;
        if (_thirdPersonCamera != null)
            _thirdPersonCamera.Current = false;

        // Activate the external camera
        externalCamera.Current = true;
        _activeCamera = externalCamera;

        GD.Print("[Character] Switched to external camera");
    }

    /// <summary>
    /// Restore the character's own camera (first person by default)
    /// </summary>
    public void RestoreCharacterCamera()
    {
        if (_firstPersonCamera == null)
        {
            GD.PrintErr("[Character] Cannot restore camera - first person camera not initialized!");
            return;
        }

        _firstPersonCamera.Current = true;
        if (_thirdPersonCamera != null)
            _thirdPersonCamera.Current = false;
        
        _activeCamera = _firstPersonCamera;
        
        // Reset camera pitch
        _cameraPitch = 0.0f;
        _firstPersonCamera.RotationDegrees = new Vector3(_cameraPitch, 0, 0);

        GD.Print("[Character] Restored character camera");
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

        // Look for IInteractable in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is IInteractable interactable && interactable.CanInteract())
            {
                // Cast to Node3D to get GlobalPosition for distance check
                if (nodeToCheck is Node3D node3D)
                {
                    float distance = GlobalPosition.DistanceTo(node3D.GlobalPosition);
                    
                    if (distance <= interactable.InteractRange)
                    {
                        interactable.InteractE(this);
                    }
                    else
                    {
                        GD.Print($"[Character] Too far away for E: {distance:F1}m");
                    }
                }
                else
                {
                    // If not Node3D, just interact without distance check
                    interactable.InteractE(this);
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

        // Look for IInteractable in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is IInteractable interactable && interactable.CanInteract())
            {
                // Cast to Node3D to get GlobalPosition for distance check
                if (nodeToCheck is Node3D node3D)
                {
                    float distance = GlobalPosition.DistanceTo(node3D.GlobalPosition);
                    
                    if (distance <= interactable.InteractRange)
                    {
                        interactable.InteractF(this);
                    }
                    else
                    {
                        GD.Print($"[Character] Too far away for F: {distance:F1}m");
                    }
                }
                else
                {
                    // If not Node3D, just interact without distance check
                    interactable.InteractF(this);
                }
                return;
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
    /// Update interaction hint text based on what the player is currently looking at
    /// </summary>
    private void UpdateInteractionHints()
    {
        // Only update hints when not in menu mode
        if (Input.MouseMode == Input.MouseModeEnum.Visible)
        {
            // Clear hints when in menu
            EmitSignal(SignalName.HintEUpdated, "");
            EmitSignal(SignalName.HintFUpdated, "");
            return;
        }
        
        var collider = RaycastFromCamera(5.0f);
        
        if (collider == null)
        {
            // Not looking at anything - clear hints
            EmitSignal(SignalName.HintEUpdated, "");
            EmitSignal(SignalName.HintFUpdated, "");
            return;
        }

        // Look for WorldItem in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is WorldItem worldItem && worldItem.CanInteract())
            {
                float distance = GlobalPosition.DistanceTo(worldItem.GlobalPosition);
                
                // Check if within interaction range
                if (distance <= worldItem.InteractRange)
                {
                    // Update hints with the item's hint text
                    EmitSignal(SignalName.HintEUpdated, worldItem.HintE);
                    EmitSignal(SignalName.HintFUpdated, worldItem.HintF);
                }
                else
                {
                    // Too far away - clear hints
                    EmitSignal(SignalName.HintEUpdated, "");
                    EmitSignal(SignalName.HintFUpdated, "");
                }
                return;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
        
        // No WorldItem found - clear hints
        EmitSignal(SignalName.HintEUpdated, "");
        EmitSignal(SignalName.HintFUpdated, "");
    }

    public void toolAnimationFire()
    {
        EmitSignal(SignalName.ToolAnimation);
    }


}
