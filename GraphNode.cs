using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class GraphNode : StaticBody3D
{
    public List<GraphNode> Connections { get; private set; }
    public MeshInstance3D MeshInstance;
    private StandardMaterial3D material;
    private float animationTime = 0.0f;
    
    // New: References to connected ground meshes and terrain
    public List<GroundMesh> ConnectedGroundMeshes { get; private set; }
    public Terrain ParentTerrain { get; set; }
    
    // Custom position setter to update ground meshes when position changes
    private Vector3 _position;
    public new Vector3 Position
    {
        get => _position;
        set
        {
            var oldPosition = _position;
            _position = value;
            base.Position = value; // Update the actual Node3D position
            
            GD.Print($"GraphNode {Name} position changed from {oldPosition} to {value}");
            GD.Print($"ParentTerrain is null: {ParentTerrain == null}");
            GD.Print($"ConnectedGroundMeshes count: {ConnectedGroundMeshes.Count}");
        }
    }

    public GraphNode()
    {
        Name = "GraphNode";
        Connections = new List<GraphNode>();
        ConnectedGroundMeshes = new List<GroundMesh>();
        _position = Vector3.Zero;
        SetupMeshInstance();
    }

    public GraphNode(string nodeName)
    {
        Name = nodeName;
        Connections = new List<GraphNode>();
        ConnectedGroundMeshes = new List<GroundMesh>();
        _position = Vector3.Zero;
        SetupMeshInstance();
    }

    public GraphNode(Vector3 position, string nodeName = "GraphNode")
    {
        Name = nodeName;
        _position = position;
        base.Position = position;
        Connections = new List<GraphNode>();
        ConnectedGroundMeshes = new List<GroundMesh>();
        SetupMeshInstance();
    }
    
    // Method to add a ground mesh reference
    public void AddGroundMeshReference(GroundMesh groundMesh)
    {
        if (!ConnectedGroundMeshes.Contains(groundMesh))
        {
            ConnectedGroundMeshes.Add(groundMesh);
        }
    }
    
    // Method to remove a ground mesh reference
    public void RemoveGroundMeshReference(GroundMesh groundMesh)
    {
        ConnectedGroundMeshes.Remove(groundMesh);
    }

    private void SetupMeshInstance()
    {
        MeshInstance = new MeshInstance3D();
        AddChild(MeshInstance);
        
        MeshInstance.Mesh = new SphereMesh()
        {
            Radius = 0.1f,
            Height = 0.2f
        };

        material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.8f, 0.8f), // Neutral gray color
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        
        MeshInstance.MaterialOverride = material;
        
        // Ensure the MeshInstance starts visible
        MeshInstance.Visible = true;
        GD.Print($"GraphNode {Name}: Created MeshInstance, visible={MeshInstance.Visible}");
        
        // Add collision shape for click detection
        var collisionShape = new CollisionShape3D();
        var sphereShape = new SphereShape3D();
        sphereShape.Radius = 0.1f; // Same as mesh radius
        collisionShape.Shape = sphereShape;
        AddChild(collisionShape);
    }

    public void Connect(GraphNode otherNode)
    {
        if (!Connections.Contains(otherNode))
        {
            Connections.Add(otherNode);
            otherNode.Connections.Add(this);
        }
    }

    public override void _Ready()
    {
        // Initialize position from base Position for existing nodes
        if (_position == Vector3.Zero)
        {
            _position = base.Position;
        }
        
        // Ensure MeshInstance is properly set up and visible when the node enters the scene tree
        if (MeshInstance != null)
        {
            MeshInstance.Visible = true;
            GD.Print($"GraphNode {Name} _Ready: MeshInstance visible={MeshInstance.Visible}, mesh null={MeshInstance.Mesh == null}");
        }
        else
        {
            GD.Print($"GraphNode {Name} _Ready: MeshInstance is null!");
        }
    }

    public void SetBlack()
    {
        if (material != null)
        {
            material.AlbedoColor = new Color(0.0f, 0.0f, 0.0f, material.AlbedoColor.A); // Keep current alpha, set to black
            GD.Print($"GraphNode {Name}: Set to black");
        }
    }

    public override void _Process(double delta)
    {
        // ...existing code...
    }
}
