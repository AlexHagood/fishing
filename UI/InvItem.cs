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
	
	// Item definition (required)
	public ItemDefinition itemDef;
	
	// Stack count for stackable items
	public int stackCount = 1;
	
	public Vector2 invSize => itemDef?.InvSize ?? new Vector2(2, 2);
	public Vector2 invPos;

	public List<InvTile> itemTiles = new List<InvTile>();

	public TextureRect itemIcon;

	private Label _numberLabel; // Hotbar bind number (1-6) - should exist in scene
	private Label _stackLabel; // Stack count - should exist in scene
	private int _displayedNumber = -1;

	// Reference to world instance (if spawned) - can be null for stacked items
	public GameItem gameItem;

	public InvItem()
	{
		// ItemIcon should be created in the editor, but fallback for code-created items
		itemIcon = GetNodeOrNull<TextureRect>("ItemIcon");
		if (itemIcon == null)
		{
			GD.PrintErr("InvItem: ItemIcon not found! Should be created in editor.");
			itemIcon = new TextureRect();
			itemIcon.Name = "ItemIcon";
			itemIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
			itemIcon.MouseFilter = Control.MouseFilterEnum.Pass;
			AddChild(itemIcon);
		}
	}

	public InvItem(ItemDefinition def, int count = 1)
	{
		itemDef = def;
		stackCount = count;
		
		// ItemIcon should be created in the editor
		itemIcon = GetNodeOrNull<TextureRect>("ItemIcon");
		if (itemIcon == null)
		{
			GD.PrintErr("InvItem: ItemIcon not found! Should be created in editor.");
			itemIcon = new TextureRect();
			itemIcon.Name = "ItemIcon";
			itemIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
			itemIcon.MouseFilter = Control.MouseFilterEnum.Pass;
			AddChild(itemIcon);
		}
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
				GD.Print($"[InvItem] Clicked on item {itemDef?.ItemName ?? Name}");
				PlaySound("res://Sounds/pickup.ogg");
				
				// Use GlobalPosition for proper coordinate calculation
				Vector2 mousePos = GetGlobalMousePosition();
				Vector2 itemGlobalPos = GlobalPosition;
				offset = mousePos - itemGlobalPos;
				
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
		// Check if inventory is set
		if (_Inventory == null)
		{
			GD.PrintErr($"InvItem {Name}: _Inventory is null! Item needs to be added to inventory before _Ready is called.");
			return;
		}
		
		this.Size = _Inventory.inventoryTileSize * invSize;
		MouseFilter = Control.MouseFilterEnum.Stop; // Ensure we receive mouse events
		
		// Ensure itemIcon passes through mouse events
		if (itemIcon != null)
		{
			itemIcon.MouseFilter = Control.MouseFilterEnum.Pass;
		}
		
		// Get references to editor-created nodes
		_numberLabel = GetNodeOrNull<Label>("NumberLabel");
		_stackLabel = GetNodeOrNull<Label>("StackLabel");
		
		// Set icon texture from item definition
		if (itemIcon != null && itemDef != null)
		{
			itemIcon.Texture = itemDef.InvTexture;
			itemIcon.Size = this.Size;
			
			GD.Print($"[InvItem._Ready] Set texture for {itemDef.ItemName}, Size: {this.Size}, Texture: {itemDef.InvTexture?.ResourcePath ?? "null"}, Position: {Position}, Visible: {Visible}");
		}
		else
		{
			GD.PrintErr($"[InvItem._Ready] Missing itemIcon ({itemIcon != null}) or itemDef ({itemDef != null})");
		}
			
		UpdateStackLabel();
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
		if (_numberLabel != null)
		{
			_numberLabel.Text = number > 0 ? number.ToString() : "";
			_numberLabel.Visible = number > 0;
		}
	}

	private void UpdateStackLabel()
	{
		if (_stackLabel == null || itemDef == null)
			return;
			
		// Only show stack count if item is stackable and count > 1
		if (itemDef.MaxStackSize > 1 && stackCount > 1)
		{
			_stackLabel.Text = stackCount.ToString();
			_stackLabel.Visible = true;
		}
		else
		{
			_stackLabel.Visible = false;
		}
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
