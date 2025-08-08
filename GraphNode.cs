using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class GraphNode : Node3D
{
    public List<GraphNode> Connections { get; private set; }
    public MeshInstance3D MeshInstance;

    public GraphNode()
    {
        Name = "GraphNode";
        Connections = new List<GraphNode>();
        SetupMeshInstance();
    }

    public GraphNode(string nodeName)
    {
        Name = nodeName;
        Connections = new List<GraphNode>();
        SetupMeshInstance();
    }

    public GraphNode(Vector3 position, string nodeName = "GraphNode")
    {
        Name = nodeName;
        Position = position;
        Connections = new List<GraphNode>();
        SetupMeshInstance();
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

        MeshInstance.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.8f, 0.8f) // Neutral gray color
        };
        
        // Ensure the MeshInstance starts visible
        MeshInstance.Visible = true;
        GD.Print($"GraphNode {Name}: Created MeshInstance, visible={MeshInstance.Visible}");
    }

    public void Connect(GraphNode otherNode)
    {
        if (!Connections.Contains(otherNode))
        {
            Connections.Add(otherNode);
            otherNode.Connections.Add(this);
            // Note: Debug cylinder drawing is now handled centrally in UpdateNodeConnectionsFromTriangulation
        }
    }

    public override void _Ready()
    {
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
}
