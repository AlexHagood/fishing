using Godot;
using System;
using System.Collections.Generic;
[Tool]
public partial class Terrain : Node3D
{
    [ExportToolButton("Click me!")]
    public Callable ResetButton => Callable.From(Reset);

    private List<GraphNode> nodes;
    public enum NodeColor
    {
        Red,
        Green,
        Blue
    }
    private GraphNode lastNode;

    public Terrain()
    {
        nodes = new List<GraphNode>();
    }
    public async void Reset()
    {
        GD.Print("Resetting terrain...");
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
        }
        nodes.Clear();
        lastNode = null; // Reset the last node reference
        CreateNode(new Vector3(1, 0, 0));
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        CreateNode(new Vector3(-1, 0, 1));
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        CreateNode(new Vector3(0, 0, -1));
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        nodes[2].Connect(nodes[0]); // seed dont delete
        CreateNodeDisconnected(new Vector3(5, 0, 5));
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        CreateNodeDisconnected(new Vector3(3f, 1f, 7f));
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        CreateNodeDisconnected(new Vector3(-3, 0, 3));
    }

    public static MeshInstance3D DebugDrawCylinder(Vector3 start, Vector3 end)
    {
        var cylinder = new MeshInstance3D();
        var mesh = new CylinderMesh
        {
            TopRadius = 0.01f,
            BottomRadius = 0.01f,
            Height = start.DistanceTo(end),
            RadialSegments = 16,
            Rings = 1
        };
        cylinder.Mesh = mesh;
        cylinder.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.8f) // Pink
        };

        // Position: midpoint between start and end
        cylinder.Position = (start + end) * 0.5f;

        // Rotation: align cylinder's local Y axis with (end - start)
        Vector3 up = new Vector3(0, 1, 0);
        Vector3 dir = (end - start).Normalized();
        if (!up.IsEqualApprox(dir))
        {
            var axis = up.Cross(dir);
            float angle = Mathf.Acos(up.Dot(dir));
            if (axis.Length() > 0.0001f)
                cylinder.Rotation = new Basis(axis.Normalized(), angle).GetEuler();
        }

        return cylinder;

    }

    public GraphNode CreateNode(Vector3 position)
    {
        NodeColor color = GetNextColor();
        GD.Print($"Creating node at {position} with color {color}");
        var newNode = new GraphNode(position, color);
        nodes.Add(newNode);
        AddChild(newNode);
        newNode.Owner = GetTree().EditedSceneRoot;
        newNode.MeshInstance.Owner = newNode.GetTree().EditedSceneRoot;
        if (lastNode != null)
        {
  //          Connect the new node to the last node
            newNode.Connect(lastNode);
            GroundMesh groundMesh = newNode.Fill(); // Attempt to fill connections
            if (groundMesh != null)
            {
                AddChild(groundMesh);
                groundMesh.Owner = GetTree().EditedSceneRoot;
                groundMesh.MeshInstance.Owner = groundMesh.GetTree().EditedSceneRoot;
            }

        }
        lastNode = newNode;
        return newNode;
    }


    public GraphNode CreateNodeDisconnected(Vector3 position)
    {
        // Find the nearest node to the given position
        GraphNode nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var node in nodes)
        {
            float dist = node.Position.DistanceTo(position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = node;
            }
        }

        if (nearest == null)
            throw new InvalidOperationException("No nodes exist to connect to.");

        // Find the best third node among nearest's neighbors to form the least acute triangle
        GraphNode bestThird = null;
        float maxMinAngle = -1f;
        NodeColor color = NodeColor.Red; // Default fallback

        foreach (var neighbor in nearest.Connections)
        {
            // Only consider nodes of a different color than nearest
            if (neighbor.Color == nearest.Color)
                continue;

            // Try all possible colors for the new node that are different from nearest and neighbor
            foreach (NodeColor candidateColor in Enum.GetValues(typeof(NodeColor)))
            {
                if (candidateColor == nearest.Color || candidateColor == neighbor.Color)
                    continue;

                // Compute the angles of the triangle (position, nearest.Position, neighbor.Position)
                Vector3 a = position;
                Vector3 b = nearest.Position;
                Vector3 c = neighbor.Position;

                float angleA = (b - a).AngleTo(c - a);
                float angleB = (a - b).AngleTo(c - b);
                float angleC = (a - c).AngleTo(b - c);

                // Find the smallest angle in the triangle
                float minAngle = Mathf.Min(angleA, Mathf.Min(angleB, angleC));

                // We want the triangle with the largest minimum angle (least acute)
                if (minAngle > maxMinAngle)
                {
                    maxMinAngle = minAngle;
                    bestThird = neighbor;
                    color = candidateColor;
                }
            }
        }

        GraphNode secondNearest = bestThird;

        if (secondNearest == null)
            throw new InvalidOperationException("Could not find a suitable third node to form a triangle.");

        var newNode = new GraphNode(position, color);
        nodes.Add(newNode);
        AddChild(newNode);
        if (nearest != null)
            newNode.Connect(nearest); // Connect to the nearest node
        if (secondNearest != null)
            newNode.Connect(secondNearest); // Connect to the second nearest node
        GroundMesh ground = newNode.Fill(); // Attempt to fill connections
        if (ground != null)
        {
            AddChild(ground);
            ground.Owner = GetTree().EditedSceneRoot;
            ground.MeshInstance.Owner = ground.GetTree().EditedSceneRoot;
        }
        newNode.Owner = GetTree().EditedSceneRoot;
        newNode.MeshInstance.Owner = newNode.GetTree().EditedSceneRoot;
        lastNode = newNode;
        return newNode;
    }

    private NodeColor GetNextColor()
    {
        return lastNode?.Color switch
        {
            NodeColor.Red => NodeColor.Green,
            NodeColor.Green => NodeColor.Blue,
            NodeColor.Blue => NodeColor.Red,
            _ => NodeColor.Red
        };
    }


    public partial class GraphNode : Node3D
    {
        public NodeColor Color { get; set; }
        public List<GraphNode> Connections { get; private set; }
        public MeshInstance3D MeshInstance;

        public GraphNode()
        {
            Name = "test2";
            Connections = new List<GraphNode>();
            MeshInstance = new MeshInstance3D();
            MeshInstance.Mesh = new SphereMesh();
            MeshInstance.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1) // Default color
            };
            AddChild(MeshInstance);
        }

        public GraphNode(Vector3 position, NodeColor color)
        {
            Name = "test";
            Position = position;
            Color = color;
            Connections = new List<GraphNode>();
            MeshInstance = new MeshInstance3D();
            this.AddChild(MeshInstance);
            MeshInstance.Mesh = new SphereMesh()
            {
                Radius = 0.1f, // Set radius for the sphere mesh
                Height = 0.2f
            };

            MeshInstance.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color switch
                {
                    NodeColor.Red => new Color(1, 0, 0),
                    NodeColor.Green => new Color(0, 1, 0),
                    NodeColor.Blue => new Color(0, 0, 1),
                    _ => new Color(1, 1, 1)
                }
            };
        }
        public void Connect(GraphNode otherNode)
        {
            if (otherNode.Color != this.Color && !Connections.Contains(otherNode))
            {
                Connections.Add(otherNode);
                otherNode.Connections.Add(this);
                var debugcyl = Terrain.DebugDrawCylinder(this.Position, otherNode.Position);
                GetParent().AddChild(debugcyl);
                debugcyl.Owner = GetTree().EditedSceneRoot;
            }
        }

        public GroundMesh Fill()
        {
            GD.Print("Attempting fill");
            foreach (var neighbor in Connections)
            {
                if (neighbor.Color != this.Color)
                {
                    foreach (var n2 in neighbor.Connections)
                    {
                        if (n2.Color != this.Color)
                        {
                            Connections.Add(n2);
                            GD.Print("Connection made!");
                            var groundMesh = new GroundMesh(this, neighbor, n2);
                            return groundMesh;
                        }
                    }
                }
            }
            GD.Print("No fill found.");
            return null;
        }
    }

    public void ConnectNodes(GraphNode nodeA, GraphNode nodeB)
    {
        nodeA.Connect(nodeB);
    }

    public partial class GroundMesh : StaticBody3D
    {
        public MeshInstance3D MeshInstance;
        public GroundMesh(GraphNode A, GraphNode B, GraphNode C)
        {
            GD.Print("Creating mesh");
            var mesh = new ArrayMesh();
            var vertices = EnsureClockwise([A.Position, B.Position, C.Position]);
            var indices = new int[] { 0, 1, 2 };

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)ArrayMesh.ArrayType.Max); // Resize to match the maximum value of ArrayType
            arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices;
            arrays[(int)ArrayMesh.ArrayType.Index] = indices;

            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);


            MeshInstance = new MeshInstance3D();
            MeshInstance.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.5f, 0.5f, 0.5f) // Set a default color for the ground mesh
            };
            MeshInstance.Mesh = mesh;

            AddChild(MeshInstance);
        }

        public CollisionShape3D CreateCollisionShape()
        {
            // Assume the mesh is a triangle, so create a concave collision shape from its vertices
            var shape = new ConcavePolygonShape3D();
            var mesh = MeshInstance.Mesh as ArrayMesh;
            if (mesh == null)
            throw new InvalidOperationException("MeshInstance.Mesh is not an ArrayMesh.");

            // Get the vertices and indices from the mesh
            var arrays = mesh.SurfaceGetArrays(0);
            var vertices = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];


            // ConcavePolygonShape3D expects a flat array of Vector3s (every 3 is a triangle)
            var points = new Vector3[3];
            for (int i = 0; i < 3; i++)
                points[i] = (Vector3)vertices[i];

            shape.Data = points;

            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = shape;
            return collisionShape;
        }

        public override void _Ready()
        {
            var collider = CreateCollisionShape();
            AddChild(collider);
            collider.Owner = GetTree().EditedSceneRoot;
        }
        
        private Vector3[] EnsureClockwise(Vector3[] vertices)
        {
            if (vertices.Length != 3)
                throw new ArgumentException("Exactly 3 vertices are required.");

            // Calculate the normal using the right-hand rule
            Vector3 a = vertices[0];
            Vector3 b = vertices[1];
            Vector3 c = vertices[2];

            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 normal = ab.Cross(ac);

            // In Godot, +Y is up. If the normal points down, swap to make it clockwise (facing up)
            if (normal.Y > 0)
            {
                // Swap b and c
                var temp = vertices[1];
                vertices[1] = vertices[2];
                vertices[2] = temp;
            }
            return vertices;
        }
    }
}

