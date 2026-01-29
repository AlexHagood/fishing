using Godot;
using System.Collections.Generic;

[Tool]
[GlobalClass]
public partial class GraphNode : Node3D
{
    [Export]
    public int Id { get; set; }
    
    public MeshInstance3D MeshInstance;
    private StandardMaterial3D material;
    private float animationTime = 0.0f;
    
    // Signal emitted when this node's position changes
    [Signal]
    public delegate void PositionChangedEventHandler(GraphNode node);
    
    // Custom Position property that emits signal on change
    public new Vector3 Position
    {
        get => base.Position;
        set
        {
            if (base.Position != value)
            {
                base.Position = value;
                // Emit signal when position changes
                if (IsInsideTree())
                {
                    EmitSignal(SignalName.PositionChanged, this);
                }
            }
        }
    }
    
    // Override GetHashCode and Equals to use Id for dictionary keys
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    
    public override bool Equals(object obj)
    {
        if (obj is GraphNode other)
        {
            return Id == other.Id;
        }
        return false;
    }

    public GraphNode()
    {
        Name = "GraphNode";
    }

    public GraphNode(Vector3 position)
    {
        Name = "GraphNode";
        Position = position;
    }


    private void SetupMeshInstance()
    {
        // Only create if it doesn't exist
        if (MeshInstance != null)
            return;
            
        MeshInstance = new MeshInstance3D();
        MeshInstance.Name = "MeshInstance3D";
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
        
        // GraphNode doesn't need collision - only the GroundMesh triangles do
    }

    public override void _Ready()
    {
        // Set up mesh and collision if not already present (important for Tool mode)
        SetupMeshInstance();
        
        // Try to get existing MeshInstance if it was created in editor
        if (MeshInstance == null)
        {
            MeshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        }
        
        // Ensure MeshInstance is properly set up and visible when the node enters the scene tree
        if (MeshInstance != null)
        {
            MeshInstance.Visible = true;
            if (material == null && MeshInstance.MaterialOverride is StandardMaterial3D mat)
            {
                material = mat;
            }
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
