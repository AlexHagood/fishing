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

    private bool _rotated;
    private bool _originalRotated;

    Label _CountLabel = new Label(); 
    
    public void setup(ItemTile original)
    {
        Count = original.ItemInstance.CurrentStackSize;
        Texture = original.Texture;
        Size = original.Size;
        _originalRotated = original.ItemInstance.IsRotated;
        _rotated = false; // Start with no additional rotation
    }

    public void UpdateRotation(bool targetRotation)
    {
        // targetRotation is the final rotation state we want
        // _originalRotated is what the item was when picked up
        // We need to rotate the visual if targetRotation != _originalRotated
        
        bool shouldBeRotated = targetRotation != _originalRotated;
        
        if (shouldBeRotated == _rotated)
            return; // Already in correct state
        
        // Need to rotate the visual
        _rotated = shouldBeRotated;
        
        Image image = Texture.GetImage();
        if (shouldBeRotated)
        {
            // Rotate 90 degrees counterclockwise
            image.Rotate90(ClockDirection.Counterclockwise);
        }
        else
        {
            // Rotate back (90 degrees clockwise)
            image.Rotate90(ClockDirection.Clockwise);
        }
        Texture = ImageTexture.CreateFromImage(image);
        Size = new Vector2(Size.Y, Size.X); // Swap dimensions
    }



    public override void _Ready()
    {
        AddChild(_CountLabel);
        _CountLabel.SetAnchorsPreset(LayoutPreset.BottomRight);
        base._Ready();
    }
}