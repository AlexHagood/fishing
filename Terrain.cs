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
    private int nodeCount = 0;

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
        nodeCount = 0; // Reset node count

        CreateDebugLine(Vector3.Zero, Vector3.Up * 10);


        GD.Print("Terrain reset complete - unified 3D Delaunay triangulation system ready!");
    }


    public List<GraphNode> GenerateNodes(int count, Vector3 startLocation, Vector3 spread, int seed = 0)
    {
        var generatedNodes = new List<GraphNode>();
        var rng = seed == 0 ? new Random() : new Random(seed);
        
        for (int i = 0; i < count; i++)
        {
            float x = (float)(startLocation.X + rng.NextDouble() * spread.X);
            float y = (float)(startLocation.Y + rng.NextDouble() * spread.Y);
            float z = (float)(startLocation.Z + rng.NextDouble() * spread.Z);
            var node = new GraphNode
            {
                Name = $"Node_{nodeCount++}",
                Position = new Vector3(x, y, z)
            };
            AddChild(node);
            generatedNodes.Add(node);
        }
        
        GD.Print($"{count} nodes generated from {startLocation} with spread {spread}.");
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
    public void ForEachTriangle(List<GraphNode> graphNodes, Action<GraphNode, GraphNode, GraphNode> triangleAction)
    {
        var visited = new HashSet<(int, int, int)>();

        for (int i = 0; i < graphNodes.Count; i++)
        {
            var nodeA = graphNodes[i];
            foreach (var nodeB in nodeA.Connections)
            {
                if (nodeB == nodeA) continue;
                foreach (var nodeC in nodeB.Connections)
                {
                    if (nodeC == nodeA || nodeC == nodeB) continue;
                    if (nodeA.Connections.Contains(nodeC))
                    {
                        // Sort indices to avoid duplicates
                        var indices = new[] { graphNodes.IndexOf(nodeA), graphNodes.IndexOf(nodeB), graphNodes.IndexOf(nodeC) };
                        Array.Sort(indices);
                        var key = (indices[0], indices[1], indices[2]);
                        if (!visited.Contains(key))
                        {
                            visited.Add(key);
                            triangleAction(graphNodes[indices[0]], graphNodes[indices[1]], graphNodes[indices[2]]);
                        }
                    }
                }
            }
        }
    }

    public override void _Ready()
    {
        base._Ready();
        var generatedNodes = GenerateNodes(20, new Vector3(0, 0, 0), new Vector3(40, 5, 40), 0);
        var triangulatedNodes = DelaunayTriangulateXZ(generatedNodes);
        ForEachTriangle(triangulatedNodes, (nodeA, nodeB, nodeC) =>
        {
            var mesh = new GroundMesh(nodeA, nodeB, nodeC);
            AddChild(mesh);
        });

        CreateDebugLine(new Vector3(0, 0, 0), new Vector3(0, 10, 0));

        // Now manually add to our terrain's node collection if desired
        nodes.AddRange(triangulatedNodes);

        GD.Print($"Ready complete: {triangulatedNodes.Count} nodes generated and triangulated.");
    }

    /// <summary>
    /// Creates a debug line as a pink cylinder between two 3D points
    /// </summary>
    /// <param name="pointA">Start point</param>
    /// <param name="pointB">End point</param>
    /// <returns>MeshInstance3D representing the debug line</returns>
    public MeshInstance3D CreateDebugLine(Vector3 pointA, Vector3 pointB)
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
        
        // Add to scene
        AddChild(meshInstance);
        
        GD.Print($"Debug line created from {pointA} to {pointB} (length: {pointA.DistanceTo(pointB):F2})");
        
        return meshInstance;
    }

    /// <summary>
    /// Demonstrates different ways to use the DelaunayTriangulator
    /// </summary>
    public void DemoTriangulation()
    {
        // Generate some test nodes
        var testNodes = GenerateNodes(10, new Vector3(-10, 0, -10), new Vector3(20, 0, 20), 42);
        
        // Option 1: Simple triangulation (most common usage)
        var triangulatedNodes = DelaunayTriangulator.TriangulateAndConnect(testNodes);
        
        // Option 2: Get triangle data without connecting (for analysis)
        var triangles = DelaunayTriangulator.Triangulate2D(testNodes);
        GD.Print($"Generated {triangles.Count} triangles: {string.Join(", ", triangles)}");
        
        // Option 3: Triangulate without clearing existing connections
        DelaunayTriangulator.TriangulateAndConnect(testNodes, clearExistingConnections: false);
        
        // Visualize connections with debug lines
        foreach (var node in triangulatedNodes)
        {
            foreach (var connection in node.Connections)
            {
                CreateDebugLine(node.Position, connection.Position);
            }
        }
        
        GD.Print($"Demo complete: {triangulatedNodes.Count} nodes triangulated");
    }

}

