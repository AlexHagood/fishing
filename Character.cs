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
    
    // Node visibility management
    private List<GraphNode> terrainNodes = new List<GraphNode>();
    private Timer visibilityTimer;
    private const float VisibilityRange = 5.0f;

    public override void _Ready()
    {
        // Capture the mouse
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Debug: Print entire scene tree
        CallDeferred(nameof(DebugPrintSceneTree));
        
        // Set up terrain node visibility system
        CallDeferred(nameof(InitializeTerrainNodeVisibility));
    }

    public override void _Input(InputEvent @event)
    {
        // Handle mouse movement
        if (@event is InputEventMouseMotion mouseMotion)
        {
            mouseDelta = mouseMotion.Relative;
        }

        // Allow releasing the mouse with Escape
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
        
        // Debug key to manually trigger visibility update
        if (@event is InputEventKey debugKey && debugKey.Pressed && debugKey.Keycode == Key.V)
        {
            GD.Print("=== MANUAL VISIBILITY UPDATE ===");
            GD.Print($"Player position: {GlobalPosition}");
            GD.Print($"Visibility range: {VisibilityRange}");
            UpdateNodeVisibility();
            GD.Print("=== END MANUAL VISIBILITY UPDATE ===");
        }
        
        // Debug key to toggle all nodes visible/invisible
        if (@event is InputEventKey toggleKey && toggleKey.Pressed && toggleKey.Keycode == Key.T)
        {
            GD.Print("=== TOGGLE ALL NODES VISIBILITY ===");
            bool allVisible = true;
            foreach (var node in terrainNodes)
            {
                if (node?.MeshInstance != null)
                {
                    allVisible = allVisible && node.MeshInstance.Visible;
                }
            }
            
            // Toggle all to opposite state
            bool newState = !allVisible;
            GD.Print($"Setting all {terrainNodes.Count} nodes to visible={newState}");
            
            foreach (var node in terrainNodes)
            {
                if (node?.MeshInstance != null)
                {
                    node.MeshInstance.Visible = newState;
                    GD.Print($"Node {node.Name}: set to {newState}, actual: {node.MeshInstance.Visible}");
                }
            }
            GD.Print("=== END TOGGLE ALL NODES VISIBILITY ===");
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
    
    private void InitializeTerrainNodeVisibility()
    {
        GD.Print("=== TERRAIN NODE VISIBILITY INITIALIZATION ===");
        
        // Try multiple approaches to find the Terrain node
        var terrain = GetTree().CurrentScene.FindChild("Terrain", true, false);
        
        if (terrain == null)
        {
            GD.Print("Terrain node not found with FindChild, searching manually...");
            terrain = FindTerrainNodeRecursive(GetTree().CurrentScene);
        }
        
        if (terrain != null && terrain is Terrain terrainNode)
        {
            GD.Print($"Character found Terrain node: {terrain.Name} (Type: {terrain.GetType().Name})");
            
            // Search within the Terrain node for GraphNode children
            terrainNodes.Clear();
            
            // Search recursively for all GraphNode instances
            FindGraphNodesRecursive(terrainNode, terrainNodes);
            
            GD.Print($"Character loaded {terrainNodes.Count} GraphNode objects for visibility management");
            
            // Print details about each found GraphNode and set initial visibility
            for (int i = 0; i < terrainNodes.Count; i++)
            {
                var node = terrainNodes[i];
                GD.Print($"  GraphNode {i}: {node.Name} at {node.GlobalPosition}");
                
                // Set initial visibility based on distance to player
                if (node.MeshInstance != null)
                {
                    float distance = node.GlobalPosition.DistanceTo(GlobalPosition);
                    bool shouldBeVisible = distance <= VisibilityRange;
                    node.MeshInstance.Visible = shouldBeVisible;
                    GD.Print($"    Initial visibility: {shouldBeVisible} (distance: {distance:F2})");
                }
            }
            
            // Create a timer to update visibility every 0.1 seconds
            visibilityTimer = new Timer();
            visibilityTimer.WaitTime = 0.1;
            visibilityTimer.Timeout += UpdateNodeVisibility;
            visibilityTimer.Autostart = true;
            AddChild(visibilityTimer);
            
            GD.Print("Terrain node visibility system initialized successfully");
        }
        else
        {
            GD.Print("Character could not find Terrain node in scene tree");
            if (terrain != null)
            {
                GD.Print($"Found node named Terrain but wrong type: {terrain.GetType().Name}");
            }
        }
        
        GD.Print("=== END TERRAIN NODE VISIBILITY INITIALIZATION ===");
    }
    
    private Node FindTerrainNodeRecursive(Node startNode)
    {
        if (startNode is Terrain)
        {
            return startNode;
        }
        
        foreach (Node child in startNode.GetChildren())
        {
            var result = FindTerrainNodeRecursive(child);
            if (result != null)
                return result;
        }
        
        return null;
    }
    
    private void FindGraphNodesRecursive(Node startNode, List<GraphNode> foundNodes)
    {
        if (startNode is GraphNode graphNode)
        {
            foundNodes.Add(graphNode);
            GD.Print($"Found GraphNode: {graphNode.Name} at {graphNode.GlobalPosition}");
        }
        
        foreach (Node child in startNode.GetChildren())
        {
            FindGraphNodesRecursive(child, foundNodes);
        }
    }
    
    private void UpdateNodeVisibility()
    {
        Vector3 playerPos = GlobalPosition;
        
        GD.Print($"UpdateNodeVisibility: Player at {playerPos}, checking {terrainNodes.Count} nodes");
        
        foreach (var node in terrainNodes)
        {
            if (node?.MeshInstance != null)
            {
                float distance = node.GlobalPosition.DistanceTo(playerPos);
                bool shouldBeVisible = distance <= VisibilityRange;
                bool wasVisible = node.MeshInstance.Visible;
                
                // Set visibility
                node.MeshInstance.Visible = shouldBeVisible;
                
                // Also change color to make visibility more obvious
                if (node.MeshInstance.MaterialOverride is StandardMaterial3D material)
                {
                    material.AlbedoColor = shouldBeVisible ? new Color(0.0f, 1.0f, 0.0f) : new Color(1.0f, 0.0f, 0.0f); // Green for visible, red for invisible
                }
                
                // Verify it was actually set
                bool actuallyVisible = node.MeshInstance.Visible;
                
                GD.Print($"Node {node.Name}: pos={node.GlobalPosition}, dist={distance:F2}, should={shouldBeVisible}, was={wasVisible}, now={actuallyVisible}");
                
                // Debug MeshInstance details
                if (shouldBeVisible != actuallyVisible)
                {
                    GD.Print($"*** VISIBILITY MISMATCH for {node.Name}: tried to set {shouldBeVisible} but got {actuallyVisible} ***");
                    GD.Print($"    MeshInstance null: {node.MeshInstance == null}");
                    GD.Print($"    MeshInstance valid: {IsInstanceValid(node.MeshInstance)}");
                    GD.Print($"    MeshInstance has mesh: {node.MeshInstance.Mesh != null}");
                    GD.Print($"    MeshInstance parent: {node.MeshInstance.GetParent()?.Name ?? "null"}");
                }
                
                // Try to force visibility update
                if (shouldBeVisible != actuallyVisible)
                {
                    GD.Print($"    Forcing visibility update for {node.Name}...");
                    node.MeshInstance.Visible = shouldBeVisible;
                    node.MeshInstance.Show();
                    if (shouldBeVisible)
                    {
                        node.MeshInstance.Show();
                    }
                    else
                    {
                        node.MeshInstance.Hide();
                    }
                    
                    // Check again
                    bool finalVisible = node.MeshInstance.Visible;
                    GD.Print($"    After forcing: {finalVisible}");
                }
                
                // Debug output when visibility changes successfully
                if (wasVisible != shouldBeVisible && actuallyVisible == shouldBeVisible)
                {
                    GD.Print($"*** Node {node.Name} visibility changed successfully: {wasVisible} -> {shouldBeVisible} ***");
                }
            }
            else
            {
                GD.Print($"Node {node?.Name ?? "null"} has no MeshInstance!");
                if (node != null)
                {
                    GD.Print($"    Node children count: {node.GetChildCount()}");
                    foreach (Node child in node.GetChildren())
                    {
                        GD.Print($"    Child: {child.Name} (Type: {child.GetType().Name})");
                    }
                }
            }
        }
    }
    
    // Debug method to print entire scene tree structure
    private void DebugPrintSceneTree()
    {
        GD.Print("=== SCENE TREE DEBUG ===");
        var root = GetTree().CurrentScene;
        GD.Print($"Scene Root: {root.Name} (Type: {root.GetType().Name})");
        
        DebugPrintNodeRecursive(root, 0);
        
        GD.Print("=== END SCENE TREE DEBUG ===");
    }
    
    private void DebugPrintNodeRecursive(Node node, int depth)
    {
        string indent = new string(' ', depth * 2);
        GD.Print($"{indent}- {node.Name} (Type: {node.GetType().Name})");
        
        // Special handling for Terrain nodes
        if (node is Terrain terrain)
        {
            GD.Print($"{indent}  -> This is a Terrain node!");
        }
        
        // Special handling for GraphNodes
        if (node.GetType().Name.Contains("GraphNode"))
        {
            GD.Print($"{indent}  -> This is a GraphNode!");
        }
        
        foreach (Node child in node.GetChildren())
        {
            DebugPrintNodeRecursive(child, depth + 1);
        }
    }
}
