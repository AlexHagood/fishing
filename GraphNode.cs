using Godot;
using System.Collections.Generic;

[Tool]
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
    
    public override void _Notification(int what)
    {
        if (what == NotificationTransformChanged)
        {
            UpdateConnectedMeshes();
        }
    }

    private void UpdateConnectedMeshes()
    {
        // Update all connected ground meshes when this node moves
        if (ConnectedGroundMeshes != null && ConnectedGroundMeshes.Count > 0)
        {
            foreach (var groundMesh in ConnectedGroundMeshes)
            {
                if (groundMesh != null && IsInstanceValid(groundMesh))
                {
                    try 
                    {
                        groundMesh.UpdateMeshGeometry();
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr($"Error updating ground mesh for node {Name}: {ex.Message}");
                    }
                }
            }
        }
    }

    public GraphNode()
    {
        Name = "GraphNode";
        Connections = new List<GraphNode>();
        ConnectedGroundMeshes = new List<GroundMesh>();
        SetupMeshInstance();
    }

    public GraphNode(Vector3 position)
    {
        Name = "GraphNode";
        Position = position;
        Connections = new List<GraphNode>();
        ConnectedGroundMeshes = new List<GroundMesh>();
        SetupMeshInstance();
    }

    public GraphNode(string nodeName)
    {
        Name = nodeName;
        Connections = new List<GraphNode>();
        ConnectedGroundMeshes = new List<GroundMesh>();
        SetupMeshInstance();
    }

    public GraphNode(Vector3 position, string nodeName = "GraphNode")
    {
        Name = nodeName;
        Position = position;
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
        MeshInstance.Position = Vector3.Zero; // Ensure mesh is centered at node origin
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
        // Enable notification for transform changes
        SetNotifyTransform(true);
        
        // Ensure MeshInstance is properly set up and visible when the node enters the scene tree
        if (MeshInstance != null)
        {
            MeshInstance.Visible = true;
        }
        else
        {
            GD.Print($"GraphNode {Name} _Ready: MeshInstance is null!");
        }

        // Set owner of children so they are saved to the scene file
        if (Engine.IsEditorHint())
        {
            var tree = GetTree();
            if (tree != null)
            {
                var root = tree.EditedSceneRoot;
                if (root != null)
                {
                    foreach (var child in GetChildren())
                    {
                        child.Owner = root;
                    }
                }
            }
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
