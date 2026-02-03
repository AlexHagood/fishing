using System.Collections.Generic;
using Godot;

public class ChatMessage
{
    public string Sender;
    public string Message;
    public float Timestamp;

    public ChatMessage(string sender, string message)
    {
        Sender = sender;
        Message = message;
        Timestamp = (float)(System.DateTime.Now - new System.DateTime(1970, 1, 1)).TotalSeconds;
    }
}

public partial class ChatManager : Node
{
    [Signal]
    public delegate void MessageAddedEventHandler(string sender, string message);
    
    private List<ChatMessage> _messages = new List<ChatMessage>();
    private const int MaxMessages = 100;

    public void AddMessage(string sender, string message)
    {
        if (_messages.Count >= MaxMessages)
        {
            _messages.RemoveAt(0); // Remove oldest message
        }
        
        var chatMessage = new ChatMessage(sender, message);
        _messages.Add(chatMessage);
        
        // Emit the signal so all connected GUIs can update
        EmitSignal(SignalName.MessageAdded, sender, message);
        
        // Also print to console for debugging
        GD.Print($"[ChatManager] {sender}: {message}");
    }

    public List<ChatMessage> GetMessages()
    {
        return _messages;
    }
}