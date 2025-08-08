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
    private GraphNode lastNode;
    private int nodeCount = 0;

    public Terrain()
    {
        nodes = new List<GraphNode>();
    }

    public override void _Ready()
    {
        // Only set runtime ownership when not in editor
        if (!Engine.IsEditorHint())
        {
            GD.Print("Setting runtime ownership for all terrain nodes...");

            // Set ownership for all GraphNode children and their MeshInstances
            foreach (Node child in GetChildren())
            {
                if (child is GraphNode graphNode)
                {
                    // Clear editor ownership and set runtime ownership
                    graphNode.Owner = null;
                    if (graphNode.MeshInstance != null)
                    {
                        graphNode.MeshInstance.Owner = null;
                        GD.Print($"Set runtime ownership for {graphNode.Name} and its MeshInstance");
                    }
                }
                else if (child is GroundMesh groundMesh)
                {
                    // Also handle GroundMesh ownership
                    groundMesh.Owner = null;
                    if (groundMesh.MeshInstance != null)
                    {
                        groundMesh.MeshInstance.Owner = null;
                    }
                }
            }

            GD.Print("Runtime ownership setup complete");
        }
    }

    public async void Reset()
    {
        GD.Print("Resetting terrain...");
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
        }
        nodes.Clear();
        triangles.Clear();
        lastNode = null; // Reset the last node reference
        nodeCount = 0; // Reset node count

        // Demonstrate unified node creation with Delaunay triangulation
        CreateNode(new Vector3(5, 0, 0));
        CreateNode(new Vector3(-5, 0, 5));
        CreateNode(new Vector3(0, 0, -5));

        // Continue with more nodes - all using the same unified method
        CreateNode(new Vector3(5, 0, 5));
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        CreateNode(new Vector3(3f, 1f, 7f));
        CreateNode(new Vector3(-3, 0, 3));

        GenerateRandomTerrain(10, Vector3.Left * 15, new Vector3(30, 2, 30));

        GD.Print("Terrain reset complete - unified 3D Delaunay triangulation system ready!");
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

    // Unified method for creating nodes using proper Delaunay triangulation
    public GraphNode CreateNode(Vector3 position)
    {
        GD.Print($"Creating node at {position}");

        // Step 1: Find all triangles whose circumcircle contains the new point (Bowyer-Watson)
        var badTriangles = new List<Triangle>();
        foreach (var triangle in triangles)
        {
            if (IsPointInCircumcircle(position, triangle))
            {
                badTriangles.Add(triangle);
                GD.Print($"Found bad triangle with vertices at {triangle.A.Position}, {triangle.B.Position}, {triangle.C.Position}");
            }
        }
        GD.Print($"Found {badTriangles.Count} bad triangles to remove");

        // Step 2: Find the boundary edges of the polygonal hole
        var boundaryEdges = FindBoundaryEdges(badTriangles);
        GD.Print($"Found {boundaryEdges.Count} boundary edges");

        // Step 3: Remove bad triangles from our triangle list
        triangles.RemoveAll(t => badTriangles.Contains(t));
        GD.Print($"Removed {badTriangles.Count} bad triangles, {triangles.Count} triangles remaining");

        // Step 4: Create the new node
        nodeCount++;
        var newNode = new GraphNode(position, $"Node{nodeCount}");
        nodes.Add(newNode);
        AddChild(newNode);

        // Step 5: Create new triangles from new point to boundary edges
        var validTriangles = new List<Triangle>();
        foreach (var edge in boundaryEdges)
        {
            var newTriangle = new Triangle(newNode, edge.Start, edge.End);
            // Validate triangle before adding
            if (IsValidTriangleAddition(newTriangle))
            {
                validTriangles.Add(newTriangle);
                GD.Print($"Created new triangle: {newNode.Name} -> {edge.Start.Name} -> {edge.End.Name}");
            }
            else
            {
                GD.Print($"Rejected invalid triangle: {newNode.Name} -> {edge.Start.Name} -> {edge.End.Name}");
            }
        }
        triangles.AddRange(validTriangles);

        // Step 6: For the first few nodes, create initial triangulation more carefully
        if (boundaryEdges.Count == 0)
        {
            if (nodes.Count == 2)
            {
                // Only one other node exists, can't form a triangle yet
                GD.Print("Only 2 nodes total, no triangle formed yet");
            }
            else if (nodes.Count == 3)
            {
                // First triangle with 3 nodes - validate it's not degenerate
                var otherNodes = nodes.Where(n => n != newNode).ToList();
                if (otherNodes.Count == 2)
                {
                    var triangle = new Triangle(newNode, otherNodes[0], otherNodes[1]);
                    // Check if triangle is valid (not degenerate)
                    if (triangle.CircumradiusSquared < float.MaxValue)
                    {
                        triangles.Add(triangle);
                        GD.Print("Created first triangle");
                    }
                }
            }
            else if (nodes.Count > 3)
            {
                // For subsequent nodes, use proper Delaunay insertion
                // Find existing triangles that might form a good connection
                var candidateTriangles = new List<Triangle>();

                // Try to form triangles with existing edges, but prioritize closer connections
                var eligibleEdges = new List<(Edge edge, float avgDistance)>();

                // Collect all existing edges with their average distance to new node
                for (int i = 0; i < nodes.Count - 1; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var nodeA = nodes[i];
                        var nodeB = nodes[j];

                        // Skip if one of them is the new node
                        if (nodeA == newNode || nodeB == newNode) continue;

                        // Check if this edge exists in current triangulation
                        bool edgeExists = triangles.Any(t => t.ContainsEdge(nodeA, nodeB));

                        if (edgeExists)
                        {
                            var edge = new Edge(nodeA, nodeB);
                            float avgDistance = (nodeA.Position.DistanceTo(position) + nodeB.Position.DistanceTo(position)) / 2.0f;
                            eligibleEdges.Add((edge, avgDistance));
                        }
                    }
                }

                // Sort by distance and only consider the closest edges to avoid long connections
                var sortedEdges = eligibleEdges.OrderBy(e => e.avgDistance).Take(3).ToList();
                GD.Print($"Considering {sortedEdges.Count} closest edges for triangulation");

                foreach (var (edge, distance) in sortedEdges)
                {
                    // Try to form a triangle with newNode
                    var testTriangle = new Triangle(newNode, edge.Start, edge.End);

                    // Validate this triangle doesn't intersect existing triangles improperly
                    if (IsValidTriangleAddition(testTriangle))
                    {
                        candidateTriangles.Add(testTriangle);
                        GD.Print($"Valid triangle found with edge {edge.Start.Name}-{edge.End.Name} (avg distance: {distance:F2})");
                    }
                    else
                    {
                        GD.Print($"Invalid triangle rejected with edge {edge.Start.Name}-{edge.End.Name} (avg distance: {distance:F2})");
                    }
                }

                // Add valid triangles
                triangles.AddRange(candidateTriangles);
                GD.Print($"Added {candidateTriangles.Count} triangles for node at boundary edge count 0");
            }
        }

        // Step 7: Update all node connections based on current triangulation
        UpdateNodeConnectionsFromTriangulation();

        // Step 8: Validate triangulation integrity - abort if invalid
        if (!ValidateTriangulation())
        {
            GD.PrintErr($"Triangulation validation failed for {newNode.Name}! Rolling back changes.");

            // Remove the new node and any triangles that were added
            nodes.Remove(newNode);
            RemoveChild(newNode);
            nodeCount--; // Rollback node count

            // Remove any triangles that include the new node
            triangles.RemoveAll(t => t.A == newNode || t.B == newNode || t.C == newNode);

            // Update connections and meshes to reflect rollback
            UpdateNodeConnectionsFromTriangulation();
            CreateGroundMeshesForNode(null); // Regenerate all meshes

            GD.PrintErr($"Rolled back {newNode.Name} due to triangulation failure");
            return null; // Return null to indicate failure
        }

        // Step 9: Create ground meshes from triangles that include this new node
        CreateGroundMeshesForNode(newNode);

        // Step 10: Set ownership for editor (only when in editor)
        if (Engine.IsEditorHint())
        {
            newNode.Owner = GetTree().EditedSceneRoot;
            newNode.MeshInstance.Owner = newNode.GetTree().EditedSceneRoot;
        }
        lastNode = newNode;
        return newNode;
    }

    // Updates all node connections based on the current triangulation
    private void UpdateNodeConnectionsFromTriangulation()
    {
        // Clear existing debug cylinders and connections
        var cylindersToRemove = new List<Node>();
        foreach (Node child in GetChildren())
        {
            if (child is MeshInstance3D meshInst && meshInst.Mesh is CylinderMesh)
                cylindersToRemove.Add(child);
        }
        foreach (var cylinder in cylindersToRemove)
        {
            RemoveChild(cylinder);
        }

        // Clear all node connections
        foreach (var node in nodes)
        {
            node.Connections.Clear();
        }

        // Rebuild connections from triangulation edges
        var addedEdges = new HashSet<string>();
        foreach (var triangle in triangles)
        {
            AddTriangleEdgeConnection(triangle.A, triangle.B, addedEdges);
            AddTriangleEdgeConnection(triangle.B, triangle.C, addedEdges);
            AddTriangleEdgeConnection(triangle.C, triangle.A, addedEdges);
        }
    }

    private void AddTriangleEdgeConnection(GraphNode nodeA, GraphNode nodeB, HashSet<string> addedEdges)
    {
        // Create a consistent edge key
        string edgeKey = nodeA.GetInstanceId() < nodeB.GetInstanceId()
            ? $"{nodeA.GetInstanceId()}-{nodeB.GetInstanceId()}"
            : $"{nodeB.GetInstanceId()}-{nodeA.GetInstanceId()}";

        if (!addedEdges.Contains(edgeKey))
        {
            addedEdges.Add(edgeKey);

            // Add bidirectional connection
            if (!nodeA.Connections.Contains(nodeB))
                nodeA.Connections.Add(nodeB);
            if (!nodeB.Connections.Contains(nodeA))
                nodeB.Connections.Add(nodeA);

            // Create debug visualization cylinder
            //var debugCyl = DebugDrawCylinder(nodeA.Position, nodeB.Position);
            //AddChild(debugCyl);
            //debugCyl.Owner = GetTree().EditedSceneRoot;
        }
    }

    // Creates ground meshes for triangles containing the specified node
    private void CreateGroundMeshesForNode(GraphNode node)
    {
        // Remove existing ground meshes to avoid duplicates
        var existingMeshes = new List<Node>();
        foreach (Node child in GetChildren())
        {
            if (child is GroundMesh)
                existingMeshes.Add(child);
        }
        foreach (var mesh in existingMeshes)
        {
            RemoveChild(mesh);
        }

        // Create ground meshes for all current triangles
        foreach (var triangle in triangles)
        {
            var groundMesh = new GroundMesh(triangle.A, triangle.B, triangle.C);
            AddChild(groundMesh);
            
            // Only set editor ownership when in editor
            if (Engine.IsEditorHint())
            {
                groundMesh.Owner = GetTree().EditedSceneRoot;
                groundMesh.MeshInstance.Owner = groundMesh.GetTree().EditedSceneRoot;
            }
        }
    }



    public void ConnectNodes(GraphNode nodeA, GraphNode nodeB)
    {
        nodeA.Connect(nodeB);
    }



    // Delaunay triangulation data structures
    public class Triangle
    {
        public GraphNode A, B, C;
        public Vector2 Circumcenter;
        public float CircumradiusSquared;

        public Triangle(GraphNode a, GraphNode b, GraphNode c)
        {
            A = a; B = b; C = c;
            CalculateCircumcircle();
        }

        private void CalculateCircumcircle()
        {
            // Convert to 2D for circumcircle calculation
            Vector2 a = new Vector2(A.Position.X, A.Position.Z);
            Vector2 b = new Vector2(B.Position.X, B.Position.Z);
            Vector2 c = new Vector2(C.Position.X, C.Position.Z);

            float d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            if (Mathf.Abs(d) < 0.0001f) // Degenerate triangle
            {
                Circumcenter = (a + b + c) / 3; // Use centroid
                CircumradiusSquared = float.MaxValue;
                return;
            }

            float ux = ((a.X * a.X + a.Y * a.Y) * (b.Y - c.Y) + (b.X * b.X + b.Y * b.Y) * (c.Y - a.Y) + (c.X * c.X + c.Y * c.Y) * (a.Y - b.Y)) / d;
            float uy = ((a.X * a.X + a.Y * a.Y) * (c.X - b.X) + (b.X * b.X + b.Y * b.Y) * (a.X - c.X) + (c.X * c.X + c.Y * c.Y) * (b.X - a.X)) / d;

            Circumcenter = new Vector2(ux, uy);
            CircumradiusSquared = Circumcenter.DistanceSquaredTo(a);
        }

        public bool ContainsInCircumcircle(Vector3 point)
        {
            Vector2 p = new Vector2(point.X, point.Z);
            float distanceSquared = Circumcenter.DistanceSquaredTo(p);

            // Use a slightly more generous epsilon for numerical stability
            return distanceSquared < CircumradiusSquared + 0.0001f;
        }

        public bool SharesVertex(GraphNode node)
        {
            return A == node || B == node || C == node;
        }

        public bool ContainsEdge(GraphNode n1, GraphNode n2)
        {
            return (A == n1 && B == n2) || (A == n2 && B == n1) ||
                   (B == n1 && C == n2) || (B == n2 && C == n1) ||
                   (C == n1 && A == n2) || (C == n2 && A == n1);
        }
    }

    public class Edge
    {
        public GraphNode Start, End;

        public Edge(GraphNode start, GraphNode end)
        {
            Start = start;
            End = end;
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge other)
                return (Start == other.Start && End == other.End) || (Start == other.End && End == other.Start);
            return false;
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() ^ End.GetHashCode();
        }
    }

    private List<Triangle> triangles = new List<Triangle>();

    private bool IsPointInCircumcircle(Vector3 point, Triangle triangle)
    {
        return triangle.ContainsInCircumcircle(point);
    }

    private List<Edge> FindBoundaryEdges(List<Triangle> badTriangles)
    {
        var edges = new List<Edge>();

        foreach (var triangle in badTriangles)
        {
            edges.Add(new Edge(triangle.A, triangle.B));
            edges.Add(new Edge(triangle.B, triangle.C));
            edges.Add(new Edge(triangle.C, triangle.A));
        }

        // Remove edges that appear twice (internal edges)
        var boundaryEdges = new List<Edge>();
        for (int i = 0; i < edges.Count; i++)
        {
            bool isShared = false;
            for (int j = 0; j < edges.Count; j++)
            {
                if (i != j && edges[i].Equals(edges[j]))
                {
                    isShared = true;
                    break;
                }
            }
            if (!isShared)
                boundaryEdges.Add(edges[i]);
        }

        return boundaryEdges;
    }

    // Creates a mesh from Delaunay triangulation for terrain generation
    public List<GroundMesh> DelaunayInterpolate(Vector3[] inputPoints)
    {
        // Save current state
        var originalNodes = new List<GraphNode>(nodes);
        var originalTriangles = new List<Triangle>(triangles);
        var originalNodeCount = nodeCount;
        var originalLastNode = lastNode;

        // Clear existing triangulation temporarily
        triangles.Clear();
        var meshes = new List<GroundMesh>();

        // Temporarily clear the main collections for isolated triangulation
        nodes.Clear();
        triangles.Clear();
        lastNode = null;
        nodeCount = 0; // Reset for isolated triangulation

        // Add each point using the unified CreateNode method
        foreach (var point in inputPoints)
        {
            CreateNode(point);
        }

        // Generate ground meshes from all triangles
        foreach (var triangle in triangles)
        {
            var groundMesh = new GroundMesh(triangle.A, triangle.B, triangle.C);
            meshes.Add(groundMesh);
        }

        // Restore original state completely
        nodes.Clear();
        nodes.AddRange(originalNodes);
        triangles.Clear();
        triangles.AddRange(originalTriangles);
        nodeCount = originalNodeCount;
        lastNode = originalLastNode;

        GD.Print($"DelaunayInterpolate: Generated {meshes.Count} isolated meshes, restored original state");
        return meshes;
    }

    // Utility method to generate terrain from random points and interpolate them properly
    public void GenerateRandomTerrain(int numPoints, Vector3 center, Vector3 size)
    {
        GD.Print($"Generating {numPoints} random terrain points and interpolating into existing terrain...");
        var random = new Random();

        // Step 1: Generate all random points first
        var randomPoints = new List<Vector3>();
        for (int i = 0; i < numPoints; i++)
        {
            float x = center.X + (float)(random.NextDouble() - 0.5) * size.X;
            float y = center.Y + (float)(random.NextDouble() - 0.5) * size.Y;
            float z = center.Z + (float)(random.NextDouble() - 0.5) * size.Z;
            randomPoints.Add(new Vector3(x, y, z));
        }

        GD.Print($"Generated {randomPoints.Count} random points, now interpolating...");

        // Step 2: Find the optimal insertion order based on proximity to existing terrain
        var sortedPoints = SortPointsByProximityToExistingTerrain(randomPoints);

        // Step 3: Add points one by one in optimal order
        foreach (var point in sortedPoints)
        {
            CreateNode(point);
        }

        GD.Print($"Successfully interpolated {numPoints} random terrain points into existing triangulation!");
    }

    // Sort points by their distance to the closest existing node for better interpolation
    private List<Vector3> SortPointsByProximityToExistingTerrain(List<Vector3> points)
    {
        if (nodes.Count == 0)
        {
            // No existing nodes, just return the points as-is
            return points;
        }

        // Calculate distance to closest existing node for each point
        var pointDistances = new List<(Vector3 point, float distance)>();

        foreach (var point in points)
        {
            float minDistance = float.MaxValue;
            foreach (var existingNode in nodes)
            {
                float distance = point.DistanceTo(existingNode.Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }
            pointDistances.Add((point, minDistance));
        }

        // Sort by distance (closest first) for gradual expansion from existing terrain
        var sortedPoints = pointDistances
            .OrderBy(pd => pd.distance)
            .Select(pd => pd.point)
            .ToList();

        GD.Print($"Sorted {points.Count} points by proximity to existing terrain");
        return sortedPoints;
    }

    // Validates that adding a triangle won't create intersecting edges
    private bool IsValidTriangleAddition(Triangle newTriangle)
    {
        // Check if triangle is too large (indicates unwanted long-distance connections)
        float maxEdgeLength = GetMaxEdgeLength(newTriangle);
        float avgExistingEdgeLength = GetAverageExistingEdgeLength();

        // Only apply edge length restriction if we have a reasonable number of triangles
        // and the average is meaningful (more than a small base case)
        if (triangles.Count > 3 && avgExistingEdgeLength > 0 && maxEdgeLength > avgExistingEdgeLength * 3.0f)
        {
            GD.Print($"Triangle rejected - edge too long ({maxEdgeLength:F2}) vs avg ({avgExistingEdgeLength:F2})");
            return false;
        }

        // Check if any edge of the new triangle intersects with existing triangle edges
        var newEdges = new[]
        {
            new Edge(newTriangle.A, newTriangle.B),
            new Edge(newTriangle.B, newTriangle.C),
            new Edge(newTriangle.C, newTriangle.A)
        };

        foreach (var existingTriangle in triangles)
        {
            var existingEdges = new[]
            {
                new Edge(existingTriangle.A, existingTriangle.B),
                new Edge(existingTriangle.B, existingTriangle.C),
                new Edge(existingTriangle.C, existingTriangle.A)
            };

            foreach (var newEdge in newEdges)
            {
                foreach (var existingEdge in existingEdges)
                {
                    // Skip if edges share a vertex (that's allowed)
                    if (newEdge.Start == existingEdge.Start || newEdge.Start == existingEdge.End ||
                        newEdge.End == existingEdge.Start || newEdge.End == existingEdge.End)
                        continue;

                    // Check for edge intersection
                    if (EdgesIntersect(newEdge, existingEdge))
                    {
                        GD.Print($"Triangle would create intersecting edges - rejected");
                        return false;
                    }
                }
            }
        }

        // Additional check: ensure the new triangle satisfies Delaunay property
        // (no other vertices lie inside its circumcircle)
        foreach (var node in nodes)
        {
            if (node != newTriangle.A && node != newTriangle.B && node != newTriangle.C)
            {
                if (newTriangle.ContainsInCircumcircle(node.Position))
                {
                    GD.Print($"Triangle fails Delaunay property - another node in circumcircle");
                    return false;
                }
            }
        }

        return true;
    }

    // Get the maximum edge length of a triangle
    private float GetMaxEdgeLength(Triangle triangle)
    {
        float edge1 = triangle.A.Position.DistanceTo(triangle.B.Position);
        float edge2 = triangle.B.Position.DistanceTo(triangle.C.Position);
        float edge3 = triangle.C.Position.DistanceTo(triangle.A.Position);

        return Mathf.Max(edge1, Mathf.Max(edge2, edge3));
    }

    // Calculate the average edge length of all existing triangles
    private float GetAverageExistingEdgeLength()
    {
        if (triangles.Count == 0) return 0;

        float totalLength = 0;
        int edgeCount = 0;

        foreach (var triangle in triangles)
        {
            totalLength += triangle.A.Position.DistanceTo(triangle.B.Position);
            totalLength += triangle.B.Position.DistanceTo(triangle.C.Position);
            totalLength += triangle.C.Position.DistanceTo(triangle.A.Position);
            edgeCount += 3;
        }

        return edgeCount > 0 ? totalLength / edgeCount : 0;
    }

    // Check if two edges intersect (not counting shared endpoints)
    private bool EdgesIntersect(Edge edge1, Edge edge2)
    {
        Vector2 p1 = new Vector2(edge1.Start.Position.X, edge1.Start.Position.Z);
        Vector2 q1 = new Vector2(edge1.End.Position.X, edge1.End.Position.Z);
        Vector2 p2 = new Vector2(edge2.Start.Position.X, edge2.Start.Position.Z);
        Vector2 q2 = new Vector2(edge2.End.Position.X, edge2.End.Position.Z);

        return DoLinesIntersect(p1, q1, p2, q2);
    }

    // Line segment intersection test
    private bool DoLinesIntersect(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
    {
        float d1 = CrossProduct(q2 - p2, p1 - p2);
        float d2 = CrossProduct(q2 - p2, q1 - p2);
        float d3 = CrossProduct(q1 - p1, p2 - p1);
        float d4 = CrossProduct(q1 - p1, q2 - p1);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        // Check for collinear cases
        if (Mathf.Abs(d1) < 0.0001f && OnSegment(p2, p1, q2)) return true;
        if (Mathf.Abs(d2) < 0.0001f && OnSegment(p2, q1, q2)) return true;
        if (Mathf.Abs(d3) < 0.0001f && OnSegment(p1, p2, q1)) return true;
        if (Mathf.Abs(d4) < 0.0001f && OnSegment(p1, q2, q1)) return true;

        return false;
    }

    private float CrossProduct(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private bool OnSegment(Vector2 p, Vector2 q, Vector2 r)
    {
        return q.X <= Mathf.Max(p.X, r.X) && q.X >= Mathf.Min(p.X, r.X) &&
               q.Y <= Mathf.Max(p.Y, r.Y) && q.Y >= Mathf.Min(p.Y, r.Y);
    }

    // Validate that the current triangulation has no intersecting edges
    public bool ValidateTriangulation()
    {
        var allEdges = new List<Edge>();

        // Collect all edges from all triangles
        foreach (var triangle in triangles)
        {
            allEdges.Add(new Edge(triangle.A, triangle.B));
            allEdges.Add(new Edge(triangle.B, triangle.C));
            allEdges.Add(new Edge(triangle.C, triangle.A));
        }

        // Check for intersections between all edge pairs
        for (int i = 0; i < allEdges.Count; i++)
        {
            for (int j = i + 1; j < allEdges.Count; j++)
            {
                var edge1 = allEdges[i];
                var edge2 = allEdges[j];

                // Skip if edges share a vertex
                if (edge1.Start == edge2.Start || edge1.Start == edge2.End ||
                    edge1.End == edge2.Start || edge1.End == edge2.End)
                    continue;

                if (EdgesIntersect(edge1, edge2))
                {
                    GD.PrintErr($"INTERSECTION FOUND: Edge {edge1.Start.Name}-{edge1.End.Name} intersects with {edge2.Start.Name}-{edge2.End.Name}");
                    return false;
                }
            }
        }

        GD.Print("Triangulation validation passed - no intersecting edges found");
        return true;
    }
}

