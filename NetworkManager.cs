using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NetworkManager : Node
{
    
    public const string DEFAULT_SERVER_IP = "localhost";

    public const int SERVER_PORT = 6969;

    public ENetMultiplayerPeer? PeerInstance = null;

    public Dictionary<long, Character> IdToPlayer = new Dictionary<long, Character>();

    public override void _Ready()
    {
        ParseCommandLineArgs();

        DisplayServer.WindowSetTitle("Fishing game");
        
        // Server listens for new connections and spawns players
        Multiplayer.PeerConnected += OnPeerConnected;
        
    }

    private void OnPeerDisconnected(long peerId)
    {
        GD.Print($"[NetworkManager {Multiplayer.GetUniqueId()}] Peer {peerId} disconnected");
        GetTree().Quit();
    }
    
    private void OnPeerConnected(long peerId)
    {
        GD.Print($"[NetworkManager {Multiplayer.GetUniqueId()}] Peer {peerId} connected");

        var pnode = GetNode("/root/Main/Players");
        var players = pnode.GetChildren();

        // Only server spawns players
        if (Multiplayer.IsServer())
        {
            CallDeferred(MethodName.SpawnPlayer, peerId);
        }

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
        GD.Print("Servier Peer ID: " + Multiplayer.GetUniqueId());
        CallDeferred("SpawnPlayer", Multiplayer.GetUniqueId());
    }

    public void ConnectToServer(string serverIp = DEFAULT_SERVER_IP)
    {
        PeerInstance = new ENetMultiplayerPeer();
        PeerInstance.CreateClient(serverIp, SERVER_PORT);
        Multiplayer.MultiplayerPeer = PeerInstance;
        GD.Print("[NetworkManager] Connecting to server at " + serverIp + ":" + SERVER_PORT);
    }

    public void SpawnPlayer(long peerId)
    {
        GD.Print($"[NetworkManager] SpawnPlayer called for {peerId}.");
        if (!Multiplayer.IsServer())
        {
            GD.PrintErr("[NetworkManager] SpawnPlayer called on client, ignoring.");
            return;
        }
        GD.Print($"[NetworkManager] Spawning player with peer ID: {peerId}");
        
        // Try to use MultiplayerSpawner if it exists
        var spawner = GetNode<MultiplayerSpawner>("/root/Main/MultiplayerSpawner");

            // Create spawn data
        var spawnData = new Godot.Collections.Dictionary
        {
            { "peer_id", peerId }
        };

        Character player = spawner.Spawn(spawnData) as Character;

        GD.Print($"Spawned player {player.Name}");

        IdToPlayer[peerId] = player;
        
        // Fallback to manual spawning if no spawner exists
        
        GD.Print($"[NetworkManager {Multiplayer.GetUniqueId()}] Spawned player {peerId} on server, authority is {player.GetMultiplayerAuthority()}");
        
        // Use RPC to tell ALL clients (including this one if it's also a client) to set authority
    }
    
    
}
