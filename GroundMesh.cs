using Godot;
using System;
using System.Linq;

[Tool]
public partial class GroundMesh : StaticBody3D
{
    public MeshInstance3D MeshInstance;
    
    // References to the nodes that form this triangle
    [Export] public GraphNode NodeA { get; set; }
    [Export] public GraphNode NodeB { get; set; }
    [Export] public GraphNode NodeC { get; set; }
    
    // Static grass material to avoid recreating for every instance
    private static StandardMaterial3D _grassMaterial;
    public static StandardMaterial3D GrassMaterial
    {
        get
        {
            if (_grassMaterial == null)
            {
                _grassMaterial = CreateGrassMaterial();
            }
            return _grassMaterial;
        }
    }
    
    // Parameterless constructor required by Godot
    public GroundMesh()
    {
        // Default constructor for Godot serialization
        // The actual mesh setup will be done in the parameterized constructor
    }

    public GroundMesh(GraphNode A, GraphNode B, GraphNode C)
    {
        // Store node references
        NodeA = A;
        NodeB = B;
        NodeC = C;
        
        // Position this GroundMesh at the triangle's centroid
        Position = (A.Position + B.Position + C.Position) / 3;
        
        // Add this ground mesh to each node's reference list
        A.AddGroundMeshReference(this);
        B.AddGroundMeshReference(this);
        C.AddGroundMeshReference(this);
        
        CreateMeshFromNodes();
    }
    
    // Efficient method to update mesh vertices without recreating the entire mesh
    public void UpdateMeshGeometry()
    {
        if (NodeA == null || NodeB == null || NodeC == null || MeshInstance == null)
            return;
            
        // Update position to triangle centroid
        Position = (NodeA.Position + NodeB.Position + NodeC.Position) / 3;
            
        // Get the current mesh
        var arrayMesh = MeshInstance.Mesh as ArrayMesh;
        if (arrayMesh == null)
        {
            // If mesh doesn't exist or is wrong type, recreate it
            CreateMeshFromNodes();
            return;
        }
        
        // Use positions relative to this GroundMesh's position
        var relativeA = NodeA.Position - Position;
        var relativeB = NodeB.Position - Position;
        var relativeC = NodeC.Position - Position;
        var vertices = EnsureClockwise([relativeA, relativeB, relativeC]);
        var indices = new int[] { 0, 1, 2 };
        // Use world positions for UV mapping, not relative positions  
        var worldPositions = new Vector3[] { NodeA.Position, NodeB.Position, NodeC.Position };
        var uvs = GenerateUVs(worldPositions);
        
        // Clear existing surfaces and add updated one
        arrayMesh.ClearSurfaces();
        
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices;
        arrays[(int)ArrayMesh.ArrayType.Index] = indices;
        arrays[(int)ArrayMesh.ArrayType.TexUV] = uvs;
        
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        
        // Update collision shape to match new geometry
        UpdateCollisionShape();
        
    }
    
    // Update collision shape to match new geometry
    private void UpdateCollisionShape()
    {
        if (NodeA == null || NodeB == null || NodeC == null)
            return;
            
        // Find existing collision shape
        var existingCollision = GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        
        if (existingCollision != null)
        {
            // Remove old collision shape - easier than trying to transform it
            RemoveChild(existingCollision);
            existingCollision.QueueFree();
        }
        
        // Create new collision shape with current triangle positions
        // Use positions relative to this GroundMesh's position
        var relativeA = NodeA.Position - Position;
        var relativeB = NodeB.Position - Position;
        var relativeC = NodeC.Position - Position;
        
        // Use the same winding order as the visual mesh for consistency
        var vertices = EnsureClockwise([relativeA, relativeB, relativeC]);
        
        // ConcavePolygonShape3D expects faces as a flat array where every 3 vertices form a triangle
        var faces = new Vector3[]
        {
            vertices[0], vertices[1], vertices[2]  // Single triangle face with correct winding
        };
        
        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces);
        
        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = shape;
        AddChild(collisionShape);
        
