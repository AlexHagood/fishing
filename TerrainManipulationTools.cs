using Godot;
using System.Collections.Generic;

/// <summary>
/// Legacy terrain manipulation tools (Shovel, CreateNode).
/// Currently disabled but preserved for future use.
/// To re-enable: uncomment the TerrainToolsManager integration in Character.cs
/// </summary>
public static class TerrainManipulationTools
{
    public abstract class TerrainTool
    {
        public abstract string Name { get; }
        protected Character Character;
        public TerrainTool(Character character) { Character = character; }
        public virtual void OnPrimaryAction() { }
        public virtual void OnPrimaryRelease() { }
        public virtual void OnScroll(float delta) { }
        public virtual void OnProcess(double delta) { }
    }

    public class ShovelTool : TerrainTool
    {
        public override string Name => "Shovel";
        private GraphNode _targetNode = null;
        private bool _holdingRaise = false;
        private bool _holdingLower = false;
        private float _holdTimer = 0f;
        private const float HoldInterval = 0.2f;
        private const float RaiseAmount = 0.1f;

        public ShovelTool(Character character) : base(character) { }

        public override void OnPrimaryAction()
        {
            // LMB pressed: start raising
            _targetNode = RaycastForNode();
            if (_targetNode != null)
            {
                _holdingRaise = true;
                _holdTimer = 0f;
                RaiseNode(_targetNode);
            }
        }

        public override void OnPrimaryRelease()
        {
            // LMB released: stop raising
            _holdingRaise = false;
            _targetNode = null;
        }

        public override void OnProcess(double delta)
        {
            if (_holdingRaise && _targetNode != null)
            {
                _holdTimer += (float)delta;
                if (_holdTimer >= HoldInterval)
                {
                    RaiseNode(_targetNode);
                    _holdTimer = 0f;
                }
            }
            else if (_holdingLower && _targetNode != null)
            {
                _holdTimer += (float)delta;
                if (_holdTimer >= HoldInterval)
                {
                    LowerNode(_targetNode);
                    _holdTimer = 0f;
                }
            }
        }

        // Right mouse button pressed: start lowering
        public void OnSecondaryAction()
        {
            _targetNode = RaycastForNode();
            if (_targetNode != null)
            {
                _holdingLower = true;
                _holdTimer = 0f;
                LowerNode(_targetNode);
            }
        }

        // Right mouse button released: stop lowering
        public void OnSecondaryRelease()
        {
            _holdingLower = false;
            _targetNode = null;
        }

        private void RaiseNode(GraphNode node)
        {
            var pos = node.Position;
            node.Position = new Vector3(pos.X, pos.Y + RaiseAmount, pos.Z);
        }

        private void LowerNode(GraphNode node)
        {
            var pos = node.Position;
            node.Position = new Vector3(pos.X, pos.Y - RaiseAmount, pos.Z);
        }

        // Helper: Raycast from camera to find a GraphNode
        private GraphNode RaycastForNode()
        {
            var camera = Character.GetNode<Camera3D>("Camera3D");
            var spaceState = Character.GetWorld3D().DirectSpaceState;
            var from = camera.GlobalTransform.Origin;
            var to = from + camera.GlobalTransform.Basis.Z * -1000f;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            query.Exclude = new Godot.Collections.Array<Rid> { Character.GetRid() };
            var result = spaceState.IntersectRay(query);
            if (result.Count > 0)
            {
                var collider = result["collider"].AsGodotObject();
                Node nodeToCheck = collider as Node;
                while (nodeToCheck != null)
                {
                    if (nodeToCheck is GraphNode graphNode)
                        return graphNode;
                    nodeToCheck = nodeToCheck.GetParent();
                }
            }
            return null;
        }
    }

    public class CreateNodeTool : TerrainTool
    {
        public override string Name => "CreateNode";
        private MeshInstance3D _previewMesh;
        private float _distance = 4.0f;
        private float _minDistance = 1.0f;
        private float _maxDistance = 20.0f;
        private bool _placing = false;

        public CreateNodeTool(Character character) : base(character) { }

        public override void OnPrimaryAction()
        {
            if (_placing) return;
            _placing = true;
            ShowPreviewMesh();
        }

        public override void OnPrimaryRelease()
        {
            if (!_placing) return;
            _placing = false;
            PlaceNodeAtPreview();
            HidePreviewMesh();
        }

        public override void OnScroll(float delta)
        {
            // Only adjust distance if placing, but don't block scroll event for tool switching
            if (_placing)
                _distance = Mathf.Clamp(_distance + delta, _minDistance, _maxDistance);
        }

        public override void OnProcess(double delta)
        {
            if (_placing)
                UpdatePreviewMesh();
        }

