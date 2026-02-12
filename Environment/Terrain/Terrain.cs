using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;


/// <summary>
/// Represents a triangle by three node indices 
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Triangle
{
    public readonly int A;
    public readonly int B;
    public readonly int C;
    
    public Triangle(int a, int b, int c)
    {
        A = a;
        B = b;
        C = c;
    }
    
    public override string ToString() => $"Triangle({A}, {B}, {C})";
}

public class Chunk 
{
	public int ChunkX;
	public int ChunkZ;
	public List<int> TriangleIndices = new List<int>(); // Indices into the Terrain.Triangles array
	
	public MeshInstance3D ChunkMesh;
	public StaticBody3D ChunkCollision;
	public Aabb Bounds;
}
[Tool]
public partial class Terrain : Node3D
{
	[ExportToolButton("Generate")]
	public Callable ResetButton => Callable.From(Reset);

	// === Core Data Structures ===
	

	public Vector3[] Vertices;

	public Triangle[] Triangles;

	public Dictionary<Vector2I, Chunk> ChunkMap = new Dictionary<Vector2I, Chunk>();

	public int[] Indices;
	
	// === Modification System ===
	private Dictionary<int, List<int>> _vertexToTriangles; // vertex index -> list of triangle indices
	private Dictionary<int, Vector2I> _triangleToChunk; // triangle index -> chunk coordinate
	
	// Cached data for regeneration
	private Vector3[] _cachedNormals;
	private Vector2[] _cachedUVs;
	private Vector3[] _cachedFaceNormals;
	
	// === Configuration ===
	[Export]
	public Vector3 TerrainSize = new Vector3(40, 3, 40);
	[Export]
	public int NodeCount = 20;
	[Export]
	Vector3 TerrainOrigin = Vector3.Zero;
	
	[Export]
	public float ChunkSize = 30.0f; // Size of each chunk in world units
	
	private Node3D _terrainElements;

	MeshInstance3D heightmapPreview;

	private static StandardMaterial3D _grassMaterial;
    private static StandardMaterial3D _rockMaterial;
	

	[Export]
	Curve HeightCurve; // Curve to control height distribution (X=normalized distance, Y=height multiplier)


	// Connect to position change signals for all nodes

	[Export(PropertyHint.Range, "0,10")]
	public int RelaxationIterations = 2; // Number of Lloyd's relaxation iterations to improve triangle quality
	
	[Export(PropertyHint.Range, "0,10")]
	public int HeightSmoothingIterations = 3; // Number of height smoothing passes
	
	[Export(PropertyHint.Range, "0,1")]
	public float HeightSmoothingStrength = 0.5f; // How much to blend with neighbors (0=none, 1=full average)
	
	[Export]
	public string HeightmapPath = "res://heightmap.png"; // Path to heightmap texture
	
	[Export]
	public float HeightmapScale = 50.0f; // Multiplier for heightmap values (black=0, white=HeightmapScale)
	
	private Image _heightmapImage;

	public override void _Ready()
	{
		base._Ready();

		heightmapPreview = GetNode<MeshInstance3D>("Heightmap");
		
		_grassMaterial = CreateGrassMaterial();
		_rockMaterial = CreateRockMaterial();
		
	}



