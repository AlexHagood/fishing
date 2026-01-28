using Godot;
using System;

public partial class UIWindow : Control
{
    private Panel _panel;
    private PanelContainer _statusBar;
    private Label _titleLabel;
    private Button _closeButton;
    private PanelContainer _contentContainer;
    
    private bool _isDragging = false;
    private Vector2 _dragOffset;
    
    public override void _Ready()
    {
        // Get references to child nodes created in editor
        _panel = GetNode<Panel>("Panel");
        var vbox = _panel.GetNode<VBoxContainer>("VBoxContainer");
        _statusBar = vbox.GetNode<PanelContainer>("StatusBar");
        _contentContainer = vbox.GetNode<PanelContainer>("Content");
        
        var hbox = _statusBar.GetNode<HBoxContainer>("HBoxContainer");
        _titleLabel = hbox.GetNode<Label>("Label");
        _closeButton = hbox.GetNode<Button>("Button");
        
        _closeButton.Text = "X";
        _closeButton.Pressed += OnClosePressed;
        
        _statusBar.GuiInput += OnStatusBarGuiInput;
        _statusBar.MouseFilter = MouseFilterEnum.Stop;

        // Connect to TreeExiting signal to notify parent when freed
        TreeExiting += OnTreeExiting;

        // Defer resize to next frame so content has time to calculate its size
        CallDeferred(MethodName.ResizeAndCenter);
    }
    
    public override void _Process(double delta)
    {
        if (_isDragging)
        {
            _panel.Position = GetViewport().GetMousePosition() - _dragOffset;
        }
    }
    
    /// <summary>
    /// Create a new window with the given title and content.
    /// The window will be automatically shown and centered.
    /// When closed, it destroys itself.
    /// </summary>
    
    private void OnClosePressed()
    {
        QueueFree();
    }
    
    private void OnTreeExiting()
    {
        // Notify parent Gui that this window is being closed
        var parentGui = GetParent<Gui>();
        if (parentGui != null)
        {
            parentGui.OnWindowClosed(this);
        }
    }
    
    private void ResizeAndCenter()
    {
        // Get content size
        if (_contentContainer.GetChildCount() > 0)
        {
            var content = _contentContainer.GetChild(0) as Control;
            if (content != null)
            {
                
                var contentSize = content.CustomMinimumSize != Vector2.Zero 
                    ? content.CustomMinimumSize 
                    : content.Size;
                
                var statusBarHeight = _statusBar.Size.Y;
                
                var newSize = new Vector2(
                    Mathf.Max(200, contentSize.X),
                    contentSize.Y + statusBarHeight
                );
                
                _panel.CustomMinimumSize = newSize;
                _panel.Size = newSize;

                var viewportSize = GetViewportRect().Size;
                _panel.Position = (viewportSize - newSize) / 2;

                
                // Center window
                _panel.Position = (viewportSize - newSize) / 2;
            }
        }
    }
    
    private void OnStatusBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    // Bring window to front when starting to drag
                    var parent = GetParent();
                    if (parent != null)
                    {
                        parent.MoveChild(this, parent.GetChildCount() - 1);
                    }
                    
                    _isDragging = true;
                    _dragOffset = GetViewport().GetMousePosition() - _panel.Position;
                }
                else
                {
                    _isDragging = false;
                }
            }
        }
    }
}