using Godot;
using System;
using System.Collections.Generic;
public partial class Inventory : Control
{
    private Panel _inventoryPanel;

    public GridContainer gridContainer;

    private Vector2 InventorySize = new Vector2(600, 400);

    private Vector2 InventoryCapacity = new Vector2(8, 4);

    public int inventoryTileSize = 64;

    InvTile[,] inventoryTiles;

    public override void _Ready()
    {
        inventoryTiles = new InvTile[(int)InventoryCapacity.X, (int)InventoryCapacity.Y];
        _inventoryPanel = GetNode<Panel>("InventoryPanel");
        gridContainer = _inventoryPanel.GetNode<GridContainer>("GridContainer");

        UpdateInventorySize();
        this.Visible = false;

        var newitem = new InvItem(new Vector2(1, 3));
        AddChild(newitem);
        newitem.Position = new Vector2(300, 300);
    }
    private void UpdateInventorySize()
    {
        // Center the inventory panel in the middle of the viewport
        var viewportSize = GetViewport().GetVisibleRect().Size;
        InventorySize = viewportSize * 0.8f;
        var invpos = (viewportSize - InventorySize) / 2;
        this.Position = invpos;
        this.Size = InventorySize;
        gridContainer.Columns = (int)InventoryCapacity.X;
        gridContainer.Size = InventoryCapacity * (inventoryTileSize);
        foreach (var itemgrid in gridContainer.GetChildren())
        {
            itemgrid.QueueFree(); // Clear existing items 
        }
        for (int y = 0; y < InventoryCapacity.Y; y++)
        {
            for (int x = 0; x < InventoryCapacity.X; x++)
            {
                var panel = new InvTile(new Vector2(x, y));
                inventoryTiles[x, y] = panel;
                panel.Name = $"ItemGrid_{x}_{y}";
                panel.SizeFlagsHorizontal = Control.SizeFlags.Expand;
                panel.SizeFlagsVertical = Control.SizeFlags.Expand;
                panel.CustomMinimumSize = new Vector2(inventoryTileSize, inventoryTileSize);
                panel.Modulate = new Color(0.7f, 0.0f, 0.0f, 1f); // gray
                gridContainer.AddChild(panel);
            }
        }

    }

    public List<InvTile> CanFitItem(InvItem item, Vector2 pos)
    {
        GD.Print("test");
        if (pos.X < 0 || pos.X >= InventoryCapacity.X || pos.Y < 0 || pos.Y >= InventoryCapacity.Y)
        {
            GD.Print($"Position out of bounds: {pos}");
            return [];
        }
        if (pos.X + item.invSize.X > InventoryCapacity.X || pos.Y + item.invSize.Y > InventoryCapacity.Y)
        {
            GD.Print($"Item overflows inventory");
            return [];
        }
        List<InvTile> itemTiles = new List<InvTile>();
        for (int y = 0; y < item.invSize.Y; y++)
        {
            for (int x = 0; x < item.invSize.X; x++)
            {
                GD.Print($"Checking tile at position {pos + new Vector2(pos.X + x, pos.Y + y)}");
                InvTile checktile = inventoryTiles[(int)(pos.X + x), (int)(pos.Y + y)];
                if (checktile.item == item || checktile.item == null)
                {
                    itemTiles.Add(checktile);
                }
                else
                {
                    GD.Print($"Tile at position {pos + new Vector2(x, y)} is occupied by another item");
                    return [];

                }
            }
        }

        var tile = inventoryTiles[(int)pos.X, (int)pos.Y];
        if (tile == null)
        {
            GD.Print($"Tile at position {pos} is null");
            return [];
        }
        return itemTiles;
    }


}
