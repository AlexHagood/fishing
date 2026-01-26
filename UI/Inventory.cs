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
        GD.Print($"[ForceFitItem] Attempting to fit item: {item.itemDef?.ItemName ?? "Unknown"} (size: {item.invSize})");
        
        for (int y = 0; y <= InventoryCapacity.Y - item.invSize.Y; y++)
        {
            for (int x = 0; x <= InventoryCapacity.X - item.invSize.X; x++)
            {
                var pos = new Vector2(x, y);
                var fitTiles = CanFitItem(item, pos);
                if (fitTiles.Count == item.invSize.X * item.invSize.Y)
                {
                    GD.Print($"[ForceFitItem] Successfully fitting item at position {pos}");
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
        GD.PrintErr($"[ForceFitItem] Failed to fit item: {item.itemDef?.ItemName ?? "Unknown"}");
        return false;
    }

    

    public List<InvTile> CanFitItem(InvItem item, Vector2 pos)
    {
        if (pos.X < 0 || pos.X >= InventoryCapacity.X || pos.Y < 0 || pos.Y >= InventoryCapacity.Y)
        {
            GD.Print($"[CanFitItem] Position out of bounds: {pos}");
            return [];
        }
        if (pos.X + item.invSize.X > InventoryCapacity.X || pos.Y + item.invSize.Y > InventoryCapacity.Y)
        {
            GD.Print($"[CanFitItem] Item overflows inventory at {pos}, size {item.invSize}");
            return [];
        }
        List<InvTile> itemTiles = new List<InvTile>();
        for (int y = 0; y < item.invSize.Y; y++)
        {
            for (int x = 0; x < item.invSize.X; x++)
            {
                int checkX = (int)(pos.X + x);
                int checkY = (int)(pos.Y + y);
                InvTile checktile = inventoryTiles[checkX, checkY];
                
                // Allow placement if tile is empty OR if it's the same item being repositioned
                if (checktile.item == null || checktile.item == item)
                {
                    itemTiles.Add(checktile);
                }
                else
                {
                    // Tile is occupied by a DIFFERENT item
                    GD.Print($"[CanFitItem] Tile at ({checkX},{checkY}) occupied by: {checktile.item.itemDef?.ItemName ?? "Unknown"} (checking for {item.itemDef?.ItemName ?? "Unknown"})");
                    return [];
                }
            }
        }

        return itemTiles;
    }

    public void PlaceItem(InvItem item, Vector2 pos)
    {
        List<InvTile> tiles = CanFitItem(item, pos);
        if (tiles.Count == item.invSize.X * item.invSize.Y)
        {
            // Clear previous tiles if item was already placed
            if (item.itemTiles != null && item.itemTiles.Count > 0)
            {
                foreach (var tile in item.itemTiles)
                {
                    if (tile != null)
                        tile.item = null; // Clear previous item from tiles
                }
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
            
            GD.Print($"[PlaceItem] Placed {item.itemDef?.ItemName ?? "Unknown"} at position {pos}");
        }
        else
        {
            GD.Print($"[PlaceItem] Item {item.itemDef?.ItemName ?? "Unknown"} cannot fit at position {pos}");
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
