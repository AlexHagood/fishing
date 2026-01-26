using Godot;
using System.Collections.Generic;

/// <summary>
/// Represents a container item that can store other items in its inventory.
/// Examples: backpack, chest, crate, etc.
/// Hold F to pickup into inventory, press E to open it.
/// </summary>
public partial class ContainerItem : GameItem
{
    [Export] public Vector2 ContainerSize = new Vector2(5, 3);
    
    // Override the button hints
    public new const string HintE = "Open";
    public new const string HintF = "Pickup";
    
    // Internal inventory for the container
    private Inventory _containerInventory;
    
    // Signal to notify when container is opened
    [Signal]
    public delegate void ContainerOpenedEventHandler(ContainerItem container);
    
    public override void _Ready()
    {
        base._Ready();
        
        // Create the internal inventory for this container with custom size
        _containerInventory = new Inventory(ContainerSize);
        _containerInventory.Name = "ContainerInventory";
        AddChild(_containerInventory);
        
        GD.Print($"[ContainerItem] {ItemName} created with {ContainerSize.X}x{ContainerSize.Y} inventory");
    }
    
    /// <summary>
    /// Called when player presses E to open the container
    /// </summary>
    public void OpenContainer()
    {
        GD.Print($"[ContainerItem] Opening {ItemName}");
        EmitSignal(SignalName.ContainerOpened, this);
    }
    
    /// <summary>
    /// Get the container's inventory
    /// </summary>
    public Inventory GetContainerInventory()
    {
        return _containerInventory;
    }
    
    /// <summary>
    /// Transfer all items from this container to a target inventory
    /// </summary>
    public void TransferAllItemsTo(Inventory targetInventory)
    {
        if (_containerInventory == null || targetInventory == null)
        {
            GD.PrintErr("[ContainerItem] Cannot transfer items - missing inventory reference");
            return;
        }
        
        var items = new List<InvItem>();
        foreach (var child in _containerInventory.GetChildren())
        {
            if (child is InvItem invItem)
            {
                items.Add(invItem);
            }
        }
        
        foreach (var item in items)
        {
            if (targetInventory.ForceFitItem(item))
            {
                item.Reparent(targetInventory);
                GD.Print($"[ContainerItem] Transferred {item.itemDef.ItemName}");
            }
            else
            {
                GD.Print($"[ContainerItem] No space for {item.itemDef.ItemName}");
            }
        }
    }
}