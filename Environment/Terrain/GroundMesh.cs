using Godot;
using System;
using System.Linq;

[Tool]
public partial class GroundMesh : StaticBody3D
{
    public MeshInstance3D MeshInstance;
    
    // Store vertex positions so mesh can be recreated in game mode
    [Export]
    private Vector3 _vertexA;
    [Export]
    private Vector3 _vertexB;
    [Export]
    private Vector3 _vertexC;
    
    // Slope-based material configuration
    [Export(PropertyHint.Range, "0,90")]
    private float _steepnessThreshold = 40.0f; // Angle in degrees where rock appears
    
    // Static materials to avoid recreating for every instance
    private static StandardMaterial3D _grassMaterial;
    private static StandardMaterial3D _rockMaterial;
    
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
    
    public static StandardMaterial3D RockMaterial
    {
        get
        {
            if (_rockMaterial == null)
            {
                _rockMaterial = CreateRockMaterial();
            }
            return _rockMaterial;
        }
    }
    
    // Parameterless constructor required by Godot
    public GroundMesh()
    {
        // Default constructor for Godot serialization
    }

    public GroundMesh(Vector3 posA, Vector3 posB, Vector3 posC)
    {
        UpdateMeshFromPositions(posA, posB, posC);
    }
    
    // Update mesh geometry from three vertex positions
    public void UpdateMeshFromPositions(Vector3 posA, Vector3 posB, Vector3 posC)
    {
        // Store positions so they persist to scene file
        _vertexA = posA;
        _vertexB = posB;
        _vertexC = posC;
        
        // Position this GroundMesh at the triangle's centroid
        Position = (posA + posB + posC) / 3;
        
        // Use positions relative to this GroundMesh's position
        var relativeA = posA - Position;
        var relativeB = posB - Position;
        var relativeC = posC - Position;
        
        // If MeshInstance doesn't exist yet, create it
        if (MeshInstance == null)
        {
            MeshInstance = new MeshInstance3D();
            MeshInstance.Visible = true;
            MeshInstance.Mesh = new ArrayMesh();
            AddChild(MeshInstance);
        }
        
        // Calculate triangle normal to determine steepness
        var edge1 = relativeB - relativeA;
        var edge2 = relativeC - relativeA;
        var normal = edge2.Cross(edge1).Normalized();
        
        // Ensure normal points upward (Y component should be positive)
        if (normal.Y < 0)
        {
            normal = -normal;
        }
        
        // Calculate angle from vertical (0° = flat horizontal, 90° = vertical wall)
        // Dot product with up vector: 1.0 = perfectly flat, 0.0 = vertical
        var dotProduct = Mathf.Clamp(normal.Dot(Vector3.Up), 0, 1);
        var angleFromVertical = Mathf.RadToDeg(Mathf.Acos(dotProduct));
        
        // Select material based on steepness
        if (angleFromVertical > _steepnessThreshold)
        {
            MeshInstance.MaterialOverride = RockMaterial;
        }
        else
        {
            MeshInstance.MaterialOverride = GrassMaterial;
        }
        
        // Update mesh geometry
        UpdateMeshGeometry(relativeA, relativeB, relativeC);
        
        // Update collision shape
        UpdateCollisionShape(relativeA, relativeB, relativeC);
    }
    
    // Build/update the mesh geometry from relative positions
    private void UpdateMeshGeometry(Vector3 relativeA, Vector3 relativeB, Vector3 relativeC)
    {
        var arrayMesh = MeshInstance.Mesh as ArrayMesh;
        if (arrayMesh == null)
        {
            arrayMesh = new ArrayMesh();
            MeshInstance.Mesh = arrayMesh;
        }
        
        var vertices = EnsureClockwise([relativeA, relativeB, relativeC]);
        var indices = new int[] { 0, 1, 2 };
        
        // Use world positions for UV mapping
        var worldPositions = new Vector3[] { 
            relativeA + Position, 
            relativeB + Position, 
            relativeC + Position 
        };
        var uvs = GenerateUVs(worldPositions);
        var normals = CalculateNormals(vertices);
        
        // Clear and rebuild mesh
        arrayMesh.ClearSurfaces();
        
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices;
        arrays[(int)ArrayMesh.ArrayType.Index] = indices;
        arrays[(int)ArrayMesh.ArrayType.Normal] = normals;
        arrays[(int)ArrayMesh.ArrayType.TexUV] = uvs;
        
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }
    
