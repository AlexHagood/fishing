using Godot;
using System;

public partial class Chat : VBoxContainer
{

	private LineEdit chatEntry;

    private TextEdit chatLog;

    private InputHandler inputHandler;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
        chatEntry = GetNode<LineEdit>("ChatEntry");
		chatLog = GetNode<TextEdit>("ChatLog");

		// Connect the LineEdit's "text_submitted" signal to a method
		chatEntry.TextSubmitted += OnChatMessageSubmitted;

        inputHandler = GetNode<InputHandler>("/root/InputHandler");

        if (inputHandler == null)
        {
            Log("Chat: Could not find InputHandler node at /root/InputHandler");
            return;
        }

        inputHandler.ChatPressed += chatEntry.Edit;
        inputHandler.EscPressed += chatEntry.ReleaseFocus;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
    // Called when ChatManager emits MessageAdded signal
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SendChatMessage(string sender, string message)
    {
        string formattedMessage = $"[{System.DateTime.Now:HH:mm}] {sender}: {message}\n";
        chatLog.Text += formattedMessage;

        // Scroll to bottom to show latest message
        chatLog.ScrollVertical = (int)chatLog.GetVScrollBar().MaxValue;
    }

    // Called when user submits text in chat entry
    private void OnChatMessageSubmitted(string text)
    {
        // Don't send empty messages
        if (string.IsNullOrWhiteSpace(text))
        {
            chatEntry.Text = "";
            return;
        }

        // Add the message to ChatManager (which will emit signal to all GUIs)
        Rpc(nameof(SendChatMessage), Multiplayer.GetUniqueId().ToString(), text);

        // Clear the input field
        chatEntry.Text = "";

        chatEntry.ReleaseFocus();

        inputHandler.CurrentContext = InputHandler.InputContext.Gameplay;
    }
}
