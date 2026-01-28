using Godot;

/// <summary>
/// Base class for all interactable world items.
/// Provides E and F interaction methods that subclasses can override.
/// </summary>
[GlobalClass]
public partial class WorldItem : RigidBody3D
{
    /// <summary>
    /// Reference to the inventory item definition (.tres resource)
    /// </summary>
    [Export] public ItemDefinition InvItemData { get; set; }
    
    [Export] public float InteractRange { get; set; } = 3.0f;
    
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
}
