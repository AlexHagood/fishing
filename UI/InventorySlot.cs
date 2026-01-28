using System.Collections.Generic;
using Godot;

#nullable enable
public enum SlotState
{
    Default,
    Valid,
    Invalid
}
public partial class InventorySlot : Panel
{

    private StyleBoxFlat _normalStyle = new StyleBoxFlat();
    private StyleBoxFlat _validStyle = new StyleBoxFlat();
    private StyleBoxFlat _invalidStyle = new StyleBoxFlat();

    public int inventoryId;


    public Vector2I slotPosition;
    
    public override void _Ready()
    {
        // Create normal style (dark gray)
        _normalStyle = new StyleBoxFlat();
        _normalStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
        _normalStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);
        _normalStyle.BorderWidthLeft = 1;
        _normalStyle.BorderWidthRight = 1;
        _normalStyle.BorderWidthTop = 1;
        _normalStyle.BorderWidthBottom = 1;
        
        // Create hover style (lighter gray)
        _validStyle.BgColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        _validStyle.BorderColor = new Color(0.6f, 0.6f, 0.6f, 1.0f);
        _validStyle.BorderWidthLeft = 1;
        _validStyle.BorderWidthRight = 1;
        _validStyle.BorderWidthTop = 1;
        _validStyle.BorderWidthBottom = 1;

        _invalidStyle.BgColor = new Color(0.5f, 0.1f, 0.1f, 1.0f);
        _invalidStyle.BorderColor = new Color(0.8f, 0.2f, 0.2f, 1.0f);
        _invalidStyle.BorderWidthLeft = 1;  
        _invalidStyle.BorderWidthRight = 1;
        _invalidStyle.BorderWidthTop = 1;
        _invalidStyle.BorderWidthBottom = 1;
        
        // Set initial style
        AddThemeStyleboxOverride("panel", _normalStyle);
        
        // Connect mouse signals
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }
    
    private void OnMouseEntered()
    {
        SetHighlight(SlotState.Valid);
    }
    
    private void OnMouseExited()
    {
        SetHighlight(SlotState.Default);
    }
    
    public void SetHighlight(SlotState state)
    {
        switch (state)
        {
            case SlotState.Default:
                AddThemeStyleboxOverride("panel", _normalStyle);
                break;
            case SlotState.Valid:
                AddThemeStyleboxOverride("panel", _validStyle);
                break;
            case SlotState.Invalid:
                AddThemeStyleboxOverride("panel", _invalidStyle);
                break;
        }
    }

    public List<InventorySlot> GetSlotsForSize(Vector2I size)
    {
        List<InventorySlot> neighbors = new List<InventorySlot>();

        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                Vector2I neighborPos = new Vector2I(slotPosition.X + x, slotPosition.Y + y);
                InventorySlot? neighbor = GetParent().GetNodeOrNull<InventorySlot>($"Slot_{neighborPos.X}_{neighborPos.Y}");
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }
        }

        return neighbors;
    }
}
