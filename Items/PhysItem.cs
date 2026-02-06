using Godot;

/// <summary>
/// Physics-based item that can be picked up and thrown by the player.
/// Uses floaty physics when held. Pickup happens on InteractE.
/// </summary>
[GlobalClass]
#nullable enable
public partial class PhysItem : WorldItem
{
    public override string HintE { get; protected set; } = "Grab";
    public override string HintF { get; protected set; } = "Pick up";


    public override void _Ready()
    {
        // Ensure default collision settings
        CollisionLayer = 1;
        CollisionMask = 1;
        GravityScale = 1.0f;
    }

    /// <summary>
    /// E key - Pick up the item
    /// </summary>
    public override void InteractE(Character character)
    {
        Grab(character.GetPath());
    }

    /// <summary>
    /// F key - Not used for PhysItem
    /// </summary>
    public override void InteractF(Character character)
    {

    }


}
