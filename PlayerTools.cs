using Godot;
using System.Collections.Generic;

public static class PlayerTools
{
    public abstract class PlayerTool
    {
        public abstract string Name { get; }
        protected Character Character;
        public PlayerTool(Character character) { Character = character; }
        public virtual void OnPrimaryAction() { }
        public virtual void OnPrimaryRelease() { }
        public virtual void OnScroll(float delta) { }
        public virtual void OnProcess(double delta) { }
    }

    public class ShovelTool : PlayerTool
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

    public class CreateNodeTool : PlayerTool
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
            var pos = camera.GlobalTransform.Origin + camera.GlobalTransform.Basis.Z * -_distance;
            var node = new GraphNode();
            node.Position = pos;
            Character.GetTree().CurrentScene.AddChild(node);
            GD.Print($"[TOOL: CreateNode] Placed GraphNode at {pos}");
        }
    }

    public class LinkerTool : PlayerTool
    {
        public override string Name => "Linker";
        private GraphNode _startNode = null;
        private GraphNode _hoverNode = null;
        private MeshInstance3D _lineInstance = null;
        private bool _dragging = false;
        private Terrain _terrain;

        public LinkerTool(Character character) : base(character)
        {
            _terrain = character.GetTree().Root.FindChild("Terrain", true, false) as Terrain;
        }

        public override void OnPrimaryAction()
        {
            if (_dragging) return;
            var node = GetNodeInConeFromPlayer();
            if (node != null)
            {
                _startNode = node;
                _dragging = true;
                CreateDynamicLine(_startNode.Position, _startNode.Position + Vector3.Up * 0.1f);
            }
        }

        public override void OnPrimaryRelease()
        {
            if (!_dragging) return;
            // Only allow linking if _hoverNode is set (i.e., actually snapped)
            if (_hoverNode != null && _hoverNode != _startNode)
            {
                ConnectNodes(_startNode, _hoverNode);
                TryCreateTriangles(_startNode, _hoverNode);
            }
            _dragging = false;
            _startNode = null;
            _hoverNode = null;
            RemoveDynamicLine();
        }

        public override void OnProcess(double delta)
        {
            if (_dragging && _startNode != null)
            {
                var node = GetNodeInConeFromPlayer();
                bool snapped = false;
                Vector3 endPos = GetMouseWorldPosition();
                if (node != null && node != _startNode)
                {
                    _hoverNode = node;
                    endPos = node.Position;
                    snapped = true;
                }
                else
                {
                    _hoverNode = null;
                }
                UpdateDynamicLine(_startNode.Position, endPos, snapped);
            }
        }

        // Casts a cone of rays from the player camera to find a GraphNode (excluding _startNode)
        private GraphNode GetNodeInConeFromPlayer()
        {
            var camera = Character.GetNode<Camera3D>("Camera3D");
            var origin = camera.GlobalTransform.Origin;
            var forward = -camera.GlobalTransform.Basis.Z.Normalized();
            float coneAngle = 8.0f; // degrees
            int rayCount = 20;
            float maxDistance = 100.0f;
            for (int i = 0; i < rayCount; i++)
            {
                float t = (float)i / (rayCount - 1);
                float angle = (t - 0.5f) * coneAngle * Mathf.DegToRad(1);
                // Spread rays horizontally
                var dir = forward.Rotated(Vector3.Up, angle).Normalized();
                var to = origin + dir * maxDistance;
                var query = PhysicsRayQueryParameters3D.Create(origin, to);
                query.CollideWithAreas = false;
                query.CollideWithBodies = true;
                query.Exclude = new Godot.Collections.Array<Rid> { Character.GetRid() };
                var result = Character.GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    var collider = result["collider"].AsGodotObject();
                    Node nodeToCheck = collider as Node;
                    while (nodeToCheck != null)
                    {
                        if (nodeToCheck is GraphNode graphNode && graphNode != _startNode)
                            return graphNode;
                        nodeToCheck = nodeToCheck.GetParent();
                    }
                }
            }
            return null;
        }

        private Vector3 GetMouseWorldPosition()
        {
            var camera = Character.GetNode<Camera3D>("Camera3D");
            return camera.GlobalTransform.Origin + camera.GlobalTransform.Basis.Z * -5.0f;
        }

        private void CreateDynamicLine(Vector3 a, Vector3 b)
        {
            RemoveDynamicLine();
            if (_terrain != null)
                _lineInstance = _terrain.CreateDebugLine(a, b);
        }
        private void UpdateDynamicLine(Vector3 a, Vector3 b, bool snapped = false)
        {
            if (_lineInstance != null)
            {
                var cylinder = _lineInstance.Mesh as CylinderMesh;
                if (cylinder != null)
                    cylinder.Height = a.DistanceTo(b);
                _lineInstance.Position = (a + b) / 2;
                var direction = (b - a).Normalized();
                if (direction.LengthSquared() > 0.001f)
                {
                    var from = Vector3.Up;
                    var to = direction;
                    var dot = from.Dot(to);
                    if (Mathf.Abs(dot) < 0.999f)
                    {
                        var axis = from.Cross(to).Normalized();
                        var angle = Mathf.Acos(Mathf.Clamp(dot, -1.0f, 1.0f));
                        _lineInstance.Basis = new Basis(axis, angle);
                    }
                    else if (dot < -0.999f)
                    {
                        var perpendicular = Mathf.Abs(from.Dot(Vector3.Right)) < 0.9f ? Vector3.Right : Vector3.Forward;
                        _lineInstance.Basis = new Basis(perpendicular, Mathf.Pi);
                    }
                }
                // Visual feedback: change color if snapped
                var mat = _lineInstance.MaterialOverride as StandardMaterial3D;
                if (mat != null)
                {
                    mat.AlbedoColor = snapped ? Colors.Lime : Colors.HotPink;
                }
            }
        }
        private void RemoveDynamicLine()
        {
            if (_lineInstance != null && _lineInstance.GetParent() != null)
                _lineInstance.GetParent().RemoveChild(_lineInstance);
            _lineInstance = null;
        }
        private void ConnectNodes(GraphNode a, GraphNode b)
        {
            if (!a.Connections.Contains(b))
                a.Connections.Add(b);
            if (!b.Connections.Contains(a))
                b.Connections.Add(a);
        }
        private void TryCreateTriangles(GraphNode a, GraphNode b)
        {
            // Check for triangles involving a and b
            var nodes = new List<GraphNode> { a, b };
            foreach (var n1 in nodes)
            {
                foreach (var n2 in n1.Connections)
                {
                    if (n2 == a || n2 == b) continue;
                    foreach (var n3 in n2.Connections)
                    {
                        if ((n3 == a || n3 == b) && n3 != n1)
                        {
                            // Triangle: a, b, n2
                            var triangle = new List<GraphNode> { a, b, n2 };
                            // Ensure all are connected
                            if (a.Connections.Contains(n2) && b.Connections.Contains(n2))
                            {
                                // Check if a GroundMesh already exists for this triangle
                                if (!GroundMeshExists(a, b, n2))
                                {
                                    var mesh = new GroundMesh(a, b, n2);
                                    _terrain.AddChild(mesh);
                                }
                            }
                        }
                    }
                }
            }
        }
        private bool GroundMeshExists(GraphNode a, GraphNode b, GraphNode c)
        {
            foreach (var child in _terrain.GetChildren())
            {
                if (child is GroundMesh gm)
                {
                    var nodes = new HashSet<GraphNode> { gm.NodeA, gm.NodeB, gm.NodeC };
                    if (nodes.SetEquals(new[] { a, b, c }))
                        return true;
                }
            }
            return false;
        }
    }

    public class CastNodeTool : PlayerTool
    {
        public override string Name => "CastNode";
        public CastNodeTool(Character character) : base(character) { }

        public override void OnPrimaryAction()
        {
            var camera = Character.GetNode<Camera3D>("Camera3D");
            var origin = camera.GlobalTransform.Origin;
            var to = origin + camera.GlobalTransform.Basis.Z * -1000f;
            var query = PhysicsRayQueryParameters3D.Create(origin, to);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            query.Exclude = new Godot.Collections.Array<Rid> { Character.GetRid() };
            var result = Character.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (result.Count > 0)
            {
                var collider = result["collider"].AsGodotObject();
                if (collider is GroundMesh || (collider is Node n && n.GetParent() is GroundMesh))
                {
                    var hitPos = (Vector3)result["position"];
                    GroundMesh groundMesh = null;
                    if (collider is GroundMesh gm)
                        groundMesh = gm;
                    else if ((collider as Node)?.GetParent() is GroundMesh gm2)
                        groundMesh = gm2;
                    var terrain = groundMesh?.GetParent() as Terrain;
                    if (terrain != null && groundMesh != null)
                    {
                        var node = new GraphNode();
                        node.Position = hitPos;
                        terrain.AddChild(node); // Add to the correct terrain
                        terrain.CrackPanel(node, groundMesh);
                    }
                }
            }
        }
    }

    public static List<PlayerTool> GetDefaultTools(Character character)
    {
        return new List<PlayerTool> {
            new ShovelTool(character),
            new CreateNodeTool(character),
            new LinkerTool(character),
            new CastNodeTool(character)
        };
    }
}
