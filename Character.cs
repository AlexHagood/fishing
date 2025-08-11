using Godot;
using System;

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
    }

    public override void _Input(InputEvent @event)
    {
        // Handle mouse movement
        if (@event is InputEventMouseMotion mouseMotion)
        {
            mouseDelta = mouseMotion.Relative;
        }

        // Handle mouse clicks for node interaction
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            RaycastForNode();
        }

        // Allow releasing the mouse with Escape
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
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
        Velocity = new Vector3(
            direction.X * Speed,
            Velocity.Y, // preserve vertical component for gravity/jump
            direction.Z * Speed
        );

        if (!isOnFloor)
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);

        if (isOnFloor && Input.IsActionJustPressed("jump"))
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);


        //Velocity = new Vector3(Velocity.X, 0, Velocity.Z);

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
}