	public void Reset()
	{
		Log("Resetting terrain...");

		heightmapPreview.Scale = new Vector3(TerrainSize.X, TerrainSize.Z, 0);
		
		
		Vertices = new Vector3[NodeCount];
	

		// Regenerate terrain
		var generatedPositions = GenerateNodes(NodeCount, TerrainOrigin, TerrainSize, 0);

		// Apply Lloyd's relaxation to improve triangle quality
		if (RelaxationIterations > 0)
		{
			generatedPositions = DelaunayTriangulator.ApplyLloydsRelaxation(
				generatedPositions, 
				RelaxationIterations, 
				TerrainOrigin, 
				TerrainSize
			);
			Log($"Applied {RelaxationIterations} iterations of Lloyd's relaxation.");
		}

		Vertices = generatedPositions.ToArray();
		Triangles = DelaunayTriangulator.Triangulate2D(generatedPositions).ToArray();

		int[] indices = new int[Triangles.Length * 3];
		for (int i = 0; i < Triangles.Length; i++)
		{
			indices[i*3 + 0] = Triangles[i].A;
			indices[i*3 + 1] = Triangles[i].B;
			indices[i*3 + 2] = Triangles[i].C;
		}

		Indices = indices;
		
		// === PARTITION INTO CHUNKS ===
		PartitionIntoChunks();
		
		// === BUILD MODIFICATION MAPPINGS ===
		BuildVertexToTriangleMap();

		// Calculate normals for proper lighting (shared across all chunks)
		_cachedNormals = new Vector3[Vertices.Length];
		_cachedFaceNormals = new Vector3[Triangles.Length];
		
		// Calculate face normals and accumulate at vertices
		for (int i = 0; i < Triangles.Length; i++)
		{
			var v0 = Vertices[Triangles[i].A];
			var v1 = Vertices[Triangles[i].B];
			var v2 = Vertices[Triangles[i].C];
			
			// Calculate face normal using cross product
			var edge1 = v1 - v0;
			var edge2 = v2 - v0;
			var faceNormal = -edge1.Cross(edge2).Normalized();
			
			// Store face normal for later use
			_cachedFaceNormals[i] = faceNormal;
			
			// Accumulate normal at each vertex
			_cachedNormals[Triangles[i].A] += faceNormal;
			_cachedNormals[Triangles[i].B] += faceNormal;
			_cachedNormals[Triangles[i].C] += faceNormal;
		}
		
		// Normalize accumulated normals (smooth shading)
		for (int i = 0; i < _cachedNormals.Length; i++)
		{
			_cachedNormals[i] = _cachedNormals[i].Normalized();
		}

		// Generate UV coordinates based on world-space XZ position for tiling (shared across all chunks)
		_cachedUVs = new Vector2[Vertices.Length];
		float uvScale = 0.1f; // Adjust this to control texture tiling (smaller = more tiles)
		
		for (int i = 0; i < Vertices.Length; i++)
		{
			// Use XZ position for UV coordinates
			_cachedUVs[i] = new Vector2(Vertices[i].X * uvScale, Vertices[i].Z * uvScale);
		}

		// Ensure we have a container to put generated terrain elements (debug lines, mesh, etc.)
		if (_terrainElements == null || !IsInstanceValid(_terrainElements))
		{
			_terrainElements = GetNodeOrNull<Node3D>("TerrainElements");
			if (_terrainElements == null)
			{
				_terrainElements = new Node3D();
				_terrainElements.Name = "TerrainElements";
				AddChild(_terrainElements);
				if (Engine.IsEditorHint())
				{
					_terrainElements.Owner = GetTree().EditedSceneRoot;
				}
			}
		}
		
		// Clear previous chunks
		foreach (Node child in _terrainElements.GetChildren())
		{
			child.QueueFree();
		}

		// === GENERATE MESH AND COLLISION PER CHUNK ===
		foreach (var kvp in ChunkMap)
		{
			var chunk = kvp.Value;
			GenerateChunkMeshAndCollision(chunk, _cachedNormals, _cachedUVs, _cachedFaceNormals);
		}
		
		Log($"Generated {ChunkMap.Count} chunk meshes and collisions");

		

 

		
		
		

		// Mark the scene as unsaved in the editor
		if (Engine.IsEditorHint())
		{
#if TOOLS
			Godot.EditorInterface.Singleton.MarkSceneAsUnsaved();
#endif
		}

		Log($"Terrain regenerated: {Vertices.Length} nodes, {Triangles.Length} triangles.");
	}

	
	


