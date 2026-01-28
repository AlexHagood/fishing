using Godot;

public partial class ItemTile : TextureRect
{
    public ItemInstance ItemInstance;

    private Label StackLabel;
    
    public override void _Ready()
    {
        StackLabel = GetNode<Label>("StackCount");
        UpdateDisplay();
    }

    private Vector2I _position;
    private Vector2I _size = new Vector2I(1, 1);

    public void UpdateDisplay()
    {
        if (ItemInstance == null) return;
        
        Size = new Vector2(ItemInstance.Size.X * 64, ItemInstance.Size.Y * 64);
        Position = new Vector2(ItemInstance.GridPosition.X * 64, ItemInstance.GridPosition.Y * 64);
        Texture = GD.Load<Texture2D>(ItemInstance.ItemData.Icon);
        if (ItemInstance.IsRotated)
        {
            Image image = GD.Load<Texture2D>(ItemInstance.ItemData.Icon).GetImage();
            image.Rotate90(ClockDirection.Counterclockwise);
            Texture = ImageTexture.CreateFromImage(image);
        }
        else
        {
            Texture = GD.Load<Texture2D>(ItemInstance.ItemData.Icon);
        }
        StackLabel.Text = $"{ItemInstance.CurrentStackSize} / {ItemInstance.ItemData.StackSize}";
    }
} 