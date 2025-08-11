using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Static utility class for performing 2D Delaunay triangulation on 3D nodes
/// projected to the XZ plane. Uses the Bowyer-Watson algorithm.
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

        // Connect nodes based on triangulation
        foreach (var triangle in triangles)
        {
            var nodeA = nodes[triangle.A];
            var nodeB = nodes[triangle.B];
            var nodeC = nodes[triangle.C];

            // Create bidirectional connections for each edge
            ConnectBidirectional(nodeA, nodeB);
            ConnectBidirectional(nodeB, nodeC);
            ConnectBidirectional(nodeC, nodeA);
        }

        GD.Print($"DelaunayTriangulator: {triangles.Count} triangles created for {nodes.Count} nodes");
        return nodes;
    }

    /// <summary>
    /// Performs 2D Delaunay triangulation without connecting nodes (useful for analysis)
    /// </summary>
    /// <param name="nodes">List of GraphNodes to triangulate</param>
    /// <returns>List of triangles as index triplets</returns>
    public static List<Triangle> Triangulate2D(List<GraphNode> nodes)
    {
        if (nodes == null || nodes.Count < 3)
            return new List<Triangle>();

        // Project nodes to 2D points (XZ plane)
        var points = nodes.Select(n => new Vector2(n.Position.X, n.Position.Z)).ToList();
        
        // Bowyer-Watson algorithm
        var triangles = new List<(int, int, int)>();
        
        // Create super triangle
        var bounds = GetBounds(points);
        var superTriangle = CreateSuperTriangle(bounds);
        var allPoints = new List<Vector2>(points);
        allPoints.AddRange(superTriangle);
        
        // Initialize with super triangle
        triangles.Add((points.Count, points.Count + 1, points.Count + 2));
        
        // Add each point incrementally
        for (int i = 0; i < points.Count; i++)
        {
            var badTriangles = new List<(int, int, int)>();
            var polygon = new HashSet<(int, int)>(); // Use HashSet for O(1) operations
            
            // Find bad triangles
            foreach (var tri in triangles)
            {
                if (IsInCircumcircle(allPoints[i], allPoints[tri.Item1], allPoints[tri.Item2], allPoints[tri.Item3]))
                {
                    badTriangles.Add(tri);
                    
                    // Add edges to polygon boundary
                    AddEdgeToPolygon(polygon, tri.Item1, tri.Item2);
                    AddEdgeToPolygon(polygon, tri.Item2, tri.Item3);
                    AddEdgeToPolygon(polygon, tri.Item3, tri.Item1);
                }
            }
            
            // Remove bad triangles
            foreach (var badTri in badTriangles)
            {
                triangles.Remove(badTri);
            }
            
            // Create new triangles from polygon edges
            foreach (var edge in polygon)
            {
                triangles.Add((i, edge.Item1, edge.Item2));
            }
        }
        
        // Remove triangles containing super triangle vertices
        var validTriangles = triangles.Where(t => 
            t.Item1 < points.Count && t.Item2 < points.Count && t.Item3 < points.Count);
        
        // Convert to Triangle objects
        return validTriangles.Select(t => new Triangle(t.Item1, t.Item2, t.Item3)).ToList();
    }

    /// <summary>
    /// Creates bidirectional connection between two nodes if not already connected
    /// </summary>
    private static void ConnectBidirectional(GraphNode nodeA, GraphNode nodeB)
    {
        if (!nodeA.Connections.Contains(nodeB))
        {
            nodeA.Connect(nodeB);
        }
    }

    /// <summary>
    /// Gets the bounding box of 2D points
    /// </summary>
    private static (Vector2 min, Vector2 max) GetBounds(List<Vector2> points)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        
        foreach (var point in points)
        {
            if (point.X < min.X) min.X = point.X;
            if (point.Y < min.Y) min.Y = point.Y;
            if (point.X > max.X) max.X = point.X;
            if (point.Y > max.Y) max.Y = point.Y;
        }
        
        return (min, max);
    }
    
    /// <summary>
    /// Creates a super triangle that encompasses all points
    /// </summary>
    private static List<Vector2> CreateSuperTriangle((Vector2 min, Vector2 max) bounds)
    {
        var width = bounds.max.X - bounds.min.X;
        var height = bounds.max.Y - bounds.min.Y;
        var margin = Math.Max(width, height) * 2;
        
        return new List<Vector2>
        {
            new Vector2(bounds.min.X - margin, bounds.min.Y - margin),
            new Vector2(bounds.max.X + margin, bounds.min.Y - margin),
            new Vector2(bounds.min.X + width * 0.5f, bounds.max.Y + margin)
        };
    }
    
    /// <summary>
    /// Tests if a point is inside the circumcircle of a triangle
    /// </summary>
    private static bool IsInCircumcircle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        var ax = a.X - point.X;
        var ay = a.Y - point.Y;
        var bx = b.X - point.X;
        var by = b.Y - point.Y;
        var cx = c.X - point.X;
        var cy = c.Y - point.Y;
        
        var det = (ax * ax + ay * ay) * (bx * cy - by * cx) -
                  (bx * bx + by * by) * (ax * cy - ay * cx) +
                  (cx * cx + cy * cy) * (ax * by - ay * bx);
        
        return det > 0;
    }
    
    /// <summary>
    /// Adds or removes an edge from the polygon boundary (for hole detection)
    /// </summary>
    private static void AddEdgeToPolygon(HashSet<(int, int)> polygon, int a, int b)
    {
        var edge = (Math.Min(a, b), Math.Max(a, b));
        if (polygon.Contains(edge))
        {
            polygon.Remove(edge);
        }
        else
        {
            polygon.Add(edge);
        }
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