	public List<Vector3> GenerateNodes(int count, Vector3 startLocation, Vector3 spread, int seed = 0)
	{
		var generatedPositions = new List<Vector3>();
		var rng = seed == 0 ? new Random() : new Random(seed);
		
		var texture = GD.Load<Texture2D>(HeightmapPath);
		if (texture != null)
		{
			_heightmapImage = texture.GetImage();
			
			// Decompress the image if it's compressed so we can use GetPixel()
			if (_heightmapImage.IsCompressed())
			{
				_heightmapImage.Decompress();
				Log("Heightmap decompressed for pixel access");
			}
			
			Log($"Heightmap loaded: {_heightmapImage.GetWidth()}x{_heightmapImage.GetHeight()}");
		}
		else
		{
			Error($"Failed to load heightmap from: {HeightmapPath}");
		}
	
		
		// Generate random points
		for (int i = 0; i < count; i++)
		{
			// Center the spread around the start location
			float x = (float)(startLocation.X + (rng.NextDouble() - 0.5) * spread.X);
			float z = (float)(startLocation.Z + (rng.NextDouble() - 0.5) * spread.Z);

			// Sample heightmap for Y value
			float y = startLocation.Y;
			if (_heightmapImage != null)
			{
				// Normalize x,z to heightmap coordinates (0 to 1)
				float normalizedX = (x - (startLocation.X - spread.X / 2.0f)) / spread.X;
				float normalizedZ = (z - (startLocation.Z - spread.Z / 2.0f)) / spread.Z;
				
				// Clamp to valid range
				normalizedX = Mathf.Clamp(normalizedX, 0.0f, 1.0f);
				normalizedZ = Mathf.Clamp(normalizedZ, 0.0f, 1.0f);
				
				// Convert to pixel coordinates
				int pixelX = (int)(normalizedX * (_heightmapImage.GetWidth() - 1));
				int pixelY = (int)(normalizedZ * (_heightmapImage.GetHeight() - 1));
				
				// Sample the heightmap (grayscale value)
				Color pixelColor = _heightmapImage.GetPixel(pixelX, pixelY);
				float heightValue = pixelColor.R; // Use red channel for grayscale (0 to 1)
				
				// If HeightCurve is set, use it to map the heightmap value to height
				if (HeightCurve != null)
				{
					// Sample the curve: heightValue (0-1) is the X axis, curve output is multiplied by scale
					float curveValue = HeightCurve.Sample(heightValue);
					y = startLocation.Y + (curveValue * HeightmapScale);
				}
				else
				{
					// Fallback to direct linear mapping if no curve is set
					y = startLocation.Y + (heightValue * HeightmapScale);
				}
			}
			else
			{
				// Fallback to random Y if heightmap not available
				y = (float)(startLocation.Y + (rng.NextDouble() - 0.5) * spread.Y);
			}
			
			// Add the position directly to the list
			generatedPositions.Add(new Vector3(x, y, z));
		}
		
		Log($"{count} positions generated with random distribution at {startLocation} with spread {spread}.");
		return generatedPositions;
	}
	
	/// <summary>
	/// Creates a debug line as a pink cylinder between two 3D points. If addToScene is false, does not add to scene tree (for dynamic lines).
	/// </summary>
	/// <param name="pointA">Start point</param>
	/// <param name="pointB">End point</param>
	/// <param name="addToScene">If true, adds to scene tree. If false, caller manages it.</param>

	public void CreateDebugLine(Vector3 pointA, Vector3 pointB, bool addToScene = true)
	{
		DebugDraw3D.DrawArrow(pointA, pointB);
	}

	private static StandardMaterial3D CreateGrassMaterial()
    {
        var grassMaterial = new StandardMaterial3D();
        grassMaterial.AlbedoColor = new Color(0.3f, 0.6f, 0.2f, 1.0f);
        grassMaterial.Roughness = 0.9f;
        grassMaterial.Metallic = 0.0f;
        grassMaterial.AlbedoTexture = CreateNoiseTexture(42, 0.3f, 0.6f, 0.2f); // Green grass
        grassMaterial.Uv1Triplanar = true;
        grassMaterial.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
        
        return grassMaterial;
    }
    
    private static StandardMaterial3D CreateRockMaterial()
    {
        var rockMaterial = new StandardMaterial3D();
        rockMaterial.AlbedoColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
        rockMaterial.Roughness = 0.95f;
        rockMaterial.Metallic = 0.0f;
        rockMaterial.AlbedoTexture = CreateNoiseTexture(123, 0.4f, 0.4f, 0.45f); // Gray rock
        rockMaterial.Uv1Triplanar = true;
        rockMaterial.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
        
        return rockMaterial;
    }


