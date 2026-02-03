using Godot;
using System;
using System.Linq;

public partial class NetworkManager : Node3D
{
    
    public const string DEFAULT_SERVER_IP = "localhost";

    public const int SERVER_PORT = 6969;

    public ENetMultiplayerPeer? PeerInstance = null;

    public override void _Ready()
    {
        ParseCommandLineArgs();
    }

    private void ParseCommandLineArgs()
    {
        var args = OS.GetCmdlineArgs();
        
        // Check for --host flag
        if (args.Contains("--host"))
        {
            GD.Print("[NetworkManager] --host flag detected, starting as server");
            StartServer();
            return;
        }
        
        // Check for --client flag
        var clientIndex = Array.IndexOf(args, "--client");
        if (clientIndex >= 0)
        {
            // Get the IP address after --client flag
            string serverIp = DEFAULT_SERVER_IP;
            if (clientIndex + 1 < args.Length)
            {
                serverIp = args[clientIndex + 1];
            }
            GD.Print($"[NetworkManager] --client flag detected, connecting to {serverIp}");
            ConnectToServer(serverIp);
            return;
        }
        
        GD.Print("[NetworkManager] Starting as server by default");
        StartServer();
        
    }

    public void StartServer()
    {
        PeerInstance = new ENetMultiplayerPeer();
        PeerInstance.CreateServer(SERVER_PORT, 32);
        Multiplayer.MultiplayerPeer = PeerInstance;
        GD.Print("[NetworkManager] Server started on port " + SERVER_PORT);
    }

    public void ConnectToServer(string serverIp = DEFAULT_SERVER_IP)
    {
        PeerInstance = new ENetMultiplayerPeer();
        PeerInstance.CreateClient(serverIp, SERVER_PORT);
        Multiplayer.MultiplayerPeer = PeerInstance;
        GD.Print("[NetworkManager] Connecting to server at " + serverIp + ":" + SERVER_PORT);
    }
    
}
