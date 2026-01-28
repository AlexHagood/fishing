using Godot;
using System;

public partial class HotbarUI : Control
{

    public const int hotbarSize = 6;
    public int selectedSlot = 0;

    ItemInstance[] hotbarItems = new ItemInstance[hotbarSize];

    public override void _Ready()
    {
        // Your custom hotbar handling code here
    }

    public void HighlightSlot(int slot)
    {
        selectedSlot = slot;
        // Update UI to highlight the selected slot
        for (int i = 0; i < GetChildCount(); i++)
        {
            var slotNode = GetChild<Control>(i);
            if (i == selectedSlot)
            {
                slotNode.AddThemeColorOverride("custom_color", Colors.Yellow);
            }
            else
            {
                slotNode.AddThemeColorOverride("custom_color", Colors.White);
            }
        }
    }
}