    // Update collision shape to match new geometry
    private void UpdateCollisionShape(Vector3 relativeA, Vector3 relativeB, Vector3 relativeC)
    {
        // Find existing collision shape
        var existingCollision = GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        
        if (existingCollision != null)
        {
            RemoveChild(existingCollision);
            existingCollision.QueueFree();
        }
        
        // Use the same winding order as the visual mesh
        var vertices = EnsureClockwise([relativeA, relativeB, relativeC]);
        
        // ConcavePolygonShape3D expects faces as a flat array
        var faces = new Vector3[] { vertices[0], vertices[1], vertices[2] };
        
        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces);
        
        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = shape;
        AddChild(collisionShape);
    }

    public override void _ExitTree()
    {
        // Cleanup handled by Terrain's data structures
    }

    public override void _Ready()
    {
        // Recreate mesh from stored positions (works in both editor and game mode)
        if (_vertexA != Vector3.Zero || _vertexB != Vector3.Zero || _vertexC != Vector3.Zero)
        {
            UpdateMeshFromPositions(_vertexA, _vertexB, _vertexC);
        }
    }
    
    private Vector3[] EnsureClockwise(Vector3[] vertices)
    {
        if (vertices.Length != 3)
            throw new ArgumentException("Exactly 3 vertices are required.");

        Vector3 a = vertices[0];
        Vector3 b = vertices[1];
        Vector3 c = vertices[2];

        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 normal = ab.Cross(ac);

        if (normal.Y > 0)
        {
            var temp = vertices[1];
            vertices[1] = vertices[2];
            vertices[2] = temp;
        }
        return vertices;
    }
    
    private Vector3 CalculateNormal(Vector3[] vertices)
    {
        if (vertices.Length != 3)
            throw new ArgumentException("Exactly 3 vertices are required.");

        Vector3 edge1 = vertices[1] - vertices[0];
        Vector3 edge2 = vertices[2] - vertices[0];
        return edge2.Cross(edge1).Normalized();
    }
    
    private Vector3[] CalculateNormals(Vector3[] vertices)
    {
        Vector3 normal = CalculateNormal(vertices);
        return new Vector3[] { normal, normal, normal };
    }
    
    private Vector2[] GenerateUVs(Vector3[] vertices)
    {
        var uvs = new Vector2[vertices.Length];
        Vector3 normal = CalculateNormal(vertices);

        normal = normal.Abs();
        
        const float textureScale = 2.0f;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 pos = vertices[i];
            
            if (normal.Y >= normal.X && normal.Y >= normal.Z)
            {
                uvs[i] = new Vector2(pos.X / textureScale, pos.Z / textureScale);
            }
            else if (normal.X >= normal.Y && normal.X >= normal.Z)
            {
                uvs[i] = new Vector2(pos.Y / textureScale, pos.Z / textureScale);
            }
            else
            {
                uvs[i] = new Vector2(pos.X / textureScale, pos.Y / textureScale);
            }
        }
        
        return uvs;
    }
    
    private static StandardMaterial3D CreateGrassMaterial()
    {
        var grassMaterial = new StandardMaterial3D();
        grassMaterial.AlbedoColor = new Color(0.3f, 0.6f, 0.2f, 1.0f);
        grassMaterial.Roughness = 0.9f;
        grassMaterial.Metallic = 0.0f;
        grassMaterial.AlbedoTexture = CreateNoiseTexture(42, 0.3f, 0.6f, 0.2f); // Green grass
        grassMaterial.Uv1Triplanar = false;
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
        rockMaterial.Uv1Triplanar = false;
        rockMaterial.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
        
        return rockMaterial;
    }
    
    private static ImageTexture CreateNoiseTexture(int seed, float baseR, float baseG, float baseB)
    {
        const int textureSize = 32;
        var image = Image.CreateEmpty(textureSize, textureSize, false, Image.Format.Rgb8);
        
        var random = new Random(seed);
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float variation = (float)(random.NextDouble() * 0.3 - 0.15);
                
                float r = Mathf.Clamp(baseR + variation, baseR - 0.1f, baseR + 0.1f);
                float g = Mathf.Clamp(baseG + variation, baseG - 0.1f, baseG + 0.1f);
                float b = Mathf.Clamp(baseB + variation, baseB - 0.1f, baseB + 0.1f);
                
                image.SetPixel(x, y, new Color(r, g, b, 1.0f));
            }
        }
        
        return ImageTexture.CreateFromImage(image);
    }
    
    // Legacy method for backwards compatibility
    private static ImageTexture CreateSimpleNoiseTexture()
    {
        return CreateNoiseTexture(42, 0.3f, 0.6f, 0.2f);
    }
}
