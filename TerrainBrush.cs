using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class TerrainBrush : Node3D
{
    [Export] public float BrushRadius = 2.0f;
    [Export] public float BrushStrength = 1.0f;
    [Export] public bool RaiseMode = true; // true = raise, false = lower
    
    // Auto-detected parent terrain
    public Terrain TerrainNode
    {
        get
        {
            var parent = GetParent();
            if (parent is Terrain terrain)
            {
                return terrain;
            }
            return null;
        }
    }
    
    private bool isActive = false;
    private MeshInstance3D brushPreview;

    [Export]
    public bool BrushEnabled
    {
        get => isActive;
        set
        {
            if (isActive != value)
            {
                isActive = value;
                if (isActive)
                {
                    ActivateBrush();
                }
                else
                {
                    DeactivateBrush();
                }
            }
        }
    }

    public override void _Ready()
    {
        base._Ready();
        
        // Find existing MeshPreview
        brushPreview = GetNodeOrNull<MeshInstance3D>("MeshPreview");
    }

    public void ActivateBrush()
    {
        isActive = true;
        GD.Print("Terrain Brush: ACTIVE - Hold Shift + LMB to raise, Shift + RMB to lower");
        GD.Print($"Brush Radius: {BrushRadius}, Strength: {BrushStrength}");
        
        // Enable existing MeshPreview
        if (brushPreview == null)
        {
            brushPreview = GetNodeOrNull<MeshInstance3D>("MeshPreview");
        }

        if (brushPreview == null)
        {
            GD.PrintErr("Error: MeshInstance3D named 'MeshPreview' not found as child of TerrainBrush. Please add one to use the brush.");
            isActive = false;
            return;
        }
        
        brushPreview.Visible = true;
        brushPreview.Scale = Vector3.One * BrushRadius * 2.0f;
        GD.Print("Brush preview enabled");
    }

    public void DeactivateBrush()
    {
        isActive = false;
        GD.Print("Terrain Brush: Deactivated");
        
        if (brushPreview != null)
        {
            brushPreview.Visible = false;
        }
    }

    public void UpdateBrushPreview(Vector3 position)
    {
        if (brushPreview != null)
        {
            brushPreview.GlobalPosition = position;
            brushPreview.Visible = true;
        }
    }

    public void OnBrushPaint(Vector3 hitPoint, bool raise)
    {
        RaiseMode = raise;
        
        if (brushPreview != null && brushPreview.MaterialOverride is StandardMaterial3D mat)
        {
            if (raise)
                mat.AlbedoColor = new Color(0, 1, 0, 0.3f); // Green for raise
            else
                mat.AlbedoColor = new Color(1, 0, 0, 0.3f); // Red for lower
        }
        
        ApplyBrush(hitPoint);
    }

    public void ApplyBrush(Vector3 hitPoint)
    {
        // Apply brush to nearby nodes
        if (TerrainNode == null)
            return;

        int morphedCount = 0;
        foreach (var child in TerrainNode.GetChildren())
        {
            if (child is GraphNode graphNode)
            {
                float distance = graphNode.GlobalPosition.DistanceTo(hitPoint);
                if (distance <= BrushRadius)
                {
                    // Calculate falloff (stronger at center, weaker at edges)
                    float falloff = 1.0f - (distance / BrushRadius);
                    float strength = BrushStrength * falloff * 0.016f; // Approximate delta time for consistency

                    if (RaiseMode)
                    {
                        graphNode.Position += Vector3.Up * strength;
                    }
                    else
                    {
                        graphNode.Position -= Vector3.Up * strength;
                    }
                    morphedCount++;
                }
            }
        }

        if (morphedCount > 0)
        {
            GD.Print($"Morphed {morphedCount} nodes: {(RaiseMode ? "raise" : "lower")}");
        }
    }
}
