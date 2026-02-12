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
    /// Performs 2D Delaunay triangulation without connecting nodes (useful for analysis)
    /// Uses Triangle.NET library for robust and correct Delaunay triangulation
    /// </summary>
    /// <param name="nodes">List of GraphNodes to triangulate</param>
    /// <param name="quality">Quality options for mesh refinement (null for basic triangulation)</param>
    /// <returns>List of triangles as index triplets</returns>
    public static List<Triangle> Triangulate2D(List<Vector3> nodes, TriangleNet.Meshing.ConstraintOptions quality = null)
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
                polygon.Add(new Vertex(node.X, node.Z) { ID = i });
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

            // Convert Triangle.NET result to our Triangle structs with correct winding
            var result = new List<Triangle>();
            foreach (var triangle in mesh.Triangles)
            {
                // Get vertex IDs from the triangle vertices array
                var v0 = triangle.GetVertex(0).ID;
                var v1 = triangle.GetVertex(1).ID;
                var v2 = triangle.GetVertex(2).ID;
                
                // Get actual 3D positions to determine winding order
                var pos0 = nodes[v0];
                var pos1 = nodes[v1];
                var pos2 = nodes[v2];
                
                // Calculate cross product in XZ plane to determine winding
                // For Godot terrain (normal pointing up +Y), we want clockwise winding when viewed from above
                var edge1 = new Vector2(pos1.X - pos0.X, pos1.Z - pos0.Z);
                var edge2 = new Vector2(pos2.X - pos0.X, pos2.Z - pos0.Z);
                var cross = edge1.X * edge2.Y - edge1.Y * edge2.X;
                
                // If cross product is negative, triangle is counter-clockwise (viewed from above)
                // If positive, it's clockwise - which is what we want for upward-facing normals
                if (cross < 0)
                {
                    // Swap v1 and v2 to reverse winding to clockwise
                    result.Add(new Triangle(v0, v2, v1));
                }
                else
                {
                    result.Add(new Triangle(v0, v1, v2));
                }
            }

            Log($"Triangle.NET: Created {result.Count} triangles from {nodes.Count} nodes with correct winding");
            return result;
        }
        catch (Exception ex)
        {
            Error($"DelaunayTriangulator error: {ex.Message}");
            
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
    public static List<Vector3> ApplyLloydsRelaxation(
        List<Vector3> nodes, 
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
                    newPositions[i] = nodes[i];
                    continue;
                }

                // Calculate centroid of neighboring points
                Vector3 centroid = Vector3.Zero;
                foreach (var neighborIdx in neighbors)
                {
                    centroid += nodes[neighborIdx];
                }
                centroid /= neighbors.Count;

                // Preserve Y coordinate (height) - only relax in XZ plane
                centroid.Y = nodes[i].Y;

                // Clamp to bounds
                centroid.X = Mathf.Clamp(centroid.X, minX, maxX);
                centroid.Z = Mathf.Clamp(centroid.Z, minZ, maxZ);

                newPositions[i] = centroid;
            }

            // Update node positions
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i] = newPositions[i];
            }
        }

        Log($"Lloyd's relaxation: {iterations} iterations completed for {nodes.Count} nodes");
        return nodes;
    }
}


