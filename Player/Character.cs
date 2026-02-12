using System;
using System.Collections.Specialized;
using System.Dynamic;
using Godot;
using TriangleNet.Tools;
using TriangleNet.Voronoi.Legacy;

public partial class Character : CharacterBody3D
{
    [Signal]
    public delegate void InventoryRequestedEventHandler(int inventoryId);
    



    [Signal]
    public delegate void HintEUpdatedEventHandler(string hint);

    [Signal]
    public delegate void HintFUpdatedEventHandler(string hint);

    [Signal]
    public delegate void DialogMessageEventHandler(string message);

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

    public Node3D holdPosition;
    public WorldItem heldPhysItem;
    private Node3D _cameraTarget; // Third-person camera pivot
    private SpringArm3D _springArm;  // For third-person collision
    private MeshInstance3D _playerBodyMesh;  // Reference to player's body mesh (not the whole armature!)
    private AnimationPlayer _animationPlayer;  // Character animations
    private Node3D _toolPosition;  // Helper node for tool positioning
    private bool _isPlayingActionAnimation = false;  // Prevent movement animations from interrupting actions

    private Camera3D _activeCamera;
    private Camera3D _firstPersonCamera;
    private Camera3D _thirdPersonCamera;

    private InventoryManager _inventoryManager;
    private InputHandler _inputHandler;
    private ToolScript _currentTool;

    public CharAnimations animTree;

    public AudioStreamPlayer3D footstepsAudioPlayer;

    Vector3 _externalVelocity = Vector3.Zero;

    public string Username = "Player";

    public int _inventoryId = 0;

    [Export]
    public int inventoryId;
    // Make hotbar slot public for multiplayer synchronization
    // Export is on the backing field for proper Godot editor integration
    [Export]
    private int _hotbarSlot = 0;

    [Signal]
    public delegate void WorldItemPickedUpEventHandler();
    
    public int HotbarSlot 
    { 
        get => _hotbarSlot;
        set 
        {
            
            _hotbarSlot = value;
            
            var hotbarItems = _inventoryManager.GetInventory(inventoryId).HotbarItems;
            
            // Check if the slot has an item before accessing it
            if (hotbarItems.ContainsKey(_hotbarSlot) && hotbarItems[_hotbarSlot] != null)
            {
                ItemDefinition heldItem = hotbarItems[_hotbarSlot].ItemData;
                if (heldItem != null && heldItem.ToolScriptScene != null)
                {
                    Rpc(nameof(UpdateEquippedTool), heldItem.ToolScriptScene.ResourcePath);
                }
                else
                {
                    Rpc(nameof(UpdateEquippedTool), "");
                }
            }
            else
            {
                // Slot is empty, unequip tool
                Rpc(nameof(UpdateEquippedTool), "");
            }
            
        }
    }

    public void SetHeadVisibility(bool isVisible)
    {
        if (!isVisible)
        {
            // Create a new transparent material
            var invisibleMaterial = new StandardMaterial3D();
            invisibleMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            invisibleMaterial.AlbedoColor = new Color(1, 1, 1, 0);

            _playerBodyMesh.SetSurfaceOverrideMaterial(1, invisibleMaterial);
            _playerBodyMesh.SetSurfaceOverrideMaterial(2, invisibleMaterial);
        } 
        else
        {   
            _playerBodyMesh.SetSurfaceOverrideMaterial(1, null);
            _playerBodyMesh.SetSurfaceOverrideMaterial(2, null);
        }
    }
    

    

