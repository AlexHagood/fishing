using Godot;
using System;

public partial class UIWindow : Control
{
    [Signal]
    public delegate void WindowClosedEventHandler();
    
    [Export] public string WindowTitle { get; set; } = "Window";
    
    private Panel _panel;
    private PanelContainer _statusBar;
    private Label _titleLabel;
    private Button _closeButton;
    private PanelContainer _contentContainer;
    
    private bool _isDragging = false;
    private Vector2 _dragOffset;
    
    public override void _Ready()
    {
        // Get references to child nodes
        _panel = GetNode<Panel>("Panel");
        var vbox = _panel.GetNode<VBoxContainer>("VBoxContainer");
        _statusBar = vbox.GetNode<PanelContainer>("StatusBar");
        _contentContainer = vbox.GetNode<PanelContainer>("Content");
        
        var hbox = _statusBar.GetNode<HBoxContainer>("HBoxContainer");
        _titleLabel = hbox.GetNode<Label>("Label");
        _closeButton = hbox.GetNode<Button>("Button");
        
        // Set title
        _titleLabel.Text = WindowTitle;
        
        // Set close button text
        _closeButton.Text = "X";
        
        // Connect signals
        _closeButton.Pressed += OnClosePressed;
        
        // Make the status bar handle drag events
        _statusBar.GuiInput += OnStatusBarGuiInput;
        
        // Initially hide the window
        Visible = false;
        
        // Set mouse filter for dragging
        _statusBar.MouseFilter = MouseFilterEnum.Pass;
    }
    
    private void OnStatusBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    // Start dragging
                    _isDragging = true;
                    _dragOffset = mouseButton.Position;
                }
                else
                {
                    // Stop dragging
                    _isDragging = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            // Update panel position while dragging
            var newPos = _panel.Position + mouseMotion.Relative;
            
            // Keep window within viewport bounds
            var viewportSize = GetViewportRect().Size;
            var panelSize = _panel.Size;
            
            newPos.X = Mathf.Clamp(newPos.X, 0, viewportSize.X - panelSize.X);
            newPos.Y = Mathf.Clamp(newPos.Y, 0, viewportSize.Y - panelSize.Y);
            
            _panel.Position = newPos;
        }
    }
    
    private void OnClosePressed()
    {
        EmitSignal(SignalName.WindowClosed);
        Hide();
    }
    
    public new void Show()
    {
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
    
    public new void Hide()
    {
        Visible = false;
        // Don't change mouse mode here - let the parent GUI handle it
    }
    
    public void Toggle()
    {
        if (Visible)
            Hide();
        else
            Show();
    }
    
    // Add content to the window
    public void SetContent(Control content)
    {
        // Clear existing content
        foreach (Node child in _contentContainer.GetChildren())
        {
            _contentContainer.RemoveChild(child);
        }
        
        // Add new content
        if (content != null)
        {
            _contentContainer.AddChild(content);
            
            // Resize panel to fit content
            ResizeToContent();
        }
    }
    
    private void ResizeToContent()
    {
        // Force update layout
        CallDeferred(MethodName.UpdatePanelSize);
    }
    
    private void UpdatePanelSize()
    {
        // Get content size
        if (_contentContainer.GetChildCount() > 0)
        {
            var content = _contentContainer.GetChild(0) as Control;
            if (content != null)
            {
                // Force content to update its size first
                content.UpdateMinimumSize();
                
                // Wait one frame for layout to update
                var contentSize = content.CustomMinimumSize != Vector2.Zero 
                    ? content.CustomMinimumSize 
                    : content.Size;
                
                var statusBarHeight = _statusBar.Size.Y;
                
                // Add padding for status bar, borders, and container margins
                var padding = 40; // Adjust based on your theme
                var newSize = new Vector2(
                    Mathf.Max(200, contentSize.X + padding),
                    contentSize.Y + statusBarHeight + padding
                );
                
                _panel.CustomMinimumSize = newSize;
                _panel.Size = newSize;
                
                // Force layout update
                _panel.UpdateMinimumSize();
                
                // Center the window if it's the first time
                if (_panel.Position == Vector2.Zero)
                {
                    CenterWindow();
                }
            }
        }
    }
    
    private void CenterWindow()
    {
        var viewportSize = GetViewportRect().Size;
        var panelSize = _panel.Size;
        _panel.Position = (viewportSize - panelSize) / 2;
    }
}
