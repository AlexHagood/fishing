using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class Character : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 8.0f;
    [Export] public float Gravity = 20.0f;
    [Export] public float MouseSensitivity = 0.01f;

    private bool isOnFloor = false;
    private Vector2 mouseDelta = Vector2.Zero;

    // Pickup system - GameItem implements IPickupable and is a Node3D
    private GameItem _heldItem;
    private Node3D _holdPosition;

    private float _baseSpeed = 5.0f;
    private float _sprintSpeed = 10.0f;
    private float _baseFov = 70.0f;
    private float _sprintFov = 80.0f;
    private bool _isSprinting = false;

    private Camera3D camera;
    private Camera3D cameraBack;
    private bool isThirdPerson = false;

    private Gui _gui;

    private Inventory _inventory;

    private bool holdingPickup = false;

    private int _currentHotbarSlot = 0; // 0 = no item, 1-6 = slots

    private InvItem[] _hotbarItems = new InvItem[7]; // 1-6, 0 unused

    // Walking sound effects
    private AudioStreamPlayer _walkSoundPlayer;
    private AudioStream _step1Sound;
    private AudioStream _step2Sound;
    private bool _wasMoving = false;
    private float _stepTimer = 0f;
    private float _baseStepInterval = 0.4f; // Base time between footsteps when walking
    private float _sprintStepInterval = 0.3f; // Faster footsteps when sprinting
    private bool _nextStepIsStep1 = true;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Setup walking sounds
        _walkSoundPlayer = new AudioStreamPlayer();
        AddChild(_walkSoundPlayer);
        _step1Sound = GD.Load<AudioStream>("Sounds/Step1.ogg");
        _step2Sound = GD.Load<AudioStream>("Sounds/Step2.ogg");
        var terrain = GetTree().Root.FindChild("Terrain", true, false);

        _gui = GetParent().GetNode<Gui>("GUI");

        // Create hold position for picked up items
        _holdPosition = new Node3D();
        _holdPosition.Name = "HoldPosition";
        camera = GetNode<Camera3D>("Camera3D");
        cameraBack = GetNode<Camera3D>("CameraBack");
        camera.AddChild(_holdPosition);
        // Position it in front of the camera
        _holdPosition.Position = new Vector3(0, -0.5f, -2.0f);
        
        // Start with first person camera active
        camera.Current = true;
        cameraBack.Current = false;

        _inventory = _gui.GetNode<Inventory>("Inventory");

        UpdateHotbarHighlight(); // Initialize hotbar highlight
        UpdateAllHotbarSlots();
    }

    private void UpdateHotbarHighlight()
    {
        var hotbar = _gui.GetNodeOrNull("Hotbar");
        if (hotbar == null)
        {
            GD.PrintErr("[Hotbar] Hotbar node not found in GUI!");
            return;
        }
        for (int i = 1; i <= 6; i++)
        {
            var panel = hotbar.GetNodeOrNull<Control>($"Slot{i}");
            if (panel == null)
            {
                GD.PrintErr($"[Hotbar] Slot{i} not found in Hotbar!");
                continue;
            }
            var style = new StyleBoxFlat();
            if (i == _currentHotbarSlot)
            {
                style.BorderColor = new Color(1, 1, 0); // Yellow border
                style.BorderWidthTop = 3;
                style.BorderWidthBottom = 3;
                style.BorderWidthLeft = 3;
                style.BorderWidthRight = 3;
                style.BgColor = new Color(0.2f, 0.2f, 0.0f, 0.2f); // Slight yellow background
            }
            else
            {
                style.BorderColor = new Color(0, 0, 0, 0); // No border
                style.BorderWidthTop = 0;
                style.BorderWidthBottom = 0;
                style.BorderWidthLeft = 0;
                style.BorderWidthRight = 0;
                style.BgColor = new Color(0.1f, 1f, 0.3f, 0.3f); // Transparent green
            }
            panel.AddThemeStyleboxOverride("panel", style);
        }
    }

    private void UpdateHotbarSlot(int slot)
    {
        var hotbar = _gui.GetNodeOrNull("Hotbar");
        if (hotbar == null || slot < 1 || slot > 6) return;
        var panel = hotbar.GetNodeOrNull<Control>($"Slot{slot}");
        if (panel == null) return;
        // Remove previous icon if any
        foreach (Node child in panel.GetChildren())
        {
            if (child is TextureRect)
                panel.RemoveChild(child);
        }
        var item = _hotbarItems[slot];
        if (item != null && item.itemIcon != null && item.itemIcon.Texture != null)
        {
            var icon = new TextureRect();
            icon.Texture = item.itemIcon.Texture;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            icon.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            icon.AnchorRight = 1;
            icon.AnchorBottom = 1;
            icon.OffsetLeft = 4;
            icon.OffsetTop = 4;
            icon.OffsetRight = -4;
            icon.OffsetBottom = -4;
            panel.AddChild(icon);
        }
    }

    private void UpdateAllHotbarSlots()
    {
        for (int i = 1; i <= 6; i++)
            UpdateHotbarSlot(i);
    }

    private void BindItemToHotbarSlot(InvItem item, int slot)
    {
        // Unbind from previous slot if needed
        for (int i = 1; i <= 6; i++)
        {
            if (_hotbarItems[i] == item)
                _hotbarItems[i] = null;
        }
        _hotbarItems[slot] = item;
        UpdateHotbarSlot(slot);
    }

    public override void _Process(double delta)
    {
        // Floaty physics hold for non-tool items only
        if (_heldItem != null && _holdPosition != null)
        {
            bool isTool = _heldItem.ItemDef?.IsTool ?? false;
            
            if (!isTool) // Only apply floaty physics to non-tools
            {
                // Unfreeze and disable gravity while held
                _heldItem.Freeze = false;
                _heldItem.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                _heldItem.GravityScale = 0;
                
                // Calculate force toward hold position
                Vector3 target = _holdPosition.GlobalPosition;
                Vector3 toTarget = target - _heldItem.GlobalPosition;
                Vector3 velocity = _heldItem.LinearVelocity;
                float followStrength = 8.0f;
                float damp = 4.0f;
                Vector3 desiredVelocity = toTarget * followStrength;
                Vector3 force = (desiredVelocity - velocity) * damp;
                _heldItem.ApplyCentralForce(force);
                
                // Angular velocity damping
                float angularDamp = 6.0f;
                _heldItem.AngularVelocity *= Mathf.Exp(-angularDamp * (float)delta);
            }
        }

        // Handle F key for pickup with progress bar (inventory pickup)
        if (Input.IsActionPressed("pickup"))
        {
            var progressBar = _gui.progressBar;
            var rayHit = PlayerObjectRay(5.0f);
            
            // Find IPickupable in the parent chain
            IPickupable pickupableItem = null;
            Node nodeToCheck = rayHit as Node;
            while (nodeToCheck != null && pickupableItem == null)
            {
                if (nodeToCheck is IPickupable p && p.CanBePickedUp())
                {
                    pickupableItem = p;
                    break;
                }
                nodeToCheck = nodeToCheck.GetParent();
            }
            
            if (pickupableItem == null)
            {
                var anim = (AnimationPlayer)_gui.progressBar.GetNode("AnimationPlayer");
                if (!anim.IsPlaying() || anim.CurrentAnimation != "BadWiggle")
                {
                    _gui.progressBar.Visible = false;
                    _gui.progressBar.Value = 0;
                }
            }
            else
            {
                progressBar.Visible = true;
                progressBar.Value += (float)GetProcessDeltaTime() * (progressBar.MaxValue / 0.5f);
                if (progressBar.Value >= progressBar.MaxValue)
                {
                    progressBar.Value = 0;
                    progressBar.Visible = false;
                    
                    // Add to inventory
                    GD.Print($"Item grabbed: {pickupableItem.ItemName}");
                    
                    // Must be GameItem to call InventoryPickup (signal emit)
                    if (pickupableItem is GameItem gameItem)
                    {
                        gameItem.InventoryPickup();
                        
                        if (gameItem.ItemDef == null)
                        {
                            GD.PrintErr($"GameItem {gameItem.Name} has no ItemDefinition!");
                            return;
                        }
                        
                        InvItem invitem = new InvItem(gameItem.ItemDef);
                        invitem.gameItem = gameItem;
                        
                        if (_inventory.ForceFitItem(invitem))
                        {
                            _inventory.GetNode("InventoryPanel").AddChild(invitem);
                            invitem.Visible = true;
                            pickupableItem.DisablePhys();
                        }
                        else 
                        { 
                            wiggleBar(); 
                        }
                    }
                }
            }
        }
        else
        {
            var anim = (AnimationPlayer)_gui.progressBar.GetNode("AnimationPlayer");
            if (!anim.IsPlaying() || anim.CurrentAnimation != "BadWiggle")
            {
                _gui.progressBar.Visible = false;
                _gui.progressBar.Value = 0;
            }
        }
    }

    public void wiggleBar()
    {
        AnimationPlayer anim = (AnimationPlayer)_gui.progressBar.GetNode("AnimationPlayer");
        if (anim.GetParent() is Control parentControl)
            parentControl.Visible = true;

        // Force progressBar visible while anim is playing
        _gui.progressBar.Visible = true;
        anim.Play("BadWiggle");
        // Optionally hide after animation
        anim.AnimationFinished += (Godot.StringName animName) => {
            if (animName == "BadWiggle")
                _gui.progressBar.Visible = false;
        };
    }

    private void SelectHotbarSlot(int slot)
    {
        if (slot < 1 || slot > 6) return;

        // Unequip current item if any
        if (_heldItem != null)
        {
            bool isTool = _heldItem.ItemDef?.IsTool ?? false;
            
            if (isTool)
            {
                if (_heldItem.GetParent() == _holdPosition)
                {
                    _holdPosition.RemoveChild(_heldItem);
                    GetParent().AddChild(_heldItem);
                }
                (_heldItem as ToolItem)?.OnUnequip();
            }
            
            _heldItem.DisablePhys();
            _heldItem.Visible = false;
            _heldItem = null;
        }

        _currentHotbarSlot = slot;
        UpdateHotbarHighlight();

        var invItem = _hotbarItems[_currentHotbarSlot];
        if (invItem != null && invItem.gameItem != null)
        {
            var item = invItem.gameItem as GameItem;
            if (item == null)
            {
                GD.PrintErr("InvItem.gameItem is not a GameItem!");
                return;
            }
            
            // Make sure item is visible
            item.Visible = true;
            _heldItem = item;
            
            // Ensure the invItem link is set
            item.invItem = invItem;
            
            bool isTool = item.ItemDef?.IsTool ?? false;
            
            // Handle tool items with static hold
            if (isTool)
            {
                 var toolItem = item as ToolItem;
                 GD.Print($"[DEBUG] Equip ToolItem: {item.Name} Type: {item.GetType()}");
                 
                 // Static hold - reparent to hold position
                 if (item.GetParent() != null)
                     item.GetParent().RemoveChild(item);
                 
                 // Verify hold position
                 if (_holdPosition == null)
                 {
                     camera = GetNode<Camera3D>("Camera3D");
                     _holdPosition = camera.GetNodeOrNull<Node3D>("HoldPosition");
                 }

                 _holdPosition.AddChild(item);
                 item.Transform = Transform3D.Identity;
                 item.RotationDegrees = toolItem.HoldRotation;
                 item.Position = toolItem.HoldPosition;
                 item.Scale = toolItem.HoldScale;
                 
                 // Disable physics completely
                 item.Freeze = true;
                 item.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                 item.CollisionLayer = 0;
                 item.CollisionMask = 0;
                 
                 toolItem?.OnEquip();
                 GD.Print($"Equipped tool from hotbar: {item.ItemName}");
            }
            // Handle regular non-tool items with floaty physics
            else
            {
                 GD.Print($"[DEBUG] Equip GameItem: {item.Name} Type: {item.GetType()}");
                 
                 item.Freeze = false;
                 item.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
                 item.GravityScale = 0;
                 // Teleport in front of camera
                 var cam = GetNode<Camera3D>("Camera3D");
                 var frontPos = cam.GlobalTransform.Origin + cam.GlobalTransform.Basis.Z * -2.0f + new Vector3(0, -0.5f, 0);
                 item.GlobalPosition = frontPos;
                 item.OnPickedUp();
                 GD.Print($"Equipped item from hotbar: {item.ItemName}");
            }
        }
        else
        {
            GD.Print("No item in this hotbar slot.");
        }
    }


    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion)
        {
            mouseDelta = mouseMotion.Relative;
        }

        // Handle mouse clicks for node interaction and tool actions
        if (@event is InputEventMouseButton mouseButton)
        {
            // Handle left mouse button (LMB)
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    if (_heldItem != null)
                    {
                        bool isTool = _heldItem.ItemDef?.IsTool ?? false;
                        
                        if (isTool)
                            (_heldItem as ToolItem)?.OnPrimaryFire();
                        else
                            ThrowHeldItem();
                    }
                }
            }
            // Handle right mouse button (RMB)
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                if (mouseButton.Pressed)
                {
                    if (_heldItem != null)
                    {
                        bool isTool = _heldItem.ItemDef?.IsTool ?? false;
                        
                        if (isTool)
                            (_heldItem as ToolItem)?.OnSecondaryFire();
                        else
                            DropHeldItem();
                    }
                }
            }
            // Hotbar scroll handler
            else if (@event is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                int newSlot = _currentHotbarSlot;
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    newSlot++;
                    if (newSlot > 6) newSlot = 1;
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    newSlot--;
                    if (newSlot < 1) newSlot = 6;
                }
                SelectHotbarSlot(newSlot);
            }
        }
        // Handle E key for pickup (non-tool items only - tools require F key)
        if (Input.IsActionJustPressed("interact"))
        {
            GD.Print("[DEBUG] E key (interact) pressed");
            if (_heldItem == null)
            {
                TryGrabNonToolItem();
            }
        }
        // Allow releasing the mouse with Escape
        if (@event is InputEventKey keyEvent2 && keyEvent2.Pressed && keyEvent2.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
        
        // Camera toggle input
        if (Input.IsActionJustPressed("camera"))
        {
            ToggleCamera();
        }
        
        // Sprint input handling
        if (Input.IsActionPressed("sprint"))
        {
            _isSprinting = true;
        }
        else
        {
            _isSprinting = false;
        }

        if (Input.IsActionJustPressed("inventory"))
        {
            _gui.ToggleInventoryOpen();
        }

        // Handle Q key to drop tool items
        if (Input.IsActionJustPressed("drop"))
        {
            if (_heldItem != null)
            {
                bool isTool = _heldItem.ItemDef?.IsTool ?? false;
                if (isTool)
                {
                    DropToolItem();
                }
            }
        }

        // Handle number key binds for hovered InvItem
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            int numberPressed = -1;
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key9)
                numberPressed = (int)(keyEvent.Keycode - Key.Key0);
            else if (keyEvent.Keycode == Key.Key0)
                numberPressed = 0;
            if (numberPressed >= 1 && numberPressed <= 6)
            {
                if (_gui.InventoryOpen)
                {
                    // Find hovered InvItem
                    var mousePos = GetViewport().GetMousePosition();
                    foreach (Node child in _inventory.GetNode("InventoryPanel").GetChildren())
                    {
                        if (child is InvItem invItem && invItem.Visible)
                        {
                            var rect = invItem.GetGlobalRect();
                            if (rect.HasPoint(mousePos))
                            {
                                invItem.SetBindNumber(numberPressed);
                                BindItemToHotbarSlot(invItem, numberPressed);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    SelectHotbarSlot(numberPressed);
                }
            }
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

        // Write into CharacterBody3D.Velocity, not a local field
        float currentSpeed = _isSprinting ? _sprintSpeed : _baseSpeed;
        Velocity = new Vector3(
            direction.X * currentSpeed,
            Velocity.Y, // preserve vertical component for gravity/jump
            direction.Z * currentSpeed
        );

        if (!isOnFloor)
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);

        if (isOnFloor && Input.IsActionJustPressed("jump"))
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);


        // Velocity = new Vector3(Velocity.X, 0, Velocity.Z);

        // Godot 4 CharacterBody3D: MoveAndSlide() takes no arguments
        MoveAndSlide();
        isOnFloor = IsOnFloor();

        // Handle mouse look
        if (_gui is null || !_gui.InventoryOpen)
        {
            RotateY(-mouseDelta.X * MouseSensitivity);
            
            // Apply vertical rotation to the active camera
            Camera3D activeCamera = isThirdPerson ? cameraBack : camera;
            activeCamera.RotateX(-mouseDelta.Y * MouseSensitivity);
            mouseDelta = Vector2.Zero;

            // Clamp camera rotation
            var rotation = activeCamera.RotationDegrees;
            rotation.X = Mathf.Clamp(rotation.X, -90, 90);
            activeCamera.RotationDegrees = rotation;
        }


        // FOV adjustment for sprint (only for first person camera)
        float targetFov = _isSprinting ? _sprintFov : _baseFov;
        camera.Fov = Mathf.Lerp(camera.Fov, targetFov, 8.0f * (float)delta);

        // Handle walking sound effects
        bool isMoving = direction.LengthSquared() > 0.01f && isOnFloor;
        if (isMoving)
        {
            float currentStepInterval = _isSprinting ? _sprintStepInterval : _baseStepInterval;
            _stepTimer += (float)delta;
            if (_stepTimer >= currentStepInterval)
            {
                // Play alternating footstep sounds
                if (_nextStepIsStep1)
                {
                    _walkSoundPlayer.Stream = _step1Sound;
                }
                else
                {
                    _walkSoundPlayer.Stream = _step2Sound;
                }
                _walkSoundPlayer.Play();
                _nextStepIsStep1 = !_nextStepIsStep1;
                _stepTimer = 0f;
            }
        }
        else
        {
            _stepTimer = 0f; // Reset timer when not moving
        }
        _wasMoving = isMoving;
    }


    private GodotObject PlayerObjectRay(float range = 5.0f)
    {
        var camera = GetNode<Camera3D>("Camera3D");
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = camera.GlobalTransform.Origin;
        var to = from + camera.GlobalTransform.Basis.Z * -range;
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

    private void TryGrabNonToolItem()
    {
        var collider = PlayerObjectRay(5.0f);
        if (collider == null)
        {
            GD.Print("[DEBUG] No object hit by pickup raycast");
            return;
        }
        
        // Look for IPickupable in the parent chain
        IPickupable pickupable = null;
        Node nodeToCheck = collider as Node;
        
        while (nodeToCheck != null && pickupable == null)
        {
            if (nodeToCheck is IPickupable p && p.CanBePickedUp())
            {
                // Skip tools - they require F key
                if (nodeToCheck is GameItem gItem && gItem.ItemDef?.IsTool == true)
                {
                    GD.Print("[DEBUG] Found tool item, but E key doesn't work on tools. Use F key.");
                    return;
                }
                pickupable = p;
                break;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
        
        if (pickupable != null && pickupable is Node3D node3D)
        {
            float distance = GlobalPosition.DistanceTo(node3D.GlobalPosition);
            GD.Print($"[DEBUG] Found '{pickupable.ItemName}' at distance {distance:F2}");
            
            if (distance <= pickupable.PickupRange)
            {
                PickupItem(node3D);
            }
            else
            {
                GD.Print($"Item is too far away ({distance:F1}m). Get closer!");
            }
        }
        else
        {
            GD.Print("[DEBUG] No pickupable non-tool item found!");
        }
    }

    private void TryGrabItem()
    {
        var collider = PlayerObjectRay(5.0f);
        if (collider == null)
        {
            GD.Print("[DEBUG] No object hit by pickup raycast");
            return;
        }
        GD.Print($"[DEBUG] Raycast hit: {collider}");
        
        // Look for IPickupable in the parent chain
        IPickupable pickupable = null;
        Node nodeToCheck = collider as Node;
        
        while (nodeToCheck != null && pickupable == null)
        {
            if (nodeToCheck is IPickupable p && p.CanBePickedUp())
            {
                pickupable = p;
                break;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
        
        if (pickupable != null && pickupable is Node3D node3D)
        {
            float distance = GlobalPosition.DistanceTo(node3D.GlobalPosition);
            GD.Print($"[DEBUG] Found '{pickupable.ItemName}' at distance {distance:F2}");
            
            if (distance <= pickupable.PickupRange)
            {
                PickupItem(node3D);
            }
            else
            {
                GD.Print($"Item is too far away ({distance:F1}m). Get closer!");
            }
        }
        else
        {
            GD.Print("[DEBUG] No pickupable item found in parent chain of collider!");
        }
    }

    private void PickupItem(Node3D pickedItem)
    {
        GD.Print($"[DEBUG] PickupItem called with {pickedItem.Name} of type {pickedItem.GetType().Name}");
        
        // Must implement IPickupable
        if (pickedItem is not IPickupable pickupable)
        {
            GD.PrintErr($"Cannot pickup {pickedItem.Name} - doesn't implement IPickupable!");
            return;
        }
        
        // Store as GameItem for physics operations (both GameItem and ToolItem are RigidBody3D)
        if (pickedItem is not GameItem gameItem)
        {
            GD.PrintErr($"Cannot pickup {pickedItem.Name} - not a GameItem!");
            return;
        }
        
        _heldItem = gameItem;
        bool isTool = gameItem.ItemDef?.IsTool ?? false;

        if (isTool)
        {
             GD.Print($"[DEBUG] Item is a tool. Equipping with static hold.");
             var toolItem = gameItem as ToolItem;
             
             // Static hold for tools - reparent to hold position
             if (pickedItem.GetParent() != null)
                 pickedItem.GetParent().RemoveChild(pickedItem);
             
             if (_holdPosition == null)
             {
                  camera = GetNode<Camera3D>("Camera3D");
                  _holdPosition = camera.GetNodeOrNull<Node3D>("HoldPosition");
             }
             
             if (_holdPosition != null)
             {
                _holdPosition.AddChild(pickedItem);
                pickedItem.Transform = Transform3D.Identity;
                pickedItem.RotationDegrees = toolItem.HoldRotation;
                pickedItem.Position = toolItem.HoldPosition;
                pickedItem.Scale = toolItem.HoldScale;
             }
             
             // Disable physics completely for held tools
             gameItem.Freeze = true;
             gameItem.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
             gameItem.LinearVelocity = Vector3.Zero;
             gameItem.AngularVelocity = Vector3.Zero;
             gameItem.CollisionLayer = 0;
             gameItem.CollisionMask = 0;
             
             toolItem?.OnEquip();
             pickupable.OnPickedUp();
             GD.Print($"Picked up tool: {pickupable.ItemName}");
        }
        else
        {
            GD.Print($"[DEBUG] Item is non-tool. Using floaty physics.");
            // Floaty physics for regular items
            gameItem.Freeze = false;
            gameItem.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            gameItem.GravityScale = 0;
            gameItem.LinearVelocity = Vector3.Zero;
            gameItem.AngularVelocity = Vector3.Zero;
            pickupable.OnPickedUp();
            GD.Print($"Picked up: {pickupable.ItemName}");
        }
    }
    
    private void RemoveHeldItemFromInventory()
    {
        if (_heldItem == null) return;

        // Find which hotbar slot contains this item
        InvItem itemToRemove = null;
        int slotToRemove = -1;
        
        for (int i = 1; i <= 6; i++)
        {
            if (_hotbarItems[i] != null && _hotbarItems[i].gameItem == _heldItem)
            {
                itemToRemove = _hotbarItems[i];
                slotToRemove = i;
                break;
            }
        }
        
        // Also check the invItem link if it exists (using IPickupable interface)
        if (itemToRemove == null && _heldItem is IPickupable pickupable && pickupable.invItem != null)
        {
            itemToRemove = pickupable.invItem;
        }
        
        if (itemToRemove != null)
        {
            // Clear the tiles that this item occupies
            if (itemToRemove.itemTiles != null)
            {
                foreach (var tile in itemToRemove.itemTiles)
                {
                    if (tile != null)
                        tile.item = null;
                }
                itemToRemove.itemTiles.Clear();
            }
            
            var invPanel = _inventory.GetNode("InventoryPanel");
            // Remove from inventory panel
            if (itemToRemove.GetParent() == invPanel)
            {
                invPanel.RemoveChild(itemToRemove);
                itemToRemove.QueueFree();
            }
            
            // Clear the invItem link using IPickupable interface
            if (_heldItem is IPickupable p)
            {
                p.invItem = null;
            }
        }
        
        // Remove from hotbar slot
        if (slotToRemove >= 1 && slotToRemove <= 6)
        {
            _hotbarItems[slotToRemove] = null;
            UpdateHotbarSlot(slotToRemove);
        }
        
        // If current hotbar slot matches, clear it
        if (_currentHotbarSlot >= 1 && _currentHotbarSlot <= 6 && 
            _hotbarItems[_currentHotbarSlot] == null)
        {
            UpdateHotbarHighlight();
        }
    }

    private void DropHeldItem()
    {
        if (_heldItem == null) return;
        
        bool isTool = _heldItem.ItemDef?.IsTool ?? false;
        
        if (isTool)
        {
             // Tool items need special handling for unequipping from hold position
             if (_heldItem.GetParent() == _holdPosition)
             {
                 _holdPosition.RemoveChild(_heldItem);
                 GetParent().AddChild(_heldItem);
                 _heldItem.GlobalPosition = _holdPosition.GlobalPosition;
             }
             
             // Ensure proper physics re-enable
             _heldItem.Freeze = false;
             _heldItem.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
             _heldItem.GravityScale = 1.0f;
             _heldItem.CollisionLayer = 1;
             _heldItem.CollisionMask = 1;
             _heldItem.LinearVelocity = Vector3.Zero;
             _heldItem.AngularVelocity = Vector3.Zero;
             
             // Call tool-specific unequip
             (_heldItem as ToolItem)?.OnUnequip();
             _heldItem.OnDropped();
             RemoveHeldItemFromInventory();
             GD.Print($"Dropped tool: {_heldItem.ItemName}");
        }
        else
        {
            _heldItem.GravityScale = 1;
            _heldItem.OnDropped();
            RemoveHeldItemFromInventory();
            GD.Print($"Dropped: {_heldItem.ItemName}");
        }
        
        _heldItem = null;
    }

    private void DropToolItem()
    {
        if (_heldItem == null) return;
        
        bool isTool = _heldItem.ItemDef?.IsTool ?? false;
        if (!isTool) return;
        
        // Remove from hold position and add to world
        if (_heldItem.GetParent() == _holdPosition)
        {
            _holdPosition.RemoveChild(_heldItem);
            GetParent().AddChild(_heldItem);
        }
        
        // Position it in front of the player
        var camera = GetNode<Camera3D>("Camera3D");
        var dropPosition = camera.GlobalTransform.Origin + camera.GlobalTransform.Basis.Z * -1.5f;
        _heldItem.GlobalPosition = dropPosition;
        
        // Ensure proper physics re-enable
        _heldItem.Freeze = false;
        _heldItem.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        _heldItem.GravityScale = 1.0f;
        _heldItem.CollisionLayer = 1;
        _heldItem.CollisionMask = 1;
        _heldItem.LinearVelocity = Vector3.Zero;
        _heldItem.AngularVelocity = Vector3.Zero;
        
        // Call unequip and dropped
        (_heldItem as ToolItem)?.OnUnequip();
        _heldItem.OnDropped();
        
        // Remove from inventory
        RemoveHeldItemFromInventory();
        
        GD.Print($"Dropped tool: {_heldItem.ItemName}");
        _heldItem = null;
    }
    
    private void ThrowHeldItem()
    {
        if (_heldItem == null) return;
        
        var camera = GetNode<Camera3D>("Camera3D");
        var throwDirection = -camera.GlobalTransform.Basis.Z;
        
        // Use interface to get throw force
        if (_heldItem is IPickupable pickupable)
        {
            var throwForce = pickupable.ThrowForce;
            _heldItem.GravityScale = 1; // Re-enable gravity for all thrown items
            pickupable.OnThrown(throwDirection, throwForce);
            RemoveHeldItemFromInventory();
            GD.Print($"Threw: {pickupable.ItemName}");
        }
        
        _heldItem = null;
    }

    // Refactored node raising logic for ShovelTool
    public void RaiseNodeWithShovel()
    {
        var camera = GetNode<Camera3D>("Camera3D");
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = camera.GlobalTransform.Origin;
        var to = from + camera.GlobalTransform.Basis.Z * -1000;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            Node nodeToCheck = collider as Node;
            while (nodeToCheck != null)
            {
                if (nodeToCheck is GraphNode graphNode)
                {
                    GD.Print($"[TOOL: Shovel] Clicked on GraphNode: {graphNode.Name}");
                    var hitPoint = (Vector3)result["position"];
                    var newPosition = new Vector3(hitPoint.X, graphNode.Position.Y + 1.0f, hitPoint.Z);
                    graphNode.Position = newPosition;
                    GD.Print($"[TOOL: Shovel] Moved {graphNode.Name} to {newPosition}");
                    return;
                }
                nodeToCheck = nodeToCheck.GetParent();
            }
            GD.Print($"[TOOL: Shovel] Clicked on: {collider}");
        }
        else
        {
            GD.Print("[TOOL: Shovel] No object hit by raycast");
        }
    }
    
    private void ToggleCamera()
    {
        isThirdPerson = !isThirdPerson;
        
        if (isThirdPerson)
        {
            // Switch to third person
            camera.Current = false;
            cameraBack.Current = true;
            GD.Print("Switched to third person camera");
        }
        else
        {
            // Switch to first person
            cameraBack.Current = false;
            camera.Current = true;
            GD.Print("Switched to first person camera");
        }
    }
}
