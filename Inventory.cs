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

    InvItem[] inventoryItems;

    public override void _Ready()
    {
        inventoryTiles = new InvTile[(int)InventoryCapacity.X, (int)InventoryCapacity.Y];
        _inventoryPanel = GetNode<Panel>("InventoryPanel");
        gridContainer = _inventoryPanel.GetNode<GridContainer>("GridContainer");

        UpdateInventorySize();
        this.Visible = false;
        
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
                gridContainer.AddChild(panel);
            }
        }

    }

    public bool ForceFitItem(InvItem item)
    {

        for (int y = 0; y <= InventoryCapacity.Y - item.invSize.Y; y++)
        {
            for (int x = 0; x <= InventoryCapacity.X - item.invSize.X; x++)
            {
                var pos = new Vector2(x, y);
                var fitTiles = CanFitItem(item, pos);
                if (fitTiles.Count == item.invSize.X * item.invSize.Y)
                {
                    GD.Print($"Fitting item at position {pos}");
                    foreach (var tile in fitTiles)
                    {
                        tile.item = item; // Use property to update color
                    }
                    item.invPos = pos;
                    item._Inventory = this; // Set parent inventory
                    item.itemTiles = fitTiles; // Track which tiles hold the item
                    // Position item using grid tile's global position minus parent global position
                    item.Position = inventoryTiles[x, y].Position + ((GridContainer)inventoryTiles[x, y].GetParent()).Position;
                    return true;
                }
            }
        }
        return false;
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

    public void PlaceItem(InvItem item, Vector2 pos)
    {
        List<InvTile> tiles = CanFitItem(item, pos);
        if (tiles.Count == item.invSize.X * item.invSize.Y)
        {
            foreach (var tile in item.itemTiles)
            {
                tile.item = null; // Clear previous item from tiles
            }
            item.itemTiles = tiles;
            foreach (var tile in item.itemTiles)
            {
                tile.item = item; // Assign the item to the tile
            }
            item.invPos = pos;
            item._Inventory = this; // Set parent inventory
            // Position item using grid tile's global position minus parent global position
            item.Position = inventoryTiles[(int)pos.X, (int)pos.Y].Position + ((GridContainer)inventoryTiles[(int)pos.X, (int)pos.Y].GetParent()).Position;
        }
        else
        {
            GD.Print("Item cannot fit in the selected position.");
        }

    }

    public void RemoveItem(InvItem item)
    {
        if (item == null || item.itemTiles == null)
        {
            GD.Print("Item or item tiles are null, cannot remove.");
            return;
        }
        foreach (var tile in item.itemTiles)
        {
            tile.item = null; // Clear the item from the tiles
        }
        item.itemTiles.Clear(); // Clear the list of tiles
        item.invPos = Vector2.Zero; // Reset position
        item._Inventory = null; // Clear parent inventory
        item.QueueFree(); // Remove the item from the scene
    }



}
