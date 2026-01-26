using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Topology;

/// <summary>
/// Static utility class for performing 2D Delaunay triangulation on 3D nodes
/// projected to the XZ plane. Uses Triangle.NET library for robust triangulation.
/// </summary>
public static class DelaunayTriangulator
{
    /// <summary>
    /// Performs Delaunay triangulation on GraphNodes projected to XZ plane and connects them
    /// </summary>
    /// <param name="nodes">List of GraphNodes to triangulate</param>
    /// <param name="clearExistingConnections">Whether to clear existing connections before triangulating</param>
    /// <returns>The same list of nodes, now connected via Delaunay triangulation</returns>
    public static List<GraphNode> TriangulateAndConnect(List<GraphNode> nodes, bool clearExistingConnections = true)
    {
        if (nodes == null || nodes.Count < 3)
        {
            GD.Print($"DelaunayTriangulator: Need at least 3 nodes for triangulation, got {nodes?.Count ?? 0}");
            return nodes ?? new List<GraphNode>();
        }

        // Clear existing connections if requested
        if (clearExistingConnections)
        {
            foreach (var node in nodes)
            {
                node.Connections.Clear();
            }
        }

        // Project to 2D and triangulate
        var triangles = Triangulate2D(nodes);
        
        // Track unique edges to avoid duplicate connections
        var edges = new HashSet<(int, int)>();
        
        // Collect all edges from triangles
        foreach (var triangle in triangles)
        {
            AddUniqueEdge(edges, triangle.A, triangle.B);
            AddUniqueEdge(edges, triangle.B, triangle.C);
            AddUniqueEdge(edges, triangle.C, triangle.A);
        }

        // Create connections based on unique edges only
        foreach (var (nodeA, nodeB) in edges)
        {
            // Only add if not already connected (avoid duplicates)
            if (!nodes[nodeA].Connections.Contains(nodes[nodeB]))
            {
                nodes[nodeA].Connections.Add(nodes[nodeB]);
            }
            if (!nodes[nodeB].Connections.Contains(nodes[nodeA]))
            {
                nodes[nodeB].Connections.Add(nodes[nodeA]);
            }
        }

        GD.Print($"DelaunayTriangulator: {triangles.Count} triangles, {edges.Count} edges created for {nodes.Count} nodes");
        return nodes;
    }

    /// <summary>
    /// Adds an edge to the set, ensuring smaller index comes first
    /// </summary>
    private static void AddUniqueEdge(HashSet<(int, int)> edges, int a, int b)
    {
        edges.Add(a < b ? (a, b) : (b, a));
    }

    /// <summary>
    /// Performs 2D Delaunay triangulation without connecting nodes (useful for analysis)
    /// Uses Triangle.NET library for robust and correct Delaunay triangulation
    /// </summary>
    /// <param name="nodes">List of GraphNodes to triangulate</param>
    /// <param name="quality">Quality options for mesh refinement (null for basic triangulation)</param>
    /// <returns>List of triangles as index triplets</returns>
    public static List<Triangle> Triangulate2D(List<GraphNode> nodes, TriangleNet.Meshing.ConstraintOptions quality = null)
    {
        if (nodes == null || nodes.Count < 3)
            return new List<Triangle>();

        try
        {
            // Create polygon from GraphNode positions (projected to XZ plane)
            var polygon = new Polygon();
            
            // Add vertices to polygon
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                // Project 3D position to 2D (XZ plane)
                polygon.Add(new Vertex(node.Position.X, node.Position.Z) { ID = i });
            }

            // Perform Delaunay triangulation with optional quality refinement
            TriangleNet.Mesh mesh;
            if (quality != null)
            {
                // Quality mesh refinement - creates more uniform triangles
                var options = new TriangleNet.Meshing.ConstraintOptions() 
                { 
                    ConformingDelaunay = true  // Ensures true Delaunay triangulation
                };
                var qualityOptions = new TriangleNet.Meshing.QualityOptions()
                {
                    MinimumAngle = 20.0,  // Minimum angle in degrees (20-30 works well)
                    MaximumArea = -1.0    // No maximum area constraint
                };
                mesh = (TriangleNet.Mesh)polygon.Triangulate(options, qualityOptions);
            }
            else
            {
                // Basic Delaunay triangulation
                mesh = (TriangleNet.Mesh)polygon.Triangulate();
            }

            // Convert Triangle.NET result to our Triangle structs
            var result = new List<Triangle>();
            foreach (var triangle in mesh.Triangles)
            {
                // Get vertex IDs from the triangle vertices array
                var v0 = triangle.GetVertex(0).ID;
                var v1 = triangle.GetVertex(1).ID;
                var v2 = triangle.GetVertex(2).ID;
                
                result.Add(new Triangle(v0, v1, v2));
            }

            GD.Print($"Triangle.NET: Created {result.Count} triangles from {nodes.Count} nodes");
            return result;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"DelaunayTriangulator error: {ex.Message}");
            
