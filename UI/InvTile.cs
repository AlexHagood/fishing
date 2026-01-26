using Godot;
using System;

public partial class InvTile : Panel
{
    public Vector2 InvPos;

    private StyleBoxFlat styleBox;

    private InvItem _item;

    public InvItem item
    {
        get => _item;
        set
        {
            _item = value;
            if (_item != null)
                SetColor(new Color(0.2f, 0.2f, 0.2f)); // Grey
            else
                SetColor(new Color(0, 0, 0)); // Black
        }
    }

    public InvTile(Vector2 pos)
    {
        InvPos = pos;
        styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0, 0, 0);
        AddThemeStyleboxOverride("panel", styleBox);
    }

    public void SetColor(Color color)
    {
        styleBox.BgColor = color; // Directly update the color
    }

    
}