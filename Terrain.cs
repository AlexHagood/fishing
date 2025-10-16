using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
[Tool]
public partial class Terrain : Node3D
{
    [ExportToolButton("Click me!")]
    public Callable ResetButton => Callable.From(Reset);

    private List<GraphNode> nodes;
    private int _nodeCount = 0;
    [Export]
    public Vector3 TerrainSize = new Vector3(40, 3, 40);
    [Export]
    public int NodeCount = 20;
    [Export]
    Vector3 TerrainOrigin = Vector3.Zero;

    public Terrain()
    {
        nodes = new List<GraphNode>();
    }



    public void Reset()
    {
        GD.Print("Resetting terrain...");
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
        }
        nodes.Clear();
        _nodeCount = 0; // Reset node count

        CreateDebugLine(Vector3.Zero, Vector3.Up * 10);


        GD.Print("Terrain reset complete - unified 3D Delaunay triangulation system ready!");
    }

    public override void _Ready()
    {
        base._Ready();
        var generatedNodes = GenerateNodes(NodeCount, TerrainOrigin, TerrainSize, 0);
        var triangulatedNodes = DelaunayTriangulateXZ(generatedNodes);
        ForEachTriangle(triangulatedNodes, (nodeA, nodeB, nodeC) =>
        {
            //CreateDebugLine(nodeA.Position, nodeB.Position);
            //CreateDebugLine(nodeB.Position, nodeC.Position);
            //CreateDebugLine(nodeC.Position, nodeA.Position);
            var mesh = new GroundMesh(nodeA, nodeB, nodeC);
            AddChild(mesh);
        });

        CreateDebugLine(new Vector3(0, 0, 0), new Vector3(0, 10, 0));

        // Now manually add to our terrain's node collection if desired
        nodes.AddRange(triangulatedNodes);

        GD.Print($"Ready complete: {triangulatedNodes.Count} nodes generated and triangulated.");
    }

    public List<GraphNode> GenerateNodes(int count, Vector3 startLocation, Vector3 spread, int seed = 0)
    {
        var generatedNodes = new List<GraphNode>();
        var rng = seed == 0 ? new Random() : new Random(seed);
        
        for (int i = 0; i < count; i++)
        {
            // Center the spread around the start location
            float x = (float)(startLocation.X + (rng.NextDouble() - 0.5) * spread.X);
            float y = (float)(startLocation.Y + (rng.NextDouble() - 0.5) * spread.Y);
            float z = (float)(startLocation.Z + (rng.NextDouble() - 0.5) * spread.Z);
            var node = new GraphNode
            {
                Name = $"Node_{_nodeCount++}",
                Position = new Vector3(x, y, z)
            };
            AddChild(node);
            generatedNodes.Add(node);
        }
        
        GD.Print($"{count} nodes generated centered at {startLocation} with spread {spread}.");
        return generatedNodes;
    }


    /// <summary>
    /// Performs Delaunay triangulation on nodes and connects them
    /// </summary>
    /// <param name="graphNodes">List of nodes to triangulate</param>
    /// <returns>The same list of nodes, now connected via Delaunay triangulation</returns>
    public List<GraphNode> DelaunayTriangulateXZ(List<GraphNode> graphNodes)
    {
        return DelaunayTriangulator.TriangulateAndConnect(graphNodes);
    }

    /// <summary>
    /// Iterates over every triangle based on the actual connections between nodes.
    /// Finds triangles by looking for sets of 3 nodes that are all connected to each other.
    /// Uses node indices for efficient duplicate detection.
    /// </summary>
    /// <param name="graphNodes">List of nodes with connections</param>
    /// <param name="action">Action to call for each triangle (nodeA, nodeB, nodeC)</param>
    public void ForEachTriangle(List<GraphNode> graphNodes, Action<GraphNode, GraphNode, GraphNode> action)
    {
        if (graphNodes == null || graphNodes.Count < 3)
        {
            GD.Print("ForEachTriangle: Need at least 3 nodes to form triangles");
            return;
        }

        // Use a HashSet of sorted index tuples for efficient duplicate detection
        var processedTriangles = new HashSet<(int, int, int)>();
        int triangleCount = 0;

        // Create a lookup dictionary for fast node index retrieval
        var nodeToIndex = new Dictionary<GraphNode, int>();
        for (int i = 0; i < graphNodes.Count; i++)
        {
            nodeToIndex[graphNodes[i]] = i;
        }

        // For each node, check all pairs of its connections to see if they form triangles
        for (int aIndex = 0; aIndex < graphNodes.Count; aIndex++)
        {
            var nodeA = graphNodes[aIndex];
            if (nodeA.Connections == null || nodeA.Connections.Count < 2)
                continue;

            // Check all pairs of connections from nodeA
            for (int i = 0; i < nodeA.Connections.Count; i++)
            {
                var nodeB = nodeA.Connections[i];
                if (nodeB == nodeA) continue;

                for (int j = i + 1; j < nodeA.Connections.Count; j++)
                {
                    var nodeC = nodeA.Connections[j];
                    if (nodeC == nodeA || nodeC == nodeB) continue;

                    // Check if nodeB and nodeC are also connected to each other
                    if (nodeB.Connections != null && nodeB.Connections.Contains(nodeC))
                    {
                        // Get indices and create sorted tuple for duplicate detection
                        var bIndex = nodeToIndex[nodeB];
                        var cIndex = nodeToIndex[nodeC];
                        
                        // Sort the indices to create a unique triangle key
                        var indices = new[] { aIndex, bIndex, cIndex };
                        Array.Sort(indices);
                        var triangleKey = (indices[0], indices[1], indices[2]);

                        // Check if we've already processed this triangle
                        if (!processedTriangles.Contains(triangleKey))
                        {
                            processedTriangles.Add(triangleKey);
                            action(nodeA, nodeB, nodeC);
                            triangleCount++;
                        }
                    }
                }
            }
        }

        GD.Print($"ForEachTriangle: Processed {triangleCount} triangles from node connections");
    }





    /// <summary>
    /// Creates a debug line as a pink cylinder between two 3D points. If addToScene is false, does not add to scene tree (for dynamic lines).
    /// </summary>
    /// <param name="pointA">Start point</param>
    /// <param name="pointB">End point</param>
    /// <param name="addToScene">If true, adds to scene tree. If false, caller manages it.</param>
    /// <returns>MeshInstance3D representing the debug line</returns>
    public MeshInstance3D CreateDebugLine(Vector3 pointA, Vector3 pointB, bool addToScene = true)
    {
        var meshInstance = new MeshInstance3D();
        
        // Create cylinder mesh with correct properties
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 0.01f;
        cylinder.BottomRadius = 0.01f;
        cylinder.Height = pointA.DistanceTo(pointB);
        
        meshInstance.Mesh = cylinder;
        
        // Create pink material
        var material = new StandardMaterial3D();
        material.AlbedoColor = Colors.HotPink;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.NoDepthTest = true;
        meshInstance.MaterialOverride = material;
        
        // Position at midpoint
        var midpoint = (pointA + pointB) / 2;
        meshInstance.Position = midpoint;
        
        // Calculate direction and rotation
        var direction = (pointB - pointA).Normalized();
        
        // Only rotate if we have a valid direction
        if (direction.LengthSquared() > 0.001f)
        {
            // Cylinder starts aligned with Y-axis, we want it aligned with our direction
            var from = Vector3.Up;
            var to = direction;
            
            // Check if vectors are nearly parallel
            var dot = from.Dot(to);
            if (Mathf.Abs(dot) < 0.999f)
            {
                // Vectors are not parallel, use cross product for rotation axis
                var axis = from.Cross(to).Normalized();
                var angle = Mathf.Acos(Mathf.Clamp(dot, -1.0f, 1.0f));
                meshInstance.Basis = new Basis(axis, angle);
            }
            else if (dot < -0.999f)
            {
                // Vectors are opposite, rotate 180 degrees around any perpendicular axis
                var perpendicular = Mathf.Abs(from.Dot(Vector3.Right)) < 0.9f ? Vector3.Right : Vector3.Forward;
                meshInstance.Basis = new Basis(perpendicular, Mathf.Pi);
            }
            // If dot > 0.999f, vectors are nearly the same, no rotation needed (identity basis)
        }
        
        if (addToScene)
            AddChild(meshInstance);
        
        
        return meshInstance;
    }

    public void CrackPanel(GraphNode node, GroundMesh groundMesh)
    {

        var line1 = CreateDebugLine(node.Position, groundMesh.NodeA.Position, true);
        var line2 = CreateDebugLine(node.Position, groundMesh.NodeB.Position, true);
        var line3 = CreateDebugLine(node.Position, groundMesh.NodeC.Position, true);

        groundMesh.NodeA.Connect(node);
        groundMesh.NodeB.Connect(node);
        groundMesh.NodeC.Connect(node);

        groundMesh.NodeA.RemoveGroundMeshReference(groundMesh);
        groundMesh.NodeB.RemoveGroundMeshReference(groundMesh);
        groundMesh.NodeC.RemoveGroundMeshReference(groundMesh);

        groundMesh.QueueFree(); // Remove the old mesh

        GroundMesh groundMesh1 = new GroundMesh(node, groundMesh.NodeA, groundMesh.NodeB);
        GroundMesh groundMesh2 = new GroundMesh(node, groundMesh.NodeB, groundMesh.NodeC);
        GroundMesh groundMesh3 = new GroundMesh(node, groundMesh.NodeC, groundMesh.NodeA);

        AddChild(groundMesh1);
        AddChild(groundMesh2);
        AddChild(groundMesh3);

        GD.Print($"Panel Cracked");
    }

}

