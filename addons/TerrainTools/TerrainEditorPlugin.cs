#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class TerrainEditorPlugin : EditorPlugin
{
    private Terrain _currentTerrain;
    private MeshInstance3D _brushPreview;
    private bool _brushEnabled = false;
    
    // Brush settings
    private float _brushRadius = 2.0f;
    private float _brushStrength = 1.0f;
    
    // UI controls
    private Control _toolbarControl;
    private Button _toggleBrushButton;
    private HSlider _radiusSlider;
    private HSlider _strengthSlider;
    private Label _statusLabel;

    public override void _EnterTree()
    {
        GD.Print("TerrainEditorPlugin: Activated");
        CreateToolbarUI();
    }

    public override void _ExitTree()
    {
        GD.Print("TerrainEditorPlugin: Deactivated");
        CleanupBrushPreview();
        
        if (_toolbarControl != null)
        {
            RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, _toolbarControl);
            _toolbarControl.QueueFree();
            _toolbarControl = null;
        }
    }

    public override bool _Handles(GodotObject @object)
    {
        return @object is Terrain;
    }

    public override void _Edit(GodotObject @object)
    {
        if (@object is Terrain terrain)
        {
            _currentTerrain = terrain;
            GD.Print($"TerrainEditorPlugin: Now editing terrain - {terrain.Name}");
            
            // Update UI visibility
            if (_toolbarControl != null)
            {
                _toolbarControl.Visible = true;
            }
        }
        else
        {
            _currentTerrain = null;
        }
    }

    public override void _MakeVisible(bool visible)
    {
        if (!visible)
        {
            _currentTerrain = null;
            SetBrushEnabled(false);
        }
        
        if (_toolbarControl != null)
        {
            _toolbarControl.Visible = visible && _currentTerrain != null;
        }
    }

    private void CreateToolbarUI()
    {
        // Create main toolbar container
        _toolbarControl = new HBoxContainer();
        
        // Toggle brush button
        _toggleBrushButton = new Button();
        _toggleBrushButton.Text = "Enable Terrain Brush";
        _toggleBrushButton.ToggleMode = true;
        _toggleBrushButton.Pressed += OnToggleBrushPressed;
        _toolbarControl.AddChild(_toggleBrushButton);
        
        // Separator
        _toolbarControl.AddChild(new VSeparator());
        
        // Radius control
        var radiusLabel = new Label();
        radiusLabel.Text = "Radius:";
        _toolbarControl.AddChild(radiusLabel);
        
        _radiusSlider = new HSlider();
        _radiusSlider.MinValue = 0.5f;
        _radiusSlider.MaxValue = 10.0f;
        _radiusSlider.Value = _brushRadius;
        _radiusSlider.Step = 0.1f;
        _radiusSlider.CustomMinimumSize = new Vector2(100, 0);
        _radiusSlider.ValueChanged += OnRadiusChanged;
        _toolbarControl.AddChild(_radiusSlider);
        
        var radiusValue = new Label();
        radiusValue.Text = _brushRadius.ToString("F1");
        radiusValue.CustomMinimumSize = new Vector2(30, 0);
        _toolbarControl.AddChild(radiusValue);
        _radiusSlider.ValueChanged += (value) => radiusValue.Text = value.ToString("F1");
        
        // Separator
        _toolbarControl.AddChild(new VSeparator());
        
        // Strength control
        var strengthLabel = new Label();
        strengthLabel.Text = "Strength:";
        _toolbarControl.AddChild(strengthLabel);
        
        _strengthSlider = new HSlider();
        _strengthSlider.MinValue = 0.1f;
        _strengthSlider.MaxValue = 5.0f;
        _strengthSlider.Value = _brushStrength;
        _strengthSlider.Step = 0.1f;
        _strengthSlider.CustomMinimumSize = new Vector2(100, 0);
        _strengthSlider.ValueChanged += OnStrengthChanged;
        _toolbarControl.AddChild(_strengthSlider);
        
        var strengthValue = new Label();
        strengthValue.Text = _brushStrength.ToString("F1");
        strengthValue.CustomMinimumSize = new Vector2(30, 0);
        _toolbarControl.AddChild(strengthValue);
        _strengthSlider.ValueChanged += (value) => strengthValue.Text = value.ToString("F1");
        
        // Separator
        _toolbarControl.AddChild(new VSeparator());
        
        // Status label
        _statusLabel = new Label();
        _statusLabel.Text = "Select a Terrain node to edit";
        _toolbarControl.AddChild(_statusLabel);
        
        // Add to editor toolbar
        AddControlToContainer(CustomControlContainer.SpatialEditorMenu, _toolbarControl);
        _toolbarControl.Visible = false;
    }

    private void OnToggleBrushPressed()
    {
        SetBrushEnabled(_toggleBrushButton.ButtonPressed);
    }

    private void OnRadiusChanged(double value)
    {
        _brushRadius = (float)value;
        UpdateBrushPreviewScale();
    }

    private void OnStrengthChanged(double value)
    {
        _brushStrength = (float)value;
    }

    private void SetBrushEnabled(bool enabled)
    {
        _brushEnabled = enabled;
        
        if (enabled)
        {
            CreateBrushPreview();
            _statusLabel.Text = "LMB: Raise | RMB: Lower | Shift+LMB: Crack Panel | ESC: Exit";
            GD.Print($"Terrain Brush ENABLED - Radius: {_brushRadius}, Strength: {_brushStrength}");
            GD.Print("BRUSH MODE: LMB=Raise, RMB=Lower, Shift+LMB=Crack Panel. Press ESC to exit.");
        }
        else
        {
            CleanupBrushPreview();
            _statusLabel.Text = "Brush disabled";
            GD.Print("Terrain Brush DISABLED - Normal editor selection restored");
        }
    }

    private void CreateBrushPreview()
    {
        if (_brushPreview != null || _currentTerrain == null)
            return;
        
        // Create brush preview mesh
        _brushPreview = new MeshInstance3D();
        
        // Create a sphere mesh for the preview
        var sphereMesh = new SphereMesh();
        sphereMesh.RadialSegments = 16;
        sphereMesh.Rings = 8;
        _brushPreview.Mesh = sphereMesh;
        
        // Create semi-transparent material
        var material = new StandardMaterial3D();
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.AlbedoColor = new Color(0, 1, 0, 0.3f); // Green, semi-transparent
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.DisableReceiveShadows = true;
        material.NoDepthTest = true;
        _brushPreview.MaterialOverride = material;
        
        // Add to scene as editor-only node
        _currentTerrain.AddChild(_brushPreview, false, Node.InternalMode.Front);
        _brushPreview.Visible = true;
        UpdateBrushPreviewScale();
        
        GD.Print("Brush preview created");
    }

    private void UpdateBrushPreviewScale()
    {
        if (_brushPreview != null)
        {
            _brushPreview.Scale = Vector3.One * _brushRadius * 2.0f;
        }
    }

    private void CleanupBrushPreview()
    {
        if (_brushPreview != null)
        {
            _brushPreview.QueueFree();
            _brushPreview = null;
        }
    }

    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        // When brush is enabled, we intercept ALL input EXCEPT camera controls
        if (_brushEnabled && _currentTerrain != null)
        {
            // Allow ESC to exit brush mode
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
            {
                _toggleBrushButton.ButtonPressed = false;
                SetBrushEnabled(false);
                return 1; // Stop - consume the ESC key
            }
            
            // Allow middle mouse button (MMB) and scroll wheel for camera controls
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Middle || 
                    mb.ButtonIndex == MouseButton.WheelUp || 
                    mb.ButtonIndex == MouseButton.WheelDown)
                {
                    return 0; // Pass - allow camera panning/zooming
                }
            }
            
            // Allow MMB drag for camera panning
            if (@event is InputEventMouseMotion mm)
            {
                if (mm.ButtonMask.HasFlag(MouseButtonMask.Middle))
                {
                    return 0; // Pass - allow camera panning
                }
            }
            
            // Handle all other mouse events when brush is active
            if (@event is InputEventMouse mouseEvent)
            {
                // Calculate ray from mouse position
                var mousePos = mouseEvent.Position;
                var from = camera.ProjectRayOrigin(mousePos);
                var normal = camera.ProjectRayNormal(mousePos);
                var to = from + normal * 10000;

                var spaceState = _currentTerrain.GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(from, to);
                query.CollideWithAreas = false;
                query.CollideWithBodies = true;
                
                var result = spaceState.IntersectRay(query);
                
                if (result.Count > 0)
                {
                    var hitPoint = (Vector3)result["position"];
                    var collider = result["collider"].AsGodotObject();
                    
                    // Update preview position
                    if (_brushPreview != null)
                    {
                        _brushPreview.GlobalPosition = hitPoint;
                        _brushPreview.Visible = true;
                    }

                    // Handle brush painting - NO SHIFT REQUIRED (except for crack panel)
                    if (@event is InputEventMouseButton mb2)
                    {
                        if (mb2.ButtonIndex == MouseButton.Left || mb2.ButtonIndex == MouseButton.Right)
                        {
                            if (mb2.Pressed)
                            {
                                // Shift+LMB = Crack panel
                                if (mb2.ButtonIndex == MouseButton.Left && Input.IsKeyPressed(Key.Shift))
                                {
                                    CrackPanelAtPoint(hitPoint, collider);
                                }
                                else
                                {
                                    // Normal raise/lower
                                    bool raise = (mb2.ButtonIndex == MouseButton.Left);
                                    ApplyBrush(hitPoint, raise);
                                }
                            }
                            return 1; // Stop - Prevents selection and consumes the click
                        }
                    }
                    else if (@event is InputEventMouseMotion mm2)
                    {
                        // Only allow drag painting if NOT holding shift
                        if (!Input.IsKeyPressed(Key.Shift))
                        {
                            // Paint while dragging - NO SHIFT REQUIRED
                            if (mm2.ButtonMask.HasFlag(MouseButtonMask.Left))
                            {
                                ApplyBrush(hitPoint, true);
                                return 1; // Stop - Prevents selection box drawing
                            }
                            else if (mm2.ButtonMask.HasFlag(MouseButtonMask.Right))
                            {
                                ApplyBrush(hitPoint, false);
                                return 1; // Stop - Prevents selection box drawing
                            }
                        }
                    }
                }
                else
                {
                    // Hide preview if not hitting anything
                    if (_brushPreview != null)
                    {
                        _brushPreview.Visible = false;
                    }
                }
                
                // Consume LMB/RMB mouse events when brush is active to prevent selection
                if (@event is InputEventMouseButton mb3)
                {
                    if (mb3.ButtonIndex == MouseButton.Left || mb3.ButtonIndex == MouseButton.Right)
                    {
                        return 1; // Stop - blocks selection behavior
                    }
                }
                
                // Consume LMB/RMB motion to prevent selection boxes
                if (@event is InputEventMouseMotion mm3)
                {
                    if (mm3.ButtonMask.HasFlag(MouseButtonMask.Left) || mm3.ButtonMask.HasFlag(MouseButtonMask.Right))
                    {
                        return 1; // Stop - prevents selection box
                    }
                }
            }
        }
        
        return 0; // Pass - allow normal editor behavior when brush is disabled
    }

    private void ApplyBrush(Vector3 hitPoint, bool raise)
    {
        if (_currentTerrain == null)
            return;
        
        // Update brush color
        if (_brushPreview?.MaterialOverride is StandardMaterial3D mat)
        {
            mat.AlbedoColor = raise ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
        }

        int morphedCount = 0;
        var affectedNodes = new List<GraphNode>();
        
        foreach (var child in _currentTerrain.GetChildren())
        {
            if (child is GraphNode graphNode)
            {
                float distance = graphNode.GlobalPosition.DistanceTo(hitPoint);
                if (distance <= _brushRadius)
                {
                    // Calculate falloff (stronger at center, weaker at edges)
                    float falloff = 1.0f - (distance / _brushRadius);
                    float strength = _brushStrength * falloff * 0.016f; // Approximate delta time

                    // Store old position for potential undo/redo
                    var oldPos = graphNode.Position;
                    
                    if (raise)
                    {
                        graphNode.Position += Vector3.Up * strength;
                    }
                    else
                    {
                        graphNode.Position -= Vector3.Up * strength;
                    }
                    
                    // Manually trigger mesh updates since we're in the editor
                    UpdateGraphNodeMeshes(graphNode);
                    
                    morphedCount++;
                    affectedNodes.Add(graphNode);
                }
            }
        }

        if (morphedCount > 0)
        {
            // Mark terrain as modified for undo/redo
            var ur = GetUndoRedo();
            // Note: For proper undo/redo, we'd need to store previous positions
            // This is a simplified version
        }
    }
    
    private void UpdateGraphNodeMeshes(GraphNode node)
    {
        // Update all connected ground meshes for this node
        if (node.ConnectedGroundMeshes != null)
        {
            foreach (var groundMesh in node.ConnectedGroundMeshes)
            {
                if (groundMesh != null && IsInstanceValid(groundMesh))
                {
                    groundMesh.UpdateMeshGeometry();
                }
            }
        }
    }
    
    private void CrackPanelAtPoint(Vector3 hitPoint, GodotObject collider)
    {
        if (_currentTerrain == null || collider == null)
            return;
        
        GroundMesh groundMesh = null;
        
        // Check if we hit a GroundMesh or its child
        if (collider is GroundMesh gm)
        {
            groundMesh = gm;
        }
        else if (collider is Node node && node.GetParent() is GroundMesh gm2)
        {
            groundMesh = gm2;
        }
        
        if (groundMesh != null)
        {
            // Use GlobalPosition to ensure we're working in the same coordinate system as hitPoint
            var posA = groundMesh.NodeA.GlobalPosition;
            var posB = groundMesh.NodeB.GlobalPosition;
            var posC = groundMesh.NodeC.GlobalPosition;
            
            GD.Print($"Cracking panel: {groundMesh.Name}");
            GD.Print($"  Corner nodes (global): A={posA}, B={posB}, C={posC}");
            GD.Print($"  Raycast hit point (global): {hitPoint}");
            
            // Calculate the exact point on the triangle's plane at the hit XZ location
            // This ensures the new point lies exactly on the original triangle surface
            Vector3 interpolatedPoint = InterpolatePointOnTriangle(hitPoint, posA, posB, posC);
            
            GD.Print($"  Interpolated point on plane (global): {interpolatedPoint}");
            
            // Create new node at the interpolated position
            var newNode = new GraphNode();
            
            // Convert global position to local position relative to the Terrain
            Vector3 localPosition = _currentTerrain.ToLocal(interpolatedPoint);
            newNode.Position = localPosition; // Set local position
            newNode.Name = $"CrackedNode_{System.DateTime.Now.Ticks}";
            
            _currentTerrain.AddChild(newNode, false, Node.InternalMode.Disabled);
            if (Engine.IsEditorHint())
            {
                newNode.Owner = EditorInterface.Singleton.GetEditedSceneRoot();
            }
            
            GD.Print($"  New node created at global: {newNode.GlobalPosition}, local: {newNode.Position}");
            
            // Crack the panel
            _currentTerrain.CrackPanel(newNode, groundMesh);
            
            GD.Print($"Panel cracked successfully");
        }
        else
        {
            GD.PrintErr($"No GroundMesh found - collider type: {collider.GetType().Name}");
        }
    }
    
    /// <summary>
    /// Calculates the exact Y position on a triangle's plane given an XZ position.
    /// Uses the plane equation to ensure the point lies perfectly on the triangle.
    /// </summary>
    private Vector3 InterpolatePointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        // Calculate the plane of the triangle using two edge vectors
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        
        // Normal to the plane
        Vector3 normal = ab.Cross(ac).Normalized();
        
        // Plane equation: normal · (P - A) = 0
        // Solve for Y: normal.x * (X - A.x) + normal.y * (Y - A.y) + normal.z * (Z - A.z) = 0
        // Y = A.y - (normal.x * (X - A.x) + normal.z * (Z - A.z)) / normal.y
        
        if (Mathf.Abs(normal.Y) < 0.0001f)
        {
            // Triangle is vertical or nearly vertical, use average Y
            return new Vector3(point.X, (a.Y + b.Y + c.Y) / 3.0f, point.Z);
        }
        
        float y = a.Y - (normal.X * (point.X - a.X) + normal.Z * (point.Z - a.Z)) / normal.Y;
        
        return new Vector3(point.X, y, point.Z);
    }
}
#endif
