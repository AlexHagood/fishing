using Godot;

/// <summary>
/// Base class for all interactable world items.
/// Provides E and F interaction methods that subclasses can override.
/// </summary>
[GlobalClass]
public partial class WorldItem : RigidBody3D, IInteractable
{
    /// <summary>
    /// Reference to the inventory item definition (.tres resource)
    /// </summary>
    [Export] public ItemDefinition InvItemData { get; set; }
    
    [Export] public float InteractRange { get; set; } = 5.0f;
    
    public virtual string HintE { get; protected set; } = "";
    public virtual string HintF { get; protected set; } = "";


    private Node3D? _holdTarget;
    public Node3D? holdTarget
    {
        get { return _holdTarget; }
        set
        {
            GravityScale = value != null ? 0.0f : 1.0f; // Disable gravity when held
            _holdTarget = value;
        }
    }

    bool spawnerManaged = false;

    // Floaty physics parameters
    private float _followStrength = 8.0f;
    private float _damping = 4.0f;
    private float _angularDamping = 6.0f;

    [Export] public float ThrowForce { get; set; } = 15.0f;

    public override void _Ready()
    {
        // Only create synchronizer if one doesn't already exist (from scene)
        if (GetNodeOrNull<MultiplayerSynchronizer>("Synchronizer") == null)
        {
            MultiplayerSynchronizer sync = new MultiplayerSynchronizer();
            sync.SetMultiplayerAuthority(GetMultiplayerAuthority());
            sync.Name = "Synchronizer";
            AddChild(sync);
            SceneReplicationConfig config = new SceneReplicationConfig();
            config.AddProperty(":position");
            sync.ReplicationConfig = config;
        }
    }



    public override void _PhysicsProcess(double delta)
    {
        if (holdTarget != null)
        {
            // Calculate the desired position and rotation
            Vector3 targetPosition = holdTarget.GlobalTransform.Origin;
            Vector3 toTarget = targetPosition - GlobalTransform.Origin;

            // Apply a force towards the target position
            Vector3 force = toTarget * _followStrength - LinearVelocity * _damping;
            ApplyCentralForce(force);

            // Optionally apply torque to align rotation (not implemented here for simplicity)
        }
    }

    /// <summary>
    /// Called when player presses E key while looking at this item
    /// </summary>
    public virtual void InteractE(Character character)
    {
        GD.Print($"[WorldItem] InteractE on {InvItemData.Name} - override this in subclasses");
    }

    /// <summary>
    /// Called when player presses F key while looking at this item
    /// </summary>
    public virtual void InteractF(Character character)
    {
        GD.Print($"[WorldItem] InteractF on {InvItemData.Name} - override this in subclasses");
    }

    /// <summary>
    /// Check if the item can be interacted with
    /// </summary>
    public virtual bool CanInteract()
    {
        return this != null && !IsQueuedForDeletion();
    }

    public void Pickup(Character character)
    {
        InventoryManager inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
        if (!spawnerManaged)
        {
            inventoryManager.RequestSpawnInstance(InvItemData.ResourcePath, character.inventoryId, GetPath(), false);
        }
        else
        {
            inventoryManager.RequestSpawnInstance(InvItemData.ResourcePath, character.inventoryId, new NodePath(), false);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Grab(NodePath characterPath)
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(GetMultiplayerAuthority(), nameof(Grab), characterPath);
        }
        Character character = GetNodeOrNull<Character>(characterPath);
        if (character == null)
        {
            throw new System.Exception($"Grab: Could not find Character node at path {characterPath}");
        }
        character.heldPhysItem = this; 
        holdTarget = character.holdPosition;
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Throw(Vector3 direction)
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(GetMultiplayerAuthority(), nameof(Throw), direction);
        }
        holdTarget = null;
        ApplyCentralImpulse(direction * ThrowForce);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Destroy()
    {
        GD.Print($"Destroying worlditem on pickup on client {Multiplayer.GetUniqueId()}");
        QueueFree();
    }

}