        // Set owner for editor persistence if needed
        if (Engine.IsEditorHint() && IsInsideTree())
        {
            var tree = GetTree();
            if (tree != null)
            {
                var editedSceneRoot = tree.EditedSceneRoot;
                if (editedSceneRoot != null)
                {
                    collisionShape.Owner = editedSceneRoot;
                }
            }
        }
        
    }
    
    // Helper method to create/update the mesh from current node positions
    private void CreateMeshFromNodes()
    {
        // Use positions relative to this GroundMesh's position
        var relativeA = NodeA.Position - Position;
        var relativeB = NodeB.Position - Position;
        var relativeC = NodeC.Position - Position;
        
        var mesh = new ArrayMesh();
        var vertices = EnsureClockwise([relativeA, relativeB, relativeC]);
        var indices = new int[] { 0, 1, 2 };

        // Generate proper UV coordinates for each vertex to prevent stretching
        // Use world positions for UV mapping, not relative positions
        var worldPositions = new Vector3[] { NodeA.Position, NodeB.Position, NodeC.Position };
        var uvs = GenerateUVs(worldPositions);

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max); // Resize to match the maximum value of ArrayType
        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices;
        arrays[(int)ArrayMesh.ArrayType.Index] = indices;
        arrays[(int)ArrayMesh.ArrayType.TexUV] = uvs; // Add UV coordinates

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        if (MeshInstance == null)
        {
            MeshInstance = new MeshInstance3D();
            MeshInstance.MaterialOverride = CreateGrassMaterial();
            MeshInstance.Visible = true; // Ensure visibility at runtime
            AddChild(MeshInstance);
            
            // Allow the mesh instance to be saved to the scene file
            if (Engine.IsEditorHint() && IsInsideTree())
            {
                var tree = GetTree();
                if (tree != null)
                {
                    MeshInstance.Owner = tree.EditedSceneRoot;
                }
            }
        }
        
        MeshInstance.Mesh = mesh;
    }

    public override void _ExitTree()
    {
        // Clean up references when this ground mesh is destroyed
        if (NodeA != null)
        {
            NodeA.RemoveGroundMeshReference(this);
        }
        if (NodeB != null)
        {
            NodeB.RemoveGroundMeshReference(this);
        }
        if (NodeC != null)
        {
            NodeC.RemoveGroundMeshReference(this);
        }
    }

    public override void _Ready()
    {
        // Find existing nodes if connections were lost
        if (MeshInstance == null)
        {
            MeshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
            // If still null, try finding by type
            if (MeshInstance == null)
            {
               foreach(var child in GetChildren())
               {
                   if (child is MeshInstance3D mi)
                   {
                       MeshInstance = mi;
                       break;
                   }
               }
            }
        }

        // Re-establish connections if they were lost during reload
        if (NodeA != null) NodeA.AddGroundMeshReference(this);
        if (NodeB != null) NodeB.AddGroundMeshReference(this);
        if (NodeC != null) NodeC.AddGroundMeshReference(this);

        // Only create collision shape if MeshInstance exists (not the case for parameterless constructor)
        if (MeshInstance != null && MeshInstance.Mesh != null)
        {
            // Use the new UpdateCollisionShape method for consistency
            UpdateCollisionShape();
            
            // Set owners for persistence in editor only - now that scene tree is available
            if (IsInsideTree())
            {
                var tree = GetTree();
                if (tree != null)
                {
                    var editedSceneRoot = tree.EditedSceneRoot;
                    if (editedSceneRoot != null)
                    {
                        MeshInstance.Owner = editedSceneRoot;
                    }
                }
            }
        }
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

        // In Godot, +Y is up. If the normal points DOWN, swap to make it point UP
        if (normal.Y > 0)
        {
            // Swap b and c to flip the winding order so normal points up
            var temp = vertices[1];
            vertices[1] = vertices[2];
            vertices[2] = temp;
        }
        return vertices;
    }
    
    // Generate UV coordinates based on world position to maintain consistent scale
    // Uses triplanar-style projection to prevent stretching on any triangle orientation
    private Vector2[] GenerateUVs(Vector3[] vertices)
    {
        var uvs = new Vector2[vertices.Length];
        
        // Calculate the triangle's normal to determine the best projection plane
        Vector3 edge1 = vertices[1] - vertices[0];
        Vector3 edge2 = vertices[2] - vertices[0];
        Vector3 normal = edge1.Cross(edge2).Normalized();
        
        // Determine which axis to use for projection based on the normal
        // Use the axis that the normal is most aligned with
        float absX = Mathf.Abs(normal.X);
        float absY = Mathf.Abs(normal.Y);
        float absZ = Mathf.Abs(normal.Z);
        
        // Define a consistent texture scale
        const float textureScale = 2.0f; // Controls texture tiling frequency
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 pos = vertices[i];
            
            // Project based on dominant normal direction to minimize distortion
            if (absY >= absX && absY >= absZ)
            {
                // Normal mostly pointing up/down - use XZ projection (most common for terrain)
                uvs[i] = new Vector2(pos.X / textureScale, pos.Z / textureScale);
            }
            else if (absX >= absY && absX >= absZ)
            {
                // Normal mostly pointing left/right - use YZ projection
                uvs[i] = new Vector2(pos.Y / textureScale, pos.Z / textureScale);
            }
            else
            {
                // Normal mostly pointing forward/back - use XY projection
                uvs[i] = new Vector2(pos.X / textureScale, pos.Y / textureScale);
            }
        }
        
        return uvs;
    }
    
    // Creates a simple, clean grass material that looks good at any scale
    private static StandardMaterial3D CreateGrassMaterial()
    {
        var grassMaterial = new StandardMaterial3D();
        
        // Simple, pleasant grass green
        grassMaterial.AlbedoColor = new Color(0.3f, 0.6f, 0.2f, 1.0f);
        
        // Natural matte finish
        grassMaterial.Roughness = 0.9f;
        grassMaterial.Metallic = 0.0f;
        
        // Simple noise texture for subtle variation
        grassMaterial.AlbedoTexture = CreateSimpleNoiseTexture();
        
        // Use regular UV mapping since we provide proper UVs
        grassMaterial.Uv1Triplanar = false;
        
        // Texture scale is handled in UV generation, so keep this at 1.0
        grassMaterial.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
        
        return grassMaterial;
    }
    
    // Creates a simple noise texture for subtle grass variation
    private static ImageTexture CreateSimpleNoiseTexture()
    {
        const int textureSize = 32; // Small texture, fast generation
        var image = Image.CreateEmpty(textureSize, textureSize, false, Image.Format.Rgb8);
        
        var random = new Random(42); // Fixed seed for consistency
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Simple random variation for grass color
                float variation = (float)(random.NextDouble() * 0.3 - 0.15); // -0.15 to +0.15
                
                // Base grass color with slight variation
                float r = Mathf.Clamp(0.3f + variation, 0.2f, 0.4f);
                float g = Mathf.Clamp(0.6f + variation, 0.5f, 0.7f);
                float b = Mathf.Clamp(0.2f + variation, 0.1f, 0.3f);
                
                image.SetPixel(x, y, new Color(r, g, b, 1.0f));
            }
        }
        
        return ImageTexture.CreateFromImage(image);
    }
    
    // Method to set up node references for existing ground meshes (used for scene file loading)
    public void SetupNodeReferences(GraphNode nodeA, GraphNode nodeB, GraphNode nodeC)
    {
        NodeA = nodeA;
        NodeB = nodeB;
        NodeC = nodeC;
        
        // Add this ground mesh to each node's reference list
        if (nodeA != null) nodeA.AddGroundMeshReference(this);
        if (nodeB != null) nodeB.AddGroundMeshReference(this);
        if (nodeC != null) nodeC.AddGroundMeshReference(this);
        
        GD.Print($"Set up node references for ground mesh: {nodeA?.Name}, {nodeB?.Name}, {nodeC?.Name}");
    }
}