	private static ImageTexture CreateNoiseTexture(int seed, float baseR, float baseG, float baseB)
    {
        const int textureSize = 32;
        var random = new Random(seed);
        
        // Create byte array for RGB8 format (3 bytes per pixel)
        byte[] data = new byte[textureSize * textureSize * 3];
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float variation = (float)(random.NextDouble() * 0.3 - 0.15);
                
                float r = Mathf.Clamp(baseR + variation, 0.0f, 1.0f);
                float g = Mathf.Clamp(baseG + variation, 0.0f, 1.0f);
                float b = Mathf.Clamp(baseB + variation, 0.0f, 1.0f);
                
                // Convert to byte values (0-255)
                int index = (y * textureSize + x) * 3;
                data[index + 0] = (byte)(r * 255);
                data[index + 1] = (byte)(g * 255);
                data[index + 2] = (byte)(b * 255);
            }
        }
        
        // Create image from byte array
        var image = Image.CreateFromData(textureSize, textureSize, false, Image.Format.Rgb8, data);
        
        return ImageTexture.CreateFromImage(image);
    }
    
	// === CHUNK SYSTEM ===
	
	/// <summary>
	/// Partitions all triangles into spatial chunks based on their centroid position
	/// </summary>
	private void PartitionIntoChunks()
	{
		ChunkMap.Clear();
		_triangleToChunk = new Dictionary<int, Vector2I>();
		
		// First pass: assign triangles to chunks based on centroid
		for (int i = 0; i < Triangles.Length; i++)
		{
			var tri = Triangles[i];
			
			// Calculate triangle centroid
			var v0 = Vertices[tri.A];
			var v1 = Vertices[tri.B];
			var v2 = Vertices[tri.C];
			var centroid = (v0 + v1 + v2) / 3.0f;
			
			// Determine which chunk this triangle belongs to
			var chunkCoord = GetChunkCoordinate(centroid);
			
			// Get or create chunk
			if (!ChunkMap.TryGetValue(chunkCoord, out var chunk))
			{
				chunk = new Chunk
				{
					ChunkX = chunkCoord.X,
					ChunkZ = chunkCoord.Y,
					TriangleIndices = new List<int>()
				};
				ChunkMap[chunkCoord] = chunk;
			}
			
			// Add triangle index to chunk
			chunk.TriangleIndices.Add(i);
			
			// Map triangle to chunk for fast lookup
			_triangleToChunk[i] = chunkCoord;
		}
		
		// Calculate bounds for each chunk
		foreach (var kvp in ChunkMap)
		{
			var chunk = kvp.Value;
			
			if (chunk.TriangleIndices.Count == 0)
				continue;
			
			// Initialize bounds with first vertex
			var firstTri = Triangles[chunk.TriangleIndices[0]];
			Vector3 min = Vertices[firstTri.A];
			Vector3 max = Vertices[firstTri.A];
			
			// Expand bounds to include all vertices in this chunk's triangles
			foreach (int triIndex in chunk.TriangleIndices)
			{
				var tri = Triangles[triIndex];
				
				min = min.Min(Vertices[tri.A]);
				min = min.Min(Vertices[tri.B]);
				min = min.Min(Vertices[tri.C]);
				
				max = max.Max(Vertices[tri.A]);
				max = max.Max(Vertices[tri.B]);
				max = max.Max(Vertices[tri.C]);
			}
			
			chunk.Bounds = new Aabb(min, max - min);
		}
		
		Log($"Partitioned {Triangles.Length} triangles into {ChunkMap.Count} chunks (chunk size: {ChunkSize})");
	}
	
	/// <summary>
	/// Converts a world position to chunk coordinates
	/// </summary>
	private Vector2I GetChunkCoordinate(Vector3 worldPos)
	{
		return new Vector2I(
			Mathf.FloorToInt((worldPos.X - TerrainOrigin.X) / ChunkSize),
			Mathf.FloorToInt((worldPos.Z - TerrainOrigin.Z) / ChunkSize)
		);
	}
	
	/// <summary>
	/// Generates mesh and collision for a single chunk
	/// </summary>
	private void GenerateChunkMeshAndCollision(Chunk chunk, Vector3[] normals, Vector2[] uvs, Vector3[] faceNormals)
	{
		if (chunk.TriangleIndices.Count == 0)
			return;
		
		// Ensure materials are initialized
		if (_grassMaterial == null)
			_grassMaterial = CreateGrassMaterial();
		if (_rockMaterial == null)
			_rockMaterial = CreateRockMaterial();
		
		// Separate triangles into grass and stone based on slope
		var grassIndices = new List<int>();
		var stoneIndices = new List<int>();
		
		float slopeThreshold = Mathf.Cos(Mathf.DegToRad(50)); // 50 degrees from vertical
		
		foreach (int triIndex in chunk.TriangleIndices)
		{
			var tri = Triangles[triIndex];
			var faceNormal = faceNormals[triIndex];
			
			// Check angle from up vector (Y axis)
			float dotWithUp = faceNormal.Dot(Vector3.Up);
			
			// If angle is greater than 50 degrees from up, use stone
			if (dotWithUp < slopeThreshold)
			{
				stoneIndices.Add(tri.A);
				stoneIndices.Add(tri.B);
				stoneIndices.Add(tri.C);
			}
			else
			{
				grassIndices.Add(tri.A);
				grassIndices.Add(tri.B);
				grassIndices.Add(tri.C);
			}
		}
		
		// Create mesh for this chunk
		ArrayMesh chunkMesh = new ArrayMesh();
		int surfaceIndex = 0;
		
		// Add grass surface
		if (grassIndices.Count > 0)
		{
			Godot.Collections.Array grassArrays = new Godot.Collections.Array();
			grassArrays.Resize((int)Mesh.ArrayType.Max);
			grassArrays[(int)Mesh.ArrayType.Vertex] = Vertices;
			grassArrays[(int)Mesh.ArrayType.Normal] = normals;
			grassArrays[(int)Mesh.ArrayType.TexUV] = uvs;
			grassArrays[(int)Mesh.ArrayType.Index] = grassIndices.ToArray();
			
			chunkMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, grassArrays);
			chunkMesh.SurfaceSetMaterial(surfaceIndex, _grassMaterial);
			surfaceIndex++;
		}
		
		// Add stone surface
		if (stoneIndices.Count > 0)
		{
			Godot.Collections.Array stoneArrays = new Godot.Collections.Array();
			stoneArrays.Resize((int)Mesh.ArrayType.Max);
			stoneArrays[(int)Mesh.ArrayType.Vertex] = Vertices;
			stoneArrays[(int)Mesh.ArrayType.Normal] = normals;
			stoneArrays[(int)Mesh.ArrayType.TexUV] = uvs;
			stoneArrays[(int)Mesh.ArrayType.Index] = stoneIndices.ToArray();
			
			chunkMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, stoneArrays);
			chunkMesh.SurfaceSetMaterial(surfaceIndex, _rockMaterial);
		}
		
		// Create StaticBody3D as the chunk container
		StaticBody3D chunkBody = new StaticBody3D();
		chunkBody.Name = $"Chunk_{chunk.ChunkX}_{chunk.ChunkZ}";
		_terrainElements.AddChild(chunkBody);
		
		if (Engine.IsEditorHint())
		{
			chunkBody.Owner = GetTree().EditedSceneRoot;
		}
		
		chunk.ChunkCollision = chunkBody;
		
		// Add MeshInstance3D as child of StaticBody3D
		MeshInstance3D chunkMeshInstance = new MeshInstance3D();
		chunkMeshInstance.Name = "ChunkMesh";
		chunkMeshInstance.Mesh = chunkMesh;
		chunkBody.AddChild(chunkMeshInstance);
		
		if (Engine.IsEditorHint())
		{
			chunkMeshInstance.Owner = GetTree().EditedSceneRoot;
		}
		
		chunk.ChunkMesh = chunkMeshInstance;
		
		// Add CollisionShape3D as child of StaticBody3D
		CollisionShape3D chunkCollider = new CollisionShape3D();
		chunkCollider.Name = "CollisionShape3D";
		
		// Build flat array of triangle vertices for this chunk
		Vector3[] chunkFaces = new Vector3[chunk.TriangleIndices.Count * 3];
		for (int i = 0; i < chunk.TriangleIndices.Count; i++)
		{
			int triIndex = chunk.TriangleIndices[i];
			var tri = Triangles[triIndex];
			
			chunkFaces[i * 3 + 0] = Vertices[tri.A];
			chunkFaces[i * 3 + 1] = Vertices[tri.B];
			chunkFaces[i * 3 + 2] = Vertices[tri.C];
		}
		
		ConcavePolygonShape3D chunkShape = new ConcavePolygonShape3D();
		chunkShape.SetFaces(chunkFaces);
		
		chunkCollider.Shape = chunkShape;
		chunkBody.AddChild(chunkCollider);
		
		if (Engine.IsEditorHint())
		{
			chunkCollider.Owner = GetTree().EditedSceneRoot;
		}
	}

	public int GetTriangleAtPosition(Vector3 worldPosition)
	{
	// Get the chunk this position is in - O(1)
		var chunkCoord = GetChunkCoordinate(worldPosition);

		if (!ChunkMap.TryGetValue(chunkCoord, out var chunk))
			return -1;

		// Only check triangles in this chunk - O(k) where k is small
		float closestDist = float.MaxValue;
		int closestTriangle = -1;

		foreach (int triIndex in chunk.TriangleIndices)
		{
			var tri = Triangles[triIndex];
			
			// Calculate triangle centroid
			var centroid = (Vertices[tri.A] + Vertices[tri.B] + Vertices[tri.C]) / 3.0f;
			float dist = worldPosition.DistanceSquaredTo(centroid);
			
			if (dist < closestDist)
			{
				closestDist = dist;
				closestTriangle = triIndex;
			}
		}

		return closestTriangle;
	}

	// === TERRAIN MODIFICATION SYSTEM ===
	
	/// <summary>
	/// Builds a mapping from vertex indices to the triangles that use them
	/// </summary>
	private void BuildVertexToTriangleMap()
	{
		_vertexToTriangles = new Dictionary<int, List<int>>();
		
		for (int i = 0; i < Triangles.Length; i++)
		{
			var tri = Triangles[i];
			
			if (!_vertexToTriangles.ContainsKey(tri.A))
				_vertexToTriangles[tri.A] = new List<int>();
			if (!_vertexToTriangles.ContainsKey(tri.B))
				_vertexToTriangles[tri.B] = new List<int>();
			if (!_vertexToTriangles.ContainsKey(tri.C))
				_vertexToTriangles[tri.C] = new List<int>();
				
			_vertexToTriangles[tri.A].Add(i);
			_vertexToTriangles[tri.B].Add(i);
			_vertexToTriangles[tri.C].Add(i);
		}
		
		Log($"Built vertex-to-triangle mapping: {_vertexToTriangles.Count} vertices");
	}
	
	/// <summary>
	/// Modifies a single vertex and regenerates affected chunks immediately
	/// </summary>
	public void ModifyVertex(int vertexIndex, Vector3 newPosition)
	{
		if (vertexIndex < 0 || vertexIndex >= Vertices.Length)
		{
			Error($"Invalid vertex index: {vertexIndex}");
			return;
		}
		
		// Update the vertex
		Vertices[vertexIndex] = newPosition;
		
		// Find all affected triangles
		if (!_vertexToTriangles.TryGetValue(vertexIndex, out var affectedTriangles))
		{
			Error($"No triangles found for vertex {vertexIndex}");
			return;
		}
		
		// Find all affected chunks
		var affectedChunks = new HashSet<Vector2I>();
		foreach (int triIndex in affectedTriangles)
		{
			if (_triangleToChunk.TryGetValue(triIndex, out var chunkCoord))
			{
				affectedChunks.Add(chunkCoord);
			}
		}
		
		// Recalculate normals for affected triangles
		RecalculateNormals(affectedTriangles);
		
		// Regenerate only affected chunks
		foreach (var chunkCoord in affectedChunks)
		{
			if (ChunkMap.TryGetValue(chunkCoord, out var chunk))
			{
				RegenerateChunk(chunk);
			}
		}
		
		Log($"Modified vertex {vertexIndex}, regenerated {affectedChunks.Count} chunks");
	}
	
	/// <summary>
	/// Modifies vertices within a radius using a modification function
	/// </summary>
	public void ModifyArea(Vector3 worldPosition, float radius, Func<Vector3, Vector3> modifyFunc)
	{
		var affectedChunks = new HashSet<Vector2I>();
		var affectedTriangles = new HashSet<int>();
		
		// Find vertices within radius
		for (int i = 0; i < Vertices.Length; i++)
		{
			if (Vertices[i].DistanceTo(worldPosition) <= radius)
			{
				// Apply modification
				Vertices[i] = modifyFunc(Vertices[i]);
				
				// Track affected triangles
				if (_vertexToTriangles.TryGetValue(i, out var tris))
				{
					foreach (int triIndex in tris)
					{
						affectedTriangles.Add(triIndex);
						
						if (_triangleToChunk.TryGetValue(triIndex, out var chunkCoord))
						{
							affectedChunks.Add(chunkCoord);
						}
					}
				}
			}
		}
		
		// Recalculate normals for affected triangles
		RecalculateNormals(affectedTriangles);
		
		// Regenerate affected chunks
		foreach (var chunkCoord in affectedChunks)
		{
			if (ChunkMap.TryGetValue(chunkCoord, out var chunk))
			{
				RegenerateChunk(chunk);
			}
		}
		
		Log($"Modified area at {worldPosition} (radius {radius}), regenerated {affectedChunks.Count} chunks");
	}
	
	/// <summary>
	/// Recalculates normals for specific triangles
	/// </summary>
	private void RecalculateNormals(IEnumerable<int> triangleIndices)
	{
		// Reset normals for affected vertices
		var affectedVertices = new HashSet<int>();
		foreach (int triIndex in triangleIndices)
		{
			if (triIndex >= 0 && triIndex < Triangles.Length)
			{
				var tri = Triangles[triIndex];
				affectedVertices.Add(tri.A);
				affectedVertices.Add(tri.B);
				affectedVertices.Add(tri.C);
			}
		}
		
		foreach (int vertIdx in affectedVertices)
		{
			_cachedNormals[vertIdx] = Vector3.Zero;
		}
		
		// Recalculate face normals and accumulate at vertices
		foreach (int triIndex in triangleIndices)
		{
			if (triIndex >= 0 && triIndex < Triangles.Length)
			{
				var tri = Triangles[triIndex];
				var v0 = Vertices[tri.A];
				var v1 = Vertices[tri.B];
				var v2 = Vertices[tri.C];
				
				var edge1 = v1 - v0;
				var edge2 = v2 - v0;
				var faceNormal = -edge1.Cross(edge2).Normalized();
				
				_cachedFaceNormals[triIndex] = faceNormal;
				
				_cachedNormals[tri.A] += faceNormal;
				_cachedNormals[tri.B] += faceNormal;
				_cachedNormals[tri.C] += faceNormal;
			}
		}
		
		// Normalize affected vertices
		foreach (int vertIdx in affectedVertices)
		{
			_cachedNormals[vertIdx] = _cachedNormals[vertIdx].Normalized();
		}
	}
	
	/// <summary>
	/// Regenerates a single chunk's mesh and collision
	/// </summary>
	private void RegenerateChunk(Chunk chunk)
	{
		// Destroy old nodes
		if (chunk.ChunkCollision != null && IsInstanceValid(chunk.ChunkCollision))
		{
			chunk.ChunkCollision.QueueFree();
		}
		
		// Regenerate with updated vertex data
		GenerateChunkMeshAndCollision(chunk, _cachedNormals, _cachedUVs, _cachedFaceNormals);
	}
	
	/// <summary>
	/// Gets the closest vertex to a world position from a specific triangle
	/// </summary>
	public int GetClosestVertexInTriangle(int triangleIndex, Vector3 worldPosition)
	{
		if (triangleIndex < 0 || triangleIndex >= Triangles.Length)
			return -1;
		
		var tri = Triangles[triangleIndex];
		
		float distA = Vertices[tri.A].DistanceSquaredTo(worldPosition);
		float distB = Vertices[tri.B].DistanceSquaredTo(worldPosition);
		float distC = Vertices[tri.C].DistanceSquaredTo(worldPosition);
		
		if (distA <= distB && distA <= distC)
			return tri.A;
		else if (distB <= distC)
			return tri.B;
		else
			return tri.C;
	}


}

