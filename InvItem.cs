using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class InvItem : Control
{
    bool isDragging = false;
	bool movedToTop = false;
	Vector2 offset;
	TextureRect dragIcon = null;
	Inventory _parentInventory;
	[Export]
	public Vector2 invSize = new Vector2(2, 2);

	public Vector2 invPos;

	List<InvTile> itemTiles = new List<InvTile>();

	TextureRect itemIcon;

	public InvItem()
	{
		itemIcon = GetNode<TextureRect>("ItemIcon");
		if (itemIcon == null)
		{
			itemIcon = new TextureRect();
			itemIcon.Name = "ItemIcon";
			AddChild(itemIcon);
		}
		}

	public InvItem(Vector2 size)
	{
		invSize = size;
		itemIcon = GetNode<TextureRect>("ItemIcon");
		if (itemIcon == null)
		{
			itemIcon = new TextureRect();
			itemIcon.Name = "ItemIcon";
			AddChild(itemIcon);
		}
	}


	public override void _GuiInput(InputEvent _event)
	{
		if (_event is InputEventMouseButton _mouseEvent)
		{
			if (_mouseEvent.ButtonIndex == MouseButton.Left && _mouseEvent.Pressed)
			{
				Vector2 mousePos = GetViewport().GetMousePosition();
				mousePos -= _parentInventory.Position;
				offset = mousePos - Position;
				isDragging = true;
				CreateDragIcon();
			}
			else if (_mouseEvent.ButtonIndex == MouseButton.Left && !_mouseEvent.Pressed && isDragging)
			{
				DropItem();
			}
		}
	}

	public override void _Ready()
	{
		_parentInventory = (Inventory)GetParent().GetParent();
		this.Size = _parentInventory.inventoryTileSize * invSize;
	}

	public override void _Process(double delta)
	{
		if (_parentInventory != null)
		{
			if (isDragging && dragIcon != null)
			{
				Vector2 mousePos = GetViewport().GetMousePosition();
				if (_parentInventory != null)
					mousePos -= _parentInventory.GlobalPosition;
				Vector2 pos = mousePos - offset;
				float _w = _parentInventory != null ? _parentInventory.Size.X - dragIcon.Size.X : Size.X - dragIcon.Size.X;
				float _h = _parentInventory != null ? _parentInventory.Size.Y - dragIcon.Size.Y : Size.Y - dragIcon.Size.Y;
				pos.X = Mathf.Clamp(pos.X, 0, _w);
				pos.Y = Mathf.Clamp(pos.Y, 0, _h);
				dragIcon.Position = pos;
			}
		}
	}

	private void CreateDragIcon()
	{
		if (dragIcon != null)
			return;
		var itemIcon = GetNode<TextureRect>("ItemIcon");
		if (itemIcon == null)
			return;
		dragIcon = new TextureRect();
		dragIcon.Texture = itemIcon.Texture;
		dragIcon.Size = itemIcon.Size;
		dragIcon.Modulate = new Color(1, 1, 1, 0.5f); // 50% transparency
		dragIcon.MouseFilter = Control.MouseFilterEnum.Ignore;
		Vector2 mousePos = GetViewport().GetMousePosition();
		if (_parentInventory != null)
		{
			mousePos -= _parentInventory.GlobalPosition;
			_parentInventory.AddChild(dragIcon);
		}
		else
		{
			AddChild(dragIcon);
		}
		dragIcon.Position = mousePos - offset;
	}

	private void DropItem()
	{
		if (dragIcon == null)
			return;

		// Use global coordinates for overlap detection
		Vector2 itemGlobalPos = dragIcon.Position + _parentInventory.GlobalPosition;
		InvTile targetPanel = null;
		foreach (InvTile invtile in _parentInventory.gridContainer.GetChildren())
		{
			Rect2 panelRect = new Rect2(invtile.GlobalPosition, invtile.Size);
			if (panelRect.HasPoint(itemGlobalPos))
			{
				GD.Print($"Overlap with panel {invtile.Name}");
				targetPanel = invtile;
				break;
			}
		}

		if (targetPanel != null)
		{
			List <InvTile> tiles = _parentInventory.CanFitItem(this, targetPanel.InvPos);
			if (tiles.Count == invSize.X * invSize.Y)
			{
				foreach (var tile in itemTiles)
				{
					tile.item = null; // Assign the item to the tile
				}
				itemTiles = tiles;
				foreach (var tile in itemTiles)
				{
					tile.item = this; // Assign the item to the tile
				}
				GlobalPosition = targetPanel.GlobalPosition;
			}
			else
			{
				GD.Print("Item cannot fit in the selected panel.");
			}
		}
		else
		{
			GD.Print("Target panel null. Item not dropped.");
		}

		dragIcon.QueueFree();
		dragIcon = null;
		isDragging = false;
		movedToTop = false;
	}
}
