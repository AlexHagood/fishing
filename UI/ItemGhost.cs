using Godot;

public partial class ItemGhost : TextureRect
{
    private int _count = 1;
    public int Count 
    { 
        get => _count; 
        set
        {
            _count = value;
            _CountLabel.Text = _count.ToString();
        }
    }

    int ItemInstanceId;

    Label _CountLabel = new Label(); 


    
    public void setup(ItemTile original)
    {
        Count = original.ItemInstance.CurrentStackSize;
        Texture = original.Texture;
        Size = original.Size;
    }

    public void RotateLeft()
    {
        Image image = Texture.GetImage();
        image.Rotate90(ClockDirection.Counterclockwise);
        Texture = ImageTexture.CreateFromImage(image);
        Size = Size.Flip(); // Swap dimensions
    }

    public void RotateRight()
    {
        Image image = Texture.GetImage();
        image.Rotate90(ClockDirection.Clockwise);
        Texture = ImageTexture.CreateFromImage(image);
        Size = Size.Flip(); // Swap dimensions
    }

    public override void _Ready()
    {
        AddChild(_CountLabel);
        _CountLabel.SetAnchorsPreset(LayoutPreset.BottomRight);
        base._Ready();
    }
}