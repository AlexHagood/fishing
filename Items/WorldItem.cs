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

    public void pickup(Character character)
    {
        InventoryManager inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
        if (Multiplayer.IsServer())
        {
            GD.Print("[WorldItem] Server processing pickup");
            bool res = inventoryManager.SpawnInstance(InvItemData, character.inventoryId);
            if (res)
            {
                GD.Print("[WorldItem] Server pickup successful, deleting world item");
                Rpc(nameof(DeleteWorldItem));
                QueueFree();
            }
            else
            {
                GD.Print("[WorldItem] Server pickup failed, inventory full?");
            }
        }
        else
        {
            GD.Print("[WorldItem] Client processing pickup");
            RpcId(1, nameof(ClientRequestPickup));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void ClientRequestPickup()
    {
        int inventoryId = (int)Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            InventoryManager inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");
            bool res = inventoryManager.SpawnInstance(InvItemData, inventoryId);
            if (res)
            {
                GD.Print("[WorldItem] ClientRequestPickup successful, deleting world item");
                Rpc(nameof(DeleteWorldItem));
            }
            else
            {
                GD.Print("[WorldItem] ClientRequestPickup failed, inventory full?");
            }
            GD.Print("[WorldItem] RequestPickup called on client, ignoring");
            return;
        }
        throw new System.Exception("ClientRequestPickup should not be called on client!");
    }


    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void DeleteWorldItem()
    {
        int inventoryId = (int)Multiplayer.GetUniqueId();
        // Runs on all clients (not server, since server already called QueueFree)
        InventoryManager inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");

        bool res = inventoryManager.SpawnInstance(InvItemData, inventoryId);

        if (!res)
        {
            throw new System.Exception("Server had inventory space, but client didnt! HOW?!");
        }
        QueueFree();
    }
}