    public override void _Ready()
    {

        GetNode<NetworkManager>("/root/NetworkManager").IdToPlayer[GetMultiplayerAuthority()] = this;

        var nametag = GetNode<Label3D>("Nametag");
        nametag.Text = Name;

        
        // Initialize animation tree for ALL characters (local and remote)
        animTree = GetNode<CharAnimations>("AnimationTree");
        footstepsAudioPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");
        
        // Get mesh for visibility control (ALL characters need this)
        _playerBodyMesh = GetNode<MeshInstance3D>("CharacterArmature/Skeleton3D/Body");
        _playerBodyMesh.Visible = true;
        
        
        // Get tool position helper (optional, but recommended)
        _toolPosition = GetNode<Node3D>("CharacterArmature/Skeleton3D/RightHandAttachment/ToolPosition");

        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");

        inventoryId = GetMultiplayerAuthority();

        holdPosition = GetNode<Node3D>("Camera3D/HoldPosition");


        if (Multiplayer.IsServer())
        {
            Log("Server creating inventory for player " + Name + " with inventoryId " + inventoryId);
            _inventoryManager.CreateInventory(new Vector2I(6, 4), inventoryId);
        }



        if (IsMultiplayerAuthority())
        {
            SetupCameraSystem();
            
            _activeCamera.Current = true;
            animTree.ReelTarget = 0;
            SetHeadVisibility(false);
            
            GetNode<Gui>("/root/Main/GUI").init(this);

            
            // Connect to InputHandler signals for local authority player
            _inputHandler = GetNode<InputHandler>("/root/InputHandler");
            ConnectInputSignals();

            
        }


        
        // Create hold position for physics items (attached to active camera)
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ApplyCharacterImpulse(Vector3 forceVelocity)
    {
        
        if (!IsMultiplayerAuthority())
        {
            long peerid = GetMultiplayerAuthority();
            RpcId(peerid, nameof(ApplyCharacterImpulse), forceVelocity);
            return;
        }

        // Apply vertical force immediately to main Velocity so gravity takes over
        Velocity = new Vector3(Velocity.X, Velocity.Y + forceVelocity.Y, Velocity.Z);

        // Store only horizontal force in external velocity for friction decay
        _externalVelocity += new Vector3(forceVelocity.X, 0, forceVelocity.Z);

    }

    
    private void SetupCameraSystem()
    {
        // Get camera target from scene (created in editor)
        _cameraTarget = GetNodeOrNull<Node3D>("CameraTarget");
        if (_cameraTarget == null)
        {
            Error("ERROR: CameraTarget node not found! Create a Node3D called 'CameraTarget' as a child of Character.");
        }
        
        // Get spring arm
        _springArm = GetNodeOrNull<SpringArm3D>("CameraTarget/SpringArm3D");
        if (_springArm == null)
        {
            Error("ERROR: SpringArm3D not found! Create a SpringArm3D as a child of CameraTarget.");
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
            _activeCamera.SetCurrent(true);
            _thirdPersonCamera.Current = false;
        }
        else
        {
            Error("ERROR: Camera system failed to initialize - missing camera nodes!");
        }
        
        // Initialize camera angles
        _cameraPitch = 0.0f;
        
    }

    private void ConnectInputSignals()
    {
        Log("Connecting to InputHandler signals");
        
        // Mouse and camera
        _inputHandler.MouseMotion += OnMouseMotion;
        _inputHandler.MouseClick += OnMouseClick;
        _inputHandler.CameraToggled += OnCameraToggled;
        
        // Hotbar
        _inputHandler.NumkeyPressed += OnHotbarSlotSelected;
        _inputHandler.Scroll += OnHotbarScroll;
        
        // Interactions
        _inputHandler.InteractEPressed += OnInteractEPressed;
        _inputHandler.InteractFPressed += OnInteractFPressed;
        
        // UI
        _inputHandler.InventoryToggled += OnInventoryToggled;
    }

    // Input signal handlers
    private void OnMouseMotion(Vector2 relative)
    {
        mouseDelta = relative;
    }

    private void OnMouseClick(MouseButton button, bool isPressed)
    {
        if (!isPressed) return;
        
        if (button == MouseButton.Left)
        {
            if (heldPhysItem != null)
            {
                // Throw the held item
                Vector3 throwDirection = -_activeCamera.GlobalTransform.Basis.Z.Normalized();
                heldPhysItem.Throw(throwDirection);
            }
            else if (_currentTool != null)
            {
                _currentTool.PrimaryFire(this);
            }
        }
        else if (button == MouseButton.Right)
        {
            if (heldPhysItem != null)
            {
                heldPhysItem.Throw(Vector3.Zero);
            }
            else if (_currentTool != null)
            {
                _currentTool.SecondaryFire(this);
            }
        }
    }


    /// <summary>
    /// Updates the equipped tool based on the current hotbar slot.
    /// Works for both local authority and remote players.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void UpdateEquippedTool(string toolScenePath)
    {
        
        // For remote players, we only show the visual mesh, not the full tool script
        Log($"{Name} Updating equipped tool. Rpc call? " + (Multiplayer.GetRemoteSenderId() != 0));
        
        if (IsMultiplayerAuthority())
        {
            var hotbarItems = _inventoryManager.GetInventory(inventoryId).HotbarItems;
            
            // Check if HotbarSlot has an item
            if (!hotbarItems.ContainsKey(HotbarSlot))
            {
                // No item in this slot, remove tool if any
                if (_currentTool != null)
                {
                    _currentTool.QueueFree();
                    _currentTool = null;
                }
                Log($"No item in hotbar slot {HotbarSlot}");
                return;
            }

            ItemInstance heldItem = hotbarItems[HotbarSlot];
            Log($"Equipping item: {heldItem.ItemData.Name}");
            
            // Remove old tool
            if (_currentTool != null)
            {
                if (_currentTool.itemInstance.Id != heldItem.Id)
                {
                    _currentTool.QueueFree();
                    _currentTool = null;
                }
                 else
                {
                    Log($"Tool in slot {HotbarSlot} is already equipped, no need to update");
                    return;
                }
            }
                
            // Load and instantiate the tool scene
            if (!string.IsNullOrEmpty(toolScenePath))
            {
                PackedScene toolScene = GD.Load<PackedScene>(toolScenePath);
                _currentTool = toolScene.Instantiate<ToolScript>();
                _currentTool.SetMultiplayerAuthority(GetMultiplayerAuthority());
                _currentTool.itemInstance = heldItem;
                _currentTool.holdingCharacter = this;
                _toolPosition.AddChild(_currentTool);
                Log("Authority player equipped tool with full functionality");
            }
        }
        else // Remote players only
        {
            if (string.IsNullOrEmpty(toolScenePath))
            {
                if (_currentTool != null)
                {
                    _currentTool.QueueFree();
                    _currentTool = null;
                }
            }
            else
            {
                PackedScene toolScene = GD.Load<PackedScene>(toolScenePath);
                _currentTool = toolScene.Instantiate<ToolScript>();
                _toolPosition.AddChild(_currentTool);
                Log("Remote player equipped tool with visual mesh only");
                _currentTool.holdingCharacter = this;
            }
        }
    }

    private void OnHotbarSlotSelected(int slotIndex)
    {
        if (_inputHandler.CurrentContext == InputHandler.InputContext.Gameplay)
        {
            HotbarSlot = slotIndex;
        }
    }

    private void OnHotbarScroll(int direction)
    {
        if (direction > 0)
        {
            HotbarSlot = (HotbarSlot + 1) % 6;
        }
        else
        {
            HotbarSlot = (HotbarSlot - 1 + 6) % 6;
        }
        Log($"Hotbar scrolled to slot {HotbarSlot}");
    }

    private void OnCameraToggled()
    {
        ToggleCameraMode();
    }

    private void OnInteractEPressed()
    {
        TryInteractE();
    }

    private void OnInteractFPressed()
    {
        TryInteractF();
    }

    private void OnInventoryToggled()
    {
        OpenInventory(inventoryId);
    }


    public void OpenInventory(int id)
    {
        Log($"{Name} - Opening inventory with Id " + id);
        EmitSignal(SignalName.InventoryRequested, id);
    }

    public override void _Process(double delta)
    {
    }

    public override void _PhysicsProcess(double delta)
    {

        if (!IsMultiplayerAuthority())
            return;

        // Get movement input from InputHandler
        Vector2 inputDir = _inputHandler.GetMovementInput();
        Vector3 direction = Vector3.Zero;

        // Convert 2D input to 3D direction relative to character rotation
        direction = Transform.Basis.Z * inputDir.Y + Transform.Basis.X * inputDir.X;
        direction = direction.Normalized();

        // Get sprint state from InputHandler
        _isSprinting = _inputHandler.IsSprintPressed();

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

        Vector3 targetVelocity = new Vector3(
            direction.X * currentSpeed,
            Velocity.Y, // preserve vertical component for gravity/jump
            direction.Z * currentSpeed
        );

        // Apply External Velocity decay
        float friction = isOnFloor ? 10.0f : 2.0f;
        _externalVelocity = _externalVelocity.Lerp(Vector3.Zero, friction * (float)delta);
        if (_externalVelocity.LengthSquared() < 0.1f) _externalVelocity = Vector3.Zero;


        Velocity = new Vector3(
            targetVelocity.X + _externalVelocity.X,
            Velocity.Y, // Use existing Y velocity (which includes gravity and jump)
            targetVelocity.Z + _externalVelocity.Z
        );


        // Apply gravity
        if (!isOnFloor)
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);

        // Handle jump using InputHandler
        if (isOnFloor && _inputHandler.IsJumpJustPressed())
        {
            EmitSignal(SignalName.DialogMessage, "Jump!");
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
            Error("Cannot toggle camera - cameras not initialized!");
            return;
        }
        
        // Check which camera is currently active and switch
        if (_activeCamera == _thirdPersonCamera)
        {
            // Switch to first-person camera
            Log("Switched to first-person camera");
            
            _firstPersonCamera.Current = true;
            _thirdPersonCamera.Current = false;
            _activeCamera = _firstPersonCamera;
            
            // Reset camera pitch
            _cameraPitch = 0.0f;
            _firstPersonCamera.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
            SetHeadVisibility(false);

        }
        else
        {
            // Switch to third-person camera
            Log("Switched to third-person camera");
            
            _thirdPersonCamera.Current = true;
            _firstPersonCamera.Current = false;
            _activeCamera = _thirdPersonCamera;
            
            // Initialize camera pitch for third person
            _cameraPitch = -10.0f;
            if (_cameraTarget != null)
            {
                _cameraTarget.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
            }
            SetHeadVisibility(true);
        }
    }

