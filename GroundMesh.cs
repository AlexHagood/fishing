using Godot;
using System;

public partial class GroundMesh : StaticBody3D
{
    public MeshInstance3D MeshInstance;
    
    // References to the nodes that form this triangle
    public GraphNode NodeA { get; private set; }
    public GraphNode NodeB { get; private set; }
    public GraphNode NodeC { get; private set; }
    
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
        
        // Add this ground mesh to each node's reference list
        A.AddGroundMeshReference(this);
        B.AddGroundMeshReference(this);
        C.AddGroundMeshReference(this);
        
        CreateMeshFromNodes();
    }
    
    // Method to update the mesh when node positions change
    public void UpdateMesh()
    {
        if (NodeA != null && NodeB != null && NodeC != null)
        {
            CreateMeshFromNodes();
        }
    }
    
    // Helper method to create/update the mesh from current node positions
    private void CreateMeshFromNodes()
    {
        GD.Print($"Creating mesh for nodes at: {NodeA.Position}, {NodeB.Position}, {NodeC.Position}");
        var mesh = new ArrayMesh();
        var vertices = EnsureClockwise([NodeA.Position, NodeB.Position, NodeC.Position]);
        var indices = new int[] { 0, 1, 2 };

        // Generate proper UV coordinates for each vertex to prevent stretching
        var uvs = GenerateUVs(vertices);

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

    public CollisionShape3D CreateCollisionShape()
    {
        // Assume the mesh is a triangle, so create a concave collision shape from its vertices
        var shape = new ConcavePolygonShape3D();
        var mesh = MeshInstance.Mesh as ArrayMesh;
        if (mesh == null)
            throw new InvalidOperationException("MeshInstance.Mesh is not an ArrayMesh.");

        // Get the vertices and indices from the mesh
        var arrays = mesh.SurfaceGetArrays(0);
        var vertices = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];

        // ConcavePolygonShape3D expects a flat array of Vector3s (every 3 is a triangle)
        var points = new Vector3[3];
        for (int i = 0; i < 3; i++)
            points[i] = (Vector3)vertices[i];

        shape.Data = points;

        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = shape;
        return collisionShape;
    }

    public override void _Ready()
    {
        // Only create collision shape if MeshInstance exists (not the case for parameterless constructor)
        if (MeshInstance != null && MeshInstance.Mesh != null)
        {
            var collider = CreateCollisionShape();
            AddChild(collider);
            
            // Set owners for persistence in editor only - now that scene tree is available
            var editedSceneRoot = GetTree()?.EditedSceneRoot;
            if (editedSceneRoot != null)
            {
                collider.Owner = editedSceneRoot;
                MeshInstance.Owner = editedSceneRoot;
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

        // In Godot, +Y is up. If the normal points down, swap to make it clockwise (facing up)
        if (normal.Y > 0)
        {
            // Swap b and c
            var temp = vertices[1];
            vertices[1] = vertices[2];
            vertices[2] = temp;
        }
        return vertices;
    }
    
    // Generate UV coordinates based on world position to maintain consistent scale
    private Vector2[] GenerateUVs(Vector3[] vertices)
    {
        var uvs = new Vector2[vertices.Length];
        
        for (int i = 0; i < vertices.Length; i++)
        {
            // Use world XZ coordinates for UV mapping with consistent scale
            // Scale down by texture scale factor to match material settings
            float u = vertices[i].X / 2.0f; // Divide by the UV scale we set in material
            float v = vertices[i].Z / 2.0f;
            uvs[i] = new Vector2(u, v);
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