        private void ShowPreviewMesh()
        {
            if (_previewMesh == null)
            {
                _previewMesh = new MeshInstance3D();
                _previewMesh.Mesh = new SphereMesh { Radius = 0.2f, Height = 0.2f };
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.2f, 0.8f, 1.0f, 0.5f);
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                _previewMesh.MaterialOverride = mat;
                _previewMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _previewMesh.Name = "NodePreviewMesh";
                // No collider, so not pickable
                Character.AddChild(_previewMesh);
            }
            _previewMesh.Visible = true;
            UpdatePreviewMesh();
        }

        private void HidePreviewMesh()
        {
            if (_previewMesh != null)
                _previewMesh.Visible = false;
        }

        private void UpdatePreviewMesh()
        {
            var camera = Character.GetNode<Camera3D>("Camera3D");
            var pos = camera.GlobalTransform.Origin + camera.GlobalTransform.Basis.Z * -_distance;
            _previewMesh.GlobalPosition = pos;
        }

        private void PlaceNodeAtPreview()
        {
            var camera = Character.GetNode<Camera3D>("Camera3D");
            var nodePos = camera.GlobalTransform.Origin + camera.GlobalTransform.Basis.Z * -_distance;

            var terrain = Character.GetTree().Root.FindChild("Terrain", true, false);
            if (terrain == null)
            {
                GD.PrintErr("Terrain node not found. Cannot create node.");
                return;
            }

            var newNode = new GraphNode();
            newNode.Position = nodePos;
            terrain.CallDeferred("add_child", newNode);

            GD.Print($"Created node at {nodePos}");
        }
    }

    public static List<TerrainTool> GetDefaultTools(Character character)
    {
        return new List<TerrainTool>
        {
            new ShovelTool(character),
            new CreateNodeTool(character)
        };
    }
}

/// <summary>
/// Optional manager class to handle terrain tool system in Character.
/// Uncomment and integrate into Character.cs to re-enable terrain tools.
/// </summary>
/*
public class TerrainToolsManager
{
    private List<TerrainManipulationTools.TerrainTool> _tools;
    private int _currentToolIndex = 0;
    private Label _toolLabel;
    private Character _character;
    private List<GraphNode> _toolNodes = new List<GraphNode>();
    private float _toolCheckTimer = 0f;

    public TerrainToolsManager(Character character, Gui gui)
    {
        _character = character;
        _tools = TerrainManipulationTools.GetDefaultTools(character);
        SetupToolLabel(gui);
        InitializeToolNodes();
    }

    private void SetupToolLabel(Gui gui)
    {
        if (gui != null)
        {
            _toolLabel = gui.GetNodeOrNull<Label>("ToolLabel");
            if (_toolLabel == null)
            {
                _toolLabel = new Label();
                _toolLabel.Name = "ToolLabel";
                _toolLabel.Position = new Vector2(12, 12);
                _toolLabel.SizeFlagsHorizontal = (Control.SizeFlags)Control.SizeFlags.ExpandFill;
                _toolLabel.SizeFlagsVertical = (Control.SizeFlags)Control.SizeFlags.ExpandFill;
                _toolLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
                _toolLabel.AddThemeFontSizeOverride("font_size", 24);
                gui.AddChild(_toolLabel);
            }
        }
        UpdateToolLabel();
    }

    private void InitializeToolNodes()
    {
        var terrain = _character.GetTree().Root.FindChild("Terrain", true, false);
        if (terrain != null)
        {
            foreach (var child in terrain.GetChildren())
            {
                if (child is GraphNode node)
                {
                    _toolNodes.Add(node);
                }
            }
        }
    }

    public void Process(double delta)
    {
        // Update tool node visibility based on distance
        _toolCheckTimer += (float)delta;
        if (_toolCheckTimer >= 0.1f)
        {
            _toolCheckTimer = 0f;
            foreach (var node in _toolNodes)
            {
                if (node == null) continue;
                float distance = _character.GlobalTransform.Origin.DistanceTo(node.GlobalTransform.Origin);
                node.Visible = distance <= 40f;
            }
        }

        // Process current tool
        if (_tools.Count > 0)
            _tools[_currentToolIndex].OnProcess(delta);
    }

    public void HandleMouseButton(InputEventMouseButton mouseButton, GameItem heldItem)
    {
        if (heldItem != null) return; // Don't handle terrain tools if holding an item

        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed && _tools.Count > 0)
                _tools[_currentToolIndex].OnPrimaryAction();
            else if (!mouseButton.Pressed && _tools.Count > 0)
                _tools[_currentToolIndex].OnPrimaryRelease();
        }
        else if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (mouseButton.Pressed && _tools.Count > 0 && _tools[_currentToolIndex] is TerrainManipulationTools.ShovelTool shovel)
                shovel.OnSecondaryAction();
            else if (!mouseButton.Pressed && _tools.Count > 0 && _tools[_currentToolIndex] is TerrainManipulationTools.ShovelTool shovel2)
                shovel2.OnSecondaryRelease();
        }
    }

    private void UpdateToolLabel()
    {
        if (_toolLabel != null && _tools.Count > 0)
            _toolLabel.Text = $"Tool: {_tools[_currentToolIndex].Name}";
    }
}
*/
