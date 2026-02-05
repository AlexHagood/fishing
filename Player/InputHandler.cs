using Godot;
using System;

/// <summary>
/// Centralized input handler that manages all game input.
/// Routes input to appropriate systems based on current context.
/// </summary>
public partial class InputHandler : Node
{
    // Input context determines which inputs are active
    public enum InputContext
    {
        Gameplay,    // Normal gameplay - character can move, use tools
        UI,          // Inventory/menu open - only UI inputs allowed
        Paused,
        
        Chatting       // Game paused - minimal inputs
    }

    private InputContext _currentContext = InputContext.Gameplay;
    
    // Signals for gameplay input
    [Signal] public delegate void MovementInputEventHandler(Vector2 direction);
    [Signal] public delegate void JumpPressedEventHandler();
    [Signal] public delegate void SprintPressedEventHandler(bool isPressed);
    [Signal] public delegate void MouseMotionEventHandler(Vector2 relative);
    [Signal] public delegate void MouseClickEventHandler(MouseButton button, bool isPressed);

    [Signal] public delegate void ChatPressedEventHandler();
    
    // Signals for hotbar
    [Signal] public delegate void NumkeyPressedEventHandler(int keyValue);
    [Signal] public delegate void ScrollEventHandler(int direction); // 1 = up, -1 = down
    
    // Signals for UI
    [Signal] public delegate void InventoryToggledEventHandler();
    [Signal] public delegate void ItemRotateRequestedEventHandler();
    
    // Signals for interactions
    [Signal] public delegate void InteractEPressedEventHandler();
    [Signal] public delegate void InteractFPressedEventHandler();
    [Signal] public delegate void CameraToggledEventHandler();


    [Signal] public delegate void EscPressedEventHandler();
    
    public InputContext CurrentContext 
    { 
        get => _currentContext;
        set 
        {
            _currentContext = value;
            GD.Print($"[InputHandler] Context changed to: {value}");
            
            // Update mouse mode based on context
            if (value == InputContext.UI || value == InputContext.Paused)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else if (value == InputContext.Gameplay)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public override void _Ready()
    {
        GD.Print("[InputHandler] Initialized");
        // Start in gameplay mode
        CurrentContext = InputContext.Gameplay;
    }

    public override void _Input(InputEvent @event)
    {
        // Handle context switching first (these work in any context)
        if (Input.IsActionJustPressed("inventory"))
        {
            EmitSignal(SignalName.InventoryToggled);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            if (_currentContext == InputContext.Chatting)
            {
                CurrentContext = InputContext.Gameplay;
            }
            EmitSignal(SignalName.EscPressed);
            GetViewport().SetInputAsHandled();
            return;
        }

        // Route input based on current context
        switch (_currentContext)
        {
            case InputContext.Gameplay:
                HandleGameplayInput(@event);
                break;
            case InputContext.UI:
                HandleUIInput(@event);
                break;
            case InputContext.Paused:
                HandlePausedInput(@event);
                break;
            case InputContext.Chatting:
                HandleChattingInput(@event);
                break;
        }
    }

    public void HandleChattingInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed("ui_close_dialog"))
        {
            EmitSignal(SignalName.ChatPressed);
            CurrentContext = InputContext.Gameplay;
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void HandleGameplayInput(InputEvent @event)
    {
        // Mouse motion for camera
        if (@event is InputEventMouseMotion mouseMotion)
        {
            EmitSignal(SignalName.MouseMotion, mouseMotion.Relative);
            GetViewport().SetInputAsHandled();
            return;
        }

        // Mouse buttons for tools/items
        if (@event is InputEventMouseButton mouseButton)
        {
            // Mouse wheel for hotbar scrolling
            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                EmitSignal(SignalName.Scroll, 1);
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                EmitSignal(SignalName.Scroll, -1);
                GetViewport().SetInputAsHandled();
                return;
            }
            
            // Left/Right click for tool use
            if (mouseButton.ButtonIndex == MouseButton.Left || mouseButton.ButtonIndex == MouseButton.Right)
            {
                EmitSignal(SignalName.MouseClick, (int)mouseButton.ButtonIndex, mouseButton.Pressed);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // Number keys for hotbar selection
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key6)
            {
                int slotIndex = (int)keyEvent.Keycode - (int)Key.Key1;
                EmitSignal(SignalName.NumkeyPressed, slotIndex);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // Action buttons
        if (Input.IsActionJustPressed("interact"))
        {
            EmitSignal(SignalName.InteractEPressed);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Input.IsActionJustPressed("pickup"))
        {
            EmitSignal(SignalName.InteractFPressed);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Input.IsActionJustPressed("camera"))
        {
            EmitSignal(SignalName.CameraToggled);
            GetViewport().SetInputAsHandled();
            return;
        }

        if(Input.IsActionJustPressed("chat"))
        {
            EmitSignal(SignalName.ChatPressed);
            _currentContext = InputContext.Chatting;
            GetViewport().SetInputAsHandled();
            return;
        }
        // Sprint handling (continuous, not just pressed)
        bool isSprinting = Input.IsActionPressed("sprint");
        EmitSignal(SignalName.SprintPressed, isSprinting);
    }

    private void HandleUIInput(InputEvent @event)
    {
        // In UI mode, only allow:
        // - Number keys for hotbar assignment (when hovering over items)
        // - R for rotation
        // - Mouse input (handled by UI nodes directly)
        
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key6)
            {
                int slotIndex = (int)keyEvent.Keycode - (int)Key.Key1;
                EmitSignal(SignalName.NumkeyPressed, slotIndex);
                // Don't consume - let GUI handle it too
                return;
            }
        }

        if (Input.IsActionJustPressed("rotate"))
        {
            EmitSignal(SignalName.ItemRotateRequested);
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void HandlePausedInput(InputEvent @event)
    {
        // In paused mode, only allow unpause input
        // (implement pause menu logic here)
    }

    private void ToggleUIContext()
    {
        if (_currentContext == InputContext.UI)
        {
            CurrentContext = InputContext.Gameplay;
        }
        else
        {
            CurrentContext = InputContext.UI;
        }
    }

    /// <summary>
    /// Get movement input direction (used in PhysicsProcess)
    /// </summary>
    public Vector2 GetMovementInput()
    {
        if (_currentContext != InputContext.Gameplay)
            return Vector2.Zero;

        Vector2 input = Vector2.Zero;
        
        if (Input.IsActionPressed("fwd"))
            input.Y -= 1;
        if (Input.IsActionPressed("back"))
            input.Y += 1;
        if (Input.IsActionPressed("left"))
            input.X -= 1;
        if (Input.IsActionPressed("right"))
            input.X += 1;

        return input.Normalized();
    }

    /// <summary>
    /// Check if jump was just pressed this frame
    /// </summary>
    public bool IsJumpJustPressed()
    {
        return _currentContext == InputContext.Gameplay && Input.IsActionJustPressed("jump");
    }

    /// <summary>
    /// Check if sprint is currently held down
    /// </summary>
    public bool IsSprintPressed()
    {
        return _currentContext == InputContext.Gameplay && Input.IsActionPressed("sprint");
    }
}
