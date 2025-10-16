using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class PathItem : GameItem
{
    private List<Vector3> pathPositions = new List<Vector3>();
    private float timeSinceLastPoint = 0.0f;
    private const float TraceInterval = 0.33f;
    private bool isCurrentlyPickedUp = false;
    private Node3D debugLinesContainer;

    public override void _Ready()
    {
        base._Ready();
        
        // Create a container in the scene root for debug lines (world space)
        debugLinesContainer = new Node3D();
        debugLinesContainer.Name = "PathItemDebugLines";
        GetTree().Root.CallDeferred("add_child", debugLinesContainer);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (isCurrentlyPickedUp)
        {
            timeSinceLastPoint += (float)delta;

            if (timeSinceLastPoint >= TraceInterval)
            {
                timeSinceLastPoint = 0.0f;
                Vector3 currentPos = GlobalPosition;
                pathPositions.Add(currentPos);
                
                GD.Print($"PathItem trace point {pathPositions.Count}: {currentPos}");
                
                // Draw a marker at current position
                DrawDebugMarker(currentPos);
                
                // Draw line from previous position to current
                if (pathPositions.Count > 1)
                {
                    Vector3 prevPos = pathPositions[pathPositions.Count - 2];
                    DrawDebugLine(prevPos, currentPos);
                }
            }
        }
    }
    
    private void DrawDebugMarker(Vector3 position)
    {
        if (debugLinesContainer == null || !IsInstanceValid(debugLinesContainer))
        {
            GD.PrintErr("Debug lines container is null or invalid for marker!");
            return;
        }
        
        // Create a small sphere at each trace point
        var sphere = new CsgSphere3D();
        sphere.GlobalPosition = position;
        sphere.Radius = 0.1f;
        
        var material = new StandardMaterial3D();
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = Colors.Yellow;
        material.NoDepthTest = true;
        sphere.Material = material;
        
        debugLinesContainer.CallDeferred("add_child", sphere);
        GD.Print($"Drew debug marker at {position}");
    }
    
    private void DrawDebugLine(Vector3 start, Vector3 end)
    {
        if (debugLinesContainer == null || !IsInstanceValid(debugLinesContainer))
        {
            GD.PrintErr("Debug lines container is null or invalid!");
            return;
        }
            
        // Create a CSGBox3D to represent the line (more visible than lines)
        var lineBox = new CsgBox3D();
        
        // Calculate the direction and length
        Vector3 direction = end - start;
        float length = direction.Length();
        Vector3 midpoint = (start + end) / 2.0f;
        
        // Position at midpoint
        lineBox.GlobalPosition = midpoint;
        
        // Set size (thin box acting as line)
        lineBox.Size = new Vector3(0.05f, 0.05f, length);
        
        // Rotate to point from start to end
        if (length > 0.001f)
        {
            Vector3 up = Vector3.Up;
            if (Mathf.Abs(direction.Normalized().Dot(up)) > 0.99f)
            {
                up = Vector3.Right;
            }
            lineBox.LookAt(end, up);
        }
        
        // Create material
        var material = new StandardMaterial3D();
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = Colors.Red;
        material.NoDepthTest = true;
        lineBox.Material = material;
        
        // Add to container
        debugLinesContainer.CallDeferred("add_child", lineBox);
        
        GD.Print($"Drew debug line box from {start} to {end}, length: {length}");
    }

    public new void OnPickedUp()
    {
        base.OnPickedUp();
        
        // Clear previous path and start fresh
        ClearPath();
        isCurrentlyPickedUp = true;
        timeSinceLastPoint = 0.0f;
        
        // Add initial position
        pathPositions.Add(GlobalPosition);
        GD.Print($"PathItem picked up - starting new path trace at {GlobalPosition}");
    }

    public new void OnDropped()
    {
        base.OnDropped();
        isCurrentlyPickedUp = false;
        GD.Print($"PathItem dropped - traced {pathPositions.Count} positions");
    }

    public new void OnThrown(Vector3 throwDirection, float force)
    {
        base.OnThrown(throwDirection, force);
        isCurrentlyPickedUp = false;
        GD.Print($"PathItem thrown - traced {pathPositions.Count} positions");
    }

    private void ClearPath()
    {
        pathPositions.Clear();
        
        // Clear all debug line meshes
        if (debugLinesContainer != null && IsInstanceValid(debugLinesContainer))
        {
            foreach (Node child in debugLinesContainer.GetChildren())
            {
                child.QueueFree();
            }
        }
        
        GD.Print("PathItem path cleared");
    }
    
    public override void _ExitTree()
    {
        base._ExitTree();
        
        // Clean up debug lines when this object is removed
        if (debugLinesContainer != null && IsInstanceValid(debugLinesContainer))
        {
            debugLinesContainer.QueueFree();
        }
    }
}
