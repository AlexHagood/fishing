#if TOOLS
using Godot;
using System;

[Tool]
public partial class TerrainEditorPlugin : EditorPlugin
{
    private TerrainBrush _currentBrush;

    public override void _EnterTree()
    {
        // Called when the plugin is activated.
    }

    public override void _ExitTree()
    {
        // Called when the plugin is deactivated.
    }

    public override bool _Handles(GodotObject @object)
    {
        return @object is TerrainBrush;
    }

    public override void _Edit(GodotObject @object)
    {
        _currentBrush = @object as TerrainBrush;
    }

    public override void _MakeVisible(bool visible)
    {
        if (!visible)
        {
            _currentBrush = null;
        }
    }

    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        if (_currentBrush == null || !_currentBrush.BrushEnabled || !_currentBrush.IsInsideTree())
        {
            return 0; // AfterGuiInput.Pass
        }

        if (@event is InputEventMouse mouseEvent)
        {
            // Calculate ray from mouse position
            var mousePos = mouseEvent.Position;
            var from = camera.ProjectRayOrigin(mousePos);
            var normal = camera.ProjectRayNormal(mousePos);
            var to = from + normal * 10000;

            var spaceState = _currentBrush.GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            
            var result = spaceState.IntersectRay(query);
            
            if (result.Count > 0)
            {
                var hitPoint = (Vector3)result["position"];
                
                // Update preview
                _currentBrush.UpdateBrushPreview(hitPoint);

                // Handle clicks
                // MouseButton.Left = 1, MouseButton.Right = 2
                bool isLeftClick = false;
                bool isRightClick = false;

                if (@event is InputEventMouseButton mb)
                {
                    if (mb.ButtonIndex == MouseButton.Left) isLeftClick = mb.Pressed;
                    if (mb.ButtonIndex == MouseButton.Right) isRightClick = mb.Pressed;
                    
                    // Consume the click event if we are in brush mode and holding shift
                    // Original logic required Shift. 
                    if (Input.IsKeyPressed(Key.Shift) && (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
                    {
                         // If pressed, apply brush
                         if (mb.Pressed)
                         {
                             bool raise = (mb.ButtonIndex == MouseButton.Left);
                             _currentBrush.OnBrushPaint(hitPoint, raise);
                         }
                         return 1; // AfterGuiInput.Stop - Prevents selection!
                    }
                }
                else if (@event is InputEventMouseMotion mm)
                {
                    // If dragging with button held
                     if (Input.IsKeyPressed(Key.Shift))
                     {
                        if (mm.ButtonMask.HasFlag(MouseButtonMask.Left))
                        {
                            _currentBrush.OnBrushPaint(hitPoint, true);
                            return 1; // Stop
                        }
                        else if (mm.ButtonMask.HasFlag(MouseButtonMask.Right))
                        {
                            _currentBrush.OnBrushPaint(hitPoint, false);
                            return 1; // Stop
                        }
                     }
                }
            }
        }

        return 0; // Pass
    }
}
#endif
