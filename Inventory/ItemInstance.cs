using Godot;

namespace InventorySystem
{
    public class ItemInstance
    {
    public int InventoryId { get; set; }
    public int Id { get; set; }
    public ItemDefinition ItemData { get; set; }
    public int Count { get; set; }
    public Vector2I GridPosition { get; set; }
    public bool IsRotated { get; set; } = false;
    public bool Infinite = false;

    public bool IsCoin => ItemData.ResourcePath == "res://Items/Coin.tres";

    public string Name => ItemData.Name;

    public Vector2I Size
    {

        get
        {
            
            if (IsRotated)
                return new Vector2I(ItemData.Size.Y, ItemData.Size.X);
            return ItemData.Size;
        }
    }

        public override string ToString()
        {
            return $"{Count} {ItemData.Name}, Pos: {GridPosition}, Rotated: {IsRotated} InventoryId: {InventoryId} InstanceId: {Id}";
        }
}
}
