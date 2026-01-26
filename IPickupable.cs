using Godot;

/// <summary>
/// Interface for all items that can be picked up and placed in inventory
/// </summary>
public interface IPickupable
{
    // Basic item properties
    bool IsPickupable { get; set; }
    float PickupRange { get; set; }
    float ThrowForce { get; set; }
    string ItemName { get; set; }
    
    // Inventory properties
    Vector2 InvSize { get; set; }
    Texture2D InvTexture { get; set; }
    InvItem invItem { get; set; }
    
    // Pickup/drop methods
    bool CanBePickedUp();
    void OnPickedUp();
    void OnDropped();
    void OnThrown(Vector3 throwDirection, float force);
    
    // Physics management
    void DisablePhys();
    void EnablePhys();
}
