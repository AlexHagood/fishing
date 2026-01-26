using Godot;

public partial class ToolItem : GameItem
{

    public new const string HintF = "Pickup";

    // Tool-specific hold properties - can be overridden or use ItemDef values
    public Vector3 HoldPosition 
    { 
        get => ItemDef?.HoldPosition ?? Vector3.Zero;
        set { if (ItemDef != null) ItemDef.HoldPosition = value; }
    }
    
    public Vector3 HoldRotation 
    { 
        get => ItemDef?.HoldRotation ?? Vector3.Zero;
        set { if (ItemDef != null) ItemDef.HoldRotation = value; }
    }
    
    public Vector3 HoldScale 
    { 
        get => ItemDef?.HoldScale ?? Vector3.One;
        set { if (ItemDef != null) ItemDef.HoldScale = value; }
    }

    public override void _Ready()
    {
        base._Ready();
        
        // Mark as tool in definition if not already set
        if (ItemDef != null && !ItemDef.IsTool)
        {
            ItemDef.IsTool = true;
        }
    }

    // Tool-specific action methods
    public virtual void OnPrimaryFire()
    {
        GD.Print($"{ItemName} Primary Fire");
    }

    public virtual void OnSecondaryFire()
    {
        GD.Print($"{ItemName} Secondary Fire");
    }

    public virtual void OnEquip() 
    { 
        GD.Print($"{ItemName} equipped");
    }
    
    public virtual void OnUnequip() 
    { 
        GD.Print($"{ItemName} unequipped");
    }
}