            // Fallback: create a simple triangulation for 3 nodes
            if (nodes.Count == 3)
            {
                return new List<Triangle> { new Triangle(0, 1, 2) };
            }
            
            return new List<Triangle>();
        }
    }

    /// <summary>
    /// Applies Lloyd's relaxation algorithm to improve mesh quality.
    /// Moves each point toward the centroid of its Voronoi cell, which tends to
    /// create more uniform triangles with better angles.
    /// </summary>
    /// <param name="nodes">List of GraphNodes to relax</param>
    /// <param name="iterations">Number of relaxation iterations</param>
    /// <param name="origin">Terrain origin (for boundary constraints)</param>
    /// <param name="size">Terrain size (for boundary constraints)</param>
    /// <returns>The same list of nodes with updated positions</returns>
    public static List<GraphNode> ApplyLloydsRelaxation(
        List<GraphNode> nodes, 
        int iterations, 
        Vector3 origin, 
        Vector3 size)
    {
        if (nodes == null || nodes.Count < 3 || iterations <= 0)
            return nodes;

        // Define bounds for clamping
        float minX = origin.X - size.X * 0.5f;
        float maxX = origin.X + size.X * 0.5f;
        float minZ = origin.Z - size.Z * 0.5f;
        float maxZ = origin.Z + size.Z * 0.5f;

        for (int iter = 0; iter < iterations; iter++)
        {
            // Triangulate current positions to get Voronoi diagram (dual of Delaunay)
            var triangles = Triangulate2D(nodes);
            
            // Build adjacency information for each node
            var nodeNeighbors = new List<HashSet<int>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                nodeNeighbors.Add(new HashSet<int>());
            }
            
            // Collect neighbors from triangles
            foreach (var tri in triangles)
            {
                nodeNeighbors[tri.A].Add(tri.B);
                nodeNeighbors[tri.A].Add(tri.C);
                nodeNeighbors[tri.B].Add(tri.A);
                nodeNeighbors[tri.B].Add(tri.C);
                nodeNeighbors[tri.C].Add(tri.A);
                nodeNeighbors[tri.C].Add(tri.B);
            }

            // Calculate new positions (centroid of neighbors approximates Voronoi cell centroid)
            var newPositions = new Vector3[nodes.Count];
            
            for (int i = 0; i < nodes.Count; i++)
            {
                var neighbors = nodeNeighbors[i];
                if (neighbors.Count == 0)
                {
                    newPositions[i] = nodes[i].Position;
                    continue;
                }

                // Calculate centroid of neighboring points
                Vector3 centroid = Vector3.Zero;
                foreach (var neighborIdx in neighbors)
                {
                    centroid += nodes[neighborIdx].Position;
                }
                centroid /= neighbors.Count;

                // Preserve Y coordinate (height) - only relax in XZ plane
                centroid.Y = nodes[i].Position.Y;

                // Clamp to bounds
                centroid.X = Mathf.Clamp(centroid.X, minX, maxX);
                centroid.Z = Mathf.Clamp(centroid.Z, minZ, maxZ);

                newPositions[i] = centroid;
            }

            // Update node positions
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Position = newPositions[i];
            }
        }

        GD.Print($"Lloyd's relaxation: {iterations} iterations completed for {nodes.Count} nodes");
        return nodes;
    }
}

/// <summary>
/// Represents a triangle by three node indices 
/// </summary>
public struct Triangle
{
    public int A { get; }
    public int B { get; }
    public int C { get; }
    
    public Triangle(int a, int b, int c)
    {
        A = a;
        B = b;
        C = c;
    }
    
    public override string ToString() => $"Triangle({A}, {B}, {C})";
}
