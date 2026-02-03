using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
[Tool]
public partial class Terrain : Node3D
{
	[ExportToolButton("Generate")]
	public Callable ResetButton => Callable.From(Reset);

	// === Core Data Structures ===
	
	// All nodes in the terrain
	private List<GraphNode> nodes;
		private int _nodeCount = 0;
	
	// Node connectivity (adjacency list)
	// Key: GraphNode, Value: Set of connected neighbor nodes
	private Dictionary<GraphNode, HashSet<GraphNode>> _edges = new();
	
	// Triangle registry
	// All ground meshes in the terrain
	private List<GroundMesh> _triangles = new();
	
	// Triangle to its three nodes
	// Key: GroundMesh, Value: tuple of (nodeA, nodeB, nodeC)
	private Dictionary<GroundMesh, (GraphNode a, GraphNode b, GraphNode c)> _triangleNodes = new();
	
	// Inverse index: which triangles use which node
	// Key: GraphNode, Value: Set of GroundMeshes that use this node as a vertex
	private Dictionary<GraphNode, HashSet<GroundMesh>> _nodeToTriangles = new();
	
	// === Configuration ===
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

	public GraphNode newNode(Vector3 position)
	{
		var node = new GraphNode
		{
			Id = _nodeCount++,
			Position = position
		};
		AddChild(node);
		
		// Allow the node to be saved to the scene file
		if (Engine.IsEditorHint())
		{
			node.Owner = GetTree().EditedSceneRoot;
		}
		
		nodes.Add(node);
		return node;
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
		ClearDataStructures(); // Clear centralized data structures

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
		
		// Get edges from triangulation (new system - doesn't modify nodes)
		var edges = DelaunayTriangulator.TriangulateAndGetEdges(generatedNodes);
		
		// Register edges in our centralized data structure
		foreach (var (nodeAIdx, nodeBIdx) in edges)
		{
			AddEdge(generatedNodes[nodeAIdx], generatedNodes[nodeBIdx]);
		}
		
		// Apply height smoothing to reduce spikiness
		if (HeightSmoothingIterations > 0)
		{
			SmoothTerrainHeights(generatedNodes, HeightSmoothingIterations, HeightSmoothingStrength);
			GD.Print($"Applied {HeightSmoothingIterations} iterations of height smoothing (strength: {HeightSmoothingStrength}).");
		}
		
		// Create triangles from edges
		ForEachTriangleFromEdges(generatedNodes, (nodeA, nodeB, nodeC) =>
		{
			var mesh = new GroundMesh(nodeA.Position, nodeB.Position, nodeC.Position);
			AddChild(mesh);
			
			// Register triangle in centralized data structure
			RegisterTriangle(mesh, nodeA, nodeB, nodeC);
			
			// In editor mode, set owner so terrain persists to scene file for game mode
			if (Engine.IsEditorHint())
			{
				CallDeferred(nameof(SetMeshOwner), mesh);
			}
		});

		CreateDebugLine(Vector3.Zero, Vector3.Up * 10);
		nodes.AddRange(generatedNodes);

		// Mark the scene as unsaved in the editor
		if (Engine.IsEditorHint())
		{
#if TOOLS
			Godot.EditorInterface.Singleton.MarkSceneAsUnsaved();
#endif
		}

		GD.Print($"Terrain regenerated: {generatedNodes.Count} nodes, {edges.Count} edges, {_triangles.Count} triangles.");
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
		
		// Connect to all GraphNode position change signals
		ConnectNodeSignals();
	}
	
	// Connect to position change signals for all nodes
	private void ConnectNodeSignals()
	{
		foreach (var node in nodes)
		{
			if (node != null && IsInstanceValid(node))
			{
				node.PositionChanged += OnNodePositionChanged;
			}
		}
	}
	
