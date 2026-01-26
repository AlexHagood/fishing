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
	public Inventory _Inventory;
	[Export]
	public Vector2 invSize = new Vector2(2, 2);

	public Vector2 invPos;

	public List<InvTile> itemTiles = new List<InvTile>();

	public TextureRect itemIcon;

	private Label _numberLabel;
	private int _displayedNumber = -1;

	// Can hold either a GameItem (RigidBody3D) or a ToolItem (Node3D)
	public Node3D gameItem;

	public InvItem()
	{
		itemIcon = new TextureRect();
		itemIcon.Name = "ItemIcon";
		itemIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
		itemIcon.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(itemIcon);
	}

	public InvItem(Vector2 size)
	{
		invSize = size;
		itemIcon = new TextureRect();
		itemIcon.Name = "ItemIcon";
		itemIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
		itemIcon.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(itemIcon);
	}


	private void PlaySound(string path)
	{
		var player = new AudioStreamPlayer();
		player.Stream = GD.Load<AudioStream>(path);
		GetTree().Root.AddChild(player);
		player.Play();
		player.Finished += () => player.QueueFree();
	}

	public override void _GuiInput(InputEvent _event)
	{
		if (_event is InputEventMouseButton _mouseEvent)
		{
			if (_mouseEvent.ButtonIndex == MouseButton.Left && _mouseEvent.Pressed)
			{
				GD.Print($"Clicked on item {Name}");
				PlaySound("res://Sounds/pickup.ogg");
				Vector2 mousePos = GetViewport().GetMousePosition();
				mousePos -= _Inventory.Position;
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
		
		this.Size = _Inventory.inventoryTileSize * invSize;
		MouseFilter = Control.MouseFilterEnum.Stop; // Ensure we receive mouse events
		if (itemIcon != null)
			itemIcon.Size = this.Size; // Make icon fill the item
		if (_numberLabel == null)
			CreateNumberLabel();
	}

	public override void _Process(double delta)
	{
		if (_Inventory != null)
		{
			if (isDragging && dragIcon != null)
			{
				Vector2 mousePos = GetViewport().GetMousePosition();
				if (_Inventory != null)
					mousePos -= _Inventory.GlobalPosition;
				Vector2 pos = mousePos - offset;
				float _w = _Inventory != null ? _Inventory.Size.X - dragIcon.Size.X : Size.X - dragIcon.Size.X;
				float _h = _Inventory != null ? _Inventory.Size.Y - dragIcon.Size.Y : Size.Y - dragIcon.Size.Y;
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

		if (OS.IsDebugBuild())
		{
			var dot = new ColorRect();
			dot.Color = new Color(1, 0, 1); // Pink
			dot.Size = new Vector2(4, 4);
			dot.Position = new Vector2(32, 32) - dot.Size / 2;
			dot.MouseFilter = Control.MouseFilterEnum.Ignore;
			dragIcon.AddChild(dot);
		}

		Vector2 mousePos = GetViewport().GetMousePosition();
		if (_Inventory != null)
		{
			mousePos -= _Inventory.GlobalPosition;
			_Inventory.AddChild(dragIcon);
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
		Vector2 itemGlobalPos = dragIcon.GlobalPosition + new Vector2(32, 32);
		InvTile targetPanel = null;
		foreach (InvTile invtile in _Inventory.gridContainer.GetChildren())
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
			_Inventory.PlaceItem(this, targetPanel.InvPos);
			PlaySound("res://Sounds/drop.ogg");
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

	public void SetBindNumber(int number)
	{
		_displayedNumber = number;
		if (_numberLabel == null)
			CreateNumberLabel();
		_numberLabel.Text = number > 0 ? number.ToString() : "";
		_numberLabel.Visible = number > 0;
	}

	private void CreateNumberLabel()
	{
		_numberLabel = new Label();
		_numberLabel.Name = "NumberLabel";
		_numberLabel.Text = "";
		_numberLabel.Visible = false;
		// Remove invalid SizeFlags.None assignments
		_numberLabel.SizeFlagsHorizontal = 0;
		_numberLabel.SizeFlagsVertical = 0;
		_numberLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0)); // Yellow
		_numberLabel.AddThemeFontSizeOverride("font_size", 18);
		_numberLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_numberLabel.VerticalAlignment = VerticalAlignment.Top;
		AddChild(_numberLabel);
		// Position in top right
		_numberLabel.AnchorRight = 1;
		_numberLabel.AnchorTop = 0;
		_numberLabel.OffsetRight = -4;
		_numberLabel.OffsetTop = 4;
	}

	public void RemoveItem()
	{
		// Remove from inventory panel
		if (GetParent() != null)
		{
			GetParent().RemoveChild(this);
		}
		// Clear tiles
		if (itemTiles != null)
		{
			foreach (var tile in itemTiles)
			{
				tile.item = null;
			}
			itemTiles.Clear();
			}
		// Hide and reset state for reuse
		Visible = false;
		isDragging = false;
		movedToTop = false;
		_displayedNumber = -1;
		if (_numberLabel != null)
		{
			_numberLabel.Text = "";
			_numberLabel.Visible = false;
		}
	}
}
