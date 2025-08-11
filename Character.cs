using Godot;
using System;
using System.Collections.Generic;

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
    private RigidBody3D _heldItem;
    private PickupableItem _heldPickupableComponent;
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

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        var terrain = GetTree().Root.FindChild("Terrain", true, false);

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

        // Create crosshair UI
        CreateCrosshair();
        
        // Create hold position for picked up items
        _holdPosition = new Node3D();
        _holdPosition.Name = "HoldPosition";
        var camera = GetNode<Camera3D>("Camera3D");
        camera.AddChild(_holdPosition);
        // Position it in front of the camera
        _holdPosition.Position = new Vector3(0, -0.5f, -2.0f);

        // Tool system setup
        _tools = PlayerTools.GetDefaultTools(this);
        _currentToolIndex = 0;
        SetupToolLabel();
        UpdateToolLabel();
    }

    private void SetupToolLabel()
    {
        // Find or create a CanvasLayer for GUI
        CanvasLayer canvasLayer = null;
        try { canvasLayer = GetParent().GetNode<CanvasLayer>("CanvasLayer"); } catch { }
        if (canvasLayer == null)
        {
            canvasLayer = new CanvasLayer();
            canvasLayer.Name = "CanvasLayer";
            GetParent().AddChild(canvasLayer);
        }
        // Create or find the label
        _toolLabel = canvasLayer.GetNodeOrNull<Label>("ToolLabel");
        if (_toolLabel == null)
        {
            _toolLabel = new Label();
            _toolLabel.Name = "ToolLabel";
            _toolLabel.Position = new Vector2(12, 12);
            _toolLabel.SizeFlagsHorizontal = (Control.SizeFlags)Control.SizeFlags.ExpandFill;
            _toolLabel.SizeFlagsVertical = (Control.SizeFlags)Control.SizeFlags.ExpandFill;
            _toolLabel.AddThemeColorOverride("font_color", new Color(1,1,1));
            _toolLabel.AddThemeFontSizeOverride("font_size", 24);
            canvasLayer.AddChild(_toolLabel);
        }
    }

    private void UpdateToolLabel()
    {
        if (_toolLabel != null && _tools.Count > 0)
            _toolLabel.Text = $"Tool: {_tools[_currentToolIndex].Name}";
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
            else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                if (_heldItem != null)
                {
                    DropHeldItem();
                }
            }
            // Tool switching with mouse wheel and tool scroll for CreateNodeTool and LinkerTool
            else if (@event is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                if (_tools.Count > 0)
                {
                    float scrollDelta = (mb.ButtonIndex == MouseButton.WheelUp) ? 1.0f : -1.0f;
                    // Only adjust distance if LMB is held and current tool is CreateNodeTool
                    if (Input.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (_tools[_currentToolIndex] is PlayerTools.CreateNodeTool createNodeTool)
                        {
                            createNodeTool.OnScroll(scrollDelta);
                            return;
                        }
                        if (_tools[_currentToolIndex] is PlayerTools.LinkerTool)
                        {
                            // Prevent tool switching while linking
                            return;
                        }
                    }
                    // Only switch tools if LMB is not pressed
                    if (mb.ButtonIndex == MouseButton.WheelUp)
                        _currentToolIndex = (_currentToolIndex + 1) % _tools.Count;
                    else if (mb.ButtonIndex == MouseButton.WheelDown)
                        _currentToolIndex = (_currentToolIndex - 1 + _tools.Count) % _tools.Count;
                    UpdateToolLabel();
                }
            }
        }
        // Handle E key for pickup
        if (Input.IsActionJustPressed("interact"))
        {
            GD.Print("[DEBUG] E key (interact) pressed");
            if (_heldItem == null)
            {
                TryPickupItem();
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
        RotateY(-mouseDelta.X * MouseSensitivity);
        GetNode<Camera3D>("Camera3D").RotateX(-mouseDelta.Y * MouseSensitivity);
        mouseDelta = Vector2.Zero;

        // Clamp camera rotation
        var camera = GetNode<Camera3D>("Camera3D");
        var rotation = camera.RotationDegrees;
        rotation.X = Mathf.Clamp(rotation.X, -90, 90);
        camera.RotationDegrees = rotation;

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

    private void CreateCrosshair()
    {
        // Find the CanvasLayer - it's a sibling of CharacterBody3D, child of Character
        var canvasLayer = GetParent().GetNode<CanvasLayer>("CanvasLayer");
        GD.Print($"Found CanvasLayer: {canvasLayer != null}");
        
        // Create the crosshair dot directly - no container needed
        var crosshair = new ColorRect();
        crosshair.Color = new Color(1.0f, 1.0f, 1.0f, 1.0f); 
        crosshair.Size = new Vector2(6, 6); 
        crosshair.Name = "Crosshair";
        
        // Position it at the center of the screen manually
        // Set anchors to center
        crosshair.AnchorLeft = 0.5f;
        crosshair.AnchorRight = 0.5f;
        crosshair.AnchorTop = 0.5f;
        crosshair.AnchorBottom = 0.5f;
        
        // Offset by half the size to center the rectangle
        crosshair.OffsetLeft = -3; // Half of 20
        crosshair.OffsetTop = -3;  // Half of 20
        crosshair.OffsetRight = 3;
        crosshair.OffsetBottom = 3;
        
        // Add directly to canvas layer
        canvasLayer.AddChild(crosshair);
        
        // Ensure it's drawn on top
        crosshair.ZIndex = 100;
        
        GD.Print($"Crosshair created - Color: {crosshair.Color}, Size: {crosshair.Size}, Visible: {crosshair.Visible}");
        GD.Print($"Crosshair anchors: L:{crosshair.AnchorLeft} R:{crosshair.AnchorRight} T:{crosshair.AnchorTop} B:{crosshair.AnchorBottom}");
        GD.Print($"Crosshair offsets: L:{crosshair.OffsetLeft} R:{crosshair.OffsetRight} T:{crosshair.OffsetTop} B:{crosshair.OffsetBottom}");
    }
    
    private void TryPickupItem()
    {
        var camera = GetNode<Camera3D>("Camera3D");
        var spaceState = GetWorld3D().DirectSpaceState;
        // Raycast to find items to pickup
        var from = camera.GlobalTransform.Origin;
        var to = from + camera.GlobalTransform.Basis.Z * -5.0f; // Shorter range for pickup
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            GD.Print($"[DEBUG] Raycast hit: {collider}");
            // If the collider is a RigidBody3D, use it directly
            RigidBody3D rigidBody = collider as RigidBody3D;
            if (rigidBody == null)
            {
                // Otherwise, walk up the parent chain to find a RigidBody3D
                Node nodeToCheck = collider as Node;
                while (nodeToCheck != null)
                {
                    if (nodeToCheck is RigidBody3D rb)
                    {
                        rigidBody = rb;
                        break;
                    }
                    nodeToCheck = nodeToCheck.GetParent();
                }
            }
            if (rigidBody != null)
            {
                GD.Print($"[DEBUG] Found RigidBody3D: {rigidBody.Name}");
                // Find any PickupableItem child (not just by name)
                PickupableItem pickupComponent = null;
                foreach (var child in rigidBody.GetChildren())
                {
                    if (child is PickupableItem pi)
                    {
                        pickupComponent = pi;
                        break;
                    }
                }
                if (pickupComponent != null && pickupComponent.CanBePickedUp())
                {
                    float distance = GlobalPosition.DistanceTo(rigidBody.GlobalPosition);
                    GD.Print($"[DEBUG] Found PickupableItem '{pickupComponent.ItemName}' at distance {distance:F2}");
                    if (distance <= pickupComponent.PickupRange)
                    {
                        PickupItem(rigidBody, pickupComponent);
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
        else
        {
            GD.Print("[DEBUG] No object hit by pickup raycast");
        }
    }
    
    private void PickupItem(RigidBody3D item, PickupableItem pickupComponent)
    {
        _heldItem = item;
        _heldPickupableComponent = pickupComponent;
        // Unfreeze and disable gravity for floaty effect
        item.Freeze = false;
        item.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        item.GravityScale = 0;
        item.LinearVelocity = Vector3.Zero;
        item.AngularVelocity = Vector3.Zero;
        pickupComponent.OnPickedUp();
        GD.Print($"Picked up: {pickupComponent.ItemName}");
    }
    
    private void DropHeldItem()
    {
        if (_heldItem == null) return;
        // Restore gravity
        _heldItem.GravityScale = 1;
        _heldPickupableComponent?.OnDropped();
        GD.Print($"Dropped: {_heldPickupableComponent?.ItemName}");
        _heldItem = null;
        _heldPickupableComponent = null;
    }
    
    private void ThrowHeldItem()
    {
        if (_heldItem == null) return;
        var camera = GetNode<Camera3D>("Camera3D");
        var throwDirection = -camera.GlobalTransform.Basis.Z;
        var throwForce = _heldPickupableComponent?.ThrowForce ?? 10.0f;
        _heldItem.GravityScale = 1;
        _heldPickupableComponent?.OnThrown(throwDirection, throwForce);
        GD.Print($"Threw: {_heldPickupableComponent?.ItemName}");
        _heldItem = null;
        _heldPickupableComponent = null;
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