	// Handle when a node's position changes - PUBLIC so editor plugin can connect
	public void OnNodePositionChanged(GraphNode node)
	{
		// Update all triangles that use this node
		if (_nodeToTriangles.TryGetValue(node, out var triangles))
		{
			foreach (var triangle in triangles)
			{
				if (triangle != null && IsInstanceValid(triangle))
				{
					// Get the three nodes for this triangle
					if (_triangleNodes.TryGetValue(triangle, out var nodes))
					{
						triangle.UpdateMeshFromPositions(nodes.a.Position, nodes.b.Position, nodes.c.Position);
					}
				}
			}
		}
	}

	[Export(PropertyHint.Range, "0,10")]
	public int RelaxationIterations = 2; // Number of Lloyd's relaxation iterations to improve triangle quality
	
	[Export(PropertyHint.Range, "0,10")]
	public int HeightSmoothingIterations = 3; // Number of height smoothing passes
	
	[Export(PropertyHint.Range, "0,1")]
	public float HeightSmoothingStrength = 0.5f; // How much to blend with neighbors (0=none, 1=full average)

	public List<GraphNode> GenerateNodes(int count, Vector3 startLocation, Vector3 spread, int seed = 0)
	{
		var generatedNodes = new List<GraphNode>();
		var rng = seed == 0 ? new Random() : new Random(seed);
		var centerXZ = new Vector2(startLocation.X, startLocation.Z);
		
		// Generate random points
		for (int i = 0; i < count; i++)
		{
			// Center the spread around the start location
			float x = (float)(startLocation.X + (rng.NextDouble() - 0.5) * spread.X);
			float y = (float)(startLocation.Y + (rng.NextDouble() - 0.5) * spread.Y);
			float z = (float)(startLocation.Z + (rng.NextDouble() - 0.5) * spread.Z);

			// Calculate distance from center in XZ plane
			
			var posXZ = new Vector2(x, z);
			var distFromCenter = posXZ.DistanceTo(centerXZ);
			var maxDist = Mathf.Min(spread.X, spread.Z) / 2.0f;

			// If in the outer 50% (distance > 0.5 * maxDist), slope down in -Y direction
			if (distFromCenter > maxDist * 0.5f)
			{
				// Slope factor: 0 at 0.5*maxDist, 1 at maxDist
				float slopeT = Mathf.Clamp((distFromCenter - maxDist * 0.5f) / (maxDist * 0.5f), 0, 1);
				// Lower the y value in the negative Y direction
				y -= slopeT * spread.Y * 4f;
			}
			
			var node = new GraphNode
			{
				Name = $"Node_{_nodeCount}",
				Id = _nodeCount++,
				Position = new Vector3(x, y, z)
			};
			AddChild(node);
			
			// Connect to position change signal
			node.PositionChanged += OnNodePositionChanged;
			
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
	/// Smooths terrain heights by averaging each node's Y position with its neighbors.
	/// Requires edges to already be registered in _edges dictionary.
	/// </summary>
	/// <param name="nodes">List of nodes to smooth</param>
	/// <param name="iterations">Number of smoothing passes</param>
	/// <param name="strength">Blend factor (0=no change, 1=full average)</param>
	private void SmoothTerrainHeights(List<GraphNode> nodes, int iterations, float strength)
	{
		for (int iter = 0; iter < iterations; iter++)
		{
			// Store new heights temporarily to avoid order-dependent results
			var newHeights = new Dictionary<GraphNode, float>();
			
			foreach (var node in nodes)
			{
				// Get neighbors from edge dictionary
				if (!_edges.TryGetValue(node, out var neighbors) || neighbors.Count == 0)
				{
					newHeights[node] = node.Position.Y;
					continue;
				}
				
				// Calculate average height of neighbors
				float avgHeight = 0f;
				int validNeighbors = 0;
				
				foreach (var neighbor in neighbors)
				{
					if (neighbor != null && IsInstanceValid(neighbor))
					{
						avgHeight += neighbor.Position.Y;
						validNeighbors++;
					}
				}
				
				if (validNeighbors > 0)
				{
					avgHeight /= validNeighbors;
					
					// Blend current height with average based on strength
					float currentHeight = node.Position.Y;
					float smoothedHeight = Mathf.Lerp(currentHeight, avgHeight, strength);
					newHeights[node] = smoothedHeight;
				}
				else
				{
					newHeights[node] = node.Position.Y;
				}
			}
			
			// Apply new heights
			foreach (var node in nodes)
			{
				if (newHeights.TryGetValue(node, out float newHeight))
				{
					var pos = node.Position;
					pos.Y = newHeight;
					node.Position = pos;
				}
			}
		}
	}

	/// <summary>
	/// Iterates over every triangle based on centralized edge data.
	/// Finds triangles by looking for sets of 3 nodes that are all connected to each other.
	/// </summary>
	/// <param name="graphNodes">List of all nodes</param>
	/// <param name="action">Action to call for each triangle (nodeA, nodeB, nodeC)</param>
	public void ForEachTriangleFromEdges(List<GraphNode> graphNodes, Action<GraphNode, GraphNode, GraphNode> action)
	{
		if (graphNodes == null || graphNodes.Count < 3)
		{
			GD.Print("ForEachTriangleFromEdges: Need at least 3 nodes to form triangles");
			return;
		}

		// Use a HashSet of sorted node tuples for efficient duplicate detection
		var processedTriangles = new HashSet<(GraphNode, GraphNode, GraphNode)>();
		int triangleCount = 0;

		// For each node, check all pairs of its neighbors to see if they form triangles
		foreach (var nodeA in graphNodes)
		{
			var neighbors = GetNeighbors(nodeA).ToList();
			if (neighbors.Count < 2)
				continue;

			// Check all pairs of neighbors from nodeA
			for (int i = 0; i < neighbors.Count; i++)
			{
				var nodeB = neighbors[i];
				if (nodeB == nodeA) continue;

				for (int j = i + 1; j < neighbors.Count; j++)
				{
					var nodeC = neighbors[j];
					if (nodeC == nodeA || nodeC == nodeB) continue;

					// Check if nodeB and nodeC are also connected to each other
					if (_edges.TryGetValue(nodeB, out var nodeBNeighbors) && nodeBNeighbors.Contains(nodeC))
					{
						// Sort nodes to create unique triangle key
						var sortedNodes = new[] { nodeA, nodeB, nodeC }.OrderBy(n => n.Id).ToArray();
						var triangleKey = (sortedNodes[0], sortedNodes[1], sortedNodes[2]);

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

		GD.Print($"ForEachTriangleFromEdges: Processed {triangleCount} triangles from edge data");
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

	public void CrackPanel(GraphNode newNode, GroundMesh groundMesh)
	{
		// Get the three nodes that form this triangle from our data structure
		if (!_triangleNodes.TryGetValue(groundMesh, out var triangleNodes))
		{
			GD.PrintErr("CrackPanel: Ground mesh not found in triangle registry");
			return;
		}
		
		var nodeA = triangleNodes.a;
		var nodeB = triangleNodes.b;
		var nodeC = triangleNodes.c;
		
		// Create debug lines
		var line1 = CreateDebugLine(newNode.Position, nodeA.Position, true);
		var line2 = CreateDebugLine(newNode.Position, nodeB.Position, true);
		var line3 = CreateDebugLine(newNode.Position, nodeC.Position, true);

		// Add edges from new node to triangle corners
		AddEdge(newNode, nodeA);
		AddEdge(newNode, nodeB);
		AddEdge(newNode, nodeC);

		// Unregister the old triangle
		UnregisterTriangle(groundMesh);
		groundMesh.QueueFree();

		// Create new triangles with correct winding order
		GroundMesh groundMesh1 = new GroundMesh(nodeA.Position, nodeB.Position, newNode.Position);
		GroundMesh groundMesh2 = new GroundMesh(nodeB.Position, nodeC.Position, newNode.Position);
		GroundMesh groundMesh3 = new GroundMesh(nodeC.Position, nodeA.Position, newNode.Position);

		AddChild(groundMesh1);
		AddChild(groundMesh2);
		AddChild(groundMesh3);
		
		// Register new triangles
		RegisterTriangle(groundMesh1, nodeA, nodeB, newNode);
		RegisterTriangle(groundMesh2, nodeB, nodeC, newNode);
		RegisterTriangle(groundMesh3, nodeC, nodeA, newNode);

		// Set owners for persistence in editor
		if (Engine.IsEditorHint() && IsInsideTree())
		{
			var editedRoot = GetTree().EditedSceneRoot;
			if (editedRoot != null)
			{
				groundMesh1.Owner = editedRoot;
				groundMesh2.Owner = editedRoot;
				groundMesh3.Owner = editedRoot;
				
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

	// === Data Structure Helper Methods ===
	
	/// <summary>
	/// Add an edge (connection) between two nodes
	/// </summary>
	public void AddEdge(GraphNode a, GraphNode b)
	{
		if (!_edges.ContainsKey(a))
			_edges[a] = new HashSet<GraphNode>();
		if (!_edges.ContainsKey(b))
			_edges[b] = new HashSet<GraphNode>();
		
		_edges[a].Add(b);
		_edges[b].Add(a);
	}
	
	/// <summary>
	/// Get all neighbor nodes connected to the given node
	/// </summary>
	public IEnumerable<GraphNode> GetNeighbors(GraphNode node)
	{
		if (_edges.TryGetValue(node, out var neighbors))
			return neighbors;
		return Enumerable.Empty<GraphNode>();
	}
	
	/// <summary>
	/// Register a triangle mesh with its three vertex nodes
	/// </summary>
	public void RegisterTriangle(GroundMesh mesh, GraphNode a, GraphNode b, GraphNode c)
	{
		_triangles.Add(mesh);
		_triangleNodes[mesh] = (a, b, c);
		
		// Update inverse index
		if (!_nodeToTriangles.ContainsKey(a))
			_nodeToTriangles[a] = new HashSet<GroundMesh>();
		if (!_nodeToTriangles.ContainsKey(b))
			_nodeToTriangles[b] = new HashSet<GroundMesh>();
		if (!_nodeToTriangles.ContainsKey(c))
			_nodeToTriangles[c] = new HashSet<GroundMesh>();
		
		_nodeToTriangles[a].Add(mesh);
		_nodeToTriangles[b].Add(mesh);
		_nodeToTriangles[c].Add(mesh);
	}
	
	/// <summary>
	/// Get all triangles that use the given node as a vertex
	/// </summary>
	public IEnumerable<GroundMesh> GetTrianglesUsingNode(GraphNode node)
	{
		if (_nodeToTriangles.TryGetValue(node, out var triangles))
			return triangles;
		return Enumerable.Empty<GroundMesh>();
	}
	
	/// <summary>
	/// Get the three nodes that form a triangle
	/// </summary>
	public (GraphNode a, GraphNode b, GraphNode c)? GetTriangleNodes(GroundMesh mesh)
	{
		if (_triangleNodes.TryGetValue(mesh, out var nodes))
			return nodes;
		return null;
	}
	
	/// <summary>
	/// Get the next available node ID
	/// </summary>
	public int GetNextNodeId()
	{
		return _nodeCount++;
	}
	
	/// <summary>
	/// Unregister a triangle (e.g., when cracking a panel)
	/// </summary>
	public void UnregisterTriangle(GroundMesh mesh)
	{
		if (_triangleNodes.TryGetValue(mesh, out var nodes))
		{
			// Remove from node-to-triangle index
			if (_nodeToTriangles.ContainsKey(nodes.a))
				_nodeToTriangles[nodes.a].Remove(mesh);
			if (_nodeToTriangles.ContainsKey(nodes.b))
				_nodeToTriangles[nodes.b].Remove(mesh);
			if (_nodeToTriangles.ContainsKey(nodes.c))
				_nodeToTriangles[nodes.c].Remove(mesh);
		}
		
		_triangleNodes.Remove(mesh);
		_triangles.Remove(mesh);
	}
	
	/// <summary>
	/// Clear all data structures (useful for Reset)
	/// </summary>
	public void ClearDataStructures()
	{
		_edges.Clear();
		_triangles.Clear();
		_triangleNodes.Clear();
		_nodeToTriangles.Clear();
	}

}
