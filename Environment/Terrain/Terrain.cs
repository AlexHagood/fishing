using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
[Tool]
public partial class Terrain : Node3D
{
	[ExportToolButton("Generate")]
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
			// Don't delete the brush if it's a child of the terrain
			if (child is TerrainBrush)
				continue;

			RemoveChild(child);
			child.QueueFree();
		}
		nodes.Clear();
		_nodeCount = 0; // Reset node count

		// Regenerate terrain
		var generatedNodes = GenerateNodes(NodeCount, TerrainOrigin, TerrainSize, 0);
		
		// Apply Lloyd's relaxation to improve triangle quality
        if (RelaxationIterations > 0)
        {
            generatedNodes = DelaunayTriangulator.ApplyLloydsRelaxation(
                generatedNodes, 
                RelaxationIterations, 
                TerrainOrigin, 
                TerrainSize
            );
			GD.Print($"Applied {RelaxationIterations} iterations of Lloyd's relaxation.");
        }
        
        var triangulatedNodes = DelaunayTriangulateXZ(generatedNodes);
        ForEachTriangle(triangulatedNodes, (nodeA, nodeB, nodeC) =>
        {
            var mesh = new GroundMesh(nodeA, nodeB, nodeC);
            AddChild(mesh);
            
            // In editor mode, set owner so terrain persists to scene file for game mode
            // Important: Owner must be set AFTER AddChild and AFTER the GroundMesh has created its children
            if (Engine.IsEditorHint())
            {
                // We need to defer this to ensure the mesh instance and collision shape are created first
                CallDeferred(nameof(SetMeshOwner), mesh);
            }
        });

        CreateDebugLine(Vector3.Zero, Vector3.Up * 10);
        nodes.AddRange(triangulatedNodes);

        // Mark the scene as unsaved in the editor
        if (Engine.IsEditorHint())
        {
#if TOOLS
            Godot.EditorInterface.Singleton.MarkSceneAsUnsaved();
#endif
        }

		GD.Print($"Terrain regenerated: {triangulatedNodes.Count} nodes triangulated.");
    }

    private void SetMeshOwner(GroundMesh mesh)
    {
        if (!Engine.IsEditorHint() || !IsInsideTree())
            return;
            
        var editedSceneRoot = GetTree().EditedSceneRoot;
        if (editedSceneRoot == null)
            return;
            
        // Set owner for the GroundMesh itself
        mesh.Owner = editedSceneRoot;
        
        // DO NOT set owner for children (MeshInstance3D, CollisionShape3D)
        // They will be recreated in _Ready() and don't need to persist separately
    }

    public override void _Ready()
    {
        base._Ready();
        
        // Only regenerate in editor, never in game
        if (!Engine.IsEditorHint())
        {
			GD.Print("Terrain: Game mode - using pre-generated terrain from editor.");
            return;
        }

		GD.Print("Terrain: Editor mode. Use 'Click me!' button to generate terrain.");
    }

	[Export(PropertyHint.Range, "0,10")]
    public int RelaxationIterations = 2; // Number of Lloyd's relaxation iterations to improve triangle quality

    public List<GraphNode> GenerateNodes(int count, Vector3 startLocation, Vector3 spread, int seed = 0)
    {
        var generatedNodes = new List<GraphNode>();
        var rng = seed == 0 ? new Random() : new Random(seed);
        
        // Generate random points
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
            
            // Allow the node to be saved to the scene file
            if (Engine.IsEditorHint())
            {
                node.Owner = GetTree().EditedSceneRoot;
            }
            
            generatedNodes.Add(node);
        }
        
		GD.Print($"{count} nodes generated with random distribution at {startLocation} with spread {spread}.");
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

        // Create new triangles with correct winding order (counter-clockwise when viewed from above)
        // Original was (A, B, C), so we maintain the same winding direction
        GroundMesh groundMesh1 = new GroundMesh(groundMesh.NodeA, groundMesh.NodeB, node);
        GroundMesh groundMesh2 = new GroundMesh(groundMesh.NodeB, groundMesh.NodeC, node);
        GroundMesh groundMesh3 = new GroundMesh(groundMesh.NodeC, groundMesh.NodeA, node);

        AddChild(groundMesh1);
        AddChild(groundMesh2);
        AddChild(groundMesh3);

        // Set owners for persistence in editor
        if (Engine.IsEditorHint() && IsInsideTree())
        {
            var editedRoot = GetTree().EditedSceneRoot;
            if (editedRoot != null)
            {
                groundMesh1.Owner = editedRoot;
                groundMesh2.Owner = editedRoot;
                groundMesh3.Owner = editedRoot;
                
                // Also set owners for their children (MeshInstance, CollisionShape)
                SetOwnerRecursive(groundMesh1, editedRoot);
                SetOwnerRecursive(groundMesh2, editedRoot);
                SetOwnerRecursive(groundMesh3, editedRoot);
            }
        }

		GD.Print($"Panel Cracked: 3 new meshes created");
    }
    
    private void SetOwnerRecursive(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            SetOwnerRecursive(child, owner);
        }
    }

}