    /// <summary>
    /// Switch to using an external camera (e.g., vehicle camera)
    /// </summary>
    public void SetExternalCamera(Camera3D externalCamera)
    {
        if (externalCamera == null)
        {
            Error("Cannot set null external camera!");
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

        Log("Switched to external camera");
    }

    /// <summary>
    /// Restore the character's own camera (first person by default)
    /// </summary>
    public void RestoreCharacterCamera()
    {
        if (_firstPersonCamera == null)
        {
            Error("Cannot restore camera - first person camera not initialized!");
            return;
        }

        _firstPersonCamera.Current = true;
        if (_thirdPersonCamera != null)
            _thirdPersonCamera.Current = false;
        
        _activeCamera = _firstPersonCamera;
        
        // Reset camera pitch
        _cameraPitch = 0.0f;
        _firstPersonCamera.RotationDegrees = new Vector3(_cameraPitch, 0, 0);

        Log("Restored character camera");
    }

    public GodotObject RaycastFromCamera(float range = 5.0f)
    {
        if (_activeCamera == null)
        {
            Error("Cannot raycast - active camera not initialized!");
            return null;
        }
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

    public Godot.Collections.Dictionary RaycastRaw(float range = 5.0f)
    {
                if (_activeCamera == null)
        {
            Error("Cannot raycast - active camera not initialized!");
            return null;
        }
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = _activeCamera.GlobalTransform.Origin;
        var to = from + _activeCamera.GlobalTransform.Basis.Z * -range;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);
        return result;
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
                        Log($"Too far away for E: {distance:F1}m");
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
                        Log($"Too far away for F: {distance:F1}m");
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


        // Look for IInteractable in the parent chain (includes WorldItem and NPCs)
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            
            if (nodeToCheck is IInteractable interactable && interactable.CanInteract())
            {
                float distance = GlobalPosition.DistanceTo((nodeToCheck as Node3D)?.GlobalPosition ?? Vector3.Zero);
                
                // Check if within interaction range
                if (distance <= interactable.InteractRange)
                {
                    // Update hints with the item's hint text
                    EmitSignal(SignalName.HintEUpdated, interactable.HintE);
                    EmitSignal(SignalName.HintFUpdated, interactable.HintF);
                }
                else
                {                    // Too far away - clear hints
                    EmitSignal(SignalName.HintEUpdated, "");
                    EmitSignal(SignalName.HintFUpdated, "");
                }
                return;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
        
        // No interactable found - clear hints
        EmitSignal(SignalName.HintEUpdated, "");
        EmitSignal(SignalName.HintFUpdated, "");
    }

    public void toolAnimationFire()
    {
        EmitSignal(SignalName.ToolAnimation);
    }


}
