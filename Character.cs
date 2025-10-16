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

    private System.Collections.Generic.List<GraphNode> toolNodes = new System.Collections.Generic.List<GraphNode>();
    private float toolCheckTimer = 0f;
    
    // Pickup system
    private GameItem _heldItem;
    private Node3D _holdPosition;

    private float _baseSpeed = 5.0f;
    private float _sprintSpeed = 10.0f;
    private float _baseFov = 70.0f;
    private float _sprintFov = 80.0f;
    private bool _isSprinting = false;

    // Tool system
    private List<PlayerTools.PlayerTool> _tools = new List<PlayerTools.PlayerTool>();
    private int _currentToolIndex = 0;
    private Label _toolLabel;

    private Camera3D camera;

    private Gui _gui;

    private Inventory _inventory;

    private bool holdingPickup = false;

    private int _currentHotbarSlot = 0; // 0 = no item, 1-6 = slots

    private InvItem[] _hotbarItems = new InvItem[7]; // 1-6, 0 unused

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        var terrain = GetTree().Root.FindChild("Terrain", true, false);

        _gui = GetParent().GetNode<Gui>("GUI");

        if (terrain != null)
        {
            foreach (var child in terrain.GetChildren())
            {
                if (child is GraphNode node)
                {
                    toolNodes.Add(node);
                }
            }
        }


        // Optionally, store crosshair as a field if you want to show/hide it later
        // _crosshair = crosshair;

        // Create hold position for picked up items
        _holdPosition = new Node3D();
        _holdPosition.Name = "HoldPosition";
        camera = GetNode<Camera3D>("Camera3D");
        camera.AddChild(_holdPosition);
        // Position it in front of the camera
        _holdPosition.Position = new Vector3(0, -0.5f, -2.0f);

        // Tool system setup
        _tools = PlayerTools.GetDefaultTools(this);
        _currentToolIndex = 0;
        SetupToolLabel();
        UpdateToolLabel();

        _inventory = _gui.GetNode<Inventory>("Inventory");

        UpdateHotbarHighlight(); // Initialize hotbar highlight
        UpdateAllHotbarSlots();
    }

    private void SetupToolLabel()
    {
        // Add the tool label directly to the GUI node (inherits from CanvasLayer)
        if (_gui != null)
        {
            _toolLabel = _gui.GetNodeOrNull<Label>("ToolLabel");
            if (_toolLabel == null)
            {
                _toolLabel = new Label();
                _toolLabel.Name = "ToolLabel";
                _toolLabel.Position = new Vector2(12, 12);
                _toolLabel.SizeFlagsHorizontal = (Control.SizeFlags)Control.SizeFlags.ExpandFill;
                _toolLabel.SizeFlagsVertical = (Control.SizeFlags)Control.SizeFlags.ExpandFill;
                _toolLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
                _toolLabel.AddThemeFontSizeOverride("font_size", 24);
                _gui.AddChild(_toolLabel);
            }
        }
    }

    private void UpdateToolLabel()
    {
        if (_toolLabel != null && _tools.Count > 0)
            _toolLabel.Text = $"Tool: {_tools[_currentToolIndex].Name}";
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
        toolCheckTimer += (float)delta;
        if (toolCheckTimer >= 0.1f)
        {
            toolCheckTimer = 0f;
            foreach (var node in toolNodes)
            {
                if (node == null) continue;
                float distance = GlobalTransform.Origin.DistanceTo(node.GlobalTransform.Origin);
                node.Visible = distance <= 40f;
            }
        }
        // Floaty physics hold for picked up item
        if (_heldItem != null && _holdPosition != null)
        {
            // Unfreeze and disable gravity while held
            _heldItem.Freeze = false;
            _heldItem.FreezeMode = RigidBody3D.FreezeModeEnum.Static; // Default for rigid
            _heldItem.GravityScale = 0;
            // Calculate force toward hold position
            Vector3 target = _holdPosition.GlobalPosition;
            Vector3 toTarget = target - _heldItem.GlobalPosition;
            Vector3 velocity = _heldItem.LinearVelocity;
            float followStrength = 8.0f; // Less aggressive
            float damp = 4.0f; // Less aggressive
            Vector3 desiredVelocity = toTarget * followStrength;
            Vector3 force = (desiredVelocity - velocity) * damp;
            _heldItem.ApplyCentralForce(force);
            // --- Angular velocity damping ---
            float angularDamp = 6.0f; // Damping factor for spin
            _heldItem.AngularVelocity *= Mathf.Exp(-angularDamp * (float)delta);
        }
        // Tool process for CreateNodeTool
        if (_tools.Count > 0)
            _tools[_currentToolIndex].OnProcess(delta);


        // Handle F key for pickup with progress bar
        if (Input.IsActionPressed("pickup"))
        {
            var progressBar = _gui.progressBar;
            Node3D collider = PlayerObjectRay(5.0f) as Node3D;
            if (collider == null)
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
                    if (collider is GameItem item)
                    {
                        GD.Print($"Item grabbed");
                        item.InventoryPickup();
                        InvItem invitem = new InvItem(new Vector2(4, 4));
                        invitem.gameItem = item; // Link the GameItem to the InvItem
                        var boxTexture = GD.Load<Texture2D>("res://Textures/boxtex.png");
                        invitem.itemIcon.Texture = boxTexture;
                        if (_inventory.ForceFitItem(invitem))
                        {
                            _inventory.GetNode("InventoryPanel").AddChild(invitem);
                            invitem.Visible = true; // Ensure item is visible after adding
                            item.DisablePhys();
                        }
                        else { wiggleBar(); }
                        
                    }
                    else
                    {
                        GD.Print("No valid item to grab");
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
                        ThrowHeldItem();
                    }
                    else if (_tools.Count > 0)
                    {
                        _tools[_currentToolIndex].OnPrimaryAction();
                    }
                }
                else // LMB released
                {
                    if (_tools.Count > 0)
                        _tools[_currentToolIndex].OnPrimaryRelease();
                }
            }
            // Handle right mouse button (RMB)
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                if (mouseButton.Pressed)
                {
                    if (_heldItem != null)
                    {
                        DropHeldItem();
                    }
                    else if (_tools.Count > 0 && _tools[_currentToolIndex] is PlayerTools.ShovelTool shovel)
                    {
                        shovel.OnSecondaryAction();
                    }
                }
                else // RMB released
                {
                    if (_tools.Count > 0 && _tools[_currentToolIndex] is PlayerTools.ShovelTool shovel)
                    {
                        shovel.OnSecondaryRelease();
                    }
                }
            }
            // Hotbar scroll handler (replace old tool scroll)
            else if (@event is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _currentHotbarSlot++;
                    if (_currentHotbarSlot > 6) _currentHotbarSlot = 1;
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _currentHotbarSlot--;
                    if (_currentHotbarSlot < 1) _currentHotbarSlot = 6;
                }
                UpdateHotbarHighlight();

                // Equip logic: show item in front of player and hold it
                var invItem = _hotbarItems[_currentHotbarSlot];
                if (invItem != null && invItem.gameItem != null)
                {
                    var gameItem = invItem.gameItem;
                    // Make sure item is visible and enable physics
                    gameItem.Visible = true;
                    gameItem.Freeze = false;
                    gameItem.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
                    gameItem.GravityScale = 0;
                    // Teleport in front of camera
                    var camera = GetNode<Camera3D>("Camera3D");
                    var frontPos = camera.GlobalTransform.Origin + camera.GlobalTransform.Basis.Z * -2.0f + new Vector3(0, -0.5f, 0);
                    gameItem.GlobalPosition = frontPos;
                    // Set as held item
                    _heldItem = gameItem;
                    gameItem.OnPickedUp();
                    
                    
                    GD.Print($"Equipped item from hotbar: {gameItem.ItemName}");
                }
                else
                {
                    // Un-equip if slot is empty or switching to a different item
                    if (_heldItem != null)
                    {
                        _heldItem.Visible = false; // Hide the item from the world
                        _heldItem.OnDropped();
                        _heldItem = null;
                        GD.Print("No item in this hotbar slot, unequipped.");
                    }
                }
            }
        }
        // Handle E key for pickup
        if (Input.IsActionJustPressed("interact"))
        {
            GD.Print("[DEBUG] E key (interact) pressed");
            if (_heldItem == null)
            {
                TryGrabItem();
            }
        }
        // Allow releasing the mouse with Escape
        if (@event is InputEventKey keyEvent2 && keyEvent2.Pressed && keyEvent2.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
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
            camera.RotateX(-mouseDelta.Y * MouseSensitivity);
            mouseDelta = Vector2.Zero;

            // Clamp camera rotation
            var rotation = camera.RotationDegrees;
            rotation.X = Mathf.Clamp(rotation.X, -90, 90);
            camera.RotationDegrees = rotation;
        }


        // FOV adjustment for sprint
        float targetFov = _isSprinting ? _sprintFov : _baseFov;
        camera.Fov = Mathf.Lerp(camera.Fov, targetFov, 8.0f * (float)delta);
    }

    private void RaycastForNode()
    {
        var camera = GetNode<Camera3D>("Camera3D");
        var spaceState = GetWorld3D().DirectSpaceState;
        
        // Get the center of the screen (where the player is aiming)
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var screenCenter = viewportSize / 2;
        
        // Create a ray from the camera through the center of the screen
        var from = camera.GlobalTransform.Origin;
        var to = from + camera.GlobalTransform.Basis.Z * -1000; // Ray extending 1000 units forward
        
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        
        // Exclude the character's own collision body to prevent hitting ourselves
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        
        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            
            // Check if the collider is a GraphNode or its parent is
            Node nodeToCheck = collider as Node;
            while (nodeToCheck != null)
            {
                if (nodeToCheck is GraphNode graphNode)
                {
                    GD.Print($"Clicked on GraphNode: {graphNode.Name}");
                    
                    // Get the hit point from the raycast
                    var hitPoint = (Vector3)result["position"];
                    
                    // Move the node to the hit point (projected to maintain terrain-like behavior)
                    var newPosition = new Vector3(hitPoint.X, graphNode.Position.Y + 1.0f, hitPoint.Z);
                    graphNode.Position = newPosition;
                    
                    GD.Print($"Moved {graphNode.Name} to {newPosition}");
                    return;
                }
                nodeToCheck = nodeToCheck.GetParent();
            }
            
            GD.Print($"Clicked on: {collider}");
        }
        else
        {
            GD.Print("No object hit by raycast");
        }
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

    private void TryGrabItem()
    {
        var collider = PlayerObjectRay(5.0f);
        if (collider == null)
        {
            GD.Print("[DEBUG] No object hit by pickup raycast");
            return;
        }
        GD.Print($"[DEBUG] Raycast hit: {collider}");
        RigidBody3D rigidBody = collider as RigidBody3D;
        if (rigidBody == null)
        {
            Node nodeToCheck = collider as Node;
            while (nodeToCheck != null && rigidBody == null)
            {
                rigidBody = nodeToCheck as RigidBody3D;
                nodeToCheck = nodeToCheck.GetParent();
            }
        }
        if (rigidBody != null)
        {
            if (rigidBody is GameItem)
            {
                var item = rigidBody as GameItem;
                float distance = GlobalPosition.DistanceTo(rigidBody.GlobalPosition);
                GD.Print($"[DEBUG] Found PickupableItem '{item.ItemName}' at distance {distance:F2}");
                if (distance <= item.PickupRange)
                {
                    PickupItem(item);
                }
                else
                {
                    GD.Print($"Item is too far away ({distance:F1}m). Get closer!");
                }
            }
            else
            {
                GD.Print("[DEBUG] This item cannot be picked up or no PickupableItem found!");
            }
        }
        else
        {
            GD.Print("[DEBUG] No RigidBody3D found in parent chain of collider!");
        }
    }

    private void PickupItem(GameItem heldItem)
    {
        _heldItem = heldItem;
        // Unfreeze and disable gravity for floaty effect
        heldItem.Freeze = false;
        heldItem.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        heldItem.GravityScale = 0;
        heldItem.LinearVelocity = Vector3.Zero;
        heldItem.AngularVelocity = Vector3.Zero;
        heldItem.OnPickedUp();
        GD.Print($"Picked up: {heldItem.Name}");
    }
    
    private void RemoveHeldItemFromInventory()
    {
        if (_heldItem != null && _heldItem.invItem != null)
        {
            var invPanel = _inventory.GetNode("InventoryPanel");
            // Remove from inventory panel
            if (_heldItem.invItem.GetParent() == invPanel)
            {
                invPanel.RemoveChild(_heldItem.invItem);
                _heldItem.invItem.QueueFree();
            }
            // Remove from hotbar slots
            for (int i = 1; i <= 6; i++)
            {
                if (_hotbarItems[i] == _heldItem.invItem)
                    _hotbarItems[i] = null;
            }
            UpdateAllHotbarSlots();
            _heldItem.invItem = null;
        }
    }

    private void DropHeldItem()
    {
        if (_heldItem == null) return;
        _heldItem.GravityScale = 1;
        _heldItem?.OnDropped();
        RemoveHeldItemFromInventory();
        GD.Print($"Dropped: {_heldItem.Name}");
        _heldItem = null;
    }
    
    private void ThrowHeldItem()
    {
        if (_heldItem == null) return;
        var camera = GetNode<Camera3D>("Camera3D");
        var throwDirection = -camera.GlobalTransform.Basis.Z;
        var throwForce = _heldItem?.ThrowForce ?? 10.0f;
        _heldItem.GravityScale = 1;
        _heldItem?.OnThrown(throwDirection, throwForce);
        RemoveHeldItemFromInventory();
        GD.Print($"Threw: {_heldItem?.ItemName}");
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
}