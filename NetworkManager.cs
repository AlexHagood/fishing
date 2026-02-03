using Godot;
using System;
using System.Linq;

public partial class NetworkManager : Node
{
    
    public const string DEFAULT_SERVER_IP = "localhost";

    public const int SERVER_PORT = 6969;

    public ENetMultiplayerPeer? PeerInstance = null;

    public override void _Ready()
    {
        ParseCommandLineArgs();
        
        // Server listens for new connections and spawns players
        Multiplayer.PeerConnected += OnPeerConnected;
    }
    
    private void OnPeerConnected(long peerId)
    {
        GD.Print($"[NetworkManager] Peer {peerId} connected");
        
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
        GD.Print("[NetworkManager] SpawnPlayer called.");
        if (!Multiplayer.IsServer())
        {
            GD.PrintErr("[NetworkManager] SpawnPlayer called on client, ignoring.");
            return;
        }
        GD.Print($"[NetworkManager] Spawning player with peer ID: {peerId}");
        
        var scene = ResourceLoader.Load<PackedScene>("res://Player/character.tscn");
        if (scene == null)
        {
            GD.PrintErr($"[NetworkManager] Failed to load scene at path: res://Player/character.tscn");
            return;
        }
        var newPlayer = scene.Instantiate<Character>();

        newPlayer.Name = peerId.ToString();

        // Add to Players node
        var playersNode = GetNode("/root/Main/Players");
        playersNode.AddChild(newPlayer, true);
        
        // Set authority on server AFTER adding to tree
        newPlayer.SetMultiplayerAuthority((int)peerId);
        
        GD.Print($"[NetworkManager] Spawned player {peerId} on server, authority is {newPlayer.GetMultiplayerAuthority()}");
        
        // Use RPC to tell ALL clients (including this one if it's also a client) to set authority
        Rpc(MethodName.ClientSetPlayerAuthority, peerId.ToString(), (int)peerId);
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientSetPlayerAuthority(string playerName, int authorityPeerId)
    {
        GD.Print($"[NetworkManager] ClientSetPlayerAuthority RPC: {playerName} -> {authorityPeerId} (I am peer {Multiplayer.GetUniqueId()})");
        
        var playersNode = GetNodeOrNull("/root/Main/Players");
        if (playersNode == null)
        {
            GD.PrintErr("[NetworkManager] Players node not found!");
            return;
        }
        
        var player = playersNode.GetNodeOrNull<Character>(playerName);
        if (player == null)
        {
            GD.PrintErr($"[NetworkManager] Player {playerName} not found!");
            return;
        }
        
        player.SetMultiplayerAuthority(authorityPeerId);
        GD.Print($"[NetworkManager] Set authority for {playerName} to {authorityPeerId}. IsAuthority: {player.IsMultiplayerAuthority()}");
    }
    
}
